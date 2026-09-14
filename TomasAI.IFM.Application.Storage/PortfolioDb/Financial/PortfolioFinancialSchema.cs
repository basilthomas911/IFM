using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Additive financial schema in the event-store database; initialization never activates a Fund or imports capital.</summary>
public sealed class PortfolioFinancialSchema(IPostgresEventTransaction transactions)
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) => transactions.ExecuteAsync(async (db, token) =>
    {
        await db.ExecuteAsync(Ddl, [], token).ConfigureAwait(false);
        var version = await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioFinancialSchema.Select01, [], token).ConfigureAwait(false);
        if (version is not int value || value != 1) throw new InvalidOperationException("Unsupported Portfolio financial schema version.");
        foreach(var type in new[] { typeof(LedgerPostingCompletedEvent),typeof(LedgerPostingBatchCompletedEvent),
            typeof(CapacityReservationCompletedEvent),typeof(CapacityConsumptionCompletedEvent),typeof(CapacityLifecycleCompletedEvent),typeof(LedgerConfigurationCompletedEvent),typeof(EmulatorOrderSubmittedEvent),
            typeof(TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioOrderCompositionCompletedEvent),
            typeof(TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioCloseOrderCompositionCompletedEvent) })
            await db.ScalarAsync(EventSourceDbSql.InsertEventNameId,[type.Name,type.AssemblyQualifiedName!],token).ConfigureAwait(false);
        return true;
    }, cancellationToken);

    public const string Ddl = PortfolioDbSql.Financial.PortfolioFinancialSchema.Create01;
}
