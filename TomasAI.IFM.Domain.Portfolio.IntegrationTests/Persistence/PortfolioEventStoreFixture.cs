using Microsoft.Extensions.Logging;
using NSubstitute;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

public sealed class PortfolioEventStoreFixture
{
    public PortfolioEventStoreFixture()
    {
        var settings = new DbConnectionSettings().Add(
            EventSourceActorDbContext.EventSourceActorDbConnection,
            "Host=localhost;Port=5432;Database=event-source-test-db",
            "System.Data.Postgres");
        var logger = Substitute.For<ILogger<DbProvider>>();
        new EventSourceSchemaDb(settings, logger).CreateAllAsync().GetAwaiter().GetResult();

        var cache = Substitute.For<IRedisCache>();
        var values = new Dictionary<string, string>();
        cache.TryGet(Arg.Any<string>(), out Arg.Any<string>()).Returns(call =>
        {
            var found = values.TryGetValue(call.ArgAt<string>(0), out var value);
            call[1] = value!;
            return found;
        });
        cache.When(x => x.Set(Arg.Any<string>(), Arg.Any<string>()))
            .Do(call => values[call.ArgAt<string>(0)] = call.ArgAt<string>(1));
        var blackboard = new BlackboardService(cache, new SystemTextJsonSerializer());
        var repositories = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
        EventSourceDb = new EventSourceActorDbContext(settings, factory, blackboard, logger);
        repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), EventSourceDb);
    }

    public EventSourceActorDbContext EventSourceDb { get; }
}
