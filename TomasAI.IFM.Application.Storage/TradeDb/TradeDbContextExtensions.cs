using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.Domain.Trade.Shared.ViewModels;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Identity;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.MarketCondition.Assessment;
using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.Events;

namespace TomasAI.IFM.Application.Storage.TradeDb;

/// <summary>Provides internal persistence helpers for <see cref="TradeDbContext"/>.</summary>
internal static class TradeDbContextExtensions
{
    extension(TradeDbContext context)
    {
        /// <summary>Hydrates an option trade with its persisted positions, legs, limits, and fills.</summary>
        internal async Task<OptionTradeReadModel> FillOptionTradeAsync(OptionTradeReadModel optionTrade)
        {
            var entityId = optionTrade.EntityId;
            var db = context._dbFactory.TradeDb;
            var tradePositionsTask = db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
                .SetParameters(new GetTradePositions(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradePosition!);
            var optionLegsTask = db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
                .SetParameters(new GetOptionLegsByOrderAndTrade(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToOptionLeg!);
            var tradeLimitTask = context.GetTradeLimitAsync(optionTrade.TradeId);
            var tradeTypeLimitsTask = db
                .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimits)}", TradeDbCql.GetTradeTypeLimits)
                .SetParameters(new GetTradeTypeLimits(optionTrade.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradeTypeLimit);
            var tradeFillsTask = context.GetTradeFillsAsync(optionTrade.OrderId, optionTrade.TradeId);

            var tradePositions = await tradePositionsTask;
            var valueDates = tradePositions.Select(position => position.ValueDate).Distinct().ToArray();
            var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, valueDates.Length),
                new ParallelOptions { MaxDegreeOfParallelism = 4 },
                async (dateIndex, _) => legDataByDate[dateIndex] = await db
                    .Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                    .SetParameters(new GetOptionLegData(entityId.OrderId, entityId.TradeId, valueDates[dateIndex]))
                    .ExecuteQueryAsync(TradeDbContext.MapToOptionLegData!));

            await Task.WhenAll(optionLegsTask, tradeLimitTask, tradeTypeLimitsTask, tradeFillsTask);
            var optionLegs = await optionLegsTask;
            var optionLegById = optionLegs.ToDictionary(leg => leg.ContractId, StringComparer.Ordinal);
            var legDataByPosition = TradeDbContext.MapToLegDataByPosition(legDataByDate, optionLegById, requireOptionLeg: true);

            foreach (var position in tradePositions)
            {
                if (legDataByPosition.TryGetValue(TradeDbContext.MapToPositionKey(position), out var legData))
                    position.AddOptionLegData(legData);
            }

            return optionTrade
                .AddOptionLegs(optionLegs)
                .AddTradePosition(tradePositions)
                .SetTradeLimit((await tradeLimitTask)!)
                .AddTradeTypeLimits(await tradeTypeLimitsTask)
                .AddTradeFills(await tradeFillsTask);
        }

