using FluentAssertions;
using Npgsql;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using static TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence.GeneralLedgerPostingIntegrationTests;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

[Collection("PortfolioFinancialDatabase"),Trait("Category","PortfolioFinancial"),Trait("Gate","PF-FIN-02")]
public sealed class FinancialRetryIntegrationTests
{
    [Theory]
    [InlineData("40001",3)]
    [InlineData("40P01",3)]
    [InlineData("55P03",1)]
    [InlineData("23505",1)]
    public async Task Provider_retries_only_confirmed_serialization_or_deadlock_rollbacks_with_a_fixed_bound(string state,int expectedAttempts)
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();
        var attempts=0;
        var operation=()=>Transactions().ExecuteAsync(async(db,ct)=>
        {
            attempts++;
            // A real PostgreSQL error aborts the transaction. This is not a mocked provider exception.
            await db.ExecuteAsync($"DO $$ BEGIN RAISE EXCEPTION 'integration rollback' USING ERRCODE='{state}'; END $$;",[],ct);
            return 1;
        });
        (await FluentActions.Awaiting(operation).Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(state);
        attempts.Should().Be(expectedAttempts);
    }

    [Fact]
    public async Task Confirmed_rollback_retry_uses_a_new_transaction_and_leaves_one_business_effect()
    {
        var book=await CreateBook(); var attempts=0;
        await Transactions().ExecuteAsync(async(db,ct)=>
        {
            attempts++;
            await db.ExecuteAsync("UPDATE portfolio_financial.financial_authority SET financial_revision=financial_revision+1 WHERE portfolio_id=$1;",[book.PortfolioId],ct);
            if(attempts==1) await db.ExecuteAsync("DO $$ BEGIN RAISE EXCEPTION 'integration rollback' USING ERRCODE='40001'; END $$;",[],ct);
            return true;
        });
        attempts.Should().Be(2);
        var revision=await Transactions().ExecuteAsync((db,ct)=>db.ScalarAsync("SELECT financial_revision FROM portfolio_financial.financial_authority WHERE portfolio_id=$1;",[book.PortfolioId],ct));
        revision.Should().Be(1L);
    }
}
