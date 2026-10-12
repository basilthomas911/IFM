using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>
/// Provides read and write persistence operations for Trade Plan projections.
/// </summary>
/// <param name="connectionSettings">The named database connection settings.</param>
/// <param name="logger">The database-provider logger.</param>
public sealed class TradePlanDbContext(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<TradePlanDbContext>(
        connectionSettings[TradePlanDbConnection],
        logger),
      ITradePlanDbContext
{
    internal readonly ILogger<DbProvider> tradePlanLogger = logger;
    internal readonly PostgresTradePlanStore? postgres = string.Equals(connectionSettings[TradePlanDbConnection].ProviderName, "System.Data.Postgres", StringComparison.Ordinal) ? new PostgresTradePlanStore(connectionSettings) : null;

    /// <summary>Writes all required plan rows on the transaction that owns the source event append.</summary>
    public Task ProjectMaterialAsync(IEnlistedPostgresTransaction transaction, StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default)
        => (postgres ?? throw new InvalidOperationException("Enlisted plan writes require PostgreSQL."))
            .ProjectMaterialAsync(transaction, plan, cancellationToken);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Disposable monitoring snapshots are logged and dropped on any persistence failure, as required; the next market update calculates a new snapshot.")]
    public Task ProjectMaterialAsync(
        StrategyTradePlanSnapshot plan,
        CancellationToken cancellationToken = default)
=>this.ProjectMaterialAsyncCoreBinding(plan, cancellationToken);
    /// <summary>Writes an exit workflow on its source event transaction.</summary>
    public Task ProjectExitWorkflowAsync(IEnlistedPostgresTransaction transaction, ExitPositionWorkflowProjection workflow, CancellationToken cancellationToken = default)
        => (postgres ?? throw new InvalidOperationException("Enlisted workflow writes require PostgreSQL."))
            .ProjectExitWorkflowAsync(transaction, workflow, cancellationToken);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
    public Task ProjectExitWorkflowAsync(
        ExitPositionWorkflowProjection workflow,
        CancellationToken cancellationToken = default)
=>this.ProjectExitWorkflowAsyncCoreBinding(workflow, cancellationToken);


    /// <summary>Gets the Trade Plan database connection-setting name.</summary>
    public const string TradePlanDbConnection = "TradePlanDbConnection";

    /// <summary>Gets the concrete Trade Plan database context.</summary>
    public override TradePlanDbContext Database => this;

    /// <summary>Gets the Trade Plan read capability.</summary>
    public ITradePlanDbReadContext DbReader => this;

    /// <summary>Gets the Trade Plan write capability.</summary>
    public ITradePlanDbWriteContext DbWriter => this;

    internal static string MapToContentHash<TDataRecord>(TDataRecord record)
        where TDataRecord : IObjectDataRecord
        => record.GetString(0);

    internal static byte[] MapToPayload<TDataRecord>(TDataRecord record)
        where TDataRecord : IObjectDataRecord
        => record.GetBytes(0);

    internal static StrategyTradePlanSnapshot MapToStrategyTradePlan<TDataRecord>(
        TDataRecord record)
        where TDataRecord : IObjectDataRecord
        => MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(
            record.GetBytes(0));

    internal static ExitPositionWorkflowProjection MapToExitPositionWorkflow<TDataRecord>(
        TDataRecord record)
        where TDataRecord : IObjectDataRecord
        => MessagePackBinarySerializer.Shared.Deserialize<ExitPositionWorkflowProjection>(
            record.GetBytes(0));

    /// <inheritdoc />
    public Task<StrategyTradePlanSnapshot?> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        if (postgres is not null) return postgres.GetCurrentAsync(positionId, strategy, valueDate, cancellationToken);
        positionId.RequireTradePlanScope(strategy, valueDate);
        var id = positionId.Trade;
        var statement = TradePlanDbCql.SelectCurrentPlan.ForTable(
            strategy.ToTradePlanTable());
        var parameters = new GetTradePlan(
            id.PortfolioId,
            id.FundId,
            id.OrderId,
            id.TradeId,
            positionId.PositionId,
            valueDate);
        return Database
            .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectCurrentPlan)}", statement)
            .SetParameters(parameters)
            .ExecuteSingleAsync(MapToStrategyTradePlan, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        if (postgres is not null) return postgres.GetHistoryAsync(positionId, strategy, valueDate, pageSize, pagingState, cancellationToken);
        positionId.RequireTradePlanScope(strategy, valueDate);
        pageSize.RequireTradePlanPageSize();
        var id = positionId.Trade;
        var statement = TradePlanDbCql.SelectPlanHistory.ForTable(
            strategy.ToTradePlanTable());
        var parameters = new GetTradePlan(
            id.PortfolioId,
            id.FundId,
            id.OrderId,
            id.TradeId,
            positionId.PositionId,
            valueDate);
        return Database
            .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectPlanHistory)}", statement)
            .SetParameters(parameters)
            .ExecutePageAsync(
                MapToStrategyTradePlan,
                pageSize,
                pagingState,
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsync(
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        if (postgres is not null) return postgres.GetActivityAsync(valueDate, pageSize, pagingState, cancellationToken);
        valueDate.RequireTradePlanValueDate();
        pageSize.RequireTradePlanPageSize();
        var parameters = new ValueDateParameter(valueDate);
        return Database
            .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectActivity)}", TradePlanDbCql.SelectActivity)
            .SetParameters(parameters)
            .ExecutePageAsync(
                MapToStrategyTradePlan,
                pageSize,
                pagingState,
                cancellationToken);
    }

    /// <inheritdoc />
    public Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsync(
        StrategyPositionId positionId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        if (postgres is not null) return postgres.GetCurrentExitWorkflowAsync(positionId, valueDate, cancellationToken);
        positionId.RequireValueDate(valueDate);
        var id = positionId.Trade;
        var parameters = new GetExitPositionWorkflow(
            id.PortfolioId,
            id.FundId,
            id.OrderId,
            id.TradeId,
            positionId.PositionId,
            valueDate);
        return Database
            .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectCurrentExitWorkflow)}", TradePlanDbCql.SelectCurrentExitWorkflow)
            .SetParameters(parameters)
            .ExecuteSingleAsync(MapToExitPositionWorkflow, cancellationToken);
    }

    /// <inheritdoc />
    public Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsync(
        StrategyPositionId positionId,
        DateOnly valueDate,
        int pageSize,
        byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        if (postgres is not null) return postgres.GetExitWorkflowTimelineAsync(positionId, valueDate, pageSize, pagingState, cancellationToken);
        positionId.RequireValueDate(valueDate);
        pageSize.RequireTradePlanPageSize();
        var id = positionId.Trade;
        var parameters = new GetExitPositionWorkflow(
            id.PortfolioId,
            id.FundId,
            id.OrderId,
            id.TradeId,
            positionId.PositionId,
            valueDate);
        return Database
            .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectExitWorkflowTimeline)}", TradePlanDbCql.SelectExitWorkflowTimeline)
            .SetParameters(parameters)
            .ExecutePageAsync(
                MapToExitPositionWorkflow,
                pageSize,
                pagingState,
                cancellationToken);
    }
}
