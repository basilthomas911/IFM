using System;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests;

public sealed class ApplicationStorageConnectionsTests
{
    [Fact]
    public void All_domain_contexts_share_one_postgres_identity_and_raw_ticks_have_the_only_scylla_destination()
    {
        const string postgres = "Host=localhost;Port=5432;Database=isolated-layout-test;Username=layout-test";
        const string raw = "Contact Points=localhost;Port=9042;Default Keyspace=trade_tick_quote_test_db";
        var settings = ApplicationStorageConnections.Create(postgres, raw);
        string[] names = ["EventSourceActorDbConnection", "ConfigurationDbConnection", "MarketDataServiceDbConnection",
            "SystemAdminDbConnection", "SequenceIdDbConnection", "PortfolioDbConnection", "MarketDataDbConnection",
            "ReferenceDbConnection", "SecuritiesDbConnection", "TradeDbConnection",
            "TradePlanDbConnection", "ScheduledTaskDbConnection"];
        foreach (var name in names)
        {
            Assert.Equal("System.Data.Postgres", settings[name]!.ProviderName);
            Assert.Equal(PostgresDatabaseIdentity.Parse(postgres), PostgresDatabaseIdentity.Parse(settings[name]!.ConnectionString));
        }
        Assert.Equal("System.Data.ScyllaDb", settings["TradeTickQuoteDbConnection"]!.ProviderName);
        Assert.Equal(raw, settings["TradeTickQuoteDbConnection"]!.ConnectionString);
    }

    [Fact]
    public void Missing_raw_destination_is_rejected_instead_of_falling_back_to_market_data()
        => Assert.Throws<ArgumentException>(() => ApplicationStorageConnections.Create(
            "Host=localhost;Database=isolated-layout-test;Username=layout-test", ""));
}
