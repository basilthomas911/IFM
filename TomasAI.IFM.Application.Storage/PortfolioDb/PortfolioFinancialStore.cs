using Newtonsoft.Json;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Compatibility facade while financial callers move to the unified PortfolioDbContext.</summary>
public sealed class PortfolioFinancialStore(IPostgresEventTransaction transactions)
{
    public Task<T?> ReadOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash = null, CancellationToken token = default)
        where T : class, IFinancialCompletedEvent => transactions.ExecuteAsync(
            (db, cancellation) => PortfolioDbFinancialSupport.ReadOperationAsync<T>(db, portfolioId, operationId, inputHash, cancellation), token);

    public Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId, CancellationToken token = default) => transactions.ExecuteAsync(async (db, cancellation) =>
    {
        var value = await db.ScalarAsync(PortfolioDbSql.Financial.ReadBook, [portfolioId], cancellation).ConfigureAwait(false);
        return value is string json ? PortfolioDbFinancialSupport.Decode<FinancialBookConfiguration>(json) : null;
    }, token);

    internal Task CreateBookAsync(FinancialBookConfiguration book, IReadOnlyList<LedgerAccountDefinition> accounts,
        IReadOnlyList<LedgerPostingRule> rules, DateOnly periodStart, DateOnly periodEnd, CancellationToken token = default) =>
        transactions.ExecuteAsync(async (db, cancellation) =>
        {
            await PortfolioDbFinancialSupport.CreateBookAsync(db, book, accounts, rules, periodStart, periodEnd, Guid.NewGuid(), cancellation).ConfigureAwait(false);
            return true;
        }, token);

}
