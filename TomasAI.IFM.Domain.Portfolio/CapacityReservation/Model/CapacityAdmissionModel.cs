using TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;

namespace TomasAI.IFM.Domain.Portfolio.CapacityReservation.Model;

/// <summary>Pure admission against a current fenced snapshot. Zero is capacity zero, never unlimited.</summary>
public static class CapacityAdmissionModel
{
    /// <summary>Cross-checks the request against independent committed Risk evidence and current Portfolio authority.</summary>
    public static void ValidateCommitted(CapacityReservationRequest request, QualifiedCapacityAssessment assessment,
        FinancialBookConfiguration book, decimal availableCash, IReadOnlyList<CapacityUsed> usage, DateTime nowUtc)
    {
        var fund = book.Funds.SingleOrDefault(x => x.FundId == request.FundId);
        var deployment=fund?.Deployments.SingleOrDefault(x=>x.Reference.DeploymentKey==request.Authority.DeploymentKey);
        var reference=fund?.Deployments.Length>0?deployment?.Reference:fund?.Reference;
        Require(fund is not null && assessment.Eligible && assessment.PortfolioId == book.PortfolioId &&
            assessment.FundId == request.FundId && assessment.InvocationId == request.RiskInvocationId &&
            assessment.ResultId == request.RiskResultId && assessment.ResultHash == request.RiskAssessmentHash &&
            assessment.WorkflowId == request.WorkflowId && assessment.WorkflowRevision == request.InputWorkflowRevision &&
            assessment.CompositionResultId == request.CompositionResultId && assessment.CompositionResultHash == request.CompositionResultHash &&
            assessment.UnitCandidateHash == request.UnitCandidateHash && assessment.SizedOrderHash == request.SizedOrderHash &&
            assessment.StrategyUnits == request.StrategyUnits && assessment.Environment == request.ExecutionEnvironment &&
            assessment.ValidUntilUtc >= request.ValidUntilUtc && assessment.ValidUntilUtc > nowUtc &&
            assessment.Authority == request.Authority && request.Authority == reference &&
            request.Authority.AuthorityEpoch == book.AuthorityEpoch &&
            request.Authority.SourceWatermark == book.SourceWatermark && request.Authority.ValuationWatermark == book.ValuationWatermark,
            FinancialReasons.AuthorityDenied, "Reservation does not match committed Risk evidence/current financial authority.");
        var margin = request.MarginEvidenceReference;
        Require(margin == assessment.MarginEvidence && margin.EvidenceId != Guid.Empty && margin.Version > 0 &&
            margin.ContentHash.Length == 64 && margin.Environment == book.Environment && margin.ObservedAtUtc <= nowUtc &&
            margin.ValidUntilUtc >= request.ValidUntilUtc && margin.ValidUntilUtc > nowUtc,
            FinancialReasons.AuthorityDenied, "Qualified margin evidence must cover the complete reservation lifetime.");
        Require(request.TradeIds.Length > 0 && request.TradeIds.All(x => x > 0) && request.TradeIds.Distinct().Count() == request.TradeIds.Length,
            FinancialReasons.InvalidContract, "Reservation requires unique allocated trade identities.");
        if(deployment is not null)
        {
            Require(deployment.MaximumRiskPerTrade>0 && request.Requirements.LossCharge<=deployment.MaximumRiskPerTrade,
                FinancialReasons.InsufficientCapacity,"Trade loss exceeds its exact deployment per-trade cap.");
            ValidateScopeVector(request.Requirements,CapacityScopeKind.Portfolio,FinancialScopeKeys.Portfolio(book.PortfolioId),false);
            ValidateScopeVector(request.Requirements,CapacityScopeKind.Fund,FinancialScopeKeys.Fund(request.FundId),true);
            ValidateScopeVector(request.Requirements,CapacityScopeKind.Deployment,FinancialScopeKeys.Deployment(request.Authority.DeploymentKey),true);
        }
        Validate(request, assessment.Requirements, availableCash, 0, 0,
            deployment is null?fund!.Limits:[..fund!.Limits,..deployment.Limits], usage, nowUtc);
    }

    public static decimal Funding(CapacityRequirements value) => checked(value.SettlementCash + value.MarginFunding + value.FeeReserve + value.VariationReserve);

    /// <summary>Prevents an otherwise valid requirement hash from omitting an aggregate cap or charging a different amount.</summary>
    public static void ValidateScopeVector(CapacityRequirements requirements,CapacityScopeKind scope,string key,bool contracts)
    {
        Check(CapacityMeasure.SettlementCash,CapacityUnit.Usd,Funding(requirements));
        Check(CapacityMeasure.LossCharge,CapacityUnit.Usd,requirements.LossCharge);
        Check(CapacityMeasure.Margin,CapacityUnit.Usd,requirements.MarginRequirement);
        Check(CapacityMeasure.GrossNotional,CapacityUnit.Usd,requirements.GrossNotional);
        Check(CapacityMeasure.PositionSlots,CapacityUnit.Positions,requirements.PositionSlots);
        if(contracts) Check(CapacityMeasure.GrossContracts,CapacityUnit.Contracts,requirements.GrossContracts);
        void Check(CapacityMeasure measure,CapacityUnit unit,decimal amount)
        {
            var values=requirements.Exposures.Where(x=>x.ScopeKind==scope && x.ScopeKey==key && x.Measure==measure && x.Unit==unit).ToArray();
            Require(values.Length==1 && values[0].Amount==amount,FinancialReasons.InvalidContract,$"Incomplete or inconsistent {scope}/{measure} exposure.");
        }
    }

