using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// MessagePack-serializable command to remove an existing futures contract.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern:
/// - Command metadata occupies keys 0..5.
/// - Derived payload keys start at 6.
/// Routes to <see cref="BoundedContextName.FuturesContractBoundedContext"/> with error code 6007.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record RemoveFuturesContractCommand : ICommand<FuturesContractId>
{
    [IgnoreMember] public const string Actor = "FuturesContractCommand";
    [IgnoreMember] public const string Verb = "Remove";
    public const int ErrorId = 6007;

    // Serialized members (unique keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesContractId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>The futures contract identifier targeted for removal.</summary>
    [Key(6)]
    public FuturesContractId ContractId { get; init; }

    /// <summary>True to overwrite/force removal logic; otherwise false.</summary>
    [Key(7)]
    public bool Overwrite { get; init; }

    /// <summary>Parameterless constructor required by serializers.</summary>
    public RemoveFuturesContractCommand() { }

    /// <summary>
    /// Creates a new command to remove the specified futures contract.
    /// </summary>
    /// <param name="contractId">Identifier of the contract to remove (required).</param>
    /// <param name="overwrite">Set true to force/overwrite related persisted data.</param>
    public RemoveFuturesContractCommand(FuturesContractId contractId, bool overwrite = false)
    {
        ContractId = contractId ?? throw new ArgumentNullException(nameof(contractId));
        Overwrite = overwrite;

        EntityId = ContractId;
        RouteTo = BoundedContextName.FuturesContractBoundedContext;
        ErrorCode = 6007;
    }

    /// <summary>
    /// MessagePack serialization constructor. Parameter order must match Key attributes (0..7).
    /// </summary>
    [SerializationConstructor]
    public RemoveFuturesContractCommand(
        Guid commandId,              // Key(0)
        ActorSubject subject,        // Key(1)
        bool postEvents,             // Key(2)
        FuturesContractId entityId,  // Key(3)
        int errorCode,               // Key(4)
        BoundedContextName routeTo,  // Key(5)
        FuturesContractId contractId,// Key(6)
        bool overwrite)              // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ContractId = contractId;
        Overwrite = overwrite;
    }
}