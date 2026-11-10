using TomasAI.IFM.Application.Serialization;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.PortfolioDb;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Order.Execution;
using TomasAI.IFM.Domain.Trade.Shared.Order;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Execution;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.EventSourcing.ViewModels;
using static TomasAI.IFM.Domain.Portfolio.Financial.Repository.FinancialRepositorySupport;

namespace TomasAI.IFM.Domain.Portfolio.Financial.Repository;

/// <summary>Converts accepted commitments to actual position/remainder usage under the financial fence.</summary>
internal static class ExecutionCapacitySettlement
{
    internal static async Task<AcceptedExecutionCapacityFacts> PrepareAndApplyAsync(EnlistedEventTransaction db,
        int portfolioId, ExecutionCapacityAdjustment outcome, long financialRevision, CancellationToken token)
    {
        var sources = await db.QueryAsync(ExecutionCapacitySql.Source, [outcome.SourceCommandId],
            row => new EventLogReadModel(row.GetInt64(0), row.GetString(1), row.GetString(2), row.GetInt64(3),
                row.GetFieldValue<byte[]>(4), row.GetGuid(5), row.GetString(6), row.GetInt64(7)), token).ConfigureAwait(false);
        var events = sources.Select(row => row.ToDomainEvent()).ToArray();
        TradeOrderDefinition order;
        long sourceVersion;
        if (outcome.UnsubmittedExpiry)
        {
            var expiry = events.OfType<TradeOrderChangedEvent>().SingleOrDefault();
            var expired = expiry?.TradeOrderDefinition;
            Require(expiry is not null && expiry.EventId > 0 && expired is
                { Status: TradeOrderStatus.Expired, BoundExecutionAttemptId: null, PositionType: TradeOrderPositionType.Opening,
                    BrokerEnvironment: BrokerEnvironment.Emulator, StrategyWorkflowId: not null } &&
                expired.Id.PortfolioId == portfolioId && expired.Id.FundId == outcome.FundId && expired.Id.OrderId == outcome.OrderId &&
                FinancialCanonicalHash.Compute(expired) == outcome.ExecutionHash &&
                StrategyOpeningExecutionIdentity.For(expired.Id, expired.StrategyWorkflowId.Value) == outcome.ExecutionId &&
                outcome.ApprovedUnits == expired.Components.Sum(component => component.Legs.Max(leg => Math.Abs(leg.SignedQuantity))) &&
                outcome.ApprovedUnits > 0 && outcome.FilledUnits == 0 && outcome.CancelledUnits == outcome.ApprovedUnits,
                FinancialReasons.AuthorityDenied, "A committed unbound Trade Order expiry is required before releasing working capacity.");
            order = expired!;
            sourceVersion = expiry!.EventId;
        }
        else
        {
        var source = events.OfType<OrderExecutionChangedEvent>().SingleOrDefault();
        var execution = source?.OrderExecutionDefinition;
        Require(execution is not null && source!.EventId > 0 && execution.TradeOrderId.PortfolioId == portfolioId &&
            execution.TradeOrderId.FundId == outcome.FundId && execution.TradeOrderId.OrderId == outcome.OrderId &&
            execution.ExecutionAttemptId == outcome.ExecutionId && execution.Order.StrategyWorkflowId is not null &&
            execution.Order.PositionType == TradeOrderPositionType.Opening && execution.Order.BrokerEnvironment == BrokerEnvironment.Emulator &&
            execution.Status is OrderExecutionStatus.PartiallyFilled or OrderExecutionStatus.Filled or OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected &&
            FinancialCanonicalHash.Compute(execution) == outcome.ExecutionHash && execution.OrderQuantity == outcome.ApprovedUnits &&
            execution.CumulativeFilledQuantity == outcome.FilledUnits && outcome.ApprovedUnits > 0 &&
            outcome.FilledUnits >= 0 && outcome.FilledUnits + outcome.CancelledUnits <= outcome.ApprovedUnits &&
            outcome.CancelledUnits == (execution.Status is OrderExecutionStatus.Cancelled or OrderExecutionStatus.Rejected
                ? outcome.ApprovedUnits - outcome.FilledUnits : 0),
            FinancialReasons.AuthorityDenied, "Exact committed automatic opening execution is required for capacity settlement.");
        order = execution!.Order;
        sourceVersion = source!.EventId;
        }
        var approved = await db.ScalarAsync(ExecutionCapacitySql.Approval,
            [outcome.OrderId, portfolioId, outcome.FundId, order.PortfolioApprovalId], token).ConfigureAwait(false);
        Require(approved is string hash && hash == order.DefinitionHash,
            FinancialReasons.AuthorityDenied, "Execution must retain its authoritative Portfolio approval.");
        var stored = await db.ScalarAsync(ExecutionCapacitySql.Read, [outcome.OrderId], token).ConfigureAwait(false);
        var prior = stored is byte[] bytes ? MessagePackBinarySerializer.Shared.Deserialize<AcceptedExecutionCapacityFacts>(bytes) : null;
        Require(prior is null || prior.Outcome.ExecutionId == outcome.ExecutionId && prior.Outcome.FundId == outcome.FundId &&
            prior.Outcome.ApprovedUnits == outcome.ApprovedUnits, FinancialReasons.SourceConflict, "Execution capacity ownership changed.");
        if (prior is not null && prior.SourceEventVersion >= sourceVersion)
            return new() { Outcome = prior.Outcome, SourceEventVersion = prior.SourceEventVersion };
        Require(prior is null || outcome.FilledUnits >= prior.Outcome.FilledUnits,
            FinancialReasons.SourceConflict, "Confirmed cumulative fills cannot regress.");
        var exposures = await db.QueryAsync(ExecutionCapacitySql.Exposures, [outcome.OrderId, portfolioId, outcome.FundId],
            row => new CapacityExposure { ScopeKind = (CapacityScopeKind)row.GetInt32(0), ScopeKey = row.GetString(1),
                Measure = (CapacityMeasure)row.GetInt32(2), Unit = (CapacityUnit)row.GetInt32(3), Amount = row.GetDecimal(4), MethodVersion = row.GetInt32(5) }, token).ConfigureAwait(false);
        Require(exposures.Count > 0, FinancialReasons.AuthorityDenied, "Original accepted capacity effects are required.");
        var previous = Fractions(prior?.Outcome);
        var next = Fractions(outcome);
        var facts = new AcceptedExecutionCapacityFacts { Outcome = outcome, SourceEventVersion = sourceVersion,
            UsageDeltas = exposures.Select(exposure => CapacityReservationStore.CalculateUsage(exposure, previous, next)).ToArray() };
        await ApplyAsync(db, portfolioId, financialRevision, facts, token).ConfigureAwait(false);
        return facts;

        (decimal Held, decimal Working, decimal Position) Fractions(ExecutionCapacityAdjustment? value)
        {
            if (value is null) return (0, 1, 0);
            return (0, (decimal)(value.ApprovedUnits - value.FilledUnits - value.CancelledUnits) / value.ApprovedUnits,
                (decimal)value.FilledUnits / value.ApprovedUnits);
        }
    }

    /// <summary>Replays recorded deltas without rereading broker state or running admission calculations.</summary>
    internal static async Task ApplyAsync(EnlistedEventTransaction db, int portfolioId, long revision,
        AcceptedExecutionCapacityFacts facts, CancellationToken token)
    {
        foreach (var delta in facts.UsageDeltas)
            await CapacityReservationStore.ApplyUsageAsync(db, portfolioId, delta, revision, token).ConfigureAwait(false);
        await db.ExecuteAsync(ExecutionCapacitySql.Write, [facts.Outcome.OrderId, portfolioId, facts.Outcome.FundId,
            facts.Outcome.ExecutionId, facts.SourceEventVersion, revision, MessagePackBinarySerializer.Shared.Serialize(facts)], token).ConfigureAwait(false);
    }
}
