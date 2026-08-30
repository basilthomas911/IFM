using FluentAssertions; using TomasAI.IFM.Domain.Portfolio.Shared.Contracts; using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.Domain.Portfolio.VerificationTests.Command;
public sealed class PortfolioRiskCapacityVerificationTests
{
 [Theory][InlineData(FundCapacityState.Available,true)][InlineData(FundCapacityState.Constrained,true)][InlineData(FundCapacityState.Blocked,false)][InlineData(FundCapacityState.ReduceOnly,false)][Trait("Gate","PF-06")][Trait("Category","Portfolio")]
 public void Representative_capacity_states_are_fail_closed(FundCapacityState state,bool expected){var n=new DateTime(2026,8,29,14,0,0,DateTimeKind.Utc);new FundRiskEnvelopeReadModel{PortfolioId=101,PortfolioVersion=1,FundId=205,FundMandateVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=1,CapacityState=state,Currency="USD",AllocatedCapital=100000,AvailableCapital=80000,MaximumRiskPerTrade=1000,MaximumAggregateRisk=5000,MaximumMargin=50000,MaximumGrossNotional=500000,MaximumContracts=10,MaximumOpenPositions=5,RemainingLossBudget=10000,EffectiveFromUtc=n,ExpiresAtUtc=n.AddDays(30),SourcePolicyId=Guid.NewGuid(),SourcePolicyVersion=1,CreatedOnUtc=n,CreatedBy="verification"}.PermitsNewExposureAt(n.AddDays(1)).Should().Be(expected);}
}
