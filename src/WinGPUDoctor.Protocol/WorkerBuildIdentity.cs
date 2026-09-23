using System.Text.Json.Serialization;
namespace WinGPUDoctor.Protocol;
internal sealed record WorkerBuildIdentity(
    [property: JsonRequired] int ProtocolVersion,
    [property: JsonRequired] string RuntimeVersion,
    [property: JsonRequired] string WorkerMvid,
    [property: JsonRequired] string ProtocolMvid,
    [property: JsonRequired] string CoreMvid,
    [property: JsonRequired] string WindowsMvid);
