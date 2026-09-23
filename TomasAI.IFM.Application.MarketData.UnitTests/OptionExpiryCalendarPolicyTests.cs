using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class OptionExpiryCalendarPolicyTests
{
    [Fact]
    public void Es_roots_cover_daily_weekly_end_of_month_and_quarterly_families()
    {
        var roots = OptionExpiryCalendarPolicy.GetRoots("es");
        Assert.Equal(7, roots.Count);
        Assert.Contains(roots, item => item.Family == "Daily-Monday" && item.Roots.Contains("E1A"));
        Assert.Contains(roots, item => item.Family == "Weekly-Friday" && item.Roots.Contains("EW4"));
        Assert.Contains(roots, item => item.Family == "End-Of-Month" && item.Roots.SequenceEqual(["EW"]));
        Assert.Contains(roots, item => item.Family == "Quarterly-Serial" && item.Roots.SequenceEqual(["ES"]));
    }

    [Fact]
    public void Coverage_uses_the_maturity_after_the_on_the_run_contract()
    {
        var valueDate = new DateOnly(2026, 9, 22);
        var contracts = new[]
        {
            Future("ES20261218", new DateOnly(2026, 12, 18), true),
            Future("ES20270319", new DateOnly(2027, 3, 19), false),
            Future("ES20270618", new DateOnly(2027, 6, 18), false)
        };
        Assert.Equal(new DateOnly(2027, 3, 19),
            OptionExpiryCalendarPolicy.CalculateCoverageThrough(valueDate, contracts));
    }

    [Fact]
    public void Coverage_falls_back_to_following_maturity_when_six_month_contract_is_not_loaded()
    {
        var valueDate = new DateOnly(2026, 9, 22);
        var contracts = new[]
        {
            Future("ES20261218", new DateOnly(2026, 12, 18), true),
            Future("ES20270319", new DateOnly(2027, 3, 19), false)
        };
        Assert.Equal(new DateOnly(2027, 3, 19),
            OptionExpiryCalendarPolicy.CalculateCoverageThrough(valueDate, contracts));
    }

    static FuturesContractV3ReadModel Future(string id, DateOnly maturity, bool onTheRun) =>
        new(id, id, "ES", id, "FUT", "USD", "XCME", "50", maturity, onTheRun, true);
}
