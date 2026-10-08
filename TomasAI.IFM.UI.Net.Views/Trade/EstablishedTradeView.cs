using System.ComponentModel;
using TomasAI.IFM.Domain.Trade.Shared;
using TomasAI.IFM.UI.Net.Views.Presentation;
namespace TomasAI.IFM.UI.Net.Views.Trade;
/// <summary>Displays the actual persisted trade, its strategy legs and execution evidence.</summary>
public sealed class EstablishedTradeView : DarkTradingView
{
    /// <summary>The established trade loaded by its exact financial identity.</summary>
    public EstablishedTradeDefinition Trade { get; }
    /// <summary>Creates a trade monitor from persisted execution-created trade data.</summary>
    /// <param name="trade">The exact projection returned by the Trade query actor.</param>
    public EstablishedTradeView(EstablishedTradeDefinition trade)
    {
        Trade = trade ?? throw new ArgumentNullException(nameof(trade));
        Name = "EstablishedTradeView"; Dock = DockStyle.Fill;
        var layout = new TableLayoutPanel { Name = "establishedTradeLayout", Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new(SizeType.Absolute, 48)); layout.RowStyles.Add(new(SizeType.Percent, 100));
        var header = new Label { Name = "establishedTradeHeader", AccessibleName = "Established trade identity and status",
            Text = $"Trade: {trade.Id.Format()}   Strategy: {trade.StrategyKind}   Status: {trade.Status}",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        var tabs = new TabControl { Name = "establishedTradeTabs", Dock = DockStyle.Fill };
        var details = new TabPage("Trade Details");
        details.Controls.Add(new PropertyGrid { Name = "establishedTradeDetails", Dock = DockStyle.Fill,
            HelpVisible = false, ToolbarVisible = false, SelectedObject = new {
                TradeId = trade.Id.Format(), trade.StrategyKind, trade.Status, trade.ExecutionAttemptId,
                trade.OpeningValue, trade.OpeningCommission, trade.EstablishedAtUtc, trade.ClosedAtUtc,
                trade.EvidenceRevision, LegCount = trade.Legs.Length, FillCount = trade.OriginalFills.Length } });
        tabs.TabPages.Add(details);
        AddGrid(tabs, "Trade Legs", "establishedTradeLegs", trade.Legs.Select(leg => new {
            leg.ContractId, Right = leg.PutCall == 1 ? "Call" : leg.PutCall == 2 ? "Put" : "Futures",
            leg.Strike, leg.Expiry, Quantity = leg.SignedQuantity, leg.CashMultiplier }).ToArray());
        AddGrid(tabs, "Opening Fills", "establishedTradeOpeningFills", trade.OriginalFills.Select(fill => new {
            fill.ContractId, Quantity = fill.SignedQuantity, fill.Price, fill.Commission, fill.FilledAtUtc, fill.ExternalExecutionId }).ToArray());
        AddGrid(tabs, "Closing Fills", "establishedTradeClosingFills", trade.ClosingFills.Select(fill => new {
            fill.ContractId, Quantity = fill.SignedQuantity, fill.Price, fill.Commission, fill.FilledAtUtc, fill.ExternalExecutionId }).ToArray());
        layout.Controls.Add(header, 0, 0); layout.Controls.Add(tabs, 0, 1); Controls.Add(layout);
    }
    /// <summary>Creates a read-only table of persisted trade evidence.</summary>
    private static void AddGrid(TabControl tabs, string caption, string name, object data)
    {
        var page = new TabPage(caption);
        page.Controls.Add(new DataGridView { Name = name, AccessibleName = caption, Dock = DockStyle.Fill,
            ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, DataSource = data });
        tabs.TabPages.Add(page);
    }
}
