using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using WinGPUDoctor.Core;

namespace WinGPUDoctor.Protocol;
internal static class ProtocolCodec
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            if (info.Kind == JsonTypeInfoKind.Object)
                foreach (var property in info.Properties) property.IsRequired = true;
        });
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = ProtocolConstants.MaxJsonDepth,
            RespectRequiredConstructorParameters = true, TypeInfoResolver = resolver
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        return options;
    }
    internal static byte[] Encode(ProtocolFrame frame)
    {
        object payload = frame switch
        {
            RequestFrame => new { }, ReadyFrame ready => ready.Identity, StartFrame start => start.Payload,
            AttemptStartedFrame attempt => new AttemptPayload(attempt.Attempt),
            ResourceLimitFrame => new { },
            ResultFrame result => result.Payload ?? new WorkerResultPayload(null, null, null, null, null),
            _ => throw Invalid()
        };
        var envelope = new Envelope(ProtocolConstants.Version, frame.Kind.ToWire(), frame.Operation.ToWire(),
            frame is ResultFrame r ? r.State.ToWire() : frame is ResourceLimitFrame ? "failed" : null,
            frame is ResultFrame reason ? JsonSerializer.SerializeToElement(reason.Reason, Options) :
                frame is ResourceLimitFrame ? JsonSerializer.SerializeToElement(ReasonCode.ResourceLimit, Options) : null,
            JsonSerializer.SerializeToElement(payload, Options));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, Options);
        _ = Decode(bytes); // Exact same trust-boundary checks for outgoing and incoming frames.
        return bytes;
    }
    internal static ProtocolFrame Decode(ReadOnlySpan<byte> bytes)
    {
        try { return DecodeCore(bytes); }
        catch (ProtocolValidationException) { throw; }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
        { throw Invalid(); }
    }
    private static ProtocolFrame DecodeCore(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > ProtocolConstants.MaxDataFrameBytes) throw Limit();
        _ = new UTF8Encoding(false, true).GetCharCount(bytes);
        // Scan depth before allocating a DOM, preserving ResourceLimit rather than a parser error.
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = ProtocolConstants.MaxParseDepth });
        while (reader.Read()) if (reader.CurrentDepth > ProtocolConstants.MaxJsonDepth - 1) throw Limit();
        using var document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions { MaxDepth = ProtocolConstants.MaxJsonDepth });
        ValidateTree(document.RootElement);
        var envelope = Deserialize<Envelope>(document.RootElement);
        if (envelope.Version != ProtocolConstants.Version || !ProtocolNames.TryParseFrameKind(envelope.Kind, out var kind) ||
            !ProtocolNames.TryParseOperation(envelope.Operation, out var operation)) throw Invalid();
        if (kind is ProtocolFrameKind.Request or ProtocolFrameKind.Ready or ProtocolFrameKind.AttemptStarted or ProtocolFrameKind.Failure &&
            bytes.Length > ProtocolConstants.MaxControlFrameBytes) throw Limit();
        if (kind == ProtocolFrameKind.Failure)
        {
            if (envelope.State != "failed" || envelope.Reason is null ||
                Deserialize<ReasonCode>(envelope.Reason.Value) != ReasonCode.ResourceLimit ||
                envelope.Payload.ValueKind != JsonValueKind.Object || envelope.Payload.EnumerateObject().Any()) throw Invalid();
            return new ResourceLimitFrame(operation);
        }
        if (kind == ProtocolFrameKind.Result)
        {
            if (!ProtocolNames.TryParseResultState(envelope.State, out var state) || envelope.Reason is null) throw Invalid();
            var reason = Deserialize<ReasonCode>(envelope.Reason.Value);
            var payload = Deserialize<WorkerResultPayload>(envelope.Payload);
            var result = new ResultFrame(operation, state, reason, payload);
            ProtocolSemantics.Result(result);
            return result;
        }
        if (envelope.State is not null || envelope.Reason is { ValueKind: not JsonValueKind.Null }) throw Invalid();
        switch (kind)
        {
            case ProtocolFrameKind.Request:
                if (envelope.Payload.ValueKind != JsonValueKind.Object || envelope.Payload.EnumerateObject().Any()) throw Invalid();
                return new RequestFrame(operation);
            case ProtocolFrameKind.Ready:
                var identity = Deserialize<WorkerBuildIdentity>(envelope.Payload);
                if (identity.ProtocolVersion != ProtocolConstants.Version || !Version.TryParse(identity.RuntimeVersion, out _) ||
                    new[] { identity.WorkerMvid, identity.ProtocolMvid, identity.CoreMvid, identity.WindowsMvid }.Any(s => !Guid.TryParseExact(s, "D", out _))) throw Invalid();
                return new ReadyFrame(operation, identity);
            case ProtocolFrameKind.Start:
                var input = Deserialize<WorkerRequestPayload>(envelope.Payload);
                ProtocolSemantics.Start(operation, input);
                return new StartFrame(operation, input);
            case ProtocolFrameKind.AttemptStarted:
                var attempt = Deserialize<AttemptPayload>(envelope.Payload).Attempt;
                if (attempt < 1 || attempt > (operation == WorkerOperation.DisplayActiveTopology ? 3 : 1)) throw Invalid();
                return new AttemptStartedFrame(operation, attempt);
            default: throw Invalid();
        }
    }
    private static T Deserialize<T>(JsonElement value) => JsonSerializer.Deserialize<T>(value, Options) ?? throw Invalid();
    private static void ValidateTree(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid();
                if (property.Name.Length > ProtocolConstants.MaxDecodedStringChars) throw Limit();
                ValidateTree(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            if (element.GetArrayLength() > ProtocolConstants.MaxCollectionItems) throw Limit();
            foreach (var item in element.EnumerateArray()) ValidateTree(item);
        }
        else if (element.ValueKind == JsonValueKind.String && element.GetString()!.Length > ProtocolConstants.MaxDecodedStringChars) throw Limit();
    }
    internal static ProtocolValidationException Invalid() => new(ReasonCode.InvalidValue, "Invalid worker protocol.");
    internal static ProtocolValidationException Limit() => new(ReasonCode.ResourceLimit, "Worker protocol resource limit.");
    private sealed record AttemptPayload(int Attempt);
    private sealed record Envelope(int Version, string Kind, string Operation, string? State, JsonElement? Reason, JsonElement Payload);
}
