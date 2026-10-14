using System.Threading.Tasks;
using Npgsql;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.PortfolioDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresFinancialRecoveryValidationTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Recovery_validation_is_read_only_and_rejects_a_missing_financial_table()
    {
        var schema = new PortfolioFinancialSchema(fixture.Transactions);
        await schema.InitializeAsync();
        var before = await fixture.Transactions.ExecuteOnceAsync((db, token) =>
            db.ScalarAsync("SELECT count(*) FROM public.event_log;", [], token));
        Assert.True(await schema.ValidateAsync() >= 29);
        var after = await fixture.Transactions.ExecuteOnceAsync((db, token) =>
            db.ScalarAsync("SELECT count(*) FROM public.event_log;", [], token));
        Assert.Equal(before, after);
        await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(
            "ALTER TABLE portfolio_financial.ledger_book RENAME TO recovery_fixture_ledger_book;", [], token));
        try
        {
            var failure = await Assert.ThrowsAsync<PostgresException>(() => schema.ValidateAsync());
            Assert.Equal(PostgresErrorCodes.UndefinedTable, failure.SqlState);
        }
        finally
        {
            await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ExecuteAsync(
                "ALTER TABLE portfolio_financial.recovery_fixture_ledger_book RENAME TO ledger_book;", [], token));
        }
        Assert.True(await schema.ValidateAsync() >= 29);
    }
}
