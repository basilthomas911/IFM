using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Shared.OptionPricer
{
    public class CreditSpreadResult
    {
        public int Index { get; }
        public int DaysToMaturity { get; }
        public double AssetVolatility { get; }
        public double AssetPrice { get; }
        public double RiskFreeRate { get; }
        public double RateOfReturn { get; }
        public double ShortStrikePrice { get; }
        public double ShortImpliedVolatility { get; set; }
        public List<double[]> ShortValues { get; }
        public double LongStrikePrice { get; }
        public double LongImpliedVolatility { get; set; }
        public List<double[]> LongValues { get; }
        public TimeSpan TaskDuration { get; set; }
        public bool ShortComplete { get; set; }
        public bool LongComplete { get; set; }
        public double[] AssetPrices { get; set; }
        public Exception Error { get; set; }

        public CreditSpreadResult(
            int index,
            int daysToMaturity,
            double assetPrice,
            double riskFreeRate,
            double rateOfReturn,
            double shortStrikePrice,
            double shortImpliedVol,
            double longStrikePrice,
            double longImpliedVol)
        {
            this.Index = index;
            this.DaysToMaturity = daysToMaturity;
            this.AssetPrice = assetPrice;
            this.RiskFreeRate = riskFreeRate;
            this.RateOfReturn = rateOfReturn;
            this.ShortImpliedVolatility = shortImpliedVol;
            this.ShortStrikePrice = shortStrikePrice;
            this.LongImpliedVolatility = longImpliedVol;
            this.LongStrikePrice = longStrikePrice;
            this.ShortValues = new List<double[]>();
            this.LongValues = new List<double[]>();
            this.ShortComplete = false;
            this.LongComplete = false;
            this.Error = null;
        }

    }
}
