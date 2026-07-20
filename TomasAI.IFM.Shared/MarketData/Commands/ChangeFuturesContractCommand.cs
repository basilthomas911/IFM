using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// MessagePack-serializable command to change (update) an existing futures contract.
/// </summary>
/// <remarks>
/// Follows the project's MessagePack pattern:
/// - Base command reserves keys 0..5.
/// - Derived payload keys start at 6.
/// - Includes a parameterless ctor for serializers and a <see cref="SerializationConstructorAttribute"/>-annotated ctor.
/// Routes to <see cref="BoundedContextName.FuturesContractBoundedContext"/> with error code 6004.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeFuturesContractCommand : ICommand<FuturesContractId>
{
    public const string Actor = "FuturesContractCommand";
    public const string Verb = "Change";
    public const int ErrorId = 6004;

    // Serialized members (unique keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesContractId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public FuturesContractId ContractId { get; init; }   
    [Key(7)] public FuturesContractV2ReadModel Contract { get; init; }
    [Key(8)] public bool Overwrite { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>
    /// Parameterless constructor required for MessagePack deserialization.
    /// </summary>
    public ChangeFuturesContractCommand() { }

    /// <summary>
    /// Creates a new command to update a futures contract.
    /// </summary>
    /// <param name="contractId">The target contract identifier.</param>
    /// <param name="contract">The updated contract details (cannot be null).</param>
    /// <param name="overwrite">Set true to overwrite existing contract data.</param>
    public ChangeFuturesContractCommand(
        FuturesContractId contractId,
        FuturesContractV2ReadModel contract,
        bool overwrite = false)
    {
        ContractId = IsArgumentNull.Set(contractId);
        Contract = IsArgumentNull.Set(contract);
        Overwrite = overwrite;

        // EntityId is the authoritative identifier for routing/event sourcing.
        EntityId = Contract.Id ?? ContractId;
        RouteTo = BoundedContextName.FuturesContractBoundedContext;
        ErrorCode = 6004;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match <see cref="KeyAttribute"/> assignments).
    /// Base keys 0..5 are the command metadata; derived keys start at 6.
    /// </summary>
    [SerializationConstructor]
    public ChangeFuturesContractCommand(
        Guid commandId,               // Key(0)
        ActorSubject subject,         // Key(1)
        bool postEvents,              // Key(2)
        FuturesContractId entityId,   // Key(3)
        int errorCode,                // Key(4)
        BoundedContextName routeTo,   // Key(5)
        FuturesContractId contractId, // Key(6)
        FuturesContractV2ReadModel contract, // Key(7)
        bool overwrite)               // Key(8)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ContractId = contractId;
        Contract = contract;
        Overwrite = overwrite;
    }
}