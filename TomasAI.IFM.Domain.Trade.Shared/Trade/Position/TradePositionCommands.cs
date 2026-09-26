using MessagePack;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Shared.Trade.Position;

/// <summary>
/// Broker-neutral market-price change sent by a realtime ContractId router to the
/// concrete strategy-position actor that owns the selected trade leg.
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record ChangeTradeLegDataCommand : ICommand<StrategyPositionId>
{

    /// <summary>Creates an empty command for serialization and existing callers.</summary>
    public ChangeTradeLegDataCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="tradeLegId">The TradeLegId field.</param>
    /// <param name="contractId">The ContractId field.</param>
    /// <param name="price">The Price field.</param>
    /// <param name="sourceSequence">The SourceSequence field.</param>
    /// <param name="routeGeneration">The RouteGeneration field.</param>
    /// <param name="effectiveAtUtc">The EffectiveAtUtc field.</param>
    /// <param name="tradeType">The TradeType field.</param>
    [SerializationConstructor]
    public ChangeTradeLegDataCommand(Guid commandId, ActorSubject subject, bool postEvents, StrategyPositionId entityId, Guid tradeLegId, string contractId, decimal price, long sourceSequence, long routeGeneration, DateTime effectiveAtUtc, TradeStrategyKind tradeType)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        TradeLegId = tradeLegId;
        ContractId = contractId;
        Price = price;
        SourceSequence = sourceSequence;
        RouteGeneration = routeGeneration;
        EffectiveAtUtc = effectiveAtUtc;
        TradeType = tradeType;
    }
    public const string Verb = "ChangeTradeLegData";

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public StrategyPositionId EntityId { get; init; }
    [Key(4)] public Guid TradeLegId { get; init; }
    [Key(5)] public string ContractId { get; init; } = string.Empty;
    [Key(6)] public decimal Price { get; init; }
    [Key(7)] public long SourceSequence { get; init; }
    [Key(8)] public long RouteGeneration { get; init; }
    [Key(9)] public DateTime EffectiveAtUtc { get; init; }
    [Key(10)] public TradeStrategyKind TradeType { get; init; }

    [IgnoreMember] public string CommandName => nameof(ChangeTradeLegDataCommand);
    [IgnoreMember] public BoundedContextName RouteTo => TradeType switch
    {
        TradeStrategyKind.FuturesOutright => BoundedContextName.FuturesTradePositionBoundedContext,
        TradeStrategyKind.IronCondor => BoundedContextName.FuturesIronCondorTradePositionBoundedContext,
        TradeStrategyKind.VerticalSpread => BoundedContextName.FuturesVerticalSpreadTradePositionBoundedContext,
        _ => throw new InvalidOperationException($"Unsupported routed trade type {TradeType}.")
    };
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Subject.Name}Actor";
    [IgnoreMember] public int ErrorCode => 25106;
}
