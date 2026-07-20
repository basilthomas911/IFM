using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// 
/// </summary>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesAdxDailySignalCommand : ICommand<FuturesAdxDailySignalEntityId>
{
    public const string Actor = "FuturesAdxSignalCommand";
    public const string Verb = "GenerateFuturesAdxDailySignal";
    public const int ErrorId = 20005;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } 
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesAdxDailySignalEntityId EntityId { get; init; } 
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Identifier describing the target futures contract and value date for the ADX signal generation.
    /// </summary>
    [Key(6)]
    public FuturesAdxSignalId FuturesAdxSignalId { get; init; }

    /// <summary>
    /// Gets the collection of futures ITI signals associated with the current context.
    /// </summary>
    /// <remarks>Each element in the array represents a specific futures ITI signal and its related data. Use
    /// this property to access all futures ITI signals for further processing or analysis.</remarks>
    [Key(7)]
    public decimal FuturesPrice { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesAdxDailySignalCommand() { }

    /// <summary>
    /// Initializes a new instance of the GenerateFuturesAdxSignalCommand class using the specified futures ADX signal
    /// identifier and associated futures ITI signals.
    /// </summary>
    /// <param name="futuresAdxSignalId">The identifier for the futures ADX signal, including contract ID, value date, and time period.</param>
    /// <param name="futuresPrice">
    /// parameter cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when the futuresItiSignals parameter is null.</exception>
    public GenerateFuturesAdxDailySignalCommand(
        FuturesAdxSignalId futuresAdxSignalId,
        decimal futuresPrice)
    {
        FuturesAdxSignalId = futuresAdxSignalId;
        FuturesPrice = futuresPrice;

        EntityId = futuresAdxSignalId.ToDailyEntityId();
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GenerateFuturesAdxDailySignalCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesAdxDailySignalEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FuturesAdxSignalId futuresAdxSignalId,
        decimal futuresPrice)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesAdxSignalId = futuresAdxSignalId;
        FuturesPrice = futuresPrice;
    }
}
