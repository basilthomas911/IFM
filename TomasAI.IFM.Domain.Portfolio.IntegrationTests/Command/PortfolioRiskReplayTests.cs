using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Command.Model;
using TomasAI.IFM.Domain.Portfolio.Command.State;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.Identities;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.Domain.Portfolio.IntegrationTests.Command;
public sealed class PortfolioRiskReplayTests
{
 [Fact][Trait("Gate","PF-06")][Trait("Category","Portfolio")]
 public void Allocation_and_envelope_replay_with_exact_limits(){var n=new DateTime(2026,8,29,14,0,0,DateTimeKind.Utc);var allocation=new FundAllocationReadModel{PortfolioId=101,PortfolioVersion=1,FundId=205,FundMandateVersion=1,AllocationVersion=1,TargetWeight=.5m,MinimumWeight=.25m,MaximumWeight=.75m,AllocatedCapital=100000,Currency="USD",EffectiveFromUtc=n,SourcePolicyVersion=1,CreatedOnUtc=n,CreatedBy="admin"};var envelope=new FundRiskEnvelopeReadModel{PortfolioId=101,PortfolioVersion=1,FundId=205,FundMandateVersion=1,EnvelopeId=Guid.NewGuid(),EnvelopeVersion=1,CapacityState=FundCapacityState.Available,Currency="USD",AllocatedCapital=100000,AvailableCapital=80000,MaximumRiskPerTrade=1000,MaximumAggregateRisk=5000,MaximumMargin=50000,MaximumGrossNotional=500000,MaximumContracts=10,MaximumOpenPositions=5,RemainingLossBudget=10000,EffectiveFromUtc=n,ExpiresAtUtc=n.AddDays(30),SourcePolicyId=Guid.NewGuid(),SourcePolicyVersion=1,CreatedOnUtc=n,CreatedBy="admin"};PortfolioDomainEvent[] h=[new PortfolioCreated(Guid.NewGuid(),Guid.NewGuid(),1,n,"admin",new PortfolioReadModel{PortfolioId=101,PortfolioCode="CORE",Name="Core",PortfolioVersion=1,OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=n,CreatedOnUtc=n,CreatedBy="admin"}),new FundAddedToPortfolio(Guid.NewGuid(),Guid.NewGuid(),2,n,"admin",new PortfolioFundId(101,205)),new FundAllocationDelegated(Guid.NewGuid(),Guid.NewGuid(),3,n,"admin",allocation),new FundRiskEnvelopeDelegated(Guid.NewGuid(),Guid.NewGuid(),4,n,"admin",envelope)];var p=new PortfolioAggregate();p.Replay(h);p.RiskEnvelopes(205).Single().Should().BeEquivalentTo(envelope);}
}
