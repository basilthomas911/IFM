using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketDataAnalytics;
using TomasAI.IFM.Shared.MarketDataAnalytics.ViewModels;

namespace TomasAI.IFM.ViewModels.MarketData
{
    public class FuturesTradeSignalUIViewModel
    {
        public string ContractId { get; private set; }
        public string Trend { get; private set; }
        public Color TrendForeColor { get; private set; }
        public Color TrendBackColor { get; private set; }
        public string MDIDownLimit { get; private set; }
        public Color MDIDownLimitForeColor { get; private set; }
        public Color MDIDownLimitBackColor { get; private set; }
        public string RSI { get; private set; }
        public Color RSIForeColor { get; private set; }
        public Color RSIBackColor { get; private set; }
        public string MDITrend { get; private set; }
        public Color MDITrendForeColor { get; private set; }
        public Color MDITrendBackColor { get; private set; }
        public string MDIUpLimit { get; private set; }
        public Color MDIUpLimitForeColor { get; private set; }
        public Color MDIUpLimitBackColor { get; private set; }
        public string RiskPosition { get; private set; }
        public Color RiskPositionForeColor { get; private set; }
        public Color RiskPositionBackColor { get; private set; }

        public string UpTrendLimit { get; private set; }
        public Color UpTrendLimitForeColor { get; private set; }
        public string DownLimitTrigger { get; private set; }
        public Color DownLimitTriggerForeColor { get; private set; }
        public string TradeEntry { get; private set; }
        public Color TradeEntryForeColor { get; private set; }
        public string TradeExit { get; private set; }
        public Color TradeExitForeColor { get; private set; }
        public string TrendDelta { get; private set; }
        public Color TrendDeltaForeColor { get; private set; }
        public string TrendExtreme { get; private set; }
        public Color TrendExtremeForeColor { get; private set; }
        public string TrendReversal { get; private set; }
        public Color TrendReversalForeColor { get; private set; }
        public string FiftyDMA { get; private set; }
        public string TwoHundredDMA { get; private set; }

        public FuturesTradeSignalUIViewModel(FuturesTradeSignalViewModel e)
        {
            ContractId = $"{e.ContractId}";
            Trend = $"{e.TrendType}";
            TrendForeColor = GetTrendForeColor();
            TrendBackColor = GetTrendBackColor();
            MDIDownLimit = $"{e.MDIDownTrendLimit:F2}";
            MDIDownLimitForeColor = GetMDIDownLimitForeColor();
            MDIDownLimitBackColor = GetMDIDownLimitBackColor();
            RSI = $"{e.RSI:F2}";
            RSIForeColor = GetRSIForeColor();
            RSIBackColor = GetRSIBackColor();
            MDITrend = $"{e.MDITrend}";
            MDITrendForeColor = GetMDITrendForeColor();
            MDITrendBackColor = GetMDITrendBackColor();
            MDIUpLimit = $"{e.MDIUpTrendLimit:F2}";
            MDIUpLimitForeColor = GetMDIUpLimitForeColor();
            MDIUpLimitBackColor = GetMDIUpLimitBackColor();
            RiskPosition = $"{e.TrendStrength}";
            RiskPositionForeColor = GetRiskPositionForeColor();
            RiskPositionBackColor = GetRiskPositionBackColor();
            TradeEntry = $"{e.EntryTrigger:F2}";
            TradeEntryForeColor = GetTradeEntryForeColor();
            TradeExit = $"{e.ExitTrigger:F2}";
            TradeExitForeColor = GetTradeExitForeColor();
            TrendDelta = $"{e.TrendDelta:F2}";
            TrendDeltaForeColor = GetTrendDeltaForeColor();
            TrendExtreme = $"{e.TrendExtreme:F2}";
            TrendExtremeForeColor = GetTrendExtremeForeColor();
            TrendReversal = $"{e.TrendReversal:F2}";
            TrendReversalForeColor = GetTrendReversalForeColor();
            UpTrendLimit = $"{e.UpTrendingTrigger:F2}";
            UpTrendLimitForeColor = GetUpTrendTriggerForeColor();
            DownLimitTrigger = $"{e.DownTrendingTrigger:F2}";
            DownLimitTriggerForeColor = GetDownTrendTriggerForeColor();
            FiftyDMA = $"{e.FiftyDMA:F2}";
            TwoHundredDMA = $"{e.TwoHundredDMA:F2}";
            return;

            Color GetTrendForeColor() => Color.Black;

            Color GetTrendBackColor()
                => e.TrendType switch {
                    FuturesTrendType.UpTrend => Color.LimeGreen,
                    FuturesTrendType.UpTrending => Color.LimeGreen,
                    FuturesTrendType.DownTrending => Color.Red,
                    FuturesTrendType.DownTrend => Color.Red,
                    _ => Color.Yellow
                };

            Color GetMDIUpLimitForeColor() => Color.Black;

            Color GetMDIUpLimitBackColor() => Color.LimeGreen;

            Color GetMDIDownLimitForeColor() => Color.Black;

            Color GetMDIDownLimitBackColor() => Color.Red;

            Color GetRSIForeColor() => Color.Black;

            Color GetRSIBackColor()
                 => e.RSI switch
                 {
                     > 60 => Color.LimeGreen,
                     < 40 => Color.Red,
                     _ => Color.Yellow
                 };

            Color GetMDITrendForeColor() => Color.Black;

            Color GetMDITrendBackColor()
                => e.MDITrend switch
                {
                    FuturesMDITrendType.UpTrending  => Color.LimeGreen,
                    FuturesMDITrendType.DownTrending => Color.Red,
                    _ => Color.Yellow
                };

            Color GetRiskPositionForeColor() => Color.Black;

            Color GetRiskPositionBackColor()
                => e.TrendStrength switch
                {
                    FuturesTrendStrengthType.High => Color.LimeGreen,
                    FuturesTrendStrengthType.Medium => Color.Yellow,
                    _ => Color.Red
                };

            Color GetTradeEntryForeColor() => Color.White;

            Color GetTradeExitForeColor() => Color.White;

            Color GetTrendDeltaForeColor() => Color.White;
            Color GetTrendExtremeForeColor() => Color.White;
            Color GetTrendReversalForeColor() => Color.White;

            Color GetUpTrendTriggerForeColor() => Color.White;

            Color GetDownTrendTriggerForeColor() => Color.White;

        }


    }
}
