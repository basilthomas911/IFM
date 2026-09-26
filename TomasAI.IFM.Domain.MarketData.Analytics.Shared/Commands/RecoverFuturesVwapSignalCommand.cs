using MessagePack;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;

/// <summary>Requests a bounded ordered exact-trade recovery batch.</summary>
[MessagePackObject(AllowPrivate = true)]
public sealed record RecoverFuturesVwapSignalCommand : ICommand<FuturesVwapSignalEntityId>
{

    /// <summary>Creates an empty command for serialization.</summary>
    public RecoverFuturesVwapSignalCommand() { }

    /// <summary>Rehydrates every published command field in permanent numeric-key order.</summary>
    /// <param name="commandId">The CommandId field.</param>
    /// <param name="subject">The Subject field.</param>
    /// <param name="postEvents">The PostEvents field.</param>
    /// <param name="entityId">The EntityId field.</param>
    /// <param name="errorCode">The ErrorCode field.</param>
    /// <param name="routeTo">The RouteTo field.</param>
    /// <param name="recoveryGenerationId">The RecoveryGenerationId field.</param>
    /// <param name="batchOrdinal">The BatchOrdinal field.</param>
    /// <param name="isFirstBatch">The IsFirstBatch field.</param>
    /// <param name="isFinalBatch">The IsFinalBatch field.</param>
    /// <param name="trades">The Trades field.</param>
    /// <param name="configuration">The Configuration field.</param>
    /// <param name="liveStreamEpochId">The LiveStreamEpochId field.</param>
    /// <param name="liveTradeOrdinal">The LiveTradeOrdinal field.</param>
    [SerializationConstructor]
    public RecoverFuturesVwapSignalCommand(Guid commandId, ActorSubject subject, bool postEvents, FuturesVwapSignalEntityId entityId, int errorCode, BoundedContextName routeTo, Guid recoveryGenerationId, long batchOrdinal, bool isFirstBatch, bool isFinalBatch, FuturesVwapTradeObservation[] trades, FuturesVwapConfiguration configuration, Guid liveStreamEpochId, long liveTradeOrdinal)
    {
        CommandId = commandId;
        Subject = subject;
        PostEvents = postEvents;
        EntityId = entityId;
        ErrorCode = errorCode;
        RouteTo = routeTo;
        RecoveryGenerationId = recoveryGenerationId;
        BatchOrdinal = batchOrdinal;
        IsFirstBatch = isFirstBatch;
        IsFinalBatch = isFinalBatch;
        Trades = trades;
        Configuration = configuration;
        LiveStreamEpochId = liveStreamEpochId;
        LiveTradeOrdinal = liveTradeOrdinal;
    }
    public const string Verb = "Recover";
    public const int ErrorId = 26401;
    [Key(0)] public Guid CommandId { get; init; }
    [Key(1)] public ActorSubject Subject { get; init; }
    [Key(2)] public bool PostEvents { get; init; }
    [Key(3)] public FuturesVwapSignalEntityId EntityId { get; init; }
    [Key(4)] public int ErrorCode { get; init; } = ErrorId;
    [Key(5)] public BoundedContextName RouteTo { get; init; } = BoundedContextName.FuturesVwapSignalBoundedContext;
    [Key(6)] public Guid RecoveryGenerationId { get; init; }
    [Key(7)] public long BatchOrdinal { get; init; }
    [Key(8)] public bool IsFirstBatch { get; init; }
    [Key(9)] public bool IsFinalBatch { get; init; }
    [Key(10)] public FuturesVwapTradeObservation[] Trades { get; init; } = [];
    [Key(11)] public FuturesVwapConfiguration Configuration { get; init; } = FuturesVwapConfiguration.Standard;
    /// <summary>Gets the live epoch that immediately follows a completed source replay.</summary>
    [Key(12)] public Guid LiveStreamEpochId { get; init; }
    /// <summary>Gets the last live ordinal included in the replay handoff.</summary>
    [Key(13)] public long LiveTradeOrdinal { get; init; }
    [IgnoreMember] public string CommandName => nameof(RecoverFuturesVwapSignalCommand);
    [IgnoreMember] public string StreamId => Subject.StreamId;
    [IgnoreMember] public string EventSource => UpdateFuturesVwapSignalCommand.Actor;
    [IgnoreMember] public DateTime OriginatedOn => DateTime.UtcNow;
    [IgnoreMember] public string OriginatedBy => $"{Environment.UserDomainName}\\{Environment.UserName}";
}
