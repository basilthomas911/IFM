using FluentAssertions;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Application.MarketData.UnitTests;

public sealed class SyntheticPersistenceIsolationGuardTests
{
    [Fact]
    public void LiveFeed_AllowsOrdinaryPersistenceTargets()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3") with
        {
            DataSource = FeedDataSourceMode.DatabentoLive
        };

        var action = () => SyntheticPersistenceIsolationGuard.Validate(
            options,
            "Host=localhost;Database=event-source-test-db",
            "Contact Points=localhost;Default Keyspace=trade_tick_quote_test_db");

        action.Should().NotThrow();
    }

    [Fact]
    public void SyntheticFeed_RejectsANonSyntheticDeploymentProfile()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.Development,
            "GLBX.MDP3");

        var action = () => SyntheticPersistenceIsolationGuard.Validate(
            options,
            "Host=localhost;Database=event-source-synthetic-db",
            "Contact Points=localhost;Default Keyspace=trade_tick_quote_synthetic_db");

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*SyntheticCi deployment profile*");
    }

    [Theory]
    [InlineData(
        "Host=localhost;Database=event-source-test-db",
        "Contact Points=localhost;Default Keyspace=trade_tick_quote_synthetic_db")]
    [InlineData(
        "Host=localhost;Database=event-source-synthetic-db",
        "Contact Points=localhost;Default Keyspace=trade_tick_quote_test_db")]
    public void SyntheticFeed_RejectsAnySharedPersistenceTarget(
        string eventSourceConnection,
        string rawTradeQuoteConnection)
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC");

        var action = () => SyntheticPersistenceIsolationGuard.Validate(
            options,
            eventSourceConnection,
            rawTradeQuoteConnection);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*isolated synthetic store*");
    }

    [Fact]
    public void SyntheticFeed_AllowsExplicitlyIsolatedPersistenceTargets()
    {
        var options = DatabentoFeedOptions.ForProfile(
            FeedDeploymentProfile.SyntheticCi,
            "SYNTHETIC");

        var action = () => SyntheticPersistenceIsolationGuard.Validate(
            options,
            "Host=localhost;Database=event-source-synthetic-db",
            "Contact Points=localhost;Default Keyspace=trade_tick_quote_synthetic_db");

        action.Should().NotThrow();
    }
}
