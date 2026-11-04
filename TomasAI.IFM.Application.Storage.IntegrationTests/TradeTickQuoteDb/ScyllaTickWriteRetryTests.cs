using Cassandra;
using TomasAI.IFM.Application.Storage.TradeTickQuoteDb;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeTickQuoteDb;

/// <summary>Injects the observed server timeout without changing live Scylla availability or writing test ticks to development.</summary>
public sealed class ScyllaTickWriteRetryTests
{
    [Fact]
    public async Task A_real_upsert_with_lost_acknowledgement_retains_one_identical_row()
    {
        var keyspace = "ifm_retry_" + Guid.NewGuid().ToString("N");
        using var cluster = Cluster.Builder().AddContactPoint("127.0.0.1").WithPort(9042).Build();
        using var admin = await cluster.ConnectAsync();
        await admin.ExecuteAsync(new SimpleStatement($"CREATE KEYSPACE {keyspace} WITH replication={{'class':'SimpleStrategy','replication_factor':1}}"));
        try
        {
            using var session = await cluster.ConnectAsync(keyspace);
            await session.ExecuteAsync(new SimpleStatement("CREATE TABLE tick_probe (id uuid PRIMARY KEY, payload blob)"));
            var insert = await session.PrepareAsync("INSERT INTO tick_probe(id,payload) VALUES (?,?)");
            var key = Guid.NewGuid();
            byte[] payload = [1, 2, 3, 4];
            var attempts = 0;
            await ScyllaTickWriteRetry.ExecuteAsync(async () =>
            {
                await session.ExecuteAsync(insert.Bind(key, payload));
                if (++attempts == 1) throw Timeout();
            }, _ => Task.CompletedTask);
            var rows = await session.ExecuteAsync(new SimpleStatement("SELECT payload FROM tick_probe WHERE id=?", key));
            Assert.Equal(payload, Assert.Single(rows).GetValue<byte[]>(0));
            Assert.Equal(2, attempts);
        }
        finally { await admin.ExecuteAsync(new SimpleStatement($"DROP KEYSPACE {keyspace}")); }
    }

    [Fact]
    public async Task Timeout_after_an_unknown_commit_retries_the_same_identity_without_duplicate_rows()
    {
        var key = Guid.NewGuid();
        var rows = new Dictionary<Guid, decimal>();
        var attempts = 0;
        var delays = new List<TimeSpan>();
        await ScyllaTickWriteRetry.ExecuteAsync(() =>
        {
            rows[key] = 123.45m;
            if (++attempts == 1) throw new InvalidOperationException("provider wrapper", Timeout());
            return Task.CompletedTask;
        }, delay => { delays.Add(delay); return Task.CompletedTask; });
        Assert.Equal(2, attempts);
        Assert.Equal(123.45m, Assert.Single(rows).Value);
        Assert.Equal(TimeSpan.FromMilliseconds(100), Assert.Single(delays));
    }

    [Fact]
    public async Task Persistent_timeout_stops_after_three_attempts_and_preserves_the_error()
    {
        var attempts = 0;
        var timeout = Timeout();
        var error = await Assert.ThrowsAsync<WriteTimeoutException>(() => ScyllaTickWriteRetry.ExecuteAsync(
            () => { attempts++; throw timeout; }, _ => Task.CompletedTask));
        Assert.Same(timeout, error);
        Assert.Equal(3, attempts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_and_nontransient_errors_are_never_retried(bool cancellation)
    {
        var attempts = 0;
        Exception expected = cancellation ? new OperationCanceledException() : new InvalidOperationException("invalid binding");
        var error = await Record.ExceptionAsync(() => ScyllaTickWriteRetry.ExecuteAsync(
            () => { attempts++; throw expected; }, _ => throw new InvalidOperationException("unexpected delay")));
        Assert.Same(expected, error);
        Assert.Equal(1, attempts);
    }

    static WriteTimeoutException Timeout() => new(ConsistencyLevel.LocalQuorum, 0, 1, "SIMPLE");
}
