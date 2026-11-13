using System.Data.Common;
using System.Reflection;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.Benchmarks;

internal sealed class LegacyFundOrderFillsQuery(IDbConnectionSettings settings)
{
    internal const string Sql = """
        WITH latest AS (
            SELECT DISTINCT ON (orderid) portfolioid, fundid, orderid, payload
            FROM trade.trade_order
            WHERE portfolioid=$1 AND fundid=$2 AND valuedate=$3 AND orderid >= $4
            ORDER BY orderid, revision DESC
        )
        SELECT o.payload, e.payload,
            (SELECT CASE WHEN COUNT(*) = 1 THEN MIN(t.tradeid) END
             FROM trade.established_trade t
             WHERE t.portfolioid=o.portfolioid AND t.fundid=o.fundid AND t.orderid=o.orderid),
            (SELECT first.updatedatutc FROM trade.trade_order first
             WHERE first.portfolioid=o.portfolioid AND first.fundid=o.fundid AND first.orderid=o.orderid
             ORDER BY first.revision LIMIT 1)
        FROM latest o
        LEFT JOIN trade.order_execution e USING (portfolioid, fundid, orderid)
        WHERE (o.orderid, COALESCE(e.executionattemptid, '00000000-0000-0000-0000-000000000000'::uuid)) > ($4,$5)
        ORDER BY o.orderid, e.executionattemptid
        LIMIT $6;
        """;

    static readonly Func<DbDataReader, FundOrderFillsRow> MapRow = typeof(TradeDbContext)
        .GetMethod("MapToFundOrderFillsRow", BindingFlags.NonPublic | BindingFlags.Static)!
        .CreateDelegate<Func<DbDataReader, FundOrderFillsRow>>();

    internal Task<FundOrderFillsRow[]> ReadAsync(int portfolio, int fund, DateOnly date, int order, Guid attempt) =>
        new PostgresEventTransaction(settings).ExecuteOnceAsync(async (db, token) =>
            (await db.QueryAsync(Sql, [portfolio, fund, date, order, attempt, FundOrderFillsRow.PageSize], MapRow, token)
                .ConfigureAwait(false)).ToArray());
}
