using MessagePack;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;
namespace TomasAI.IFM.Domain.MarketData.Feed.Shared.Recovery;
/// <summary>Records one immutable fact with a fresh delivery attempt and a common persistence deadline.</summary>
[MessagePackObject]
public sealed record AppendMarketDataFeedRecoveryCommand : ICommand<MarketDataFeedRecoveryId>, ICommandRetryIdentity
{
    public const string Actor = "MarketDataFeedRecoveryCommand";
    public const string Verb = "Record";
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public MarketDataFeedRecoveryId EntityId { get; init; } = new(Guid.Empty);
    [Key(4)] public int ErrorCode { get; init; } = 5090;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.MarketDataFeedBoundedContext;
    [Key(6)] public string Payload { get; init; } = "";
    [Key(7)] public string Digest { get; init; } = "";
    [Key(8)] public Guid AttemptId { get; init; }
    [Key(9)] public DateTime AttemptStartedAtUtc { get; init; }
    [Key(10)] public DateTime RequestDeadlineUtc { get; init; }
    /// <summary>Server-captured receipt time; never supplied as an incident's occurrence time.</summary>
    [Key(11)] public DateTime ReceivedAtUtc { get; init; }
    public ICommand ForRetryIdentity() => this with { CommandId = Guid.Empty, AttemptId = Guid.Empty, AttemptStartedAtUtc = default, RequestDeadlineUtc = default, ReceivedAtUtc = default };
    [IgnoreMember] public string CommandName => GetType().Name;
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => Actor+"Actor";
    [IgnoreMember] public DateTime OriginatedOn => AttemptStartedAtUtc;
    [IgnoreMember] public string OriginatedBy => "NativeRecoveryOutbox";
}
