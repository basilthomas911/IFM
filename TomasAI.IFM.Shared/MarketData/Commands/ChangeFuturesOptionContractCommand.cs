using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// Represents a command to update the definition of a futures option contract, including its identifier and contract
/// details.
/// </summary>
/// <remarks>This command is typically used to request changes to an existing futures option contract or to create
/// a new definition if one does not exist. The <see cref="Overwrite"/> property determines whether an existing contract
/// definition will be replaced. The command includes routing and error information for processing within a bounded
/// context. All required contract data must be provided; invalid or missing values may result in errors during
/// processing.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeFuturesOptionContractCommand
    : ICommand<FuturesOptionContractEntityId>
{
    public const string Actor = "FuturesOptionContractCommand";
    public const string Verb = "Change";
    public const int ErrorId = 6005;

    // Serialized members (base keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Default;
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Payload keys (6..)
    [Key(6)] public string ContractId { get; init; } = string.Empty;
    [Key(7)] public FuturesOptionContractReadModel Contract { get; init; }
    [Key(8)] public bool Overwrite { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Events";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Parameterless constructor required by serializers.</summary>
    public ChangeFuturesOptionContractCommand()
    {
    }

    /// <summary>
    /// Creates a new command to update a futures option contract definition.
    /// </summary>
    /// <param name="contractId">Raw contract identifier string (cannot be null or empty).</param>
    /// <param name="contract">Updated contract view model (cannot be null).</param>
    /// <param name="overwrite">Set true to overwrite existing definition.</param>
    public ChangeFuturesOptionContractCommand(
        string contractId,
        FuturesOptionContractReadModel contract,
        bool overwrite = false)
    {
        IsArgumentNull.Check(contractId);
        ContractId = contractId;
        Contract = IsArgumentNull.Set(contract);
        Overwrite = overwrite;

        // Use the contract's year-based entity id; fall back to current UTC year if not set.
        EntityId = new FuturesOptionContractEntityId(contractId, contract.ContractMonth.Year);
        RouteTo = BoundedContextName.FuturesOptionContractBoundedContext;
        ErrorCode = 6005;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public ChangeFuturesOptionContractCommand(
        Guid commandId,                         // Key(0)
        ActorSubject subject,                   // Key(1)
        bool postEvents,                        // Key(2)
        FuturesOptionContractEntityId entityId, // Key(3)
        int errorCode,                          // Key(4)
        BoundedContextName routeTo,             // Key(5)
        string contractId,                      // Key(6)
        FuturesOptionContractReadModel contract,// Key(7)
        bool overwrite)                         // Key(8)
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