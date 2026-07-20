using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.Trade.ViewModels;
using TomasAI.IFM.Shared.TradePlan.ViewModels;

namespace TomasAI.IFM.Application.Storage.TradePlanDb
{
    public interface ITradePlanDbReadContext : ITradePlanDbContext
    {
        Task<TradePlanStopLossLimitReadModel> GetIronCondorTradePlanStopLossLimitAsync(int orderId, int tradeId);
        Task<IReadOnlyList<IronCondorTradePlanReadModel>> GetIronCondorTradePlansAsync(int orderId, int tradeId, DateTime valueDate);
        Task<IReadOnlyList<IronCondorTradePlanReadModel>> GetIronCondorTradePlansAsync(DateTime startDate, DateTime endDate);
        Task<IReadOnlyList<TradePlanForwardLossRatioReadModel>> GetIronCondorTradePlanForwardLossRatiosAsync(DateTime startDate, DateTime endDate);
        Task<TradePlanForwardLossRatioReadModel> GetIronCondorTradePlanForwardLossRatioAsync(DateTime valueDate);
    }
}