    public static string Hash(CapacityRequirements value) => FinancialCanonicalHash.Requirements(value);

    public static void Validate(CapacityReservationRequest request, CapacityRequirements independentlyDerived,
        decimal settledCash, decimal pendingWithdrawals, decimal reservedFunding,
        IReadOnlyList<CapacityLimit> limits, IReadOnlyList<CapacityUsed> usage, DateTime nowUtc)
    {
        Require(nowUtc.Kind == DateTimeKind.Utc && request.ValidUntilUtc.Kind == DateTimeKind.Utc && nowUtc < request.ValidUntilUtc,
            FinancialReasons.ReservationExpired, "Reservation evidence expired.");
        Require(request.ReservationId != Guid.Empty && request.FundId > 0 && request.BookId > 0 && request.OrderId > 0 && request.StrategyUnits > 0,
            FinancialReasons.InvalidContract, "Reservation identities and whole strategy units are required.");
        var proposed = request.Requirements;
        Require(proposed.Currency == "USD" && independentlyDerived.Currency == "USD",
            FinancialReasons.UnsupportedCurrency, "Only USD capacity is supported.");
        Require(proposed.AccountingMethodVersion > 0 && proposed.ContentHash == Hash(proposed) && Hash(proposed) == Hash(independentlyDerived),
            FinancialReasons.RequestMismatch, "Requirement vector must match independent qualified risk/accounting calculation.");
        Require(new[] { proposed.SettlementCash, proposed.MarginFunding, proposed.FeeReserve, proposed.VariationReserve,
            proposed.LossCharge, proposed.MarginRequirement, proposed.GrossNotional }.All(x => x >= 0) &&
            proposed.GrossContracts > 0 && proposed.PositionSlots == 1 && pendingWithdrawals >= 0 && reservedFunding >= 0,
            FinancialReasons.InvalidContract, "Funding/counts must be valid nonnegative financial measures.");
        var required = Funding(proposed);
        Require(settledCash - pendingWithdrawals - reservedFunding >= required,
            FinancialReasons.InsufficientCash, "Settled cash is already committed or insufficient.");
        Require(proposed.Exposures.Length > 0 && proposed.Exposures.Length <= 256 &&
            proposed.Exposures.Select(Key).Distinct().Count() == proposed.Exposures.Length &&
            usage.Select(Key).Distinct().Count() == usage.Count && limits.Select(Key).Distinct().Count() == limits.Count,
            FinancialReasons.InvalidContract, "Each exposure/usage/limit must have one unambiguous scope and unit.");
        Require(proposed.Exposures.All(x => x.ScopeKind != CapacityScopeKind.Undefined && Enum.IsDefined(x.ScopeKind) &&
            x.Measure != CapacityMeasure.Undefined && Enum.IsDefined(x.Measure) && x.Unit != CapacityUnit.Undefined &&
            Enum.IsDefined(x.Unit) && x.MethodVersion == proposed.AccountingMethodVersion && !string.IsNullOrWhiteSpace(x.ScopeKey)),
            FinancialReasons.InvalidContract, "Unknown capacity scope/measure/unit/method.");
        foreach (var exposure in proposed.Exposures)
        {
            var limit = limits.SingleOrDefault(x => Key(x) == Key(exposure));
            Require(limit is not null && limit.Maximum >= 0, FinancialReasons.AuthorityDenied, "Capacity limit configuration is missing.");
            if (!limit!.Enabled) continue;
            var used = usage.SingleOrDefault(x => Key(x) == Key(exposure));
            var committed = used is null ? 0 : Math.Abs(used.Held) + Math.Abs(used.Working) + Math.Abs(used.Position);
            Require(committed + Math.Abs(exposure.Amount) <= limit.Maximum,
                FinancialReasons.InsufficientCapacity, $"Capacity exceeded for {exposure.ScopeKind}/{exposure.Measure}.");
        }
    }

    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityExposure x) => (x.ScopeKind,x.ScopeKey,x.Measure,x.Unit);
    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityLimit x) => (x.ScopeKind,x.ScopeKey,x.Measure,x.Unit);
    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityUsed x) => (x.ScopeKind,x.ScopeKey,x.Measure,x.Unit);
    static void Require(bool condition, int code, string message)
    { if (!condition) throw new FinancialOperationException(code, message); }
}
