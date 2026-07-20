using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to insert a futures tick data snapshot for a specific futures contract.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesTickDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertFuturesTickDataCommand : ICommand<FuturesDataId>
{
    public const string Actor = "FuturesTickDataCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 5001;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesDataId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Futures contract metadata associated with the tick data.</summary>
    [Key(6)]
    public FuturesContractV2ReadModel Contract { get; init; }

    /// <summary>Tick data payload to insert.</summary>
    [Key(7)]
    public FuturesTickDataV2ReadModel TickData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertFuturesTickDataCommand() { }

    /// <summary>
    /// Creates a new command to insert futures tick data.
    /// </summary>
    /// <param name="contract">Futures contract metadata (cannot be null).</param>
    /// <param name="tickData">Tick data payload (cannot be null).</param>
    public InsertFuturesTickDataCommand(
        FuturesContractV2ReadModel contract,
        FuturesTickDataV2ReadModel tickData)
    {
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        TickData = tickData ?? throw new ArgumentNullException(nameof(tickData));

        EntityId = TickData.DataId;
        ErrorCode = 5001;
        RouteTo = BoundedContextName.FuturesTickDataBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public InsertFuturesTickDataCommand(
        Guid commandId,                        // Key(0)
        ActorSubject subject,                  // Key(1)
        bool postEvents,                       // Key(2)
        FuturesDataId entityId,                // Key(3)
        int errorCode,                         // Key(4)
        BoundedContextName routeTo,            // Key(5)
        FuturesContractV2ReadModel contract,   // Key(6)
        FuturesTickDataV2ReadModel tickData)   // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Contract = contract;
        TickData = tickData;
    }
}