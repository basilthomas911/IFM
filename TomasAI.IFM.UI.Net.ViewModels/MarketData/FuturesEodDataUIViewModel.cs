using TomasAI.IFM.UI.Net.ViewModels.Presentation;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.ViewModels.MarketData;

public class FuturesEodDataUIViewModel
{
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
        MDI = $"{e.MarketDirectionIndicator:F4}";
        MDIForeColor = PresentationColorRole.DarkText;
        MDIBackColor = GetMDIBackColor();
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
    public string MDI { get; private set; }
    public PresentationColorRole MDIForeColor { get; private set; }
    public PresentationColorRole MDIBackColor { get; private set; }
}
