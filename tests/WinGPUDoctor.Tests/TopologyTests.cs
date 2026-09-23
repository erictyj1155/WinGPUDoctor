using System.Runtime.InteropServices;
using System.Text.Json;
using WinGPUDoctor.Core;
using WinGPUDoctor.Windows;
using Xunit;

namespace WinGPUDoctor.Tests;

public class TopologyTests
{
    internal static readonly Luid AdapterA = new(99887766, 11335577), AdapterB = new(77665544, 22446688);
    internal const string RawPathA = @"\\?\PCI#VEN_1234&DEV_5678#PRIVATE_ADAPTER_INSTANCE#{12345678-1234-1234-1234-123456789abc}";
    internal const string RawInstanceA = @"PCI\VEN_1234&DEV_5678\PRIVATE_ADAPTER_INSTANCE";
    internal const string RawMonitor = @"\\?\DISPLAY#PRIVATE_EDID_MONITOR#SERIAL_ABC123";
    internal static readonly GpuCorrelationIdentity[] Inventory = [new("gpu-1", "OTHER-DEVICE"), new("gpu-2", RawInstanceA)];

    internal sealed class FakeApi : IDisplayConfigApi, IAdapterInstanceResolver
    {
        internal NativePath[] Paths = [];
        internal NativeMode[] Modes = [];
        internal int SizeError, SourceError, TargetError, AdapterError, ResolverError;
        internal bool ThrowUnavailable, ThrowSource, EmptyFriendly;
        internal string FriendlyName = "Example Panel";
        internal uint? SizePathOverride, SizeModeOverride, ReturnedPathCountOverride, ReturnedModeCountOverride;
        internal int SizeCalls, QueryCalls, AdapterCalls, ResolverCalls;
        internal uint LastFlags;
        internal Queue<int> QueryErrors { get; } = new();
        internal Queue<uint> SizePathSequence { get; } = new();
        internal Queue<uint> SizeModeSequence { get; } = new();
        internal HashSet<uint> TargetFailureIds { get; } = [];
        internal Dictionary<Luid, string> AdapterPaths { get; } = new() { [AdapterA] = RawPathA, [AdapterB] = "OTHER_INTERFACE" };
        internal Dictionary<string, string> InstanceIds { get; } = new() { [RawPathA] = RawInstanceA, ["OTHER_INTERFACE"] = "OTHER-DEVICE" };
        public int GetBufferSizes(uint flags, out uint paths, out uint modes)
        {
            SizeCalls++; LastFlags = flags;
            if (ThrowUnavailable) throw new EntryPointNotFoundException("PRIVATE_ERROR_PATH");
            paths = SizePathOverride ?? (SizePathSequence.Count > 0 ? SizePathSequence.Dequeue() : (uint)Paths.Length);
            modes = SizeModeOverride ?? (SizeModeSequence.Count > 0 ? SizeModeSequence.Dequeue() : (uint)Modes.Length);
            return SizeError;
        }
        public int Query(uint flags, ref uint paths, NativePath[] pathArray, ref uint modes, NativeMode[] modeArray)
        {
            QueryCalls++; LastFlags = flags;
            var error = QueryErrors.Count > 0 ? QueryErrors.Dequeue() : 0;
            if (error != 0) return error;
            Array.Copy(Paths, pathArray, Math.Min(Paths.Length, pathArray.Length));
            Array.Copy(Modes, modeArray, Math.Min(Modes.Length, modeArray.Length));
            paths = ReturnedPathCountOverride ?? (uint)Paths.Length;
            modes = ReturnedModeCountOverride ?? (uint)Modes.Length;
            return 0;
        }
        public NativeResult<string> SourceName(Luid adapter, uint source)
        {
            if (ThrowSource) throw new InvalidOperationException("PRIVATE_ERROR_PATH");
            return new(SourceError, @"\\.\DISPLAY" + (source + 1));
        }
        public NativeResult<NativeTargetName> TargetName(Luid adapter, uint target) => new(
            TargetFailureIds.Count > 0 && !TargetFailureIds.Contains(target) ? 0 : TargetError,
            new() { FriendlyName = EmptyFriendly ? "" : FriendlyName, MonitorDevicePath = RawMonitor, EdidManufacturer = 54321, EdidProduct = 45678, ConnectorInstance = 987654 });
        public NativeResult<string> AdapterName(Luid adapter) { AdapterCalls++; return new(AdapterError, AdapterPaths.GetValueOrDefault(adapter, "")); }
        public NativeResult<string> Resolve(string path) { ResolverCalls++; return new(ResolverError, InstanceIds.GetValueOrDefault(path, "")); }
    }
    internal static NativePath Path(Luid adapter, uint source, uint target, uint sourceMode, uint targetMode, bool virtualMode = false) => new()
    {
        Source = new() { AdapterId = adapter, Id = source, ModeIndex = virtualMode ? sourceMode << 16 | 0xffff : sourceMode },
        Target = new() { AdapterId = adapter, Id = target, ModeIndex = virtualMode ? targetMode << 16 | 0xffff : targetMode,
            OutputTechnology = 11, Rotation = 1, TargetAvailable = 1, ScanLineOrdering = 1, RefreshRate = new() { Numerator = 165, Denominator = 1 } },
        Flags = Ccd.PathActive | (virtualMode ? Ccd.PathVirtualMode : 0)
    };
    internal static NativeMode SourceMode(Luid adapter, uint id) => new() { InfoType = 1, Id = id, AdapterId = adapter, SourceMode = new() { Width = 2560, Height = 1600 } };
    internal static NativeMode TargetMode(Luid adapter, uint id) => new() { InfoType = 2, Id = id, AdapterId = adapter,
        TargetSignal = new() { VSync = new() { Numerator = 165000, Denominator = 1000 }, ScanLineOrdering = 1 } };
    internal static FakeApi Single(bool virtualMode = false) => new()
    {
        Paths = [Path(AdapterA, 7, 13, 0, 1, virtualMode)], Modes = [SourceMode(AdapterA, 7), TargetMode(AdapterA, 13)]
    };
    internal static TopologyResult Collect(FakeApi fake, GpuCorrelationIdentity[]? inventory = null, DisplayQueryMode mode = DisplayQueryMode.VirtualModeAndRefreshAware) =>
        new DisplayTopologyCollector(fake, fake, mode).Collect(inventory ?? Inventory);

