using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to generate an ATR (Average True Range) signal for a futures contract
/// by combining a collection of prior RSI signals.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern adopted by other analytics commands. The command is routed to
/// <see cref="BoundedContextName.FuturesAtrSignalBoundedContext"/>. Properties beyond the base command (keys 0–5)
/// start at index 6 for MessagePack.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesAtrDailySignalCommand : ICommand<FuturesAtrDailySignalEntityId>
{
    public const string Actor = "FuturesAtrSignalCommand";
    public const string Verb = "Generate";
    public const int ErrorId = 20004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesAtrDailySignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Identifier describing the target futures contract and value date for the ATR signal generation.
    /// </summary>
    [Key(6)]
    public FuturesAtrSignalId FuturesAtrSignalId { get; init; }

    /// <summary>
    /// Collection of ITI (Intrinsic Time Indicator) signals used as input factors for computing the ATR signal.
    /// </summary>
    [Key(7)]
    public decimal FuturesPrice { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesAtrDailySignalCommand() { }

    /// <summary>
    /// Creates a new ATR signal generation command for the specified contract/date and RSI signal set.
    /// </summary>
    /// <param name="futuresAtrSignalId">Target ATR signal identifier (contract + value date + timestamp context).</param>
    /// <param name="futuresPrice">Input RSI signal series (cannot be null).</param>
    public GenerateFuturesAtrDailySignalCommand(
        FuturesAtrSignalId futuresAtrSignalId,
        decimal futuresPrice)
    {
        FuturesAtrSignalId = futuresAtrSignalId;
        FuturesPrice = futuresPrice;

        EntityId = futuresAtrSignalId.ToDailyEntityId();
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesAtrSignalBoundedContext;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GenerateFuturesAtrDailySignalCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesAtrDailySignalEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FuturesAtrSignalId futuresAtrSignalId,
        decimal futuresPrice)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesAtrSignalId = futuresAtrSignalId;
        FuturesPrice = futuresPrice;
    }
}
