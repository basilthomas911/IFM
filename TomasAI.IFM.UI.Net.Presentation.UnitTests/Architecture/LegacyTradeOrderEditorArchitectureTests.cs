using FluentAssertions;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Architecture;

public class LegacyTradeOrderEditorArchitectureTests
{
    [Fact]
    public void LegacyIronCondorSelection_UsesOriginalTradeOrderEditorInHistoricalReadOnlyMode()
    {
        var form = File.ReadAllText(Path.Combine(
            SolutionSource.RootPath,
            "TomasAI.IFM.UI.Net.Views",
            "Trade",
            "TradeOrderEditorForm.cs"));
        var methodStart = form.IndexOf(
            "async Task ShowLegacyTradeEditorAsync",
            StringComparison.Ordinal);
        var methodEnd = form.IndexOf(
            "void ShowTradeEditorUnavailable",
            methodStart,
            StringComparison.Ordinal);
        methodStart.Should().BeGreaterThanOrEqualTo(0);
        methodEnd.Should().BeGreaterThan(methodStart);
        var legacyRoute = form[methodStart..methodEnd];

        legacyRoute.Should().Contain("new IronCondorTradeOrderViewModel(");
        legacyRoute.Should().Contain("historicalReadOnly: true");
        legacyRoute.Should().Contain("historicalTrade: trade");
        legacyRoute.Should().Contain("new IronCondorTradeOrderView(this, viewModel)");
        legacyRoute.Should().NotContain("TradeBlotterFactory.Create");
        legacyRoute.Should().NotContain("BaseContracts.ElementAt(0)");
    }

    [Fact]
    public void HistoricalIronCondorEditor_DisablesInputsAndFencesSideEffects()
    {
        var view = File.ReadAllText(Path.Combine(
            SolutionSource.RootPath,
            "TomasAI.IFM.UI.Net.Views",
            "Trade",
            "IronCondor",
            "IronCondorTradeOrderView.cs"));
        var viewModel = File.ReadAllText(Path.Combine(
            SolutionSource.RootPath,
            "TomasAI.IFM.UI.Net.ViewModels",
            "Trade",
            "IronCondor",
            "IronCondorTradeOrderViewModel.cs"));

        view.Should().Contain("ApplyHistoricalReadOnlyState();");
        view.Should().Contain("comboBox.Enabled = false;");
        view.Should().Contain("dateTimePicker.Enabled = false;");
        view.Should().Contain("numericUpDown.Enabled = false;");
        view.Should().Contain("button.Enabled = false;");
        viewModel.Should().Contain("void ThrowIfHistoricalReadOnly()");
        viewModel.Should().Contain("A hydrated TradeDb option trade is required for historical read-only display.");
        viewModel.Should().Contain("Historical trade orders are read-only.");
    }
}
