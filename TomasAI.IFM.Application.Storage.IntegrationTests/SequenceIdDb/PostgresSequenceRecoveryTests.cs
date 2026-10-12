using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.SequenceIdDb;

[Collection("PostgreSQL plan atomicity")]
public sealed class PostgresSequenceRecoveryTests(PostgresTradePlanFixture fixture)
{
    [Fact]
    public async Task Recovery_checks_all_sequences_without_allocating_ids_and_rejects_missing_functions()
    {
        var settings = new DbConnectionSettings().Add(SequenceIdDbContext.SequenceIdDbConnection, PostgresTradePlanFixture.Connection, "System.Data.Postgres");
        var schema = new SequenceIdSchemaDb(settings, NullLogger<DbProvider>.Instance);
        await schema.CreateAllAsync();
        var sql = "SELECT jsonb_agg(to_jsonb(s) ORDER BY name)::text FROM (" + string.Join(" UNION ALL ",
            Enum.GetNames<SequenceName>().Select(name => "SELECT '" + name + "'::text AS name,last_value,is_called FROM public." + name)) + ") s;";
        var before = await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync(sql, [], token));
        (await schema.ValidateTablesAsync()).Should().Be(Enum.GetNames<SequenceName>().Length + 2);
        (await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync(sql, [], token))).Should().Be(before);
        try
        {
            await fixture.Transactions.ExecuteOnceAsync(async (db, token) => { await db.ExecuteAsync("DROP FUNCTION public.fn_get_next_sequence_id(text);", [], token); return true; });
            await Assert.ThrowsAsync<InvalidOperationException>(() => schema.ValidateTablesAsync());
        }
        finally { await schema.CreateAllAsync(); }
        (await fixture.Transactions.ExecuteOnceAsync((db, token) => db.ScalarAsync(sql, [], token))).Should().Be(before);
    }
}
