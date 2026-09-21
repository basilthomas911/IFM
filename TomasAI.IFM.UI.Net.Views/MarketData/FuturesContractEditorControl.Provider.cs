using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

public partial class FuturesContractEditorControl
{
    FuturesContractV3ReadModel? _providerReference;
    void InitializeProviderSelection()
    {
        var button = new Button { Text = "Databento definition / reviewed conventions…", Dock = DockStyle.Bottom, Height = 32 };
        splitContainer1.Panel2.Controls.Add(button);
        button.BringToFront();
        button.Click += (_, _) =>
        {
            if (_editMode == EditMode.View)
            {
                MessageBox.Show("Choose Add or Change before selecting a provider definition.");
                return;
            }
            using var dialog = new InstrumentDefinitionSelectorForm(_viewModel.CreateDefinitionSelector(), false,
                _editMode == EditMode.Change ? _viewModel.GetFuturesContract(_lastContractIndex) : null);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.Future is not { } value) return;
            _providerReference = value;
            ShowSelectedFuturesContract(_lastContractIndex, value);
            txtContractId.Text = value.ContractId;
            txtDescription.Text = value.Description;
            txtDescription.Enabled = true;
            ddlOnTheRun.Enabled = true;
            button.Text = $"Databento: {value.RawSymbol} — {value.ReviewState}";
        };
    }
}
