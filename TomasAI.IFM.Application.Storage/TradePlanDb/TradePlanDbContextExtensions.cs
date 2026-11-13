using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using static TomasAI.IFM.Shared.Storage.BindValues;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>PostgreSQL mutations and scoped keyset pagination.</summary>
internal static class TradePlanDbContextExtensions
{
    const string PositionFilter = "portfolioid=$1 AND fundid=$2 AND orderid=$3 AND tradeid=$4 AND positionid=$5 AND valuedate=$6";

    static readonly (string Name, string Type)[] PlanOrder = [("planrevision", "bigint")];
    static readonly (string Name, string Type)[] ActivityOrder =
    [
        ("calculatedatutc", "timestamptz"), ("portfolioid", "integer"), ("fundid", "integer"),
        ("orderid", "integer"), ("tradeid", "integer"), ("positionid", "uuid"), ("planrevision", "bigint")
    ];
    static readonly (string Name, string Type)[] WorkflowOrder =
        [("updatedatutc", "timestamptz"), ("exitdecisionid", "uuid"), ("stagerevision", "bigint")];

    extension(string table)
    {
        /// <summary>Substitutes the allowlisted table in a trade-plan query template.</summary>
        internal string BuildTradePlanStatement(string template) =>
            string.Format(CultureInfo.InvariantCulture, template, table);

