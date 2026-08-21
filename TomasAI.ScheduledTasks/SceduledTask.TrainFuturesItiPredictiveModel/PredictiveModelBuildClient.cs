using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend;
using TomasAI.IFM.Domain.PredictiveModel.Shared.FuturesItiTrend.Commands;
using TomasAI.IFM.Application.Api.Nats.Client;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.ScheduledTasks.SceduledTask.TrainFuturesItiPredictiveModel;

public sealed class PredictiveModelBuildClient(IActorProducer producer) : NatsCommandApi(producer)
{
    public async Task<ServiceResult<Guid>> BuildAsync(
        string symbol,
        DateOnly valueDate,
        DateOnly startDate,
        DateOnly endDate)
    {
        var commandId = Guid.NewGuid();
        try
        {
            var entityId = new FuturesItiTrendEntityId(symbol, valueDate);
            var command = new BuildFuturesItiTrendModelCommand(symbol, valueDate, startDate, endDate)
            {
                CommandId = commandId,
                Subject = new ActorSubject(
                    ActorType.Command,
                    BuildFuturesItiTrendModelCommand.Actor,
                    BuildFuturesItiTrendModelCommand.Verb,
                    entityId.Format())
            };
            return await RequestCommandAsync(command, entityId).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            return OnError(exception, commandId, 20014);
        }
    }
}
