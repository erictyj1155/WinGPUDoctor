using System.Text;
using System.Text.Json;
using WinGPUDoctor.Core;
using WinGPUDoctor.Protocol;
using WinGPUDoctor.Supervisor;
using WinGPUDoctor.Windows;
using WinGPUDoctor.Worker;
using Xunit;

namespace WinGPUDoctor.Tests;

public class IntegratedBoundaryTests
{
    private static readonly CollectionTimingPolicy Policy = new(TimeSpan.FromMinutes(2), TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    private static WmiRow Row(params (string Key, string? Value)[] fields) => new(fields.ToDictionary(f => f.Key, f => f.Value));
    private static WmiRow Video(string? identity, string name = "Example GPU") => Row(("PNPDeviceID", identity), ("Name", name));
    private static WmiRow Driver(string? identity, string provider = "Example Provider") => Row(("DeviceID", identity),
        ("DriverProviderName", provider), ("DriverVersion", "1.2"), ("DriverDate", "20260821000000.******+***"));
    private static Reader Sources() => new()
    {
        [WmiQuery.OperatingSystem] = WmiResult.Success(Row(("Version", "10.0.26200"), ("BuildNumber", "26200"))),
        [WmiQuery.ComputerSystem] = WmiResult.Success(Row(("Manufacturer", "Example OEM"), ("Model", "Example Model"))),
        [WmiQuery.VideoControllers] = WmiResult.Success(Video(TopologyTests.RawInstanceA)),
        [WmiQuery.DisplayDrivers] = WmiResult.Success(Driver(TopologyTests.RawInstanceA.ToLowerInvariant()), Driver("PRIVATE_UNMATCHED_DRIVER"))
    };
    private static DisplayTopologyCollector Topology(TopologyTests.FakeApi api) => new(api, api, DisplayQueryMode.VirtualModeAndRefreshAware);
    private static Task<CollectionSnapshot> Collect(WireFactory factory) =>
        new SupervisedWindowsCollector(Policy, TimeProvider.System, factory, new HostAdmission()).CollectAsync();
    private static string Graph(CollectionSnapshot snapshot) => JsonSerializer.Serialize(snapshot, ReportWriter.JsonOptions);
    private static void Equivalent(CollectionSnapshot old, CollectionSnapshot current) => Assert.Equal(Graph(old), Graph(current));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public async Task MissingIdentitiesPreserveRowsAndOldSemanticsAcrossWire(string? missing)
    {
        var reader = Sources();
        reader[WmiQuery.VideoControllers] = WmiResult.Success(Video(missing, "Unjoinable GPU"), Video(TopologyTests.RawInstanceA));
        reader[WmiQuery.DisplayDrivers] = WmiResult.Success(Driver(missing), Driver(TopologyTests.RawInstanceA.ToLowerInvariant()));
        var old = new WindowsCollector(reader, Topology(TopologyTests.Single())).Collect();
        var factory = new WireFactory(reader, TopologyTests.Single());
        var current = await Collect(factory);
        Equivalent(old, current);
        Assert.Equal(2, current.Facts.Gpus.Value!.Count);
        Assert.Equal("Unjoinable GPU", current.Facts.Gpus.Value[0].Name.Value);
        Assert.Equal(ReasonCode.NoMatchingDriver, current.Facts.Gpus.Value[0].Driver.Provider.Reason);
        Assert.Equal("Example Provider", current.Facts.Gpus.Value[1].Driver.Provider.Value);
        Assert.Null(factory.TopologyInput!.Inventory[0].TransientInstanceId);
        Assert.Equal("gpu-2", current.Facts.Displays.Value![0].SourceAdapter.Value!.GpuId);
    }

    [Theory]
    [InlineData("available")]
    [InlineData("missing")]
    [InlineData("failure")]
    public async Task SystemMappingMatchesOldPath(string scenario)
    {
        var reader = Sources();
        if (scenario == "missing")
        {
            reader[WmiQuery.OperatingSystem] = WmiResult.Success(Row(("Version", " "), ("BuildNumber", null)));
            reader[WmiQuery.ComputerSystem] = WmiResult.Success(Row(("Manufacturer", ""), ("Model", null)));
        }
        if (scenario == "failure")
        {
            reader[WmiQuery.OperatingSystem] = new(DataState.Failed, [], ReasonCode.AccessDenied);
            reader[WmiQuery.ComputerSystem] = new(DataState.Failed, [], ReasonCode.ProviderUnavailable);
        }
        Equivalent(new WindowsCollector(reader, Topology(TopologyTests.Single())).Collect(),
            await Collect(new(reader, TopologyTests.Single())));
    }

    [Theory]
    [InlineData("matched")]
    [InlineData("unmatched")]
    [InlineData("optional-name")]
    [InlineData("target-failed")]
    [InlineData("target-unsupported")]
    [InlineData("partial")]
    public async Task TopologyAndDriverFactsMatchOldPath(string scenario)
    {
        var reader = Sources();
        var api = TopologyTests.Single();
        switch (scenario)
        {
            case "unmatched": reader[WmiQuery.VideoControllers] = WmiResult.Success(Video(null)); break;
            case "optional-name": api.EmptyFriendly = true; break;
            case "target-failed": api.TargetError = 5; break;
            case "target-unsupported": api.TargetError = 50; break;
            case "partial": api.SourceError = 5; break;
        }
        var old = new WindowsCollector(reader, Topology(api)).Collect();
        var current = await Collect(new(reader, api));
        Equivalent(old, current);
        if (scenario == "optional-name") Assert.Equal(CollectorStatus.Succeeded, current.Collection[3].Status);
        if (scenario.StartsWith("target-", StringComparison.Ordinal)) Assert.Equal(CollectorStatus.Partial, current.Collection[3].Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DriverProviderFailureOrSupervisorTimeoutPreservesInventory(bool supervisorTimeout)
    {
        var reader = Sources();
        reader[WmiQuery.DisplayDrivers] = new(DataState.Failed, [], ReasonCode.Timeout);
        var old = new WindowsCollector(reader, Topology(TopologyTests.Single())).Collect();
        var factory = new WireFactory(reader, TopologyTests.Single());
        if (supervisorTimeout) factory.TimeoutAfterAttempts[WorkerOperation.WmiDisplayDrivers] = 1;
        var snapshot = await Collect(factory);
        Equivalent(old, snapshot);
        Assert.Equal(DataState.Available, snapshot.Facts.Gpus.State);
        Assert.Equal(ReasonCode.Timeout, snapshot.Facts.Gpus.Value![0].Driver.Provider.Reason);
    }

    [Fact]
    public async Task PrivateIdentifiersNeverEnterCoreBeforeProjectionOrEitherExport()
    {
        var snapshot = await Collect(new(Sources(), TopologyTests.Single()));
        var prepared = PrivacyPolicy.Prepare(snapshot, new(2026, 9, 21));
        var representations = new[] { Graph(snapshot), ReportWriter.Json(prepared), ReportWriter.Markdown(prepared) };
        foreach (var text in representations)
            foreach (var marker in new[] { "PRIVATE_ADAPTER_INSTANCE", "PRIVATE_UNMATCHED_DRIVER", "PRIVATE_EDID_MONITOR",
                "SERIAL_ABC123", "99887766", "11335577", "54321", "45678", "987654", "adapterDevicePath", "monitorDevicePath", "transientInstanceId" })
                Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("gpu-1", snapshot.Facts.Displays.Value![0].SourceAdapter.Value!.GpuId);
    }

    [Fact]
    public async Task SyntheticExampleFixtureUsesProductionProvenanceAndCollectorOrder()
    {
        // The published examples are generated from TopologyTests.WithTopology. Their values are synthetic,
        // but per-field provenance and collector order must match the supervised production path.
        static DiagnosticReport Published(CollectionSnapshot snapshot) => JsonSerializer.Deserialize<DiagnosticReport>(
            ReportWriter.Json(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 10))), ReportWriter.JsonOptions)!;
        static DataSource[] SystemSources(CollectedFacts facts) =>
            [facts.System.WindowsVersion.Source, facts.System.WindowsBuild.Source, facts.System.Manufacturer.Source, facts.System.Model.Source];
        static DataSource[] GpuSources(GpuFacts gpu) => [gpu.Name.Source, gpu.PciVendorId.Source, gpu.PciDeviceId.Source,
            gpu.Classification.Source, gpu.Driver.Provider.Source, gpu.Driver.Version.Source, gpu.Driver.Date.Source];
        var production = Published(await Collect(new(Sources(), TopologyTests.Single(true))));
        var example = Published(TopologyTests.WithTopology(TopologyTests.Collect(TopologyTests.Single(true))));
        Assert.Equal(SystemSources(production.Facts), SystemSources(example.Facts));
        Assert.Equal(production.Facts.Gpus.Source, example.Facts.Gpus.Source);
        var expectedGpu = GpuSources(Assert.Single(production.Facts.Gpus.Value!));
        Assert.All(example.Facts.Gpus.Value!, gpu => Assert.Equal(expectedGpu, GpuSources(gpu)));
        Assert.Equal(production.Facts.Displays.Source, example.Facts.Displays.Source);
        Assert.Equal(production.Collection.Select(run => run.Source), example.Collection.Select(run => run.Source));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task ActualRetryBoundariesEmitMatchingMarkers(int attempts)
    {
        var api = TopologyTests.Single();
        for (var i = 1; i < attempts; i++) api.QueryErrors.Enqueue(Ccd.InsufficientBuffer);
        var factory = new WireFactory(Sources(), api);
        factory.OnMarker = (operation, attempt) =>
        {
            if (operation == WorkerOperation.DisplayActiveTopology) Assert.Equal(attempt - 1, api.SizeCalls);
        };
        var snapshot = await Collect(factory);
        Assert.Equal(Enumerable.Range(1, attempts), factory.Markers[WorkerOperation.DisplayActiveTopology]);
        Assert.Equal(attempts, snapshot.Collection[3].Attempts);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 1)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    public async Task ParentTimeoutRetainsOnlyObservedAttemptMarkers(int operationValue, int observed)
    {
        var operation = (WorkerOperation)operationValue;
        var reader = Sources();
        var api = TopologyTests.Single();
        api.QueryErrors.Enqueue(Ccd.InsufficientBuffer);
        var factory = new WireFactory(reader, api);
        factory.TimeoutAfterAttempts[operation] = observed;
        reader.OnRead = query => Assert.Contains(1, factory.Markers[(WorkerOperation)query]);
        var snapshot = await Collect(factory);
        var source = operation == WorkerOperation.DisplayActiveTopology ? DataSource.DisplayConfig : DataSource.WmiOperatingSystem;
        var run = snapshot.Collection.Single(r => r.Source == source);
        Assert.Equal(ReasonCode.Timeout, run.Reason);
        Assert.Equal(observed, run.Attempts);
        Assert.Empty(run.Issues);
        if (observed == 0) Assert.DoesNotContain(WmiQuery.OperatingSystem, reader.Calls);
    }

    [Fact]
    public void FailedAttemptNotificationDoesNotEnterNativeApiOrInventNativeFailure()
    {
        var api = TopologyTests.Single();
        Assert.Throws<IOException>(() => Topology(api).Collect([], _ => throw new IOException("synthetic transport failure")));
        Assert.Equal(0, api.SizeCalls);
    }

    [Theory]
    [InlineData("video-name")]
    [InlineData("driver-provider")]
    [InlineData("video-count")]
    [InlineData("topology-name")]
    public async Task OversizedOutputsBecomeBoundedResourceLimitWithoutRawContent(string scenario)
    {
        var reader = Sources();
        var api = TopologyTests.Single();
        var huge = "PRIVATE_OVERSIZED_" + new string('x', ProtocolConstants.MaxDecodedStringChars);
        var operation = WorkerOperation.WmiVideoControllers;
        if (scenario == "video-name") reader[WmiQuery.VideoControllers] = WmiResult.Success(Video(TopologyTests.RawInstanceA, huge));
        if (scenario == "driver-provider")
        {
            operation = WorkerOperation.WmiDisplayDrivers;
            reader[WmiQuery.DisplayDrivers] = WmiResult.Success(Driver(TopologyTests.RawInstanceA, huge));
        }
        if (scenario == "video-count") reader[WmiQuery.VideoControllers] = WmiResult.Success(
            Enumerable.Repeat(Video(null), ProtocolConstants.MaxCollectionItems + 1).ToArray());
        if (scenario == "topology-name") { operation = WorkerOperation.DisplayActiveTopology; api.FriendlyName = huge; }
        var factory = new WireFactory(reader, api);
        var snapshot = await Collect(factory);
        var source = operation switch
        {
            WorkerOperation.WmiDisplayDrivers => DataSource.WmiSignedDriver,
            WorkerOperation.DisplayActiveTopology => DataSource.DisplayConfig,
            _ => DataSource.WmiVideoController
        };
        var run = snapshot.Collection.Single(r => r.Source == source);
        Assert.Equal(ReasonCode.ResourceLimit, run.Reason);
        Assert.Equal(1, run.Attempts);
        Assert.Empty(run.Issues);
        var failure = Assert.Single(factory.Sent[operation], b => Decode(b) is ResourceLimitFrame);
        Assert.True(failure.Length < ProtocolConstants.MaxControlFrameBytes);
        foreach (var text in new[] { Encoding.UTF8.GetString(failure), Graph(snapshot),
            ReportWriter.Json(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 21))),
            ReportWriter.Markdown(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 21))) })
            Assert.DoesNotContain("PRIVATE_OVERSIZED_", text);
    }

    [Fact]
    public void BrokenResourceLimitWriteDoesNotRetryOrEncodeOffendingDataAgain()
    {
        var reader = Sources();
        reader[WmiQuery.VideoControllers] = WmiResult.Success(Video(null, new string('x', 16385)));
        var sends = 0;
        Assert.Throws<IOException>(() => WorkerOperationRunner.Execute(new(reader), WorkerOperation.WmiVideoControllers,
            new(null), (frame, _) => { sends++; if (frame is ResourceLimitFrame) throw new IOException(); }));
        Assert.Equal(2, sends); // One attempt marker, one bounded terminal write, no retry.
    }

    [Fact]
    public async Task UndeliveredResourceLimitDoesNotOverrideParentTimeout()
    {
        var reader = Sources();
        reader[WmiQuery.VideoControllers] = WmiResult.Success(Video(null, new string('x', 16385)));
        var factory = new WireFactory(reader, TopologyTests.Single());
        factory.TimeoutAfterAttempts[WorkerOperation.WmiVideoControllers] = 1;
        var snapshot = await Collect(factory);
        Assert.Contains(factory.Sent[WorkerOperation.WmiVideoControllers], bytes => Decode(bytes) is ResourceLimitFrame);
        Assert.Equal(ReasonCode.Timeout, snapshot.Facts.Gpus.Reason);
        Assert.DoesNotContain(WorkerOperation.WmiDisplayDrivers, factory.Calls);
    }

    [Theory]
    [InlineData("wmi-timeout")]
    [InlineData("topology-timeout")]
    [InlineData("omitted-driver")]
    [InlineData("zero-attempt")]
    [InlineData("partial-unmatched")]
    [InlineData("optional-name")]
    public async Task FullPathSchemaScenarios(string scenario)
    {
        var reader = Sources(); var api = TopologyTests.Single();
        if (scenario == "omitted-driver") reader[WmiQuery.VideoControllers] = WmiResult.Success();
        if (scenario == "partial-unmatched") reader[WmiQuery.VideoControllers] = WmiResult.Success(Video(null));
        if (scenario == "optional-name") api.EmptyFriendly = true;
        var factory = new WireFactory(reader, api);
        if (scenario == "wmi-timeout") factory.TimeoutAfterAttempts[WorkerOperation.WmiOperatingSystem] = 1;
        if (scenario == "topology-timeout") factory.TimeoutAfterAttempts[WorkerOperation.DisplayActiveTopology] = 1;
        if (scenario == "zero-attempt") factory.TimeoutAfterAttempts[WorkerOperation.WmiOperatingSystem] = 0;
        var snapshot = await Collect(factory);
        if (scenario == "omitted-driver") Assert.DoesNotContain(WorkerOperation.WmiDisplayDrivers, factory.Calls);
        var json = ReportWriter.Json(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 21)));
        var directory = Environment.GetEnvironmentVariable("WINGPUDOCTOR_M4_SCHEMA_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, scenario + ".json"), json, new UTF8Encoding(false));
        }
    }

    private static ProtocolFrame Decode(byte[] bytes) =>
        ProtocolFraming.ReadAsync(new MemoryStream(bytes), default).AsTask().GetAwaiter().GetResult();

    private sealed class Reader : Dictionary<WmiQuery, WmiResult>, IWmiReader
    {
        internal Action<WmiQuery>? OnRead;
        internal List<WmiQuery> Calls { get; } = [];
        public WmiResult Read(WmiQuery query)
        {
            OnRead?.Invoke(query); Calls.Add(query);
            return this[query];
        }
    }

    // Uses real dispatch, real topology mapping, production result encoding, framing/decoding and
    // the production parent. Only providers, transport delivery and cleanup ownership are synthetic.
    private sealed class WireFactory(Reader reader, TopologyTests.FakeApi api) : IWorkerSessionFactory
    {
        internal Dictionary<WorkerOperation, int> TimeoutAfterAttempts { get; } = [];
        internal Dictionary<WorkerOperation, List<int>> Markers { get; } = [];
        internal Dictionary<WorkerOperation, List<byte[]>> Sent { get; } = [];
        internal List<WorkerOperation> Calls { get; } = [];
        internal DisplayActiveTopologyRequest? TopologyInput;
        internal Action<WorkerOperation, int>? OnMarker;
        public ValueTask<IWorkerSession> CreateAsync(WorkerOperation operation, WorkerLaunchOptions? options,
            Deadline connectDeadline, Deadline cleanupLimit, TimeSpan cleanupAllowance, HostAdmission admission, CancellationToken token)
        {
            Calls.Add(operation); Markers[operation] = []; Sent[operation] = [];
            return ValueTask.FromResult<IWorkerSession>(new Session(this, operation, new(reader,
                (inventory, progress) => Topology(api).Collect(inventory, progress))));
        }
        private sealed class Session(WireFactory owner, WorkerOperation operation, WorkerOperationDispatcher dispatcher) : IWorkerSession
        {
            private readonly Queue<byte[]> _wire = [];
            private readonly ProtocolSequenceValidator _workerSequence = new();
            private bool _started;
            private int _observed;
            public bool IsInCreationTimeJob => true;
            public bool IsElevated => false;
            public WorkerBuildIdentity ExpectedIdentity => ProtocolTests.Identity;
            public ValueTask SendAsync(ProtocolFrame frame, Deadline deadline, CancellationToken token)
            {
                var received = Decode(ProtocolFraming.Encode(frame));
                _workerSequence.Record(received);
                if (received is RequestFrame)
                {
                    var ready = new ReadyFrame(operation, ExpectedIdentity);
                    _workerSequence.Record(ready); _wire.Enqueue(ProtocolFraming.Encode(ready));
                }
                if (received is StartFrame start)
                {
                    _started = true;
                    if (operation == WorkerOperation.DisplayActiveTopology) owner.TopologyInput = start.Payload.DisplayActiveTopology;
                    if (owner.TimeoutAfterAttempts.GetValueOrDefault(operation, -1) == 0) return ValueTask.CompletedTask;
                    WorkerOperationRunner.Execute(dispatcher, operation, start.Payload, (outgoing, bytes) =>
                    {
                        _workerSequence.Record(outgoing);
                        if (outgoing is AttemptStartedFrame marker)
                        {
                            owner.OnMarker?.Invoke(operation, marker.Attempt);
                            owner.Markers[operation].Add(marker.Attempt);
                        }
                        owner.Sent[operation].Add(bytes); _wire.Enqueue(bytes);
                    });
                }
                return ValueTask.CompletedTask;
            }
            public ValueTask<ProtocolFrame> ReceiveAsync(Deadline deadline, CancellationToken token)
            {
                if (_started && owner.TimeoutAfterAttempts.TryGetValue(operation, out var count) && _observed >= count)
                    throw new TimeoutException(); // Controlled delivery cutoff; no real provider hang or delay.
                var frame = Decode(_wire.Dequeue());
                if (frame is AttemptStartedFrame) _observed++;
                return ValueTask.FromResult(frame);
            }
            public ValueTask<bool> CleanupAsync(Deadline deadline) => ValueTask.FromResult(true);
            public void Dispose() { }
        }
    }
}
