using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to change the balance of a fund by applying a specified transaction.
/// </summary>
/// <remarks>This command is used to modify the balance of a fund by processing a transaction encapsulated  in the
/// <see cref="FundTransaction" /> property. It includes metadata such as the command ID,  subject, and routing
/// information for use in distributed systems.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record ChangeFundBalanceCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "ChangeBalance";

    // Base command members (keys 0..5)
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FundId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; }
    [Key(5)] public BoundedContextName RouteTo { get; init; }

    // Ignored / derived members
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => $"{Subject.StreamId}";
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    // BaseCommand keys are 0..5. New member gets key 6.
    [Key(6)]
    public FundTransactionReadModel FundTransaction { get; init; }

    // Parameterless ctor for MessagePack deserialization via property setters.
    public ChangeFundBalanceCommand() { }

    // Convenience ctor for normal in-code use (not marked as SerializationConstructor).
    public ChangeFundBalanceCommand(FundTransactionReadModel fundTransaction)
    {
        FundTransaction = fundTransaction ?? throw new ArgumentNullException(nameof(fundTransaction));
        EntityId = new(fundTransaction.FundId);
        ErrorCode = 2003;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    // Full deserializing ctor: parameters MUST appear in ascending key order and match types.
    [SerializationConstructor]
    public ChangeFundBalanceCommand(
        Guid commandId,               // Key(0)
        ActorSubject subject,         // Key(1)
        bool postEvents,              // Key(2)
        FundId entityId,              // Key(3)
        int errorCode,                // Key(4)
        BoundedContextName routeTo,   // Key(5)
        FundTransactionReadModel fundTransaction) // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundTransaction = fundTransaction;
    }
}