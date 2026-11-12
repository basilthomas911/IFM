using System.Text.Json;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;
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

    /// <inheritdoc/>
    public Task<IReadOnlyList<FinancialWorkflowSourceRow>> ReadFinancialWorkflowSourcesAsync(long afterStreamId, string? exactStream = null, CancellationToken cancellationToken = default) =>
        this.RequiredTransactions.ExecuteAsync((transaction, token) =>
            transaction.QueryAsync(PortfolioDbSql.Financial_FinancialWorkflowRecoveryJournal_With01, [afterStreamId, exactStream], MapToFinancialWorkflowSource, token), cancellationToken);

    internal static (string Hash, TomasAI.IFM.Shared.EventSourcing.ViewModels.EventLogReadModel Event) MapToFinancialOperation(System.Data.Common.DbDataReader reader) =>
        (reader.GetString(0), new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventLogReadModel(
            reader.GetInt64(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4), reader.GetFieldValue<byte[]>(5),
            reader.GetGuid(6), reader.GetString(7), reader.GetInt64(8)));

    internal static FinancialWorkflowSourceRow MapToFinancialWorkflowSource(System.Data.Common.DbDataReader r) =>
        new(r.GetInt64(0), r.IsDBNull(1) ? null :
            new TomasAI.IFM.Shared.EventSourcing.ViewModels.EventLogReadModel(
                r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetFieldValue<byte[]>(4),
                r.GetGuid(5), r.GetString(6), r.GetInt64(7)));

    /// <summary>Gets the Portfolio database connection-setting name.</summary>
    public const string PortfolioDbConnection = "PortfolioDbConnection";

    /// <summary>Gets the concrete Portfolio database context.</summary>
    public override PortfolioDbContext Database => this;

    /// <summary>Gets the Portfolio database read capability.</summary>
    public IPortfolioDbReadContext DbReader => this;

    /// <summary>Gets the Portfolio database write capability.</summary>
    public IPortfolioDbWriteContext DbWriter => this;

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<T?> ReadOperationAsync<T>(int portfolioId, Guid operationId, string? inputHash = null, CancellationToken ct = default)
        where T : class, IFinancialCompletedEvent => this.RequiredTransactions.ExecuteAsync(
            (db, cancellation) => db.ReadFinancialOperationAsync<T>(portfolioId, operationId, inputHash, cancellation), ct);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FinancialBookConfiguration?> ReadBookAsync(int portfolioId, CancellationToken ct = default) =>
        this.RequiredTransactions.ExecuteAsync(async (db, cancellation) =>
        {
            var value = await db
                .ScalarAsync(PortfolioDbSql.Financial_ReadBook, [portfolioId], cancellation)
            .ConfigureAwait(false);
            return value is string json ? json.DecodeFinancialValue<FinancialBookConfiguration>() : null;
        }, ct);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FinancialBookConfiguration?> ReadActiveBookByExecutionAccountAsync(
        string environment,
        string executionAccountReference,
        CancellationToken ct = default)
    {
        return this.RequiredTransactions.ExecuteAsync(async (db, cancellation) =>
        {
            var value = await db
                .ScalarAsync(PortfolioDbSql.Financial_ReadActiveBookByExecutionAccount, [environment, executionAccountReference], cancellation)
            .ConfigureAwait(false);
            return value is string json ? json.DecodeFinancialValue<FinancialBookConfiguration>() : null;
        }, ct);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<PortfolioReadModel?> GetPortfolioAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<PortfolioReadModel>(nameof(PortfolioDbSql.Portfolio_Get), PortfolioDbSql.Portfolio_Get, this.ToParameters(id), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<PortfolioProjectionRevision?> GetPortfolioRevisionAsync(int id, CancellationToken ct = default) =>
        this.ReadOneValueAsync(nameof(PortfolioDbSql.Portfolio_Revision), PortfolioDbSql.Portfolio_Revision, this.ToParameters(id),
            row => new PortfolioProjectionRevision(id, null, row.GetLong(0), row.GetLong(1)), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<PortfolioReadModel>> GetPortfoliosByStateAsync(PortfolioOperatingState state, int? bucket, int afterId, int size, CancellationToken ct = default) =>
        bucket is { } partition
            ? this.ReadManyAsync<PortfolioReadModel>(nameof(PortfolioDbSql.Portfolio_ByState), PortfolioDbSql.Portfolio_ByState, this.ToParameters(state.ToString(), partition, afterId, size), ct)
            : this.ReadManyAsync<PortfolioReadModel>(nameof(PortfolioDbSql.Portfolio_ByOperatingState), PortfolioDbSql.Portfolio_ByOperatingState, this.ToParameters(state.ToString(), afterId, size), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<FundMandateReadModel>> GetFundsByPortfolioAsync(int portfolioId, int afterId, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<FundMandateReadModel>(nameof(PortfolioDbSql.Fund_ByPortfolio), PortfolioDbSql.Fund_ByPortfolio, this.ToParameters(portfolioId, afterId, size), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FundMandateReadModel?> GetFundAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<FundMandateReadModel>(nameof(PortfolioDbSql.Fund_Get), PortfolioDbSql.Fund_Get, this.ToParameters(id), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<PortfolioProjectionRevision?> GetFundRevisionAsync(int id, CancellationToken ct = default) =>
        this.ReadOneValueAsync(nameof(PortfolioDbSql.Fund_Revision), PortfolioDbSql.Fund_Revision, this.ToParameters(id),
            row => new PortfolioProjectionRevision(row.GetInt(0), id, row.GetLong(1), row.GetLong(2)), ct);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<FundMandateReadModel>> GetActiveFundsAsync(int portfolioId, int year, string horizon, DateTime atUtc, int size, CancellationToken ct = default)
    {
        return this.ReadManyAsync<FundMandateReadModel>(nameof(PortfolioDbSql.Fund_Active), PortfolioDbSql.Fund_Active,
            this.ToParameters(portfolioId, year, horizon, atUtc, size), ct);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public async Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetSelectionAssignmentsAsync(int portfolioId, int fundId, long version, string horizon, string root, DateTime asOfUtc, CancellationToken ct = default)
    {
        var rows = await this
            .ReadManyAsync<FundTradeTemplateAssignmentReadModel>(
                nameof(PortfolioDbSql.Fund_Assignments),
                PortfolioDbSql.Fund_Assignments,
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
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<FundTradeTemplateAssignmentReadModel>> GetAssignmentsAsync(int portfolioId, int fundId, long version, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<FundTradeTemplateAssignmentReadModel>(nameof(PortfolioDbSql.Fund_Assignments), PortfolioDbSql.Fund_Assignments,
            this.ToParameters(portfolioId, fundId, version, size), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FundAllocationReadModel?> GetCurrentAllocationAsync(int portfolioId, int fundId, CancellationToken ct = default) =>
        this.ReadOneAsync<FundAllocationReadModel>(nameof(PortfolioDbSql.Fund_Allocation), PortfolioDbSql.Fund_Allocation, this.ToParameters(portfolioId, fundId), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FundRiskEnvelopeReadModel?> GetCurrentRiskEnvelopeAsync(int portfolioId, int fundId, CancellationToken ct = default) =>
        this.ReadOneAsync<FundRiskEnvelopeReadModel>(nameof(PortfolioDbSql.Fund_Envelope), PortfolioDbSql.Fund_Envelope, this.ToParameters(portfolioId, fundId), ct);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<FundOrderProjectionReadModel>> GetOrdersAsync(int portfolioId, int fundId, DateOnly month, DateTime beforeUtc, int size, CancellationToken ct = default)
    {
        return this.ReadManyAsync<FundOrderProjectionReadModel>(nameof(PortfolioDbSql.Orders_Timeline), PortfolioDbSql.Orders_Timeline,
            this.ToParameters(portfolioId, fundId, month, beforeUtc, size), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FundOrderProjectionReadModel?> GetOrderAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<FundOrderProjectionReadModel>(nameof(PortfolioDbSql.Orders_Get), PortfolioDbSql.Orders_Get, this.ToParameters(id), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<FundOrderTradeProjectionReadModel>> GetOrderTradesAsync(int id, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<FundOrderTradeProjectionReadModel>(nameof(PortfolioDbSql.Orders_Trades), PortfolioDbSql.Orders_Trades, this.ToParameters(id, size), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<FundOrderTradeProjectionReadModel?> GetTradeAsync(int id, CancellationToken ct = default) =>
        this.ReadOneAsync<FundOrderTradeProjectionReadModel>(nameof(PortfolioDbSql.Orders_Trade), PortfolioDbSql.Orders_Trade, this.ToParameters(id), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<FundCompositionWorkflowProjectionReadModel>> GetCompositionsAsync(Guid workflowId, int size, CancellationToken ct = default)
    {
        return this.ReadManyAsync<FundCompositionWorkflowProjectionReadModel>(nameof(PortfolioDbSql.Orders_Compositions), PortfolioDbSql.Orders_Compositions, this.ToParameters(workflowId, size), ct);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<PortfolioFinancialPolicyReadModel?> GetPolicyAsync(int id, long? version = null, CancellationToken ct = default) => version is null
        ? this.ReadOneAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy_GetCurrent), PortfolioDbSql.Policy_GetCurrent, this.ToParameters(id), ct)
        : this.ReadOneAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy_GetVersion), PortfolioDbSql.Policy_GetVersion, this.ToParameters(id, version.Value), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<IReadOnlyList<PortfolioFinancialPolicyReadModel>> GetPoliciesAsync(int portfolioId, int size, CancellationToken ct = default) =>
        this.ReadManyAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy_ByPortfolio), PortfolioDbSql.Policy_ByPortfolio, this.ToParameters(portfolioId, size), ct);
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task<PortfolioFinancialPolicyReadModel?> GetActivePolicyAsync(int portfolioId, CancellationToken ct = default) =>
        this.ReadOneAsync<PortfolioFinancialPolicyReadModel>(nameof(PortfolioDbSql.Policy_Active), PortfolioDbSql.Policy_Active, this.ToParameters(portfolioId), ct);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertPortfolioAsync(PortfolioProjection<PortfolioReadModel> row, int bucket, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Portfolio_Upsert), PortfolioDbSql.Portfolio_Upsert,
            this.ToParameters(x.PortfolioId, x.PortfolioVersion, x.OperatingState.ToString(), row.ToCommonValues(), bucket), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertFundAsync(PortfolioProjection<FundMandateReadModel> row, CancellationToken ct = default)
    {
        return this.RequiredTransactions.ExecuteAsync(async (db, token) =>
        {
            await db.UpsertPreparedFundProjectionAsync(row, token)
            .ConfigureAwait(false);
            return true;
        }, ct);
    }
    internal static FundMandateReadModel MapToLockedFundProjection(System.Data.Common.DbDataReader reader)
        => JsonSerializer.Deserialize<FundMandateReadModel>(reader.GetString(0))
            ?? throw new InvalidDataException("Persisted Fund projection payload is empty.");

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertAssignmentAsync(PortfolioProjection<FundTradeTemplateAssignmentReadModel> row, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Fund_UpsertAssignment), PortfolioDbSql.Fund_UpsertAssignment,
            this.ToParameters(x.PortfolioId, x.FundId, x.FundMandateVersion, x.TradeTemplateId, x.TradeTemplateVersion, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertAllocationAsync(PortfolioProjection<FundAllocationReadModel> row, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Fund_UpsertAllocation), PortfolioDbSql.Fund_UpsertAllocation, this.ToParameters(x.PortfolioId, x.FundId, x.AllocationVersion, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertRiskEnvelopeAsync(PortfolioProjection<FundRiskEnvelopeReadModel> row, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Fund_UpsertEnvelope), PortfolioDbSql.Fund_UpsertEnvelope, this.ToParameters(x.PortfolioId, x.FundId, x.EnvelopeVersion, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertOrderAsync(PortfolioProjection<FundOrderProjectionReadModel> row, DateOnly month, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Orders_UpsertOrder), PortfolioDbSql.Orders_UpsertOrder,
            this.ToParameters(x.PortfolioId, x.FundId, month, x.CreatedOnUtc, x.OrderId, x.Status, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertTradeAsync(PortfolioProjection<FundOrderTradeProjectionReadModel> row, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Orders_UpsertTrade), PortfolioDbSql.Orders_UpsertTrade, this.ToParameters(x.OrderId, x.TradeId, x.PortfolioId, x.FundId, row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertCompositionAsync(PortfolioProjection<FundCompositionWorkflowProjectionReadModel> row, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Orders_UpsertComposition), PortfolioDbSql.Orders_UpsertComposition,
            this.ToParameters(x.WorkflowId, x.OrderId, x.PortfolioId, x.FundId, x.Status, row.ToCommonValues()), ct);
    }

    /// <summary>Deletes an order projection and its subordinate trades when the source event is current or newer.</summary>
    /// <param name="orderId">The canonical order identifier.</param>
    /// <param name="sourceEventId">The committed event sequence authorizing deletion.</param>
    /// <param name="ct">A token that cancels the database operation.</param>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task DeleteOrderAsync(int orderId, long sourceEventId, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Orders_DeleteOrder), PortfolioDbSql.Orders_DeleteOrder,
            this.ToParameters(orderId, sourceEventId), ct);
    }
    /// <summary>Deletes a trade projection when the source event is current or newer.</summary>
    /// <param name="tradeId">The canonical trade identifier.</param>
    /// <param name="sourceEventId">The committed event sequence authorizing deletion.</param>
    /// <param name="ct">A token that cancels the database operation.</param>
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    public Task DeleteTradeAsync(int tradeId, long sourceEventId, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Orders_DeleteTrade), PortfolioDbSql.Orders_DeleteTrade, this.ToParameters(tradeId, sourceEventId), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task UpsertPolicyAsync(PortfolioProjection<PortfolioFinancialPolicyReadModel> row, CancellationToken ct = default)
    {
        var x = row.RequiredValue();
        return this.WriteAsync(nameof(PortfolioDbSql.Policy_Upsert), PortfolioDbSql.Policy_Upsert,
            this.ToParameters(x.PolicyId, x.PolicyVersion, x.PortfolioId, x.OperatingState.ToString(), row.ToCommonValues()), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task DeleteDraftPolicyAsync(DraftPolicyProjectionDeletion deletion, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Policy_DeleteDraft), PortfolioDbSql.Policy_DeleteDraft,
            this.ToParameters(deletion.ToDeletionValues()), ct);
    }
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1725", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S927", Justification = "Preserve the established storage context contract, including public named arguments and default values used by callers.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1062", Justification = "The typed extension validates the projection or deletion before dereferencing it; the DbContext delegates parameter validation to its binding extensions.")]
    public Task DeleteDraftPortfolioAsync(DraftPortfolioProjectionDeletion deletion, CancellationToken ct = default)
    {
        return this.WriteAsync(nameof(PortfolioDbSql.Portfolio_DeleteDraft), PortfolioDbSql.Portfolio_DeleteDraft,
            this.ToParameters(deletion.ToDeletionValues()), ct);
    }

    /// <summary>Maps the JSON payload at ordinal zero to its typed projection model.</summary>
    internal static T MapToModel<T>(IObjectDataRecord row) where T : class => JsonSerializer.Deserialize<T>(row.GetString(0))
        ?? throw new InvalidOperationException($"Stored {typeof(T).Name} payload is invalid.");

    /// <inheritdoc />
    public Task SaveFinancialHistoryAsync(int portfolioId, int month, long revision, Guid operationId, long eventId,
        string eventType, DateTime committedAtUtc, string payload, CancellationToken cancellationToken = default)
        => this.WriteAsync("PortfolioFinancial.ProjectHistory", PortfolioDbSql.Financial_FinancialHistoryProjection_Insert01,
            this.ToParameters(portfolioId, month, revision, operationId, eventId, eventType, committedAtUtc, new PortfolioJson(payload)), cancellationToken);
}
