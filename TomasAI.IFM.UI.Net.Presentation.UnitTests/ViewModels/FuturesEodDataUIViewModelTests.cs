using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesVwapSignal;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.Presentation;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class FuturesEodDataUIViewModelTests
{
    [Theory]
    [InlineData(0.0046, PriceDirectionType.Rising, PresentationColorRole.Positive)]
    [InlineData(-0.0046, PriceDirectionType.Falling, PresentationColorRole.Negative)]
    [InlineData(0, PriceDirectionType.Flat, PresentationColorRole.Caution)]
    public void Daily_change_is_formatted_as_a_percentage_without_recalculation(
        double ratio,
        PriceDirectionType direction,
        PresentationColorRole expectedChangeColor)
    {
        var source = Eod(ratio, direction);

        var viewModel = new FuturesEodDataUIViewModel(source);

        viewModel.OpenPrice.Should().Be("5400.00");
        viewModel.ClosePrice.Should().Be("5425.00");
        viewModel.DailyPercentChange.Should().Be($"{ratio:P2}");
        viewModel.DailyPercentChangeBackColor.Should().Be(expectedChangeColor);
        viewModel.PriceDirection.Should().Be(direction.ToString());
    }

    [Fact]
    public void A_new_live_value_creates_a_new_percentage_presentation()
    {
        var first = new FuturesEodDataUIViewModel(Eod(0.0046, PriceDirectionType.Rising));
        var second = new FuturesEodDataUIViewModel(Eod(-0.0046, PriceDirectionType.Falling));

        first.DailyPercentChange.Should().Be($"{0.0046:P2}");
        second.DailyPercentChange.Should().Be($"{-0.0046:P2}");
        second.DailyPercentChange.Should().NotBe(first.DailyPercentChange);
    }

    [Theory]
    [InlineData(PriceVolatilityType.Rising)]
    [InlineData(PriceVolatilityType.Falling)]
    [InlineData(PriceVolatilityType.Flat)]
    public void Price_volatility_classification_is_displayed_without_reinterpretation(
        PriceVolatilityType priceVolatility)
    {
        var viewModel = new FuturesEodDataUIViewModel(
            Eod(0.0046, PriceDirectionType.Rising, priceVolatility));

        viewModel.PriceVolatility.Should().Be(priceVolatility.ToString());
    }

    [Theory]
    [InlineData(5426, PresentationColorRole.Negative)]
    [InlineData(5424, PresentationColorRole.Positive)]
    [InlineData(5425, PresentationColorRole.Caution)]
    public void Vwap_value_background_compares_vwap_with_the_close_price(
        int vwap,
        PresentationColorRole expectedBackground)
    {
        var viewModel = new FuturesEodDataUIViewModel(Snapshot(vwap));

        viewModel.Vwap.Should().Be($"{vwap:F2}");
        viewModel.VwapForeColor.Should().Be(PresentationColorRole.DarkText);
        viewModel.VwapBackColor.Should().Be(expectedBackground);
    }

    [Fact]
    public void Invalid_vwap_is_presented_as_unavailable()
    {
        var viewModel = new FuturesEodDataUIViewModel(Snapshot(5426m, isValid: false));

        viewModel.Vwap.Should().Be("N/A");
        viewModel.VwapForeColor.Should().Be(PresentationColorRole.LightText);
        viewModel.VwapBackColor.Should().Be(PresentationColorRole.Default);
    }

    [Fact]
    public void Five_minute_indicators_use_the_market_outlook_red_yellow_green_ranges()
    {
        var snapshot = Snapshot(5426m) with
        {
            FuturesAdxSignal = new FuturesAdxSignalReadModel
            {
                ContractId = "ES20260918",
                ValueDate = new DateOnly(2026, 8, 21),
                TimePeriod = TimeFrameType.FiveMinutes,
                PeriodLength = 14,
                AdxValue = 31d,
                PlusDI = 27d,
                MinusDI = 14d,
                IsWarm = true
            },
            FuturesAtrSignal = new FuturesAtrSignalReadModel
            {
                ContractId = "ES20260918",
                ValueDate = new DateOnly(2026, 8, 21),
                TimePeriod = TimeFrameType.FiveMinutes,
                PeriodLength = 14,
                AtrValue = 8.25d,
                AtrRatio = 1.33d,
                IsWarm = true
            },
            FuturesMacdSignal = new FuturesMacdSignalReadModel
            {
                ContractId = "ES20260918",
                ValueDate = new DateOnly(2026, 8, 21),
                TimePeriod = TimeFrameType.FiveMinutes,
                SignalEmaPeriod = 9,
                FastEmaPeriod = 12,
                SlowEmaPeriod = 26,
                Histogram = -2.5d,
                IsWarm = true
            }
        };

        var viewModel = new FuturesEodDataUIViewModel(snapshot);

        viewModel.Adx.Should().Be("31.00");
        viewModel.AdxBackColor.Should().Be(PresentationColorRole.Positive);
        viewModel.Atr.Should().Be("8.25");
        viewModel.AtrBackColor.Should().Be(PresentationColorRole.Caution);
        viewModel.Macd.Should().Be("-2.50");
        viewModel.MacdBackColor.Should().Be(PresentationColorRole.Negative);
    }

    [Fact]
    public void Indicators_remain_neutral_until_their_five_minute_state_is_warm()
    {
        var snapshot = Snapshot(5426m) with
        {
            FuturesAdxSignal = new FuturesAdxSignalReadModel { AdxValue = 30d },
            FuturesAtrSignal = new FuturesAtrSignalReadModel { AtrValue = 8d, AtrRatio = 1d },
            FuturesMacdSignal = new FuturesMacdSignalReadModel { Histogram = 3d }
        };

        var viewModel = new FuturesEodDataUIViewModel(snapshot);

        viewModel.Adx.Should().Be("N/A");
        viewModel.Atr.Should().Be("N/A");
        viewModel.Macd.Should().Be("N/A");
        viewModel.AdxBackColor.Should().Be(PresentationColorRole.Default);
        viewModel.AtrBackColor.Should().Be(PresentationColorRole.Default);
        viewModel.MacdBackColor.Should().Be(PresentationColorRole.Default);
    }
    static MarketOutlookReadModel Snapshot(decimal vwap, bool isValid = true) => new()
    {
        ContractId = "ES20260918",
        ValueDate = new DateOnly(2026, 8, 21),
        FuturesEodData = Eod(0.0046, PriceDirectionType.Rising),
        FuturesVwapSignal = new FuturesVwapSignalReadModel
        {
            ContractId = "ES20260918",
            ValueDate = new DateOnly(2026, 8, 21),
            Vwap = vwap,
            IsWarm = true,
            IsValid = isValid,
            IsTickExact = true
        }
    };
    static FuturesEodDataV2ReadModel Eod(
        double dailyPercentChange,
        PriceDirectionType direction,
        PriceVolatilityType priceVolatility = PriceVolatilityType.Unknown) => new(
            "ES20260918",
            new DateOnly(2026, 8, 21),
            "ES",
            5400m,
            5500m,
            5350m,
            5425m,
            100_000,
            dailyPercentChange,
            priceDirection: direction,
            priceVolatility: priceVolatility);
}
