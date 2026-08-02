using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;

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
        this IActorOptionPricerCommandApi commandApi,
        SpreadDistributionJobReadModel spreadDistributionJob)
    {
        _ = await commandApi.SubmitSpreadDistributionJobAsync(spreadDistributionJob);
    }
}
