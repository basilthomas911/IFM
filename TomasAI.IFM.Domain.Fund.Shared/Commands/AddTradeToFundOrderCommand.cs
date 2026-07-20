using MessagePack;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Fund.Shared.Commands;

/// <summary>
/// Represents a command to add a trade to a fund order.
/// </summary>
/// <remarks>This command is used to associate a specific trade with a fund order. It includes the details of the
/// trade through the <see cref="FundOrderTrade" /> property. The command is typically routed to the fund bounded context
/// for processing.</remarks>
[MessagePackObject(AllowPrivate = true)]
public record AddTradeToFundOrderCommand
    : ICommand<FundId>
{
    public const string Actor = "FundCommand";
    public const string Verb = "AddTradeToFund";
    public const int ErrorId = 2002;

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
    public FundOrderTradeReadModel FundOrderTrade { get; init; }

    public AddTradeToFundOrderCommand()
    {
    }

    // Convenience ctor for normal use (MessagePack will use the parameterless ctor)
    public AddTradeToFundOrderCommand(FundOrderTradeReadModel fundOrderTrade)
    {
        FundOrderTrade = fundOrderTrade ?? throw new ArgumentNullException(nameof(fundOrderTrade));
        EntityId = new(fundOrderTrade?.Id?.FundId ?? 0);
        ErrorCode = ErrorId;
        RouteTo = BoundedContextName.FundBoundedContext;
    }

    // Full deserializing constructor for MessagePack (keys 0..6)
    [SerializationConstructor]
    public AddTradeToFundOrderCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FundId entityId,
        int errorCode,
        BoundedContextName routeTo,
        FundOrderTradeReadModel fundOrderTrade)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        FundOrderTrade = fundOrderTrade;
    }
}