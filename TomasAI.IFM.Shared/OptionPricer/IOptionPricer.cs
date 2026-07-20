using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.Trade;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface IOptionPricer : IDisposable
    {
        int Id { get; }
        OptionPricerId OptionPricerId { get; }
        bool IsBusy { get; }
        int DeviceId { get; }
        void SetBusyFlag(bool busyFlag);
        void SetDeviceId();
  
        Task<double> PriceOptionAsync(
            int paths,
            int tradingDays,
            double rateOfReturn,
            double volatility,
            double assetPrice,
            double strikePrice,
            double riskFreeRate);

        Task<double[]> PriceOptionsAsync(
            int spreadPaths,
            double strikePrice,
            double volatility,
            OptionSpreadResult csp);

        void PriceOptions(
            int spreadPaths,
            double strikePrice,
            double volatility,
            OptionSpreadResult csp,
            OptionType optionType,
            Action<double[]> setOptionValues);

        double[] GenerateAssetPrices(
            int spreadPaths,
            int tradingDays,
            double assetPrice);

   
        
    }
}
