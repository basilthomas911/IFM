using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.TradePlan.ServiceApi;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Service.TradePlan.EventHandlers
{
    public class TradePlanUpdatedEventHandler : BaseEventServiceHandler,
        IAsyncEventHandler<TradePlanUpdatedEvent, TradePlanService>
    {
        readonly ITradePlanCommandApi _tradePlanCommand;
        readonly ITradePlanQueryApi _tradePlanQuery;

        public TradePlanUpdatedEventHandler(
            ITradePlanCommandApi tradePlanCommand,
            ITradePlanQueryApi tradePlanQuery,
            IStatusConsoleWriter statusConsoleWriter):base(statusConsoleWriter)
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
            switch (e.TradePlan.ActionSubType)
            {
                case ActionSubType.ForwardLossRiskLimitReachedWarning:
                    await _tradePlanCommand.UpdateTradePlanForwardLossLimitAsync(new TradePlanForwardLossLimitReadModel(
                        orderId: e.TradePlan.OrderId,
                        tradeId: e.TradePlan.TradeId,
                        tradeType: e.TradePlan.TradeType,
                        valueDate: e.TradePlan.ValueDate,
                        limitType: ForwardLossLimitType.LimitWarning));
                    break;
                case ActionSubType.ForwardLossRiskLimitWarning:
                case ActionSubType.ForwardLossRiskLimitReached:
                    break;
                default:
                    var sr = await _tradePlanQuery.GetForwardLossLimitTypeAsync(e.TradePlan.OrderId, e.TradePlan.TradeId, e.TradePlan.ValueDate, e.TradePlan.TradeType);
                    if (sr.Success && sr.Value is not null)
                    {
                        var id = new TradePlanForwardLossLimitEntityId(e.TradePlan.OrderId, e.TradePlan.TradeId, e.TradePlan.ValueDate, e.TradePlan.TradeType);
                        await _tradePlanCommand.ClearTradePlanForwardLossLimitAsync(id);
                    }
                    break;
            }
        }
    }
}
