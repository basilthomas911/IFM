using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Shared.MarketDataFeed.Commands;

/// <summary>
/// Command to insert a futures option tick data snapshot for a specific contract.
/// </summary>
/// <remarks>
/// Follows the MessagePack serialization pattern used by other commands. Routes to
/// <see cref="BoundedContextName.FuturesOptionTickDataBoundedContext"/>. Custom properties begin at key index 6
/// because base command members occupy keys 0�5.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record InsertFuturesOptionTickDataCommand
    : ICommand<FuturesOptionTickEntityId>
{
    public const string Actor = "FuturesOptionTickDataCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 5002;

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesOptionTickEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Futures contract metadata associated with the option tick data.</summary>
    [Key(6)]
    public FuturesContractV2ReadModel Contract { get; init; }

    /// <summary>Option tick data payload to insert.</summary>
    [Key(7)]
    public FuturesOptionTickDataV2ReadModel OptionTickData { get; init; }

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public InsertFuturesOptionTickDataCommand() { }

    /// <summary>
    /// Creates a new command to insert futures option tick data.
    /// </summary>
    /// <param name="contract">Futures contract metadata (cannot be null).</param>
    /// <param name="optionTickData">Option tick data payload (cannot be null).</param>
    public InsertFuturesOptionTickDataCommand(
        FuturesContractV2ReadModel contract,
        FuturesOptionTickDataV2ReadModel optionTickData)
    {
        Contract = contract ?? throw new ArgumentNullException(nameof(contract));
        OptionTickData = optionTickData ?? throw new ArgumentNullException(nameof(optionTickData));

        EntityId = OptionTickData.EntityId;
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FuturesOptionTickDataBoundedContext;
    }

    // Optional explicit serialization constructor (keys must match indices)
    [SerializationConstructor]
    public InsertFuturesOptionTickDataCommand(
        Guid commandId,                           // Key(0)
        ActorSubject subject,                     // Key(1)
        bool postEvents,                          // Key(2)
        FuturesOptionTickEntityId entityId,       // Key(3)
        int errorCode,                            // Key(4)
        BoundedContextName routeTo,               // Key(5)
        FuturesContractV2ReadModel contract,      // Key(6)
        FuturesOptionTickDataV2ReadModel data)    // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Contract = contract;
        OptionTickData = data;
    }
}