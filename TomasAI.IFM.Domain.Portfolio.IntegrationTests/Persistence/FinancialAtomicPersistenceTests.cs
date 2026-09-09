using FluentAssertions;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Shared.Exceptions;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Persistence;

/// <summary>Exercises actual PostgreSQL business/event enlistment, not a transaction mock.</summary>
[Trait("Category", "PortfolioFinancial")]
[Trait("Gate", "PF-FIN-02")]
[Collection("PortfolioFinancialDatabase")]
public sealed class FinancialAtomicPersistenceTests(PortfolioEventStoreFixture fixture) : IClassFixture<PortfolioEventStoreFixture>
{
    static PostgresEventTransaction Transactions() => new(new DbConnectionSettings().Add(
        EventSourceActorDbContext.EventSourceActorDbConnection,
        "Host=localhost;Port=5432;Database=event-source-test-db", "System.Data.Postgres"));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Business_and_existing_event_append_commit_or_rollback_together(int failAt)
    {
        var transactions = Transactions();
        await new PortfolioFinancialSchema(transactions).InitializeAsync();
        var id = Random.Shared.Next(100000, int.MaxValue); var source = Guid.NewGuid().ToString("N");
        var stream = $"FinancialAtomicTest.{source}"; var commandId = Guid.NewGuid();
        var domainEvent = new PortfolioCreated(Guid.NewGuid(), commandId, 1, DateTime.UtcNow, "integration", new());
        async Task Commit() => await transactions.ExecuteAsync(async (db, token) =>
        {
            await InsertBook(db, id, source, token);
            if (failAt == 1) throw new InvalidOperationException("Before event append");
            await db.AppendAsync(stream, commandId, domainEvent, 0, token);
            if (failAt == 2) throw new InvalidOperationException("After event append before commit");
            return true;
        });
        if (failAt == 0) await Commit();
        else await FluentActions.Awaiting(Commit).Should().ThrowAsync<InvalidOperationException>();
        var found = await Transactions().ExecuteAsync(async (db, token) => (
            Books: (long)(await db.ScalarAsync("SELECT count(*) FROM portfolio_financial.ledger_book WHERE book_id=$1;", [id], token))!,
            Events: (long)(await db.ScalarAsync("SELECT count(*) FROM event_log WHERE commandid=$1;", [commandId], token))!));
        found.Books.Should().Be(failAt == 0 ? 1 : 0);
        found.Events.Should().Be(found.Books);
        if (failAt == 0)
        {
            var restored = await fixture.EventSourceDb.GetEventLogByEventIdAsync(domainEvent.EventId);
            restored.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task Independent_connections_racing_expected_version_cannot_commit_two_business_changes()
    {
        await new PortfolioFinancialSchema(Transactions()).InitializeAsync();
        var source = Guid.NewGuid().ToString("N"); var stream = $"FinancialAtomicRace.{source}";
        async Task<bool> Attempt()
        {
            var commandId = Guid.NewGuid();
            try
            {
                return await Transactions().ExecuteAsync(async (db, token) =>
                {
                    await InsertBook(db, Random.Shared.Next(100000, int.MaxValue), source, token);
                    await db.AppendAsync(stream, commandId,
                        new PortfolioCreated(Guid.NewGuid(), commandId, 1, DateTime.UtcNow, "integration", new()), 0, token);
                    return true;
                });
            }
            catch (ConcurrencyException) { return false; }
        }
        var results = await Task.WhenAll(Attempt(), Attempt());
        results.Count(x => x).Should().Be(1);
        var books = await Transactions().ExecuteAsync((db, token) => db.ScalarAsync(
            "SELECT count(*) FROM portfolio_financial.ledger_book WHERE execution_account_ref=$1;", [source], token));
        books.Should().Be(1L);
    }

    [Fact]
    public async Task Additive_schema_reapply_preserves_existing_financial_rows()
    {
        var transactions = Transactions(); var schema = new PortfolioFinancialSchema(transactions);
        await schema.InitializeAsync();
        var id = Random.Shared.Next(100000, int.MaxValue);
        await transactions.ExecuteAsync(async (db, token) => { await InsertBook(db, id, Guid.NewGuid().ToString("N"), token); return true; });
        await schema.InitializeAsync();
        (await transactions.ExecuteAsync((db, token) => db.ScalarAsync(
            "SELECT count(*) FROM portfolio_financial.ledger_book WHERE book_id=$1;", [id], token))).Should().Be(1L);
    }

    static Task<int> InsertBook(EnlistedEventTransaction db, int id, string source, CancellationToken token) => db.ExecuteAsync(
        "INSERT INTO portfolio_financial.ledger_book(book_id,accounting_entity_id,portfolio_id,base_currency,execution_account_ref,environment,version,status) VALUES($1,$2,$1,'USD',$3,'Integration',1,'Draft');",
        [id, Guid.NewGuid(), source], token);
}
