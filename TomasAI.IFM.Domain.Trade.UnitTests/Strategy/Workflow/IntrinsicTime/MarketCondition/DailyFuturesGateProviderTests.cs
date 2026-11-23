using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Domain.MarketData.Analytics.RegimeDiscovery;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.FuturesMarketPrice.Events;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Configuration.MarketCondition;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Model;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.MarketCondition.Model;
using TomasAI.IFM.Tests.Support;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime.MarketCondition;

public sealed class DailyFuturesGateProviderTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(0d, -1d)]
    [InlineData(6d, 5d/6)]
    public async Task Open_uses_preopen_atr_provenance_without_history_database_or_vx_reads_and_missing_cache_fails_closed(double? baseline, double? ratio)
    {
        var contract = "ES-GATE-" + Guid.NewGuid().ToString("N");
        var (command,_) = DailyFuturesGateTestScenario.Create("open",contractId:contract);
        var at = command.RequestedAtUtc;
        var market = Substitute.For<IMarketDataApi>();
        market.TryGetOnTheRunFuturesContract("ES",out Arg.Any<FuturesContractV3ReadModel>()).Returns(call =>
        { call[1] = new FuturesContractV3ReadModel { ContractId = contract, Symbol = "ES" }; return true; });
        market.GetFuturesMarketHealth(contract).Returns(new FuturesMarketHealthSnapshot(true,true,"generation",DateOnly.FromDateTime(at),new(at),1));
        market.TryGetLastTickPrice(contract,out Arg.Any<FuturesMarketPriceSnapshot>()).Returns(call =>
        { call[1] = new FuturesMarketPriceSnapshot(contract,1,1,default,DateOnly.FromDateTime(at),
            new FuturesMarketQuoteSnapshot(5000,10,5000.25m,10,1,1,1,new(at),new(at)),
            new FuturesMarketTradeSnapshot(5000,1,1,new(at),new(at))); return true; });
        var calendar = new CmeFuturesMarketSessionCalendar();
        var storage = Substitute.For<IDbContextFactory>();
        var events = Substitute.For<IMarketConditionEventRiskAdapter>();
        events.ReadOnceAsync(Arg.Any<MarketConditionEventRiskConfiguration>(),at,Arg.Any<CancellationToken>()).Returns(new MarketConditionEventRiskState
        { Status = MarketEventRiskStatus.Clear, DownloadEvidence = new() { CoverageConfirmed = true, CheckedAtUtc = at, ValidUntilUtc = at.AddMinutes(30) },
            Observation = new() { SourceTimestampUtc = at, ReceivedAtUtc = at, SequenceId = 1, Availability = MarketSourceAvailability.Available, Validity = MarketSourceValidity.Valid } });
        var pipeline = new LivePipelineEvidence(new Clock(at));
        pipeline.PublishAudit(new(at,DateOnly.FromDateTime(at),"Degraded",
            [new("MarketData dataset","Futures/XCBF.PITCH","Degraded","VX unavailable",at)]));
        Assert.False(pipeline.AllowsNewDecisions);
        var provider = new MarketConditionAssessmentSnapshotProvider(market,storage,calendar,events,pipeline,atrCache:new FuturesAtrHotCacheReader());
        var cold = await provider.CaptureDailyFuturesMarketAsync(command.ParameterSet,at,default);
        var coldResult = new MarketConditionAssessmentCalculator().Calculate(command,cold,command.CommandId);
        Assert.Contains("MC.DAILY_FUTURES.ATR_NOT_READY",coldResult.Assessment.LimitationReasons);
        var source = at.AddMinutes(-5).AddSeconds(-1); // Last completed preopen bar; original time stays intact.
        RegimeDiscoverySignalCacheAdapter.Publish(new FuturesAtrSignalReadModel { ContractId = contract, ValueDate = DateOnly.FromDateTime(at),
            TimePeriod = TimeFrameType.FiveMinutes, PeriodLength = 14, IsWarm = true, AtrValue = 5, AtrBaseline = baseline, AtrRatio = ratio,
            Metadata = new() { ContractId = contract, SignalKey = new(MarketSeriesIdentity.ForContract(contract),MarketAnalyticsSignalKind.Atr,TimeFrameType.FiveMinutes,"atr-14"),
                IsValid = true, MarketDataAsOfUtc = new(source), CalculatedAtUtc = new(source.AddSeconds(1)), SourceSequence = 1, SchemaVersion = 2 } });
        var snapshot = await provider.CaptureDailyFuturesMarketAsync(command.ParameterSet,at,default);
        var result = new MarketConditionAssessmentCalculator().Calculate(command,snapshot,command.CommandId);
        Assert.Equal(AssessmentAvailability.Available,result.Assessment.Availability);
        Assert.Equal(source,snapshot.Observations.Single(o => o.SourceId == "Atr14").ObservedAtUtc);
        if (baseline is null)
        {
            var legacySnapshot = await provider.CaptureDailyFuturesLegacyAsync(command.ParameterSet,at,default);
            var legacyResult = new MarketConditionAssessmentCalculator().Calculate(
                command with { CalculationPolicy = DailyFuturesGatePolicy.LegacyVersion },legacySnapshot,command.CommandId);
            Assert.Contains("MC.DAILY_FUTURES.ATR_NOT_READY",legacyResult.Assessment.LimitationReasons);
        }
        Assert.Empty(storage.ReceivedCalls());
        market.DidNotReceive().TryGetOnTheRunFuturesContract("VX",out Arg.Any<FuturesContractV3ReadModel>());
    }
    sealed class Clock(DateTime at) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(at); }
}
