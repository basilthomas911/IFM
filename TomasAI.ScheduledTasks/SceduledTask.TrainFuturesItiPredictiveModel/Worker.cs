using TomasAI.IFM.Application.ScheduledTask.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;

namespace TomasAI.ScheduledTasks.SceduledTask.TrainFuturesItiPredictiveModel;

public sealed class Worker(
    IHostApplicationLifetime lifetime,
    ScheduledTaskOutcome outcome,
    ILogger<Worker> logger,
    IActorProducer actorProducer,
    IMarketDataQueryApi marketDataQueryApi,
    PredictiveModelBuildClient commandApi,
    IConfiguration configuration)
    : OneShotScheduledTaskWorker(lifetime, outcome, logger)
{
    protected override async Task ExecuteTaskAsync(CancellationToken cancellationToken)
    {
        await actorProducer.StartAsync(
            new ActorMailboxId(ActorType.Query, "TrainFuturesItiPredictiveModel"),
            cancellationToken).ConfigureAwait(false);
        try
        {
            var valueDateResult = await marketDataQueryApi.GetValueDateAsync().ConfigureAwait(false);
            if (!valueDateResult.Success || valueDateResult.Value is null)
            {
                throw new InvalidOperationException($"Unable to load value date: {valueDateResult.ErrorMessage}");
            }

            var symbol = configuration["PredictiveModel:Symbol"] ?? "ES";
            var startDateText = configuration["PredictiveModel:TrainingStartDate"]
                ?? throw new InvalidOperationException("PredictiveModel:TrainingStartDate is required.");
            if (!DateOnly.TryParse(startDateText, out var startDate))
            {
                throw new InvalidOperationException("PredictiveModel:TrainingStartDate must be an ISO date.");
            }

            var valueDate = valueDateResult.Value.Value;
            if (startDate >= valueDate)
            {
                throw new InvalidOperationException("Training start date must precede the current value date.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var result = await commandApi.BuildAsync(symbol, valueDate, startDate, valueDate).ConfigureAwait(false);
            if (!result.Success)
            {
                throw new InvalidOperationException($"Predictive-model build command was rejected: {result.ErrorMessage}");
            }

            logger.LogInformation("Predictive-model build command {CommandId} accepted for {Symbol}.", result.Value, symbol);
        }
        finally
        {
            await actorProducer.StopAsync().ConfigureAwait(false);
        }
    }
}
