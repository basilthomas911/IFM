using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Query.Model;

namespace TomasAI.IFM.Domain.Trade.UnitTests.Strategy.Workflow.IntrinsicTime;

public sealed class IntrinsicTimeStrategyWorkflowHistoryIdentityTests
{
    [Fact]
    public void MonthlyRange_UsesTheFirstOfTheMonthBucket()
    {
        var result = IntrinsicTimeStrategyWorkflowQueryModel.ResolveCalendarBucketStarts(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 21),
            TimeFrameType.Monthly);

        result.Should().Equal(new DateOnly(2026, 9, 1));
    }

    [Fact]
    public void RollingWeeklyRange_IncludesBothIntersectedMondayBuckets()
    {
        var result = IntrinsicTimeStrategyWorkflowQueryModel.ResolveCalendarBucketStarts(
            new DateOnly(2026, 9, 15),
            new DateOnly(2026, 9, 21),
            TimeFrameType.Weekly);

        result.Should().Equal(
            new DateOnly(2026, 9, 14),
            new DateOnly(2026, 9, 21));
    }

    [Fact]
    public void OvernightDailyRange_IncludesBothCalendarDateBuckets()
    {
        var result = IntrinsicTimeStrategyWorkflowQueryModel.ResolveCalendarBucketStarts(
            new DateOnly(2026, 9, 20),
            new DateOnly(2026, 9, 21),
            TimeFrameType.Daily);

        result.Should().Equal(
            new DateOnly(2026, 9, 20),
            new DateOnly(2026, 9, 21));
    }

    [Theory]
    [InlineData("IntrinsicTimeStrategy.ES20260918.20260901.Monthly")]
    [InlineData("IntrinsicTimeStrategy.ES20261218.20260901.Monthly")]
    [InlineData("IntrinsicTimeStrategy.ESU25.20260901.Monthly")]
    public void MonthlyHistory_MatchesHistoricalContractsForTheSymbol(string workflowEntityId)
    {
        var result = IntrinsicTimeStrategyWorkflowQueryModel.MatchesHistoryEntity(
            workflowEntityId,
            "ES",
            [new DateOnly(2026, 9, 1)],
            TimeFrameType.Monthly);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("IntrinsicTimeStrategy.ES20260918.20260914.Weekly")]
    [InlineData("IntrinsicTimeStrategy.ES20261218.20260914.Weekly")]
    public void WeeklyHistory_MatchesBothSidesOfAMidweekRollover(string workflowEntityId)
    {
        var result = IntrinsicTimeStrategyWorkflowQueryModel.MatchesHistoryEntity(
            workflowEntityId,
            "ES",
            [new DateOnly(2026, 9, 14)],
            TimeFrameType.Weekly);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("IntrinsicTimeStrategy.NQ20260918.20260901.Monthly")]
    [InlineData("IntrinsicTimeStrategy.ES-RDV-TEST.20260901.Monthly")]
    [InlineData("IntrinsicTimeStrategy.ES20260918.20260801.Monthly")]
    [InlineData("IntrinsicTimeStrategy.ES20260918.20260901.Weekly")]
    public void MonthlyHistory_RejectsOtherSymbolsSyntheticContractsAndBuckets(string workflowEntityId)
    {
        var result = IntrinsicTimeStrategyWorkflowQueryModel.MatchesHistoryEntity(
            workflowEntityId,
            "ES",
            [new DateOnly(2026, 9, 1)],
            TimeFrameType.Monthly);

        result.Should().BeFalse();
    }
}
