using System.Globalization;
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

/// <summary>Persists plan and activity rows on a caller-owned PostgreSQL transaction and reads ordered history.</summary>
public sealed class PostgresTradePlanStore(IDbConnectionSettings settings)
{
    readonly string connectionString = settings[TradePlanDbContext.TradePlanDbConnection].ConnectionString;
    static readonly (string Name, string Type)[] PlanOrder = [("planRevision", "bigint")];
    static readonly (string Name, string Type)[] ActivityOrder = [("calculatedAtUtc", "timestamptz"), ("portfolioId", "integer"), ("fundId", "integer"), ("orderId", "integer"), ("tradeId", "integer"), ("positionId", "uuid"), ("planRevision", "bigint")];
    static readonly (string Name, string Type)[] WorkflowOrder = [("updatedAtUtc", "timestamptz"), ("exitDecisionId", "uuid"), ("stageRevision", "bigint")];
    const string PositionFilter = "portfolioId=$1 AND fundId=$2 AND orderId=$3 AND tradeId=$4 AND positionId=$5 AND valueDate=$6";

    /// <summary>Writes one immutable revision and its activity row atomically; failures propagate to the transaction owner.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectMaterialAsync(IEnlistedPostgresTransaction transaction, StrategyTradePlanSnapshot plan, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(transaction);
        plan.Position.Id.RequireTradePlanScope(plan.Position.StrategyKind, plan.ValueDate);
        if (!plan.MaterialChange && plan.Position.StrategyKind != TradeStrategyKind.IronCondor) return;
        var id = plan.Position.Id.Trade;
        var table = "trade_plan." + plan.Position.StrategyKind.ToTradePlanTable();
        var payload = MessagePackBinarySerializer.Shared.Serialize(plan);
        object?[] values = [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, plan.Position.Id.PositionId, plan.ValueDate, plan.PlanRevision, plan.CalculatedAtUtc, plan.State.ToString(), plan.RequiresExit, plan.ContentHash, payload];
        var insert = ToSql(TradePlanDbCql.InsertPlan.ForTable(table)).TrimEnd(';');
        var stored = await transaction.ScalarAsync(insert + $" ON CONFLICT (portfolioId,fundId,orderId,tradeId,positionId,valueDate,planRevision) DO UPDATE SET contentHash={table}.contentHash WHERE {table}.contentHash=EXCLUDED.contentHash RETURNING contentHash;", values, token)
            .ConfigureAwait(false);
        if (stored is null) throw new InvalidOperationException("Trade plan revision conflicts with a different content hash.");
        if (plan.IronCondorTradePlanSnapshot is { } snapshot)
        {
            var scalars = (object?[])new IronCondorTradePlanProjection.ScalarParameters(plan, snapshot).Bind();
            await transaction.ExecuteAsync(ToSql(IronCondorTradePlanProjection.UpdateScalars.Replace("UPDATE iron_condor_trade_plan", "UPDATE trade_plan.iron_condor_trade_plan", StringComparison.Ordinal)), scalars, token)
            .ConfigureAwait(false);
        }
        object?[] activity = [plan.ValueDate, plan.CalculatedAtUtc, id.PortfolioId, id.FundId, id.OrderId, id.TradeId, plan.Position.Id.PositionId, checked((short)plan.Position.StrategyKind), plan.PlanRevision, checked((short)plan.State), plan.RequiresExit, plan.ContentHash, payload];
        var activitySql = ToSql(TradePlanDbCql.InsertActivity.Replace("INTO position_trade_plan_activity_by_date", "INTO trade_plan.position_trade_plan_activity_by_date", StringComparison.Ordinal)).TrimEnd(';');
        await transaction.ExecuteAsync(activitySql + " ON CONFLICT DO NOTHING;", activity, token)
            .ConfigureAwait(false);
    }

    /// <summary>Writes an immutable exit-workflow revision on the transaction supplied by its business operation.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1822", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
    public async Task ProjectExitWorkflowAsync(IEnlistedPostgresTransaction transaction, ExitPositionWorkflowProjection workflow, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(workflow);
        workflow.RequireValidExitWorkflow();
        var id = workflow.WorkflowId.Position.Trade;
        var payload = MessagePackBinarySerializer.Shared.Serialize(workflow);
        object?[] values = [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, workflow.WorkflowId.Position.PositionId, workflow.WorkflowId.ValueDate, workflow.UpdatedAtUtc, workflow.WorkflowId.ExitDecisionId, workflow.StageRevision, checked((short)workflow.StrategyKind), checked((short)workflow.State), workflow.SourcePlanEventId, payload];
        var sql = ToSql(TradePlanDbCql.InsertExitWorkflow.Replace("INTO position_exit_workflow", "INTO trade_plan.position_exit_workflow", StringComparison.Ordinal)).TrimEnd(';');
        var stored = await transaction.ScalarAsync(sql + " ON CONFLICT (portfolioId,fundId,orderId,tradeId,positionId,valueDate,updatedAtUtc,exitDecisionId,stageRevision) DO UPDATE SET payload=trade_plan.position_exit_workflow.payload WHERE trade_plan.position_exit_workflow.payload=EXCLUDED.payload RETURNING stageRevision;", values, token)
            .ConfigureAwait(false);
        if (stored is null) throw new InvalidOperationException("Exit workflow revision conflicts with a different payload.");
    }

    public async Task<StrategyTradePlanSnapshot?> GetCurrentAsync(StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, CancellationToken token = default)
    {
        var page = await GetHistoryAsync(position, strategy, date, 1, null, token)
            .ConfigureAwait(false);
        return page.Items.FirstOrDefault();
    }

    public Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsync(StrategyPositionId position, TradeStrategyKind strategy, DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        position.RequireTradePlanScope(strategy, date);
        return ReadPageAsync<StrategyTradePlanSnapshot>("trade_plan." + strategy.ToTradePlanTable(), PositionFilter, PositionValues(position, date), PlanOrder, size, cursor, token);
    }

    public Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsync(DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        date.RequireTradePlanValueDate();
        return ReadPageAsync<StrategyTradePlanSnapshot>("trade_plan.position_trade_plan_activity_by_date", "valueDate=$1", [date], ActivityOrder, size, cursor, token);
    }

    public async Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsync(StrategyPositionId position, DateOnly date, CancellationToken token = default)
    {
        var page = await GetExitWorkflowTimelineAsync(position, date, 1, null, token)
            .ConfigureAwait(false);
        return page.Items.FirstOrDefault();
    }

    public Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsync(StrategyPositionId position, DateOnly date, int size, byte[]? cursor = null, CancellationToken token = default)
    {
        position.RequireValueDate(date);
        return ReadPageAsync<ExitPositionWorkflowProjection>("trade_plan.position_exit_workflow", PositionFilter, PositionValues(position, date), WorkflowOrder, size, cursor, token);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA2100", Justification = "This storage executor accepts application SQL from reviewed schema/query contracts; runtime values are sent as bound parameters rather than interpolated into SQL.")]
    async Task<QueryPage<T>> ReadPageAsync<T>(string table, string filter, object?[] scope, (string Name, string Type)[] order, int size, byte[]? cursor, CancellationToken token)
    {
        size.RequireTradePlanPageSize();
        var scopeId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(table + "|" + JsonSerializer.Serialize(scope))));
        var values = scope.ToList();
        if (cursor is not null)
        {
            var resume = JsonSerializer.Deserialize<PageCursor>(cursor) ?? throw new ArgumentException("Invalid plan history cursor.", nameof(cursor));
            if (!string.Equals(resume.Scope,scopeId,StringComparison.Ordinal) || resume.Keys.Length != order.Length) throw new ArgumentException("Plan history cursor belongs to a different query.", nameof(cursor));
            var markers = new List<string>();
            for (var i = 0; i < order.Length; i++)
            {
                values.Add(resume.Keys[i]);
                markers.Add($"${values.Count}::{order[i].Type}");
            }
            filter += $" AND ({string.Join(',', order.Select(x => x.Name))}) < ({string.Join(',', markers)})";
        }
        values.Add(size + 1);
        var sql = $"SELECT payload,{string.Join(',', order.Select(x => x.Name))} FROM {table} WHERE {filter} ORDER BY {string.Join(',', order.Select(x => x.Name + " DESC"))} LIMIT ${values.Count};";
        var connection = new PostgresObjectDataRepositoryConnection().As<NpgsqlConnection>(connectionString);
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
            items.Add(MessagePackBinarySerializer.Shared.Deserialize<T>((await reader.GetFieldValueAsync<byte[]>(0,token)
            .ConfigureAwait(false))));
            lastKeys = Enumerable.Range(1, order.Length).Select(i => reader.GetValue(i) switch
            {
                DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
                IFormattable value => value.ToString(null, CultureInfo.InvariantCulture),
                var value => value.ToString()!
            }).ToArray();
        }
        return new(items.ToArray(), hasMore ? JsonSerializer.SerializeToUtf8Bytes(new PageCursor(scopeId, lastKeys!)) : null);
    }

    static object?[] PositionValues(StrategyPositionId position, DateOnly date)
    {
        var id = position.Trade;
        return [id.PortfolioId, id.FundId, id.OrderId, id.TradeId, position.PositionId, date];
    }

    // These fixed checked-in statements contain only positional markers, never string literals with question marks.
    static string ToSql(string cql)
    {
        var index = 0;
        return System.Text.RegularExpressions.Regex.Replace(cql, "\\?", _ => "$" + (++index).ToString(CultureInfo.InvariantCulture), System.Text.RegularExpressions.RegexOptions.NonBacktracking);
    }

    sealed record PageCursor(string Scope, string[] Keys);
}
