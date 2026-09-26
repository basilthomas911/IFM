using System.Globalization;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan;
using TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Workflow;

namespace TomasAI.IFM.Application.Storage.TradePlanDb;

/// <summary>
/// Provides internal persistence helpers for <see cref="TradePlanDbContext"/>.
/// </summary>
internal static class TradePlanDbContextExtensions
{
    extension(TradePlanDbContext context)
    {
        /// <summary>
        /// Projects the date-indexed activity entry for a material Trade Plan revision.
        /// </summary>
        /// <param name="plan">The material-plan snapshot being projected.</param>
        /// <param name="payload">The serialized plan payload.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous command.</param>
        /// <returns>A task representing the asynchronous projection operation.</returns>
        internal async Task ProjectActivityAsync(
            StrategyTradePlanSnapshot plan,
            byte[] payload,
            CancellationToken cancellationToken)
        {
            var id = plan.Position.Id.Trade;
            var parameters = new InsertTradePlanActivity(
                plan.ValueDate,
                plan.CalculatedAtUtc,
                id.PortfolioId,
                id.FundId,
                id.OrderId,
                id.TradeId,
                plan.Position.Id.PositionId,
                plan.Position.StrategyKind.ToCqlTinyInt(),
                plan.PlanRevision,
                plan.State.ToCqlTinyInt(),
                plan.RequiresExit,
                plan.ContentHash,
                payload);
            await context.Database
                .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.InsertActivity)}", TradePlanDbCql.InsertActivity)
                .SetParameters(parameters)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    extension(TradeStrategyKind strategy)
    {
        /// <summary>
        /// Gets the materialized Trade Plan table for a supported trade strategy.
        /// </summary>
        /// <returns>The provider table name.</returns>
        internal string ToTradePlanTable() => strategy switch
        {
            TradeStrategyKind.IronCondor => "iron_condor_trade_plan_v1",
            TradeStrategyKind.VerticalSpread => "vertical_spread_trade_plan_v1",
            TradeStrategyKind.FuturesOutright => "futures_trade_plan_v1",
            _ => throw new ArgumentOutOfRangeException(
                nameof(strategy),
                strategy,
                "Unsupported Trade Plan strategy.")
        };
    }

    extension<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        /// <summary>
        /// Converts an enum value to the provider's CQL <c>tinyint</c> representation.
        /// </summary>
        /// <returns>The checked signed-byte representation.</returns>
        internal sbyte ToCqlTinyInt() => checked((sbyte)Convert.ToByte(value));
    }

    extension(string statement)
    {
        /// <summary>
        /// Formats a table-templated Trade Plan statement with a provider table name.
        /// </summary>
        /// <param name="table">The provider table name.</param>
        /// <returns>The formatted CQL statement.</returns>
        internal string ForTable(string table) =>
            string.Format(CultureInfo.InvariantCulture, statement, table);
    }

    extension(StrategyPositionId positionId)
    {
        /// <summary>
        /// Requires a valid Trade Plan identity, supported strategy, and value date.
        /// </summary>
        /// <param name="strategy">The trade strategy.</param>
        /// <param name="valueDate">The plan value date.</param>
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
                    "Valid Trade Plan identity, strategy, and value date are required.");
            }
        }

        /// <summary>
        /// Requires a valid strategy-position identity and value date.
        /// </summary>
        /// <param name="valueDate">The plan or workflow value date.</param>
        internal void RequireValueDate(DateOnly valueDate)
        {
            if (!positionId.IsValid || valueDate == default)
            {
                throw new ArgumentException(
                    "Valid strategy-position identity and value date are required.");
            }
        }
    }

    extension(DateOnly valueDate)
    {
        /// <summary>
        /// Requires a non-default Trade Plan value date.
        /// </summary>
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
        internal void RequireValidExitWorkflow()
        {
            if (workflow.SchemaVersion != 1
                || !workflow.WorkflowId.IsValid
                || workflow.StrategyKind is not (
                    TradeStrategyKind.IronCondor
                    or TradeStrategyKind.VerticalSpread
                    or TradeStrategyKind.FuturesOutright)
                || workflow.State is < ExitPositionWorkflowState.Started
                    or > ExitPositionWorkflowState.Completed
                || workflow.StageRevision < 1
                || workflow.UpdatedAtUtc.Kind != DateTimeKind.Utc
                || workflow.SourcePlanEventId == Guid.Empty
                || workflow.ExitPlan.Position.Id != workflow.WorkflowId.Position
                || workflow.ExitPlan.ValueDate != workflow.WorkflowId.ValueDate
                || workflow.ExitPlan.Position.StrategyKind != workflow.StrategyKind)
            {
                throw new ArgumentException(
                    "Valid exit-workflow projection is required.",
                    nameof(workflow));
            }
        }
    }
}
