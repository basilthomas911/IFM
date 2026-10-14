using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;

using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

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
        foreach (var type in new[] { typeof(LedgerPostingCompletedEvent),typeof(LedgerPostingBatchCompletedEvent),
            typeof(CapacityReservationCompletedEvent),typeof(CapacityConsumptionCompletedEvent),typeof(CapacityLifecycleCompletedEvent),typeof(LedgerConfigurationCompletedEvent),typeof(EmulatorOrderSubmittedEvent),
            typeof(TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioOrderCompositionCompletedEvent),
            typeof(TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioCloseOrderCompositionCompletedEvent) })
            await db.ScalarAsync(EventSourceDbSql.InsertEventNameId, [type.Name, type.AssemblyQualifiedName!], token).ConfigureAwait(false);
        return true;
    }, cancellationToken);

    /// <summary>Checks financial schema version and decodes at most one complete row from each financial table without changing state.</summary>
    public Task<int> ValidateAsync(CancellationToken cancellationToken = default)
        => transactions.ExecuteAsync(async (db, token) =>
        {
            var version = await db.ScalarAsync(PortfolioDbSql.Financial.PortfolioFinancialSchema.Select01, [], token)
                .ConfigureAwait(false);
            if (version is not int value || value != 2)
                throw new InvalidOperationException("Unsupported restored Portfolio financial schema version; expected version 2.");
            foreach (var table in HealthTables)
                _ = await db.QueryAsync("SELECT * FROM " + table + " LIMIT 1;", [],
                    static reader => reader.GetValues(new object[reader.FieldCount]), token)
                    .ConfigureAwait(false);
            return HealthTables.Length;
        }, cancellationToken);

    // Names come only from the checked-in DDL; no external identifier is interpolated.
    static readonly string[] HealthTables = System.Text.RegularExpressions.Regex.Matches(
        Ddl, @"CREATE TABLE IF NOT EXISTS (?<table>portfolio_financial\.[a-z0-9_]+)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1))
        .Select(static match => match.Groups["table"].Value).Distinct(StringComparer.Ordinal).ToArray();

    public const string Ddl = PortfolioSchemaSql.Financial;
}
