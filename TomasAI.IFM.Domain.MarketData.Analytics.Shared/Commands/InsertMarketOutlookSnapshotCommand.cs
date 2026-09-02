using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Persists one complete Market Outlook display snapshot.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record InsertMarketOutlookSnapshotCommand : ICommand<MarketOutlookEntityId>
{
    public const string Actor = "MarketOutlookSnapshotCommand";
    public const string Verb = "Insert";
    public const int ErrorId = 20040;

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public MarketOutlookEntityId EntityId { get; init; } = new();
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.MarketOutlookSnapshotBoundedContext;
    [Key(6)] public MarketOutlookReadModel MarketOutlook { get; init; } = new();

    [IgnoreMember] public string CommandName => nameof(InsertMarketOutlookSnapshotCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => $"{Actor}Actor";
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";

    public InsertMarketOutlookSnapshotCommand() { }

    public InsertMarketOutlookSnapshotCommand(MarketOutlookReadModel marketOutlook)
    {
        ArgumentNullException.ThrowIfNull(marketOutlook);
        CommandId = Guid.NewGuid();
        MarketOutlook = marketOutlook;
        EntityId = new(marketOutlook.ContractId, marketOutlook.ValueDate);
        Subject = new(ActorType.Command, Actor, Verb, EntityId.Format());
    }

    [SerializationConstructor]
    public InsertMarketOutlookSnapshotCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        MarketOutlookEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        MarketOutlookReadModel marketOutlook)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        MarketOutlook = marketOutlook;
    }
}
