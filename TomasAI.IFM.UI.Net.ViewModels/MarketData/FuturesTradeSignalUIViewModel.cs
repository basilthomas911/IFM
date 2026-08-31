using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

public class FuturesTradeSignalUIViewModel
{
    const string Unavailable = "N/A";
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
    public string TdiDirection { get; private set; } = Unavailable;
    public string TdiStrength { get; private set; } = Unavailable;
    public string TdiMarketState { get; private set; } = Unavailable;
    public string TdiCross { get; private set; } = Unavailable;
    public string TdiDivergence { get; private set; } = Unavailable;

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

    /// <summary>
    /// Builds a display from an OR-composite snapshot. Values backed by an absent component are
    /// shown as unavailable instead of retaining a stale sibling composite or displaying zero.
    /// </summary>
    public FuturesTradeSignalUIViewModel(MarketOutlookSnapshotReadModel snapshot)
        : this(snapshot.FuturesTradeSignal ?? new FuturesTradeSignalV2ReadModel
        {
            ContractId = snapshot.ContractId,
            ValueDate = snapshot.ValueDate
        })
    {
        FiftyDMA = snapshot.FuturesEmaSignal?.Ema50 is { } ema50
            ? $"{ema50:F2}"
            : Unavailable;
        TwoHundredDMA = snapshot.FuturesEmaSignal is { IsWarm: true, Ema200: { } ema200 }
            ? $"{ema200:F2}"
            : Unavailable;
        if (!snapshot.FuturesEodData.IsValid)
            RiskPosition = Unavailable;

        if (snapshot.FuturesRsiSignal is { } rsi)
        {
            RSI = $"{rsi.RSI:F2}";
            RSIBackColor = rsi.RSI switch
            {
                > 60 => PresentationColorRole.Positive,
                < 40 => PresentationColorRole.Negative,
                _ => PresentationColorRole.Caution
            };
        }
        else
        {
            RSI = Unavailable;
            RSIBackColor = PresentationColorRole.Default;
        }

        if (snapshot.FuturesBbSignal?.Position20 is { } position)
        {
            var mdi = Math.Clamp(position * 100m, 0m, 100m);
            MDITrend = mdi switch
            {
                >= 60m => $"{FuturesMDITrendType.UpTrending}",
                < 30m => $"{FuturesMDITrendType.DownTrending}",
                _ => $"{FuturesMDITrendType.RangeBound}"
            };
            MDITrendBackColor = mdi switch
            {
                >= 60m => PresentationColorRole.Positive,
                < 30m => PresentationColorRole.Negative,
                _ => PresentationColorRole.Caution
            };
            MDIUpLimit = "60.00";
            MDIDownLimit = "30.00";
        }
        else
        {
            MDITrend = Unavailable;
            MDITrendBackColor = PresentationColorRole.Default;
        }

        if (snapshot.LatestItiTrendSignal is { } direction)
        {
            Trend = direction.IntrinsicTimeTrend switch
            {
                IntrinsicTimeTrendType.UpTrend => $"{FuturesTrendType.UpTrending}",
                IntrinsicTimeTrendType.DownTrend => $"{FuturesTrendType.DownTrending}",
                _ => $"{FuturesTrendType.RangeBound}"
            };
            UpTrendLimit = $"{direction.UpTrendTrigger:F2}";
            DownLimitTrigger = $"{direction.DownTrendTrigger:F2}";
            TradeEntry = $"{direction.IntrinsicPrice:F2}";
            TrendDelta = $"{direction.TrendDelta:F2}";
        }
        else
        {
            Trend = Unavailable;
            UpTrendLimit = Unavailable;
            DownLimitTrigger = Unavailable;
            TradeEntry = Unavailable;
            TradeExit = Unavailable;
            TrendDelta = Unavailable;
        }

        TrendExtreme = snapshot.TrendExtremeChange is { } extreme
            ? $"{extreme.TrendExtreme:F2}"
            : Unavailable;
        TrendReversal = snapshot.TrendReversalChange is { } reversal
            ? $"{reversal.TrendReversal:F2}"
            : Unavailable;

        if (snapshot.FuturesTdiSignal is { } tdi)
        {
            TdiDirection = $"{tdi.TDI}";
            TdiStrength = $"{tdi.TDIStrength}";
            TdiMarketState = $"{tdi.MarketState}";
            TdiCross = $"{tdi.Cross}";
            TdiDivergence = $"{tdi.PriceSignalDivergence:F2}";
        }
        else
        {
            TdiDirection = "Warming";
            TdiStrength = Unavailable;
            TdiMarketState = Unavailable;
            TdiCross = Unavailable;
            TdiDivergence = Unavailable;
        }
    }


}
