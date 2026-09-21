using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

public partial class FuturesOptionContractEditorControl
{
    FuturesOptionContractReadModel? _providerReference;
    void InitializeProviderSelection()
    {
        var button = new Button { Text = "Databento definition / reviewed conventions…", Dock = DockStyle.Bottom, Height = 32 };
        pnlEditorSplitter.Panel2.Controls.Add(button);
        button.BringToFront();
        button.Click += (_, _) =>
        {
            if (_editMode == EditMode.View)
            {
                MessageBox.Show("Choose Add or Change before selecting a provider definition.");
                return;
            }
            using var dialog = new InstrumentDefinitionSelectorForm(_viewModel.CreateDefinitionSelector(), true,
                option: _editMode == EditMode.Change ? _viewModel.GetFuturesOptionContract(_lastContractIndex) : null);
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.Option is not { } value) return;
            _providerReference = value;
            var previous = _isBinding;
            _isBinding = true;
            try { ShowSelectedFuturesOptionContract(_lastContractIndex, value); }
            finally { _isBinding = previous; }
            txtDescription.ReadOnly = false;
            txtDescription.Enabled = true;
            ddlSymbol.Enabled = false;
            button.Text = $"Databento: {value.RawSymbol} — {value.ReviewState}";
        };
    }
}
