using TomasAI.IFM.Domain.Application.Shared.Events;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.Application.Shared.ServiceApi;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen;

public sealed class Worker(
    IHostApplicationLifetime lifetime,
    ScheduledTaskOutcome outcome,
    ILogger<Worker> logger,
    IActorProducer actorProducer,
    IMarketDataQueryApi marketDataQueryApi,
    IApplicationCommandApi applicationCommandApi,
    TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi feedQueries,
    ScheduledTaskEventCompletion completion,
    ScheduledTaskBusinessReceipts receipts)
    : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        await actorProducer.StartAsync(new ActorMailboxId(ActorType.Query, "FuturesMarketOpen"), cancellationToken).ConfigureAwait(false);
        try
        {
            await receipts.WaitForRunningAsync(cancellationToken);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            var session = await marketDataQueryApi.GetMarketSessionAsync().WaitAsync(deadline.Token);
            if (!session.Success || session.Value is null) throw new InvalidOperationException("Unable to load authoritative market session: " + session.ErrorMessage);
            if (session.Value.IsEndOfDayPending || session.Value.ActiveValueDate != session.Value.OperationalValueDate)
                throw new InvalidOperationException("Market open is waiting for successful EOD completion of the prior operational value date.");
            var valueDate = session.Value.OperationalValueDate;
            var started = await completion.ExecuteAsync<ApplicationStartupCompleteEvent,ApplicationStartupFailEvent>(
                ApplicationStartupCompleteEvent.Actor, ApplicationStartupCompleteEvent.Verb, ApplicationStartupFailEvent.Verb,
                () => applicationCommandApi.StartApplicationAsync(valueDate), deadline.Token);
            if (started.EntityId.ValueDate != valueDate) throw new InvalidOperationException("Application startup completed for a different value date.");
            using var feedDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            feedDeadline.CancelAfter(TimeSpan.FromMinutes(1));
            while (true)
            {
                var runtime = await feedQueries.GetRuntimeStatusAsync().WaitAsync(feedDeadline.Token);
                var readiness = await feedQueries.GetDatabentoReadinessAsync().WaitAsync(feedDeadline.Token);
                if (runtime.Success && readiness.Success && ScheduledMarketOpenReadiness.IsReady(runtime.Value, readiness.Value, valueDate)) break;
                await Task.Delay(TimeSpan.FromSeconds(1), feedDeadline.Token);
            }
            await receipts.RecordAsync(valueDate, "ApplicationStarted", "Correlated application startup and healthy subscribed GLBX.MDP3 generation confirmed for the admitted operational value date.", deadline.Token);
            logger.LogInformation("{Component}.{Method} Application startup completed for {ValueDate} {CommandId}.", nameof(Worker), nameof(ExecuteTaskAsync), valueDate, started.CommandId);

        }
        finally
        {
            await actorProducer.StopAsync().ConfigureAwait(false);
        }
    }
}
