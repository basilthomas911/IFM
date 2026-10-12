using NSubstitute;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.EventLog;
using TomasAI.IFM.Shared.EventModelActor.Contracts;

namespace TomasAI.IFM.Domain.MarketData.UnitTests;

/// <summary>Supplies only the transaction boundary for handler tests; native import transaction behavior is covered by isolated PostgreSQL integration tests.</summary>
internal static class PostgresImportBoundary
{
    internal static IPostgresEventTransaction Configure(IDbContextFactory factory, IEventActorContext context)
    {
        const string connection = "Host=localhost;Database=ifm_import_unit_test;Username=unit_test";
        factory.MarketDataDb.ProviderName.Returns("System.Data.Postgres");
        factory.MarketDataDb.ConnectionString.Returns(connection);
        factory.ActorEventSourceDb.ProviderName.Returns("System.Data.Postgres");
        factory.ActorEventSourceDb.ConnectionString.Returns(connection);
        var transaction = Substitute.For<IPostgresEventTransaction>();
        transaction.ExecuteAsync(Arg.Any<Func<EnlistedEventTransaction, CancellationToken, Task<bool>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var container = Substitute.For<IContainerInstance>();
        container.Resolve<IPostgresEventTransaction>().Returns(transaction);
        context.Container.Returns(container);
        return transaction;
    }
}
