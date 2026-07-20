using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// Represents a command to remove a specified futures option contract from the system.
/// </summary>
/// <remarks>This command encapsulates all necessary information to request the removal of a futures option
/// contract, including the contract identifier and options for removal behavior. It is typically used in distributed or
/// event-driven architectures to initiate contract deletion workflows. The command supports serialization via
/// MessagePack for efficient transport between services. Thread safety and idempotency depend on the consuming
/// infrastructure.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record RemoveFuturesOptionContractCommand
    : ICommand<FuturesOptionContractEntityId>
{
    public const string Actor = "FuturesOptionContractCommand";
    public const string Verb = "Remove";
    public const int ErrorId = 6008;

    // Serialized members (base keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Default;
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Payload keys (6..)
    [Key(6)] public string ContractId { get; init; } = string.Empty;
    [Key(7)] public bool Overwrite { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Events";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Parameterless constructor required by serializers.</summary>
    public RemoveFuturesOptionContractCommand()
    {
    }

    /// <summary>
    /// Creates a new command to remove a futures option contract.
    /// </summary>
    /// <param name="contractId">Raw contract identifier (cannot be null or empty).</param>
    /// <param name="overwrite">Set true to force removal logic.</param>
    public RemoveFuturesOptionContractCommand(string contractId, bool overwrite = false)
    {
        IsArgumentNull.Check(contractId);
        ContractId = contractId;
        Overwrite = overwrite;

        var futuresOptionContractId = new FuturesOptionContractId(contractId);
        EntityId = new FuturesOptionContractEntityId(contractId, futuresOptionContractId.MaturityDate.Year);
        RouteTo = BoundedContextName.FuturesOptionContractBoundedContext;
        ErrorCode = 6008;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public RemoveFuturesOptionContractCommand(
        Guid commandId,                         // Key(0)
        ActorSubject subject,                   // Key(1)
        bool postEvents,                        // Key(2)
        FuturesOptionContractEntityId entityId, // Key(3)
        int errorCode,                          // Key(4)
        BoundedContextName routeTo,             // Key(5)
        string contractId,                      // Key(6)
        bool overwrite)                         // Key(7)
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