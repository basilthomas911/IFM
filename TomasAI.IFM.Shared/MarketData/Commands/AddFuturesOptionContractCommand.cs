using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.MarketData.ViewModels;

namespace TomasAI.IFM.Shared.MarketData.Commands;

/// <summary>
/// Command to add (or overwrite) a single futures option contract definition.
/// </summary>
/// <remarks>
/// MessagePack serialization pattern: base command keys 0�5; custom properties start at key 6.
/// Routes to <see cref="BoundedContextName.FuturesOptionContractBoundedContext"/> with error code 7789.
/// </remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddFuturesOptionContractCommand
    : ICommand<FuturesOptionContractEntityId>
{
    public const string Actor = "FuturesOptionContractCommand";
    public const string Verb = "Add";
    public const int ErrorId = 7789;

    // Serialized members (base keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; } = ActorSubject.Default;
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesOptionContractEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Payload keys (6..)
    [Key(6)] public FuturesOptionContractReadModel Contract { get; init; }
    [Key(7)] public bool Overwrite { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Events";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    /// <summary>Parameterless constructor required by serializers.</summary>
    public AddFuturesOptionContractCommand()
    {
    }

    /// <summary>
    /// Creates a new command to add a futures option contract.
    /// </summary>
    /// <param name="contract">The futures option contract definition (cannot be null).</param>
    /// <param name="overwrite">Set true to overwrite an existing definition.</param>
    public AddFuturesOptionContractCommand(FuturesOptionContractReadModel contract, bool overwrite = false)
    {
        Contract = IsArgumentNull.Set(contract);
        Overwrite = overwrite;

        // Derive entity id by contract year (falls back to current year if missing).
        EntityId = Contract.EntityId;

        RouteTo = BoundedContextName.FuturesOptionContractBoundedContext;
        ErrorCode = 7789;
    }

    /// <summary>
    /// MessagePack serialization constructor (indices must match Key attributes).
    /// </summary>
    [SerializationConstructor]
    public AddFuturesOptionContractCommand(
        Guid commandId,                         // Key(0)
        ActorSubject subject,                   // Key(1)
        bool postEvents,                        // Key(2)
        FuturesOptionContractEntityId entityId, // Key(3)
        int errorCode,                          // Key(4)
        BoundedContextName routeTo,             // Key(5)
        FuturesOptionContractReadModel contract,// Key(6)
        bool overwrite)                         // Key(7)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        Contract = contract;
        Overwrite = overwrite;
    }
}