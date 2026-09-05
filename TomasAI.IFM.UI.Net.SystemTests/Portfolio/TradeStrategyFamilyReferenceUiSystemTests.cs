using System.Reflection;
using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.UI.Net.Views.Reference;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeStrategyFamilyReferenceUiSystemTests
{
    [Fact]
    public async Task Creation_is_enabled_after_success_and_disabled_when_catalog_reload_throws()
    {
        var queries = Substitute.For<IReferenceQueryApi>();
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(new TomasAI.IFM.Shared.EventSourcing.ServiceOk<TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReadModel[]>([]));
        using var view = new TradeStrategyFamilyReferenceView(queries, Substitute.For<IReferenceCommandApi>());
        var create = (Button)view.GetType().GetField("_create", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
        await view.LoadAsync(); create.Enabled.Should().BeTrue();
        queries.GetTradeStrategyFamiliesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<TomasAI.IFM.Shared.EventSourcing.ServiceResult<TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyReadModel[]>>(new InvalidOperationException("offline")));
        await view.LoadAsync(); create.Enabled.Should().BeFalse();
        ((Label)view.GetType().GetField("_status", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!).Text.Should().Contain("offline");
    }

    [Fact]
    [Trait("Gate", "PF-22")]
    [Trait("Gate", "PF-29")]
    [Trait("Category", "Portfolio")]
    public void Existing_definitions_are_read_only_and_creation_is_disabled_without_command_api()
    {
        using var view = new TradeStrategyFamilyReferenceView(Substitute.For<IReferenceQueryApi>());
        var grid = (DataGridView)(view.GetType().GetField("_grid", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(view)
            ?? throw new InvalidOperationException("Missing family grid."));

        grid.ReadOnly.Should().BeTrue();
        grid.AllowUserToAddRows.Should().BeFalse();
        grid.AllowUserToDeleteRows.Should().BeFalse();
        ((Button)view.GetType().GetField("_create", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!).Enabled.Should().BeFalse();
    }
}
