namespace TomasAI.IFM.Domain.Trade.IntegratedTests;

public sealed class EventLogEngineQualificationTests
{
    private static Dictionary<string, string?> Settings() => new()
    {
        ["IFM_ENGINE_QUALIFICATION_RUN"] = "092020260032",
        ["DOTNET_ENVIRONMENT"] = "Test",
        ["IFM_TEST_POSTGRES_CONNECTION"] = "Host=127.0.0.1;Port=25432;Database=ifm_eventlog_bench_092020260032_synthetic_host",
        ["IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION"] = "Host=127.0.0.1;Port=25432;Database=ifm_eventlog_bench_092020260032_synthetic_host",
        ["IFM_TEST_TRADE_CONNECTION"] = "Contact Points=127.0.0.1;Port=29042;Default Keyspace=ifm_synthetic_092020260032_trade",
        ["IFM_TEST_REDIS_URL"] = "127.0.0.1:26379",
        ["IFM_FINANCIAL_TEST_NATS_URL"] = "nats://127.0.0.1:24223"
    };

    [Fact]
    public void Exact_owned_endpoints_are_accepted()
    {
        var settings = Settings();
        Assert.Equal("092020260032", EventLogEngineQualification.Validate(key => settings.GetValueOrDefault(key)));
    }

    [Fact]
    public void Existing_tests_are_unchanged_without_opt_in()
        => Assert.Null(EventLogEngineQualification.Validate(_ => null));

    [Theory]
    [InlineData("IFM_ENGINE_QUALIFICATION_RUN")]
    [InlineData("DOTNET_ENVIRONMENT")]
    [InlineData("IFM_TEST_POSTGRES_CONNECTION")]
    [InlineData("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")]
    [InlineData("IFM_TEST_TRADE_CONNECTION")]
    [InlineData("IFM_TEST_REDIS_URL")]
    [InlineData("IFM_FINANCIAL_TEST_NATS_URL")]
    public void Invalid_setting_fails_before_fixture_connections(string key)
    {
        var settings = Settings();
        settings[key] = "not-the-owned-fixture";
        Assert.Throws<InvalidOperationException>(() =>
            EventLogEngineQualification.Validate(name => settings.GetValueOrDefault(name)));
    }
}
