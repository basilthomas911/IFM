using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Shared.MarketDataAnalytics.Commands;

/// <summary>
/// Represents a command to generate an Average True Range (ATR) signal from intra-day data for a specified futures
/// contract.
/// </summary>
/// <remarks>This command encapsulates the necessary parameters for generating an ATR signal, including the target
/// signal identifier and a collection of intra-day data. It is intended for use within a bounded context related to
/// futures trading and is typically processed by a handler that computes the ATR signal based on the provided data. The
/// command is serializable with MessagePack for distributed processing scenarios.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record GenerateFuturesAtrSignalCommand : ICommand<FuturesAtrSignalEntityId>
{
    public const string Actor = "FuturesAtrSignalCommand";
    public const string Verb = "GenerateFuturesAtrSignal";
    public const int ErrorId = 20004;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesAtrSignalEntityId EntityId { get; init; }
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
    /// Gets the collection of intra-day data view models representing trading activity for futures contracts.
    /// </summary>
    /// <remarks>Use this property to access detailed intra-day trading information for each futures contract,
    /// which can be utilized for analysis, reporting, or trading decision support. The array may be empty if no
    /// intra-day data is available.</remarks>
    [Key(7)]
    public decimal FuturesPrice { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public GenerateFuturesAtrSignalCommand() { }

    /// <summary>
    /// Initializes a new instance of the GenerateFuturesAtrSignalFromIntraDayDataCommand class using the specified ATR
    /// signal identifier and intra-day data.
    /// </summary>
    /// <remarks>This constructor sets the EntityId based on the provided futuresAtrSignalId and routes the
    /// command to the FuturesAtrSignal bounded context.</remarks>
    /// <param name="futuresAtrSignalId">The identifier for the futures ATR signal, including contract ID, value date, and time period.</param>
    /// <param name="futuresIntraDayData">An array of intra-day data used to generate the futures ATR signal. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when the futuresIntraDayData parameter is null.</exception>
    public GenerateFuturesAtrSignalCommand(
        FuturesAtrSignalId futuresAtrSignalId,
        decimal futuresPrice)
    {
        FuturesAtrSignalId = futuresAtrSignalId;
        FuturesPrice = futuresPrice;
        EntityId = futuresAtrSignalId.ToEntityId();
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesAtrSignalBoundedContext;
    }

    /// <summary>
    /// MessagePack serialization constructor.
    /// </summary>
    [SerializationConstructor]
    public GenerateFuturesAtrSignalCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesAtrSignalEntityId entityId,
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
