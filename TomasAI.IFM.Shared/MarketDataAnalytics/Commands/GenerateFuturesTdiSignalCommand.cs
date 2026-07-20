using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to generate a TDI (Trend Direction / Divergence Index) signal for a futures contract
/// by combining a collection of prior RSI signals.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern adopted by other analytics commands. The command is routed to
/// <see cref="BoundedContextName.FuturesTdiSignalBoundedContext"/>. Properties beyond the base command (keys 0�5)
/// start at index 6 for MessagePack.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesTdiSignalCommand : ICommand<FuturesTdiSignalEntityId>
{
    public const string Actor = "FuturesTdiSignalCommand";
    public const string Verb = "Generate";
    public const int ErrorId = 20001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesTdiSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Identifier describing the target futures contract and value date for the TDI signal generation.
    /// </summary>
    [Key(6)]
    public FuturesTdiSignalId FuturesTdiSignalId { get; init; }

    /// <summary>
    /// Collection of RSI signals used as input factors for computing the TDI signal.
    /// </summary>
    [Key(7)]
    public FuturesRsiSignalReadModel[] FuturesRsiSignals { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesTdiSignalCommand() { }

    /// <summary>
    /// Creates a new TDI signal generation command for the specified contract/date and RSI signal set.
    /// </summary>
    /// <param name="futuresTdiSignalId">Target TDI signal identifier (contract + value date + timestamp context).</param>
    /// <param name="futuresRsiSignals">Input RSI signal series (cannot be null).</param>
    public GenerateFuturesTdiSignalCommand(
        FuturesTdiSignalId futuresTdiSignalId,
        FuturesRsiSignalReadModel[] futuresRsiSignals)
    {
        FuturesTdiSignalId = futuresTdiSignalId;
        FuturesRsiSignals = futuresRsiSignals ?? throw new ArgumentNullException(nameof(futuresRsiSignals));

        EntityId = new FuturesTdiSignalEntityId(FuturesTdiSignalId.ContractId, FuturesTdiSignalId.ValueDate, TradeTimePeriodType.Daily);
        ErrorCode = 20001;
        RouteTo = BoundedContextName.FuturesTdiSignalBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public GenerateFuturesTdiSignalCommand(
        Guid commandId,                     // Key(0)
        ActorSubject subject,               // Key(1)
        bool postEvents,                    // Key(2)
        FuturesTdiSignalEntityId entityId,  // Key(3)
        int errorCode,                      // Key(4)
        BoundedContextName routeTo,         // Key(5)
        FuturesTdiSignalId tdiSignalId,     // Key(6)
        FuturesRsiSignalReadModel[] rsiSignals) // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesTdiSignalId = tdiSignalId;
        FuturesRsiSignals = rsiSignals;
    }
}