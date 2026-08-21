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
    IApplicationCommandApi applicationCommandApi)
    : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        await actorProducer.StartAsync(new ActorMailboxId(ActorType.Query, "FuturesMarketOpen"), cancellationToken).ConfigureAwait(false);
        try
        {
            var valueDateResult = await marketDataQueryApi.GetValueDateAsync().ConfigureAwait(false);
            if (!valueDateResult.Success || valueDateResult.Value is null)
            {
                throw new InvalidOperationException($"Unable to load value date: {valueDateResult.ErrorMessage}");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var valueDate = valueDateResult.Value.Value;
            var startResult = await applicationCommandApi.StartApplicationAsync(valueDate).ConfigureAwait(false);
            if (!startResult.Success)
            {
                throw new InvalidOperationException($"Application start command was rejected: {startResult.ErrorMessage}");
            }

            logger.LogInformation("Application start command {CommandId} accepted for {ValueDate}.", startResult.Value, valueDate);
        }
        finally
        {
            await actorProducer.StopAsync().ConfigureAwait(false);
        }
    }
}
