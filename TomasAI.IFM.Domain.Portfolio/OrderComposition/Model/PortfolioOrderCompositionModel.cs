using TomasAI.IFM.Domain.Portfolio.Identity;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition;

namespace TomasAI.IFM.Domain.Portfolio.OrderComposition.Model;

public static class PortfolioOrderCompositionModel
{
    public static async ValueTask<PortfolioOrderCompositionReceipt> EvaluateAsync(
        EvaluatePortfolioOrderCompositionCommand request,
        FinancialBookConfiguration book,
        long nextRevision,
        IReadOnlyList<PortfolioFundFinancialSnapshot> financial,
        IPortfolioBusinessIdAllocator identities,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(financial);
        ArgumentNullException.ThrowIfNull(identities);
        var candidate = request.Body;
        ValidateRequest(request, book);

        var decisions = new List<PortfolioFundOrderDecision>(book.Funds.Length);
        var orders = new List<PortfolioExecutionOrderInstruction>(book.Funds.Length);
        var effects = new List<PortfolioAcceptedCapacityEffect>(book.Funds.Length);
        var provisional = new Dictionary<(CapacityScopeKind ScopeKind, string ScopeKey, CapacityMeasure Measure, CapacityUnit Unit), decimal>();
        foreach (var fund in book.Funds.OrderBy(value => value.FundId))
        {
            if (candidate.SetupTrade is { } setupScope && setupScope.FundId != fund.FundId) continue;
            if (!TrySelectDeployment(fund, candidate, request.RequestedAtUtc, out var deployment, out var reason))
            {
                decisions.Add(new(fund.FundId, false, reason, null));
                continue;
            }
            var snapshot = financial.SingleOrDefault(value => value.FundId == fund.FundId);
            if (snapshot is null)
            {
                decisions.Add(new(fund.FundId, false, "FinancialSnapshotUnavailable", null));
                continue;
            }
            if (!TryCreateCapacityEffect(request.PortfolioId, fund, deployment!, candidate, snapshot,
                provisional, out var effect, out var capacityReason))
            {
                decisions.Add(new(fund.FundId, false, capacityReason, null));
                continue;
            }
            var orderId = await identities.AllocateOrderIdAsync(cancellationToken).ConfigureAwait(false);
            var components = await AllocateComponentsAsync(candidate, identities, cancellationToken).ConfigureAwait(false);
            decisions.Add(new(fund.FundId, true, "Accepted", orderId));
            orders.Add(CreateOrder(request, fund.FundId, orderId, components));
            effect = effect! with { OrderId = orderId };
            effects.Add(effect);
            foreach (var exposure in effect.Exposures)
            {
                var key = Key(exposure);
                provisional[key] = provisional.GetValueOrDefault(key) + Math.Abs(exposure.Amount);
            }
        }
        return new()
        {
            CompositionId = candidate.CompositionId,
            WorkflowId = candidate.WorkflowId,
            Status = orders.Count == 0 ? PortfolioOrderCompositionStatus.NoTradeOrders : PortfolioOrderCompositionStatus.ExecuteTradeOrders,
            FundDecisions = [.. decisions],
            TradeOrders = [.. orders],
            FinancialRevision = nextRevision,
            PortfolioId = request.PortfolioId,
            CapacityEffects = [.. effects],
            VolatilityEvidence = candidate.VolatilityEvidence
        };
    }

    /// <summary>Allocates trade identifiers only after admission, preserving the component order and copied leg facts.</summary>
    static async ValueTask<PortfolioExecutionComponent[]> AllocateComponentsAsync(PortfolioOrderCandidate candidate,
        IPortfolioBusinessIdAllocator identities, CancellationToken cancellationToken)
    {
            var components = new PortfolioExecutionComponent[candidate.Components.Length];
            for (var componentIndex = 0; componentIndex < candidate.Components.Length; componentIndex++)
            {
                var component = candidate.Components[componentIndex];
                components[componentIndex] = component with
                {
                    Legs = [.. component.Legs],
                    ReservedTradeId = await identities.AllocateTradeIdAsync(cancellationToken).ConfigureAwait(false)
                };
            }
        return components;
    }

