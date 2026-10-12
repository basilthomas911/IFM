using TomasAI.IFM.Domain.Trade.Shared;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Domain.Trade.Shared.Events;
using TomasAI.IFM.Domain.Trade.Shared.TradePlan.ServiceApi;

namespace TomasAI.IFM.TradePlan.EventHandlers
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711", Justification = "Existing event service handler identity is used by service registration.")]
public class TradePlanUpdatedEventHandler : BaseEventServiceHandler,
        IAsyncEventHandler<TradePlanUpdatedEvent, TradePlanService>
    {
        readonly ITradePlanCommandApi _tradePlanCommand;
        readonly ITradePlanQueryApi _tradePlanQuery;

        public TradePlanUpdatedEventHandler(
            ITradePlanCommandApi tradePlanCommand,
            ITradePlanQueryApi tradePlanQuery,
            IStatusConsoleWriter statusConsoleWriter) : base(statusConsoleWriter)
        {
            _tradePlanQuery = tradePlanQuery;
            _tradePlanCommand = tradePlanCommand;
        }

        /// <summary>
        /// execute forward loss limit workflow once trade is in forward loss limit warning action
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePlanUpdatedEvent e)
        {
            ArgumentNullException.ThrowIfNull(e);
            switch (e.TradePlan.ActionSubType)
            {
                case ActionSubType.ForwardLossRiskLimitReachedWarning:
                    await (_tradePlanCommand.UpdateTradePlanForwardLossLimitAsync(new TradePlanForwardLossLimitReadModel(
                        orderId: e.TradePlan.OrderId,
                        tradeId: e.TradePlan.TradeId,
                        tradeType: e.TradePlan.TradeType,
                        valueDate: e.TradePlan.ValueDate,
                        limitType: ForwardLossLimitType.LimitWarning))).ConfigureAwait(false);
                    break;
                case ActionSubType.ForwardLossRiskLimitWarning:
                case ActionSubType.ForwardLossRiskLimitReached:
                    break;
                default:
                    var sr = await (_tradePlanQuery.GetForwardLossLimitTypeAsync(e.TradePlan.OrderId, e.TradePlan.TradeId, e.TradePlan.ValueDate, e.TradePlan.TradeType)).ConfigureAwait(false);
                    if (sr.Success && sr.Value is not null)
                    {
                        var id = new TradePlanForwardLossLimitEntityId(e.TradePlan.OrderId, e.TradePlan.TradeId, e.TradePlan.ValueDate, e.TradePlan.TradeType);
                        await (_tradePlanCommand.ClearTradePlanForwardLossLimitAsync(id)).ConfigureAwait(false);
                    }
                    break;
            }
        }
    }
}
