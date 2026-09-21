using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
using TomasAI.IFM.UI.Net.Views.Presentation;

namespace TomasAI.IFM.UI.Net.Views.MarketData;

/// <summary>Shared bounded provider browser and explicit convention review for both reference editors.</summary>
public sealed class InstrumentDefinitionSelectorForm : DarkTradingForm
{
    readonly InstrumentDefinitionSelectorViewModel model;
    readonly TimeProvider clock;
    readonly Func<Form, string, bool> confirm;
    readonly bool options;
    readonly FuturesContractV3ReadModel? originalFuture;
    readonly FuturesOptionContractReadModel? originalOption;
    readonly CancellationTokenSource lifetime = new();
    readonly TextBox dataset = new() { Text = "GLBX.MDP3", Width = 100 };
    readonly TextBox root = new() { Width = 80, PlaceholderText = "Provider root" };
    readonly TextBox exchange = new() { Width = 85, PlaceholderText = "Exchange" };
    readonly CheckBox history = new() { Text = "Expired/deleted", AutoSize = true };
    readonly DateTimePicker expiry = new() { Width = 115, Format = DateTimePickerFormat.Short, ShowCheckBox = true, Checked = false };
    readonly ComboBox right = new() { Width = 80, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox underlyingFilter = new() { Width = 100, PlaceholderText = "Underlying ID" };
    readonly TextBox minimumStrike = new() { Width = 95, PlaceholderText = "Min strike" };
    readonly TextBox maximumStrike = new() { Width = 95, PlaceholderText = "Max strike" };
    bool disposedResources;
    long filterRevision;
    readonly DataGridView rows = new() { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoGenerateColumns = false, RowHeadersVisible = false };
    readonly Button next = new() { Text = "Next page", AutoSize = true, Enabled = false };
    readonly Button select = new() { Text = "Use selected definition", AutoSize = true, Enabled = false };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 52, AutoEllipsis = true };
    readonly ReferenceConventionEditor review;
    InstrumentDefinitionPageRequest? search;
    public FuturesContractV3ReadModel? Future { get; private set; }
    public FuturesOptionContractReadModel? Option { get; private set; }

