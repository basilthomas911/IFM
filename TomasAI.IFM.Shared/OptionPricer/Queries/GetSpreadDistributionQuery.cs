using MessagePack;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.OptionPricer.QueryParameters;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Shared.OptionPricer.Queries;

/// <summary>
/// MessagePack-serializable query to retrieve the spread distribution for a specific trade.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GetSpreadDistributionQuery : IQuery<SpreadDistributionReadModel>
{
    [IgnoreMember] public const string Actor = "SpreadDistributionQuery";
    [IgnoreMember] public const string Verb = "GetSpreadDistribution";
    [IgnoreMember] public const int ErrorId = 1016;

    [Key(0)] public ActorSubject Subject { get; init; } 
    [Key(1)] public IActorEntityId EntityId { get; init; } 
    [IgnoreMember] public int ErrorCode { get; init; }
    [IgnoreMember] public string? QueryParams { get; init; }

    [Key(2)]
    public int TradeId { get; init; }

    [Key(3)]
    public TradeType TradeType { get; init; }

    [Key(4)]
    public TradeStatus TradeStatus { get; init; }

    [Key(5)]
    public DateOnly ValueDate { get; init; }

    [Key(6)]
    public int DaysToExpiry { get; init; }

    /// <summary>Parameterless constructor for serializers.</summary>
    public GetSpreadDistributionQuery() { }

    /// <summary>Primary constructor to create the query in code and initialize defaults.</summary>
    public GetSpreadDistributionQuery(int tradeId, TradeType tradeType, TradeStatus tradeStatus, DateOnly valueDate, int daysToExpiry)
    {
        TradeId = tradeId;
        TradeType = tradeType;
        TradeStatus = tradeStatus;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        EntityId = new GetSpreadDistributionParameter(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);
        Subject = new ActorSubject(ActorType.Query, Actor, Verb, EntityId.Format());
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GetSpreadDistributionQuery(
        ActorSubject subject,       // Key(0)
        IActorEntityId entityId,    // Key(1)
        int tradeId,                // Key(2)
        TradeType tradeType,        // Key(3)
        TradeStatus tradeStatus,    // Key(4)
        DateOnly valueDate,         // Key(5)
        int daysToExpiry)           // Key(6)
    {
        Subject = subject;
        EntityId = new GetSpreadDistributionParameter(tradeId, tradeType, tradeStatus, valueDate, daysToExpiry);
        TradeId = tradeId;
        TradeType = tradeType;
        TradeStatus = tradeStatus;
        ValueDate = valueDate;
        DaysToExpiry = daysToExpiry;
        ErrorCode = ErrorId;
    }
}
