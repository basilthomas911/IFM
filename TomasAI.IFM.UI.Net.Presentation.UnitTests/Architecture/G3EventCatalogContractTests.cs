using FluentAssertions;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Architecture;

public sealed class G3EventCatalogContractTests
{
    [Fact]
    public void EveryG3EventFamily_HasAnExplicitUiConsumerRoute()
    {
        var routes = new Dictionary<string, string[]>
        {
            ["TomasAI.IFM.Service.StatusConsole/StatusConsoleEventConsumer.cs"] =
                ["StatusConsoleLoggedEvent"],
            ["TomasAI.IFM.UI.EventConsumer/ApplicationUIEventConsumer.cs"] =
                ["ApplicationStartupEvent", "ApplicationShutdownEvent"],
            ["TomasAI.IFM.UI.EventConsumer/CommandResponseUIEventConsumer.cs"] =
                ["NatsMessagePackDataSerializer", "CommandResponseDescriptor", "base.StartAsync"],
            ["TomasAI.IFM.UI.EventConsumer/EconomicCalendarUIEventConsumer.cs"] =
                [
                    "EconomicCalendarAddedCompleteEvent", "EconomicCalendarAddedFailEvent",
                    "EconomicCalendarChangedCompleteEvent", "EconomicCalendarChangedFailEvent",
                    "EconomicCalendarRemovedCompleteEvent", "EconomicCalendarRemovedFailEvent"
                ],
            ["TomasAI.IFM.UI.EventConsumer/MarketDataUIEventConsumer.cs"] =
                ["NatsEventConsumer", "Subscribe"],
            ["TomasAI.IFM.UI.EventConsumer/FuturesEodDataUIEventConsumer.cs"] =
                ["FuturesEodDataUpdatedNotifyEvent"],
            ["TomasAI.IFM.UI.EventConsumer/FuturesBarDataUIEventConsumer.cs"] =
                ["FuturesBarDataInsertedCompleteEvent"],
            ["TomasAI.IFM.UI.EventConsumer/FuturesRsiSignalUIEventConsumer.cs"] =
                ["FuturesTdiSignalGeneratedCompleteEvent"],
            ["TomasAI.IFM.UI.EventConsumer/FuturesTradeSignalUIEventConsumer.cs"] =
                ["FuturesTradeSignalUpdatedNotifyEvent"],
            ["TomasAI.IFM.UI.EventConsumer/TradePlanUIEventConsumer.cs"] =
                ["TradePlanUpdatedEvent"],
            ["TomasAI.IFM.UI.EventConsumer/TradePositionUIEventConsumer.cs"] =
                ["TradePositionUpdatedEvent"],
            ["TomasAI.IFM.UI.EventConsumer/TradePlacementUIEventConsumer.cs"] =
                ["TradePlacementSetEvent", "TradePlacementWaitEvent", "TradePlacementClearedEvent"],
            ["TomasAI.IFM.UI.EventConsumer/FundUIEventConsumer.cs"] =
                ["FundTransactionCreatedCompleteEvent", "EndOfDayFundTransactionProcessedCompleteEvent"],
            ["TomasAI.IFM.UI.EventConsumer/FundOrderUIEventConsumer.cs"] =
                ["OrderAddedToFundCompleteEvent", "OrderRemovedFromFundCompleteEvent", "TradeAddedToFundOrderCompleteEvent", "TradeRemovedFromFundOrderCompleteEvent"],
            ["TomasAI.IFM.UI.EventConsumer/FundOrderTradeStateUIEventConsumer.cs"] =
                ["FundOrderTradeStateChangedCompleteEvent", "FundOrderTradeStateChangedFailEvent"],
            ["TomasAI.IFM.UI.EventConsumer/MarketDataFeedResetUIEventConsumer.cs"] =
                ["MarketDataFeedResetStreamingEvent"],
            ["TomasAI.IFM.UI.EventConsumer/FuturesOptionTickDataUIEventConsumer.cs"] =
                ["OptionTradeTickPriceDataUpdatedEvent", "ValueTask"],
            ["TomasAI.IFM.UI.EventConsumer/OptionTradeSpreadBarDataUIEventConsumer.cs"] =
                ["OptionTradeSpreadBarDataInsertedCompleteEvent", "ValueTask"],
            ["TomasAI.IFM.UI.EventConsumer/SystemAdminUIEventConsumer.cs"] =
                ["DatabaseOperationCompletedEvent", "DatabaseOperationFailedEvent", "DatabaseOperationErrorRecordedEvent"]
        };

        foreach (var (relativePath, requiredTokens) in routes)
        {
            var source = File.ReadAllText(Path.Combine(
                SolutionSource.RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            foreach (var token in requiredTokens)
                source.Should().Contain(token, $"{relativePath} owns the G3 route for {token}");
        }
    }

    [Fact]
    public void IntradayAnalytics_PreservesEveryConfiguredSignalAndTimeframeBeforeDerivedTdiDisplay()
    {
        var commandModel = Read(
            "TomasAI.IFM.UI.Net.Models/MarketDataAnalyticsCommandModel.cs");
        foreach (var signal in new[] { "StartFuturesRsiSignalAsync", "StartFuturesAtrSignalAsync", "StartFuturesAdxSignalAsync", "StartFuturesMacdSignalAsync" })
            commandModel.Should().Contain(signal);

        var activationProfile = Read(
            "TomasAI.IFM.Domain.MarketData.Analytics.Shared/FuturesIntradaySignalActivationProfile.cs");
        foreach (var timeframe in new[] { "FifteenSeconds", "OneMinute", "FiveMinutes", "FifteenMinutes", "OneHour", "FourHours" })
            activationProfile.Should().Contain(timeframe);

        var tdiConsumer = Read(
            "TomasAI.IFM.UI.EventConsumer/FuturesRsiSignalUIEventConsumer.cs");
        tdiConsumer.Should().Contain("FuturesTdiSignalGeneratedCompleteEvent");
        var tradeSignalConsumer = Read(
            "TomasAI.IFM.UI.EventConsumer/FuturesTradeSignalUIEventConsumer.cs");
        tradeSignalConsumer.Should().Contain("FuturesTradeSignalUpdatedNotifyEvent");
    }

    [Fact]
    public void G3Streams_DeclareLosslessOrLatestValueSemanticsAtTheirUiBoundary()
    {
        var shell = Read("TomasAI.IFM.UI.Net.ViewModels/App/IFMAppViewModel.cs");
        shell.Should().Contain("OrderedBatchAsyncChannel<IEvent>");
        shell.Should().Contain("OrderedBatchAsyncChannel<StatusConsoleLogReadModel>");
        shell.Should().Contain("LatestValueAsyncChannel<FuturesEodDataV2ReadModel>");
        shell.Should().Contain("KeyedLatestValueAsyncChannel<string, FuturesBarDataInsertedCompleteEvent>");
        shell.Should().Contain("LatestValueAsyncChannel<FuturesTradeSignalV2ReadModel>");

        var monitor = Read(
            "TomasAI.IFM.UI.Net.ViewModels/Trade/IronCondor/IronCondorViewModel.cs");
        monitor.Should().Contain("OrderedBatchAsyncChannel<TradePlanReadModel>");
        monitor.Should().Contain("KeyedLatestValueAsyncChannel<string, OptionTradeTickPriceDataUpdatedEvent>");
        monitor.Should().Contain("LatestValueAsyncChannel<OptionTradeSpreadBarDataInsertedCompleteEvent>");
    }

    static string Read(string relativePath)
        => File.ReadAllText(Path.Combine(
            SolutionSource.RootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
