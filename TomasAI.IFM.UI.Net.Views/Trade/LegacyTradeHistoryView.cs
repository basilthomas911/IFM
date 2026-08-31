using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;

namespace TomasAI.IFM.UI.Net.Views.Trade;

/// <summary>Read-only center-tab presentation of a legacy composition and its hydrated TradeDb history.</summary>
public sealed class LegacyTradeHistoryView : UserControl
{
    static readonly Color Surface = Color.FromArgb(48, 48, 48);

    public LegacyTradeHistoryView(LegacyFundTradeHistoryReadModel history)
    {
        History = history ?? throw new ArgumentNullException(nameof(history));
        Dock = DockStyle.Fill;
        BackColor = Surface;
        ForeColor = Color.White;
        AccessibleName = $"Read-only legacy trade {history.Composition.OrderId}:{history.Composition.TradeId}";

        var composition = history.Composition;
        var trade = history.TradeDbTrade;
        var header = new Label
        {
            Dock = DockStyle.Top,
            Height = 70,
            Padding = new Padding(12, 8, 12, 6),
            BackColor = Color.Black,
            ForeColor = Color.White,
            Font = new Font("Microsoft Sans Serif", 11F, FontStyle.Bold),
            Text = $"READ-ONLY LEGACY TRADE   {composition.OrderId}:{composition.TradeId}\r\n"
                + $"{composition.TradeType} | composition={composition.TradeState} | TradeDb={history.MatchStatus}"
        };
        var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Legacy trade history sections" };
        tabs.TabPages.Add(TextTab("Summary", Summary(history)));
        tabs.TabPages.Add(GridTab("Option Legs", (trade?.OptionLegs ?? []).Select(x => new
        {
            x.ContractId, x.Quantity, x.StrikePrice, Type = x.OptionLegType, Action = x.OptionLegAction,
        }).ToArray()));
        tabs.TabPages.Add(GridTab("Fills", (trade?.TradeFills ?? []).Select(x => new
        {
            x.FillDate, x.FillQuantity, x.Price, x.Commission, x.CreatedOn, x.CreatedBy,
        }).ToArray()));
        tabs.TabPages.Add(GridTab("Positions", (trade?.TradePositions ?? []).OrderByDescending(x => x.ValueDate).Select(x => new
        {
            x.ValueDate, x.TradeStatus, x.DaysToExpiry, x.TradePnl, x.TradeValue, x.AssetPrice,
            x.NetSpread, x.Commission, x.DeltaHedge,
        }).ToArray()));
        Controls.Add(tabs);
        Controls.Add(header);
    }

    public LegacyFundTradeHistoryReadModel History { get; }

    /// <summary>The legacy view is structurally read-only and exposes no command service.</summary>
    public bool IsReadOnly => true;

    static TabPage TextTab(string title, string text)
    {
        var page = Page(title);
        page.Controls.Add(new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.None,
            Font = new Font("Consolas", 11F),
            Text = text,
        });
        return page;
    }

    static TabPage GridTab<T>(string title, T[] rows)
    {
        var page = Page($"{title} ({rows.Length})");
        page.Controls.Add(new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToOrderColumns = true,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            BackgroundColor = Color.Black,
            ForeColor = Color.Black,
            DataSource = rows,
        });
        return page;
    }

    static TabPage Page(string title) => new(title)
    {
        BackColor = Surface,
        ForeColor = Color.White,
        UseVisualStyleBackColor = false,
    };

    static string Summary(LegacyFundTradeHistoryReadModel history)
    {
        var composition = history.Composition;
        var trade = history.TradeDbTrade;
        return $"SOURCE\r\n"
            + $"FundId: {composition.FundId}\r\nOrderId: {composition.OrderId}\r\nTradeId: {composition.TradeId}\r\n"
            + $"Trade type: {composition.TradeType}\r\nComposition state: {composition.TradeState}\r\n"
            + $"Trade date: {Date(composition.TradeDate)}\r\nMaturity: {Date(composition.MaturityDate)}\r\n"
            + $"Reference: {composition.Reference}\r\n\r\nTRADEDB\r\nMatch: {history.MatchStatus}\r\n"
            + (trade is null
                ? "No corresponding TradeDb option_trade definition. Composition history remains available."
                : $"Strategy: {trade.TradeStrategy}\r\nState: {trade.TradeState}\r\nAction: {trade.TradeAction}\r\n"
                    + $"Underlying: {trade.UnderlyingContractId}\r\nOption legs: {history.OptionLegCount}\r\n"
                    + $"Fills: {history.FillCount}\r\nPositions: {history.PositionCount}");
    }

    static string Date(DateOnly value) => value == DateOnly.MinValue ? "Unknown" : $"{value:yyyy-MMM-dd}";
}

/// <summary>Creates or activates the unique read-only legacy trade tab in the main blotter area.</summary>
public static class LegacyTradeHistoryTabFactory
{
    public static TabPage OpenOrActivate(TabControl host, LegacyFundTradeHistoryReadModel history)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(history);
        var composition = history.Composition;
        var key = $"LegacyTrade-{composition.OrderId}-{composition.TradeId}";
        var page = host.TabPages.Cast<TabPage>().SingleOrDefault(x => x.Name == key);
        if (page is null)
        {
            page = new TabPage($"{composition.OrderId}:{composition.TradeId}")
            {
                Name = key,
                BackColor = Color.Black,
                UseVisualStyleBackColor = false,
                Tag = history,
            };
            page.Controls.Add(new LegacyTradeHistoryView(history));
            host.TabPages.Add(page);
        }
        host.SelectedTab = page;
        host.Visible = true;
        return page;
    }
}
