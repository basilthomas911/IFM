using MessagePack;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference.QueryParameters;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the MDI forward loss ratio map based on the specified trend direction and trade type.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetMDIForwardLossRatioMapQuery : IQuery<MDIForwardLossRatioMap>
{
    [IgnoreMember] public const string Actor = "MDIForwardLossRatioMapQuery";
    [IgnoreMember] public const string Verb = "GetMDIForwardLossRatioMap";
    [IgnoreMember] public const int ErrorId = 1033;

    [Key(0)] public ActorSubject Subject { get; init; }
    [Key(1)] public IActorEntityId EntityId { get; init; }
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public IntrinsicTimeTrendType TrendDirection { get; init; }

    [Key(3)]
    public TradeType TradeType { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetMDIForwardLossRatioMapQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetMDIForwardLossRatioMapQuery(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
    {
        TrendDirection = trendDirection;
        TradeType = tradeType;
        EntityId = new GetMDIForwardLossRatiosParameter(trendDirection, tradeType);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetMDIForwardLossRatioMapQuery(
        ActorSubject subject,                       // Key(0)
        IActorEntityId entityId,                    // Key(1)
        IntrinsicTimeTrendType trendDirection,      // Key(2)
        TradeType tradeType)                        // Key(3)
    {
        Subject = subject;
        EntityId = new GetMDIForwardLossRatiosParameter(trendDirection, tradeType);
        TrendDirection = trendDirection;
        TradeType = tradeType;
        ErrorCode = ErrorId;
    }
}


