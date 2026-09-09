using TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement;
using System.Collections.Immutable;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Model;

/// <summary>Enumerates the bounded feasible set without assuming monotonic margin or an offset from pending hedges.</summary>
public static class RiskSizingModel
{
    public static RiskSizingResult Calculate(RiskUnitResult unit, RiskSizingPolicy policy, RiskSizingAuthority authority,
        int liquidityUnits, ImmutableArray<RiskQuantityFunding> funding, decimal upstreamRiskMultiplier,
        CancellationToken cancellationToken = default, Action<RiskQuantityCheck>? inspect = null)
    {
        RiskUnitModel.Require(policy.Horizon is TimeFrameType.Daily or TimeFrameType.Weekly or TimeFrameType.Monthly
            && policy.MaximumUnits is > 0 and <= 100 && policy.PerTradeRiskFraction is > 0 and <= 1
            && upstreamRiskMultiplier is >= 0 and <= 1, "RM.POLICY.INVALID");
        RiskUnitModel.Require(authority.PortfolioId > 0 && authority.FundId > 0 && authority.RiskCapital >= 0
            && authority.PerTradeLossBudget >= 0 && authority.EvaluatedAtUtc.Kind == DateTimeKind.Utc
            && authority.ValidUntilUtc > authority.EvaluatedAtUtc && !string.IsNullOrWhiteSpace(authority.Environment)
            && unit.LossCharge > 0 && unit.GrossContracts > 0 && unit.GrossNotional > 0, "RM.AUTHORITY.INVALID");
        RiskUnitModel.Require(authority.Limits.Length is > 0 and <= 256 && authority.Usage.Length <= 10000
            && authority.Limits.Select(Key).Distinct().Count() == authority.Limits.Length
            && authority.Usage.Select(Key).Distinct().Count() == authority.Usage.Length
            && authority.Limits.All(x => x.Maximum >= 0), "RM.AUTHORITY.DUPLICATE_OR_INVALID_SCOPE");
        RiskUnitModel.Require(funding.Length <= 100 && funding.Select(x => x.StrategyUnits).Distinct().Count() == funding.Length,
            "RM.MARGIN.DUPLICATE_QUANTITY");
        int maximum = Math.Min(policy.MaximumUnits, Math.Max(0, liquidityUnits));
        decimal lossBudget = Math.Min(authority.PerTradeLossBudget,
            authority.RiskCapital * policy.PerTradeRiskFraction) * upstreamRiskMultiplier;
        var quotes = funding.ToDictionary(x => x.StrategyUnits);
        var limits = authority.Limits.ToDictionary(Key);
        var usage = authority.Usage.ToDictionary(Key);
        // Validate the whole required grid before selecting; a missing larger quote must not silently undersize.
        for (int quantity = 1; quantity <= maximum; quantity++)
        {
            RiskUnitModel.Require(quotes.TryGetValue(quantity, out var quote) && quote.MarginRequirement >= 0
                && quote.MarginFunding >= 0 && quote.EntryFees >= 0 && quote.VariationReserve >= 0,
                "RM.MARGIN.QUANTITY_MISSING");
            var evidence = quote!.Evidence;
            RiskUnitModel.Require(evidence.EvidenceId != Guid.Empty && evidence.Version > 0 && evidence.ContentHash.Length == 64
                && evidence.Environment == authority.Environment && evidence.ObservedAtUtc <= authority.EvaluatedAtUtc
                && (authority.EvaluatedAtUtc - evidence.ObservedAtUtc).TotalSeconds <= 60
                && evidence.ValidUntilUtc >= authority.ValidUntilUtc && !string.IsNullOrWhiteSpace(evidence.Source), "RM.MARGIN.EVIDENCE");
        }
        for (int quantity = maximum; quantity >= 1; quantity--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quote = quotes[quantity];
            var requirements = Requirements(unit, authority, quote);
            RiskUnitModel.Require(requirements.Exposures.All(x=>limits.ContainsKey(Key(x))),"RM.AUTHORITY.LIMIT_MISSING");
            decimal cash = requirements.SettlementCash + requirements.MarginFunding + requirements.FeeReserve + requirements.VariationReserve;
            bool fits = true;
            var checks = ImmutableArray.CreateBuilder<RiskLimitCheck>();
            foreach (var exposure in requirements.Exposures)
            {
                RiskUnitModel.Require(limits.TryGetValue(Key(exposure), out var limit), "RM.AUTHORITY.LIMIT_MISSING");
                var committed = usage.GetValueOrDefault(Key(exposure));
                // Each component can still reach either endpoint independently. Pending opposite orders are not hedges.
                decimal existing = committed is null ? 0 : Math.Abs(committed.Held) + Math.Abs(committed.Working) + Math.Abs(committed.Position);
                bool within = !limit!.Enabled || existing + Math.Abs(exposure.Amount) <= limit.Maximum;
                if (inspect is not null) checks.Add(new(authority.Limits.IndexOf(limit), exposure.Amount, existing, within));
                if (!within) fits = false;
            }
            inspect?.Invoke(new(quantity, cash, requirements.LossCharge, cash <= authority.AvailableCash,
                requirements.LossCharge <= lossBudget, checks.ToImmutable()));
            if (cash > authority.AvailableCash || requirements.LossCharge > lossBudget) continue;
            if (fits) return new(quantity, requirements, quote.Evidence, []);
        }
        return new(0, null, null, ["RM.CAPACITY.NO_FEASIBLE_QUANTITY"]);
    }

