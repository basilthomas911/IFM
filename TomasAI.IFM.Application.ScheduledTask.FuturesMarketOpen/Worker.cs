using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.Events;
using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.IFM.Application.ScheduledTask.FuturesMarketOpen;

public sealed class Worker(
    IHostApplicationLifetime lifetime,
    ScheduledTaskOutcome outcome,
    ILogger<Worker> logger,
    IActorProducer actorProducer,
    IMarketDataFeedCommandApi feedCommands,
    TomasAI.IFM.Domain.MarketData.Feed.Shared.ServiceApi.IMarketDataFeedQueryApi feedQueries,
    ScheduledTaskEventCompletion completion,
    ScheduledTaskBusinessReceipts receipts,
    IHostEnvironment environment)
    : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        await actorProducer.StartAsync(new ActorMailboxId(ActorType.Query, "FuturesMarketOpen"), cancellationToken).ConfigureAwait(false);
        try
        {
            var requiresAdmission = ScheduledTaskExecutionPolicy.RequiresAdmission(ScheduledTaskKeys.FuturesMarketOpen, environment.EnvironmentName);
            if (requiresAdmission)
                await receipts.WaitForRunningAsync(cancellationToken);
            else
                logger.LogInformation("Development Market Open starting immediately; runtime admission checks are disabled.");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromMinutes(10));
            // Execution is explicit: the API resolves its own date and assignments, regardless of clock/session state.
            var started = await completion.ExecuteAsync<MarketDataFeedStartedCompleteEvent, MarketDataFeedStartedFailEvent>(
                MarketDataFeedStartedCompleteEvent.Actor, MarketDataFeedStartedCompleteEvent.Verb, MarketDataFeedStartedFailEvent.Verb,
                () => feedCommands.StartMarketDataFeedAsync(), deadline.Token);
            var valueDate = started.EntityId.ValueDate;
            var startCommandId = started.CommandId;
            using var feedDeadline = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
            feedDeadline.CancelAfter(TimeSpan.FromMinutes(1));
            while (true)
            {
                var runtime = await feedQueries.GetRuntimeStatusAsync().WaitAsync(feedDeadline.Token);
                var readiness = await feedQueries.GetDatabentoReadinessAsync().WaitAsync(feedDeadline.Token);
                if (runtime.Success && readiness.Success && ScheduledMarketOpenReadiness.IsReady(runtime.Value, readiness.Value, valueDate)) break;
                await Task.Delay(TimeSpan.FromSeconds(1), feedDeadline.Token);
            }
            if (requiresAdmission || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IFM_SCHEDULED_RUN_ID")))
                await receipts.RecordAsync(valueDate, "FeedsStarted", "Healthy subscribed GLBX.MDP3 generation and session-specific ADX/ATR/MACD/RSI/TDI initialization confirmed for the API-selected value date; unrelated application startup activities were not repeated.", deadline.Token);
            logger.LogInformation("{Component}.{Method} Databento market feed ready for {ValueDate} {CommandId}.", nameof(Worker), nameof(ExecuteTaskAsync), valueDate, startCommandId);

        }
        finally
        {
            await actorProducer.StopAsync().ConfigureAwait(false);
        }
    }
}
