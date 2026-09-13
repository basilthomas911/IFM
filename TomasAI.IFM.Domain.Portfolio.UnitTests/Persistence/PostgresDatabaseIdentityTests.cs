using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.UnitTests.Persistence;

public sealed class PostgresDatabaseIdentityTests
{
    [Fact]
    [Trait("Gate", "PPG-02")]
    public void Equivalent_Postgres_connections_are_accepted()
    {
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, "Database=ifm;Host=LOCALHOST;Port=5432;Username=ifm", "System.Data.Postgres")
            .Add(PortfolioDbContext.PortfolioDbConnection, "Host=localhost;Username=ifm;Database=IFM;Port=5432", "System.Data.Postgres");
        FluentActions.Invoking(() => PostgresEventTransaction.ValidatePortfolioDatabaseIdentity(settings)).Should().NotThrow();
    }

    [Theory]
    [InlineData("Host=localhost;Database=events;Username=ifm", "Host=localhost;Database=portfolio;Username=ifm")]
    [InlineData("Host=localhost;Database=ifm;Username=a", "Host=localhost;Database=ifm;Username=b")]
    public void Different_database_or_identity_is_rejected(string eventSource, string portfolio)
    {
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, eventSource, "System.Data.Postgres")
            .Add(PortfolioDbContext.PortfolioDbConnection, portfolio, "System.Data.Postgres");
        FluentActions.Invoking(() => PostgresEventTransaction.ValidatePortfolioDatabaseIdentity(settings))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Scylla_Portfolio_registration_is_rejected()
    {
        var settings = new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, "Host=localhost;Database=ifm", "System.Data.Postgres")
            .Add(PortfolioDbContext.PortfolioDbConnection, "Contact Points=localhost", "System.Data.ScyllaDb");
        FluentActions.Invoking(() => PostgresEventTransaction.ValidatePortfolioDatabaseIdentity(settings))
            .Should().Throw<InvalidOperationException>().WithMessage("*requires PostgreSQL*");
    }
}
