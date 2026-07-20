using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TomasAI.IFM.Shared.Trade;
using TomasAI.IFM.Shared.OptionPricer;
using TomasAI.IFM.Shared.OptionPricer.ViewModels;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb
{
    public interface IOptionPricerDbReadContext : IOptionPricerDbContext
    {
        Task<IReadOnlyList<OptionPricerDeviceReadModel>> GetOptionPricerDevicesAsync();
        Task<SpreadDistributionReadModel> GetSpreadDistributionAsync(
            int tradeId,
            TradeType tradeType,
            TradeStatus tradeStatus,
            DateTime valueDate,
            int daysToExpiry);
        Task<int> GetSpreadDistributionJobInProgressCountAsync(int orderId, int tradeId);
    }
}
