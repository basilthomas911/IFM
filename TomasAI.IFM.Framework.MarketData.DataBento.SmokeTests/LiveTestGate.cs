namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

internal static class LiveTestGate
{
    internal static bool IsEnabled() =>
        IsOne("IFM_RUN_DATABENTO_SMOKE_TESTS")
        || IsOne("IFM_RUN_DATABENTO_LIVE_TESTS");

    internal static void AssertCredential()
    {
        Assert.False(string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("DATABENTO_API_KEY")));
    }

    internal static DatabentoFeedOptions CreateOptions() =>
        DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");

    internal static ulong UtcNowNanoseconds() => checked(
        (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000_000UL);

    private static bool IsOne(string name) => string.Equals(
        Environment.GetEnvironmentVariable(name),
        "1",
        StringComparison.Ordinal);
}
