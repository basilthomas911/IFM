using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using System.Reflection;
using FluentAssertions;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.SystemTests.Portfolio;

public sealed class TradeStrategyTimeFrameUiTests
{
    [Theory]
    [InlineData("Daily", TimeFrameType.Daily)]
    [InlineData("Weekly", TimeFrameType.Weekly)]
    [InlineData("Monthly", TimeFrameType.Monthly)]
    public void Mandate_and_assignment_select_only_named_strategy_timeframes(string name, TimeFrameType expected)
    {
        var fund = new FundMandateReadModel { FundId = 2, PortfolioId = 1, TradingYear = 2026, DecisionHorizon = name };
        using var mandate = new FundMandateEditorForm(1, 2, fund);
        using var assignment = new FundAssignmentEditorForm(new PortfolioReadModel { PortfolioId = 1 }, fund);
        foreach (var form in new Form[] { mandate, assignment })
        {
            var combo = Horizon(form);
            combo.DropDownStyle.Should().Be(ComboBoxStyle.DropDownList);
            combo.Items.Cast<TimeFrameType>().Should().Equal(TimeFrameType.Daily, TimeFrameType.Weekly, TimeFrameType.Monthly);
            combo.Items.Cast<object>().Select(x => x.ToString()).Should().Equal("Daily", "Weekly", "Monthly");
            combo.SelectedItem.Should().Be(expected);
            combo.Text.Should().Be(name);
        }
    }

    [Theory]
    [InlineData("Quarterly")]
    [InlineData("OneMinute")]
    [InlineData("1")]
    [InlineData("")]
    public void Unsupported_stored_horizon_is_not_silently_changed_to_daily(string name)
    {
        var fund = new FundMandateReadModel { FundId = 2, PortfolioId = 1, TradingYear = 2026, DecisionHorizon = name };
        using var mandate = new FundMandateEditorForm(1, 2, fund);
        using var assignment = new FundAssignmentEditorForm(new PortfolioReadModel { PortfolioId = 1 }, fund);
        Horizon(mandate).SelectedIndex.Should().Be(-1);
        Horizon(assignment).SelectedIndex.Should().Be(-1);
    }

    [Fact]
    public void Saving_mandate_uses_selected_enum_name_and_rejects_no_selection()
    {
        using var form = new FundMandateEditorForm(1, 2, catalog: [new StrategyDeploymentChoice(new(StrategyCatalogKind.Deployment, Guid.NewGuid(), 1), "Weekly-ES", "Weekly ES", CatalogLifecycleStatus.Draft, TimeFrameType.Weekly, [new(71, "ES", "XCME", "USD")], ["FuturesOption"], [], [])]);
        ((CheckedListBox)Field(form, "_families")).SetItemChecked(0, true);
        foreach (var (field, text) in new[] { ("_code", "weekly"), ("_name", "Weekly Fund"), ("_objective", "Test"),
                     ("_underlyings", "ES"), ("_assets", "FuturesOptions"), ("_directions", "Bullish"), ("_conditions", "Directional") })
            ((TextBox)Field(form, field)).Text = text;
        var save = form.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Horizon(form).SelectedIndex = -1;
        save.Invoke(form, null);
        form.Value.Should().BeNull();
        Horizon(form).SelectedItem = TimeFrameType.Weekly;
        save.Invoke(form, null);
        form.Value.Should().NotBeNull();
        form.Value!.DecisionHorizon.Should().Be("Weekly");
    }

    static ComboBox Horizon(Form form) => (ComboBox)Field(form, "_horizon");
    static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
}
