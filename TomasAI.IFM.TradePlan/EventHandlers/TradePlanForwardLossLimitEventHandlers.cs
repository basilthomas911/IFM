using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.Events;
using TomasAI.IFM.Shared.StatusConsole.ServiceApi;
using TomasAI.IFM.Shared.EventService;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Application.Blackboard;

namespace TomasAI.IFM.Service.TradePlan.EventHandlers
{
    public class TradePlanForwardLossLimitEventHandlers : BaseEventServiceHandler,
        IAsyncEventHandler<TradePlanForwardLossLimitWarningUpdatedEvent, TradePlanService>,
        IAsyncEventHandler<TradePlanForwardLossLimitReachedUpdatedEvent, TradePlanService>,
        IAsyncEventHandler<TradePlanForwardLossLimitClearedEvent, TradePlanService>
    {
        readonly IBlackboardService _blackboardService;

        public TradePlanForwardLossLimitEventHandlers(
            IBlackboardService blackboardService,
            IStatusConsoleWriter statusConsoleWriter) : base(statusConsoleWriter)
        {
            _blackboardService = blackboardService;
        }

        /// <summary>
        /// cache forward loss limit warning
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePlanForwardLossLimitWarningUpdatedEvent e)
        {
            var forwardTradeLossLimit = _blackboardService.TradePlanForwardLossLimit.Get(e.TradePlanForwardLossLimit.EntityId);
            if (forwardTradeLossLimit is not null)
                _blackboardService.TradePlanForwardLossLimit.Remove(e.TradePlanForwardLossLimit.EntityId);
            _blackboardService.TradePlanForwardLossLimit.Set(e.TradePlanForwardLossLimit.EntityId, e.TradePlanForwardLossLimit);
            await Task.CompletedTask;
        }

        /// <summary>
        /// cache forward loss limit reached
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePlanForwardLossLimitReachedUpdatedEvent e)
        {
            var forwardTradeLossLimit = _blackboardService.TradePlanForwardLossLimit.Get(e.TradePlanForwardLossLimit.EntityId);
            if (forwardTradeLossLimit is not null)
                _blackboardService.TradePlanForwardLossLimit.Remove(e.TradePlanForwardLossLimit.EntityId);
            _blackboardService.TradePlanForwardLossLimit.Set(e.TradePlanForwardLossLimit.EntityId, e.TradePlanForwardLossLimit);
            await Task.CompletedTask;
        }

        /// <summary>
        /// remove cached forward loss limit if found
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(TradePlanForwardLossLimitClearedEvent e)
        {
            var forwardTradeLossLimit = _blackboardService.TradePlanForwardLossLimit.Get(e.ForwardLossLimitId);
            if (forwardTradeLossLimit is not null)
                _blackboardService.TradePlanForwardLossLimit.Remove(e.ForwardLossLimitId);
            await Task.CompletedTask;
        }

    }
}
