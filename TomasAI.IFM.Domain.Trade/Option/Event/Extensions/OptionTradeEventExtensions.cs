using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.Commands;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Domain.Trade.Option.Event.Extensions;

internal static class OptionTradeEventExtensions
{
    /// <summary>
    /// Asynchronously submits a spread distribution job for processing.
    /// </summary>
    /// <param name="context">The event actor context used to dispatch the command.</param>
    /// <param name="spreadDistributionJob">The spread distribution job payload to submit.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the submit operation fails or the service result indicates an error.</exception>
    internal static async ValueTask SubmitSpreadDistributionJobAsync(
        this IEventActorContext context,
        SpreadDistributionJobReadModel spreadDistributionJob)
    {
        var entityId = spreadDistributionJob.EntityId;
        SubmitSpreadDistributionJobCommand cmd = new(spreadDistributionJob)
        {
            CommandId = Guid.NewGuid(),
            Subject = new ActorSubject(ActorType.Command, SubmitSpreadDistributionJobCommand.Actor, SubmitSpreadDistributionJobCommand.Verb, entityId.Format()),
            EntityId = entityId,
        };
        var serviceResult = await context.RequestAsync<SubmitSpreadDistributionJobCommand, SpreadDistributionJobEntityId>(cmd);
        if (serviceResult?.Success != true)
            throw new InvalidOperationException(serviceResult?.ErrorMessage);
    }
}
