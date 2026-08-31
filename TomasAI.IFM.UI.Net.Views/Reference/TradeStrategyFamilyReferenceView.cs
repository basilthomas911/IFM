using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Reference;

/// <summary>Read-only v1 TradeStrategyFamily catalog; management is intentionally deferred.</summary>
public sealed class TradeStrategyFamilyReferenceView : UserControl
{
    readonly IReferenceQueryApi _queries;
    readonly DataGridView _grid = new()
    {
        Name = "tradeStrategyFamilyGrid", AccessibleName = "Read-only trade strategy families",
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.FromArgb(48, 48, 48), ForeColor = Color.White,
    };
    readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 32, ForeColor = Color.White, BackColor = Color.Black };

    public TradeStrategyFamilyReferenceView(IReferenceQueryApi queries)
    {
        _queries = queries; Name = "TradeStrategyFamilyReferenceView"; BackColor = Color.FromArgb(64, 64, 64);
        Controls.Add(_grid); Controls.Add(_status);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var result = await _queries.GetTradeStrategyFamiliesAsync(cancellationToken);
        _grid.DataSource = result.Success && result.Value is not null ? result.Value : Array.Empty<TradeStrategyFamilyReadModel>();
        _status.Text = result.Success ? "V1 catalog is read-only. Strategy variants are managed in a later release." : result.ErrorMessage;
    }
}
