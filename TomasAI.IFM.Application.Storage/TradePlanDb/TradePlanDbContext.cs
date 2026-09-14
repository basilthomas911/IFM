using Microsoft.Extensions.Logging;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

public sealed class TradePlanDbContext(
    IDbConnectionSettings connectionSettings,
    ILogger<DbProvider> logger)
    : ObjectDataRepository<TradePlanDbContext>(
        connectionSettings[Application.Storage.TradeDb.TradeDbContext.TradeDbConnection], logger),
      ITradePlanDbContext
{
    public override TradePlanDbContext Database => this;
    public ITradePlanDbReadContext DbReader => this;
    public ITradePlanDbWriteContext DbWriter => this;

    public async Task ProjectMaterialAsync(StrategyTradePlanSnapshot plan,
        CancellationToken cancellationToken = default)
    {
        Validate(plan.Position.Id, plan.Position.StrategyKind, plan.ValueDate);
        if (!plan.MaterialChange) return;
        var table = Table(plan.Position.StrategyKind);
        var id = plan.Position.Id.Trade;
        var existing = await this.Use("TradePlan.Exact",
                TradePlanDbCql.ForTable(TradePlanDbCql.SelectExactPlan, table))
            .SetParameters(new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId,
                plan.Position.Id.PositionId, plan.ValueDate, plan.PlanRevision]))
            .ExecuteSingleAsync(static row => row.GetString(0), cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!string.Equals(existing, plan.ContentHash, StringComparison.Ordinal))
                throw new InvalidOperationException("Trade Plan projection revision conflicts with a different content hash.");
            await ProjectActivityAsync(plan, MessagePackBinarySerializer.Shared.Serialize(plan),
                cancellationToken).ConfigureAwait(false);
            return;
        }
        var payload = MessagePackBinarySerializer.Shared.Serialize(plan);
        await this.Use("TradePlan.Insert",
                TradePlanDbCql.ForTable(TradePlanDbCql.InsertPlan, table))
            .SetParameters(new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId,
                plan.Position.Id.PositionId, plan.ValueDate, plan.PlanRevision, plan.CalculatedAtUtc,
                plan.State.ToString(), plan.RequiresExit, plan.ContentHash,
                payload]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
        await ProjectActivityAsync(plan, payload, cancellationToken).ConfigureAwait(false);
    }

    async Task ProjectActivityAsync(StrategyTradePlanSnapshot plan, byte[] payload,
        CancellationToken cancellationToken)
    {
        var id = plan.Position.Id.Trade;
        await this.Use("TradePlan.Activity.Insert", TradePlanDbCql.InsertActivity)
            .SetParameters(new Values([plan.ValueDate, plan.CalculatedAtUtc, id.PortfolioId,
                id.FundId, id.OrderId, id.TradeId, plan.Position.Id.PositionId,
                ToCqlTinyInt(plan.Position.StrategyKind), plan.PlanRevision, ToCqlTinyInt(plan.State),
                plan.RequiresExit, plan.ContentHash, payload]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<StrategyTradePlanSnapshot?> GetCurrentAsync(StrategyPositionId positionId,
        TradeStrategyKind strategy, DateOnly valueDate, CancellationToken cancellationToken = default)
    {
        Validate(positionId, strategy, valueDate);
        var id = positionId.Trade;
        return this.Use("TradePlan.Current",
                TradePlanDbCql.ForTable(TradePlanDbCql.SelectCurrentPlan, Table(strategy)))
            .SetParameters(new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId,
                positionId.PositionId, valueDate]))
            .ExecuteSingleAsync(static row => MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(row.GetBytes(0)), cancellationToken);
    }

    public Task<QueryPage<StrategyTradePlanSnapshot>> GetHistoryAsync(StrategyPositionId positionId,
        TradeStrategyKind strategy, DateOnly valueDate, int pageSize, byte[]? pagingState = null,
        CancellationToken cancellationToken = default)
    {
        Validate(positionId, strategy, valueDate);
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
        var id = positionId.Trade;
        return this.Use("TradePlan.History",
                TradePlanDbCql.ForTable(TradePlanDbCql.SelectPlanHistory, Table(strategy)))
            .SetParameters(new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId,
                positionId.PositionId, valueDate]))
            .ExecutePageAsync(static row => MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(row.GetBytes(0)),
                pageSize, pagingState, cancellationToken);
    }

    public Task<QueryPage<StrategyTradePlanSnapshot>> GetActivityAsync(DateOnly valueDate,
        int pageSize, byte[]? pagingState = null, CancellationToken cancellationToken = default)
    {
        if (valueDate == default) throw new ArgumentException("A value date is required.", nameof(valueDate));
        ValidatePageSize(pageSize);
        return this.Use("TradePlan.Activity", TradePlanDbCql.SelectActivity)
            .SetParameters(new Values([valueDate]))
            .ExecutePageAsync(static row =>
                    MessagePackBinarySerializer.Shared.Deserialize<StrategyTradePlanSnapshot>(row.GetBytes(0)),
                pageSize, pagingState, cancellationToken);
    }

    public async Task ProjectExitWorkflowAsync(ExitPositionWorkflowProjection workflow,
        CancellationToken cancellationToken = default)
    {
        ValidateExitWorkflow(workflow);
        var id = workflow.WorkflowId.Position.Trade;
        var positionId = workflow.WorkflowId.Position.PositionId;
        var payload = MessagePackBinarySerializer.Shared.Serialize(workflow);
        var key = new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId, positionId,
            workflow.WorkflowId.ValueDate, workflow.UpdatedAtUtc,
            workflow.WorkflowId.ExitDecisionId, workflow.StageRevision]);
        var existing = await this.Use("TradePlan.ExitWorkflow.Exact",
                TradePlanDbCql.SelectExactExitWorkflow)
            .SetParameters(key)
            .ExecuteSingleAsync(static row => row.GetBytes(0), cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            if (!existing.AsSpan().SequenceEqual(payload))
                throw new InvalidOperationException(
                    "Exit-workflow projection revision conflicts with a different payload.");
            return;
        }

        await this.Use("TradePlan.ExitWorkflow.Insert", TradePlanDbCql.InsertExitWorkflow)
            .SetParameters(new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId,
                positionId, workflow.WorkflowId.ValueDate, workflow.UpdatedAtUtc,
                workflow.WorkflowId.ExitDecisionId, workflow.StageRevision,
                ToCqlTinyInt(workflow.StrategyKind), ToCqlTinyInt(workflow.State),
                workflow.SourcePlanEventId, payload]))
            .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<ExitPositionWorkflowProjection?> GetCurrentExitWorkflowAsync(
        StrategyPositionId positionId, DateOnly valueDate,
        CancellationToken cancellationToken = default)
    {
        ValidatePosition(positionId, valueDate);
        var id = positionId.Trade;
        return this.Use("TradePlan.ExitWorkflow.Current", TradePlanDbCql.SelectCurrentExitWorkflow)
            .SetParameters(new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId,
                positionId.PositionId, valueDate]))
            .ExecuteSingleAsync(static row =>
                MessagePackBinarySerializer.Shared.Deserialize<ExitPositionWorkflowProjection>(
                    row.GetBytes(0)), cancellationToken);
    }

    public Task<QueryPage<ExitPositionWorkflowProjection>> GetExitWorkflowTimelineAsync(
        StrategyPositionId positionId, DateOnly valueDate, int pageSize,
        byte[]? pagingState = null, CancellationToken cancellationToken = default)
    {
        ValidatePosition(positionId, valueDate);
        ValidatePageSize(pageSize);
        var id = positionId.Trade;
        return this.Use("TradePlan.ExitWorkflow.Timeline", TradePlanDbCql.SelectExitWorkflowTimeline)
            .SetParameters(new Values([id.PortfolioId, id.FundId, id.OrderId, id.TradeId,
                positionId.PositionId, valueDate]))
            .ExecutePageAsync(static row =>
                    MessagePackBinarySerializer.Shared.Deserialize<ExitPositionWorkflowProjection>(
                        row.GetBytes(0)),
                pageSize, pagingState, cancellationToken);
    }

    static string Table(TradeStrategyKind strategy) => strategy switch
    {
        TradeStrategyKind.IronCondor => "iron_condor_trade_plan_v1",
        TradeStrategyKind.VerticalSpread => "vertical_spread_trade_plan_v1",
        TradeStrategyKind.FuturesOutright => "futures_trade_plan_v1",
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unsupported Trade Plan strategy.")
    };

    static void Validate(StrategyPositionId id, TradeStrategyKind strategy, DateOnly valueDate)
    {
        if (!id.IsValid || valueDate == default || strategy is not (TradeStrategyKind.IronCondor or
            TradeStrategyKind.VerticalSpread or TradeStrategyKind.FuturesOutright))
            throw new ArgumentException("Valid Trade Plan identity, strategy, and value date are required.");
    }

    static void ValidatePosition(StrategyPositionId id, DateOnly valueDate)
    {
        if (!id.IsValid || valueDate == default)
            throw new ArgumentException("Valid strategy-position identity and value date are required.");
    }

    static void ValidatePageSize(int pageSize)
    {
        if (pageSize is < 1 or > 500) throw new ArgumentOutOfRangeException(nameof(pageSize));
    }

    static void ValidateExitWorkflow(ExitPositionWorkflowProjection workflow)
    {
        if (workflow.SchemaVersion != 1 || !workflow.WorkflowId.IsValid ||
            workflow.StrategyKind is not (TradeStrategyKind.IronCondor or
                TradeStrategyKind.VerticalSpread or TradeStrategyKind.FuturesOutright) ||
            workflow.State is < ExitPositionWorkflowState.Started or > ExitPositionWorkflowState.Completed ||
            workflow.StageRevision < 1 || workflow.UpdatedAtUtc.Kind != DateTimeKind.Utc ||
            workflow.SourcePlanEventId == Guid.Empty ||
            workflow.ExitPlan.Position.Id != workflow.WorkflowId.Position ||
            workflow.ExitPlan.ValueDate != workflow.WorkflowId.ValueDate ||
            workflow.ExitPlan.Position.StrategyKind != workflow.StrategyKind)
            throw new ArgumentException("Valid exit-workflow projection is required.", nameof(workflow));
    }

    static sbyte ToCqlTinyInt<TEnum>(TEnum value) where TEnum : struct, Enum =>
        checked((sbyte)Convert.ToByte(value));

    readonly record struct Values(object?[] Items) : IBindValue
    {
        public object Bind() => Items;
    }
}
