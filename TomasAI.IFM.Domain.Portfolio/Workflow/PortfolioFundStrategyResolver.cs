using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.Workflow;

public sealed class PortfolioResolutionException(string reasonCode, string message) : InvalidOperationException(message)
{
    public string ReasonCode { get; } = reasonCode;
}

/// <summary>Deterministically freezes the single eligible Portfolio/Fund configuration used by one workflow.</summary>
public sealed class PortfolioFundStrategyResolver
{
    public PortfolioFundStrategySnapshot Resolve(
        Guid workflowId,
        long workflowRevision,
        Guid correlationId,
        PortfolioReadModel portfolio,
        PortfolioFinancialPolicyReadModel financialPolicy,
        IEnumerable<FundMandateReadModel> funds,
        IEnumerable<FundAllocationReadModel> allocations,
        IEnumerable<FundRiskEnvelopeReadModel> envelopes,
        IEnumerable<FundTradeTemplateAssignmentReadModel> assignments,
        int tradingYear,
        string decisionHorizon,
        string underlyingRoot,
        string assetType,
        DateTime asOfUtc)
        => ResolveCore(workflowId, workflowRevision, correlationId, portfolio, financialPolicy, funds, allocations, envelopes, assignments, tradingYear, decisionHorizon, underlyingRoot, assetType, asOfUtc, false, null);

    public PortfolioFundStrategySnapshot ResolveForSelection(
        Guid workflowId,
        long workflowRevision,
        Guid correlationId,
        PortfolioReadModel portfolio,
        PortfolioFinancialPolicyReadModel financialPolicy,
        IEnumerable<FundMandateReadModel> funds,
        IEnumerable<FundAllocationReadModel> allocations,
        IEnumerable<FundRiskEnvelopeReadModel> envelopes,
        IEnumerable<FundTradeTemplateAssignmentReadModel> assignments,
        int tradingYear,
        string decisionHorizon,
        string underlyingRoot,
        DateTime asOfUtc, int? fundId = null)
        => ResolveCore(workflowId, workflowRevision, correlationId, portfolio, financialPolicy, funds, allocations, envelopes, assignments, tradingYear, decisionHorizon, underlyingRoot, string.Empty, asOfUtc, true, fundId);

