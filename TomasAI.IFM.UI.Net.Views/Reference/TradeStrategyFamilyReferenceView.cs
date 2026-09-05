using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Reference;

/// <summary>Immutable definition catalog with explicit creation through the command API.</summary>
public sealed class TradeStrategyFamilyReferenceView : UserControl
{
    readonly IReferenceQueryApi _queries;
    readonly IReferenceCommandApi? _commands;
    readonly Button _create = new() { Text = "Create Family...", AccessibleName = "Create trade strategy family", AutoSize = true, Enabled = false };
    readonly DataGridView _grid = new()
    {
        Name = "tradeStrategyFamilyGrid", AccessibleName = "Read-only trade strategy families",
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.FromArgb(48, 48, 48), ForeColor = Color.White,
    };
    readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 32, ForeColor = Color.White, BackColor = Color.Black };

    public TradeStrategyFamilyReferenceView(IReferenceQueryApi queries, IReferenceCommandApi? commands = null)
    {
        _queries = queries; _commands = commands; Name = "TradeStrategyFamilyReferenceView"; BackColor = Color.FromArgb(64, 64, 64);
        Controls.Add(_grid); Controls.Add(_status);
        _grid.DataBindingComplete += (_, _) =>
        {
            if (_grid.Columns[nameof(TradeStrategyFamilyReadModel.Exchange)] is { } exchange &&
                _grid.Columns[nameof(TradeStrategyFamilyReadModel.Description)] is { } description)
                exchange.DisplayIndex = description.DisplayIndex;
        };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44 };
        actions.Controls.Add(_create); Controls.Add(actions);
        _create.Click += async (_, _) =>
        {
            if (_commands is null) return;
            using var editor = new TradeStrategyFamilyEditorForm(_queries, _commands);
            if (editor.ShowDialog(this) == DialogResult.OK) await LoadAsync();
        };
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        _create.Enabled = false;
        try
        {
            var result = await _queries.GetTradeStrategyFamiliesAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _grid.DataSource = result.Success && result.Value is not null ? result.Value : Array.Empty<TradeStrategyFamilyReadModel>();
            _create.Enabled = result.Success && result.Value is not null && _commands is not null;
            _status.Text = result.Success ? "Select Create Family to add a product/timeframe definition. Existing IDs and versions remain immutable." : result.ErrorMessage;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            _grid.DataSource = Array.Empty<TradeStrategyFamilyReadModel>();
            _status.Text = $"Family catalog unavailable: {ex.Message}";
        }
    }
}
