using TomasAI.IFM.Domain.Reference.Shared.ServiceApi;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Views.Portfolio;

namespace TomasAI.IFM.UI.Net.Views.Reference;

/// <summary>The active Reference strategy catalog UI, backed exclusively by ConfigurationDb APIs.</summary>
public sealed class StrategyCatalogReferenceView : DarkTradingView, IControlCommand, IAsyncFormControl
{
    readonly IReferenceQueryApi queries;
    readonly IReferenceCommandApi commands;
    readonly Func<string, DialogResult> confirm;
    readonly CheckBox showAll = new() { Text = "Show all catalog data", AccessibleName = "Show all catalog data", AutoSize = true, Margin = new Padding(6, 8, 0, 0) };
    static readonly HashSet<(StrategyCatalogKind, Guid)> defaultIdentities = StrategyCatalogDefaults.Create().Select(x => (x.Key.Kind, x.Key.Id)).ToHashSet();
    readonly ComboBox kind = PortfolioUiStyle.Combo("Catalog section");
    readonly ComboBox template = PortfolioUiStyle.Combo("Starting definition");
    readonly ListBox list = new() { Name = "CatalogDefinitions", AccessibleName = "Catalog definitions", Dock = DockStyle.Fill, IntegralHeight = false, HorizontalScrollbar = true };
    readonly Panel detail = new() { Dock = DockStyle.Fill };
    readonly Label message = new() { AutoSize = true, Dock = DockStyle.Top, MaximumSize = new Size(1300, 0), AccessibleName = "Catalog status" };
    readonly Button publish = PortfolioUiStyle.Button("Publish", "Publish exact catalog version");
    readonly Button refresh = PortfolioUiStyle.Button("Refresh", "Refresh catalog");
    readonly CancellationTokenSource lifetime = new();
    readonly Dictionary<CatalogKey, StrategyCatalogSummary> references = [];
    CatalogProduct[] products = [];
    StrategyCatalogDefinitionEditor? editor;
    StoredStrategyCatalogDefinition? selected;
    Task pending = Task.CompletedTask;
    int generation;
    bool closed, loaded, loading;
    CatalogCommandRequest? retry;
    public event EventHandler? StateChanged;
    public bool IsEditing { get; private set; }
    public bool IsChanging { get; private set; }
    public bool IsSaving { get; private set; }
    public bool CanAdd => loaded && !closed && !loading && !IsEditing && !IsSaving;
    public bool CanSave => IsEditing && editor is not null && !IsSaving;
    public bool CanChangeRemove => CanAdd && selected is not null;
    public bool CanRemove => CanChangeRemove && selected!.Status == CatalogLifecycleStatus.Published;
    public bool CanImport => false;

