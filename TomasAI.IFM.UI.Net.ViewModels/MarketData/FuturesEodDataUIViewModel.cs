using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

public class FuturesEodDataUIViewModel
{
    const string Unavailable = "N/A";
    public FuturesEodDataUIViewModel(FuturesEodDataV2ReadModel e)
    {
        MarketDirection = $"{e.MarketDirection}";
        MarketDirectionForeColor = PresentationColorRole.DarkText;
        MarketDirectionBackColor = GetMarketDirectionBackColor();
        MarketVolatility = $"{e.MarketVolatility}";
        MarketVolatilityForeColor = PresentationColorRole.DarkText;
        MarketVolatilityBackColor = GetMarketVolatilityBackColor();
        PriceDirection = $"{e.PriceDirection}";
        PriceDirectionForeColor = PresentationColorRole.DarkText;
        PriceDirectionBackColor = GetPriceDirectionBackColor();
        PriceVolatility = $"{e.PriceVolatility}";
        PriceVolatilityForeColor = PresentationColorRole.DarkText;
        PriceVolatilityBackColor = GetPriceVolatilityBackColor();
        OpenPrice = $"{e.OpenPrice:F2}";
        HighPrice = $"{e.HighPrice:F2}";
        LowPrice = $"{e.LowPrice:F2}";
        ClosePrice = $"{e.ClosePrice:F2}";
        Volume = $"{e.Volume}";
        DailyPercentChange = $"{e.DailyPercentChange:P2}";
        DailyPercentChangeForeColor = PresentationColorRole.DarkText;
        DailyPercentChangeBackColor = e.DailyPercentChange switch
        {
            > 0 => PresentationColorRole.Positive,
            < 0 => PresentationColorRole.Negative,
            _ => PresentationColorRole.Caution
        };
        DailyStdDev = $"{e.DailyStdDev:F2}";
        UpperBand = $"{e.UpperBand:F2}";
        Mean = $"{e.Mean:F2}";
        LowerBand = $"{e.LowerBand:F2}";
        Vwap = Unavailable;
        VwapForeColor = PresentationColorRole.LightText;
        VwapBackColor = PresentationColorRole.Default;
        MDI = $"{e.MarketDirectionIndicator:F4}";
        MDIForeColor = PresentationColorRole.DarkText;
        MDIBackColor = GetMDIBackColor();
        Adx = Unavailable;
        AdxForeColor = PresentationColorRole.LightText;
        AdxBackColor = PresentationColorRole.Default;
        Atr = Unavailable;
        AtrForeColor = PresentationColorRole.LightText;
        AtrBackColor = PresentationColorRole.Default;
        Macd = Unavailable;
        MacdForeColor = PresentationColorRole.LightText;
        MacdBackColor = PresentationColorRole.Default;
        return;

        PresentationColorRole GetMarketDirectionBackColor()
            => e.MarketDirection switch {
                MarketDirectionType.Up => PresentationColorRole.Caution,
                MarketDirectionType.NeutralDown => PresentationColorRole.Warning,
                MarketDirectionType.Down => PresentationColorRole.Negative,
                _ => PresentationColorRole.Positive
            };

        PresentationColorRole GetMarketVolatilityBackColor()
            => e.MarketVolatility switch {
                MarketVolatilityType.High => PresentationColorRole.Negative,
                MarketVolatilityType.Low => PresentationColorRole.Caution,
                MarketVolatilityType.Rising => PresentationColorRole.Warning,
                _ => PresentationColorRole.Positive
            };

        PresentationColorRole GetPriceDirectionBackColor()
            => e.PriceDirection switch {
                PriceDirectionType.Rising => PresentationColorRole.Positive,
                PriceDirectionType.RisingSlowly => PresentationColorRole.PositiveMuted,
                PriceDirectionType.Flat => PresentationColorRole.Caution,
                PriceDirectionType.FallingSlowly => PresentationColorRole.NegativeMuted,
                PriceDirectionType.Falling => PresentationColorRole.Negative,
                _ => PresentationColorRole.Positive
            };

        PresentationColorRole GetPriceVolatilityBackColor()
            => e.PriceVolatility switch {
                PriceVolatilityType.Rising => PresentationColorRole.Negative,
                PriceVolatilityType.Flat => PresentationColorRole.Caution,
                _ => PresentationColorRole.Positive
            };

        PresentationColorRole GetMDIBackColor()
           => e.MarketDirectionIndicator switch
           {
               _ when e.MarketDirectionIndicator >= 60 => PresentationColorRole.Positive,
               _ when e.MarketDirectionIndicator >= 30 => PresentationColorRole.Caution,
               _ => PresentationColorRole.Negative,
           };
    }