    /// <summary>Validates the complete order candidate before admitting funds or allocating business identifiers.</summary>
    static void ValidateRequest(EvaluatePortfolioOrderCompositionCommand request, FinancialBookConfiguration book)
    {
        var candidate = request.Body;
        if (request.PortfolioId <= 0 || request.OperationId == Guid.Empty || candidate.CompositionId == Guid.Empty
            || candidate.WorkflowId == Guid.Empty || candidate.Components.Length == 0
            || candidate.DecisionEvidence is { IsValid: false }
            || (candidate.SetupTrade is { } setup && (setup.OrderId <= 0 || setup.TradeId <= 0 || setup.FundId <= 0 || candidate.Components.Length != 1))
            || candidate.TimeInForce is not ("Day" or "GTC")
            || candidate.AlgorithmPace is not ("Patient" or "Normal" or "Urgent")
            || candidate.PositionType != PortfolioExecutionPositionType.Opening
            || candidate.Components.Any(component => component.Legs.Length == 0 ||
                component.Legs.Any(leg => string.IsNullOrWhiteSpace(leg.ContractId)))
            || candidate.DeploymentKey.Kind != TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog.StrategyCatalogKind.Deployment
            || candidate.DeploymentKey.Id == Guid.Empty || candidate.DeploymentKey.Version <= 0
            || candidate.RequiredCapital < 0 || candidate.MaximumLoss < 0 || candidate.StressLoss < 0 || candidate.Notional < 0
            || string.IsNullOrWhiteSpace(candidate.ProductSymbol) || string.IsNullOrWhiteSpace(candidate.ProductExchange)
            || !string.Equals(candidate.ProductCurrency, book.Currency, StringComparison.OrdinalIgnoreCase)
            || candidate.ValidUntilUtc.Kind != DateTimeKind.Utc || candidate.ValidUntilUtc <= request.RequestedAtUtc
            || candidate.EvidenceHash.Length != 64 || request.InputSha256.Length != 64)
            throw new ArgumentException("Portfolio order composition input is incomplete or invalid.", nameof(request));
        if (book.PortfolioId != request.PortfolioId || !book.MigrationQualified)
            throw new InvalidOperationException("Portfolio financial authority is unavailable.");

    }

    /// <summary>Selects the exact unexpired assigned deployment after applying the existing spending and per-trade risk rules.</summary>
    static bool TrySelectDeployment(FinancialFundAuthority fund, PortfolioOrderCandidate candidate, DateTime requestedAtUtc,
        out FinancialDeploymentAuthority? deployment, out string reason)
    {
        deployment = null;
        reason = string.Empty;
            if (!fund.CanSpend)
            {
                reason = "FundSpendingDisabled";
                return false;
            }
            deployment = fund.Deployments.SingleOrDefault(value => value.Reference.DeploymentKey == candidate.DeploymentKey);
            if (deployment is null)
            {
                reason = "StrategyDeploymentNotAssigned";
                return false;
            }
            if (deployment.Reference.ValidUntilUtc <= requestedAtUtc)
            {
                reason = "FinancialAuthorityExpired";
                return false;
            }
            if (deployment.MaximumRiskPerTrade > 0
                && Math.Max(candidate.MaximumLoss, candidate.StressLoss) > deployment.MaximumRiskPerTrade)
            {
                reason = "MaximumRiskPerTradeExceeded";
                return false;
            }
        return true;
    }

    /// <summary>Freezes the admitted candidate and allocated component identities into the approved broker instruction.</summary>
    static PortfolioExecutionOrderInstruction CreateOrder(EvaluatePortfolioOrderCompositionCommand request, int fundId,
        int orderId, PortfolioExecutionComponent[] components)
    {
        var candidate = request.Body;
            return new PortfolioExecutionOrderInstruction
            {
                Id = new(request.PortfolioId, fundId, orderId),
                Revision = 1,
                Status = PortfolioExecutionOrderStatus.Approved,
                ValueDate = candidate.ValueDate,
                PositionType = candidate.PositionType,
                ValidUntilUtc = candidate.ValidUntilUtc,
                Origin = candidate.Origin,
                Components = components,
                DefinitionHash = candidate.EvidenceHash,
                BrokerAccountAlias = candidate.BrokerAccountAlias,
                BrokerEnvironment = candidate.BrokerEnvironment,
                PortfolioApprovalId = request.OperationId,
                MicroExecutionProfileId = candidate.MicroExecutionProfileId,
                MicroExecutionProfileVersion = candidate.MicroExecutionProfileVersion,
                MicroExecutionProfileHash = candidate.MicroExecutionProfileHash,
                AccountPromotionApprovalReference = candidate.AccountPromotionApprovalReference,
                RequiredCapital = candidate.RequiredCapital,
                MaximumLoss = candidate.MaximumLoss,
                BrokerOrderType = candidate.BrokerOrderType,
                BrokerAlgorithm = candidate.BrokerAlgorithm,
                TimeInForce = candidate.TimeInForce,
                AlgorithmPace = candidate.AlgorithmPace,
                SetupTrade = candidate.SetupTrade,
                DecisionEvidence = candidate.DecisionEvidence,
                VolatilityEvidence = candidate.VolatilityEvidence
            };
    }

