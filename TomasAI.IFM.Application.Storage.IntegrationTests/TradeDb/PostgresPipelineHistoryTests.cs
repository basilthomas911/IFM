using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using FluentAssertions;
using TomasAI.IFM.Application.Storage.IntegrationTests.TradePlanDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.TradeDb;

[Collection("PostgreSQL plan atomicity")]
[Trait("Infrastructure", "SelfContained")]
public sealed class PostgresPipelineHistoryTests(PostgresTradePlanFixture fixture)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Keyset_history_reads_every_tied_timestamp_row_and_rejects_other_scopes(bool composition)
    {
        var portfolio = Random.Shared.Next(20_000_000, 30_000_000);
        var date = new DateOnly(2026, 10, 11);
        var at = new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);
        var expected = Enumerable.Range(0, 9).Select(_ => Guid.NewGuid()).ToArray();
        await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            await db.ExecuteAsync(TradeSchemaSql.Create, [], ct);
            foreach (var invocation in expected)
            {
                var sql = composition
                    ? "INSERT INTO trade.order_composition_history(portfolio_id,fund_id,value_date,evaluated_at_utc,invocation_id,workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_hash) VALUES($1,1,$2,$3,$4,$5,$6,1,1,'Test',$7,'Hash');"
                    : "INSERT INTO trade.trade_selection_history_by_fund_date(portfolio_id,fund_id,value_date,occurred_at_utc,invocation_id,workflow_id,event_id,target_horizon,outcome,reason_code,result_id,result_sha256) VALUES($1,1,$2,$3,$4,$5,$6,1,1,'Test',$7,'Hash');";
                await db.ExecuteAsync(sql, [portfolio, date, at, invocation, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], ct);
            }
            return true;
        });
        byte[]? cursor = null;
        byte[]? firstCursor = null;
        var actual = new List<Guid>();
        do
        {
            var page = await fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
            {
                if (composition)
                {
                    var read = await PostgresPipelineReadStore.CompositionHistoryAsync(db, portfolio, 1, date, 2, cursor, ct);
                    return (Ids: read.Items.Select(r => r.InvocationId).ToArray(), Cursor: read.PagingState);
                }
                var selection = await PostgresPipelineReadStore.SelectionHistoryAsync(db, portfolio, 1, date, 2, cursor, ct);
                return (Ids: selection.Items.Select(r => r.InvocationId).ToArray(), Cursor: selection.PagingState);
            });
            actual.AddRange(page.Ids);
            cursor = page.Cursor;
            firstCursor ??= cursor;
        } while (cursor is not null);
        actual.Should().BeEquivalentTo(expected);
        actual.Distinct().Count().Should().Be(expected.Length);
        firstCursor.Should().NotBeNull();
        await FluentActions.Awaiting(() => fixture.Transactions.ExecuteOnceAsync(async (db, ct) =>
        {
            if (composition) await PostgresPipelineReadStore.CompositionHistoryAsync(db, portfolio, 2, date, 2, firstCursor, ct);
            else await PostgresPipelineReadStore.SelectionHistoryAsync(db, portfolio, 2, date, 2, firstCursor, ct);
            return true;
        })).Should().ThrowAsync<ArgumentException>();
    }
}
