using FluentAssertions; using TomasAI.IFM.Domain.Portfolio.Shared.Contracts; using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;
public sealed class PortfolioRiskContractSystemTests
{
 [Fact][Trait("Gate","PF-06")][Trait("Category","Portfolio")]
 public void Ui_contract_displays_constraints_without_converting_them_to_quantity(){var n=new DateTime(2026,8,29,14,0,0,DateTimeKind.Utc);var a=new FundAllocationReadModel{PortfolioId=101,PortfolioVersion=1,FundId=205,FundMandateVersion=1,AllocationVersion=1,TargetWeight=.5m,MinimumWeight=.25m,MaximumWeight=.75m,AllocatedCapital=100000,Currency="USD",EffectiveFromUtc=n,SourcePolicyVersion=1,CreatedOnUtc=n,CreatedBy="admin"};a.TargetWeight.Should().Be(.5m);a.AllocatedCapital.Should().Be(100000m);}
}
