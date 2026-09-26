using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.PortfolioDb;

/// <summary>Provides read and write persistence operations for Portfolio domain data.</summary>
/// <param name="settings">The named database connection settings.</param>
/// <param name="logger">The database-provider logger.</param>
/// <param name="transactions">The optional PostgreSQL event transaction coordinator.</param>
public sealed class PortfolioDbContext(IDbConnectionSettings settings, ILogger<DbProvider> logger,
    IPostgresEventTransaction? transactions = null)
    : ObjectDataRepository<PortfolioDbContext>(settings[PortfolioDbConnection], logger),
      IPortfolioDbContext
{
    internal readonly IPostgresEventTransaction? _transactions = transactions;

    /// <summary>Gets the Portfolio database connection-setting name.</summary>
    public const string PortfolioDbConnection = "PortfolioDbConnection";

    /// <summary>Gets the concrete Portfolio database context.</summary>
    public override PortfolioDbContext Database => this;

    /// <summary>Gets the Portfolio database read capability.</summary>
    public IPortfolioDbReadContext DbReader => this;

    /// <summary>Gets the Portfolio database write capability.</summary>
    public IPortfolioDbWriteContext DbWriter => this;

    /// <inheritdoc />
    public Task<T?> ReadOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash = null, CancellationToken ct = default)
        where T : class, IFinancialCompletedEvent => this.RequiredTransactions.ExecuteAsync(
            (db, cancellation) => PortfolioDbFinancialSupport.ReadOperationAsync<T>(db, portfolioId, operationId, inputHash, cancellation), ct);

    /// <inheritdoc />
    public Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId, CancellationToken ct = default) =>
        this.RequiredTransactions.ExecuteAsync(async (db, cancellation) =>
        {
            var value = await db
                .ScalarAsync(PortfolioDbSql.Financial.ReadBook, [portfolioId], cancellation)
                .ConfigureAwait(false);
            return value is string json ? PortfolioDbFinancialSupport.Decode<FinancialBookConfiguration>(json) : null;
        }, ct);

    /// <inheritdoc />
    public Task<FinancialBookConfiguration?> ReadActiveBookByExecutionAccountAsync(
        string environment,
        string executionAccountReference,
        CancellationToken ct = default)
    {
        return this.RequiredTransactions.ExecuteAsync(async (db, cancellation) =>
        {
            var value = await db
                .ScalarAsync(
                    PortfolioDbSql.Financial.ReadActiveBookByExecutionAccount,
                    [environment, executionAccountReference],
                    cancellation)
                .ConfigureAwait(false);
            return value is string json ? PortfolioDbFinancialSupport.Decode<FinancialBookConfiguration>(json) : null;
        }, ct);
    }

    /// <inheritdoc />
    public Task<PortfolioReadModel?> GetPortfolioAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<PortfolioReadModel>(nameof(PortfolioDbSql.Portfolio.Get), PortfolioDbSql.Portfolio.Get, this.ToParameters(id), ct);
    /// <inheritdoc />
    public Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int id, CancellationToken ct = default) =>
        this.ReadOneValueAsync(nameof(PortfolioDbSql.Portfolio.Revision), PortfolioDbSql.Portfolio.Revision, this.ToParameters(id),
            row => new PortfolioProjectionRevision(id, null, row.GetLong(0), row.GetLong(1)), ct);
    /// <inheritdoc />
    public Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState state, int bucket, int afterId, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<PortfolioReadModel>(nameof(PortfolioDbSql.Portfolio.ByState), PortfolioDbSql.Portfolio.ByState, this.ToParameters(state.ToString(), bucket, afterId, size), ct);
    /// <inheritdoc />
    public Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int portfolioId, int afterId, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<FundMandateReadModel>(nameof(PortfolioDbSql.Fund.ByPortfolio), PortfolioDbSql.Fund.ByPortfolio, this.ToParameters(portfolioId, afterId, size), ct);
    /// <inheritdoc />
    public Task<FundMandateReadModel?> GetFundAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<FundMandateReadModel>(nameof(PortfolioDbSql.Fund.Get), PortfolioDbSql.Fund.Get, this.ToParameters(id), ct);
    /// <inheritdoc />
    public Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int id, CancellationToken ct = default) =>
        this.ReadOneValueAsync(nameof(PortfolioDbSql.Fund.Revision), PortfolioDbSql.Fund.Revision, this.ToParameters(id),
            row => new PortfolioProjectionRevision(row.GetInt(0), id, row.GetLong(1), row.GetLong(2)), ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int portfolioId, int year, string horizon, DateTime atUtc, int size, CancellationToken ct = default)
    {
        return this.ReadManyAsync<FundMandateReadModel>(nameof(PortfolioDbSql.Fund.Active), PortfolioDbSql.Fund.Active,
            this.ToParameters(portfolioId, year, horizon, atUtc, size), ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetSelectionAssignmentsAsync(int portfolioId, int fundId, long version, string horizon, string root, DateTime asOfUtc, CancellationToken ct = default)
    {
        var rows = await this
            .ReadManyAsync<FundTradeTemplateAssignmentReadModel>(
                nameof(PortfolioDbSql.Fund.Assignments),
                PortfolioDbSql.Fund.Assignments,
                this.ToParameters(portfolioId, fundId, version, 4096),
                ct)
            .ConfigureAwait(false);
        if (rows.Count == 4096)
            throw new InvalidOperationException("Selection assignment partition reached the 4096-row historical scan budget; no truncated candidates returned.");
        return rows.Where(x => x.EffectiveFromUtc <= asOfUtc && !(x.EffectiveUntilUtc <= asOfUtc)
                && x.DecisionHorizon.Equals(horizon, StringComparison.OrdinalIgnoreCase)
                && x.UnderlyingUniverse.Contains(root, StringComparer.OrdinalIgnoreCase)).Take(17).ToArray();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int portfolioId, int fundId, long version, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<FundTradeTemplateAssignmentReadModel>(nameof(PortfolioDbSql.Fund.Assignments), PortfolioDbSql.Fund.Assignments,
            this.ToParameters(portfolioId, fundId, version, size), ct);
    /// <inheritdoc />
    public Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int portfolioId, int fundId, CancellationToken ct = default) =>
        this.ReadOneAsync<FundAllocationReadModel>(nameof(PortfolioDbSql.Fund.Allocation), PortfolioDbSql.Fund.Allocation, this.ToParameters(portfolioId, fundId), ct);
    /// <inheritdoc />
    public Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int portfolioId, int fundId, CancellationToken ct = default) =>
        this.ReadOneAsync<FundRiskEnvelopeReadModel>(nameof(PortfolioDbSql.Fund.Envelope), PortfolioDbSql.Fund.Envelope, this.ToParameters(portfolioId, fundId), ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int portfolioId, int fundId, DateOnly month, DateTime beforeUtc, int size, CancellationToken ct = default)
    {
        return this.ReadManyAsync<FundOrderProjectionReadModel>(nameof(PortfolioDbSql.Orders.Timeline), PortfolioDbSql.Orders.Timeline,
            this.ToParameters(portfolioId, fundId, month, beforeUtc, size), ct);
    }
    /// <inheritdoc />
    public Task<FundOrderProjectionReadModel?> GetOrderAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<FundOrderProjectionReadModel>(nameof(PortfolioDbSql.Orders.Get), PortfolioDbSql.Orders.Get, this.ToParameters(id), ct);
    /// <inheritdoc />
    public Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int id, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<FundOrderTradeProjectionReadModel>(nameof(PortfolioDbSql.Orders.Trades), PortfolioDbSql.Orders.Trades, this.ToParameters(id, size), ct);
    /// <inheritdoc />
    public Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<FundOrderTradeProjectionReadModel>(nameof(PortfolioDbSql.Orders.Trade), PortfolioDbSql.Orders.Trade, this.ToParameters(id), ct);
    /// <inheritdoc />
    public Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid workflowId, int size, CancellationToken ct = default)
    {
        return this.ReadManyAsync<FundCompositionWorkflowProjectionReadModel>(nameof(PortfolioDbSql.Orders.Compositions), PortfolioDbSql.Orders.Compositions, this.ToParameters(workflowId, size), ct);
    }

    /// <inheritdoc />
    public Task<PortfolioFinancialPolicyReadModel?> GetPolicyAsync(int id, long? version = null, CancellationToken ct = default) => version is null
        ? this.ReadOneAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.GetCurrent), PortfolioDbSql.Policy.GetCurrent, this.ToParameters(id), ct)
        : this.ReadOneAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.GetVersion), PortfolioDbSql.Policy.GetVersion, this.ToParameters(id, version.Value), ct);
    /// <inheritdoc />
    public Task<IReadOnlyList<PortfolioFinancialPolicyReadModel>> GetPoliciesAsync(int portfolioId, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.ByPortfolio), PortfolioDbSql.Policy.ByPortfolio, this.ToParameters(portfolioId, size), ct);
    /// <inheritdoc />
    public Task<PortfolioFinancialPolicyReadModel?> GetActivePolicyAsync(int portfolioId, CancellationToken ct = default) =>
        this.ReadOneAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy.Active), PortfolioDbSql.Policy.Active, this.ToParameters(portfolioId), ct);

    /// <inheritdoc />
    public Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int bucket, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Portfolio.Upsert), PortfolioDbSql.Portfolio.Upsert,
            this.ToParameters(x.PortfolioId, x.PortfolioVersion, x.OperatingState.ToString(), row.ToCommonValues(), bucket), ct);
    }
    /// <inheritdoc />
    public Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Fund.Upsert), PortfolioDbSql.Fund.Upsert,
            this.ToParameters(x.PortfolioId, x.FundId, x.FundMandateVersion, x.OperatingState.ToString(), row.ToCommonValues(), x.TradingYear, x.DecisionHorizon, x.EffectiveFromUtc), ct);
    }
    /// <inheritdoc />
    public Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Fund.UpsertAssignment), PortfolioDbSql.Fund.UpsertAssignment,
            this.ToParameters(x.PortfolioId, x.FundId, x.FundMandateVersion, x.TradeTemplateId, x.TradeTemplateVersion, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    public Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Fund.UpsertAllocation), PortfolioDbSql.Fund.UpsertAllocation, this.ToParameters(x.PortfolioId, x.FundId, x.AllocationVersion, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    public Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Fund.UpsertEnvelope), PortfolioDbSql.Fund.UpsertEnvelope, this.ToParameters(x.PortfolioId, x.FundId, x.EnvelopeVersion, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    public Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row, DateOnly month, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Orders.UpsertOrder), PortfolioDbSql.Orders.UpsertOrder,
            this.ToParameters(x.PortfolioId, x.FundId, month, x.CreatedOnUtc, x.OrderId, x.Status, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    public Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Orders.UpsertTrade), PortfolioDbSql.Orders.UpsertTrade, this.ToParameters(x.OrderId, x.TradeId, x.PortfolioId, x.FundId, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    public Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Orders.UpsertComposition), PortfolioDbSql.Orders.UpsertComposition,
            this.ToParameters(x.WorkflowId, x.OrderId, x.PortfolioId, x.FundId, x.Status, row.ToCommonValues()), ct);
    }

    /// <summary>Deletes an order projection and its subordinate trades when the source event is current or newer.</summary>
    /// <param name="orderId">The canonical order identifier.</param>
    /// <param name="sourceEventId">The committed event sequence authorizing deletion.</param>
    /// <param name="ct">A token that cancels the database operation.</param>
    /// <inheritdoc />
    public Task DeleteOrderAsync(int orderId, long sourceEventId, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Orders.DeleteOrder), PortfolioDbSql.Orders.DeleteOrder,
            this.ToParameters(orderId, sourceEventId), ct);
    }
    /// <summary>Deletes a trade projection when the source event is current or newer.</summary>
    /// <param name="tradeId">The canonical trade identifier.</param>
    /// <param name="sourceEventId">The committed event sequence authorizing deletion.</param>
    /// <param name="ct">A token that cancels the database operation.</param>
    /// <inheritdoc />
    public Task DeleteTradeAsync(int tradeId, long sourceEventId, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Orders.DeleteTrade), PortfolioDbSql.Orders.DeleteTrade, this.ToParameters(tradeId, sourceEventId), ct);
    }
    /// <inheritdoc />
    public Task UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel> row, CancellationToken ct = default)
    {
        var x = row.Value;
        return this.WriteAsync(nameof(PortfolioDbSql.Policy.Upsert), PortfolioDbSql.Policy.Upsert,
            this.ToParameters(x.PolicyId, x.PolicyVersion, x.PortfolioId, x.OperatingState.ToString(), row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    public Task DeleteDraftPolicyAsync(DraftPolicyProjectionDeletion deletion, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Policy.DeleteDraft), PortfolioDbSql.Policy.DeleteDraft,
            this.ToParameters(deletion.PortfolioId, deletion.PolicyId, deletion.SourceEventId), ct);
    }
    /// <inheritdoc />
    public Task DeleteDraftPortfolioAsync(DraftPortfolioProjectionDeletion deletion, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Portfolio.DeleteDraft), PortfolioDbSql.Portfolio.DeleteDraft,
            this.ToParameters(deletion.PortfolioId, deletion.SourceEventId), ct);
    }

    internal static T MapToModel<T>(IObjectDataRecord row) where T : class => JsonSerializer.Deserialize<T>(row.GetString(0))
        ?? throw new InvalidOperationException($"Stored {typeof(T).Name} payload is invalid.");
}
