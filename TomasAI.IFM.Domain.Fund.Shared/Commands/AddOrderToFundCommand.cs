using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to add an order to a specific fund.
/// </summary>
/// <remarks>This command encapsulates the details of a fund order and provides the necessary context for
/// processing the operation within the fund bounded context.</remarks>
/// <param name="fundOrder"></param>
[MessagePackObject(AllowPrivate = true)]
public record AddOrderToFundCommand : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "AddOrderToFund";
    public const int ErrorId = 2001;

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

    [Key(6)]
    public FundOrderReadModel FundOrder { get; init; }

    public AddOrderToFundCommand() { }

    public AddOrderToFundCommand(FundOrderReadModel fundOrder)
    {
        FundOrder = fundOrder ?? throw new ArgumentNullException(nameof(fundOrder));
        EntityId = new(FundOrder?.Id?.FundId ?? 0);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    /*
    [SerializationConstructor]
    public AddOrderToFundCommand(
        Guid commandId,                 // Key(0)
        ActorSubject subject,           // Key(1)
        bool postEvents,                // Key(2)
        FundId entityId,                // Key(3)
        int errorCode,                  // Key(4)
        BoundedContextName routeTo,     // Key(5)
        FundOrderReadModel fundOrder)   // Key(6)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundOrder = fundOrder;
    }
    */
}
