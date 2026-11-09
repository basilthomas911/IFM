using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Records the completed outcome of one historical load attempt.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record CompleteFuturesAnalyticsHistoricalDataLoaderCommand : ICommand<FuturesAnalyticsHistoricalDataLoaderEntityId>
{
    public const string Actor = LoadFuturesAnalyticsHistoricalDataCommand.Actor;
    public const string Verb = "Complete";
    public const int ErrorId = 26022;

    public CompleteFuturesAnalyticsHistoricalDataLoaderCommand() { }

    [SerializationConstructor]
    public CompleteFuturesAnalyticsHistoricalDataLoaderCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesAnalyticsHistoricalDataLoaderEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        Guid manifestId,
        int validSessionCount,
        int gapCount,
        int rollCount,
        string requestSha256,
        DateTime completedAtUtc, int lastCompletedBatchOrdinal = -1, long lastCompletedRecordOrdinal = -1)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ManifestId = manifestId;
        ValidSessionCount = validSessionCount;
        GapCount = gapCount;
        RollCount = rollCount;
        RequestSha256 = requestSha256;
        CompletedAtUtc = completedAtUtc;
        LastCompletedBatchOrdinal = lastCompletedBatchOrdinal;
        LastCompletedRecordOrdinal = lastCompletedRecordOrdinal;
    }

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public Guid ManifestId { get; init; }
    [Key(7)] public int ValidSessionCount { get; init; }
    [Key(8)] public int GapCount { get; init; }
    [Key(9)] public int RollCount { get; init; }
    [Key(10)] public string RequestSha256 { get; init; } = string.Empty;
    [Key(11)] public DateTime CompletedAtUtc { get; init; }
    [Key(12)] public int LastCompletedBatchOrdinal { get; init; } = -1;
    [Key(13)] public long LastCompletedRecordOrdinal { get; init; } = -1;

    [IgnoreMember] public string CommandName => nameof(CompleteFuturesAnalyticsHistoricalDataLoaderCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => LoadFuturesAnalyticsHistoricalDataCommand.Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [IgnoreMember] public string OriginatedBy => nameof(CompleteFuturesAnalyticsHistoricalDataLoaderCommand);
}
