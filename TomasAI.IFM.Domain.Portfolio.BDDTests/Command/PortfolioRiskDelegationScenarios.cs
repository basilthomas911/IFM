using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.BDDTests.Command;

public sealed class PortfolioRiskDelegationScenarios
{
    [Fact]
    [Trait("Gate","PF-06")]
    [Trait("Category","Portfolio")]
    public void Given_a_member_Fund_when_capital_and_risk_are_delegated_then_Portfolio_remains_the_authority()
    {
        var now=new DateTime(2026,8,29,14,0,0,DateTimeKind.Utc); var p=new PortfolioAggregate();
        p.Create(Guid.NewGuid(),new PortfolioReadModel{PortfolioId=101,PortfolioCode="CORE",Name="Core",PortfolioVersion=1,OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="admin"},now,"admin");
        p.AddFund(Guid.NewGuid(),1,new PortfolioFundId(101,205),now,"admin");
        p.DelegateAllocation(Guid.NewGuid(),2,new FundAllocationReadModel{PortfolioId=101,PortfolioVersion=1,FundId=205,FundMandateVersion=1,AllocationVersion=1,TargetWeight=.5m,MinimumWeight=.25m,MaximumWeight=.75m,AllocatedCapital=100000,Currency="USD",EffectiveFromUtc=now,SourcePolicyVersion=1,CreatedOnUtc=now,CreatedBy="admin"},now,"admin");
        p.DelegateRiskEnvelope(Guid.NewGuid(),3,new FundRiskEnvelopeReadModel{PortfolioId=101,PortfolioVersion=1,FundId=205,FundMandateVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=1,CapacityState=FundCapacityState.Constrained,Currency="USD",AllocatedCapital=100000,AvailableCapital=50000,MaximumRiskPerTrade=1000,MaximumAggregateRisk=5000,MaximumMargin=50000,MaximumGrossNotional=500000,MaximumContracts=10,MaximumOpenPositions=5,RemainingLossBudget=10000,EffectiveFromUtc=now,ExpiresAtUtc=now.AddDays(30),SourcePolicyId=Guid.NewGuid(),SourcePolicyVersion=1,CreatedOnUtc=now,CreatedBy="admin"},now,"admin");
        p.Allocations(205).Should().ContainSingle(); p.RiskEnvelopes(205).Should().ContainSingle().Which.CapacityState.Should().Be(FundCapacityState.Constrained);
    }
}