    static PortfolioFundStrategySnapshot ResolveCore(
        Guid workflowId,
        long workflowRevision,
        Guid correlationId,
        PortfolioReadModel portfolio,
        PortfolioFinancialPolicyReadModel financialPolicy,
        IEnumerable<FundMandateReadModel> funds,
        IEnumerable<FundAllocationReadModel> allocations,
        IEnumerable<FundRiskEnvelopeReadModel> envelopes,
        IEnumerable<FundTradeTemplateAssignmentReadModel> assignments,
        int tradingYear,
        string decisionHorizon,
        string underlyingRoot,
        string assetType,
        DateTime asOfUtc, bool forSelection, int? fundId)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(financialPolicy);
        ArgumentNullException.ThrowIfNull(funds);
        ArgumentNullException.ThrowIfNull(allocations);
        ArgumentNullException.ThrowIfNull(envelopes);
        ArgumentNullException.ThrowIfNull(assignments);
        if (workflowId == Guid.Empty || workflowRevision <= 0)
            throw new PortfolioResolutionException("WorkflowIdentityInvalid", "A workflow identity and positive revision are required.");
        if (asOfUtc.Kind != DateTimeKind.Utc)
            throw new PortfolioResolutionException("AsOfInvalid", "Resolution time must be UTC.");
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionHorizon);
        ArgumentException.ThrowIfNullOrWhiteSpace(underlyingRoot);
        if (!forSelection) ArgumentException.ThrowIfNullOrWhiteSpace(assetType);
        if (forSelection && decisionHorizon is not ("Daily" or "Weekly" or "Monthly"))
            throw new PortfolioResolutionException("DecisionHorizonInvalid", "Selection requires Daily, Weekly or Monthly.");
        if (!Enum.IsDefined(portfolio.OperatingState) || portfolio.OperatingState == PortfolioOperatingState.Unknown)
            throw new PortfolioResolutionException("PortfolioStateInvalid", "Portfolio operating state must be known.");

        if ((!forSelection && portfolio.OperatingState != PortfolioOperatingState.Active) || !IsEffective(portfolio.EffectiveFromUtc, portfolio.EffectiveUntilUtc, asOfUtc))
            throw new PortfolioResolutionException("PortfolioNotActive", "The Portfolio is not active and effective at the requested time.");
        if (financialPolicy.PortfolioId != portfolio.PortfolioId
            || financialPolicy.PolicyId != portfolio.ActivePolicyId
            || financialPolicy.PolicyVersion != portfolio.ActivePolicyVersion
            || financialPolicy.OperatingState != PortfolioFinancialPolicyState.Active)
            throw new PortfolioResolutionException("FinancialPolicyMismatch", "The exact Active Portfolio financial policy was not resolved.");
        if (financialPolicy.Validate(forActivation: true).Count != 0 || !IsEffective(financialPolicy.EffectiveFromUtc, financialPolicy.EffectiveUntilUtc, asOfUtc))
            throw new PortfolioResolutionException("FinancialPolicyInvalid", "The selected financial policy is invalid or not effective.");

        var matches = funds
            .Where(x => x.PortfolioId == portfolio.PortfolioId
                        && x.TradingYear == tradingYear
                        && string.Equals(x.DecisionHorizon, decisionHorizon, StringComparison.OrdinalIgnoreCase)
                        && (!forSelection ? x.OperatingState == FundOperatingState.Active : (!fundId.HasValue || x.FundId == fundId.Value))
                        && IsEffective(x.EffectiveFromUtc, x.EffectiveUntilUtc, asOfUtc)
                        && x.UnderlyingUniverse.Contains(underlyingRoot, StringComparer.OrdinalIgnoreCase)
                        && (forSelection || x.EligibleAssetTypes.Contains(assetType, StringComparer.OrdinalIgnoreCase)))
            .OrderBy(x => x.FundId)
            .ToArray();
        if (matches.Length == 0)
            throw new PortfolioResolutionException("ActiveFundMissing", "No active Fund matches the Portfolio, year, horizon, asset, and evaluation time.");
        if (matches.Length > 1)
            throw new PortfolioResolutionException("ActiveFundAmbiguous", "More than one active Fund matches the resolution key.");
        var fund = matches[0];
        if (!Enum.IsDefined(fund.OperatingState) || fund.OperatingState == FundOperatingState.Unknown)
            throw new PortfolioResolutionException("FundStateInvalid", "Fund operating state must be known.");

        var allocation = allocations
            .Where(x => x.PortfolioId == portfolio.PortfolioId && x.PortfolioVersion == portfolio.PortfolioVersion
                        && x.FundId == fund.FundId && x.FundMandateVersion == fund.FundMandateVersion
                        && IsEffective(x.EffectiveFromUtc, x.EffectiveUntilUtc, asOfUtc))
            .OrderByDescending(x => x.AllocationVersion)
            .FirstOrDefault()
            ?? throw new PortfolioResolutionException("FundAllocationMissing", "A current Fund allocation is required.");

        var envelope = envelopes
            .Where(x => x.PortfolioId == portfolio.PortfolioId && x.PortfolioVersion == portfolio.PortfolioVersion
                        && x.FundId == fund.FundId && x.FundMandateVersion == fund.FundMandateVersion
                        && x.EffectiveFromUtc <= asOfUtc && asOfUtc < x.ExpiresAtUtc)
            .OrderByDescending(x => x.EnvelopeVersion)
            .FirstOrDefault()
            ?? throw new PortfolioResolutionException("FundRiskEnvelopeMissing", "A current Fund risk envelope is required.");
        if (!forSelection && !envelope.PermitsNewExposureAt(asOfUtc))
            throw new PortfolioResolutionException("FundRiskEnvelopeBlocked", "The current Fund risk envelope does not permit new exposure.");

        var compatibleAssignments = assignments
            .Where(x => x.PortfolioId == portfolio.PortfolioId && x.PortfolioVersion == portfolio.PortfolioVersion
                        && x.FundId == fund.FundId && x.FundMandateVersion == fund.FundMandateVersion
                        && string.Equals(x.DecisionHorizon, decisionHorizon, StringComparison.OrdinalIgnoreCase)
                        && (forSelection || string.Equals(x.AssetType, assetType, StringComparison.OrdinalIgnoreCase))
                        && x.UnderlyingUniverse.Contains(underlyingRoot, StringComparer.OrdinalIgnoreCase)
                        && (forSelection ? IsEffective(x.EffectiveFromUtc, x.EffectiveUntilUtc, asOfUtc) : x.IsEffectiveAt(asOfUtc)))
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.TradeTemplateId)
            .ThenBy(x => x.AssignmentVersion)
            .Select(x => x.DefensiveCopy())
            .ToArray();
        if (forSelection) compatibleAssignments = compatibleAssignments.OrderBy(x=>x.Priority).ThenBy(x=>x.TradeTemplateId.ToString("D"),StringComparer.Ordinal).ThenBy(x=>x.AssignmentVersion).ToArray();
        if (!forSelection && compatibleAssignments.Length == 0)
            throw new PortfolioResolutionException("TemplateAssignmentMissing", "No enabled and effective template assignment matches the resolved Fund.");
        if (forSelection && compatibleAssignments.Length > 16)
            throw new PortfolioResolutionException("SelectionAssignmentsTooMany", "At most sixteen effective assignments may be frozen; assignments are never truncated.");
        if (forSelection && compatibleAssignments.GroupBy(x => x.TradeStrategyFamily?.CatalogDeployment).Any(x => x.Key is not null && x.Count() > 1))
            throw new PortfolioResolutionException("SelectionAssignmentAmbiguous", "An exact deployment has more than one effective assignment.");
        if (compatibleAssignments.Any(x => x.TradeSelectionHintProfileId == Guid.Empty || x.TradeSelectionHintProfileVersion <= 0
                                           || x.OrderCompositionProfileId == Guid.Empty || x.OrderCompositionProfileVersion <= 0))
            throw new PortfolioResolutionException("ProfileReferenceInvalid", "Every resolved assignment requires versioned selection-hint and composition profiles.");

        var validUntil = new[]
        {
            portfolio.EffectiveUntilUtc ?? DateTime.MaxValue,
            fund.EffectiveUntilUtc ?? DateTime.MaxValue,
            allocation.EffectiveUntilUtc ?? DateTime.MaxValue,
            envelope.ExpiresAtUtc,
            financialPolicy.EffectiveUntilUtc ?? DateTime.MaxValue,
            compatibleAssignments.Select(x => x.EffectiveUntilUtc ?? DateTime.MaxValue).DefaultIfEmpty(DateTime.MaxValue).Min(),
        }.Min();

        var unhashed = new PortfolioFundStrategySnapshot
        {
            WorkflowId = workflowId,
            WorkflowRevision = workflowRevision,
            CorrelationId = correlationId == Guid.Empty ? workflowId : correlationId,
            Portfolio = portfolio.DefensiveCopy(),
            FinancialPolicy = financialPolicy.DefensiveCopy(),
            Fund = fund.DefensiveCopy(),
            Allocation = allocation,
            RiskEnvelope = envelope,
            Assignments = compatibleAssignments,
            ResolvedAtUtc = asOfUtc,
            ValidUntilUtc = validUntil,
        };
        return unhashed with { PayloadSha256 = PortfolioCanonicalHash.Compute(unhashed) };
    }

    static bool IsEffective(DateTime from, DateTime? until, DateTime at) => from <= at && (until is null || at < until);
}
