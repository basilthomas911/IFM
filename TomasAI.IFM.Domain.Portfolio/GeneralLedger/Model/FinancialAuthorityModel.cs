using System.Globalization;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.GeneralLedger.Model;

/// <summary>Validates proposed spending authority against committed business definitions; stream checks close the read/write race.</summary>
public static class FinancialAuthorityModel
{
    public static void Validate(FinancialBookConfiguration book,PortfolioAggregate portfolio,
        IReadOnlyDictionary<int,PortfolioFundAggregate> funds,IReadOnlyDictionary<int,PortfolioFinancialPolicyAggregate> policies,DateTime now)
    {
        Require(portfolio.Current is { } && !portfolio.IsDeleted && portfolio.Current.PortfolioId==book.PortfolioId && portfolio.Current.BaseCurrency==book.Currency,
            "Book must use an existing Portfolio and its currency.");
        Require(portfolio.Current!.BrokerAccountRefs.Contains(book.ExecutionAccountReference,StringComparer.Ordinal),"Execution account is not assigned to this Portfolio.");
        foreach(var proposed in book.Funds)
        {
            Require(portfolio.FundIds.Contains(proposed.FundId) && funds.TryGetValue(proposed.FundId,out var found) && found.Current?.PortfolioId==book.PortfolioId,
                "Book contains a Fund outside this Portfolio.");
            var fund=funds[proposed.FundId];
            Require(proposed.PortfolioStreamVersion==portfolio.Revision && proposed.FundStreamVersion==fund.Revision,
                "Prepared Portfolio/Fund source versions are stale.");
            if(!proposed.CanSpend) continue;
            Require(book.MigrationQualified && portfolio.Current.OperatingState==PortfolioOperatingState.Active &&
                fund.Current!.OperatingState==FundOperatingState.Active && !fund.Current.IsLegacyHistory &&
                Effective(portfolio.Current.EffectiveFromUtc,portfolio.Current.EffectiveUntilUtc,now) &&
                Effective(fund.Current.EffectiveFromUtc,fund.Current.EffectiveUntilUtc,now),"Portfolio/Fund is not active and effective.");
            Require(policies.TryGetValue(proposed.Reference.PolicyId,out var value) && value.Current is not null,"Financial policy does not exist.");
            var aggregate=policies[proposed.Reference.PolicyId];
            var policy=aggregate.Versions.SingleOrDefault(x=>x.PolicyVersion==portfolio.Current.ActivePolicyVersion);
            Require(policy is not null && policy.PolicyId==portfolio.Current.ActivePolicyId && policy.OperatingState==PortfolioFinancialPolicyState.Active &&
                policy.BaseCurrency=="USD" && Effective(policy.EffectiveFromUtc,policy.EffectiveUntilUtc,now) && proposed.PolicyStreamVersion==aggregate.Revision,
                "Exact active Portfolio policy and source revision are required.");
            Require(proposed.Deployments.Length>0 && proposed.Deployments.Select(x=>x.Reference.DeploymentKey).Distinct().Count()==proposed.Deployments.Length,
                "Spendable Funds require distinct exact deployment authorities.");
            foreach(var deployment in proposed.Deployments)
            {
                var reference=deployment.Reference;
                Require(reference.PortfolioVersion==portfolio.Current.PortfolioVersion && reference.FundMandateVersion==fund.Current!.FundMandateVersion &&
                    reference.PolicyId==policy!.PolicyId && reference.PolicyVersion==policy.PolicyVersion && reference.AuthorityEpoch==book.AuthorityEpoch &&
                    reference.SourceWatermark==book.SourceWatermark && reference.ValuationWatermark==book.ValuationWatermark && reference.ValidUntilUtc>now &&
                    reference.FinancialSnapshotHash.Length==64,"Financial reference does not match committed source definitions.");
                Require(fund.Current!.PermittedTradeStrategyFamilies.Any(x=>x.CatalogDeployment==reference.DeploymentKey),"Deployment is not authorized by the Fund mandate.");
                var assignment=fund.Assignments.SingleOrDefault(x=>x.AssignmentVersion==reference.AssignmentVersion && x.TradeStrategyFamily?.CatalogDeployment==reference.DeploymentKey);
                Require(assignment is not null && assignment.IsEffectiveAt(now) && assignment.FundMandateVersion==reference.FundMandateVersion && assignment.PortfolioVersion==reference.PortfolioVersion,
                    "Exact effective Fund deployment assignment is required.");
                var envelope=portfolio.RiskEnvelopes(proposed.FundId).SingleOrDefault(x=>x.EnvelopeId==reference.EnvelopeId && x.EnvelopeVersion==reference.EnvelopeVersion);
                Require(envelope is not null && envelope.PermitsNewExposureAt(now) && envelope.FundMandateVersion==reference.FundMandateVersion &&
                    envelope.PortfolioVersion==reference.PortfolioVersion && reference.ValidUntilUtc<=envelope.ExpiresAtUtc,"Exact unexpired Fund envelope is required.");
                Require(reference.ValidUntilUtc<=(portfolio.Current.EffectiveUntilUtc??DateTime.MaxValue) &&
                    reference.ValidUntilUtc<=(fund.Current.EffectiveUntilUtc??DateTime.MaxValue) &&
                    reference.ValidUntilUtc<=(policy!.EffectiveUntilUtc??DateTime.MaxValue) &&
                    reference.ValidUntilUtc<=(assignment!.EffectiveUntilUtc??DateTime.MaxValue),"Authority outlives its effective source definitions.");
                foreach(var limit in proposed.Limits.Where(x=>x.ScopeKind==CapacityScopeKind.Underlying))
                {
                    var maximum=limit.Measure switch { CapacityMeasure.Delta=>envelope!.MaximumAbsoluteDelta,
                        CapacityMeasure.Gamma=>envelope!.MaximumAbsoluteGamma,CapacityMeasure.Vega=>envelope!.MaximumAbsoluteVega,_=>null };
                    var unit=limit.Measure switch { CapacityMeasure.Delta=>CapacityUnit.NormalizedDelta,
                        CapacityMeasure.Gamma=>CapacityUnit.NormalizedGamma,CapacityMeasure.Vega=>CapacityUnit.NormalizedVega,_=>CapacityUnit.Undefined };
                    Require(unit!=CapacityUnit.Undefined && limit.Unit==unit && limit.Maximum>=0 && limit.ScopeKey.StartsWith("U1:",StringComparison.Ordinal) &&
                        (maximum is null || limit.Enabled && limit.Maximum<=maximum),"Underlying authority exceeds the normalized envelope constraint.");
                }
                var caps=policy!.ResolveEffectiveCaps(reference.DeploymentKey,envelope!,now);
                Require(caps.PermitsNewExposure,"Policy disables this deployment.");
                Require(deployment.MaximumRiskPerTrade>0 && deployment.MaximumRiskPerTrade<=caps.MaximumRiskPerTrade,
                    "Deployment per-trade loss limit is missing or exceeds its exact policy/envelope cap.");
                ValidateLimits(proposed.Limits,CapacityScopeKind.Portfolio,book.PortfolioId.ToString(CultureInfo.InvariantCulture),
                    policy.MaximumDeployableCapital,policy.MaximumAggregateRisk,policy.MaximumMargin,policy.MaximumGrossNotional,policy.MaximumOpenPositions,null);
                ValidateLimits(proposed.Limits,CapacityScopeKind.Fund,proposed.FundId.ToString(CultureInfo.InvariantCulture),
                    envelope!.AvailableCapital,Math.Min(envelope.MaximumAggregateRisk,envelope.RemainingLossBudget),envelope.MaximumMargin,envelope.MaximumGrossNotional,envelope.MaximumOpenPositions,envelope.MaximumContracts);
                ValidateLimits(deployment.Limits,CapacityScopeKind.Deployment,FinancialScopeKeys.Deployment(reference.DeploymentKey),
                    Math.Min(policy.MaximumDeployableCapital,envelope.AvailableCapital),caps.MaximumAggregateRisk,caps.MaximumMargin,caps.MaximumGrossNotional,caps.MaximumOpenPositions,envelope.MaximumContracts);
            }
            Require(proposed.Deployments.Any(x=>x.Reference==proposed.Reference),"Primary policy reference must be one of the exact deployment authorities.");
        }
        foreach(var shared in book.Funds.Where(x=>x.CanSpend).SelectMany(x=>x.Limits).Where(x=>x.ScopeKind is CapacityScopeKind.Portfolio or CapacityScopeKind.Underlying)
            .GroupBy(x=>(x.ScopeKind,x.ScopeKey,x.Measure,x.Unit)))
            Require(shared.Select(x=>(x.Enabled,x.Maximum)).Distinct().Count()==1,"Shared capacity scopes require one consistent cap across Funds.");
    }

