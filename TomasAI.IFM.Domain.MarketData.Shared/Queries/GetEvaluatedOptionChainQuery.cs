using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetEvaluatedOptionChainQuery : IQuery<EvaluatedOptionChainReadModel>
{
    [IgnoreMember] public const string Actor = "MarketDataQuery";
    [IgnoreMember] public const string Verb = "GetEvaluatedOptionChain";
    [IgnoreMember] public const int ErrorId = 1065;
    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; } = ActorEntityId.Default;
    [Key(2)] public string UnderlyingContractId { get; set; } = string.Empty;
    [Key(3)] public string UnderlyingSymbol { get; set; } = string.Empty;
    [Key(4)] public string[] ProviderRoots { get; set; } = [];
    [Key(5)] public DateOnly ExpiryDate { get; set; }
    [Key(6)] public decimal? StandardDeviationAmount { get; set; }
    [Key(7)] public double StandardDeviationMultiplier { get; set; } = 2.5;
    /// <summary>Retained solely for MessagePack compatibility; strike selection no longer applies a count cap.</summary>
    [Obsolete("Strike count limits are ignored; the entire calculated window is selected.")]
    [Key(8)] public int MaximumStrikeCount { get; set; }
    [Key(9)] public string[] RequiredContractIds { get; set; } = [];
    [Key(10)] public bool ReleaseOnly { get; set; }
    [IgnoreMember] public int ErrorCode => ErrorId;
    [IgnoreMember] public string QueryParams => UnderlyingContractId;
}
