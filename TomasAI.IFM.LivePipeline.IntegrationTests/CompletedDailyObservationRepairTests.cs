using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Remote;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Application.MarketData.Historical;
using TomasAI.IFM.Domain.MarketData.Feed.Shared.TickAggregation;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.FuturesTradeSessionBarSignal;
using TomasAI.IFM.Framework.MarketData.DataBento;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class CompletedDailyObservationRepairTests
{
    const string Contract = "ES20261218";
    static readonly CmeFuturesMarketSessionCalendar Calendar = new();
    static readonly DateOnly Date = new(2026, 11, 17);
    static readonly MarketSeriesIdentity Series = MarketSeriesIdentity.ForFuturesSeries(new("ES", "calendar-front", "unadjusted", 1));

    [Fact]
    public void Full_original_interval_coverage_recovers_daily_ohlcv_without_inventing_trade_statistics()
    {
        var session = Calendar.GetSession(Date);
        var windows = Windows();
        var bars = Bars();
        var result = StoredSessionIndicatorSeedReader.AggregateCompletedDaily(Series, Contract, session, windows, bars);

        Assert.NotNull(result);
        Assert.Equal(session.StartUtc, result.SessionStartUtc);
        Assert.Equal(session.EndUtc, result.SessionEndUtc);
        Assert.Equal(bars[0].Open, result.Open);
        Assert.Equal(bars.Max(bar => bar.High), result.High);
        Assert.Equal(bars.Min(bar => bar.Low), result.Low);
        Assert.Equal(bars[^1].Close, result.Close);
        Assert.Equal(bars.Sum(bar => bar.Volume), result.Volume);
        Assert.Equal(0, result.TradeCount);
        Assert.Equal(0, result.PriceVolumeSum);
        Assert.Equal(bars[^1].LastMarketEventUtc, result.LastMarketEventUtc);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("partial")]
    [InlineData("wrong-contract")]
    [InlineData("unqualified-live")]
    public void Incomplete_or_unqualified_coverage_cannot_repair_a_daily_session(string issue)
    {
        var bars = Bars();
        if (issue == "missing") bars = bars[..^1];
        else if (issue == "duplicate") bars = [.. bars, bars[^1]];
        else bars[^1] = issue switch
        {
            "partial" => bars[^1] with { IsComplete = false },
            "wrong-contract" => bars[^1] with { ContractId = "ES20270319" },
            _ => bars[^1] with { CalculationVersion = "trade-session-bar-v1" }
        };
        Assert.Null(StoredSessionIndicatorSeedReader.AggregateCompletedDaily(Series, Contract, Calendar.GetSession(Date), Windows(), bars));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    public async Task Repair_uses_durable_qualified_coverage_and_is_idempotent_without_provider_or_tick_reads(bool beforeNativeStartup, int dayOffset)
    {
        var date = Date.AddDays(dayOffset);
        var db = Substitute.For<IDbContextFactory>();
        var market = Substitute.For<TomasAI.IFM.Application.MarketData.Contracts.IMarketDataApi>();
        var raw = (IHistoricalObservationStore)db.MarketDataDb;
        FuturesEodObservationReadModel? saved = null;
        raw.GetRawEodAsync(Series, date, Arg.Any<CancellationToken>()).Returns(_ => new ValueTask<FuturesEodObservationReadModel?>(saved));
        raw.TryWriteRawEodAsync(Arg.Any<FuturesEodObservationReadModel>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            saved = call.Arg<FuturesEodObservationReadModel>();
            return new ValueTask<bool>(true);
        });
        db.MarketDataDb.GetQualifiedIndicatorSeedBarsAsync(Contract, TimeFrameType.FourHours, date,
            Arg.Any<DateTimeOffset[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Series)
            .Returns(Task.FromResult<IReadOnlyList<FuturesTradeSessionBarReadModel>>(Bars(date)));
        var options = new DatabentoMarketDataRuntimeOptions
        {
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development, "GLBX.MDP3") with { DataSource = FeedDataSourceMode.DatabentoLive },
            Contracts = [new() { DomainContractId = Contract, ProviderContractName = "ESZ6", AssetTypeId = AssetTypeId.Futures }]
        };
        db.SecuritiesDb.GetFuturesContractAsync(Contract, Arg.Any<CancellationToken>())
            .Returns(new FuturesContractV3ReadModel { ContractId = Contract, LocalSymbol = "ESZ6" });
        var reader = new StoredSessionIndicatorSeedReader(db, Calendar, new Clock(Calendar.GetSession(date).EndUtc.AddHours(1)),
            NullLogger<StoredSessionIndicatorSeedReader>.Instance, market, beforeNativeStartup ? null : options,
            beforeNativeStartup ? new MarketDataReadMirror(Substitute.For<IOptionChainCache>()) : null);

        await reader.RepairAsync(Series, Contract, date, default);
        await reader.RepairAsync(Series, Contract, date, default);

        Assert.NotNull(saved);
        Assert.Equal(date, saved.ValueDate);
        Assert.Equal(Bars(date)[^1].LastMarketEventUtc, saved.LastMarketEventUtc);
        await raw.Received(1).TryWriteRawEodAsync(Arg.Any<FuturesEodObservationReadModel>(), Arg.Any<CancellationToken>());
        await market.DidNotReceive().ReadIndicatorSeedBarsAsync(Arg.Any<string>(), Arg.Any<TimeFrameType>(), Arg.Any<CancellationToken>());
        await market.DidNotReceive().GetFuturesContractAsync(Arg.Any<string>());
        Assert.Empty(db.TradeTickQuoteDb.ReceivedCalls());
    }

    [Fact]
    public void Next_six_pm_rollover_uses_new_completed_session_and_rejects_previous_session_coverage()
    {
        var boundary = new DateTimeOffset(2026, 11, 18, 23, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 19), Calendar.GetValueDate(boundary));
        var completed = new DateOnly(2026, 11, 18);
        Assert.Null(StoredSessionIndicatorSeedReader.AggregateCompletedDaily(Series, Contract,
            Calendar.GetSession(completed), Windows(completed), Bars()));
        var result = StoredSessionIndicatorSeedReader.AggregateCompletedDaily(Series, Contract,
            Calendar.GetSession(completed), Windows(completed), Bars(completed));
        Assert.NotNull(result);
        Assert.Equal(completed, result.ValueDate);
        Assert.Equal(Bars(completed)[^1].LastMarketEventUtc, result.LastMarketEventUtc);
    }

    static MarketSessionBounds[] Windows(DateOnly? valueDate = null) => RsiHistoricalSeedWindowModel.Create(TimeFrameType.FourHours, 8,
        Calendar.GetSession(valueDate ?? Date).EndUtc, Calendar).Where(window => window.ValueDate == (valueDate ?? Date)).ToArray();

    static FuturesTradeSessionBarReadModel[] Bars(DateOnly? valueDate = null) => Windows(valueDate).Select((window, index) => new FuturesTradeSessionBarReadModel
    {
        MarketSeriesIdentity = Series, ContractId = Contract, ValueDate = valueDate ?? Date, TimeFrame = TimeFrameType.FourHours,
        ObservationId = FuturesTradeSessionBarId.Create(Series, TimeFrameType.FourHours, window.EndUtc, 0),
        IntervalStartUtc = window.StartUtc, IntervalEndUtc = window.EndUtc,
        Open = 100 + index, High = 102 + index, Low = 99 + index, Close = 101 + index, Volume = 10 + index,
        FirstMarketEventUtc = window.StartUtc, LastMarketEventUtc = window.EndUtc.AddMinutes(-1),
        CalculatedAtUtc = Calendar.GetSession(valueDate ?? Date).EndUtc, IsComplete = true, IsValid = true,
        CalculationVersion = "databento-historical-minute-seed-v1", CalculationMethod = MarketSignalCalculationMethod.NormalizedHistoricalAggregate
    }).ToArray();

    sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
