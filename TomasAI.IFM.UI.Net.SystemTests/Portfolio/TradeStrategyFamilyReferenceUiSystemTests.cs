using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.UI.Net.Views.Reference;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeStrategyFamilyReferenceUiSystemTests
{
    [Fact]
    [Trait("Gate", "PF-22")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Family_catalog_view_is_read_only_and_has_no_mutation_surface()
    {
        using var view = new TradeStrategyFamilyReferenceView(Substitute.For<IReferenceQueryApi>());
        var grid = (DataGridView)(view.GetType().GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(view)
            ?? throw new InvalidOperationException("Missing family grid."));

        grid.ReadOnly.Should().BeTrue();
        grid.AllowUserToAddRows.Should().BeFalse();
        grid.AllowUserToDeleteRows.Should().BeFalse();
        view.Controls.OfType<Button>().Should().BeEmpty();
    }
}
