using TomasAI.IFM.UI.Rust.Bridge.Net;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
public sealed class TradePlanDisplayTests
{
    [Theory]
    [InlineData("2026-01-15T12:34:56.123Z", "2026-01-15 07:34:56.123")]
    [InlineData("2026-07-15T12:34:56.123Z", "2026-07-15 08:34:56.123")]
    [InlineData("2026-07-15T02:00:00Z", "2026-07-14 22:00:00.000")]
    public void Action_time_uses_Eastern_time_with_exact_milliseconds(string timestamp,string expected)
    {
        var utc=DateTime.Parse(timestamp,null,System.Globalization.DateTimeStyles.RoundtripKind);
        var plan=new StrategyTradePlanSnapshot {CalculatedAtUtc=utc.AddDays(1),IronCondorTradePlanSnapshot=new(){ActionDateTime=utc}};
        Assert.Equal(expected,TradePlanDisplay.ActionTime(plan));
        Assert.Equal(utc,plan.IronCondorTradePlanSnapshot.ActionDateTime);
    }
    [Fact]
    public void Generic_plan_uses_calculation_time_and_missing_time_remains_unavailable()
    {
        Assert.Equal("2026-01-15 07:34:56.000",TradePlanDisplay.ActionTime(new(){CalculatedAtUtc=new DateTime(2026,1,15,12,34,56,DateTimeKind.Utc)}));
        Assert.Null(TradePlanDisplay.ActionTime(new()));
    }
}
