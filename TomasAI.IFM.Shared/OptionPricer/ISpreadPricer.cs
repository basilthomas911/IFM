using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public interface ISpreadPricer: IDisposable
    {
        int Id { get; }
        OptionPricerId OptionPricerId { get; }
        bool IsBusy { get; }
        int DeviceId { get; }
        void SetBusyFlag(bool busyFlag);
        void SetDeviceId();

        void PriceSpreads(int spreadPaths, OptionSpreadResult csp);

        double[] GenerateAssetPrices(
            int spreadPaths,
            int tradingDays,
            double assetPrice);

        
    }
}