    [Fact] public void NativeLayoutsMatchWindowsSdk()
    {
        Assert.True(DisplayConfigApi.LayoutsValid);
        Assert.Equal(20, Marshal.SizeOf<NativeSourceMode>());
        Assert.Equal(40, Marshal.SizeOf<NativeDesktopImage>());
        Assert.Equal(20, Marshal.OffsetOf<NativePath>(nameof(NativePath.Target)).ToInt32());
        Assert.Equal(68, Marshal.OffsetOf<NativePath>(nameof(NativePath.Flags)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<NativeMode>(nameof(NativeMode.SourceMode)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<NativeMode>(nameof(NativeMode.TargetSignal)).ToInt32());
        Assert.Equal(36, Marshal.OffsetOf<NativeTargetName>(nameof(NativeTargetName.FriendlyName)).ToInt32());
        Assert.Equal(164, Marshal.OffsetOf<NativeTargetName>(nameof(NativeTargetName.MonitorDevicePath)).ToInt32());
        Assert.Equal(IntPtr.Size == 8 ? 32 : 28, Marshal.SizeOf<SetupApiAdapterResolver.DeviceInfoData>());
        Assert.Equal(IntPtr.Size == 8 ? 32 : 28, Marshal.SizeOf<SetupApiAdapterResolver.DeviceInterfaceData>());
    }
    [Fact] public void BinarySdkFixtureDecodesUnionAtDocumentedOffsets()
    {
        var bytes = new byte[64];
        BitConverter.GetBytes(2u).CopyTo(bytes, 0); // DISPLAYCONFIG_MODE_INFO_TYPE_TARGET
        BitConverter.GetBytes(13u).CopyTo(bytes, 4);
        BitConverter.GetBytes(AdapterA.LowPart).CopyTo(bytes, 8);
        BitConverter.GetBytes(AdapterA.HighPart).CopyTo(bytes, 12);
        BitConverter.GetBytes(60000u).CopyTo(bytes, 32); // union + signal.vSyncFreq
        BitConverter.GetBytes(1001u).CopyTo(bytes, 36);
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            var mode = Marshal.PtrToStructure<NativeMode>(ptr);
            Assert.Equal(AdapterA, mode.AdapterId);
            Assert.Equal(60000u, mode.TargetSignal.VSync.Numerator);
            Assert.Equal(1001u, mode.TargetSignal.VSync.Denominator);
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void OnePathMapsToExactInventoryEntryRatherThanEnumerationOrder(bool virtualMode)
    {
        var fake = Single(virtualMode);
        var result = Collect(fake);
        var d = Assert.Single(result.Displays.Value!);
        Assert.Equal(CollectorStatus.Succeeded, result.Run.Status);
        Assert.Equal("gpu-2", d.SourceAdapter.Value!.GpuId);
        Assert.Equal(AdapterMatchEvidence.ExactSetupApiInstanceId, d.SourceAdapter.Value.Evidence);
        Assert.Equal(AdapterMatchConfidence.Exact, d.SourceAdapter.Value.Confidence);
        Assert.Equal(d.SourceAdapterId, d.TargetAdapterId);
        Assert.Equal(new PixelSize(2560, 1600), d.SourceResolution.Value);
        Assert.Equal(1, fake.ResolverCalls);
        Assert.Equal((uint)0x52, fake.LastFlags);
    }
    [Fact] public void OneAdapterOneDisplayWorks()
    {
        var d = Assert.Single(Collect(Single(), [new("gpu-1", RawInstanceA)]).Displays.Value!);
        Assert.Equal("gpu-1", d.TargetAdapter.Value!.GpuId);
    }
    [Fact] public void ExactJoinIsCaseInsensitive()
    {
        var fake = Single(); fake.InstanceIds[RawPathA] = RawInstanceA.ToLowerInvariant();
        Assert.Equal("gpu-2", Collect(fake).Displays.Value![0].TargetAdapter.Value!.GpuId);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void MultipleTargetsRetainCloneOrExtendedSourceRelationships(bool clone)
    {
        var fake = Single();
        var secondSource = clone ? 7u : 8u;
        fake.Paths = [fake.Paths[0], Path(AdapterA, secondSource, 14, 2, 3)];
        fake.Paths[1].Target.OutputTechnology = 5;
        fake.Modes = [..fake.Modes, SourceMode(AdapterA, secondSource), TargetMode(AdapterA, 14)];
        var d = Collect(fake).Displays.Value!;
        Assert.Equal(2, d.Count);
        Assert.Equal(clone, d[0].SourceId == d[1].SourceId);
        Assert.NotEqual(d[0].TargetId, d[1].TargetId);
        Assert.Equal(d[0].TargetAdapterId, d[1].TargetAdapterId);
        Assert.Equal("hdmi", d[1].OutputTechnology.Value);
        Assert.Equal(1, fake.AdapterCalls);
    }
    [Fact] public void DisplaysOnDifferentAdaptersDoNotCollapseSourceOrTargetIds()
    {
        var fake = Single();
        fake.Paths = [fake.Paths[0], Path(AdapterB, 7, 13, 2, 3)];
        fake.Modes = [..fake.Modes, SourceMode(AdapterB, 7), TargetMode(AdapterB, 13)];
        var d = Collect(fake).Displays.Value!;
        Assert.NotEqual(d[0].SourceId, d[1].SourceId);
        Assert.NotEqual(d[0].TargetId, d[1].TargetId);
        Assert.Equal("gpu-1", d[1].SourceAdapter.Value!.GpuId);
    }
    [Fact] public void DistinctSourceAndTargetAdaptersArePreserved()
    {
        var fake = Single(); fake.Paths[0].Target.AdapterId = AdapterB; fake.Modes[1].AdapterId = AdapterB;
        var d = Collect(fake).Displays.Value![0];
        Assert.NotEqual(d.SourceAdapterId, d.TargetAdapterId);
        Assert.Equal("gpu-2", d.SourceAdapter.Value!.GpuId); Assert.Equal("gpu-1", d.TargetAdapter.Value!.GpuId);
    }
    [Fact] public void DuplicateInstanceIdentityIsAmbiguous()
    {
        var result = Collect(Single(), [new("gpu-1", RawInstanceA), new("gpu-2", RawInstanceA)]);
        Assert.Equal(ReasonCode.AmbiguousAdapter, result.Displays.Value![0].SourceAdapter.Reason);
        Assert.Equal(CollectorStatus.Partial, result.Run.Status);
    }
    [Fact] public void SamePciTypeWithDifferentInstanceIsUnmatched()
    {
        var d = Collect(Single(), [new("gpu-1", @"PCI\VEN_1234&DEV_5678\DIFFERENT_INSTANCE")]).Displays.Value![0];
        Assert.Equal(ReasonCode.UnmatchedAdapter, d.TargetAdapter.Reason);
    }
    [Fact] public void EmptyInventoryKeepsTopologyUnmatched()
    {
        var result = Collect(Single(), []);
        Assert.Equal(DataState.Available, result.Displays.State);
        Assert.Equal(ReasonCode.UnmatchedAdapter, result.Displays.Value![0].SourceAdapter.Reason);
    }
    [Theory] [InlineData(5, ReasonCode.SessionAccessDenied, DataState.Failed)]
    [InlineData(50, ReasonCode.NotSupported, DataState.Unsupported)]
    [InlineData(87, ReasonCode.NativeError, DataState.Failed)]
    [InlineData(31, ReasonCode.NativeError, DataState.Failed)]
    public void QueryFailureIsStructured(int error, ReasonCode reason, DataState state)
    {
        var fake = Single(); fake.QueryErrors.Enqueue(error);
        var result = Collect(fake);
        Assert.Equal(state, result.Displays.State); Assert.Null(result.Displays.Value);
        Assert.Equal(reason, result.Displays.Reason); Assert.Equal(error, result.Run.Issues.Last().NativeErrorCode);
    }
    [Fact] public void MissingApiIsUnsupportedWithoutExceptionLeak()
    {
        var fake = Single(); fake.ThrowUnavailable = true;
        Assert.Equal(ReasonCode.ApiUnavailable, Collect(fake).Displays.Reason);
    }
    [Fact] public void UnsupportedAttemptIsIncompleteButDeferredFeatureIsNotFailure()
    {
        Assert.True(new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Unsupported, ReasonCode.NotSupported).IsIncomplete());
        Assert.False(new CollectorRun(DataSource.DisplayConfig, CollectorStatus.Unsupported, ReasonCode.NotImplemented).IsIncomplete());
    }
    [Fact] public void SizeCallFailureDoesNotAttemptQuery()
    {
        var fake = Single(); fake.SizeError = 5;
        Assert.Equal(ReasonCode.SessionAccessDenied, Collect(fake).Displays.Reason); Assert.Equal(0, fake.QueryCalls);
    }
    [Fact] public void SizeRaceRetriesAndRecordsRecoveredAttempt()
    {
        var fake = Single(); fake.QueryErrors.Enqueue(122);
        var result = Collect(fake);
        Assert.Equal(CollectorStatus.Succeeded, result.Run.Status);
        Assert.Equal(2, fake.SizeCalls); Assert.Equal(2, result.Run.Attempts);
        Assert.Equal(ReasonCode.TopologyChanged, Assert.Single(result.Run.Issues).Reason);
    }
    [Fact] public void InsufficientBufferRetryUsesGrownAllocationAndChangedTopology()
    {
        var fake = Single();
        fake.Paths = [fake.Paths[0], Path(AdapterA, 8, 14, 2, 3)];
        fake.Modes = [..fake.Modes, SourceMode(AdapterA, 8), TargetMode(AdapterA, 14)];
        fake.SizePathSequence.Enqueue(0); fake.SizeModeSequence.Enqueue(0); fake.QueryErrors.Enqueue(Ccd.InsufficientBuffer);
        var result = Collect(fake);
        Assert.Equal(CollectorStatus.Succeeded, result.Run.Status);
        Assert.Equal(2, result.Run.Attempts);
        Assert.Equal(2, result.Displays.Value!.Count);
        Assert.Contains(result.Run.Issues, i => i.Operation == CollectionOperation.QueryPaths && i.Reason == ReasonCode.TopologyChanged);
    }
    [Fact] public void InsufficientBufferRetryCanConvergeToSmallerFinalTopology()
    {
        var fake = Single();
        fake.SizePathSequence.Enqueue(2); fake.SizeModeSequence.Enqueue(2); fake.QueryErrors.Enqueue(Ccd.InsufficientBuffer);
        var result = Collect(fake);
        Assert.Equal(CollectorStatus.Succeeded, result.Run.Status);
        Assert.Equal(2, result.Run.Attempts);
        Assert.Single(result.Displays.Value!);
    }
    [Fact] public void PerpetualSizeRaceIsBounded()
    {
        var fake = Single(); for (var i = 0; i < 4; i++) fake.QueryErrors.Enqueue(122);
        var result = Collect(fake);
        Assert.Equal(ReasonCode.TopologyChanged, result.Displays.Reason);
        Assert.Equal(3, fake.QueryCalls); Assert.Equal(3, result.Run.Attempts);
    }
    [Fact] public void NoActivePathsIsAvailableEmptyNotFailure()
    {
        var fake = new FakeApi(); var result = Collect(fake);
        Assert.Equal(DataState.Available, result.Displays.State); Assert.Empty(result.Displays.Value!);
        Assert.Equal(CollectorStatus.Succeeded, result.Run.Status); Assert.Equal(1, fake.QueryCalls);
    }
    [Fact] public void ExcessiveNativeCountsAreRejectedBeforeAllocation()
    {
        var fake = Single(); fake.SizePathOverride = 129;
        Assert.Equal(ReasonCode.ResourceLimit, Collect(fake).Displays.Reason); Assert.Equal(0, fake.QueryCalls);
    }
    [Fact] public void ExcessiveModeCountIsRejectedBeforeAllocation()
    {
        var fake = Single(); fake.SizeModeOverride = 513;
        Assert.Equal(ReasonCode.ResourceLimit, Collect(fake).Displays.Reason); Assert.Equal(0, fake.QueryCalls);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void SuccessfulReturnedCountExceedingAllocationIsRejected(bool pathCount)
    {
        var fake = Single();
        if (pathCount) fake.ReturnedPathCountOverride = (uint)fake.Paths.Length + 1;
        else fake.ReturnedModeCountOverride = (uint)fake.Modes.Length + 1;
        Assert.Equal(ReasonCode.InvalidValue, Collect(fake).Displays.Reason);
    }
    [Fact] public void RepeatedCollectorRunsDoNotLeakPriorFactsOrDiagnostics()
    {
        var fake = Single();
        var collector = new DisplayTopologyCollector(fake, fake, DisplayQueryMode.VirtualModeAndRefreshAware);
        var first = collector.Collect(Inventory);
        Assert.Equal("gpu-2", Assert.Single(first.Displays.Value!).SourceAdapter.Value!.GpuId);

        for (var i = 0; i < DisplayTopologyCollector.MaxAttempts; i++) fake.QueryErrors.Enqueue(Ccd.InsufficientBuffer);
        var second = collector.Collect(Inventory);
        Assert.Equal(DataState.Failed, second.Displays.State);
        Assert.Equal(ReasonCode.TopologyChanged, second.Displays.Reason);
        Assert.Equal(DisplayTopologyCollector.MaxAttempts, second.Run.Attempts);
        Assert.Equal("gpu-2", Assert.Single(first.Displays.Value!).SourceAdapter.Value!.GpuId);

        fake.Paths = [Path(AdapterB, 7, 13, 0, 1)];
        fake.Modes = [SourceMode(AdapterB, 7), TargetMode(AdapterB, 13)];
        var third = collector.Collect(Inventory);
        Assert.Equal(DataState.Available, third.Displays.State);
        Assert.Equal("gpu-1", Assert.Single(third.Displays.Value!).SourceAdapter.Value!.GpuId);
        Assert.Equal("gpu-2", Assert.Single(first.Displays.Value!).SourceAdapter.Value!.GpuId);
    }
    [Fact] public void MissingFriendlyNameDoesNotLosePathOrInventInternalPanelName()
    {
        var fake = Single(); fake.EmptyFriendly = true;
        var result = Collect(fake); var d = Assert.Single(result.Displays.Value!);
        Assert.Equal(DataState.Unknown, d.Name.State); Assert.Null(d.Name.Value);
        Assert.Equal(DataState.Available, d.SourceResolution.State);
        Assert.Equal(CollectorStatus.Succeeded, result.Run.Status);
        Assert.Equal(ReasonCode.None, result.Run.Reason);
        Assert.False(result.Run.IsIncomplete());
        var issue = Assert.Single(result.Run.Issues);
        Assert.Equal(CollectionOperation.TargetName, issue.Operation);
        Assert.Equal(ReasonCode.MissingValue, issue.Reason);
        Assert.False(issue.BlocksCompletion());
    }
    [Fact] public void MissingFriendlyNameDoesNotHideInvalidModeFailure()
    {
        var fake = Single(); fake.EmptyFriendly = true; fake.Paths[0].Source.ModeIndex = 99;
        var result = Collect(fake);
        Assert.Equal(CollectorStatus.Partial, result.Run.Status);
        Assert.Equal(ReasonCode.InvalidModeIndex, result.Run.Reason);
        Assert.True(result.Run.IsIncomplete());
        Assert.Contains(result.Run.Issues, i => i.Operation == CollectionOperation.TargetName && i.Reason == ReasonCode.MissingValue);
        Assert.Contains(result.Run.Issues, i => i.Operation == CollectionOperation.DecodeMode && i.Reason == ReasonCode.InvalidModeIndex);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void MissingFriendlyNameDoesNotMaskAdapterCorrelationProblems(bool ambiguous)
    {
        var fake = Single(); fake.EmptyFriendly = true;
        var inventory = ambiguous
            ? new GpuCorrelationIdentity[] { new("gpu-1", RawInstanceA), new("gpu-2", RawInstanceA) }
            : [new("gpu-1", "OTHER-DEVICE")];
        var result = Collect(fake, inventory);
        var expected = ambiguous ? ReasonCode.AmbiguousAdapter : ReasonCode.UnmatchedAdapter;
        Assert.Equal(CollectorStatus.Partial, result.Run.Status);
        Assert.Equal(expected, result.Run.Reason);
        Assert.Equal(expected, result.Displays.Value![0].SourceAdapter.Reason);
    }
    [Fact] public void TargetNameApiFailureRemainsBlocking()
    {
        var fake = Single(); fake.EmptyFriendly = true; fake.TargetError = 31;
        var result = Collect(fake);
        Assert.Equal(CollectorStatus.Partial, result.Run.Status);
        Assert.Equal(ReasonCode.NativeError, result.Run.Reason);
        Assert.Equal(DataState.Failed, result.Displays.Value![0].Name.State);
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void FailedNameQueryKeepsOtherPathFacts(bool source)
    {
        var fake = Single(); if (source) fake.SourceError = 5; else fake.TargetError = 31;
        var result = Collect(fake); var d = result.Displays.Value![0];
        Assert.Equal(CollectorStatus.Partial, result.Run.Status);
        Assert.Equal(DataState.Failed, (source ? d.SourceGdiName : d.Name).State);
        Assert.Equal("gpu-2", d.SourceAdapter.Value!.GpuId);
    }
    [Fact] public void UnexpectedMetadataExceptionIsContained()
    {
        var fake = Single(); fake.ThrowSource = true;
        Assert.Equal(ReasonCode.NativeError, Collect(fake).Displays.Value![0].SourceGdiName.Reason);
    }
    [Fact] public void OneTargetMetadataFailureDoesNotCorruptAnotherValidPath()
    {
        var fake = Single();
        fake.Paths = [fake.Paths[0], Path(AdapterA, 8, 14, 2, 3)];
        fake.Modes = [..fake.Modes, SourceMode(AdapterA, 8), TargetMode(AdapterA, 14)];
        fake.TargetError = 31; fake.TargetFailureIds.Add(13);
        var result = Collect(fake);
        Assert.Equal(CollectorStatus.Partial, result.Run.Status);
        Assert.Equal(ReasonCode.NativeError, result.Run.Reason);
        var displays = result.Displays.Value!;
        Assert.Equal(DataState.Failed, displays[0].Name.State);
        Assert.Equal(ReasonCode.NativeError, displays[0].Name.Reason);
        Assert.Equal("Example Panel", displays[1].Name.Value);
        Assert.All(displays, d => Assert.Equal("gpu-2", d.SourceAdapter.Value!.GpuId));
    }
    [Theory] [InlineData(true)] [InlineData(false)]
    public void AdapterResolutionFailuresDoNotEraseTopology(bool interfaceQuery)
    {
        var fake = Single(); if (interfaceQuery) fake.AdapterError = 31; else fake.ResolverError = 5;
        var result = Collect(fake);
        Assert.Equal(DataState.Available, result.Displays.State);
        Assert.Equal(DataState.Failed, result.Displays.Value![0].SourceAdapter.State);
    }
    [Theory] [InlineData(99)] [InlineData(1)]
    public void WrongModeIndexOrUnionTypeIsUnknown(int index)
    {
        var fake = Single(); fake.Paths[0].Source.ModeIndex = (uint)index;
        Assert.Equal(ReasonCode.InvalidModeIndex, Collect(fake).Displays.Value![0].SourceResolution.Reason);
    }
    [Fact] public void WrongModeAdapterOrIdentityCannotSupplyResolution()
    {
        var fake = Single(); fake.Modes[0].AdapterId = AdapterB;
        Assert.Equal(ReasonCode.InvalidModeIndex, Collect(fake).Displays.Value![0].SourceResolution.Reason);
        fake.Modes[0].AdapterId = AdapterA; fake.Modes[0].Id = 999;
        Assert.Equal(ReasonCode.InvalidModeIndex, Collect(fake).Displays.Value![0].SourceResolution.Reason);
    }
    [Fact] public void CloneGroupFallbackUsesLow16BitsOnlyWhenSourceModeAbsent()
    {
        var fake = Single(true); fake.Paths[0].Source.ModeIndex = 0xffff0000 | 42;
        var result = Collect(fake); var d = result.Displays.Value![0];
        Assert.Equal("clone-1", d.CloneGroupId.Value); Assert.Equal(DataState.Unknown, d.SourceResolution.State);
    }
    [Fact] public void PathAndSignalRefreshRemainSeparateRationals()
    {
        var fake = Single(true); fake.Paths[0].Target.RefreshRate = new() { Numerator = 60000, Denominator = 1001 };
        fake.Paths[0].Flags |= Ccd.PathBoostRefresh;
        var d = Collect(fake).Displays.Value![0];
        Assert.Equal(new RationalRate(60000, 1001), d.PathRefreshRate.Value);
        Assert.Equal(new RationalRate(165000, 1000), d.SignalRefreshRate.Value);
        Assert.True(d.RefreshRateBoost.Value!.Enabled);
    }
    [Fact] public void ZeroRefreshDenominatorIsUnknown()
    {
        var fake = Single(); fake.Paths[0].Target.RefreshRate.Denominator = 0;
        Assert.Equal(ReasonCode.InvalidValue, Collect(fake).Displays.Value![0].PathRefreshRate.Reason);
    }
    [Theory] [InlineData(DisplayQueryMode.ActivePaths, 2)] [InlineData(DisplayQueryMode.VirtualModeAware, 18)]
    public void OlderQueryModesDoNotPretendToKnowRefreshBoost(DisplayQueryMode mode, int flags)
    {
        var fake = Single(); var d = Collect(fake, mode: mode).Displays.Value![0];
        Assert.Equal((uint)flags, fake.LastFlags); Assert.Equal(DataState.Unsupported, d.RefreshRateBoost.State);
    }
    [Fact] public void RemovedTargetAndInactivePathAreNotReportedAsHealthy()
    {
        var fake = Single(); fake.Paths[0].Target.TargetAvailable = 0;
        var result = Collect(fake); Assert.True(result.Displays.Value![0].PathActive); Assert.False(result.Displays.Value[0].TargetAvailable);
        Assert.Equal(CollectorStatus.Partial, result.Run.Status);
        fake.Paths[0].Flags = 0; result = Collect(fake);
        Assert.Empty(result.Displays.Value!); Assert.Contains(result.Run.Issues, i => i.Reason == ReasonCode.InactivePathSkipped);
    }

    internal static CollectionSnapshot WithTopology(TopologyResult topology)
    {
        var s = ModelAndPrivacyTests.Sample(count: 2);
        var list = s.Facts.Gpus.Value!.Select((g, i) => g with { Id = $"gpu-{i + 1}" }).ToArray();
        return new(s.Facts with { Gpus = Observation<IReadOnlyList<GpuFacts>>.Known(list, DataSource.WmiVideoController), Displays = topology.Displays }, [..s.Collection, topology.Run]);
    }
    [Fact] public void NativeCorrelationIdentifiersAndEdidNeverReachExports()
    {
        var safe = PrivacyPolicy.Prepare(WithTopology(Collect(Single(true))), new(2026, 9, 10));
        var json = ReportWriter.Json(safe); var md = ReportWriter.Markdown(safe);
        foreach (var secret in new[] { RawPathA, RawInstanceA, RawMonitor, "PRIVATE_ADAPTER_INSTANCE", "PRIVATE_EDID_MONITOR", "SERIAL_ABC123",
            "99887766", "11335577", "54321", "45678", "987654", "adapterDevicePath", "monitorDevicePath", "luid" })
        { Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain(secret, md, StringComparison.OrdinalIgnoreCase); }
        Assert.DoesNotContain("\"instanceId\":", json, StringComparison.OrdinalIgnoreCase);
        var parsed = JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!;
        Assert.Equal("gpu-2", parsed.Facts.Displays.Value![0].SourceAdapter.Value!.GpuId);
        Assert.Contains("Display source adapter", md); Assert.DoesNotContain("GPU currently rendering", md);
    }
    [Fact] public void MissingFriendlyNameExportsUnknownWithoutCollectionIncompleteWarning()
    {
        var fake = Single(); fake.EmptyFriendly = true;
        var safe = PrivacyPolicy.Prepare(WithTopology(Collect(fake)), new(2026, 9, 13));
        var json = ReportWriter.Json(safe);
        var markdown = ReportWriter.Markdown(safe);
        Assert.DoesNotContain("collectionIncomplete", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CollectionIncomplete", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TargetName: MissingValue", markdown);
        // Distinctive markers survive JSON/Markdown escaping, unlike the full raw path.
        Assert.DoesNotContain("PRIVATE", json); Assert.DoesNotContain("PRIVATE", markdown);
        var report = JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!;
        var display = Assert.Single(report.Facts.Displays.Value!);
        Assert.Equal(DataState.Unknown, display.Name.State);
        Assert.Equal(ReasonCode.MissingValue, display.Name.Reason);
        var run = report.Collection.Single(c => c.Source == DataSource.DisplayConfig);
        Assert.Equal(CollectorStatus.Succeeded, run.Status);
        Assert.False(run.IsIncomplete());
        Assert.Contains(run.Issues, i => i.Operation == CollectionOperation.TargetName && i.Reason == ReasonCode.MissingValue);
        Assert.DoesNotContain(WarningCode.CollectionIncomplete, report.Warnings);
    }
    [Fact] public void MultiPathPrivacyPreservesRelationshipsAndRemovesRawIdentifiers()
    {
        var fake = Single();
        fake.Paths = [fake.Paths[0], Path(AdapterA, 7, 14, 2, 3)];
        fake.Modes = [..fake.Modes, SourceMode(AdapterA, 7), TargetMode(AdapterA, 14)];
        var json = ReportWriter.Json(PrivacyPolicy.Prepare(WithTopology(Collect(fake)), new(2026, 9, 13)));
        var report = JsonSerializer.Deserialize<DiagnosticReport>(json, ReportWriter.JsonOptions)!;
        var displays = report.Facts.Displays.Value!;
        Assert.Equal(2, displays.Count);
        Assert.Equal(displays[0].SourceId, displays[1].SourceId);
        Assert.Equal(displays[0].SourceAdapterId, displays[1].SourceAdapterId);
        Assert.NotEqual(displays[0].TargetId, displays[1].TargetId);
        Assert.All(displays, d => Assert.Equal("gpu-2", d.SourceAdapter.Value!.GpuId));
        foreach (var secret in new[] { "PRIVATE_ADAPTER_INSTANCE", "PRIVATE_EDID_MONITOR", "SERIAL_ABC123",
            "99887766", "11335577", "54321", "45678", "987654" })
            Assert.DoesNotContain(secret, json, StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public void PrivacyRegeneratesKeysAndFiltersEveryDisplayString()
    {
        var snapshot = WithTopology(Collect(Single()));
        var d = snapshot.Facts.Displays.Value![0];
        static Observation<string> Text(string value) => Observation<string>.Known(value, DataSource.DisplayConfig);
        d = d with { Id = RawMonitor, SourceId = RawMonitor, TargetId = RawMonitor, SourceAdapterId = RawPathA, TargetAdapterId = RawPathA,
            Name = Text("serial=SECRET"), SourceGdiName = Text(RawPathA), Rotation = Text(RawPathA), OutputTechnology = Text(RawPathA),
            ScanLineOrdering = Text(RawPathA), CloneGroupId = Text(RawInstanceA) };
        snapshot = snapshot with { Facts = snapshot.Facts with { Displays = Observation<IReadOnlyList<DisplayFacts>>.Known([d], DataSource.DisplayConfig) } };
        var safe = PrivacyPolicy.Prepare(snapshot, new(2026, 9, 10));
        Assert.DoesNotContain("PRIVATE", ReportWriter.Json(safe)); Assert.DoesNotContain("SECRET", ReportWriter.Markdown(safe));
        var result = JsonSerializer.Deserialize<DiagnosticReport>(ReportWriter.Json(safe), ReportWriter.JsonOptions)!;
        Assert.Equal(5, result.Privacy.RedactedFields); Assert.Equal("clone-1", result.Facts.Displays.Value![0].CloneGroupId.Value);
    }
    [Fact] public void GdiAliasIsAllowedOnlyInItsOwnField()
    {
        var result = WithTopology(Collect(Single()));
        var d = result.Facts.Displays.Value![0];
        result = result with { Facts = result.Facts with { Displays = Observation<IReadOnlyList<DisplayFacts>>.Known([d with { Name = d.SourceGdiName }], DataSource.DisplayConfig) } };
        var report = JsonSerializer.Deserialize<DiagnosticReport>(ReportWriter.Json(PrivacyPolicy.Prepare(result, new(2026, 9, 10))), ReportWriter.JsonOptions)!;
        Assert.Equal(DataState.Available, report.Facts.Displays.Value![0].SourceGdiName.State);
        Assert.Equal(DataState.Redacted, report.Facts.Displays.Value[0].Name.State);
    }
    [Fact] public void TopologyFailureLeavesWindowsInventoryReportIntact()
    {
        var snapshot = WithTopology(Collect(new FakeApi { SizeError = 5 }));
        var report = JsonSerializer.Deserialize<DiagnosticReport>(ReportWriter.Json(PrivacyPolicy.Prepare(snapshot, new(2026, 9, 10))), ReportWriter.JsonOptions)!;
        Assert.Equal(2, report.Facts.Gpus.Value!.Count); Assert.Equal(DataState.Failed, report.Facts.Displays.State);
        Assert.Contains(WarningCode.CollectionIncomplete, report.Warnings);
    }
}
