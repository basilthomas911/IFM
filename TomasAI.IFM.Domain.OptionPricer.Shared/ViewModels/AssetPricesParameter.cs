using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Domain.MarketData.Shared;

namespace TomasAI.IFM.Domain.OptionPricer.Shared.ViewModels
{
    public class AssetPricesParameter
    {
        public MarketDirectionType MarketTrend { get; set; }
        public int SpreadPaths { get; set; }
        public int DaysToMaturity { get; set; }
        public double AssetPrice { get; set; }
        public double NextAssetPrice { get; set; }
        public double RateOfReturn { get; set; }
        public double AssetVolatility { get; set; }
    }
}
