using TomasAI.IFM.Shared.MarketData;
using TomasAI.IFM.Shared.MarketData.ViewModels;
using TomasAI.IFM.Shared.MarketDataFeed.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Feed.FuturesEodData.Command.Model;

/// <summary>
/// Represents a set of Bollinger Bands, which are used to measure market volatility and identify potential price trends
/// based on historical price data.
/// </summary>
/// <remarks>The BollingerBands class calculates upper and lower bands based on a specified window size and
/// standard deviation of price data. It provides various properties to access market direction, volatility, and daily
/// percentage change, allowing users to analyze market conditions effectively.</remarks>
public class BollingerBands
{
    readonly int _windowSize;
    readonly double _stdDev;
    readonly double _stdDevAmount;
    readonly double _sma;
    readonly ICollection<FuturesEodDataV2ReadModel> _futuresEodData;
    readonly ICollection<VixFuturesEodDataReadModel> _vixFuturesEodData;

    /// <summary>
    /// create bollinger bands object
    /// </summary>
    /// <param name="windowSize"></param>
    /// <param name="futuresEodData"></param>
    /// <param name="normalCurveTable"></param>
    public BollingerBands(int windowSize, ICollection<FuturesEodDataV2ReadModel> futuresEodData, NormalCurveTableReadModel normalCurveTable, ICollection<VixFuturesEodDataReadModel> vixFuturesEodData)
    {
        FuturesEodDataV2ReadModel[] eodData = [.. futuresEodData.Take(windowSize * 2)];
        var hiloStdDevCalc = new StdDevCalculator(windowSize, eodData, e => Convert.ToDouble(e.HighPrice - e.LowPrice));
        var hiloStdDevAmount = 1.0 / Math.Sqrt(windowSize) * (hiloStdDevCalc.StdDev * (4 * Math.Sqrt(hiloStdDevCalc.StdDev/ hiloStdDevCalc.Mean)));
        var eodStdDevCalc = new StdDevCalculator(windowSize, eodData, e => Convert.ToDouble(e.ClosePrice));
        _windowSize = windowSize;
        _stdDev = eodStdDevCalc.StdDev;
        _stdDevAmount = 1.0 / Math.Sqrt(windowSize) * _stdDev;
        _sma = eodStdDevCalc.Mean;
        _futuresEodData = eodData.ToList();
        _vixFuturesEodData = vixFuturesEodData;
    }

    public string ContractId => _futuresEodData.ElementAt(0).ContractId;
    public DateOnly ValueDate => _futuresEodData.ElementAt(0).ValueDate;
    public decimal Open => _futuresEodData.ElementAt(0).OpenPrice;
    public decimal High => _futuresEodData.ElementAt(0).HighPrice;
    public decimal Low => _futuresEodData.ElementAt(0).LowPrice;
    public decimal Close => _futuresEodData.ElementAt(0).ClosePrice;
    public int Volume => _futuresEodData.ElementAt(0).Volume;

    public int WindowSize => _windowSize;
    public double StdDev => _stdDev;
    public double StdDevAmount => _stdDevAmount;
    public double UpperBand => Convert.ToDouble( Convert.ToDouble(Close) > _sma + (2 * _stdDev) ? Close : _sma + (2 * _stdDev));
    public double Mean => _sma;
    public double LowerBand => Convert.ToDouble(Convert.ToDouble(Close) < _sma - (2 * _stdDev) ? Close : _sma - (2 * _stdDev));

    public MarketDirectionType MarketDirection => GetMarketDirection();
    public MarketVolatilityType MarketVolatility => GetMarketVolatility();
    public PriceDirectionType PriceDirection => GetPriceDirection();
    public PriceVolatilityType PriceVolatility => GetPriceVolatility();
    public double MarketDirectionIndicator => GetMarketDirectionIndicator();   

    public double DailyPercentageChange => GetDailyPercentageChange();
    public double RateOfReturn => _stdDev / _sma * Math.Sqrt(_windowSize);
    public double AssetVolatility => _stdDev / _sma;

    double GetDailyPercentageChange()
    {
        if (_futuresEodData == null || _futuresEodData.Count == 0)
            return 0.0;
        var dailyPC = Convert.ToDouble(Math.Round((Close - Open) / Open, 4));
        return dailyPC;
    }

    MarketDirectionType GetMarketDirection()
    {
        var assetPrice = _futuresEodData.ElementAt(0).ClosePrice;
        return Mean switch
        {
            _ when Convert.ToDouble(assetPrice) >= UpperBand => MarketDirectionType.Up,
            _ when Convert.ToDouble(assetPrice) >= Mean => MarketDirectionType.NeutralUp,
            _ when Convert.ToDouble(assetPrice) < LowerBand => MarketDirectionType.Down,
            _ when Convert.ToDouble(assetPrice) < Mean => MarketDirectionType.NeutralDown,
            _ => MarketDirectionType.NeutralUp
        };
    }

    MarketVolatilityType GetMarketVolatility()
    {
        var e1 = _futuresEodData.ElementAt(1);
        return _stdDev switch {
            _ when _stdDev > e1.DailyStdDev => MarketVolatilityType.Rising,
            _ when _stdDev < e1.DailyStdDev => MarketVolatilityType.Falling,
            _ => MarketVolatilityType.Falling
        };
    }

    PriceDirectionType GetPriceDirection()
        => default(PriceDirectionType) switch {
            _ when Close > Open => PriceDirectionType.Rising,
            _ when Close < Open => PriceDirectionType.Falling,
            _ => PriceDirectionType.Rising
        };

    PriceVolatilityType GetPriceVolatility()
    {
        var vixVol = PriceVolatilityType.Falling;
        if (_vixFuturesEodData?.Count > 1)
        {
            var e0 = _vixFuturesEodData.ElementAt(0);
            var e1 = _vixFuturesEodData.ElementAt(1);
            if (e0.ValueDate == e1.ValueDate)
                return e0.ClosePrice switch {
                    _ when e0.ClosePrice > e0.OpenPrice => PriceVolatilityType.Rising,
                    _ when e0.ClosePrice < e0.OpenPrice => PriceVolatilityType.Falling,
                    _ => vixVol
                };
            return e0.ClosePrice switch {
                _ when e0.ClosePrice > Math.Min(e0.ClosePrice, e1.ClosePrice) => PriceVolatilityType.Rising,
                _ when e0.ClosePrice < Math.Max(e0.ClosePrice, e1.ClosePrice) => PriceVolatilityType.Falling,
                _ => vixVol
            };

        }
        else if (_vixFuturesEodData?.Count == 1)
        {
            var e0 = _vixFuturesEodData.ElementAt(0);
            return e0.ClosePrice switch {
                _ when e0.ClosePrice > e0.OpenPrice => PriceVolatilityType.Rising,
                _ when e0.ClosePrice < e0.OpenPrice => PriceVolatilityType.Falling,
                _ => vixVol
            };
        }
        return vixVol;
    }

    double GetMarketDirectionIndicator()
    {
        var maxUpperBand = Math.Max(UpperBand, Convert.ToDouble(Close));
        var maxLowerBand = Math.Min(LowerBand, Convert.ToDouble(Close));
        var maxBandSize = maxUpperBand - maxLowerBand;
        var mdi = maxBandSize <= 0  ? 0  : (Convert.ToDouble(Close) - maxLowerBand) / maxBandSize;
        return mdi * 100;
    }
   
}
