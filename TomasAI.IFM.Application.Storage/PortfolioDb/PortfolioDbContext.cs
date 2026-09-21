using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

public sealed class PortfolioDbContext(IDbConnectionSettings settings, ILogger<DbProvider> logger,
    IPostgresEventTransaction? transactions = null)
    : ObjectDataRepository<PortfolioDbContext>(settings[PortfolioDbConnection], logger),
      IPortfolioDbReadContext, IPortfolioDbWriteContext
{
    public const string PortfolioDbConnection = "PortfolioDbConnection";
    public override PortfolioDbContext Database => this;

    public Task<T?> ReadOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash = null, CancellationToken ct = default)
        where T : class, IFinancialCompletedEvent => RequiredTransactions().ExecuteAsync(
            (db, cancellation) => PortfolioDbFinancialSupport.ReadOperationAsync<T>(db, portfolioId, operationId, inputHash, cancellation), ct);

    public Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId, CancellationToken ct = default) =>
        RequiredTransactions().ExecuteAsync(async (db, cancellation) =>
        {
            var value = await db.ScalarAsync(PortfolioDbSql.Financial.ReadBook, [portfolioId], cancellation).ConfigureAwait(false);
            return value is string json ? PortfolioDbFinancialSupport.Decode<FinancialBookConfiguration>(json) : null;
        }, ct);

    public Task<FinancialBookConfiguration?> ReadActiveBookByExecutionAccountAsync(
        string environment,
        string executionAccountReference,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionAccountReference);
        return RequiredTransactions().ExecuteAsync(async (db, cancellation) =>
        {
            var value = await db.ScalarAsync(
                PortfolioDbSql.Financial.ReadActiveBookByExecutionAccount,
                [environment, executionAccountReference],
                cancellation).ConfigureAwait(false);
            return value is string json ? PortfolioDbFinancialSupport.Decode<FinancialBookConfiguration>(json) : null;
        }, ct);
    }

    internal Task CreateBookAsync(FinancialBookConfiguration book, IReadOnlyList<LedgerAccountDefinition> accounts,
        IReadOnlyList<LedgerPostingRule> rules, DateOnly periodStart, DateOnly periodEnd, CancellationToken ct = default) =>
        RequiredTransactions().ExecuteAsync(async (db, cancellation) =>
        {
            await PortfolioDbFinancialSupport.CreateBookAsync(db, book, accounts, rules, periodStart, periodEnd, Guid.NewGuid(), cancellation).ConfigureAwait(false);
            return true;
        }, ct);

    public Task<PortfolioReadModel?> GetPortfolioAsync(int id, CancellationToken ct = default) =>
        One<PortfolioReadModel>(nameof(PortfolioDbSql.Portfolio.Get), PortfolioDbSql.Portfolio.Get, Values(Pos(id)), ct);
    public Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int id, CancellationToken ct = default) =>
        OneValue(nameof(PortfolioDbSql.Portfolio.Revision), PortfolioDbSql.Portfolio.Revision, Values(Pos(id)),
            row => new PortfolioProjectionRevision(id, null, row.GetLong(0), row.GetLong(1)), ct);
    public Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState state, int bucket, int afterId, int size, CancellationToken ct = default) =>
        Many<PortfolioReadModel>(nameof(PortfolioDbSql.Portfolio.ByState), PortfolioDbSql.Portfolio.ByState, Values(state.ToString(), bucket, afterId, Page(size)), ct);
    public Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int portfolioId, int afterId, int size, CancellationToken ct = default) =>
        Many<FundMandateReadModel>(nameof(PortfolioDbSql.Fund.ByPortfolio), PortfolioDbSql.Fund.ByPortfolio, Values(Pos(portfolioId), afterId, Page(size)), ct);
    public Task<FundMandateReadModel?> GetFundAsync(int id, CancellationToken ct = default) =>
        One<FundMandateReadModel>(nameof(PortfolioDbSql.Fund.Get), PortfolioDbSql.Fund.Get, Values(Pos(id)), ct);
    public Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int id, CancellationToken ct = default) =>
        OneValue(nameof(PortfolioDbSql.Fund.Revision), PortfolioDbSql.Fund.Revision, Values(Pos(id)),
            row => new PortfolioProjectionRevision(row.GetInt(0), id, row.GetLong(1), row.GetLong(2)), ct);

    public Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int portfolioId, int year, string horizon, DateTime atUtc, int size, CancellationToken ct = default)
    {
        Utc(atUtc); ArgumentException.ThrowIfNullOrWhiteSpace(horizon);
        return Many<FundMandateReadModel>(nameof(PortfolioDbSql.Fund.Active), PortfolioDbSql.Fund.Active,
            Values(Pos(portfolioId), year, horizon, atUtc, Page(size)), ct);
    }

    public async Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetSelectionAssignmentsAsync(int portfolioId, int fundId, long version, string horizon, string root, DateTime asOfUtc, CancellationToken ct = default)
    {
        Utc(asOfUtc); ArgumentException.ThrowIfNullOrWhiteSpace(horizon); ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var rows = await Many<FundTradeTemplateAssignmentReadModel>(nameof(PortfolioDbSql.Fund.Assignments), PortfolioDbSql.Fund.Assignments,
            Values(Pos(portfolioId), Pos(fundId), Positive(version), 4096), ct).ConfigureAwait(false);
        if (rows.Count == 4096)
            throw new InvalidOperationException("Selection assignment partition reached the 4096-row historical scan budget; no truncated candidates returned.");
        return rows.Where(x => x.EffectiveFromUtc <= asOfUtc && !(x.EffectiveUntilUtc <= asOfUtc)
                && x.DecisionHorizon.Equals(horizon, StringComparison.OrdinalIgnoreCase)
                && x.UnderlyingUniverse.Contains(root, StringComparer.OrdinalIgnoreCase)).Take(17).ToArray();
    }

    public Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int portfolioId, int fundId, long version, int size, CancellationToken ct = default) =>
        Many<FundTradeTemplateAssignmentReadModel>(nameof(PortfolioDbSql.Fund.Assignments), PortfolioDbSql.Fund.Assignments,
            Values(Pos(portfolioId), Pos(fundId), Positive(version), Page(size)), ct);
    public Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int portfolioId, int fundId, CancellationToken ct = default) =>
        One<FundAllocationReadModel>(nameof(PortfolioDbSql.Fund.Allocation), PortfolioDbSql.Fund.Allocation, Values(Pos(portfolioId), Pos(fundId)), ct);
    public Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int portfolioId, int fundId, CancellationToken ct = default) =>
        One<FundRiskEnvelopeReadModel>(nameof(PortfolioDbSql.Fund.Envelope), PortfolioDbSql.Fund.Envelope, Values(Pos(portfolioId), Pos(fundId)), ct);

    public Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int portfolioId, int fundId, DateOnly month, DateTime beforeUtc, int size, CancellationToken ct = default)
    {
        Utc(beforeUtc);
        return Many<FundOrderProjectionReadModel>(nameof(PortfolioDbSql.Orders.Timeline), PortfolioDbSql.Orders.Timeline,
            Values(Pos(portfolioId), Pos(fundId), month, beforeUtc, Page(size)), ct);
    }
    public Task<FundOrderProjectionReadModel?> GetOrderAsync(int id, CancellationToken ct = default) =>
        One<FundOrderProjectionReadModel>(nameof(PortfolioDbSql.Orders.Get), PortfolioDbSql.Orders.Get, Values(Pos(id)), ct);
    public Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int id, int size, CancellationToken ct = default) =>
        Many<FundOrderTradeProjectionReadModel>(nameof(PortfolioDbSql.Orders.Trades), PortfolioDbSql.Orders.Trades, Values(Pos(id), Page(size)), ct);
    public Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int id, CancellationToken ct = default) =>
        One<FundOrderTradeProjectionReadModel>(nameof(PortfolioDbSql.Orders.Trade), PortfolioDbSql.Orders.Trade, Values(Pos(id)), ct);
    public Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid workflowId, int size, CancellationToken ct = default)
    {
        if (workflowId == Guid.Empty) throw new ArgumentException("WorkflowId is required.", nameof(workflowId));
        return Many<FundCompositionWorkflowProjectionReadModel>(nameof(PortfolioDbSql.Orders.Compositions), PortfolioDbSql.Orders.Compositions, Values(workflowId, Page(size)), ct);
    }

    public Task<PortfolioFinancialPolicyReadModel?> GetPolicyAsync(int id, long? version = null, CancellationToken ct = default) => version is null
        ? One<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.GetCurrent), PortfolioDbSql.Policy.GetCurrent, Values(Pos(id)), ct)
        : One<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.GetVersion), PortfolioDbSql.Policy.GetVersion, Values(Pos(id), Positive(version.Value)), ct);
    public Task<IReadOnlyList<PortfolioFinancialPolicyReadModel>> GetPoliciesAsync(int portfolioId, int size, CancellationToken ct = default) =>
        Many<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.ByPortfolio), PortfolioDbSql.Policy.ByPortfolio, Values(Pos(portfolioId), Page(size)), ct);
    public Task<PortfolioFinancialPolicyReadModel?> GetActivePolicyAsync(int portfolioId, CancellationToken ct = default) =>
        One<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.Active), PortfolioDbSql.Policy.Active, Values(Pos(portfolioId)), ct);

    public Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int bucket, CancellationToken ct = default)
    {
        Check(row); if (bucket < 0) throw new ArgumentOutOfRangeException(nameof(bucket)); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Portfolio.Upsert), PortfolioDbSql.Portfolio.Upsert,
            Values(x.PortfolioId, x.PortfolioVersion, x.OperatingState.ToString(), Common(row), bucket), ct);
    }
    public Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Fund.Upsert), PortfolioDbSql.Fund.Upsert,
            Values(x.PortfolioId, x.FundId, x.FundMandateVersion, x.OperatingState.ToString(), Common(row), x.TradingYear, x.DecisionHorizon, x.EffectiveFromUtc), ct);
    }
    public Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Fund.UpsertAssignment), PortfolioDbSql.Fund.UpsertAssignment,
            Values(x.PortfolioId, x.FundId, x.FundMandateVersion, x.TradeTemplateId, x.TradeTemplateVersion, Common(row)), ct);
    }
    public Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Fund.UpsertAllocation), PortfolioDbSql.Fund.UpsertAllocation, Values(x.PortfolioId, x.FundId, x.AllocationVersion, Common(row)), ct);
    }
    public Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Fund.UpsertEnvelope), PortfolioDbSql.Fund.UpsertEnvelope, Values(x.PortfolioId, x.FundId, x.EnvelopeVersion, Common(row)), ct);
    }
    public Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row, DateOnly month, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Orders.UpsertOrder), PortfolioDbSql.Orders.UpsertOrder,
            Values(x.PortfolioId, x.FundId, month, x.CreatedOnUtc, x.OrderId, x.Status, Common(row)), ct);
    }
    public Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Orders.UpsertTrade), PortfolioDbSql.Orders.UpsertTrade, Values(x.OrderId, x.TradeId, x.PortfolioId, x.FundId, Common(row)), ct);
    }
    public Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Orders.UpsertComposition), PortfolioDbSql.Orders.UpsertComposition,
            Values(x.WorkflowId, x.OrderId, x.PortfolioId, x.FundId, x.Status, Common(row)), ct);
    }

    /// <summary>Deletes an order projection and its subordinate trades when the source event is current or newer.</summary>
    /// <param name="orderId">The canonical order identifier.</param>
    /// <param name="sourceEventId">The committed event sequence authorizing deletion.</param>
    /// <param name="ct">A token that cancels the database operation.</param>
    public Task DeleteOrderAsync(int orderId, long sourceEventId, CancellationToken ct = default)
    {
        if (orderId <= 0 || sourceEventId <= 0) throw new ArgumentOutOfRangeException(nameof(orderId));
        return Put(nameof(PortfolioDbSql.Orders.DeleteOrder), PortfolioDbSql.Orders.DeleteOrder,
            Values(orderId, sourceEventId), ct);
    }
    /// <summary>Deletes a trade projection when the source event is current or newer.</summary>
    /// <param name="tradeId">The canonical trade identifier.</param>
    /// <param name="sourceEventId">The committed event sequence authorizing deletion.</param>
    /// <param name="ct">A token that cancels the database operation.</param>
    public Task DeleteTradeAsync(int tradeId, long sourceEventId, CancellationToken ct = default)
    {
        if (tradeId <= 0 || sourceEventId <= 0) throw new ArgumentOutOfRangeException(nameof(tradeId));
        return Put(nameof(PortfolioDbSql.Orders.DeleteTrade), PortfolioDbSql.Orders.DeleteTrade, Values(tradeId, sourceEventId), ct);
    }
    public Task UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel> row, CancellationToken ct = default)
    {
        Check(row); var x = row.Value;
        return Put(nameof(PortfolioDbSql.Policy.Upsert), PortfolioDbSql.Policy.Upsert,
            Values(x.PolicyId, x.PolicyVersion, x.PortfolioId, x.OperatingState.ToString(), Common(row)), ct);
    }
    public Task DeleteDraftPolicyAsync(DraftPolicyProjectionDeletion deletion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deletion);
        return Put(nameof(PortfolioDbSql.Policy.DeleteDraft), PortfolioDbSql.Policy.DeleteDraft,
            Values(Pos(deletion.PortfolioId), Pos(deletion.PolicyId), Positive(deletion.SourceEventId)), ct);
    }
    public Task DeleteDraftPortfolioAsync(DraftPortfolioProjectionDeletion deletion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(deletion); if (deletion.StateBucket < 0) throw new ArgumentOutOfRangeException(nameof(deletion));
        return Put(nameof(PortfolioDbSql.Portfolio.DeleteDraft), PortfolioDbSql.Portfolio.DeleteDraft,
            Values(Pos(deletion.PortfolioId), Positive(deletion.SourceEventId)), ct);
    }

    async Task<T?> One<T>(string name, string sql, PortfolioParameters values, CancellationToken ct) where T : class =>
        await Use($"{nameof(PortfolioDbSql)}.{name}", sql).SetParameters(values).ExecuteSingleAsync(Map<T>, ct).ConfigureAwait(false);
    async Task<T?> OneValue<T>(string name, string sql, PortfolioParameters values, Func<IObjectDataRecord, T> map, CancellationToken ct) where T : class =>
        await Use($"{nameof(PortfolioDbSql)}.{name}", sql).SetParameters(values).ExecuteSingleAsync(map, ct).ConfigureAwait(false);
    async Task<IReadOnlyList<T>> Many<T>(string name, string sql, PortfolioParameters values, CancellationToken ct) where T : class =>
        [.. await Use($"{nameof(PortfolioDbSql)}.{name}", sql).SetParameters(values).ExecuteQueryAsync(Map<T>, ct).ConfigureAwait(false)];
    Task Put(string name, string sql, PortfolioParameters values, CancellationToken ct) =>
        Use($"{nameof(PortfolioDbSql)}.{name}", sql).SetParameters(values).ExecuteCommandAsync(ct);

    static T Map<T>(IObjectDataRecord row) where T : class => JsonSerializer.Deserialize<T>(row.GetString(0))
        ?? throw new InvalidOperationException($"Stored {typeof(T).Name} payload is invalid.");
    static object?[] Common<T>(PortfolioProjection<T> row) =>
        [row.SchemaVersion, row.AggregateVersion, row.SourceEventId, row.UpdatedOnUtc, new PortfolioJson(JsonSerializer.Serialize(row.Value)), row.PayloadHash];
    static PortfolioParameters Values(params object?[] values) => new([.. Flatten(values).Select(Parameter)]);
    static IEnumerable<object?> Flatten(IEnumerable<object?> values) => values.SelectMany(value => value is object?[] array ? Flatten(array) : [value]);
    static NpgsqlParameter Parameter(object? value) => value is PortfolioJson json
        ? new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = json.Value }
        : new NpgsqlParameter { Value = value ?? DBNull.Value };
    static void Check<T>(PortfolioProjection<T> row)
    {
        ArgumentNullException.ThrowIfNull(row);
        if (row.SchemaVersion <= 0 || row.AggregateVersion <= 0 || row.SourceEventId <= 0 || row.PayloadHash.Length != 64)
            throw new ArgumentException("Projection metadata is invalid.", nameof(row));
        Utc(row.UpdatedOnUtc);
    }
    static int Pos(int value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    static long Positive(long value) => value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    static int Page(int value) => value is >= 1 and <= 4096 ? value : throw new ArgumentOutOfRangeException(nameof(value), "Page size must be 1..4096.");
    static void Utc(DateTime value) { if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Timestamp must be UTC."); }
    readonly record struct PortfolioJson(string Value);

    IPostgresEventTransaction RequiredTransactions() => transactions
        ?? throw new InvalidOperationException("The EventSource PostgreSQL transaction coordinator is required for financial operations.");
}

internal readonly record struct PortfolioParameters(NpgsqlParameter[] Values) : IBindValue { public object Bind() => Values; }
