using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Event.Extensions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.MarketSignals.Historical;
using TomasAI.IFM.Framework.MarketData.Contracts.Historical;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;

namespace TomasAI.IFM.Domain.MarketData.Analytics.HistoricalBootstrap.Event.Actor;

/// <summary>Runs one durable external history acquisition after its Requested event is delivered.</summary>
public sealed class FuturesAnalyticsHistoryBootstrapEventActor(
    IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor> actorContext)
    : BaseEventActor<FuturesAnalyticsHistoryBootstrapEventActor>(actorContext, actorContext.Logger)
{
    /// <summary>Gets the Event actor mailbox name.</summary>
    public const string ActorName = FuturesAnalyticsHistoryBootstrapRequestedEvent.Actor;

    /// <inheritdoc />
    protected override ValueTask OnStartup(
        IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor> context) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override ValueTask OnShutdown(
        IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor> context) => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override IEvent ParseMessage(
        IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor> context,
        IActorMessage message)
    {
        if (message.Subject is not { ActorType: ActorType.Event, Name: ActorName }) return default!;
        return message.Subject.Verb switch
        {
            FuturesAnalyticsHistoryBootstrapRequestedEvent.Verb =>
                message.AsEvent<FuturesAnalyticsHistoryBootstrapRequestedEvent>()!,
            FuturesAnalyticsHistoryBootstrapCompletedEvent.Verb =>
                message.AsEvent<FuturesAnalyticsHistoryBootstrapCompletedEvent>()!,
            FuturesAnalyticsHistoryBootstrapFailedEvent.Verb =>
                message.AsEvent<FuturesAnalyticsHistoryBootstrapFailedEvent>()!,
            _ => default!
        };
    }

    /// <inheritdoc />
    protected override async ValueTask ReceiveAsync(
        IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor> context,
        IEvent @event)
    {
        if (@event is not FuturesAnalyticsHistoryBootstrapRequestedEvent requested) return;
        var request = ToApplicationRequest(requested);
        try
        {
            var state = await context.BootstrapCoordinator.ExecuteAsync(
                request, CancellationToken.None).ConfigureAwait(false);
            var terminal = new FuturesAnalyticsHistoryBootstrapCompletedEvent
            {
                Subject = new(ActorType.Event, ActorName,
                    FuturesAnalyticsHistoryBootstrapCompletedEvent.Verb, requested.EntityId.Format()),
                Id = Guid.NewGuid(), EntityId = requested.EntityId,
                CommandId = requested.CommandId, AggregateId = requested.EntityId.Format(),
                EventSource = nameof(FuturesAnalyticsHistoryBootstrapEventActor),
                ReceivedOn = DateTime.UtcNow,
                ManifestId = state.Manifest?.ManifestId ?? Guid.Empty,
                ValidSessionCount = state.Audit?.ValidSessionCount ?? 0,
                GapCount = state.Audit?.Gaps.Count ?? 0,
                RollCount = state.Audit?.Rolls.Count ?? 0,
                RequestSha256 = state.RequestSha256,
                CompletedAtUtc = DateTime.UtcNow
            };
            await context.SendAsync<
                FuturesAnalyticsHistoryBootstrapCompletedEvent,
                FuturesAnalyticsHistoryBootstrapEntityId>(terminal).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var state = await context.BootstrapStore.GetAsync(
                requested.EntityId.Value, CancellationToken.None).ConfigureAwait(false);
            var terminal = new FuturesAnalyticsHistoryBootstrapFailedEvent
            {
                Subject = new(ActorType.Event, ActorName,
                    FuturesAnalyticsHistoryBootstrapFailedEvent.Verb, requested.EntityId.Format()),
                Id = Guid.NewGuid(), EntityId = requested.EntityId,
                CommandId = requested.CommandId, AggregateId = requested.EntityId.Format(),
                EventSource = nameof(FuturesAnalyticsHistoryBootstrapEventActor),
                ReceivedOn = DateTime.UtcNow, ErrorMessage = exception.Message,
                LastCompletedBatchOrdinal = checked((int)(state?.Checkpoint.BatchOrdinal ?? -1)),
                LastCompletedRecordOrdinal = ParseRecordOrdinal(state?.Checkpoint.SourcePosition)
            };
            await context.SendAsync<
                FuturesAnalyticsHistoryBootstrapFailedEvent,
                FuturesAnalyticsHistoryBootstrapEntityId>(terminal).ConfigureAwait(false);
        }
    }

    static MarketDataHistoricalRequest ToApplicationRequest(
        FuturesAnalyticsHistoryBootstrapRequestedEvent requested) => new()
    {
        BootstrapAttemptId = requested.EntityId.Value,
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
        IEventActorContext<FuturesAnalyticsHistoryBootstrapEventActor> context,
        ActorThreadId threadId,
        IEvent @event,
        Exception exception) => await exception.SendErrorEventAsync<
            TomasAI.IFM.Shared.EventModelActor.Events.EventExceptionEvent,
            ActorEntityId>(ErrorType.EventService, context).ConfigureAwait(false);
}
