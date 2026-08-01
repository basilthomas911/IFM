namespace TomasAI.IFM.Framework.MarketData.DataBento.IntegrationTests;

internal static class LiveTestGate
{
    internal static bool IsEnabled() =>
        IsOne("IFM_RUN_DATABENTO_INTEGRATION_TESTS")
        || IsOne("IFM_RUN_DATABENTO_LIVE_TESTS");

    internal static void AssertCredential()
    {
        Assert.False(string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("DATABENTO_API_KEY")));
    }

    internal static IDatabentoMarketDataQueries CreateConnectedQueries()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");
        var queries = new DatabentoFeedFactory().CreateMarketDataQueries(options);
        Assert.NotEmpty(queries.GetContractDetails("ES", TimeSpan.FromSeconds(90)));
        return queries;
    }

    private static bool IsOne(string name) => string.Equals(
        Environment.GetEnvironmentVariable(name),
        "1",
        StringComparison.Ordinal);
}
