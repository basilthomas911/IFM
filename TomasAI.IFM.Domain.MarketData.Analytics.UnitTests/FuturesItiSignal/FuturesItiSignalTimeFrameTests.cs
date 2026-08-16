using System.Reflection;
using FluentAssertions;
using MessagePack;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Realtime;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Events;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class FuturesItiSignalTimeFrameTests
{
    const string ContractId = "ES20260918";
    static readonly DateOnly Tuesday = new(2026, 9, 8);
    static readonly DateTime Timestamp = new(2026, 9, 8, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task FirstObservedTick_StartsIndependentDailyWeeklyAndMonthlyGroupZeroStreams()
    {
        var (state, _) = CreateState();

        var evaluations = await state.EvaluateAsync(
            ContractId, Tuesday, Timestamp, 5_000, 20);

        evaluations.Should().HaveCount(3);
        evaluations.Select(x => x.Command.TimePeriod).Should().BeEquivalentTo(new[]
        {
            TimeFrameType.Daily,
            TimeFrameType.Weekly,
            TimeFrameType.Monthly
        });
        evaluations.Should().OnlyContain(x =>
            x.Command.TimeFrameStartValueDate == Tuesday
            && x.Command.EntityId.ValueDate == Tuesday
            && x.Signal.IntrinsicTimeGroupId == 0
            && x.Signal.TimeFrameStartValueDate == Tuesday
            && x.Signal.BandPercentage == 0.10
            && x.Signal.BandSize == x.Signal.Threshold * 0.10);
    }

    [Fact]
    public async Task RepeatedPriceInsideBand_UpdatesHotStateWithoutAnotherDurableEvaluation()
    {
        var (state, db) = CreateState();
        var first = await state.EvaluateAsync(
            ContractId, Tuesday, Timestamp, 5_000, 20);
        foreach (var evaluation in first)
            state.Confirm(evaluation);

        var repeated = await state.EvaluateAsync(
            ContractId, Tuesday, Timestamp.AddSeconds(1), 5_000, 20);

        repeated.Should().BeEmpty();
        await db.Received(3).GetFuturesItiTimeFrameStateAsync(
            ContractId,
            Arg.Any<TimeFrameType>(),
            Arg.Any<DateOnly>(),
            Arg.Any<CancellationToken>());
        await db.Received(3).GetFuturesItiSignalsForContractAsync(
            ContractId,
            Arg.Any<DateOnly>(),
            Tuesday);
    }

    [Fact]
    public async Task OneFullThresholdBand_PublishesEachPeriodAgain()
    {
        var (state, _) = CreateState();
        var first = await state.EvaluateAsync(
            ContractId, Tuesday, Timestamp, 5_000, 20);
        foreach (var evaluation in first)
            state.Confirm(evaluation);
        var movement = first.Max(x => x.Signal.BandSize) + 0.01;

        var next = await state.EvaluateAsync(
            ContractId,
            Tuesday,
            Timestamp.AddSeconds(1),
            5_000 + movement,
            20);

        next.Should().HaveCount(3);
        next.Should().OnlyContain(x =>
            x.Signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendExtremeChanged
            && x.Signal.IntrinsicTimeGroupId == 0);
    }

    [Fact]
    public async Task RestartMidWeek_ReusesProjectedFirstObservedTradingDate()
    {
        var frameStart = new DateOnly(2026, 9, 8);
        var restartDate = new DateOnly(2026, 9, 10);
        var projectedWeekly = Signal(
            TimeFrameType.Weekly,
            restartDate,
            frameStart,
            groupId: 4);
        var (state, db) = CreateState((period, _) =>
            period == TimeFrameType.Weekly ? projectedWeekly : null);

        var evaluations = await state.EvaluateAsync(
            ContractId, restartDate, Timestamp.AddDays(2), 5_000, 20);

        var weekly = evaluations.SingleOrDefault(x =>
            x.Command.TimePeriod == TimeFrameType.Weekly);
        weekly.Should().BeNull("an unchanged projected weekly price is inside its band");
        await db.Received(1).GetFuturesItiTimeFrameStateAsync(
            ContractId,
            TimeFrameType.Weekly,
            new DateOnly(2026, 9, 7),
            Arg.Any<CancellationToken>());

        var moved = await state.EvaluateAsync(
            ContractId, restartDate, Timestamp.AddDays(2).AddSeconds(1), 5_002, 20);
        moved.Single(x => x.Command.TimePeriod == TimeFrameType.Weekly)
            .Command.TimeFrameStartValueDate.Should().Be(frameStart);
    }

    [Fact]
    public async Task FirstTradeAfterMondayHoliday_UsesTuesdayAsWeeklyFrameStart()
    {
        var (state, _) = CreateState();

        var evaluations = await state.EvaluateAsync(
            ContractId, Tuesday, Timestamp, 5_000, 20);

        evaluations.Single(x => x.Command.TimePeriod == TimeFrameType.Weekly)
            .Command.TimeFrameStartValueDate.Should().Be(Tuesday);
    }

    [Fact]
    public async Task FirstTickInNewWeekAndMonth_ResetsBothStreamsToGroupZero()
    {
        var friday = new DateOnly(2026, 10, 30);
        var monday = new DateOnly(2026, 11, 2);
        var (state, _) = CreateState();
        var prior = await state.EvaluateAsync(
            ContractId, friday, Timestamp, 5_000, 20);
        foreach (var evaluation in prior)
            state.Confirm(evaluation);

        var next = await state.EvaluateAsync(
            ContractId, monday, Timestamp.AddDays(3), 5_000, 20);

        next.Single(x => x.Command.TimePeriod == TimeFrameType.Weekly)
            .Signal.IntrinsicTimeGroupId.Should().Be(0);
        next.Single(x => x.Command.TimePeriod == TimeFrameType.Monthly)
            .Signal.IntrinsicTimeGroupId.Should().Be(0);
        next.Where(x => x.Command.TimePeriod is TimeFrameType.Weekly or TimeFrameType.Monthly)
            .Should().OnlyContain(x => x.Command.TimeFrameStartValueDate == monday);
    }

    [Fact]
    public void Evaluator_DirectionIsImmediateAndOnlyDirectionIncrementsGroup()
    {
        var current = Signal(TimeFrameType.Daily, Tuesday, Tuesday, groupId: 3) with
        {
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            DownTrendTrigger = 4_990,
            TrendExtreme = 5_000,
            TrendReversal = 5_000,
            BandAnchorPrice = 5_000,
            BandSize = 1
        };
        var command = Command(TimeFrameType.Daily, 4_990);

        FuturesItiSignalCompute.TryCompute(command, current, out var changed)
            .Should().BeTrue();
        changed.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendDirectionChanged);
        changed.IntrinsicTimeTrend.Should().Be(IntrinsicTimeTrendType.DownTrend);
        changed.IntrinsicTimeGroupId.Should().Be(4);
    }

    [Fact]
    public void Evaluator_ExtremeReversalAndTrendingRequireAFullBand()
    {
        var current = Signal(TimeFrameType.Daily, Tuesday, Tuesday, groupId: 2) with
        {
            IntrinsicTimeTrend = IntrinsicTimeTrendType.UpTrend,
            DownTrendTrigger = 4_900,
            TrendExtreme = 5_010,
            TrendReversal = 4_990,
            BandAnchorPrice = 5_000,
            BandSize = 2
        };

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_001.99), current, out _)
            .Should().BeFalse();

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_012), current, out var extreme)
            .Should().BeTrue();
        extreme.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendExtremeChanged);
        extreme.IntrinsicTimeGroupId.Should().Be(2);

        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 4_988), current, out var reversal)
            .Should().BeTrue();
        reversal.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.TrendReversalChanged);
        reversal.IntrinsicTimeGroupId.Should().Be(2);

        var rangeCurrent = current with
        {
            TrendExtreme = 5_100,
            TrendReversal = 4_900
        };
        FuturesItiSignalCompute.TryCompute(Command(TimeFrameType.Daily, 5_002), rangeCurrent, out var trending)
            .Should().BeTrue();
        trending.IntrinsicTimeMode.Should().Be(IntrinsicTimeModeType.Trending);
        trending.IntrinsicTimeGroupId.Should().Be(2);
    }

    [Theory]
    [InlineData(typeof(GenerateFuturesItiSignalCommand), nameof(GenerateFuturesItiSignalCommand.TimeFrameStartValueDate), 12)]
    [InlineData(typeof(FuturesItiSignalGeneratedEvent), nameof(FuturesItiSignalGeneratedEvent.DeriveLongerPeriods), 12)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.TimeFrameStartValueDate), 21)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.BandAnchorPrice), 22)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.BandPercentage), 23)]
    [InlineData(typeof(FuturesItiSignalV2ReadModel), nameof(FuturesItiSignalV2ReadModel.BandSize), 24)]
    public void MessagePackContracts_PreserveEstablishedAndAdditiveKeys(
        Type contractType,
        string propertyName,
        int expectedKey)
    {
        var key = contractType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
            ?.GetCustomAttribute<KeyAttribute>();

        key.Should().NotBeNull();
        key!.IntKey.Should().Be(expectedKey);
    }

    static GenerateFuturesItiSignalCommand Command(TimeFrameType period, double price)
        => new(ContractId, Tuesday, period, Timestamp.AddSeconds(1), price, 20, Tuesday);

    static FuturesItiSignalV2ReadModel Signal(
        TimeFrameType period,
        DateOnly valueDate,
        DateOnly frameStart,
        int groupId)
        => new(
            ContractId,
            valueDate,
            period,
            10,
            Timestamp,
            groupId,
            0,
            5_000,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeModeType.Trending,
            5_000,
            5_000,
            5_000,
            0,
            0,
            0.003,
            period == TimeFrameType.Daily ? 1 : period == TimeFrameType.Weekly ? 5 : 20,
            10,
            5_000,
            4_990,
            IntrinsicTimeTradeState.Ready,
            frameStart,
            5_000,
            0.10,
            1);

    static (FuturesItiSignalRealtimeState State, IMarketDataDbContext Db) CreateState(
        Func<TimeFrameType, DateOnly, FuturesItiSignalV2ReadModel?>? projected = null)
    {
        var db = Substitute.For<IMarketDataDbContext>();
        db.GetFuturesItiTimeFrameStateAsync(
                Arg.Any<string>(),
                Arg.Any<TimeFrameType>(),
                Arg.Any<DateOnly>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(projected?.Invoke(
                call.ArgAt<TimeFrameType>(1),
                call.ArgAt<DateOnly>(2))));
        db.GetFuturesItiSignalsForContractAsync(
                Arg.Any<string>(),
                Arg.Any<DateOnly>(),
                Arg.Any<DateOnly>())
            .Returns(Task.FromResult<ICollection<FuturesItiSignalV2ReadModel>>([]));
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(db);
        return (new FuturesItiSignalRealtimeState(factory), db);
    }
}
