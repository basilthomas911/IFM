using FluentAssertions;
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

    static FuturesEodDataV2ReadModel Eod(
        double dailyPercentChange,
        PriceDirectionType direction) => new(
            "ES20260918",
            new DateOnly(2026, 8, 21),
            "ES",
            5400m,
            5500m,
            5350m,
            5425m,
            100_000,
            dailyPercentChange,
            priceDirection: direction);
}
