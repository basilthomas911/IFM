using System;
using System.Collections.Generic;
using System.Text;

namespace TomasAI.IFM.Domain.OptionPricer.Shared
{
    public class OptionSpreadResult
    {
        public int Index { get; }
        public int DaysToMaturity { get; }
        public double AssetVolatility { get; }
        public double AssetPrice { get; }
        public double RiskFreeRate { get; }
        public double RateOfReturn { get; }
        public double ShortStrikePrice { get; }
        public double ShortImpliedVolatility { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        public List<double[]> ShortValues { get; }
        public double LongStrikePrice { get; }
        public double LongImpliedVolatility { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1002", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0016", Justification = "Retains the existing public collection contract used by adapters and validation callers.")]
        public List<double[]> LongValues { get; }
        public TimeSpan TaskDuration { get; set; }
        public bool ShortComplete { get; set; }
        public bool LongComplete { get; set; }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1819", Justification = "Retains the existing array-shaped serialization and application contract.")]
        public double[] AssetPrices { get; set; }
        public Exception Error { get; set; }

        public OptionSpreadResult(
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
