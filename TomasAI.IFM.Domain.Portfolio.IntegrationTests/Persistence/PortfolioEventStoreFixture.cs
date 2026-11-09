using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

public sealed class PortfolioEventStoreFixture
{
    public PortfolioEventStoreFixture() : this(true) { }

    internal PortfolioEventStoreFixture(bool initializeSchema)
    {
        var settings = new DbConnectionSettings().Add(
            EventSourceActorDbContext.EventSourceActorDbConnection,
            Environment.GetEnvironmentVariable("IFM_POSTGRES_EVENTSOURCE_TEST_CONNECTION")
                ?? "Host=localhost;Port=5432;Database=event-source-test-db",
            "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        if (initializeSchema) new EventSourceSchemaDb(settings, logger).CreateAllAsync().GetAwaiter().GetResult();


        var values = new Dictionary<string, string>();


        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        EventSourceDb = new EventSourceActorDbContext(settings, factory, logger);
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), EventSourceDb);
    }

    public EventSourceActorDbContext EventSourceDb { get; }
}
