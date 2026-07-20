using MessagePack;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.Reference.QueryParameters;
using TomasAI.IFM.Shared.Reference.ViewModels;

namespace TomasAI.IFM.Shared.Reference.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve MDI forward loss ratios for a specified trend direction and trade type.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern used by other view models/queries:
/// - Annotated with <see cref="MessagePackObjectAttribute"/>.
/// - Explicit properties annotated with sequential <see cref="KeyAttribute"/> indices.
/// - A parameterless constructor for serializers and a full constructor annotated with <see cref="SerializationConstructorAttribute"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public class GetMDIForwardLossRatiosQuery : IQuery<MDIForwardLossRatioReadModel[]>
{
    [IgnoreMember] public const string Actor = "ReferenceQuery";
    [IgnoreMember] public const string Verb = "GetMDIForwardLossRatios";
    [IgnoreMember] public const int ErrorId = 1035;

    [Key(0)] public ActorSubject Subject { get; set; }
    [Key(1)] public IActorEntityId EntityId { get; set; }
    [IgnoreMember] public int ErrorCode { get; set; }
    [IgnoreMember] public string QueryParams { get; set; }

    /// <summary>
    /// The intrinsic time trend direction for the query.
    /// </summary>
    [Key(2)]
    public IntrinsicTimeTrendType TrendDirection { get; set; }

    /// <summary>
    /// The trade type for the query.
    /// </summary>
    [Key(3)]
    public TradeType TradeType { get; set; }

    /// <summary>
    /// Parameterless constructor for serializers.
    /// </summary>
    public GetMDIForwardLossRatiosQuery() { }

    public GetMDIForwardLossRatiosQuery(IntrinsicTimeTrendType trendDirection, TradeType tradeType)
    {
        TrendDirection = trendDirection;
        TradeType = tradeType;
        EntityId = new GetMDIForwardLossRatiosParameter(trendDirection, tradeType);
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// Indices must match the combined keys from the interface properties (0..1) and this derived type (2..3).
    /// </summary>
    [SerializationConstructor]
    public GetMDIForwardLossRatiosQuery(
        ActorSubject subject,                      // Key(0)
        IActorEntityId entityId,                   // Key(1)
        IntrinsicTimeTrendType trendDirection,     // Key(2)
        TradeType tradeType)                       // Key(3)
    {
        Subject = subject;
        EntityId = new GetMDIForwardLossRatiosParameter(trendDirection, tradeType);
        TrendDirection = trendDirection;
        TradeType = tradeType;
        ErrorCode = ErrorId;
    }
}

