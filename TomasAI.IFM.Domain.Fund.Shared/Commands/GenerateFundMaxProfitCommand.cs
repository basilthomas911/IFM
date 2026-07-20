using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to generate the maximum profit scenario for a specific fund order.
/// </summary>
/// <remarks>
/// MessagePack-serializable using numeric keys:
/// - Base command members use keys 0�5.
/// - Payload member (<see cref="FundOrder" />) uses key 6.
/// - Trade time period uses key 7.
/// Routes to <see cref="BoundedContextName.FundBoundedContext" /> and targets the fund identified by the order.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFundMaxProfitCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "GenerateMaxProfit";
    public const int ErrorId = 2014;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FundId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Gets or sets the fund order to evaluate for maximum profit.
    /// </summary>
    /// <remarks>Serialized with key 6.</remarks>
    [Key(6)] public FundOrderReadModel FundOrder { get; init; }

    /// <summary>
    /// Gets or sets the trade time period for the fund.
    /// </summary>
    /// <remarks>Serialized with key 7.</remarks>
    [Key(7)] public TradeTimePeriodType FundTimePeriod { get; init; }

    /// <summary>
    /// Parameterless constructor used by MessagePack for property-based deserialization.
    /// </summary>
    public GenerateFundMaxProfitCommand() { }

    /// <summary>
    /// Convenience constructor for creating the command in application code.
    /// </summary>
    /// <param name="fundOrder">The fund order to evaluate.</param>
    /// <param name="fundTimePeriod">The trade time period for the fund.</param>
    public GenerateFundMaxProfitCommand(FundOrderReadModel fundOrder, TradeTimePeriodType fundTimePeriod)
    {
        FundOrder = fundOrder ?? throw new ArgumentNullException(nameof(fundOrder));
        FundTimePeriod = fundTimePeriod;
        EntityId = new(fundOrder.FundId);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    /// <summary>
    /// Full deserializing constructor used by MessagePack. Parameters must align with key order 0�7.
    /// </summary>
    /// <param name="commandId">Command identifier (key 0).</param>
    /// <param name="subject">Actor subject for routing (key 1).</param>
    /// <param name="postEvents">Whether to publish resulting events (key 2).</param>
    /// <param name="entityId">Fund entity identifier (key 3).</param>
    /// <param name="errorCode">Associated error code (key 4).</param>
    /// <param name="routeTo">Target bounded context (key 5).</param>
    /// <param name="fundOrder">Fund order payload (key 6).</param>
    /// <param name="fundTimePeriod">Trade time period (key 7).</param>
    [SerializationConstructor]
    public GenerateFundMaxProfitCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FundOrderReadModel fundOrder,
        TradeTimePeriodType fundTimePeriod)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundOrder = fundOrder;
        FundTimePeriod = fundTimePeriod;
    }
}