    /// <summary>Builds identical mandatory Portfolio, Fund and deployment measures and underlying Greek charges.</summary>
    public static CapacityRequirements Requirements(RiskUnitResult unit, RiskSizingAuthority authority, RiskQuantityFunding quote)
    {
        int quantity = quote.StrategyUnits;
        RiskUnitModel.Require(quantity is > 0 and <= 100, "RM.QUANTITY.INVALID");
        var result = new CapacityRequirements
        {
            Currency = "USD", SettlementCash = RiskUnitModel.CeilingMoney(unit.SettlementCash * quantity),
            MarginFunding = RiskUnitModel.CeilingMoney(quote.MarginFunding), FeeReserve = RiskUnitModel.CeilingMoney(quote.EntryFees),
            VariationReserve = RiskUnitModel.CeilingMoney(quote.VariationReserve),
            LossCharge = RiskUnitModel.CeilingMoney(unit.LossCharge * quantity
                + Math.Max(0,quote.EntryFees-unit.ComposerFeeReserve*quantity)),
            MarginRequirement = RiskUnitModel.CeilingMoney(quote.MarginRequirement),
            GrossNotional = RiskUnitModel.CeilingMoney(unit.GrossNotional * quantity),
            GrossContracts = checked(unit.GrossContracts * quantity), PositionSlots = 1, AccountingMethodVersion = 1
        };
        var values = new List<CapacityExposure>();
        Add(CapacityScopeKind.Portfolio, FinancialScopeKeys.Portfolio(authority.PortfolioId), false);
        Add(CapacityScopeKind.Fund, FinancialScopeKeys.Fund(authority.FundId), true);
        Add(CapacityScopeKind.Deployment, FinancialScopeKeys.Deployment(authority.DeploymentKey), true);
        Exposure(CapacityScopeKind.Underlying, authority.UnderlyingId, CapacityMeasure.Delta, CapacityUnit.NormalizedDelta, unit.Delta * quantity);
        Exposure(CapacityScopeKind.Underlying, authority.UnderlyingId, CapacityMeasure.Gamma, CapacityUnit.NormalizedGamma, unit.Gamma * quantity);
        Exposure(CapacityScopeKind.Underlying, authority.UnderlyingId, CapacityMeasure.Vega, CapacityUnit.NormalizedVega, unit.VegaPerPoint * quantity);
        result = result with { Exposures = values.ToArray() };
        return result with { ContentHash = FinancialCanonicalHash.Requirements(result) };

        void Add(CapacityScopeKind kind, string key, bool contracts)
        {
            Exposure(kind, key, CapacityMeasure.SettlementCash, CapacityUnit.Usd,
                result.SettlementCash + result.MarginFunding + result.FeeReserve + result.VariationReserve);
            Exposure(kind, key, CapacityMeasure.LossCharge, CapacityUnit.Usd, result.LossCharge);
            Exposure(kind, key, CapacityMeasure.Margin, CapacityUnit.Usd, result.MarginRequirement);
            Exposure(kind, key, CapacityMeasure.GrossNotional, CapacityUnit.Usd, result.GrossNotional);
            Exposure(kind, key, CapacityMeasure.PositionSlots, CapacityUnit.Positions, 1);
            if (contracts) Exposure(kind, key, CapacityMeasure.GrossContracts, CapacityUnit.Contracts, result.GrossContracts);
        }
        void Exposure(CapacityScopeKind kind, string key, CapacityMeasure measure, CapacityUnit units, decimal amount)
            => values.Add(new() { ScopeKind = kind, ScopeKey = key, Measure = measure, Unit = units, Amount = amount, MethodVersion = 1 });
    }

    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityLimit x) => (x.ScopeKind, x.ScopeKey, x.Measure, x.Unit);
    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityUsed x) => (x.ScopeKind, x.ScopeKey, x.Measure, x.Unit);
    static (CapacityScopeKind, string, CapacityMeasure, CapacityUnit) Key(CapacityExposure x) => (x.ScopeKind, x.ScopeKey, x.Measure, x.Unit);
}
