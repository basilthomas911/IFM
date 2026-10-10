using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
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
    IMarketDataFeedCommandApi feedCommands,
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
            ScheduledMarketOpenReadiness.ValidateSession(session.Value);
            var valueDate = session.Value.OperationalValueDate;
            var currentRuntime = await feedQueries.GetRuntimeStatusAsync().WaitAsync(deadline.Token);
            var currentReadiness = await feedQueries.GetDatabentoReadinessAsync().WaitAsync(deadline.Token);
            if (!currentRuntime.Success || !currentReadiness.Success)
                throw new InvalidOperationException("Market open could not verify the current Databento feed state.");
            Guid? startCommandId = null;
            if (!ScheduledMarketOpenReadiness.IsReady(currentRuntime.Value, currentReadiness.Value, valueDate))
            {
                if (currentRuntime.Value is null || currentReadiness.Value is null)
                    throw new InvalidOperationException("Market open requires an observed Databento feed state.");
                if (currentRuntime.Value.IsRunning)
                    throw new InvalidOperationException("A Databento feed is already running but is not ready for the admitted value date; feed recovery is required.");
                // Feed lifecycle resolves its authoritative date-specific contract manifests.
                // The scheduled task does not run application initialization or reference imports.
                var started = await completion.ExecuteAsync<MarketDataFeedStartedCompleteEvent, MarketDataFeedStartedFailEvent>(
                    MarketDataFeedStartedCompleteEvent.Actor, MarketDataFeedStartedCompleteEvent.Verb, MarketDataFeedStartedFailEvent.Verb,
                    () => feedCommands.StartMarketDataFeedAsync([], valueDate), deadline.Token);
                if (started.EntityId.ValueDate != valueDate)
                    throw new InvalidOperationException("Databento feed start completed for a different value date.");
                startCommandId = started.CommandId;
            }
            using var feedDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            feedDeadline.CancelAfter(TimeSpan.FromMinutes(1));
            while (true)
            {
                var runtime = await feedQueries.GetRuntimeStatusAsync().WaitAsync(feedDeadline.Token);
                var readiness = await feedQueries.GetDatabentoReadinessAsync().WaitAsync(feedDeadline.Token);
                if (runtime.Success && readiness.Success && ScheduledMarketOpenReadiness.IsReady(runtime.Value, readiness.Value, valueDate)) break;
                await Task.Delay(TimeSpan.FromSeconds(1), feedDeadline.Token);
            }
            await receipts.RecordAsync(valueDate, "FeedsStarted", "Healthy subscribed GLBX.MDP3 generation confirmed for the admitted operational value date; application initialization was not repeated.", deadline.Token);
            logger.LogInformation("{Component}.{Method} Databento market feed ready for {ValueDate} {CommandId}.", nameof(Worker), nameof(ExecuteTaskAsync), valueDate, startCommandId);

        }
        finally
        {
            await actorProducer.StopAsync().ConfigureAwait(false);
        }
    }
}
