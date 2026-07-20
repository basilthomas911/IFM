using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// Represents a command to add one or more futures option contracts to the system.
/// </summary>
/// <remarks>This command encapsulates all necessary information for adding futures option contracts, including
/// the contracts to be added and relevant routing, error, and context metadata. It is typically used in distributed or
/// event-driven architectures to initiate the addition of new contracts. The command is serializable via MessagePack
/// for efficient transport between services. Thread safety and mutation of properties should be managed externally if
/// used in concurrent scenarios.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddFuturesOptionContractsCommand
    : ICommand<FuturesOptionContractsEntityId>
{
    public const string Actor = "FuturesOptionContractCommand";
    public const string Verb = "AddContracts";
    public const int ErrorId = 6011;

    // Serialized members (base keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Default;
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesOptionContractsEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Payload keys (6..)
    [Key(6)] public FuturesOptionContractReadModel[] Contracts { get; init; } = [];

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Events";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Parameterless constructor required by serializers.</summary>
    public AddFuturesOptionContractsCommand()
    {
    }

    /// <summary>
    /// Creates a new command to add futures option contracts.
    /// </summary>
    /// <param name="contracts">Array of contracts to add (cannot be null).</param>
    public AddFuturesOptionContractsCommand(FuturesOptionContractReadModel[] contracts)
    {
        Contracts = IsArgumentNull.Set(contracts);

        // Derive entity id by current year.
        EntityId = new FuturesOptionContractsEntityId(DateTime.UtcNow.Year);

        RouteTo = BoundedContextName.FuturesOptionContractBoundedContext;
        ErrorCode = ErrorId;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public AddFuturesOptionContractsCommand(
        Guid commandId,                             // Key(0)
        ActorSubject subject,                       // Key(1)
        bool postEvents,                            // Key(2)
        FuturesOptionContractsEntityId entityId,     // Key(3)
        int errorCode,                              // Key(4)
        BoundedContextName routeTo,                 // Key(5)
        FuturesOptionContractReadModel[] contracts) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Contracts = contracts;
    }
}