using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Portfolio.Shared.ServiceApi;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class PortfolioCompositionSystemTests
{
    [Fact]
    [Trait("Gate", "PF-10")]
    [Trait("Gate", "PF-16")]
    [Trait("Gate", "PF-17")]
    [Trait("Category", "Portfolio")]
    public void Ui_uses_typed_api_and_has_no_PortfolioDb_reference()
    {
        var uiAssemblies = new[] { typeof(PortfolioCompositionViewModel).Assembly, typeof(PortfolioCompositionForm).Assembly };
        uiAssemblies.SelectMany(x => x.GetReferencedAssemblies()).Select(x => x.Name)
            .Should().NotContain(name => name != null && name.Contains("Application.Storage", StringComparison.Ordinal));
        typeof(PortfolioCompositionViewModel).GetConstructors().Single().GetParameters().Single().ParameterType
            .Should().Be(typeof(IPortfolioQueryApi));
    }

    [Fact]
    [Trait("Gate", "PF-17")]
    [Trait("Category", "Portfolio")]
    public void Composition_screen_exposes_integer_search_without_create_fund_or_execution_semantics()
    {
        var fields = typeof(PortfolioCompositionForm)
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Select(x => x.Name).ToArray();
        fields.Should().Contain(["_identity", "_findOrder", "_findTrade", "_orders", "_trades"]);
        fields.Should().NotContain(name => name.Contains("createFund", StringComparison.OrdinalIgnoreCase));
        var viewModel = new PortfolioCompositionViewModel(Substitute.For<IPortfolioQueryApi>());
        viewModel.Semantics.Should().Contain("not a broker order").And.Contain("live trade");
        typeof(PortfolioCompositionViewModel).GetMethods().Select(x => x.Name)
            .Should().NotContain(name => name.Contains("Execute", StringComparison.OrdinalIgnoreCase));
    }
}