    public StrategyCatalogReferenceView(IReferenceQueryApi queries, IReferenceCommandApi commands, Func<string, DialogResult>? confirm = null)
    {
        this.queries = queries; this.commands = commands;
        this.confirm = confirm ?? (text => MessageBox.Show(this, text, "Remove strategy definition", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2));
        Name = nameof(StrategyCatalogReferenceView); AccessibleName = "ConfigurationDb trade strategy families";
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 44, WrapContents = false, AutoScroll = true };
        kind.Width = 175; kind.Dock = DockStyle.None; template.Width = 230; template.Dock = DockStyle.None;
        kind.Items.AddRange(Enum.GetValues<StrategyCatalogKind>().Cast<object>().ToArray());
        bar.Controls.AddRange([new Label { Text = "Manage", AutoSize = true, Padding = new Padding(0, 9, 0, 0) }, kind,
            new Label { Text = "Start from", AutoSize = true, Padding = new Padding(0, 9, 0, 0) }, template, publish, refresh, showAll]);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        body.ColumnStyles.Add(new(SizeType.Percent, 29)); body.ColumnStyles.Add(new(SizeType.Percent, 71)); body.RowStyles.Add(new(SizeType.Percent, 100));
        body.Controls.Add(list, 0, 0); body.Controls.Add(detail, 1, 0);
        Controls.Add(body); Controls.Add(message); Controls.Add(bar); DarkTradingTheme.Apply(this);
        showAll.CheckedChanged += async (_, _) => { if (loaded && !IsEditing && !loading) await LoadKindAsync(); };
        kind.SelectedIndexChanged += async (_, _) => { if (loaded && !IsEditing) await LoadKindAsync(); };
        list.SelectedIndexChanged += async (_, _) => { if (!loading && !IsEditing) await SelectAsync(); };
        publish.Click += (_, _) => { if (selected is { Status: CatalogLifecycleStatus.Draft } && !IsEditing && !IsSaving) Start(() => LifecycleAsync(CatalogCommandOperation.Publish)); };
        refresh.Click += (_, _) => { if (!IsEditing && !IsSaving) Start(async () => await LoadAsync()); };
        kind.SelectedItem = StrategyCatalogKind.Family; Notify();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, ct);
        try
        {
            loaded = false; loading = true; ++generation; selected = null; ClearEditor(); Notify(); references.Clear();
            foreach (var value in Enum.GetValues<StrategyCatalogKind>())
                foreach (var item in await ReadAll(value, linked.Token)) references[item.Key] = item;
            var productRows = new List<CatalogProduct>();
            foreach (var family in new[] { TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType.Futures, TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType.FuturesOption })
            {
                var response = await queries.GetTradeStrategySymbolsAsync(family, linked.Token);
                if (!response.Success || response.Value is null) throw new InvalidOperationException(response.ErrorMessage ?? "Product catalog unavailable.");
                productRows.AddRange(response.Value.Select(x => new CatalogProduct(x.Id, x.Symbol, x.Exchange, x.Currency)));
            }
            products = productRows.Distinct().ToArray();
            if (closed) return;
            loaded = true; loading = false; await LoadKindAsync();
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception ex) { Error(ex.Message); }
        finally { loading = false; Notify(); }
    }

    async Task<StrategyCatalogSummary[]> ReadAll(StrategyCatalogKind value, CancellationToken ct)
    {
        var result = new List<StrategyCatalogSummary>(); string? cursor = null;
        do
        {
            var response = await queries.QueryStrategyCatalogAsync(new(CatalogQueryOperation.List, value, Limit: 100, AfterCode: cursor), ct);
            if (!response.Success || response.Value is null) throw new InvalidOperationException(response.ErrorMessage ?? "Catalog unavailable.");
            var page = StrategyCatalogJson.Read<StrategyCatalogSummary[]>(response.Value);
            result.AddRange(page);
            if (page.Length < 100) break;
            var next = page[^1].Code;
            if (next == cursor || result.Count >= 4096) throw new InvalidOperationException("Catalog exceeds the editor limit or returned a repeated page.");
            cursor = next;
        } while (true);
        return result.ToArray();
    }

    async Task LoadKindAsync()
    {
        if (closed || kind.SelectedItem is not StrategyCatalogKind value) return;
        loading = true; selected = null; ++generation; ClearEditor();
        template.Items.Clear(); template.Items.Add("Custom definition");
        template.Items.AddRange(StrategyCatalogDefaults.Create().Where(x => x.Key.Kind == value).Select(x => new TemplateChoice(x)).Cast<object>().ToArray()); template.SelectedIndex = 0;
        list.Items.Clear(); list.Items.AddRange(references.Values.Where(x => x.Key.Kind == value && VisibleDefinition(x)).OrderBy(StrategyCatalogDefaults.DisplayOrder).ThenBy(x => x.Name).Select(x => new Choice(x)).Cast<object>().ToArray());
        if (list.Items.Count > 0) list.SelectedIndex = 0;
        loading = false;
        message.Text = list.Items.Count == 0 ? "No default definitions in this section. Add one or select Show all catalog data." : !showAll.Checked && value == StrategyCatalogKind.Family ? "Default families: Futures, Vertical Spreads and Iron Condor." : "Change creates a new version. Publish validates its dependencies and capabilities.";
        await SelectAsync(); Notify();
    }

    async Task SelectAsync()
    {
        var mine = ++generation;
        selected = null; ClearEditor(); Notify();
        if (list.SelectedItem is not Choice choice || closed) return;
        try
        {
            var result = await queries.QueryStrategyCatalogAsync(new(CatalogQueryOperation.Exact, Key: choice.Value.Key), lifetime.Token);
            if (closed || mine != generation) return;
            if (!result.Success || result.Value is null || result.Value == "null") throw new InvalidOperationException(result.ErrorMessage ?? "Exact catalog version is unavailable.");
            selected = StrategyCatalogJson.Read<StoredStrategyCatalogDefinition>(result.Value);
            ShowEditor(selected.Definition, false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception ex) { Error(ex.Message); }
        finally { Notify(); }
    }

    public void Add(Action<bool> action)
    {
        if (IsEditing && !IsChanging && CanSave) { Start(() => SaveAsync(action)); return; }
        if (!CanAdd || kind.SelectedItem is not StrategyCatalogKind value) return;
        var id = Guid.NewGuid();
        var definition = template.SelectedItem is TemplateChoice t ? t.Value with { Key = new(value, id, 1), Code = t.Value.Code + "-" + id.ToString("N")[..6] }
            : new StrategyCatalogDefinition { Key = new(value, id, 1), Code = "", Name = "", Horizon = value == StrategyCatalogKind.Deployment ? TomasAI.IFM.Domain.MarketData.Analytics.Shared.TimeFrameType.Daily : default,
                Side = value == StrategyCatalogKind.Variant ? "Long" : "", Bias = value == StrategyCatalogKind.Variant ? "Balanced" : "", PremiumMode = value == StrategyCatalogKind.Variant ? "None" : "" };
        IsEditing = true; IsChanging = false; retry = null; ++generation; ShowEditor(definition, true); action(false); Notify();
    }
    public void Change(Action<bool> action)
    {
        if (IsChanging && CanSave) { Start(() => SaveAsync(action)); return; }
        if (!CanChangeRemove || selected is null) return;
        IsEditing = IsChanging = true; retry = null; ++generation;
        ShowEditor(selected.Definition with { Key = selected.Definition.Key with { Version = checked(selected.Definition.Key.Version + 1) } }, true); action(false); Notify();
    }
    async Task SaveAsync(Action<bool> action)
    {
        var definition = editor!.ReadDefinition();
        var request = new CatalogCommandRequest(Guid.NewGuid(), CatalogCommandOperation.SaveDraft, Definition: definition, ExpectedPreviousVersion: definition.Key.Version - 1);
        if (retry is { Operation: CatalogCommandOperation.SaveDraft } && StrategyCatalogJson.Write(retry.Definition) == StrategyCatalogJson.Write(definition)) request = retry;
        retry = request;
        await Send(request);
        if (!defaultIdentities.Contains((definition.Key.Kind, definition.Key.Id))) showAll.Checked = true;
        IsEditing = IsChanging = false; retry = null; action(true); await ReloadSelected(definition.Key);
        message.Text = "Draft saved. Publish after configuring its exact dependencies and supported capabilities.";
    }
    public void Remove()
    {
        if (!CanRemove || selected is null) return;
        var definition = selected.Definition;
        var label = definition.Key.Kind == StrategyCatalogKind.Deployment
            ? $"{definition.Name}-{string.Join(",", definition.Products.Select(x => x.Symbol))}-{string.Join(",", definition.Products.Select(x => x.Currency).Distinct())}-{definition.Horizon} {string.Join(",", definition.Products.Select(x => x.Exchange).Distinct())}"
            : definition.Name;
        if (confirm($"Remove {label} ?") != DialogResult.Yes) return;
        Start(() => LifecycleAsync(CatalogCommandOperation.Retire));
    }
    async Task LifecycleAsync(CatalogCommandOperation operation)
    {
        var row = selected!;
        var request = retry is { } previous && previous.Operation == operation && previous.Key == row.Definition.Key ? previous :
            new CatalogCommandRequest(Guid.NewGuid(), operation, Key: row.Definition.Key, ExpectedHash: row.ContentHash, EffectiveUtc: new DateTime(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc));
        retry = request; await Send(request); retry = null; await ReloadSelected(row.Definition.Key);
        message.Text = operation == CatalogCommandOperation.Publish ? "Definition published. Fund assignment and workflow activation are separate." : "Definition retired. Historical versions remain available.";
    }
    async Task Send(CatalogCommandRequest request)
    {
        var result = await commands.ExecuteStrategyCatalogAsync(request, lifetime.Token);
        if (!result.Success) throw new InvalidOperationException(result.ErrorMessage ?? "Catalog operation failed.");
    }
    async Task ReloadSelected(CatalogKey key)
    {
        await LoadAsync();
        if (closed) return;
        list.SelectedItem = list.Items.Cast<Choice>().SingleOrDefault(x => x.Value.Key == key);
        await SelectAsync();
    }
    void Start(Func<Task> operation)
    {
        if (IsSaving || closed) return;
        IsSaving = true; Notify(); pending = Run();
        async Task Run()
        {
            try { await operation(); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            catch (Exception ex) { Error(ex.Message); }
            finally { IsSaving = false; Notify(); }
        }
    }
    void ShowEditor(StrategyCatalogDefinition definition, bool editable)
    { ClearEditor(); editor = new(definition, editable, references.Values.Where(VisibleDefinition).ToArray(), products); detail.Controls.Add(editor); }
    bool VisibleDefinition(StrategyCatalogSummary definition) => showAll.Checked || defaultIdentities.Contains((definition.Key.Kind, definition.Key.Id))
        || (definition.Key.Kind == StrategyCatalogKind.Deployment && definition.Code.StartsWith("Legacy-", StringComparison.Ordinal));
    void ClearEditor() { if (editor is not null) { detail.Controls.Remove(editor); editor.Dispose(); editor = null; } }
    void Error(string text) { if (!closed) message.Text = text; }
    void Notify()
    {
        if (closed || IsDisposed) return;
        showAll.Enabled = kind.Enabled = template.Enabled = list.Enabled = !IsEditing && !IsSaving && !loading;
        refresh.Enabled = !IsEditing && !IsSaving && !loading;
        publish.Enabled = CanChangeRemove && selected?.Status == CatalogLifecycleStatus.Draft;
        if (editor is not null) editor.Enabled = !IsSaving;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
    public bool Close(Action<bool> action)
    {
        if (IsSaving) return false;
        if (!IsEditing) return true;
        IsEditing = IsChanging = false; retry = null;
        if (selected is not null) ShowEditor(selected.Definition, false); else ClearEditor();
        action(true); Notify(); return false;
    }
    public void Load(IAppRoot appRoot, Action<bool> action) => Start(async () => { await LoadAsync(); action(true); });
    public void Import() { }
    public void Unload() { closed = true; ++generation; lifetime.Cancel(); }
    public async ValueTask CloseAsync() { Unload(); await pending; ClearEditor(); }
    public void Open() { }
    public void Resize(Control control) => Dock = DockStyle.Fill;
    public void Close() => Unload();
    protected override void Dispose(bool disposing) { if (disposing && !IsDisposed) { Unload(); lifetime.Dispose(); } base.Dispose(disposing); }
    sealed record Choice(StrategyCatalogSummary Value) { public override string ToString() => $"{Value.Name}  [v{Value.Key.Version} {Value.Status}]"; }
    sealed record TemplateChoice(StrategyCatalogDefinition Value) { public override string ToString() => Value.Name; }
}
