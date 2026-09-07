using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
namespace TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.TradeSelection;
public static partial class TradeSelectionContracts
{
    static void ValidateAuthority(PortfolioFundStrategySnapshot snapshot,TradeSelectionBinding binding)
    {
        var p=snapshot.Portfolio;var f=snapshot.Fund;var policy=snapshot.FinancialPolicy;var allocation=snapshot.Allocation;var envelope=snapshot.RiskEnvelope;var at=binding.FrozenAtUtc;
        Require(p.Validate().Count==0 && policy.Validate(forActivation:true).Count==0 && allocation.Validate().Count==0 && envelope.Validate().Count==0,"TS.CONFIG.AUTHORITY","Invalid authority objects.");
        Require(Enum.IsDefined(p.OperatingState) && Enum.IsDefined(f.OperatingState) && f.OperatingState!=FundOperatingState.Unknown && Enum.IsDefined(envelope.CapacityState)
            && f.FundId>0 && f.FundMandateVersion>0 && f.SchemaVersion==3 && !f.IsLegacyHistory && f.PortfolioId==p.PortfolioId && f.TradingYear==binding.RequestedTradeDate.Year
            && Utc(f.CreatedOnUtc) && !string.IsNullOrWhiteSpace(f.CreatedBy),"TS.CONFIG.AUTHORITY","Invalid Fund scope/state/schema.");
        Require(policy.PortfolioId==p.PortfolioId && policy.PolicyId==p.ActivePolicyId && policy.PolicyVersion==p.ActivePolicyVersion && policy.OperatingState==PortfolioFinancialPolicyState.Active
            && allocation.PortfolioId==p.PortfolioId && allocation.PortfolioVersion==p.PortfolioVersion && allocation.FundId==f.FundId && allocation.FundMandateVersion==f.FundMandateVersion
            && allocation.SourcePolicyId==policy.PolicyId && allocation.SourcePolicyVersion==policy.PolicyVersion && envelope.PortfolioId==p.PortfolioId && envelope.PortfolioVersion==p.PortfolioVersion
            && envelope.FundId==f.FundId && envelope.FundMandateVersion==f.FundMandateVersion && envelope.SourcePolicyId==policy.PolicyId && envelope.SourcePolicyVersion==policy.PolicyVersion,"TS.CONFIG.AUTHORITY","Authority versions do not match.");
        var limits=new[]{p.EffectiveUntilUtc,f.EffectiveUntilUtc,policy.EffectiveUntilUtc,allocation.EffectiveUntilUtc,envelope.ExpiresAtUtc}.Where(x=>x.HasValue).Select(x=>x!.Value).ToArray();
        Require(Utc(snapshot.ResolvedAtUtc) && Utc(snapshot.ValidUntilUtc) && snapshot.ValidUntilUtc>at && limits.All(x=>Utc(x) && snapshot.ValidUntilUtc<=x)
            && p.EffectiveFromUtc<=at && f.EffectiveFromUtc<=at && policy.EffectiveFromUtc<=at && allocation.EffectiveFromUtc<=at && envelope.EffectiveFromUtc<=at,"TS.CONFIG.AUTHORITY","Authority is expired or not effective.");
        foreach(var assignment in snapshot.Assignments)
            Require(assignment.Validate().Count==0 && assignment.PortfolioId==p.PortfolioId && assignment.PortfolioVersion==p.PortfolioVersion && assignment.FundId==f.FundId
                && assignment.FundMandateVersion==f.FundMandateVersion && assignment.DecisionHorizon==f.DecisionHorizon && assignment.UnderlyingUniverse.Contains(CommonPolicy(binding).InstrumentRoot,StringComparer.Ordinal)
                && assignment.EffectiveFromUtc<=at && (assignment.EffectiveUntilUtc is null || binding.ValidUntilUtc<=assignment.EffectiveUntilUtc),"TS.CONFIG.AUTHORITY","Invalid effective assignment scope or validity.");
    }
}