        internal string PageScope(object?[] scope) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table + "|" + JsonSerializer.Serialize(scope))));
    }

    extension(TradePlanDbContext context)
    {
    /// <summary>Writes one immutable revision and its activity row atomically; failures propagate to the transaction owner.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the internal access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the internal access contract.")]
    internal async Task ProjectMaterialAsyncCore(IEnlistedPostgresTransaction transaction, StrategyTradePlanSnapshot plan, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var id = plan.Position.Id.Trade;
        var table = "trade_plan." + plan.Position.StrategyKind.ToTradePlanTable();
        var payload = MessagePackBinarySerializer.Shared.Serialize(plan);
        object?[] values = [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, plan.Position.Id.PositionId, plan.ValueDate, plan.PlanRevision, plan.CalculatedAtUtc, plan.State.ToString(), plan.RequiresExit, plan.ContentHash, payload];
        var insert = table.BuildTradePlanStatement(TradePlanDbSql.InsertPlan);
        var stored = await transaction.ScalarAsync(insert, values, token)
            .ConfigureAwait(false);
        if (stored is null) throw new InvalidOperationException("Trade plan revision conflicts with a different content hash.");
        if (plan.IronCondorTradePlanSnapshot is { } snapshot)
        {
            var scalars = (object?[])new IronCondorScalarParameters(plan, snapshot).Bind();
            await transaction.ExecuteAsync(TradePlanDbSql.UpdateScalars, scalars, token)
            .ConfigureAwait(false);
        }
        object?[] activity = [plan.ValueDate, plan.CalculatedAtUtc, id.PortfolioId, id.FundId, id.OrderId, id.TradeId, plan.Position.Id.PositionId, checked((short)plan.Position.StrategyKind), plan.PlanRevision, checked((short)plan.State), plan.RequiresExit, plan.ContentHash, payload];
        await transaction.ExecuteAsync(TradePlanDbSql.InsertActivity, activity, token)
            .ConfigureAwait(false);
    }

    /// <summary>Writes an immutable exit-workflow revision on the transaction supplied by its business operation.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the internal access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the internal access contract.")]
    internal async Task ProjectExitWorkflowAsyncCore(IEnlistedPostgresTransaction transaction, ExitPositionWorkflowProjection workflow, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        var id = workflow.WorkflowId.Position.Trade;
        var payload = MessagePackBinarySerializer.Shared.Serialize(workflow);
        object?[] values = [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, workflow.WorkflowId.Position.PositionId, workflow.WorkflowId.ValueDate, workflow.UpdatedAtUtc, workflow.WorkflowId.ExitDecisionId, workflow.StageRevision, checked((short)workflow.StrategyKind), checked((short)workflow.State), workflow.SourcePlanEventId, payload];
        var stored = await transaction.ScalarAsync(TradePlanDbSql.InsertExitWorkflow, values, token)
            .ConfigureAwait(false);
        if (stored is null) throw new InvalidOperationException("Exit workflow revision conflicts with a different payload.");
    }

    internal async Task<StrategyTradePlanSnapshot?> GetCurrentAsyncCore(StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, CancellationToken token = default)
    {
        var page = await context.GetHistoryAsyncCore(position, strategy, date, 1, null, token)
            .ConfigureAwait(false);
        return page.Items.FirstOrDefault();
    }

    internal async Task<IReadOnlyDictionary<DateOnly, decimal?>> ReadDailyForwardSpreadsAsync(StrategyPositionId position,
        IReadOnlyDictionary<DateOnly, DateTime> dayEndsUtc, CancellationToken token)
    {
        var result = new Dictionary<DateOnly, decimal?>();
        if (dayEndsUtc.Count == 0) return result;
        var days = dayEndsUtc.OrderBy(day => day.Key).ToArray();
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
        await connection.OpenAsync(token)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(TradePlanDbSql.TradePlanDbContextExtensions_ReadDailyForwardSpreadsAsync_1, connection);
        foreach (var value in new object[] { position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId,
            position.Trade.TradeId, position.PositionId })
            PostgresValueBinding.AddParameter(command, value);
        PostgresValueBinding.AddParameter(command, days.Select(day => day.Key).ToArray());
        PostgresValueBinding.AddParameter(command, days.Select(day => day.Value).ToArray());
        await using var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
            result.Add(reader.GetFieldValue<DateOnly>(0), reader.IsDBNull(1) ? null : reader.GetDecimal(1));
        return result;
    }

    internal async Task<StrategyTradePlanChartSeries> ReadChartRangeAsync(StrategyPositionId position, DateOnly firstValueDate,
        DateOnly lastValueDate, DateTime startUtc, DateTime endUtc, CancellationToken token)
    {
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
        await connection.OpenAsync(token)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(TradePlanDbSql.TradePlanDbContextExtensions_ReadChartRangeAsync_1, connection);
        foreach (var value in new object[] { position.Trade.PortfolioId,position.Trade.FundId,position.Trade.OrderId,
            position.Trade.TradeId,position.PositionId,firstValueDate,lastValueDate,startUtc,endUtc })
            PostgresValueBinding.AddParameter(command, value);
        var points = new List<StrategyTradePlanChartPoint>();
        await using var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
            points.Add(new(reader.GetDateTime(0),reader.GetDecimal(1)) { ForwardSpread = reader.IsDBNull(2) ? null : reader.GetDecimal(2) });
        return new(startUtc,endUtc,points.ToArray());
    }

    internal Task<ITradePlanOffsetReadSession> OpenOffsetHistoryCoreAsync(StrategyPositionId position, TradeStrategyKind strategy,
        DateOnly date, int offset, int size, long? maximumRevision, CancellationToken token,
        bool returnContinuation = false, byte[]? pagingState = null)
    {
        var table = "trade_plan." + strategy.ToTradePlanTable();
        return PostgresTradePlanOffsetReadSession.OpenAsync(context.ConnectionString, table, position, date, offset, size,
            maximumRevision, token, returnContinuation, pagingState);
    }

    internal Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsyncCore(StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        return context.ReadPageAsync<StrategyTradePlanSnapshot>("trade_plan." + strategy.ToTradePlanTable(), PositionFilter, (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind(), PlanOrder, size, cursor, token);
    }

    internal Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsyncCore(DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        return context.ReadPageAsync<StrategyTradePlanSnapshot>("trade_plan.position_trade_plan_activity_by_date", "valuedate=$1", [date], ActivityOrder, size, cursor, token);
    }

    internal async Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsyncCore(StrategyPositionId position, DateOnly date, CancellationToken token = default)
    {
        var page = await context.GetExitWorkflowTimelineAsyncCore(position, date, 1, null, token)
            .ConfigureAwait(false);
        return page.Items.FirstOrDefault();
    }

    internal Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsyncCore(StrategyPositionId position, DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        return context.ReadPageAsync<ExitPositionWorkflowProjection>("trade_plan.position_exit_workflow", PositionFilter, (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind(), WorkflowOrder, size, cursor, token);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    async Task<QueryPage<T>> ReadPageAsync<T>(string table, string filter, object?[] scope, (string Name, string Type)[] order, int size, byte[]? cursor, CancellationToken token)
    {
        var scopeId = table.PageScope(scope);
        var values = scope.ToList();
        var columns = string.Join(',', order.Select(column => column.Name));
        if (cursor is not null)
        {
            var resume = JsonSerializer.Deserialize<TradePlanPageCursor>(cursor) ?? throw new ArgumentException("Invalid plan history cursor.", nameof(cursor));
            if (!string.Equals(resume.Scope, scopeId, StringComparison.Ordinal) || resume.Keys.Length != order.Length)
                throw new ArgumentException("Plan history cursor belongs to a different query.", nameof(cursor));
            var markers = new List<string>();
            for (var i = 0; i < order.Length; i++)
            {
                values.Add(resume.Keys[i]);
                markers.Add($"${values.Count}::{order[i].Type}");
            }
            filter += $" AND ({columns}) < ({string.Join(',', markers)})";
        }
        values.Add(size + 1);
        var ordering = string.Join(',', order.Select(column => column.Name + " DESC"));
        var limit = "$" + values.Count.ToString(CultureInfo.InvariantCulture);
        var sql = string.Format(CultureInfo.InvariantCulture, TradePlanDbSql.ReadPageTemplate,
            columns, table, filter, ordering, limit);
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        await connection.OpenAsync(token)
            .ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, connection);
        await using var commandDisposal = command.ConfigureAwait(false);
        foreach (var value in values) PostgresValueBinding.AddParameter(command, value);
        var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
        await using var readerDisposal = reader.ConfigureAwait(false);
        var items = new List<T>();
        string[]? lastKeys = null;
        var hasMore = false;
        while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
        {
            if (items.Count == size) { hasMore = true; break; }
            var mapped = TradePlanDbContext.MapToPageItem<T>(reader, order.Length);
            items.Add(mapped.Item);
            lastKeys = mapped.Keys;
        }
        return new(items.ToArray(), hasMore ? JsonSerializer.SerializeToUtf8Bytes(new TradePlanPageCursor(scopeId, lastKeys!)) : null);
    }

        /// <summary>Locks and reads accepted stages without creating or dispatching a baseline event.</summary>
        internal async Task<IReadOnlyList<ExitPositionWorkflowProjection>> ReadExitWorkflowBaselineCoreAsync(IEnlistedPostgresTransaction transaction, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(transaction);
            await transaction.ExecuteAsync(TradePlanDbSql.LockExitWorkflowBaseline, [], token)
            .ConfigureAwait(false);
            var rows = await transaction.QueryAsync(TradePlanDbSql.ReadExitWorkflowBaseline, [], TradePlanDbContext.MapToExitWorkflow, token)
            .ConfigureAwait(false);
            return rows.ToArray();
        }

        /// <summary>Owns one write attempt when there is no caller-owned transaction.</summary>
        internal PostgresEventTransaction StandaloneTransaction() => new(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, context.ConnectionString, "System.Data.Postgres"));

        internal Task ProjectMaterialAsyncCoreBinding(StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default) =>
            context.StandaloneTransaction()
            .ExecuteOnceAsync(async (transaction, token) =>
            {
                await context.ProjectMaterialAsyncCore(transaction, plan, token)
                    .ConfigureAwait(false);
                return true;
            }, cancellationToken);

        internal Task ProjectExitWorkflowAsyncCoreBinding(ExitPositionWorkflowProjection workflow, CancellationToken cancellationToken = default)
        {
            return context.StandaloneTransaction()
            .ExecuteOnceAsync(async (transaction, token) =>
            {
                await context.ProjectExitWorkflowAsyncCore(transaction, workflow, token)
            .ConfigureAwait(false);
                return true;
            }, cancellationToken);
        }
    }
    extension(TradeStrategyKind strategy)
    {
        /// <summary>
        /// Gets the materialized Trade Plan table for a supported trade strategy.
        /// </summary>
        /// <returns>The provider table name.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal string ToTradePlanTable() => strategy switch
        {
            TradeStrategyKind.IronCondor => "iron_condor_trade_plan",
            TradeStrategyKind.VerticalSpread => "vertical_spread_trade_plan",
            TradeStrategyKind.FuturesOutright => "futures_trade_plan",
            _ => throw new ArgumentOutOfRangeException(
                nameof(strategy),
                strategy,
                "Unsupported Trade Plan strategy.")
        };
    }

}

