using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Application.Storage.PortfolioFinancial;

/// <summary>Owns the canonical Portfolio financial schema in the event-store database.</summary>
/// <param name="transactions">The transactional event-store boundary.</param>
public sealed class PortfolioFinancialSchema(IPostgresEventTransaction transactions)
{
    /// <summary>Creates or upgrades the canonical Portfolio financial schema.</summary>
    /// <param name="cancellationToken">A token that cancels schema initialization.</param>
    /// <returns>A task that completes after schema and event identities are ready.</returns>
    public Task InitializeAsync(CancellationToken cancellationToken = default) => transactions.ExecuteAsync(async (db, token) =>
    {
        await db.ExecuteAsync(Ddl, [], token).ConfigureAwait(false);
        var version = await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioFinancialSchema.Select01, [], token).ConfigureAwait(false);
        if (version is not int value || value != 2) throw new InvalidOperationException("Unsupported Portfolio financial schema version; expected version 2.");
        foreach(var type in new[] { typeof(LedgerPostingCompletedEvent),typeof(LedgerPostingBatchCompletedEvent),
            typeof(CapacityReservationCompletedEvent),typeof(CapacityConsumptionCompletedEvent),typeof(CapacityLifecycleCompletedEvent),typeof(LedgerConfigurationCompletedEvent),typeof(EmulatorOrderSubmittedEvent),
            typeof(TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioOrderCompositionCompletedEvent),
            typeof(TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioCloseOrderCompositionCompletedEvent) })
            await db.ScalarAsync(EventSourceDbSql.InsertEventNameId,[type.Name,type.AssemblyQualifiedName!],token).ConfigureAwait(false);
        return true;
    }, cancellationToken);

    public const string Ddl = PortfolioDbSql.Financial.PortfolioFinancialSchema.Create01;
}
