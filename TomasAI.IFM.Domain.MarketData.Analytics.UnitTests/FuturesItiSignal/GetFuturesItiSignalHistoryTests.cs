using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Query;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Queries;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

namespace TomasAI.IFM.Domain.MarketData.Analytics.UnitTests.FuturesItiSignal;

public sealed class GetFuturesItiSignalHistoryTests
{
    const string ContractId = "ES-HISTORY-UNIT";
    static readonly DateOnly Tuesday = new(2026, 9, 8);

    [Fact]
    public async Task History_ReadsCompleteWindowAndReturnsRequestedPeriodChronologically()
    {
        var database = Substitute.For<IMarketDataDbContext>();
        var factory = Substitute.For<IDbContextFactory>();
        factory.MarketDataDb.Returns(database);
        database.GetFuturesItiSignalsForContractAsync(
                ContractId,
                new DateOnly(2026, 9, 2),
                new DateOnly(2026, 9, 8))
            .Returns(Task.FromResult<ICollection<FuturesItiSignalV2ReadModel>>(
            [
                Signal(TimeFrameType.Weekly, sequenceId: 2, Tuesday, hour: 15),
                Signal(TimeFrameType.Daily, sequenceId: 1, Tuesday, hour: 13),
                Signal(TimeFrameType.Weekly, sequenceId: 1, Tuesday.AddDays(-1), hour: 13)
            ]));
        var query = new GetFuturesItiSignalHistoryQuery(
            ContractId,
            Tuesday,
            TimeFrameType.Weekly);

        var result = await query.GetFuturesItiSignalHistoryAsync(factory);

        result.Select(signal => signal.SequenceId).Should().Equal(1, 2);
        result.Should().OnlyContain(signal => signal.TimePeriod == TimeFrameType.Weekly);
        await database.Received(1).GetFuturesItiSignalsForContractAsync(
            ContractId,
            new DateOnly(2026, 9, 2),
            new DateOnly(2026, 9, 8));
    }

    static FuturesItiSignalV2ReadModel Signal(
        TimeFrameType timePeriod,
        long sequenceId,
        DateOnly valueDate,
        int hour)
        => new(
            ContractId,
            valueDate,
            timePeriod,
            sequenceId,
            valueDate.ToDateTime(new TimeOnly(hour, 0), DateTimeKind.Utc),
            0,
            0,
            5_000 + sequenceId,
            IntrinsicTimeTrendType.UpTrend,
            IntrinsicTimeModeType.Trending,
            5_000,
            5_001,
            5_000,
            1,
            1,
            0.003,
            5,
            10,
            5_010,
            4_990,
            IntrinsicTimeTradeState.Ready,
            Tuesday,
            5_000,
            0.1,
            1,
            0.1,
            0);
}
