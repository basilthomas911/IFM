
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
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Framework.Storage.Postgres;

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
        ArgumentNullException.ThrowIfNull(transaction);
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
        DateOnly date, int offset, int size, long? maximumRevision, CancellationToken token)
    {
        var table = "trade_plan." + strategy.ToTradePlanTable();
        return PostgresTradePlanOffsetReadSession.OpenAsync(context.ConnectionString, table, position, date, offset, size, maximumRevision, token);
    }

    internal Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsyncCore(StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        return context.ReadPageAsync<StrategyTradePlanSnapshot>("trade_plan." + strategy.ToTradePlanTable(), TradePlanDbSql.PositionFilter, (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind(), TradePlanDbSql.PlanOrder, size, cursor, token);
    }

    internal Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsyncCore(DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
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
        return context.ReadPageAsync<ExitPositionWorkflowProjection>("trade_plan.position_exit_workflow", TradePlanDbSql.PositionFilter, (object?[])new GetTradePlan(position.Trade.PortfolioId, position.Trade.FundId, position.Trade.OrderId, position.Trade.TradeId, position.PositionId, date).Bind(), TradePlanDbSql.WorkflowOrder, size, cursor, token);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    async Task<QueryPage<T>> ReadPageAsync<T>(string table, string filter, object?[] scope, (string Name, string Type)[] order, int size, byte[]? cursor, CancellationToken token)
    {
        var scopeId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table + "|" + JsonSerializer.Serialize(scope))));
        var values = scope.ToList();
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
