using System.Collections.Concurrent;
using TomasAI.IFM.Application.Api.Server.Core.Startup.Application;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using Xunit;

namespace TomasAI.IFM.LivePipeline.IntegrationTests;

public sealed class IndicatorStartupRetentionTests
{
    [Fact]
    public void Restart_hints_expire_old_dates_and_remain_bounded_across_many_contracts()
    {
        var date = new DateOnly(2026, 11, 7);
        var entries = new ConcurrentDictionary<(DateOnly Date, string ContractId, TimeFrameType Frame), byte>();
        for (var index = 0; index < 1024; index++) entries[(date, "ES-" + index.ToString("D4"), TimeFrameType.OneMinute)] = 0;
        entries[(date.AddDays(-8), "expired", TimeFrameType.OneMinute)] = 0;
        StoredSessionIndicatorWarmup.TrimRetained(entries, date);
        Assert.Equal(512, entries.Count);
        Assert.DoesNotContain(entries.Keys, key => key.ContractId == "expired");
        Assert.Contains(entries.Keys, key => key.ContractId == "ES-1023");
        StoredSessionIndicatorWarmup.TrimRetained(entries, date.AddDays(8));
        Assert.Empty(entries);
    }
}
