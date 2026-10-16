using MessagePack;
namespace TomasAI.IFM.Domain.Reference.Shared.ParameterSets;

/// <summary>Generic consumer identity, role and canonical domain-owned scope.</summary>
[MessagePackObject]
public sealed record ParameterAssignmentScope(
    [property: Key(0)] string ConsumerKindCode,
    [property: Key(1)] string ConsumerId,
    [property: Key(2)] string Role,
    [property: Key(3)] string ComponentCode,
    [property: Key(4)] string ScopeJson,
    [property: Key(5)] string ScopeSha256)
{
    [Key(6)] public TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType? TargetHorizon {get;init;}
    [Key(7)] public string? Strategy {get;init;}
    [Key(8)] public string Codec {get;init;}=ParameterValueCodec.Codec;
}