internal sealed record TradePlanPageCursor(string Scope, string[] Keys);

/// <summary>Owns the connection and repeatable read transaction for a history page and optional chart.</summary>
internal sealed class PostgresTradePlanOffsetReadSession(
    NpgsqlConnection connection, NpgsqlTransaction transaction, string table, object?[] scope, StrategyTradePlanHistoryPage page)
    : ITradePlanOffsetReadSession
{
    sealed record HistoryCursor(
        [property: JsonRequired] string Scope,
        [property: JsonRequired] long MaximumRevision,
        [property: JsonRequired] long LastRevision,
        [property: JsonRequired] int Offset);

    public StrategyTradePlanHistoryPage Page => page;

    internal static async Task<ITradePlanOffsetReadSession> OpenAsync(string connectionString, string table, StrategyPositionId position,
        DateOnly date, int offset, int size, long? maximumRevision, CancellationToken token,
        bool returnContinuation = false, byte[]? pagingState = null)
    {
        var scope = (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId,
            position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind();
        var scopeId = returnContinuation ? table.PageScope(scope) : null;
        var cursor = pagingState is null ? null : ReadCursor(pagingState, scopeId, offset, maximumRevision);
        maximumRevision ??= cursor?.MaximumRevision;

        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
        NpgsqlTransaction? transaction = null;
        try
        {
            await connection.OpenAsync(token)
            .ConfigureAwait(false);
            transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token)
            .ConfigureAwait(false);
            // The split uses the Iron Condor observation-time index.
            var metadataTemplate = table == "trade_plan.iron_condor_trade_plan"
                ? maximumRevision.HasValue ? TradePlanDbSql.AnchoredHistoryMetadataTemplate : TradePlanDbSql.LatestHistoryMetadataTemplate
                : TradePlanDbSql.OffsetHistoryMetadataTemplate;
            await using var count = new NpgsqlCommand(table.BuildTradePlanStatement(metadataTemplate), connection, transaction);
            PostgresValueBinding.AddParameters(count, metadataTemplate == TradePlanDbSql.LatestHistoryMetadataTemplate
                ? scope : [.. scope, Bigint(maximumRevision)]);
            long total, anchor;
            DateTime? chartEnd;
            await using (var reader = await count.ExecuteReaderAsync(token)
            .ConfigureAwait(false))
            {
                await reader.ReadAsync(token)
            .ConfigureAwait(false);
                total = reader.GetInt64(0);
                anchor = maximumRevision ?? reader.GetInt64(1);
                chartEnd = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            }
            var (template, values) = HistoryRows(scope, anchor, offset, size, returnContinuation, cursor);
            await using var command = new NpgsqlCommand(table.BuildTradePlanStatement(template), connection, transaction);
            PostgresValueBinding.AddParameters(command, values);
            var items = new List<StrategyTradePlanSnapshot>(size);
            long lastRevision = 0;
            await using (var reader = await command.ExecuteReaderAsync(token)
                .ConfigureAwait(false))
            {
                while (await reader.ReadAsync(token)
                    .ConfigureAwait(false))
                {
                    items.Add(MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(reader.GetFieldValue<byte[]>(0)));
                    lastRevision = reader.GetInt64(1);
                }
            }
            var nextOffset = (long)offset + items.Count;
            var next = scopeId is not null && items.Count > 0 && nextOffset < total && nextOffset <= int.MaxValue
                ? JsonSerializer.SerializeToUtf8Bytes(new HistoryCursor(scopeId, anchor, lastRevision, (int)nextOffset))
                : null;
            var page = new StrategyTradePlanHistoryPage(items.ToArray(), next)
            {
                Offset = offset, TotalCount = total, MaximumRevision = anchor, ChartEndUtc = chartEnd
            };
            return new PostgresTradePlanOffsetReadSession(connection, transaction, table, scope, page);
        }
        catch
        {
            try { if (transaction is not null) await transaction.DisposeAsync()
            .ConfigureAwait(false); }
            finally { await connection.DisposeAsync()
            .ConfigureAwait(false); }
            throw;
        }
    }

    static (string Template, object?[] Values) HistoryRows(object?[] scope, long anchor, int offset, int size,
        bool returnContinuation, HistoryCursor? cursor)
    {
        if (cursor is not null)
            return (TradePlanDbSql.ContinueHistoryRowsTemplate, [.. scope, anchor, cursor.LastRevision, size]);
        if (returnContinuation && offset == 0)
            return (TradePlanDbSql.FirstHistoryRowsTemplate, [.. scope, anchor, size]);
        return (TradePlanDbSql.OffsetHistoryRowsTemplate, [.. scope, anchor, offset, size]);
    }

    static HistoryCursor ReadCursor(byte[] pagingState, string? scope, int offset, long? maximumRevision)
    {
        HistoryCursor? cursor;
        try
        {
            cursor = JsonSerializer.Deserialize<HistoryCursor>(pagingState);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Invalid trade plan history continuation.", nameof(pagingState), ex);
        }

        if (cursor is null || !string.Equals(cursor.Scope, scope, StringComparison.Ordinal)
            || offset <= 0 || cursor.Offset != offset || cursor.MaximumRevision < 0
            || cursor.LastRevision > cursor.MaximumRevision
            || (maximumRevision is { } anchor && cursor.MaximumRevision != anchor))
            throw new ArgumentException("Trade plan history continuation does not match the requested scope, anchor or offset.", nameof(pagingState));
        return cursor;
    }

    public async Task<StrategyTradePlanChartPoint[]> ReadChartAsync(DateTime startUtc, DateTime endUtc, CancellationToken token)
    {
        await using var command = new NpgsqlCommand(table.BuildTradePlanStatement(TradePlanDbSql.OffsetHistoryChartTemplate), connection, transaction);
        PostgresValueBinding.AddParameters(command, [.. scope, page.MaximumRevision, startUtc, endUtc]);
        var samples = new List<StrategyTradePlanChartPoint>();
        await using var reader = await command.ExecuteReaderAsync(token)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(token)
            .ConfigureAwait(false))
            samples.Add(new(reader.GetDateTime(0), reader.GetDecimal(1)) { ForwardSpread = reader.IsDBNull(2) ? null : reader.GetDecimal(2) });
        return samples.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        try { await transaction.DisposeAsync()
            .ConfigureAwait(false); }
        finally { await connection.DisposeAsync()
            .ConfigureAwait(false); }
    }
}
