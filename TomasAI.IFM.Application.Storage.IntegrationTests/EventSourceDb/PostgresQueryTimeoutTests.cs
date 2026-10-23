using Npgsql;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresQueryTimeoutTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Default_allows_statements_longer_than_the_former_server_and_client_limits()
    {
        var result = await fixture.Transactions.ExecuteOnceAsync(async (tx, token) =>
        {
            Assert.Equal("2min", await tx.ScalarAsync("SELECT current_setting('statement_timeout');", [], token));
            Assert.Equal("2min", await tx.ScalarAsync("SELECT current_setting('lock_timeout');", [], token));
            return await tx.QueryAsync("SELECT 42 AS value FROM pg_sleep(3.2);", [], reader => reader.GetInt32(0), token);
        });
        Assert.Equal(42, Assert.Single(result));
    }

    [Fact]
    public async Task Query_override_applies_only_to_that_statement_and_the_next_query_uses_the_default()
    {
        await fixture.Transactions.ExecuteOnceAsync(async (tx, token) =>
        {
            var setting = await tx.ScalarAsync("SELECT current_setting('statement_timeout');", [], token,
                timeout: new(TimeSpan.FromMilliseconds(80)));
            Assert.Equal("80ms", setting);
            await tx.ExecuteAsync("SELECT pg_sleep(0.15);", [], token);
            Assert.Equal("2min", await tx.ScalarAsync("SELECT current_setting('statement_timeout');", [], token));
            return true;
        });
    }

    [Fact]
    public async Task Explicit_statement_timeout_rolls_back_without_replaying_the_operation()
    {
        var table = "timeout_" + Guid.NewGuid().ToString("N");
        await using var db = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection); await db.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE TABLE {table}(value int);", db)) await create.ExecuteNonQueryAsync();
        try
        {
            var attempts = 0;
            var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.Transactions.ExecuteAsync(async (tx, token) =>
            {
                attempts++;
                await tx.ExecuteAsync($"INSERT INTO {table} VALUES(1);", [], token);
                await tx.ExecuteAsync("SELECT pg_sleep(2);", [], token, timeout: new(TimeSpan.FromMilliseconds(80)));
                return true;
            }));
            Assert.Equal(PostgresErrorCodes.QueryCanceled, error.SqlState);
            Assert.Equal(1, attempts);
            await using var count = new NpgsqlCommand($"SELECT count(*) FROM {table};", db);
            Assert.Equal(0L, await count.ExecuteScalarAsync());
        }
        finally { await using var drop = new NpgsqlCommand($"DROP TABLE {table};", db); await drop.ExecuteNonQueryAsync(); }
    }

    [Fact]
    public async Task Caller_cancellation_can_interrupt_a_query_with_a_long_explicit_timeout()
    {
        using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Transactions.ExecuteOnceAsync(async (tx, token) =>
        {
            await tx.ScalarAsync("SELECT pg_sleep(10);", [], token, timeout: new(TimeSpan.FromMinutes(5)));
            return true;
        }, cancelled.Token));
        Assert.Equal(1, await fixture.Transactions.ExecuteOnceAsync((tx, token) => tx.ScalarAsync("SELECT 1;", [], token)));
    }

    [Fact]
    public async Task An_explicit_lock_timeout_is_reported_and_does_not_change_other_transactions()
    {
        await using var blocker = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(fixture.Connection); await blocker.OpenAsync();
        await using var hold = await blocker.BeginTransactionAsync();
        var key = Random.Shared.NextInt64(1, long.MaxValue);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_xact_lock($1);", blocker, hold))
        { acquire.Parameters.AddWithValue(key); await acquire.ExecuteNonQueryAsync(); }
        var error = await Assert.ThrowsAsync<PostgresException>(() => fixture.Transactions.ExecuteOnceAsync(
            (tx, token) => tx.ScalarAsync("SELECT pg_advisory_xact_lock($1);", [key], token,
                timeout: new(TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(80)))));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
        Assert.Equal("2min", await fixture.Transactions.ExecuteOnceAsync((tx, token) => tx.ScalarAsync("SELECT current_setting('lock_timeout');", [], token)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Invalid_query_limits_are_rejected_before_executing_business_SQL(int milliseconds)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => fixture.Transactions.ExecuteOnceAsync(
            (tx, token) => tx.ScalarAsync("SELECT 1;", [], token, timeout: new(TimeSpan.FromMilliseconds(milliseconds)))));
    }
}
