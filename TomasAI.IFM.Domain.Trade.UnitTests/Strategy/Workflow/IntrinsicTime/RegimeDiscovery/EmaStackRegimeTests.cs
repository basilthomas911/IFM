using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesEmaSignal;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RegimeDiscovery.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Model;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.RegimeDiscovery;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RegimeDiscovery.Model;
using Xunit;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.RegimeDiscovery;

public sealed class EmaStackRegimeTests
{
    static readonly DateTimeOffset Now = new(2026, 11, 19, 16, 31, 0, TimeSpan.Zero);
    [Theory]
    [InlineData(30, 20, 10, RegimeDirection.Up)]
    [InlineData(10, 20, 30, RegimeDirection.Down)]
    [InlineData(20, 30, 10, RegimeDirection.Neutral)]
    [InlineData(30, 10, 20, RegimeDirection.Neutral)]
    [InlineData(10, 30, 20, RegimeDirection.Neutral)]
    [InlineData(20, 10, 30, RegimeDirection.Neutral)]
    [InlineData(20, 20, 10, RegimeDirection.Neutral)]
    [InlineData(20, 10, 10, RegimeDirection.Neutral)]
    public async Task All_orderings_are_deterministic_for_every_horizon(int ema20, int ema50, int ema200, RegimeDirection expected)
    {
        var contract = "ES-EMA-" + Guid.NewGuid().ToString("N");
        foreach (var horizon in new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly })
        {
            Publish(contract, horizon, ema20, ema50, ema200);
            var parameters = RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), horizon);
            var request = RegimeDiscoverySnapshotRequestFactory.CreateEmaStack(MarketSeriesIdentity.ForContract(contract), parameters);
            Assert.Equal(3, request.Requirements.Length);
            var capture = await new RegimeDiscoveryMarketSignalSnapshotProvider(new Clock()).CaptureAsync(request);
            Assert.True(capture.IsSuccess);
            var result = await new RegimeDiscoveryCalculationModel().CalculateAsync(new() { ResultId = Guid.NewGuid(), ParameterSet = parameters,
                Snapshot = capture.Snapshot!, CalculationPolicy = EmaStackRegimePolicy.Version, ProducedAtUtc = Now.UtcDateTime });
            Assert.True(result.Decision.IsComplete);
            Assert.Equal(expected, result.Decision.Direction);
            Assert.True(EmaStackRegimePolicy.Applies(result));
            Assert.Equal(3, result.SupportingEvidence.Length);
            var envelope = StrategyStageResultEnvelope.CreateRegime(result);
            Assert.True(envelope.HasValidPayloadSha256());
            Assert.Equal(expected, envelope.ReadRegimeResult().Decision.Direction);
        }
    }

    [Fact]
    public async Task Missing_or_cold_weekly_never_blocks_daily_or_monthly_and_cold_is_not_rangebound()
    {
        var contract = "ES-INDEPENDENT-" + Guid.NewGuid().ToString("N");
        Publish(contract, TimeFrameType.Daily, 30, 20, 10);
        Publish(contract, TimeFrameType.Monthly, 20, 30, 10);
        Publish(contract, TimeFrameType.Weekly, 30, 20, null, false);
        var provider = new RegimeDiscoveryMarketSignalSnapshotProvider(new Clock());
        foreach (var horizon in new[] { TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly })
        {
            var parameters = RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), horizon);
            var request = RegimeDiscoverySnapshotRequestFactory.CreateEmaStack(MarketSeriesIdentity.ForContract(contract), parameters);
            var result = await provider.CaptureAsync(request);
            Assert.Equal(horizon != TimeFrameType.Weekly, result.IsSuccess);
        }
        Publish(contract, TimeFrameType.Weekly, 30, 20, 10);
        var weekly = RegimeDiscoveryParameterSet.CreateDefault(Guid.NewGuid(), Guid.NewGuid(), TimeFrameType.Weekly);
        Assert.True((await provider.CaptureAsync(RegimeDiscoverySnapshotRequestFactory.CreateEmaStack(MarketSeriesIdentity.ForContract(contract), weekly))).IsSuccess);
    }

    static void Publish(string contract, TimeFrameType horizon, decimal ema20, decimal ema50, decimal? ema200, bool warm = true)
    {
        var frame = EmaStackRegimePolicy.Frame(horizon);
        var window = RsiHistoricalSeedWindowModel.Create(frame, 1, Now, new CmeFuturesMarketSessionCalendar())[0];
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesEmaSignalReadModel { IsWarm = warm, Ema20 = ema20, Ema50 = ema50, Ema200 = ema200,
            Metadata = new() { SignalKey = new(MarketSeriesIdentity.ForContract(contract), MarketAnalyticsSignalKind.Ema, frame, "ema"),
                ContractId = contract, ValueDate = window.ValueDate, MarketDataAsOfUtc = window.EndUtc.AddSeconds(-1),
                CalculatedAtUtc = Now, SchemaVersion = 2, IsValid = true, SourceSequence = warm ? 1 : 999999 } });
    }
    sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