        /// <summary>Hydrates an option trade with its persisted graph while observing cancellation.</summary>
        internal async Task<OptionTradeReadModel> FillOptionTradeAsync(
                OptionTradeReadModel optionTrade,
                CancellationToken cancellationToken)
        {
            var entityId = optionTrade.EntityId;
            var db = context._dbFactory.TradeDb;
            var tradePositionsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradePositions)}", TradeDbCql.GetTradePositions)
                .SetParameters(new GetTradePositions(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradePosition!, cancellationToken);
            var optionLegsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegsByOrderAndTrade)}", TradeDbCql.GetOptionLegsByOrderAndTrade)
                .SetParameters(new GetOptionLegsByOrderAndTrade(entityId.OrderId, entityId.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToOptionLeg!, cancellationToken);
            var tradeLimitTask = context.GetTradeLimitAsync(optionTrade.TradeId, cancellationToken);
            var tradeTypeLimitsTask = db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetTradeTypeLimits)}", TradeDbCql.GetTradeTypeLimits)
                .SetParameters(new GetTradeTypeLimits(optionTrade.TradeId))
                .ExecuteQueryAsync(TradeDbContext.MapToTradeTypeLimit, cancellationToken);
            var tradeFillsTask = context.GetTradeFillsAsync(
                optionTrade.OrderId,
                optionTrade.TradeId,
                cancellationToken);

            var tradePositions = await tradePositionsTask;
            var valueDates = tradePositions
                .Select(position => position.ValueDate)
                .Distinct()
                .ToArray();
            var legDataByDate = new ICollection<OptionTradeLegDataReadModel>[valueDates.Length];
            await Parallel.ForEachAsync(
                Enumerable.Range(0, valueDates.Length),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cancellationToken
                },
                async (dateIndex, token) =>
                    legDataByDate[dateIndex] = await db.Use($"{nameof(TradeDbCql)}.{nameof(TradeDbCql.GetOptionLegData)}", TradeDbCql.GetOptionLegData)
                        .SetParameters(new GetOptionLegData(
                            entityId.OrderId,
                            entityId.TradeId,
                            valueDates[dateIndex]))
                        .ExecuteQueryAsync(TradeDbContext.MapToOptionLegData!, token));

            await Task.WhenAll(
                optionLegsTask,
                tradeLimitTask,
                tradeTypeLimitsTask,
                tradeFillsTask);
            cancellationToken.ThrowIfCancellationRequested();

            var optionLegs = await optionLegsTask;
            var optionLegById = optionLegs.ToDictionary(
                leg => leg.ContractId,
                StringComparer.Ordinal);
            var legDataByPosition = TradeDbContext.MapToLegDataByPosition(
                legDataByDate,
                optionLegById,
                requireOptionLeg: true);
            foreach (var position in tradePositions)
            {
                if (legDataByPosition.TryGetValue(TradeDbContext.MapToPositionKey(position), out var legData))
                    position.AddOptionLegData(legData);
            }

            return optionTrade
                .AddOptionLegs(optionLegs)
                .AddTradePosition(tradePositions)
                .SetTradeLimit((await tradeLimitTask)!)
                .AddTradeTypeLimits(await tradeTypeLimitsTask)
                .AddTradeFills(await tradeFillsTask);
        }
    }

    extension(int pageSize)
    {
        /// <summary>Validates a requested TradeDb query page size.</summary>
        internal int RequirePageSize()
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
            return pageSize;
        }
    }

    extension(DateOnly endDate)
    {
        /// <summary>Requires an inclusive date range whose end does not precede its start.</summary>
        internal void RequireNotBefore(DateOnly startDate)
        {
            if (endDate < startDate)
                throw new ArgumentOutOfRangeException(nameof(endDate), endDate, "End date must not precede start date.");
        }
    }

    extension(int fundId)
    {
        /// <summary>Validates the market-condition history partition and requested page size.</summary>
        internal void RequireMarketConditionHistoryScope(string instrumentRoot, int pageSize)
        {
            if (fundId <= 0)
                throw new ArgumentOutOfRangeException(nameof(fundId));
            if (string.IsNullOrWhiteSpace(instrumentRoot))
                throw new ArgumentException("An instrument root is required.", nameof(instrumentRoot));
            if (pageSize is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(pageSize));
        }
    }

    extension(string profile)
    {
        /// <summary>Validates the market-condition assessment history partition and page bound.</summary>
        internal void RequireAssessmentHistoryScope(string root, TimeFrameType horizon, DateTime beforeUtc, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(profile) || root != "ES" ||
                !MarketConditionAssessmentParameterSet.IsHorizon(horizon) || pageSize is < 1 or > 100 ||
                !MarketConditionAssessmentContracts.Utc(beforeUtc))
                throw new ArgumentException("Invalid assessment history partition or bound.");
        }

        /// <summary>Requires a non-empty routed contract identifier.</summary>
        internal void RequireContractId()
        {
            if (string.IsNullOrWhiteSpace(profile))
                throw new ArgumentException("ContractId is required.", "contractId");
        }
    }

    extension(MarketConditionAssessmentCompletedEvent completed)
    {
        /// <summary>Requires the assessment snapshot hashes to match the projected result.</summary>
        internal void RequireAssessmentProjectionIdentity(MarketConditionAssessmentResult result)
        {
            if (completed.Snapshot.ComputeHash() != result.SnapshotSha256 ||
                completed.Snapshot.PayloadSha256 != result.SnapshotSha256)
                throw new ArgumentException("Assessment projection snapshot hash mismatch.");
        }
    }

    extension(StrategyWorkflowId workflowId)
    {
        /// <summary>Requires exact workflow and invocation identities.</summary>
        internal void RequireInvocation(Guid invocationId, string message)
        {
            if (workflowId.Value == Guid.Empty || invocationId == Guid.Empty)
                throw new ArgumentException(message);
        }
    }

    extension(Guid identifier)
    {
        /// <summary>Requires exact workflow and invocation identifiers.</summary>
        internal void RequireInvocation(Guid invocationId, string message)
        {
            if (identifier == Guid.Empty || invocationId == Guid.Empty)
                throw new ArgumentException(message);
        }

        /// <summary>Requires a non-empty identifier.</summary>
        internal void Require(string message, string parameterName)
        {
            if (identifier == Guid.Empty)
                throw new ArgumentException(message, parameterName);
        }

        /// <summary>Validates a position-history identity, UTC range, and page size.</summary>
        internal void RequireUtcHistoryScope(DateTime fromUtc, DateTime toUtc, int pageSize)
        {
            if (identifier == Guid.Empty || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc ||
                fromUtc > toUtc || pageSize is < 1 or > 1000)
                throw new ArgumentException("Valid PositionId, UTC range, and page size 1..1000 are required.");
        }
    }

    extension(int portfolioId)
    {
        /// <summary>Validates a portfolio/fund/date history partition and page size.</summary>
        internal void RequireHistoryScope(int fundId, DateOnly valueDate, int pageSize, int maximumPageSize, string message)
        {
            if (portfolioId <= 0 || fundId <= 0 || valueDate == default || pageSize < 1 || pageSize > maximumPageSize)
                throw new ArgumentException(message);
        }

        /// <summary>Validates a portfolio/fund UTC history range and page size.</summary>
        internal void RequireUtcHistoryScope(int fundId, DateTime fromUtc, DateTime toUtc, int pageSize)
        {
            if (portfolioId <= 0 || fundId <= 0 || fromUtc.Kind != DateTimeKind.Utc ||
                toUtc.Kind != DateTimeKind.Utc || fromUtc > toUtc || pageSize is < 1 or > 1000)
                throw new ArgumentException("Valid ownership, UTC range, and page size 1..1000 are required.");
        }
    }

    extension(StrategyPositionSnapshot position)
    {
        /// <summary>Requires a strategy position with a valid identity.</summary>
        internal void RequireValidIdentity()
        {
            if (!position.Id.IsValid)
                throw new ArgumentException("Valid position identity is required.", nameof(position));
        }

        /// <summary>Requires every routed position leg to identify its contract.</summary>
        internal void RequireRoutableLegs()
        {
            if (position.Legs.Any(static leg => string.IsNullOrWhiteSpace(leg.ContractId)))
                throw new ArgumentException("Every routed position leg requires ContractId.", nameof(position));
        }
    }

    extension(StrategyPositionId id)
    {
        /// <summary>Requires a valid strategy-position identity.</summary>
        internal void Require()
        {
            if (!id.IsValid)
                throw new ArgumentException("Valid position identity is required.", nameof(id));
        }
    }

    extension(TradeEntityId id)
    {
        /// <summary>Requires a valid portfolio, fund, order, and trade identity.</summary>
        internal void Require()
        {
            if (!id.IsValid)
                throw new ArgumentException(
                    "Valid Portfolio, Fund, Order, and Trade identity is required.",
                    nameof(id));
        }
    }

    extension(TradeOrderId id)
    {
        /// <summary>Requires a valid portfolio, fund, and order identity.</summary>
        internal void Require()
        {
            if (!id.IsValid)
                throw new ArgumentException(
                    "Valid Portfolio, Fund, and Order identity is required.",
                    nameof(id));
        }
    }

    extension<T>(T? value) where T : class
    {
        /// <summary>Requires a non-null TradeDb contract argument and returns it.</summary>
        internal T RequireNotNull(string parameterName)
        {
            ArgumentNullException.ThrowIfNull(value, parameterName);
            return value;
        }
    }
}
