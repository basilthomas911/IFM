using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Domain.OptionPricer.Shared;
using TomasAI.IFM.Domain.OptionPricer.Shared.Commands;
using TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels;
using TomasAI.IFM.Domain.OptionPricer.Shared.ServiceApi;
using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Shared.Events;

namespace TomasAI.IFM.Domain.Trade.Option.Event.Extensions;

internal static class OptionTradeEventExtensions
{
    /// <summary>
    /// Continues the option-trade end-of-day workflow at the durable Fund boundary while preserving the
    /// originating command identifier used by the UI and other terminal-operation clients.
    /// </summary>
    internal static async ValueTask<bool> ProcessFundEndOfDayAsync(
        this OptionTradeEndOfDayProcessedEvent source,
        IEventActorContext context)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);

        var transaction = FundTransactionReadModel.AsUnrealizedTradePnlTransaction(
            source.FundId,
            source.OrderId,
            source.EntityId.TradeId,
            source.EodKey.TradeType,
            source.EodKey.ValueDate,
            source.Reference,
            source.TradePnl);
        var command = new ProcessEndOfDayFundTransactionCommand(transaction)
        {
            CommandId = Guid.NewGuid(),
            CorrelationId = source.CommandId,
            Subject = new ActorSubject(
                ActorType.Command,
                ProcessEndOfDayFundTransactionCommand.Actor,
                ProcessEndOfDayFundTransactionCommand.Verb,
                transaction.EntityId.Format()),
            PostEvents = true
        };
        var result = await context
            .RequestAsync<ProcessEndOfDayFundTransactionCommand, FundTransactionEntityId>(command)
            .ConfigureAwait(false);
        if (result?.Success != true)
            throw new InvalidOperationException(
                result?.ErrorMessage ?? "Fund end-of-day processing returned no result.");
        return true;
    }

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
