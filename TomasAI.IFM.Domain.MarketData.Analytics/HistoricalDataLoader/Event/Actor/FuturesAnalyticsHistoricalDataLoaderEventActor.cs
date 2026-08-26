using TomasAI.IFM.Application.MarketData.Contracts.Historical;
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
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string ActorName = FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Actor;

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
    {
        if (message.Subject is not { ActorType: ActorType.Event, Name: ActorName }) return default!;
        return message.Subject.Verb switch
        {
            FuturesAnalyticsHistoricalDataLoaderRequestedEvent.Verb =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderRequestedEvent>()!,
            FuturesAnalyticsHistoricalDataLoaderCompletedEvent.Verb =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderCompletedEvent>()!,
            FuturesAnalyticsHistoricalDataLoaderFailedEvent.Verb =>
                message.AsEvent<FuturesAnalyticsHistoricalDataLoaderFailedEvent>()!,
            _ => default!
        };
    }

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        IEvent @event)
    {
        if (@event is not FuturesAnalyticsHistoricalDataLoaderRequestedEvent requested) return;
        var request = ToApplicationRequest(requested);
        try
        {
            var state = await context.DataLoader.ExecuteAsync(
                request, CancellationToken.None).ConfigureAwait(false);
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
            var state = await context.DataLoaderStore.GetAsync(
                requested.EntityId.Value, CancellationToken.None).ConfigureAwait(false);
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
                _ => throw new InvalidOperationException($"Unsupported historical schema {value.Schema}.")
            },
            ExactTradesRequired = value.ExactTradesRequired || requested.Parameters.ExactVwapRequired
        }).ToArray(),
        StartDate = requested.Parameters.StartDate,
        EndDate = requested.Parameters.EndDate,
        MaximumCostUsd = requested.Parameters.MaximumCostUsd,
        MaximumBytes = requested.Parameters.MaximumBytes,
        NormalizationVersion = requested.Parameters.NormalizationVersion,
        RequestedBy = requested.Parameters.RequestedBy
    };

    static long ParseRecordOrdinal(string? sourcePosition) =>
        long.TryParse(sourcePosition, out var value) ? value : -1;

    /// <inheritdoc />
    protected override async ValueTask OnExceptionAsync(
        IEventActorContext<FuturesAnalyticsHistoricalDataLoaderEventActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
