using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.ViewModels;

public sealed class FuturesTradeSignalUIViewModelTests
{
    [Fact]
    public void PartialSnapshot_DisplaysAvailableRsiAndMarksMissingSiblingsUnavailable()
    {
        var snapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = "ESU26",
            ValueDate = new DateOnly(2026, 8, 21),
            Revision = 1,
            UpdatedOn = DateTime.UtcNow,
            FuturesRsiSignal = new FuturesRsiSignalReadModel
            {
                ContractId = "ESU26",
                ValueDate = new DateOnly(2026, 8, 21),
                TimePeriod = TimeFrameType.FifteenSeconds,
                RSI = 64.25
            },
            MissingInputs = "EOD, TDI, ITI direction, ITI extreme, ITI reversal, VX price"
        };

        var model = new FuturesTradeSignalUIViewModel(snapshot);

        model.RSI.Should().Be("64.25");
        model.MDITrend.Should().Be("N/A");
        model.Trend.Should().Be("N/A");
        model.FiftyDMA.Should().Be("N/A");
        model.TrendExtreme.Should().Be("N/A");
        model.TrendReversal.Should().Be("N/A");
    }

    [Fact]
    public void PartialSnapshot_UsesEachItiModeOnlyForItsOwnDisplayField()
    {
        var valueDate = new DateOnly(2026, 8, 21);
        var snapshot = new MarketOutlookSnapshotReadModel
        {
            ContractId = "ESU26",
            ValueDate = valueDate,
            Revision = 2,
            UpdatedOn = DateTime.UtcNow,
            TrendExtremeChange = new FuturesItiSignalV2ReadModel
            {
                ContractId = "ESU26",
                ValueDate = valueDate,
                IntrinsicTimeMode = IntrinsicTimeModeType.TrendExtremeChanged,
                TrendExtreme = 6_525.5
            }
        };

        var model = new FuturesTradeSignalUIViewModel(snapshot);

        model.TrendExtreme.Should().Be("6525.50");
        model.Trend.Should().Be("N/A");
        model.TrendReversal.Should().Be("N/A");
    }
}
