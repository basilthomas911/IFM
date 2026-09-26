using MessagePack;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Shared.Queries;

[MessagePackObject(AllowPrivate = true)]
public sealed record GetEvaluatedOptionChainQuery : IQuery<EvaluatedOptionChainReadModel>
{

    /// <summary>Creates an empty query for serialization.</summary>
    public GetEvaluatedOptionChainQuery() { }

    /// <summary>Rehydrates every published query field in permanent numeric-key order.</summary>
    /// <param name="subject">The Subject field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="underlyingContractId">The UnderlyingContractId field.</param>
    /// <param name="underlyingSymbol">The UnderlyingSymbol field.</param>
    /// <param name="providerRoots">The ProviderRoots field.</param>
    /// <param name="expiryDate">The ExpiryDate field.</param>
    /// <param name="standardDeviationAmount">The StandardDeviationAmount field.</param>
    /// <param name="standardDeviationMultiplier">The StandardDeviationMultiplier field.</param>
    /// <param name="maximumStrikeCount">The MaximumStrikeCount field.</param>
    /// <param name="requiredContractIds">The RequiredContractIds field.</param>
    /// <param name="releaseOnly">The ReleaseOnly field.</param>
    [SerializationConstructor]
    public GetEvaluatedOptionChainQuery(ActorSubject subject, IActorEntityId entityId, string underlyingContractId, string underlyingSymbol, string[] providerRoots, DateOnly expiryDate, decimal? standardDeviationAmount, double standardDeviationMultiplier, int maximumStrikeCount, string[] requiredContractIds, bool releaseOnly)
    {
        Subject = subject;
        EntityId = entityId;
        UnderlyingContractId = underlyingContractId;
        UnderlyingSymbol = underlyingSymbol;
        ProviderRoots = providerRoots;
        ExpiryDate = expiryDate;
        StandardDeviationAmount = standardDeviationAmount;
        StandardDeviationMultiplier = standardDeviationMultiplier;
#pragma warning disable CS0618 // Published key 8 must still round-trip even though the value is ignored.
        MaximumStrikeCount = maximumStrikeCount;
#pragma warning restore CS0618
        RequiredContractIds = requiredContractIds;
        ReleaseOnly = releaseOnly;
    }
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
