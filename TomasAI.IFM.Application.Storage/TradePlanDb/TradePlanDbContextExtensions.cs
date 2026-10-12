using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Shared.Storage;
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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ProjectMaterialAsync operation.</summary>
        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031", Justification = "Disposable monitoring snapshots are logged and dropped on any persistence failure, as required; the next market update calculates a new snapshot.")]
        internal async Task ProjectMaterialAsyncCoreBinding(StrategyTradePlanSnapshot plan, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);
            try
            {
                if (context.postgres is not null)
                {
                    await context.StandaloneTransaction().ExecuteOnceAsync(async (transaction, token) =>
                    {
                        await context.postgres.ProjectMaterialAsync(transaction, plan, token)
                .ConfigureAwait(false);
                        return true;
                    }, cancellationToken)
                .ConfigureAwait(false);
                    return;
                }
        
                await context.WriteTradePlanOnceAsync(plan, cancellationToken)
                .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                StorageDiagnosticLog.PlanDropped(context.tradePlanLogger, exception, nameof(context.ProjectMaterialAsync), plan.Position.Id, plan.PlanRevision, plan.ValueDate);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","S2325", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design","CA1822", Justification="Keep the C# 14 DbContext binding extension receiver contract.")]
        /// <summary>Validates and persists the ProjectExitWorkflowAsync operation.</summary>
        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0051", Justification = "Keep the transaction, resource ownership and failure ordering together during this provider migration; splitting it would obscure the atomic persistence boundary.")]
        internal async Task ProjectExitWorkflowAsyncCoreBinding(ExitPositionWorkflowProjection workflow, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(workflow);
            if (context.postgres is not null)
            {
                await context.StandaloneTransaction().ExecuteOnceAsync(async (transaction, token) =>
                {
                    await context.postgres.ProjectExitWorkflowAsync(transaction, workflow, token)
                .ConfigureAwait(false);
                    return true;
                }, cancellationToken)
                .ConfigureAwait(false);
                return;
            }
        
            workflow.RequireValidExitWorkflow();
            var id = workflow.WorkflowId.Position.Trade;
            var positionId = workflow.WorkflowId.Position.PositionId;
            var payload = MessagePackBinarySerializer.Shared.Serialize(workflow);
            var selectParameters = new GetExactExitPositionWorkflow(id.PortfolioId, id.FundId, id.OrderId, id.TradeId, positionId, workflow.WorkflowId.ValueDate, workflow.UpdatedAtUtc, workflow.WorkflowId.ExitDecisionId, workflow.StageRevision);
            var existing = await context.Database.Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectExactExitWorkflow)}", TradePlanDbCql.SelectExactExitWorkflow)
                .SetParameters(selectParameters)
                .ExecuteSingleAsync(TradePlanDbContext.MapToPayload, cancellationToken)
                .ConfigureAwait(false);
            if (existing is not null)
            {
                if (!existing.AsSpan().SequenceEqual(payload))
                {
                    throw new InvalidOperationException("Exit-workflow projection revision conflicts with a different payload.");
                }
        
                return;
            }
        
            var insertParameters = new InsertExitPositionWorkflow(id.PortfolioId, id.FundId, id.OrderId, id.TradeId, positionId, workflow.WorkflowId.ValueDate, workflow.UpdatedAtUtc, workflow.WorkflowId.ExitDecisionId, workflow.StageRevision, workflow.StrategyKind.ToCqlTinyInt(), workflow.State.ToCqlTinyInt(), workflow.SourcePlanEventId, payload);
            await context.Database.Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.InsertExitWorkflow)}", TradePlanDbCql.InsertExitWorkflow)
                .SetParameters(insertParameters)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>Owns one standalone PostgreSQL write attempt when no source transaction was supplied.</summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal PostgresEventTransaction StandaloneTransaction() => new(new DbConnectionSettings()
            .Add(EventSourceActorDbContext.EventSourceActorDbConnection, context.ConnectionString, "System.Data.Postgres"));

        /// <summary>Attempts persistence once; duplicate revisions are skipped without secondary-write repair.</summary>
        /// <param name="plan">The immutable plan revision to write.</param>
        /// <param name="cancellationToken">Cancels the single persistence attempt.</param>
        /// <returns>The attempt; exceptions are logged and dropped by the public projection boundary.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
        internal async Task WriteTradePlanOnceAsync(
            StrategyTradePlanSnapshot plan,
            CancellationToken cancellationToken = default)
        {
            plan.Position.Id.RequireTradePlanScope(
                plan.Position.StrategyKind,
                plan.ValueDate);
            if (!plan.MaterialChange && plan.Position.StrategyKind != TradeStrategyKind.IronCondor)
                return;

            var id = plan.Position.Id.Trade;
            var table = plan.Position.StrategyKind.ToTradePlanTable();
            var selectStatement = TradePlanDbCql.SelectExactPlan.ForTable(table);
            var selectParameters = new GetExactTradePlan(
                id.PortfolioId,
                id.FundId,
                id.OrderId,
                id.TradeId,
                plan.Position.Id.PositionId,
                plan.ValueDate,
                plan.PlanRevision);
            var existing = await context.Database
                .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.SelectExactPlan)}", selectStatement)
                .SetParameters(selectParameters)
                .ExecuteSingleAsync(TradePlanDbContext.MapToContentHash, cancellationToken)
                .ConfigureAwait(false);

            var payload = MessagePackBinarySerializer.Shared.Serialize(plan);
            if (existing is not null)
            {
                if (!string.Equals(existing, plan.ContentHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Trade Plan projection revision conflicts with a different content hash.");
                }

                return;
            }

            var insertStatement = TradePlanDbCql.InsertPlan.ForTable(table);
            var insertParameters = new InsertTradePlan(
                id.PortfolioId,
                id.FundId,
                id.OrderId,
                id.TradeId,
                plan.Position.Id.PositionId,
                plan.ValueDate,
                plan.PlanRevision,
                plan.CalculatedAtUtc,
                plan.State.ToString(),
                plan.RequiresExit,
                plan.ContentHash,
                payload);
            await context.Database
                .Use($"{nameof(TradePlanDbCql)}.{nameof(TradePlanDbCql.InsertPlan)}", insertStatement)
                .SetParameters(insertParameters)
                .ExecuteCommandAsync(cancellationToken)
                .ConfigureAwait(false);
            await IronCondorTradePlanProjection.ProjectAsync(context, plan, cancellationToken)
                .ConfigureAwait(false);
            await context
                .ProjectActivityAsync(plan, payload, cancellationToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Projects the date-indexed activity entry for a material Trade Plan revision.
        /// </summary>
        /// <param name="plan">The material-plan snapshot being projected.</param>
        /// <param name="payload">The serialized plan payload.</param>
        /// <param name="cancellationToken">The token used to cancel the asynchronous command.</param>
        /// <returns>A task representing the asynchronous projection operation.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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

    extension<TEnum>(TEnum value) where TEnum : struct, Enum
    {
        /// <summary>
        /// Converts an enum value to the provider's CQL <c>tinyint</c> representation.
        /// </summary>
        /// <returns>The checked signed-byte representation.</returns>
        internal sbyte ToCqlTinyInt() => checked((sbyte)Convert.ToByte(value,System.Globalization.CultureInfo.InvariantCulture));
    }

    extension(string statement)
    {
        /// <summary>
        /// Formats a table-templated Trade Plan statement with a provider table name.
        /// </summary>
        /// <param name="table">The provider table name.</param>
        /// <returns>The formatted CQL statement.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "S2325", Justification = "Retain the established context/extension instance API used by storage callers; changing it to static would change the public access contract.")]
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
