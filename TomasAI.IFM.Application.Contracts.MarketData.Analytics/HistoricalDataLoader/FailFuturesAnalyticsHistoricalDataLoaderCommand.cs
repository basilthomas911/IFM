using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;

/// <summary>Records the failed outcome of one historical load attempt.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record FailFuturesAnalyticsHistoricalDataLoaderCommand : ICommand<FuturesAnalyticsHistoricalDataLoaderEntityId>
{
    public const string Actor = LoadFuturesAnalyticsHistoricalDataCommand.Actor;
    public const string Verb = "Fail";
    public const int ErrorId = 26023;

    public FailFuturesAnalyticsHistoricalDataLoaderCommand() { }

    [SerializationConstructor]
    public FailFuturesAnalyticsHistoricalDataLoaderCommand(
        Guid commandId,
        ActorSubject subject,
        bool postEvents,
        FuturesAnalyticsHistoricalDataLoaderEntityId entityId,
        int errorCode,
        BoundedContextName routeTo,
        string errorMessage,
        int lastCompletedBatchOrdinal,
        long lastCompletedRecordOrdinal )
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        ErrorMessage = errorMessage;
        LastCompletedBatchOrdinal = lastCompletedBatchOrdinal;
        LastCompletedRecordOrdinal = lastCompletedRecordOrdinal;
    }

    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; } = true;
    [Key(3)] public FuturesAnalyticsHistoricalDataLoaderEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; }
    [Key(6)] public string ErrorMessage { get; init; } = string.Empty;
    [Key(7)] public int LastCompletedBatchOrdinal { get; init; }
    [Key(8)] public long LastCompletedRecordOrdinal { get; init; }

    [IgnoreMember] public string CommandName => nameof(FailFuturesAnalyticsHistoricalDataLoaderCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => LoadFuturesAnalyticsHistoricalDataCommand.Actor + "Actor";
    [IgnoreMember] public DateTime OriginatedOn => global::TomasAI.IFM.Shared.EventModelActor.MessageOrigins.Get(this)?.OriginatedAtUtc.UtcDateTime ?? default;
    [IgnoreMember] public string OriginatedBy => nameof(FailFuturesAnalyticsHistoricalDataLoaderCommand);
}
