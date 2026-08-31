using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.HistoricalDataLoader;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalDataLoader.Event.Actor;

/// <summary>Runs one durable external history acquisition after its Requested event is delivered.</summary>
public sealed class FuturesAnalyticsHistoricalDataLoaderEventActor(
    IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> actorContext)
    : BaseEventActor<FuturesAnalyticsHistoricalDataLoaderEventActor>(actorContext, actorContext.Logger)
{
    static readonly TimeSpan AutomaticRequestMaximumAge = TimeSpan.FromMinutes(5);
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string ActorName = FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor;

    static readonly IReadOnlyDictionary<string, Func<IActorMessage, IEvent>> _parseMap =
        new Dictionary<string, Func<IActorMessage, IEvent>>(StringComparer.Ordinal)
        {
            [FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Verb] = static message =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderRequestedEvent>()!,
            [FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Verb] = static message =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderCompletedEvent>()!,
            [FuturesAnalyticsHistoricalDataLoaderFailedEvent.Verb] = static message =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderFailedEvent>()!
        };

    static readonly IReadOnlyDictionary<Type, Func<
        FuturesAnalyticsHistoricalDataLoaderEventActor,
        IEvent,
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor>,
        ValueTask>> _receiveMap = new Dictionary<Type, Func<
            FuturesAnalyticsHistoricalDataLoaderEventActor,
            IEvent,
            IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor>,
            ValueTask>>
        {
            [typeof(FuturesAnalyticsHistoricalDataLoaderRequestedEvent)] = static (actor, @event, context) =>
                actor.ReceiveRequestedAsync(
                    context,
                    (FuturesAnalyticsHistoricalDataLoaderRequestedEvent)@event),
            [typeof(FuturesAnalyticsHistoricalDataLoaderCompletedEvent)] = static (_, _, _) =>
                ValueTask.CompletedTask,
            [typeof(FuturesAnalyticsHistoricalDataLoaderFailedEvent)] = static (_, _, _) =>
                ValueTask.CompletedTask
        };

    /// <inheritdoc />
    protected override ValueTask OnStartup(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        IActorMessage message)
        => ParseMappedEvent(context, message, _parseMap);

    /// <inheritdoc />
    protected override ValueTask ReceiveAsync(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        IEvent @event)
    {
        var receive = ResolveMappedEventHandler(@event, _receiveMap);
        return receive(this, @event, context);
    }

    async ValueTask ReceiveRequestedAsync(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested)
    {
        var request = ToApplicationRequest(requested);
        context.Logger.LogInformation(
            "Received historical Analytics request {AttemptId}; automatic={AutomaticStartupWarmup}, received={ReceivedOnUtc}, target={AnalyticsTargetContractId}.",
            requested.EntityId.Value,
            requested.Parameters.AutomaticStartupWarmup,
            requested.ReceivedOn,
            requested.Parameters.AnalyticsTargetContractId);
        try
        {
            if (requested.Parameters.AutomaticStartupWarmup
                && requested.ReceivedOn != default
                && DateTime.UtcNow - DateTime.SpecifyKind(requested.ReceivedOn, DateTimeKind.Utc)
                    > AutomaticRequestMaximumAge)
            {
                context.Logger.LogInformation(
                    "Ignoring stale automatic historical Analytics request {AttemptId} received at {ReceivedOnUtc}.",
                    requested.EntityId.Value,
                    requested.ReceivedOn);
                return;
            }

            HistoricalAnalyticsWarmupResult? warmupResult = null;
            HistoricalDataLoaderState state;
            if (requested.Parameters.AutomaticStartupWarmup)
            {
                context.Logger.LogInformation(
                    "Writing historical Analytics coverage-scan checkpoint for {AttemptId}.",
                    requested.EntityId.Value);
                await context.DataLoaderStore.SaveAsync(new HistoricalDataLoaderState
                {
                    DataLoadAttemptId = requested.EntityId.Value,
                    RequestSha256 = $"coverage-scan:{requested.EntityId.Value:D}",
                    Status = HistoricalDataLoaderStatus.Processing,
                    Checkpoint = new HistoricalAcquisitionCheckpoint
                    {
                        DataLoadAttemptId = requested.EntityId.Value,
                        Stage = HistoricalAcquisitionStage.None
                    },
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, CancellationToken.None).ConfigureAwait(false);
                context.Logger.LogInformation(
                    "Historical Analytics coverage-scan checkpoint written for {AttemptId}.",
                    requested.EntityId.Value);
                warmupResult = await context.WarmupService.EnsureAsync(
                    request, CancellationToken.None).ConfigureAwait(false);
                state = ToState(requested, warmupResult);
                await context.DataLoaderStore.SaveAsync(new HistoricalDataLoaderState
                {
                    DataLoadAttemptId = requested.EntityId.Value,
                    RequestSha256 = $"automatic:{requested.EntityId.Value:D}:{warmupResult.Outcome}:{warmupResult.StartDate:O}:{warmupResult.EndDate:O}",
                    Status = HistoricalDataLoaderStatus.Completed,
                    Checkpoint = new HistoricalAcquisitionCheckpoint
                    {
                        DataLoadAttemptId = requested.EntityId.Value,
                        Stage = HistoricalAcquisitionStage.Completed
                    },
                    Audit = new HistoricalDataLoaderAudit(
                        warmupResult.ValidSessionCount,
                        [],
                        []),
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                }, CancellationToken.None).ConfigureAwait(false);
                context.Logger.LogInformation(
                    "Historical Analytics warm-up {Outcome} for {StartDate} through {EndDate}; valid ES sessions {ValidSessionCount}, initially missing sessions {MissingSessionCount}.",
                    warmupResult.Outcome,
                    warmupResult.StartDate,
                    warmupResult.EndDate,
                    warmupResult.ValidSessionCount,
                    warmupResult.MissingSessionCount);
            }
            else
            {
                state = await context.DataLoader.ExecuteAsync(
                    request, CancellationToken.None).ConfigureAwait(false);
            }
            var terminal = new FuturesAnalyticsHistoricalDataLoaderCompletedEvent
            {
                Subject = new(ActorType.Event, ActorName,
                    FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Verb, requested.EntityId.Format()),
                Id = Guid.NewGuid(), EntityId = requested.EntityId,
                CommandId = requested.CommandId, AggregateId = requested.EntityId.Format(),
                EventSource = nameof(FuturesAnalyticsHistoricalDataLoaderEventActor),
                ReceivedOn = DateTime.UtcNow,
                ManifestId = state.Manifest?.ManifestId ?? Guid.Empty,
                ValidSessionCount = state.Audit?.ValidSessionCount ?? 0,
                GapCount = state.Audit?.Gaps.Count ?? 0,
                RollCount = state.Audit?.Rolls.Count ?? 0,
                RequestSha256 = state.RequestSha256,
                CompletedAtUtc = DateTime.UtcNow
            };
            await context.SendAsync<
                FuturesAnalyticsHistoricalDataLoaderCompletedEvent,
                FuturesAnalyticsHistoricalDataLoaderEntityId>(terminal).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            context.Logger.LogError(
                exception,
                "Historical Analytics request {AttemptId} failed before terminal publication.",
                requested.EntityId.Value);
            var state = await context.DataLoaderStore.GetAsync(
                requested.EntityId.Value, CancellationToken.None).ConfigureAwait(false);
            if (state is not null)
            {
                state = state with
                {
                    Status = HistoricalDataLoaderStatus.Failed,
                    ErrorMessage = exception.Message,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                await context.DataLoaderStore.SaveAsync(state, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            var terminal = new FuturesAnalyticsHistoricalDataLoaderFailedEvent
            {
                Subject = new(ActorType.Event, ActorName,
                    FuturesAnalyticsHistoricalDataLoaderFailedEvent.Verb, requested.EntityId.Format()),
                Id = Guid.NewGuid(), EntityId = requested.EntityId,
                CommandId = requested.CommandId, AggregateId = requested.EntityId.Format(),
                EventSource = nameof(FuturesAnalyticsHistoricalDataLoaderEventActor),
                ReceivedOn = DateTime.UtcNow, ErrorMessage = exception.Message,
                LastCompletedBatchOrdinal = checked((int)(state?.Checkpoint.BatchOrdinal ?? -1)),
                LastCompletedRecordOrdinal = ParseRecordOrdinal(state?.Checkpoint.SourcePosition)
            };
            await context.SendAsync<
                FuturesAnalyticsHistoricalDataLoaderFailedEvent,
                FuturesAnalyticsHistoricalDataLoaderEntityId>(terminal).ConfigureAwait(false);
        }
    }

    static MarketDataHistoricalRequest ToApplicationRequest(
        FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested) => new()
    {
        DataLoadAttemptId = requested.EntityId.Value,
        Series = requested.Parameters.Series.Select(value => new MarketDataHistoricalSeriesRequest
        {
            SeriesIdentity = value.MarketSeriesIdentity,
            ContractId = value.ContractId,
            Schema = value.Schema switch
            {
                FuturesAnalyticsHistoricalSchema.OhlcvOneMinute => HistoricalDataSchema.OhlcvOneMinute,
                FuturesAnalyticsHistoricalSchema.Trades => HistoricalDataSchema.Trades,
                FuturesAnalyticsHistoricalSchema.OhlcvDaily => HistoricalDataSchema.OhlcvDaily,
                _ => throw new InvalidOperationException($"Unsupported historical schema {value.Schema}.")
            },
            ExactTradesRequired = value.ExactTradesRequired || requested.Parameters.ExactVwapRequired
        }).ToArray(),
        StartDate = requested.Parameters.StartDate,
        EndDate = requested.Parameters.EndDate,
        MaximumCostUsd = requested.Parameters.MaximumCostUsd,
        MaximumBytes = requested.Parameters.MaximumBytes,
        NormalizationVersion = requested.Parameters.NormalizationVersion,
        RequestedBy = requested.Parameters.RequestedBy,
        AnalyticsTargetContractId = requested.Parameters.AnalyticsTargetContractId
    };

    static long ParseRecordOrdinal(string? sourcePosition) =>
        long.TryParse(sourcePosition, out var value) ? value : -1;

    static HistoricalDataLoaderState ToState(
        FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested,
        HistoricalAnalyticsWarmupResult result)
        => result.LastLoadState ?? new HistoricalDataLoaderState
        {
            DataLoadAttemptId = requested.EntityId.Value,
            RequestSha256 = $"automatic:{result.Outcome}:{result.StartDate:O}:{result.EndDate:O}",
            Status = HistoricalDataLoaderStatus.Completed,
            Checkpoint = new HistoricalAcquisitionCheckpoint
            {
                DataLoadAttemptId = requested.EntityId.Value,
                Stage = HistoricalAcquisitionStage.Completed
            },
            Audit = new HistoricalDataLoaderAudit(result.ValidSessionCount, [], []),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
