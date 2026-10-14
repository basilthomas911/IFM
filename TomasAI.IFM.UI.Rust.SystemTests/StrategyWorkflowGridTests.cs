using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed class StrategyWorkflowGridTests
{
    [Theory]
    [InlineData("2026-07-13T18:24:36.123Z", TimeFrameType.Daily, "02:24:36.123 PM")]
    [InlineData("2026-01-13T18:24:36.123Z", TimeFrameType.Daily, "01:24:36.123 PM")]
    [InlineData("2026-07-13T18:24:36.123Z", TimeFrameType.Weekly, "13-Jul-2026 02:24:36.123 PM")]
    [InlineData("2026-01-13T18:24:36.123Z", TimeFrameType.Monthly, "13-Jan-2026 01:24:36.123 PM")]
    public void Workflow_timestamp_matches_WinForms_Eastern_time(string utc, TimeFrameType timeFrame, string expected)
        => Assert.Equal(expected, StrategyDetailsAdapter.FormatWorkflowTime(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind), timeFrame));
}
