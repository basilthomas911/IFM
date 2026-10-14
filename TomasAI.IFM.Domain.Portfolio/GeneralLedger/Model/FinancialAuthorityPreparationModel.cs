using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Financial.Repository;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Derives exact spending caps from committed policy, mandates, assignments and envelopes; missing authority stays disabled.</summary>
public static class FinancialAuthorityPreparationModel
{
    public static FinancialAuthorityDraft Create(FinancialAuthorityPreparationSnapshot financial, PortfolioAggregate portfolio,
        IReadOnlyDictionary<int, PortfolioFundAggregate> funds, PortfolioFinancialPolicyAggregate? policies,
        IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> catalog, bool permitNewSpending, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(financial);
        ArgumentNullException.ThrowIfNull(funds);
        ArgumentNullException.ThrowIfNull(portfolio);
        if (portfolio.Current is null || portfolio.IsDeleted || now.Kind != DateTimeKind.Utc) throw new InvalidOperationException("Current Portfolio authority is required.");
        var book = financial.Book; var notes = new List<string>(); var prepared = new List<FinancialFundAuthority>();
        var policy = policies?.Versions.SingleOrDefault(x => x.PolicyId == portfolio.Current.ActivePolicyId && x.PolicyVersion == portfolio.Current.ActivePolicyVersion);
        var sourceCut = $"Portfolio:{book.PortfolioId}:{portfolio.Revision};Financial:{financial.Revision};Epoch:{financial.Epoch}";
        var valuationCut = $"CashBasis:{financial.Revision}";
        var snapshotHash = FinancialCanonicalHash.Compute(new { financial.Revision, financial.Epoch, financial.AvailableCash, sourceCut, valuationCut });
        foreach (var owned in book.Funds.OrderBy(x => x.FundId))
            prepared.Add(PrepareFund(owned, financial, portfolio, funds[owned.FundId], policies, policy,
                catalog, permitNewSpending, now, sourceCut, valuationCut, snapshotHash, notes));
        // A shared product bucket has one cap across Funds. Use the most restrictive enabled mandate cap.
        var sharedProducts = prepared.SelectMany(x => x.Limits).Where(x => x.ScopeKind == CapacityScopeKind.Underlying)
            .GroupBy(x => (x.ScopeKey, x.Measure, x.Unit)).ToDictionary(x => x.Key, x =>
            {
                var enabled = x.Where(v => v.Enabled).ToArray();
                return x.First() with { Enabled = enabled.Length > 0, Maximum = enabled.Length > 0 ? enabled.Min(v => v.Maximum) : 0 };
            });
        prepared = prepared.Select(x => x with { Limits = x.Limits.Select(v => v.ScopeKind == CapacityScopeKind.Underlying ? sharedProducts[(v.ScopeKey, v.Measure, v.Unit)] : v).ToArray() }).ToList();
        var result = book with { Funds = prepared.ToArray(), AuthorityEpoch = financial.Epoch, SourceWatermark = sourceCut, ValuationWatermark = valuationCut };
        FinancialAuthorityModel.Validate(result, portfolio, funds, policies is null ? new Dictionary<int, PortfolioFinancialPolicyAggregate>() : new() { [policy?.PolicyId ?? 0] = policies }, now);
        return new(new() { Action = LedgerConfigurationAction.RefreshAuthority, BookId = book.BookId, Book = result, SourceCut = sourceCut }, notes.ToArray());
    }
    /// <summary>Derives one fund's exact deployment caps from its active mandate, selected policy, risk envelope and published catalog assignments.</summary>
    static FinancialFundAuthority PrepareFund(FinancialFundAuthority owned, FinancialAuthorityPreparationSnapshot financial,
        PortfolioAggregate portfolio, PortfolioFundAggregate fund, PortfolioFinancialPolicyAggregate? policies,
        PortfolioFinancialPolicyReadModel? policy, IReadOnlyDictionary<CatalogKey, StoredStrategyCatalogDefinition> catalog,
        bool permitNewSpending, DateTime now, string sourceCut, string valuationCut, string snapshotHash, List<string> notes)
    {
        var book = financial.Book;
        var mandate = fund.Current;
            if (mandate is null || !portfolio.FundIds.Contains(owned.FundId)) throw new InvalidOperationException("Financial membership no longer matches a current Fund.");
            var disabled = DisableSpending(owned, financial, portfolio, fund, policies);
            bool active = permitNewSpending && book.MigrationQualified && financial.State is not ("Overdrawn" or "NeedsReconciliation") &&
                portfolio.Current.OperatingState == PortfolioOperatingState.Active && mandate.OperatingState == FundOperatingState.Active &&
                Effective(portfolio.Current.EffectiveFromUtc, portfolio.Current.EffectiveUntilUtc, now) && Effective(mandate.EffectiveFromUtc, mandate.EffectiveUntilUtc, now) &&
                policy?.OperatingState == PortfolioFinancialPolicyState.Active && Effective(policy.EffectiveFromUtc, policy.EffectiveUntilUtc, now);
            var envelope = portfolio.RiskEnvelopes(owned.FundId).Where(x => x.PortfolioVersion == portfolio.Current.PortfolioVersion && x.FundMandateVersion == mandate.FundMandateVersion &&
                x.SourcePolicyId == policy?.PolicyId && x.SourcePolicyVersion == policy?.PolicyVersion && x.PermitsNewExposureAt(now)).OrderByDescending(x => x.EnvelopeVersion).FirstOrDefault();
            if (!active || envelope is null) { notes.Add($"{mandate.Name}: new spending disabled; active qualified policy, mandate and envelope are required."); return disabled; }
            var shared = new List<CapacityLimit>();
            Limits(shared, CapacityScopeKind.Portfolio, FinancialScopeKeys.Portfolio(book.PortfolioId), policy!.MaximumDeployableCapital, policy.MaximumAggregateRisk, policy.MaximumMargin, policy.MaximumGrossNotional, policy.MaximumOpenPositions, null);
            Limits(shared, CapacityScopeKind.Fund, FinancialScopeKeys.Fund(owned.FundId), envelope.AvailableCapital, Math.Min(envelope.MaximumAggregateRisk, envelope.RemainingLossBudget), envelope.MaximumMargin, envelope.MaximumGrossNotional, envelope.MaximumOpenPositions, envelope.MaximumContracts);
            var deployments = new List<FinancialDeploymentAuthority>();
            var assignments = fund.Assignments.Where(x => x.IsEffectiveAt(now) && x.FundMandateVersion == mandate.FundMandateVersion && x.PortfolioVersion == portfolio.Current.PortfolioVersion &&
                x.TradeStrategyFamily?.CatalogDeployment is not null).GroupBy(x => x.TradeStrategyFamily!.CatalogDeployment!).Select(x => x.OrderByDescending(a => a.AssignmentVersion).First());
            var underlyingLimits = new Dictionary<(string, CapacityMeasure), CapacityLimit>();
            foreach (var assignment in assignments.OrderBy(x => x.TradeTemplateId).ThenBy(x => x.TradeTemplateVersion))
            {
                var key = assignment.TradeStrategyFamily!.CatalogDeployment!;
                if (!mandate.PermittedTradeStrategyFamilies.Any(x => x.CatalogDeployment == key) || !catalog.TryGetValue(key, out var definition) ||
                    definition.Status != CatalogLifecycleStatus.Published || definition.EffectiveFromUtc is null || definition.EffectiveFromUtc > now || definition.RetiredAtUtc <= now ||
                    definition.Definition.Products.Length == 0 || !policy.TradeFamilyLimits.Any(x => x.CatalogDeployment == key)) continue;
                var products = definition.Definition.Products.Where(x => mandate.UnderlyingUniverse.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase) && assignment.UnderlyingUniverse.Contains(x.Symbol, StringComparer.OrdinalIgnoreCase)).ToArray();
                if (products.Length == 0) continue;
                var caps = policy.ResolveEffectiveCaps(key, envelope, now); if (!caps.PermitsNewExposure) continue;
                var expires = new[] { now.AddMinutes(5),envelope.ExpiresAtUtc,portfolio.Current.EffectiveUntilUtc??DateTime.MaxValue,
                    mandate.EffectiveUntilUtc??DateTime.MaxValue,policy.EffectiveUntilUtc??DateTime.MaxValue,assignment.EffectiveUntilUtc??DateTime.MaxValue,
                    definition.RetiredAtUtc??DateTime.MaxValue }.Min();
                var reference = CreateAuthorityReference(financial, portfolio.Current, mandate, policy, envelope,
                    assignment, key, sourceCut, valuationCut, snapshotHash, expires);
                var limits = new List<CapacityLimit>();
                Limits(limits, CapacityScopeKind.Deployment, FinancialScopeKeys.Deployment(key), Math.Min(policy.MaximumDeployableCapital, envelope.AvailableCapital),
                    caps.MaximumAggregateRisk, caps.MaximumMargin, caps.MaximumGrossNotional, caps.MaximumOpenPositions, envelope.MaximumContracts);
                AddUnderlyingLimits(products, envelope, underlyingLimits);
                deployments.Add(new(reference, limits.ToArray(), caps.MaximumRiskPerTrade));
            }
            shared.AddRange(underlyingLimits.Values.OrderBy(x => x.ScopeKey, StringComparer.Ordinal).ThenBy(x => x.Measure));
            if (deployments.Count == 0) { notes.Add($"{mandate.Name}: no effective assigned and published deployment is ready."); return disabled; }
            if (shared.Count > 128 || deployments.Count > 128) throw new InvalidOperationException("Prepared financial authority exceeds its supported bound.");
            return EnablePreparedSpending(disabled, deployments, shared, mandate, policy, envelope, notes);


    }

    /// <summary>Enables the completely validated deployment set and records its exact policy and envelope versions.</summary>
    static FinancialFundAuthority EnablePreparedSpending(FinancialFundAuthority disabled,
        List<FinancialDeploymentAuthority> deployments, List<CapacityLimit> shared, FundMandateReadModel mandate,
        PortfolioFinancialPolicyReadModel policy, FundRiskEnvelopeReadModel envelope, List<string> notes)
    {
            var prepared = disabled with { CanSpend = true, Reference = deployments[0].Reference, Limits = shared.ToArray(), Deployments = deployments.ToArray() };
            notes.Add($"{mandate.Name}: {deployments.Count} exact deployment(s); caps from policy {policy.PolicyVersion}, envelope {envelope.EnvelopeVersion}.");
            return prepared;
    }

    /// <summary>Freezes each eligible product's absolute delta, gamma and vega caps, with missing caps explicitly disabled.</summary>
    static void AddUnderlyingLimits(CatalogProduct[] products, FundRiskEnvelopeReadModel envelope,
        Dictionary<(string, CapacityMeasure), CapacityLimit> underlyingLimits)
    {
                foreach (var product in products)
                {
                    var underlying = FinancialScopeKeys.Underlying(product.Symbol, product.Exchange, product.Currency);
                    Greek(underlying, CapacityMeasure.Delta, CapacityUnit.NormalizedDelta, envelope.MaximumAbsoluteDelta);
                    Greek(underlying, CapacityMeasure.Gamma, CapacityUnit.NormalizedGamma, envelope.MaximumAbsoluteGamma);
                    Greek(underlying, CapacityMeasure.Vega, CapacityUnit.NormalizedVega, envelope.MaximumAbsoluteVega);
                }

            void Greek(string key, CapacityMeasure measure, CapacityUnit unit, decimal? maximum)
                => underlyingLimits[(key, measure)] = new(CapacityScopeKind.Underlying, key, measure, unit, maximum ?? 0, maximum is not null);
    }

    /// <summary>Clears spending permissions and deployments while preserving the current stream revisions.</summary>
    static FinancialFundAuthority DisableSpending(FinancialFundAuthority owned, FinancialAuthorityPreparationSnapshot financial,
        PortfolioAggregate portfolio, PortfolioFundAggregate fund, PortfolioFinancialPolicyAggregate? policies)
    {
            return owned with
            {
                CanSpend = false,
                PortfolioStreamVersion = portfolio.Revision,
                FundStreamVersion = fund.Revision,
                PolicyStreamVersion = policies?.Revision ?? 0,
                Reference = new() { AuthorityEpoch = financial.Epoch },
                Deployments = [],
                Limits = []
            };
    }

    /// <summary>Freezes every exact policy, mandate, envelope, assignment and source-watermark version used to authorize the deployment.</summary>
    static FinancialAuthorityReference CreateAuthorityReference(FinancialAuthorityPreparationSnapshot financial,
        PortfolioReadModel portfolio, FundMandateReadModel mandate, PortfolioFinancialPolicyReadModel policy,
        FundRiskEnvelopeReadModel envelope, FundTradeTemplateAssignmentReadModel assignment, CatalogKey key,
        string sourceCut, string valuationCut, string snapshotHash, DateTime expires)
    {
        return new FinancialAuthorityReference
                {
                    PortfolioVersion = portfolio.PortfolioVersion,
                    FundMandateVersion = mandate.FundMandateVersion,
                    PolicyId = policy.PolicyId,
                    PolicyVersion = policy.PolicyVersion,
                    EnvelopeId = envelope.EnvelopeId,
                    EnvelopeVersion = envelope.EnvelopeVersion,
                    AssignmentVersion = assignment.AssignmentVersion,
                    DeploymentKey = key,
                    AuthorityEpoch = financial.Epoch,
                    SourceWatermark = sourceCut,
                    ValuationWatermark = valuationCut,
                    FinancialSnapshotHash = snapshotHash,
                    ValidUntilUtc = expires
                };
    }

    static void Limits(List<CapacityLimit> target, CapacityScopeKind scope, string key, decimal funding, decimal loss, decimal margin, decimal notional, int positions, int? contracts)
    {
        target.Add(new(scope, key, CapacityMeasure.SettlementCash, CapacityUnit.Usd, funding)); target.Add(new(scope, key, CapacityMeasure.LossCharge, CapacityUnit.Usd, loss));
        target.Add(new(scope, key, CapacityMeasure.Margin, CapacityUnit.Usd, margin)); target.Add(new(scope, key, CapacityMeasure.GrossNotional, CapacityUnit.Usd, notional));
        target.Add(new(scope, key, CapacityMeasure.PositionSlots, CapacityUnit.Positions, positions)); if (contracts is { } count) target.Add(new(scope, key, CapacityMeasure.GrossContracts, CapacityUnit.Contracts, count));
    }
    static bool Effective(DateTime start, DateTime? end, DateTime now) => start <= now && (end is null || end > now);
}
