using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

public class FuturesTradeSignalUIViewModel
{
    public string ContractId { get; private set; }
    public string Trend { get; private set; }
    public PresentationColorRole TrendForeColor { get; private set; }
    public PresentationColorRole TrendBackColor { get; private set; }
    public string MDIDownLimit { get; private set; }
    public PresentationColorRole MDIDownLimitForeColor { get; private set; }
    public PresentationColorRole MDIDownLimitBackColor { get; private set; }
    public string RSI { get; private set; }
    public PresentationColorRole RSIForeColor { get; private set; }
    public PresentationColorRole RSIBackColor { get; private set; }
    public string MDITrend { get; private set; }
    public PresentationColorRole MDITrendForeColor { get; private set; }
    public PresentationColorRole MDITrendBackColor { get; private set; }
    public string MDIUpLimit { get; private set; }
    public PresentationColorRole MDIUpLimitForeColor { get; private set; }
    public PresentationColorRole MDIUpLimitBackColor { get; private set; }
    public string RiskPosition { get; private set; }
    public PresentationColorRole RiskPositionForeColor { get; private set; }
    public PresentationColorRole RiskPositionBackColor { get; private set; }

    public string UpTrendLimit { get; private set; }
    public PresentationColorRole UpTrendLimitForeColor { get; private set; }
    public string DownLimitTrigger { get; private set; }
    public PresentationColorRole DownLimitTriggerForeColor { get; private set; }
    public string TradeEntry { get; private set; }
    public PresentationColorRole TradeEntryForeColor { get; private set; }
    public string TradeExit { get; private set; }
    public PresentationColorRole TradeExitForeColor { get; private set; }
    public string TrendDelta { get; private set; }
    public PresentationColorRole TrendDeltaForeColor { get; private set; }
    public string TrendExtreme { get; private set; }
    public PresentationColorRole TrendExtremeForeColor { get; private set; }
    public string TrendReversal { get; private set; }
    public PresentationColorRole TrendReversalForeColor { get; private set; }
    public string FiftyDMA { get; private set; }
    public string TwoHundredDMA { get; private set; }

    public FuturesTradeSignalUIViewModel(FuturesTradeSignalV2ReadModel e)
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

        PresentationColorRole GetTrendForeColor() => PresentationColorRole.DarkText;

        PresentationColorRole GetTrendBackColor()
            => e.TrendType switch {
                FuturesTrendType.UpTrend => PresentationColorRole.Positive,
                FuturesTrendType.UpTrending => PresentationColorRole.Positive,
                FuturesTrendType.DownTrending => PresentationColorRole.Negative,
                FuturesTrendType.DownTrend => PresentationColorRole.Negative,
                _ => PresentationColorRole.Caution
            };

        PresentationColorRole GetMDIUpLimitForeColor() => PresentationColorRole.DarkText;

        PresentationColorRole GetMDIUpLimitBackColor() => PresentationColorRole.Positive;

        PresentationColorRole GetMDIDownLimitForeColor() => PresentationColorRole.DarkText;

        PresentationColorRole GetMDIDownLimitBackColor() => PresentationColorRole.Negative;

        PresentationColorRole GetRSIForeColor() => PresentationColorRole.DarkText;

        PresentationColorRole GetRSIBackColor()
             => e.RSI switch
             {
                 > 60 => PresentationColorRole.Positive,
                 < 40 => PresentationColorRole.Negative,
                 _ => PresentationColorRole.Caution
             };

        PresentationColorRole GetMDITrendForeColor() => PresentationColorRole.DarkText;

        PresentationColorRole GetMDITrendBackColor()
            => e.MDITrend switch
            {
                FuturesMDITrendType.UpTrending  => PresentationColorRole.Positive,
                FuturesMDITrendType.DownTrending => PresentationColorRole.Negative,
                _ => PresentationColorRole.Caution
            };

        PresentationColorRole GetRiskPositionForeColor() => PresentationColorRole.DarkText;

        PresentationColorRole GetRiskPositionBackColor()
            => e.TrendStrength switch
            {
                FuturesTrendStrengthType.High => PresentationColorRole.Positive,
                FuturesTrendStrengthType.Medium => PresentationColorRole.Caution,
                _ => PresentationColorRole.Negative
            };

        PresentationColorRole GetTradeEntryForeColor() => PresentationColorRole.LightText;

        PresentationColorRole GetTradeExitForeColor() => PresentationColorRole.LightText;

        PresentationColorRole GetTrendDeltaForeColor() => PresentationColorRole.LightText;
        PresentationColorRole GetTrendExtremeForeColor() => PresentationColorRole.LightText;
        PresentationColorRole GetTrendReversalForeColor() => PresentationColorRole.LightText;

        PresentationColorRole GetUpTrendTriggerForeColor() => PresentationColorRole.LightText;

        PresentationColorRole GetDownTrendTriggerForeColor() => PresentationColorRole.LightText;

    }


}
