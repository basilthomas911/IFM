using TomasAI.IFM.Domain.Fund.Shared;
using TomasAI.IFM.Domain.Fund.Shared.Commands;
using TomasAI.IFM.Domain.Fund.Shared.ViewModels;
using TomasAI.IFM.Domain.Trade.Futures.Option.Event.Actor;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.Trade.Futures.Option.Event;

public static class OptionTradeEndOfDayProcessed
{
    /// <summary>Continues completed option end-of-day processing at the durable Fund boundary.</summary>
    public static async ValueTask ExecuteAsync(
        this OptionTradeEndOfDayProcessedEvent source,
        IFuturesOptionTradeEventContext context)
    {
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
    }
}