    static void ValidateLimits(CapacityLimit[] limits,CapacityScopeKind scope,string key,decimal funding,decimal loss,decimal margin,decimal notional,int positions,int? contracts)
    {
        Check(CapacityMeasure.SettlementCash,CapacityUnit.Usd,funding);
        Check(CapacityMeasure.LossCharge,CapacityUnit.Usd,loss);
        Check(CapacityMeasure.Margin,CapacityUnit.Usd,margin);
        Check(CapacityMeasure.GrossNotional,CapacityUnit.Usd,notional);
        Check(CapacityMeasure.PositionSlots,CapacityUnit.Positions,positions);
        if(contracts is { } count) Check(CapacityMeasure.GrossContracts,CapacityUnit.Contracts,count);
        void Check(CapacityMeasure measure,CapacityUnit unit,decimal maximum)
        {
            var selected=limits.Where(x=>x.ScopeKind==scope && x.ScopeKey==key && x.Measure==measure && x.Unit==unit).ToArray();
            Require(selected.Length==1 && selected[0].Enabled && selected[0].Maximum>=0 && selected[0].Maximum<=maximum,
                $"Missing or excessive {scope}/{measure} authority.");
        }
    }
    static bool Effective(DateTime from,DateTime? until,DateTime now)=>now>=from && (until is null || now<until);
    static void Require(bool condition,string message)
    { if(!condition) throw new FinancialOperationException(FinancialReasons.AuthorityDenied,message); }
}