    public InstrumentDefinitionSelectorForm(InstrumentDefinitionSelectorViewModel model, bool options,
        FuturesContractV3ReadModel? future = null, FuturesOptionContractReadModel? option = null,
        TimeProvider? time = null, Func<Form, string, bool>? confirm = null)
    {
        this.model = model; this.options = options; originalFuture = future; originalOption = option;
        clock = time ?? TimeProvider.System;
        this.confirm = confirm ?? ((owner, preview) => MessageBox.Show(owner, preview + "\nUse this reference in the editor?",
            "Confirm provider mapping", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK);
        review = option is not null ? ReferenceConventionEditor.From(option)
            : future is not null ? ReferenceConventionEditor.From(future) : new();
        Text = options ? "Databento futures option reference" : "Databento futures reference";
        Size = new(1120, 660); MinimumSize = new(850, 500); StartPosition = FormStartPosition.CenterParent;
        root.Text = option?.Symbol ?? future?.Symbol ?? "";
        var filters = new FlowLayoutPanel { Name = "DefinitionFilters", Dock = DockStyle.Top, AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = true, Padding = new(4) };
        var find = new Button { Text = "Search", AutoSize = true };
        find.Name = "SearchDefinitions"; select.Name = "UseDefinition"; next.Name = "NextDefinitions";
        root.Name = "ProviderRoot"; dataset.Name = "ProviderDataset"; status.Name = "DefinitionStatus";
        rows.Name = "DefinitionRows";
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        right.DataSource = Enum.GetValues<ReferenceOptionRight>();
        right.Enabled = minimumStrike.Enabled = maximumStrike.Enabled = underlyingFilter.Enabled = options;
        filters.Controls.AddRange([Labelled(dataset, "Dataset"), Labelled(root, "Provider root"), Labelled(exchange, "Exchange"),
            Labelled(expiry, "Expiry UTC"), Labelled(right, "Right (any = Unknown)"),
            Labelled(underlyingFilter, "Underlying ID"), Labelled(minimumStrike, "Minimum strike"),
            Labelled(maximumStrike, "Maximum strike"), history, find, next, select, cancel]);
        foreach (var control in new[] { dataset, root, exchange, underlyingFilter, minimumStrike, maximumStrike })
            control.TextChanged += (_, _) => InvalidateSearch();
        history.CheckedChanged += (_, _) => InvalidateSearch();
        expiry.ValueChanged += (_, _) => InvalidateSearch();
        right.SelectedValueChanged += (_, _) => InvalidateSearch();
        var properties = new PropertyGrid { Dock = DockStyle.Right, Width = 330, SelectedObject = review,
            HelpVisible = true, ToolbarVisible = false };
        foreach (var (property, caption, width) in new[] {
            ("RawSymbol", "Provider symbol", 150), ("InstrumentId", "Instrument", 90), ("InstrumentClass", "Class", 50),
            ("Strike", "Strike", 90), ("ExpirationUtc", "Expiry UTC", 145), ("Exchange", "Exchange", 75),
            ("UnderlyingInstrumentId", "Underlying", 95) })
            rows.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = caption, Width = width });
        Controls.Add(rows); Controls.Add(properties); Controls.Add(status); Controls.Add(filters);
        status.Text = "Provider values are read-only. Enter the exchange time zone and review conventions. Options require an existing exact underlying IFM contract.";
        find.Click += async (_, _) => await SearchAsync(false);
        next.Click += async (_, _) => await SearchAsync(true);
        select.Click += async (_, _) => await SelectAsync();
        FormClosed += (_, _) => { lifetime.Cancel(); model.Dispose(); };
        CancelButton = cancel;
    }

    async Task SearchAsync(bool more)
    {
        var revision = filterRevision;
        try
        {
            select.Enabled = false; next.Enabled = false;
            search = more && search is not null && model.Page is { } page
                ? search with { SnapshotId = page.SnapshotId, ContinuationToken = page.ContinuationToken }
                : new() { Dataset = dataset.Text.Trim(), Root = root.Text.Trim(), Options = options,
                    Exchange = string.IsNullOrWhiteSpace(exchange.Text) ? null : exchange.Text.Trim(),
                    Expiry = expiry.Checked ? DateOnly.FromDateTime(expiry.Value) : null,
                    Right = options ? (ReferenceOptionRight)right.SelectedItem! : ReferenceOptionRight.Unknown,
                    UnderlyingInstrumentId = string.IsNullOrWhiteSpace(underlyingFilter.Text) ? null
                        : uint.Parse(underlyingFilter.Text, System.Globalization.CultureInfo.InvariantCulture),
                    MinimumStrike = ParseStrike(minimumStrike.Text), MaximumStrike = ParseStrike(maximumStrike.Text),
                    IncludeExpiredOrDeleted = history.Checked };
            if (!await model.SearchAsync(search, lifetime.Token) || IsDisposed || revision != filterRevision) return;
            rows.DataSource = model.Page!.Items;
            next.Enabled = model.Page.ContinuationToken is not null;
            select.Enabled = true;
            status.Text = $"Stored Databento snapshot {model.Page.SnapshotId}; completed {model.Page.SnapshotCompletedUtc:u}. " +
                "Expiry filter/display is UTC. Unknown conventions remain unqualified.";
        }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Search cancelled or timed out."; }
        catch (Exception e) { if (!IsDisposed) status.Text = e.Message; }
    }

    async Task SelectAsync()
    {
        var revision = filterRevision;
        if (rows.CurrentRow?.DataBoundItem is not InstrumentDefinitionSelection definition) return;
        select.Enabled = false;
        try
        {
            if (originalFuture is { SchemaVersion: > 0 } && (originalFuture.Dataset != definition.Dataset || originalFuture.PublisherId != definition.PublisherId
                || originalFuture.InstrumentId != definition.InstrumentId)
                || originalOption is { SchemaVersion: > 0 } && (originalOption.Dataset != definition.Dataset || originalOption.PublisherId != definition.PublisherId
                || originalOption.InstrumentId != definition.InstrumentId))
                throw new InvalidOperationException("Change cannot replace the provider instrument. Use Add for a replacement.");
            if (options)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var underlying = await model.ResolveUnderlyingAsync(review.UnderlyingContractId, timeout.Token);
                var value = review.Apply(InstrumentDefinitionImport.Option(definition, underlying,
                    review.ExchangeTimeZoneId, clock.GetUtcNow()));
                if (originalOption is not null)
                {
                    if (originalOption.Symbol != value.Symbol || originalOption.ContractMonth != value.ContractMonth
                        || originalOption.GetExactStrikePrice() != value.GetExactStrikePrice() || originalOption.OptionType != value.OptionType)
                        throw new InvalidOperationException("Selected definition changes contract identity. Use Add.");
                    value = value with { ContractId = originalOption.ContractId };
                }
                if (value.ReviewState == ReferenceReviewState.Reviewed)
                    Check(FuturesReferenceQualification.Errors(value));
                Option = value;
                status.Text = $"Preview IFM ID: {value.ContractId}; {value.ReviewState}";
            }
            else
            {
                var value = review.Apply(InstrumentDefinitionImport.Future(definition, review.ExchangeTimeZoneId, clock.GetUtcNow()));
                if (originalFuture is not null)
                {
                    if (originalFuture.Symbol != value.Symbol || originalFuture.LastTradeDate != value.LastTradeDate)
                        throw new InvalidOperationException("Selected definition changes contract identity. Use Add.");
                    value = value with { ContractId = originalFuture.ContractId, OnTheRun = originalFuture.OnTheRun, Rollover = originalFuture.Rollover };
                }
                if (value.ReviewState == ReferenceReviewState.Reviewed)
                    Check(FuturesReferenceQualification.Errors(value));
                Future = value;
                status.Text = $"Preview IFM ID: {value.ContractId}; {value.ReviewState}";
            }
            if (IsDisposed || lifetime.IsCancellationRequested || revision != filterRevision) return;
            if (!confirm(this, status.Text)) return;
            DialogResult = DialogResult.OK;
        }
        catch (OperationCanceledException) { if (!IsDisposed) status.Text = "Underlying lookup cancelled or timed out."; }
        catch (Exception e) { if (!IsDisposed) status.Text = e.Message; }
        finally { if (!IsDisposed && revision == filterRevision) select.Enabled = true; }
    }
    void InvalidateSearch()
    {
        filterRevision++;
        rows.DataSource = null;
        select.Enabled = next.Enabled = false;
        status.Text = "Filters changed. Search to load a matching provider snapshot page.";
    }
    static Control Labelled(Control control, string text)
    {
        var panel = new Panel { Width = Math.Max(control.Width, text.Length * 6), Height = 43, Margin = new(3) };
        panel.Controls.Add(new Label { Text = text, AutoSize = true, Location = new(0, 0) });
        control.Location = new(0, 19);
        panel.Controls.Add(control);
        return panel;
    }
    static decimal? ParseStrike(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (!TomasAI.IFM.Domain.MarketData.Shared.FuturesOptionContractId.TryParseStrike(text.Trim(), out var value))
            throw new ArgumentException("Strike filters require an exact positive invariant decimal.");
        return value;
    }
    static void Check(IReadOnlyList<string> errors)
    { if (errors.Count != 0) throw new InvalidOperationException("Missing/invalid reviewed fields: " + string.Join(", ", errors)); }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposedResources)
        { disposedResources = true; lifetime.Cancel(); model.Dispose(); lifetime.Dispose(); }
        base.Dispose(disposing);
    }
}