    /// <summary>Builds the EOD display while sourcing Bollinger values from typed Analytics.</summary>
    public FuturesEodDataUIViewModel(MarketOutlookReadModel snapshot)
        : this(snapshot.FuturesEodData)
    {
        var bb = snapshot.FuturesBbSignal;
        DailyStdDev = bb?.StandardDeviation20 is { } standardDeviation
            ? $"{standardDeviation:F2}"
            : Unavailable;
        UpperBand = bb?.Upper20 is { } upper ? $"{upper:F2}" : Unavailable;
        Mean = bb?.Ema20Center is { } center ? $"{center:F2}" : Unavailable;
        LowerBand = bb?.Lower20 is { } lower ? $"{lower:F2}" : Unavailable;
        if (bb?.Position20 is { } position)
        {
            var mdi = Math.Clamp(position * 100m, 0m, 100m);
            MDI = $"{mdi:F2}";
            MDIBackColor = mdi switch
            {
                >= 60m => PresentationColorRole.Positive,
                >= 30m => PresentationColorRole.Caution,
                _ => PresentationColorRole.Negative
            };
        }
        else
        {
            MDI = Unavailable;
            MDIBackColor = PresentationColorRole.Default;
        }

        if (snapshot.FuturesVwapSignal is { IsWarm: true, Vwap: > 0m } vwap)
        {
            var exact = vwap.IsValid && vwap.IsTickExact;
            Vwap = exact ? $"{vwap.Vwap.Value:F2}" : $"~{vwap.Vwap.Value:F2}";
            VwapForeColor = PresentationColorRole.DarkText;
            VwapBackColor = !exact ? PresentationColorRole.Caution
                : vwap.Vwap.Value.CompareTo(snapshot.FuturesEodData.ClosePrice) switch
            {
                > 0 => PresentationColorRole.Negative,
                < 0 => PresentationColorRole.Positive,
                _ => PresentationColorRole.Caution
            };
        }
        else
        {
            Vwap = Unavailable;
            VwapForeColor = PresentationColorRole.LightText;
            VwapBackColor = PresentationColorRole.Default;
        }

        if (snapshot.FuturesAdxSignal is { IsWarm: true } adx)
        {
            Adx = $"{adx.AdxValue:F2}";
            AdxForeColor = PresentationColorRole.DarkText;
            AdxBackColor = adx.AdxValue < 25d
                ? PresentationColorRole.Caution
                : adx.PlusDI > adx.MinusDI
                    ? PresentationColorRole.Positive
                    : adx.MinusDI > adx.PlusDI
                        ? PresentationColorRole.Negative
                        : PresentationColorRole.Caution;
        }

        if (snapshot.FuturesAtrSignal is { IsWarm: true, AtrRatio: { } atrRatio } atr)
        {
            Atr = $"{atr.AtrValue:F2}";
            AtrForeColor = PresentationColorRole.DarkText;
            AtrBackColor = atrRatio switch
            {
                < 0.60d or > 1.50d => PresentationColorRole.Negative,
                < 0.80d or > 1.25d => PresentationColorRole.Caution,
                _ => PresentationColorRole.Positive
            };
        }

        if (snapshot.FuturesMacdSignal is { IsWarm: true } macd)
        {
            Macd = $"{macd.Histogram:F2}";
            MacdForeColor = PresentationColorRole.DarkText;
            MacdBackColor = macd.Histogram switch
            {
                >= 2d => PresentationColorRole.Positive,
                <= -2d => PresentationColorRole.Negative,
                _ => PresentationColorRole.Caution
            };
        }
    }

    public string MarketDirection { get; private set; }
    public PresentationColorRole MarketDirectionForeColor { get; private set; }
    public PresentationColorRole MarketDirectionBackColor { get; private set; }
    public string MarketVolatility { get; private set; }
    public PresentationColorRole MarketVolatilityForeColor { get; private set; }
    public PresentationColorRole MarketVolatilityBackColor { get; private set; }
    public string PriceDirection { get; private set; }
    public PresentationColorRole PriceDirectionForeColor { get; private set; }
    public PresentationColorRole PriceDirectionBackColor { get; private set; }
    public string PriceVolatility { get; private set; }
    public PresentationColorRole PriceVolatilityForeColor { get; private set; }
    public PresentationColorRole PriceVolatilityBackColor { get; private set; }
    public string OpenPrice { get; private set; }
    public string HighPrice { get; private set; }
    public string LowPrice { get; private set; }
    public string ClosePrice { get; private set; }
    public string Volume { get; private set; }
    public string DailyPercentChange { get; private set; }
    public PresentationColorRole DailyPercentChangeForeColor { get; private set; }
    public PresentationColorRole DailyPercentChangeBackColor { get; private set; }
    public string DailyStdDev { get; private set; }
    public string UpperBand { get; private set; }
    public string Mean { get; private set; }
    public string LowerBand { get; private set; }
    public string Vwap { get; private set; }
    public PresentationColorRole VwapForeColor { get; private set; }
    public PresentationColorRole VwapBackColor { get; private set; }
    public string MDI { get; private set; }
    public PresentationColorRole MDIForeColor { get; private set; }
    public PresentationColorRole MDIBackColor { get; private set; }
    public string Adx { get; private set; }
    public PresentationColorRole AdxForeColor { get; private set; }
    public PresentationColorRole AdxBackColor { get; private set; }
    public string Atr { get; private set; }
    public PresentationColorRole AtrForeColor { get; private set; }
    public PresentationColorRole AtrBackColor { get; private set; }
    public string Macd { get; private set; }
    public PresentationColorRole MacdForeColor { get; private set; }
    public PresentationColorRole MacdBackColor { get; private set; }
}
