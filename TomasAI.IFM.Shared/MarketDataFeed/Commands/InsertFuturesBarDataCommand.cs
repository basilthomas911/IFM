using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to insert a futures bar (OHLCV) data snapshot into the system.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesBarDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertFuturesBarDataCommand : ICommand<FuturesBarDataId>
{
    public const string Actor = "FuturesBarDataCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 5003;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesBarDataId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// The futures bar data payload to insert.
    /// </summary>
    [Key(6)]
    public FuturesBarDataReadModel FuturesBarData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertFuturesBarDataCommand() { }

    /// <summary>
    /// Creates a new command to insert futures bar data.
    /// </summary>
    /// <param name="futuresBarData">The futures bar data payload (cannot be null).</param>
    public InsertFuturesBarDataCommand(FuturesBarDataReadModel futuresBarData)
    {
        FuturesBarData = futuresBarData ?? throw new ArgumentNullException(nameof(futuresBarData));

        EntityId = FuturesBarData.Id;
        ErrorCode = 5003;
        RouteTo = BoundedContextName.FuturesBarDataBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public InsertFuturesBarDataCommand(
        Guid commandId,                       // Key(0)
        ActorSubject subject,                 // Key(1)
        bool postEvents,                      // Key(2)
        FuturesBarDataId entityId,            // Key(3)
        int errorCode,                        // Key(4)
        BoundedContextName routeTo,           // Key(5)
        FuturesBarDataReadModel futuresBarData) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FuturesBarData = futuresBarData;
    }
}