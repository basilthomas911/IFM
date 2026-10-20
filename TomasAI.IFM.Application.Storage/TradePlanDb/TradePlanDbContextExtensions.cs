
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>PostgreSQL mutations and scoped keyset pagination.</summary>
internal static class TradePlanDbContextExtensions
{
    extension(TradePlanDbContext context)
    {
    /// <summary>Writes one immutable revision and its activity row atomically; failures propagate to the transaction owner.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the internal access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the internal access contract.")]
    internal async Task ProjectMaterialAsyncCore(IEnlistedPostgresTransaction transaction, StrategyTradePlanSnapshot plan, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(transaction);
        plan.Position.Id.RequireTradePlanScope(plan.Position.StrategyKind, plan.ValueDate);
        if (!plan.MaterialChange && plan.Position.StrategyKind != TradeStrategyKind.IronCondor) return;
        var id = plan.Position.Id.Trade;
        var table = "trade_plan." + plan.Position.StrategyKind.ToTradePlanTable();
        var payload = MessagePackBinarySerializer.Shared.Serialize(plan);
        object?[] values = [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, plan.Position.Id.PositionId, plan.ValueDate, plan.PlanRevision, plan.CalculatedAtUtc, plan.State.ToString(), plan.RequiresExit, plan.ContentHash, payload];
        var insert = TradePlanDbSql.PlanInsert(table);
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
        ArgumentNullException.ThrowIfNull(workflow);
        workflow.RequireValidExitWorkflow();
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

    internal async Task<StrategyTradePlanHistoryPage> ReadOffsetHistoryAsync(StrategyPositionId position, TradeStrategyKind strategy,
        DateOnly date, int offset, int size, long? maximumRevision, CancellationToken token)
    {
        position.RequireTradePlanScope(strategy, date);
        size.RequireTradePlanPageSize();
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        if (maximumRevision is < 0) throw new ArgumentOutOfRangeException(nameof(maximumRevision));
        var table = "trade_plan." + strategy.ToTradePlanTable();
        var filter = TradePlanDbSql.PositionFilter + " AND ($7::bigint IS NULL OR planRevision <= $7)";
        await using var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
        await connection.OpenAsync(token).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, token).ConfigureAwait(false);
        object?[] scope = [position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date];
        await using var count = new NpgsqlCommand($"SELECT count(*),coalesce(max(planRevision),0),max(calculatedAtUtc) FROM {table} WHERE {filter}", connection, transaction);
        foreach (var value in scope) count.Parameters.Add(new NpgsqlParameter { Value = value! });
        count.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Bigint, Value = (object?)maximumRevision ?? DBNull.Value });
        long total, anchor;
        DateTime? chartEnd;
        await using (var reader = await count.ExecuteReaderAsync(token).ConfigureAwait(false))
        {
            await reader.ReadAsync(token).ConfigureAwait(false);
            total = reader.GetInt64(0); anchor = maximumRevision ?? reader.GetInt64(1);
            chartEnd = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
        }
        await using var command = new NpgsqlCommand($"SELECT payload FROM {table} WHERE {filter} ORDER BY planRevision DESC OFFSET $8 LIMIT $9", connection, transaction);
        foreach (var value in scope) command.Parameters.Add(new NpgsqlParameter { Value = value! });
        foreach (var value in new object[] { anchor, offset, size }) command.Parameters.Add(new NpgsqlParameter { Value = value });
        var items = new List<StrategyTradePlanSnapshot>(size);
        await using (var reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
            while (await reader.ReadAsync(token).ConfigureAwait(false))
                items.Add(MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(reader.GetFieldValue<byte[]>(0)));
        StrategyTradePlanChartPoint[] points = [];
        if (offset == 0 && chartEnd is { } end)
        {
            // Cache the independent chart for one minute; grid rows retain their own revision boundary.
            var series = await TradePlanChartCache.ReadAsync(
                (context.ConnectionString, position, strategy, date, maximumRevision), async () =>
            {
                var samples = new List<StrategyTradePlanChartPoint>();
                // Latest price-only sample per UTC minute (at most 361 buckets across six hours).
                // No historical position/leg payload is fetched for the chart.
                await using var chart = new NpgsqlCommand($"SELECT DISTINCT ON (date_bin(interval '1 minute',calculatedAtUtc,timestamptz '2000-01-01')) calculatedAtUtc,combinedSpreadPrice,combinedForwardPrice FROM {table} WHERE {filter} AND calculatedAtUtc >= $8 AND calculatedAtUtc <= $9 AND combinedSpreadPrice IS NOT NULL ORDER BY date_bin(interval '1 minute',calculatedAtUtc,timestamptz '2000-01-01'),calculatedAtUtc DESC,planRevision DESC", connection, transaction);
                foreach (var value in scope) chart.Parameters.Add(new NpgsqlParameter { Value = value! });
                foreach (var value in new object[] { anchor, end.AddHours(-6), end }) chart.Parameters.Add(new NpgsqlParameter { Value = value });
                await using var reader = await chart.ExecuteReaderAsync(token).ConfigureAwait(false);
                while (await reader.ReadAsync(token).ConfigureAwait(false)) samples.Add(new(reader.GetDateTime(0),reader.GetDecimal(1)) { ForwardSpread = reader.IsDBNull(2) ? null : reader.GetDecimal(2) });
                return new TradePlanChartCache.Series(end, samples.ToArray());
            }, token).ConfigureAwait(false);
            chartEnd = series.EndUtc;
            points = series.Points;
        }
        await transaction.CommitAsync(token).ConfigureAwait(false);
        return new(items.ToArray(), null) { Offset = offset, TotalCount = total, MaximumRevision = anchor,
            ChartEndUtc = chartEnd, ChartPoints = points };
    }