    static bool TryCreateCapacityEffect(int portfolioId, FinancialFundAuthority fund,
        FinancialDeploymentAuthority deployment, PortfolioOrderCandidate candidate,
        PortfolioFundFinancialSnapshot snapshot,
        IReadOnlyDictionary<(CapacityScopeKind ScopeKind, string ScopeKey, CapacityMeasure Measure, CapacityUnit Unit), decimal> provisional,
        out PortfolioAcceptedCapacityEffect? effect, out string reason)
    {
        effect = null;
        reason = string.Empty;
        var underlying = FinancialScopeKeys.Underlying(candidate.ProductSymbol, candidate.ProductExchange, candidate.ProductCurrency);
        var expectedScopes = CreateExpectedScopes(portfolioId, fund.FundId, candidate, underlying);
        var limits = fund.Limits.Concat(deployment.Limits).ToArray();
        if (limits.Length == 0 || limits.Select(Key).Distinct().Count() != limits.Length
            || limits.Any(limit => !expectedScopes.TryGetValue(limit.ScopeKind, out var scopeKey)
                || !string.Equals(limit.ScopeKey, scopeKey, StringComparison.Ordinal)
                || limit.Measure == CapacityMeasure.Undefined || limit.Unit == CapacityUnit.Undefined || limit.Maximum < 0))
        {
            reason = "CapacityConfigurationInvalid";
            return false;
        }
        var exposures = limits.Select(limit => new CapacityExposure
        {
            ScopeKind = limit.ScopeKind,
            ScopeKey = limit.ScopeKey,
            Measure = limit.Measure,
            Amount = CalculateCapacityAmount(candidate, limit.Measure),
            Unit = limit.Unit,
            MethodVersion = 1
        }).ToArray();
        var persisted = snapshot.Usage.ToDictionary(Key, usage =>
            Math.Abs(usage.Held) + Math.Abs(usage.Working) + Math.Abs(usage.Position));
        var committedCash = snapshot.Usage.Where(usage => usage.ScopeKind == CapacityScopeKind.Fund
                && string.Equals(usage.ScopeKey,expectedScopes[CapacityScopeKind.Fund],StringComparison.Ordinal)
                && usage.Measure == CapacityMeasure.SettlementCash && usage.Unit == CapacityUnit.Usd)
            .Sum(usage => Math.Abs(usage.Held) + Math.Abs(usage.Working) + Math.Abs(usage.Position));
        committedCash += provisional.Where(value => value.Key.ScopeKind == CapacityScopeKind.Fund
                && string.Equals(value.Key.ScopeKey,expectedScopes[CapacityScopeKind.Fund],StringComparison.Ordinal)
                && value.Key.Measure == CapacityMeasure.SettlementCash && value.Key.Unit == CapacityUnit.Usd)
            .Sum(value => value.Value);
        if (snapshot.AvailableCash - committedCash < candidate.RequiredCapital)
        {
            reason = "InsufficientCash";
            return false;
        }
        foreach (var pair in limits.Zip(exposures))
        {
            if (!pair.First.Enabled) continue;
            var key = Key(pair.Second);
            if (persisted.GetValueOrDefault(key) + provisional.GetValueOrDefault(key) + Math.Abs(pair.Second.Amount) > pair.First.Maximum)
            {
                reason = "CapacityExceeded";
                return false;
            }
        }
        effect = new PortfolioAcceptedCapacityEffect
        {
            PortfolioId = portfolioId,
            FundId = fund.FundId,
            UnderlyingScopeKey = underlying,
            RequiredCash = candidate.RequiredCapital,
            Exposures = exposures
        };
        return true;
    }

    /// <summary>Defines the exact portfolio, fund, deployment and underlying scope keys allowed for this candidate.</summary>
    static Dictionary<CapacityScopeKind, string> CreateExpectedScopes(int portfolioId, int fundId,
        PortfolioOrderCandidate candidate, string underlying)
    {
        return new Dictionary<CapacityScopeKind, string>
        {
            [CapacityScopeKind.Portfolio] = FinancialScopeKeys.Portfolio(portfolioId),
            [CapacityScopeKind.Fund] = FinancialScopeKeys.Fund(fundId),
            [CapacityScopeKind.Deployment] = FinancialScopeKeys.Deployment(candidate.DeploymentKey),
            [CapacityScopeKind.Underlying] = underlying
        };
    }

    /// <summary>Maps each accepted capacity measure to the candidate's cash, loss, notional, contract or Greek exposure.</summary>
    static decimal CalculateCapacityAmount(PortfolioOrderCandidate candidate, CapacityMeasure measure)
    {
        var grossContracts = candidate.Components.SelectMany(component => component.Legs)
            .Sum(leg => Math.Abs(leg.SignedQuantity));
        return measure switch
        {
            CapacityMeasure.SettlementCash => candidate.RequiredCapital,
            CapacityMeasure.LossCharge => Math.Max(candidate.MaximumLoss, candidate.StressLoss),
            CapacityMeasure.Margin => candidate.RequiredCapital,
            CapacityMeasure.GrossNotional => candidate.Notional,
            CapacityMeasure.GrossContracts => grossContracts,
            CapacityMeasure.PositionSlots => 1,
            CapacityMeasure.Delta => candidate.Delta,
            CapacityMeasure.Gamma => candidate.Gamma,
            CapacityMeasure.Vega => candidate.Vega,
            _ => throw new InvalidOperationException($"Unsupported accepted capacity measure {measure}.")
        };
    }

    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityExposure value)
        => (value.ScopeKind, value.ScopeKey, value.Measure, value.Unit);
    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityLimit value)
        => (value.ScopeKind, value.ScopeKey, value.Measure, value.Unit);
    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityUsed value)
        => (value.ScopeKind, value.ScopeKey, value.Measure, value.Unit);
}
