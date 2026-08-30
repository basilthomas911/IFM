using FluentAssertions;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Models.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioProjectionBindingSystemTests
{
    [Fact]
    [Trait("Gate", "PF-08")]
    public void Typed_projection_DTOs_bind_to_Portfolio_and_ordered_Fund_navigation_without_storage_fields()
    {
        var now=DateTime.UtcNow;
        var p=new PortfolioReadModel{PortfolioId=1,PortfolioCode="P",Name="P",PortfolioVersion=1,OperatingState=PortfolioOperatingState.Draft,EffectiveFromUtc=now,CreatedOnUtc=now,CreatedBy="ui"};
        var a=new FundMandateReadModel{PortfolioId=1,FundId=3,FundCode="B",Name="B",FundMandateVersion=1};
        var b=a with{FundId=2,FundCode="A",Name="A"};
        var model=new PortfolioNavigationModel(p,[a,b]);
        model.OrderedFunds.Select(x=>x.FundCode).Should().Equal("A","B");
        typeof(PortfolioNavigationModel).GetProperties().Should().NotContain(x=>x.Name.Contains("Cql",StringComparison.OrdinalIgnoreCase));
    }
}