    internal Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsyncCore(StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        position.RequireTradePlanScope(strategy, date);
        return context.ReadPageAsync<StrategyTradePlanSnapshot>("trade_plan." + strategy.ToTradePlanTable(), TradePlanDbSql.PositionFilter, (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind(), TradePlanDbSql.PlanOrder, size, cursor, token);
    }

    internal Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsyncCore(DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        date.RequireTradePlanValueDate();
        return context.ReadPageAsync<StrategyTradePlanSnapshot>("trade_plan.position_trade_plan_activity_by_date", "valueDate=$1", [date], TradePlanDbSql.ActivityOrder, size, cursor, token);
    }

    internal async Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsyncCore(StrategyPositionId position, DateOnly date, CancellationToken token = default)
    {
        var page = await context.GetExitWorkflowTimelineAsyncCore(position, date, 1, null, token)
            .ConfigureAwait(false);
        return page.Items.FirstOrDefault();
    }

    internal Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsyncCore(StrategyPositionId position, DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        position.RequireValueDate(date);
        return context.ReadPageAsync<ExitPositionWorkflowProjection>("trade_plan.position_exit_workflow", TradePlanDbSql.PositionFilter, (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind(), TradePlanDbSql.WorkflowOrder, size, cursor, token);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    async Task<QueryPage<T>> ReadPageAsync<T>(string table, string filter, object?[] scope, (string Name, string Type)[] order, int size, byte[]? cursor, CancellationToken token)
    {
        size.RequireTradePlanPageSize();
        var scopeId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table + "|" + JsonSerializer.Serialize(scope))));
        var values = scope.ToList();
        if (cursor is not null)
        {
            var resume = JsonSerializer.Deserialize<TradePlanPageCursor>(cursor) ?? throw new ArgumentException("Invalid plan history cursor.", nameof(cursor));
            if (!string.Equals(resume.Scope,scopeId,StringComparison.Ordinal) || resume.Keys.Length != order.Length) throw new ArgumentException("Plan history cursor belongs to a different query.", nameof(cursor));
            var markers = new List<string>();
            for (var i = 0; i < order.Length; i++)
            {
                values.Add(resume.Keys[i]);
                markers.Add($"${values.Count}::{order[i].Type}");
            }
            filter = TradePlanDbSql.ContinuedFilter(filter, order, markers);
        }
        values.Add(size + 1);
        var sql = TradePlanDbSql.Page(table, filter, order, values.Count);
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(context.ConnectionString);
        await using var connectionDisposal = connection.ConfigureAwait(false);
        await connection.OpenAsync(token)
            .ConfigureAwait(false);
        var command = new NpgsqlCommand(sql, connection);
        await using var commandDisposal = command.ConfigureAwait(false);
        foreach (var value in values) command.Parameters.Add(new NpgsqlParameter { Value = value ?? DBNull.Value });
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

        internal async Task ProjectMaterialAsyncCoreBinding(StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);
            try
            {
                await context.StandaloneTransaction().ExecuteOnceAsync(async (transaction, token) =>
                {
                    await context.ProjectMaterialAsyncCore(transaction, plan, token)
                .ConfigureAwait(false);
                    return true;
                }, cancellationToken)
                .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                StorageDiagnosticLog.PlanDropped(context.tradePlanLogger, exception, nameof(context.ProjectMaterialAsync), plan.Position.Id, plan.PlanRevision, plan.ValueDate);
            }
        }

        internal Task ProjectExitWorkflowAsyncCoreBinding(ExitPositionWorkflowProjection workflow, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            return context.StandaloneTransaction().ExecuteOnceAsync(async (transaction, token) =>
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

    extension(StrategyPositionId positionId)
    {
        /// <summary>
        /// Requires a valid Trade Plan identity, supported strategy, and value date.
        /// </summary>
        /// <param name="strategy">The trade strategy.</param>
        /// <param name="valueDate">The plan value date.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2208", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireTradePlanScope(TradeStrategyKind strategy, DateOnly valueDate)
        {
            if (!positionId.IsValid
                || valueDate == default
                || strategy is not (
                    TradeStrategyKind.IronCondor
                    or TradeStrategyKind.VerticalSpread
                    or TradeStrategyKind.FuturesOutright))
            {
                throw new ArgumentException(
                    "Valid Trade Plan identity, strategy, and value date are required.",nameof(positionId));
            }
        }

        /// <summary>
        /// Requires a valid strategy-position identity and value date.
        /// </summary>
        /// <param name="valueDate">The plan or workflow value date.</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireValueDate(DateOnly valueDate)
        {
            if (!positionId.IsValid || valueDate == default)
            {
                throw new ArgumentException(                    "Valid strategy-position identity and value date are required.",nameof(valueDate));
            }
        }
    }

    extension(DateOnly valueDate)
    {
        /// <summary>
        /// Requires a non-default Trade Plan value date.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireTradePlanValueDate()
        {
            if (valueDate == default)
                throw new ArgumentException("A value date is required.", nameof(valueDate));
        }
    }

    extension(int pageSize)
    {
        /// <summary>
        /// Requires a Trade Plan page size between one and five hundred records.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "The named argument is the declared C# 14 extension receiver; its name is part of the caller contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireTradePlanPageSize()
        {
            if (pageSize is < 1 or > 500)
                throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
    }

    extension(ExitPositionWorkflowProjection workflow)
    {
        /// <summary>
        /// Requires a structurally valid exit-position workflow projection.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S3928", Justification = "This existing options validator reports the invalid configuration property by name; preserve its established exception type and diagnostic property name.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal void RequireValidExitWorkflow()
        {
            ExitWorkflowProjectionFacts.RequireValid(workflow);
        }
    }
}

internal sealed record TradePlanPageCursor(string Scope, string[] Keys);
