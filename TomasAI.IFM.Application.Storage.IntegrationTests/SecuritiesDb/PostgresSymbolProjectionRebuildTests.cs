using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SecuritiesDb;

/// <summary>Qualifies the native transaction guarantees that replace CQL projection journals.</summary>
public sealed class PostgresSymbolProjectionRebuildTests(SecuritiesDatabaseFixture fixture) : IClassFixture<SecuritiesDatabaseFixture>
{
    [Fact]
    public async Task Rebuild_removes_stale_rows_and_repairs_missing_rows_without_changing_canonical_payloads()
    {
        var row = Future();
        await fixture.Db.InsertFuturesContractAsync(row);
        await Sql("DELETE FROM securities.futures_contract_by_symbol WHERE contractId=$1", row.ContractId);
        Assert.Empty(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol));
        await fixture.Db.BackfillSymbolProjectionsAsync();
        Assert.Equal(row, Assert.Single(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol)));
        await Sql("DELETE FROM securities.futures_contract WHERE contractId=$1", row.ContractId);
        Assert.Single(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol));
        await fixture.Db.BackfillSymbolProjectionsAsync();
        Assert.Empty(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol));
        Assert.True((await fixture.Db.ReconcileSymbolProjectionsAsync()).IsConsistent);
        Assert.Null(await fixture.Db.GetFuturesContractAsync(row.ContractId));
    }

    [Fact]
    public async Task Ambiguous_canonical_identity_rejects_rebuild_before_either_projection_changes()
    {
        var row = Future();
        await fixture.Db.InsertFuturesContractAsync(row);
        var otherSymbol = "AMBIGUOUS-" + Guid.NewGuid().ToString("N");
        await Sql("""
            INSERT INTO securities.futures_contract
            (contractId,description,symbol,localSymbol,securityType,currency,exchange,multiplier,lastTradeDate,onTheRun,rollover,referencePayload)
            SELECT contractId,description,$2,localSymbol,securityType,currency,exchange,multiplier,lastTradeDate,onTheRun,rollover,referencePayload
            FROM securities.futures_contract WHERE contractId=$1;
            """, row.ContractId, otherSymbol);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Db.BackfillSymbolProjectionsAsync());
            Assert.Equal(row, Assert.Single(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol)));
            Assert.Empty(await fixture.Db.GetFuturesContractsBySymbolAsync(otherSymbol));
        }
        finally { await Sql("DELETE FROM securities.futures_contract WHERE contractId=$1 AND symbol=$2", row.ContractId, otherSymbol); }
        await fixture.Db.BackfillSymbolProjectionsAsync();
        Assert.Equal(row, Assert.Single(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol)));
    }

    [Fact]
    public async Task Failure_after_clearing_both_projections_rolls_back_the_entire_rebuild()
    {
        var row = Future();
        var option = SampleData.FuturesOptionContract with { ContractId = "OPTION" + Guid.NewGuid().ToString("N"), Symbol = row.Symbol };
        await fixture.Db.InsertFuturesContractAsync(row);
        await fixture.Db.InsertFuturesOptionContractAsync(option);
        await Sql("""
            CREATE FUNCTION securities.reject_rebuild_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'injected symbol rebuild failure'; END $$;
            CREATE TRIGGER reject_rebuild_fixture BEFORE INSERT ON securities.futures_option_contract_by_symbol
                FOR EACH ROW EXECUTE FUNCTION securities.reject_rebuild_fixture();
            """);
        try
        {
            var error = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Db.BackfillSymbolProjectionsAsync());
            Assert.Contains("injected symbol rebuild failure", error.ToString(), StringComparison.Ordinal);
            Assert.Equal(row, Assert.Single(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol)));
            Assert.Equal(option, Assert.Single(await fixture.Db.GetFuturesOptionContractsAsync(row.Symbol)));
            Assert.True((await fixture.Db.ReconcileSymbolProjectionsAsync()).IsConsistent);
        }
        finally
        {
            await Sql("DROP TRIGGER reject_rebuild_fixture ON securities.futures_option_contract_by_symbol; DROP FUNCTION securities.reject_rebuild_fixture();");
        }
        await fixture.Db.BackfillSymbolProjectionsAsync();
        Assert.True((await fixture.Db.ReconcileSymbolProjectionsAsync()).IsConsistent);
    }

    [Fact]
    public async Task Independent_committed_writes_survive_concurrent_rebuilds()
    {
        var rows = Enumerable.Range(0, 8).Select(_ => Future()).ToArray();
        await Task.WhenAll(rows.Select(row => fixture.Db.InsertFuturesContractAsync(row))
            .Append(fixture.Db.BackfillSymbolProjectionsAsync()));
        foreach (var row in rows)
            Assert.Equal(row, Assert.Single(await fixture.Db.GetFuturesContractsBySymbolAsync(row.Symbol)));
    }

    Task Sql(string sql, params object?[] values) => fixture.Db.Database.Use("NativeRebuildFixture", sql)
        .SetParameters((object)values).ExecuteCommandAsync();

    static FuturesContractV3ReadModel Future() => new("TEST" + Guid.NewGuid().ToString("N"), "isolated rebuild",
        "SYM" + Guid.NewGuid().ToString("N"), "local", "FUT", "USD", "CME", "50", new(2026, 12, 18), true);
}
