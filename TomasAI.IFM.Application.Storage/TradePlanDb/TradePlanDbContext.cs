using Microsoft.Extensions.Logging;
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
    private readonly ILogger<DbProvider> tradePlanLogger = logger;

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
    public async Task ProjectMaterialAsync(
        StrategyTradePlanSnapshot plan,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await this
                .WriteTradePlanOnceAsync(plan, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            tradePlanLogger.LogError(exception,
                "{MethodName}: dropping trade plan after persistence failure. PositionId={PositionId} PlanRevision={PlanRevision} ValueDate={ValueDate}",
                nameof(ProjectMaterialAsync), plan.Position.Id, plan.PlanRevision, plan.ValueDate);
        }
    }

    /// <inheritdoc />
    public Task<StrategyTradePlanSnapshot?> GetCurrentAsync(
        StrategyPositionId positionId,
        TradeStrategyKind strategy,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
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
    public async Task ProjectExitWorkflowAsync(
        ExitPositionWorkflowProjection workflow,
        CancellationToken cancellationToken = default)
    {
        workflow.RequireValidExitWorkflow();
        var id = workflow.WorkflowId.Position.Trade;
        var positionId = workflow.WorkflowId.Position.PositionId;
        var payload = MessagePackBinarySerializer.Shared.Serialize(workflow);
        var selectParameters = new GetExactExitPositionWorkflow(
            id.PortfolioId,
            id.FundId,
            id.OrderId,
            id.TradeId,
            positionId,
            workflow.WorkflowId.ValueDate,
            workflow.UpdatedAtUtc,
            workflow.WorkflowId.ExitDecisionId,
            workflow.StageRevision);
        var existing = await Database
            .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectExactExitWorkflow)}", TradePlanDbCql.SelectExactExitWorkflow)
            .SetParameters(selectParameters)
            .ExecuteSingleAsync(MapToPayload, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            if (!existing.AsSpan().SequenceEqual(payload))
            {
                throw new InvalidOperationException(
                    "Exit-workflow projection revision conflicts with a different payload.");
            }

            return;
        }

        var insertParameters = new InsertExitPositionWorkflow(
            id.PortfolioId,
            id.FundId,
            id.OrderId,
            id.TradeId,
            positionId,
            workflow.WorkflowId.ValueDate,
            workflow.UpdatedAtUtc,
            workflow.WorkflowId.ExitDecisionId,
            workflow.StageRevision,
            workflow.StrategyKind.ToCqlTinyInt(),
            workflow.State.ToCqlTinyInt(),
            workflow.SourcePlanEventId,
            payload);
        await Database
            .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.InsertExitWorkflow)}", TradePlanDbCql.InsertExitWorkflow)
            .SetParameters(insertParameters)
            .ExecuteCommandAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsync(
        StrategyPositionId positionId,
        DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
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
