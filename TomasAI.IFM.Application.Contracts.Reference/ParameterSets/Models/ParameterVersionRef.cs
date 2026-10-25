using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

[MessagePackObject]
public sealed record ParameterVersionRef(
    [property: Key(0)] Guid SetId, [property: Key(1)] int Version,
    [property: Key(2)] string ComponentCode, [property: Key(3)] string PayloadSha256);
