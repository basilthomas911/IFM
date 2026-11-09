using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Shared.Storage;
using Xunit;
using Xunit.Abstractions;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.MarketDataDb;

public sealed class PostgresOptionEvidenceBatchTests(EventSourceActorSnapshotRangeFixture fixture, ITestOutputHelper output)
    : IClassFixture<EventSourceActorSnapshotRangeFixture>
{
    [Fact]
    public async Task First_attempt_and_exact_retry_are_classified_for_each_record_after_restart()
    {
        var settings = await CreateSchemaAsync();
        var db = Store(settings);
        var source = Evidence(1);
        var previous = Evidence(2);
        await db.WriteFirstAsync(previous, CancellationToken.None);
        var differentPrice = source with { Source = source.Source with { Price = 14m } };
        var differentAttempt = previous with { Source = previous.Source with { ReceiveNanoseconds = previous.Source.ReceiveNanoseconds + 10 } };
        var group = new[] { source, differentPrice, previous, differentAttempt };
        Assert.Equal(new[] { true, false, true, false }, await db.WriteFirstBatchAsync(group, CancellationToken.None));
        var restarted = Store(settings);
        Assert.Equal(new[] { true, false, true, false }, await restarted.WriteFirstBatchAsync(group, CancellationToken.None));
        Assert.Equal(source, await restarted.ReadAsync(source.Source.ContractId, source.Source.ValueDate, source.Source.Identity));

        var shared = Evidence(3);
        var changed = shared with { Source = shared.Source with { Price = 17m } };
        var result = await Task.WhenAll(
            db.WriteFirstBatchAsync([shared, Evidence(4)], CancellationToken.None).AsTask(),
            restarted.WriteFirstBatchAsync([changed, Evidence(5)], CancellationToken.None).AsTask());
        Assert.NotEqual(result[0][0], result[1][0]);
        Assert.True(result[0][1]);
        Assert.True(result[1][1]);
    }

    [Fact]
    public async Task Rejected_row_rolls_back_the_entire_statement_and_allows_retry()
    {
        var db = Store(await CreateSchemaAsync());
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var constraint = new NpgsqlCommand("""
            ALTER TABLE market_data.option_trade_evidence
            ADD CONSTRAINT evidence_batch_test_reject CHECK (contract_id <> 'ZZZ-REJECTED-BATCH');
            """, connection);
        await constraint.ExecuteNonQueryAsync();
        var valid = Evidence(91);
        var rejected = Evidence(92);
        rejected = rejected with
        {
            Source = rejected.Source with { ContractId = "ZZZ-REJECTED-BATCH" },
            Failure = rejected.Failure! with { ContractId = "ZZZ-REJECTED-BATCH" }
        };
        var error = await Assert.ThrowsAnyAsync<Exception>(() => db.WriteFirstBatchAsync([valid, rejected], CancellationToken.None).AsTask());
        Assert.Equal(PostgresErrorCodes.CheckViolation, ((error as PostgresException) ?? (error.InnerException as PostgresException))?.SqlState);
        Assert.Null(await db.ReadAsync(valid.Source.ContractId, valid.Source.ValueDate, valid.Source.Identity));
        Assert.True(await db.WriteFirstAsync(valid, CancellationToken.None));
    }

    [Fact]
    public async Task Compare_serial_and_batched_durable_writes()
    {
        if (Environment.GetEnvironmentVariable("IFM_OPTION_EVIDENCE_TIMINGS") != "1") return;
        var db = Store(await CreateSchemaAsync());
        for (var index = 0; index < 8; index++)
            await WriteFourAsync(db, 100 + 4 * index, index % 2 == 0);
        for (var pair = 0; pair < 5; pair++)
        {
            foreach (var batched in pair % 2 == 0 ? new[] { false, true } : [true, false])
            {
                for (var index = 0; index < 5; index++)
                {
                    var first = 1000 + 100 * pair + 20 * Convert.ToInt32(batched) + 4 * index;
                    var elapsed = await WriteFourAsync(db, first, batched);
                    output.WriteLine("EVIDENCE_WRITE_TIMING pair={0} sample={1} batch={2} milliseconds={3:F5}",
                        pair, index, batched, elapsed.TotalMilliseconds);
                }
            }
        }
    }

    async Task<IDbConnectionSettings> CreateSchemaAsync()
    {
        var connection = new NpgsqlConnectionStringBuilder(fixture.ConnectionString) { Username = "", Password = "" }.ConnectionString;
        var settings = new DbConnectionSettings().Add(MarketDataDbContext.MarketDataDbConnection, connection, "System.Data.Postgres");
        await new MarketDataSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAsync(["market_data"]);
        return settings;
    }

    static MarketDataDbContext Store(IDbConnectionSettings settings)
        => new(settings, Substitute.For<IDbContextFactory>(),
            Substitute.For<ISequenceIdGenerator>(), NullLogger<DbProvider>.Instance);

    static OptionTradeEvidence Evidence(int sequence)
    {
        var source = new OptionTradeSource("GLBX.MDP3", 1, 100, "OPTION-BATCH", new(2026, 11, 5),
            12.125m, 1, sequence, 1000, 1001, Guid.NewGuid(), "raw-option");
        return new(source, null, null, new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), null,
            new OptionPricingFailure("FixtureFailure", "Quote", source.ContractId, "Isolated persistence test"));
    }

    static async Task<TimeSpan> WriteFourAsync(MarketDataDbContext db, int first, bool batched)
    {
        var evidence = Enumerable.Range(first, 4).Select(Evidence).ToArray();
        var start = Stopwatch.GetTimestamp();
        if (batched)
            Assert.All(await db.WriteFirstBatchAsync(evidence, CancellationToken.None), accepted => Assert.True(accepted));
        else
            foreach (var item in evidence)
                Assert.True(await db.WriteFirstAsync(item, CancellationToken.None));
        return Stopwatch.GetElapsedTime(start);
    }
}
