using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.ViewModels.MarketData
{
    public class FuturesEodDataUIViewModel
    {
        public FuturesEodDataUIViewModel(FuturesEodDataViewModel e)
        {
            MarketDirection = $"{e.MarketDirection}";
            MarketDirectionBackColor = GetMarketDirectionBackColor();
            MarketVolatility = $"{e.MarketVolatility}";
            MarketVolatilityBackColor = GetMarketVolatilityBackColor();
            PriceDirection = $"{e.PriceDirection}";
            PriceDirectionBackColor = GetPriceDirectionBackColor();
            PriceVolatility = $"{e.PriceVolatility}";
            PriceVolatilityBackColor = GetPriceVolatilityBackColor();
            OpenPrice = $"{e.OpenPrice:F2}";
            HighPrice = $"{e.HighPrice:F2}";
            LowPrice = $"{e.LowPrice:F2}";
            ClosePrice = $"{e.ClosePrice:F2}";
            Volume = $"{e.Volume}";
            DailyPercentChange = $"{e.DailyPercentChange:P2}";
            DailyPercentChangeBackColor = e.DailyPercentChange >= 0 ? Color.LimeGreen : Color.Red;
            DailyStdDev = $"{e.DailyStdDev:F2}";
            UpperBand = $"{e.UpperBand:F2}";
            Mean = $"{e.Mean:F2}";
            LowerBand = $"{e.LowerBand:F2}";
            MDI = $"{e.MarketDirectionIndicator:F4}";
            MDIBackColor = GetMDIBackColor();
            return;

            Color GetMarketDirectionBackColor()
                => e.MarketDirection switch {
                    MarketDirectionType.Up => Color.Yellow,
                    MarketDirectionType.NeutralDown => Color.DarkOrange,
                    MarketDirectionType.Down => Color.Red,
                    _ => Color.LimeGreen
                };

            Color GetMarketVolatilityBackColor()
                => e.MarketVolatility switch {
                    MarketVolatilityType.High => Color.Red,
                    MarketVolatilityType.Low => Color.Yellow,
                    MarketVolatilityType.Rising => Color.DarkOrange,
                    _ => Color.LimeGreen
                };

            Color GetPriceDirectionBackColor()
                => e.PriceDirection switch {
                    PriceDirectionType.Rising => Color.LimeGreen,
                    PriceDirectionType.RisingSlowly => Color.YellowGreen,
                    PriceDirectionType.Flat => Color.Yellow,
                    PriceDirectionType.FallingSlowly => Color.OrangeRed,
                    PriceDirectionType.Falling => Color.Red,
                    _ => Color.LimeGreen
                };

            Color GetPriceVolatilityBackColor()
                => e.PriceVolatility switch {
                    PriceVolatilityType.Rising => Color.Red,
                    PriceVolatilityType.Flat => Color.Yellow,
                    _ => Color.LimeGreen
                };

            Color GetMDIBackColor()
               => e.MarketDirectionIndicator switch
               {
                   _ when e.MarketDirectionIndicator >= 60 => Color.LimeGreen,
                   _ when e.MarketDirectionIndicator >= 30 => Color.Yellow,
                   _ => Color.Red,
               };
        }

        public string MarketDirection { get; private set; }
        public Color MarketDirectionBackColor { get; private set; }
        public string MarketVolatility { get; private set; }
        public Color MarketVolatilityBackColor { get; private set; }
        public string PriceDirection { get; private set; }
        public Color PriceDirectionBackColor { get; private set; }
        public string PriceVolatility { get; private set; }
        public Color PriceVolatilityBackColor { get; private set; }
        public string OpenPrice { get; private set; }
        public string HighPrice { get; private set; }
        public string LowPrice { get; private set; }
        public string ClosePrice { get; private set; }
        public string Volume { get; private set; }
        public string DailyPercentChange { get; private set; }
        public Color DailyPercentChangeBackColor { get; private set; }
        public string DailyStdDev { get; private set; }
        public string UpperBand { get; private set; }
        public string Mean { get; private set; }
        public string LowerBand { get; private set; }
        public string MDI { get; private set; }
        public Color MDIBackColor { get; private set; }
    }
}
