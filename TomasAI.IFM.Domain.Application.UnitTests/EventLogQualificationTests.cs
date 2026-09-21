using Microsoft.Extensions.Configuration;
using TomasAI.IFM.Application.Api.Server;

namespace TomasAI.IFM.Domain.Application.UnitTests;

public sealed class EventLogQualificationTests
{
    static EventLogQualification Profile() => new("0123456789ab", "Test", Path.GetTempPath());

    [Theory]
    [InlineData("Production", "0123456789ab")]
    [InlineData("Development", "0123456789ab")]
    [InlineData("Test", "../escape")]
    [InlineData("Test", "0123456789AB")]
    public void Rejects_non_test_or_invalid_run(string environment, string run) =>
        Assert.Throws<InvalidOperationException>(() => new EventLogQualification(run, environment, Path.GetTempPath()));

    [Fact]
    public void Generated_profile_is_valid_and_synthetic()
    {
        var profile = Profile();
        var config = new ConfigurationBuilder().AddInMemoryCollection(profile.Settings).Build();
        profile.Validate(config);
        Assert.Equal("Synthetic", config["AppSettings:Databento:DataSource"]);
        Assert.Equal("BinaryCopy", config["EventLogPersistence:WriteMode"]);
        Assert.Contains("synthetic", config.GetConnectionString("EventSourceActorDbConnection"));
    }

    [Theory]
    [InlineData("ConnectionStrings:EventSourceActorDbConnection", "Host=localhost;Port=5432;Database=event-source-dev-db")]
    [InlineData("ConnectionStrings:TradeDbConnection", "Contact Points=localhost;Port=9042;Default Keyspace=trade_test_db")]
    [InlineData("Nats:Consumer:Url", "nats://localhost:4222")]
    [InlineData("AppSettings:Databento:DataSource", "DatabentoLive")]
    [InlineData("AppSettings:RedisUri", "localhost:6379")]
    [InlineData("TradeBroker:Emulator:LedgerPath", "shared-ledger.json")]
    [InlineData("ConnectionStrings:Unexpected", "Host=localhost")]
    [InlineData("Kestrel:Endpoints:Other:Url", "http://0.0.0.0:80")]
    public void Rejects_shared_or_unreviewed_overrides(string key, string value)
    {
        var profile = Profile();
        var config = new ConfigurationBuilder().AddInMemoryCollection(profile.Settings)
            .AddInMemoryCollection(new Dictionary<string,string?> { [key] = value }).Build();
        Assert.Throws<InvalidOperationException>(() => profile.Validate(config));
    }
}
