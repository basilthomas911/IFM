using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Command to generate a daily RSI (Relative Strength Index) signal for a futures contract using end-of-day data.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used for other analytics commands. The RSI window size is fixed (14).
/// The command is routed to <see cref="BoundedContextName.FuturesRsiSignalBoundedContext"/>.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesRsiDailySignalCommand : ICommand<FuturesRsiDailySignalEntityId>
{
    public const string Actor = "FuturesRsiSignalCommand";
    public const string Verb = "GenerateDaily";
    public const int ErrorId = 20001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesRsiDailySignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    [Key(6)]
    public FuturesRsiSignalId FuturesRsiSignalId { get; init; }
    [Key(7)]
    public decimal FuturesPrice { get; init; }

    /// <summary>
    /// RSI calculation window size (period length). Not serialized.
    /// </summary>
    [IgnoreMember]
    public static int WindowSize => 14;

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesRsiDailySignalCommand() { }

    /// <summary>
    /// Creates a new daily RSI signal generation command.
    /// </summary>
    /// <param name="futuresEodData">The end-of-day futures data input (cannot be null).</param>
    public GenerateFuturesRsiDailySignalCommand(
        FuturesRsiSignalId futuresRsiSignalId,
        decimal futuresPrice)
    {
        FuturesRsiSignalId = futuresRsiSignalId;
        FuturesPrice = futuresPrice;

        // Derive EntityId from the symbol parsed out of the contract id.
        EntityId = futuresRsiSignalId.ToEntityDailyId();
        ErrorCode = 20001;
        RouteTo = BoundedContextName.FuturesRsiSignalBoundedContext;
    }


    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public GenerateFuturesRsiDailySignalCommand(
        Guid commandId,                  // Key(0)
        ActorSubject subject,            // Key(1)
        bool postEvents,                 // Key(2)
        FuturesRsiDailySignalEntityId entityId,          // Key(3)
        int errorCode,                   // Key(4)
        BoundedContextName routeTo,      // Key(5)
        FuturesRsiSignalId futuresRsiSignalId,
        decimal futuresPrice)  // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesRsiSignalId = futuresRsiSignalId;
        FuturesPrice = futuresPrice;
    }
}