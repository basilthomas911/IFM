using TomasAI.IFM.Framework.MarketData.DataBento.Interop;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

internal static class LiveTestGate
{
    internal static bool IsEnabled() =>
        IsOne("IFM_RUN_DATABENTO_SMOKE_TESTS")
        || IsOne("IFM_RUN_DATABENTO_LIVE_TESTS");

    internal static bool IsOneHourTestEnabled() =>
        IsOne("IFM_RUN_DATABENTO_ONE_HOUR_TESTS");

    internal static TimeSpan GetSoakDuration()
    {
        const int defaultMinutes = 60;
        var value = Environment.GetEnvironmentVariable(
            "IFM_DATABENTO_SOAK_MINUTES");
        if (string.IsNullOrWhiteSpace(value))
        {
            return TimeSpan.FromMinutes(defaultMinutes);
        }
        if (!int.TryParse(value, out var minutes) || minutes <= 0)
        {
            throw new InvalidOperationException(
                "IFM_DATABENTO_SOAK_MINUTES must be a positive whole number.");
        }
        return TimeSpan.FromMinutes(minutes);
    }

    internal static MarketDataKinds GetSoakDataKinds() =>
        MarketDataKinds.Quote
        | MarketDataKinds.Trade
        | (IsOne("IFM_DATABENTO_INCLUDE_MBO")
            ? MarketDataKinds.MboOrderUpdate
            : MarketDataKinds.None);

    internal static void AssertCredential()
    {
        Assert.False(string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("DATABENTO_API_KEY")));
    }

    internal static DatabentoFeedOptions CreateOptions() =>
        DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");

    internal static DatabentoFeedOptions CreateLiveOptions() => CreateOptions() with
    {
        DataSource = FeedDataSourceMode.DatabentoLive,
        CpuAffinity = new FeedCpuAffinityOptions
        {
            Mode = CpuAffinityMode.Unpinned,
            RequirePerformanceCore = false
        },
        ThreadPriority = new FeedThreadPriorityOptions(),
        Memory = new FeedMemoryOptions { LockRingMemory = false },
        GarbageCollection = new FeedGcOptions { EnableSustainedLowLatency = false },
        Numa = new FeedNumaOptions { Mode = NumaLocalityMode.Disabled },
        CoreIsolation = new FeedCoreIsolationOptions
        {
            Mode = FeedCoreIsolationMode.PinnedOnly
        }
    };

    internal static ulong UtcNowNanoseconds() => checked(
        (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000UL);

    internal static Task DrainUntilCompletedAsync(
        ISynchronousBatchReader<MarketDataBatch64> reader) => Task.Run(() =>
    {
        while (true)
        {
            try
            {
                using var batch = reader.Read(Timeout.InfiniteTimeSpan);
            }
            catch (EndOfStreamException)
            {
                return;
            }
        }
    });

    private static bool IsOne(string name) => string.Equals(
        Environment.GetEnvironmentVariable(name),
        "1",
        StringComparison.Ordinal);
}
