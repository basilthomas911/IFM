using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TomasAI.IFM.Domain.Portfolio.Shared.Contracts;
using TomasAI.IFM.Domain.Portfolio.Shared.ViewModels;
using TomasAI.IFM.Domain.Portfolio.Shared.Financial;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Domain.Reference.Shared.Lookups;
using TomasAI.IFM.UI.Net.ViewModels.Portfolio;

namespace TomasAI.IFM.UI.Rust.Bridge.Net;

public sealed record PortfolioChoice(string Value, string Label, bool Enabled = true);
public sealed record PortfolioField(string Key, string Caption, string Value, string Kind = "text", bool Enabled = true,
    PortfolioChoice[]? Choices = null, double Minimum = 0, double Maximum = 1000000000000, int Decimals = 2);
public sealed record PortfolioGrid(string Key, string[] Columns, ContractEditorRow[] Rows, string? Selected = null, bool Editable = false);
public sealed record PortfolioDialog(string Key, string Title, int Width, int Height, int CaptionWidth,
    PortfolioField[] Fields, string[] Buttons, string Status = "", PortfolioGrid[]? Grids = null,
    Dictionary<string,bool>? ButtonEnabled = null);
public sealed record PortfolioDocument(PortfolioGrid[] Grids, Dictionary<string, bool> Enabled,
    Dictionary<string, string> Context, PortfolioDialog[] Dialogs, PortfolioDialog? Policy = null, PortfolioDialog? FundView = null);

/// <summary>Portfolio command/query workflows shared with the WinForms administration view models.</summary>
sealed partial class PortfolioSession : IDisposable
{
    readonly RustClientServiceScope? scope;
    readonly TomasAI.IFM.UI.Net.Services.IUiServiceCatalog services;
    readonly PortfolioAdministrationViewModel model;
    readonly FundMetricsViewModel metrics;
    readonly Task ready;
    readonly CancellationTokenSource stopping = new();
    readonly List<PortfolioDialog> dialogs = [];
    PortfolioOperatingState filter = PortfolioOperatingState.Active;
    FundOperatingState? fundFilter = FundOperatingState.Active;
    DateOnly from = new(Eastern(DateTime.UtcNow).Year, 1, 1), through = DateOnly.FromDateTime(Eastern(DateTime.UtcNow));
    string message = "";
    string refreshParent = "";
    string? inlineFundView;
    object? editing;
    string editMode = "";
    StrategyDeploymentChoice[] deployments = [];
    FundSelectionCatalog? selections;
    static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };
    internal static readonly HashSet<string> Actions = ["load", "portfolio-refresh", "portfolio-select", "portfolio-fund", "portfolio-fund-filter", "portfolio-fund-view", "portfolio-metrics", "portfolio-open", "portfolio-save", "portfolio-cancel", "portfolio-choice", "portfolio-policy", "portfolio-financial", "portfolio-grid"];
    public static bool Validate(ContractEditorIntent input) => input.Schema == 1 && input.RequestId != Guid.Empty
        && input.View == "portfolio" && Actions.Contains(input.Intent) && input.Fields is { Count: <= 4096 }
        && input.Fields.All(x => x.Key.Length <= 128 && x.Value is { Length: <= 131072 })
        && input.Fields.Sum(x => (long)x.Key.Length + x.Value.Length) <= 4194304 && input.OriginalId?.Length is not > 512;
    public PortfolioSession()
    {
        scope = new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
        services = scope.Services;
        model = new(services.PortfolioQueries, services.PortfolioCommands, services.PortfolioFundCommands, services.PortfolioIdentities, true);
        metrics = new(services.PortfolioFinancial);
        ready = scope.StartAsync().AsTask();
    }
    static PortfolioSession()
    {
        // Grid/Details accept objects; retain the concrete row shapes for generated wire accessors.
        DisplayDescriptors.Include<PortfolioReadModel>();
        DisplayDescriptors.Include<FundMandateReadModel>();
        DisplayDescriptors.Include<FundAllocationReadModel>();
        DisplayDescriptors.Include<FundRiskEnvelopeReadModel>();
        DisplayDescriptors.Include<FundTradeTemplateAssignmentReadModel>();
        DisplayDescriptors.Include<PortfolioFinancialPolicyReadModel>();
        DisplayDescriptors.Include<FundFinancialViewState>();
        DisplayDescriptors.Include<FinancialLedgerConfiguration>();
        DisplayDescriptors.Include<TomasAI.IFM.Domain.Trade.Shared.Strategy.Workflow.IntrinsicTime.Pipeline.RiskManagement.RiskHistoryPage>();
    }
    internal PortfolioSession(TomasAI.IFM.UI.Net.Services.IUiServiceCatalog services,
        IPendingFinancialOperationStore? pendingPostingStore=null, IPendingFinancialConfigurationStore? pendingConfigurationStore=null)
    {
        this.services=services;
        postingStore=pendingPostingStore??postingStore;
        configStore=pendingConfigurationStore??configStore;
        model=new(services.PortfolioQueries,services.PortfolioCommands,services.PortfolioFundCommands,services.PortfolioIdentities,true);
        metrics=new(services.PortfolioFinancial);ready=Task.CompletedTask;
    }
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        await ready;
        if(dialogs.Count>0 && input.Intent is "portfolio-save" or "portfolio-financial" or "portfolio-policy" or "portfolio-choice") {
            dialogs[^1]=dialogs[^1] with { Fields=dialogs[^1].Fields.Select(f=>input.Fields.TryGetValue(f.Key,out var value)?f with {Value=value}:f).ToArray() };
        }
        try {
            switch(input.Intent) {
                case "load": case "portfolio-refresh":
                    if(editingPolicy is not null)throw new InvalidOperationException("Save or cancel the edited policy before changing Portfolios.");
                    if(input.Fields.TryGetValue("filter", out var state)) filter = Enum.Parse<PortfolioOperatingState>(state);
                    await Refresh(); break;
                case "portfolio-select":
                    if(editingPolicy is not null)throw new InvalidOperationException("Save or cancel the edited policy before changing Portfolios.");
                    dialogs.Clear(); metrics.Clear();
                    await model.SelectPortfolioAsync(model.Portfolios.Single(p => p.PortfolioId.ToString(CultureInfo.InvariantCulture) == input.OriginalId), stopping.Token);
                    await SelectVisibleFund(); await LoadInlinePolicy(); break;
                case "portfolio-fund": await SelectFund(VisibleFunds().Single(f => FundKey(f) == input.OriginalId)); break;
                case "portfolio-fund-view": await ShowInlineFundView(F(input,"view")); break;
                case "portfolio-fund-filter":
                    var fundState = F(input,"fundFilter");
                    fundFilter = fundState=="All" ? null : Enum.Parse<FundOperatingState>(fundState);
                    if(fundFilter==FundOperatingState.Unknown)throw new ArgumentException("Select a valid Fund state.");
                    await SelectVisibleFund(); break;
                case "portfolio-metrics":
                    from = DateOnly.Parse(input.Fields["from"], CultureInfo.InvariantCulture); through = DateOnly.Parse(input.Fields["through"], CultureInfo.InvariantCulture); await LoadMetrics(); break;
                case "portfolio-open": await Open(input.Fields["view"]); break;
                case "portfolio-save": await Save(input); break;
                case "portfolio-cancel":
                    if(dialogs.LastOrDefault()?.Key=="risk-policy" && editingPolicy is not null && bool.TryParse(F(input,"dirty"),out var dirty) && dirty) {
                        policyDirty=true;
                        dialogs.Add(new("discard-policy","Unsaved Risk Policy",440,180,0,[Field("warning","","Discard unsaved Risk Policy changes?","label",false)],["Yes","No"]));
                    } else if(dialogs.Count>0) {
                        dialogs.RemoveAt(dialogs.Count-1);editing=null;
                        refreshParent=dialogs.LastOrDefault()?.Key is "financials" or "ledger"?dialogs[^1].Key:"";
                    }
                    break;
                case "portfolio-choice": await Choose(input); break;
                case "portfolio-policy": await PolicyAction(input); break;
                case "portfolio-financial": await FinancialAction(input); break;
                case "portfolio-grid": await GridAction(input); break;
            }
        } catch(Exception error) when(error is not OperationCanceledException) { message = error.Message; if(dialogs.Count > 0) dialogs[^1] = dialogs[^1] with { Status = message }; }
        return Publish();
    }
    async Task Refresh()
    {
        var selected = model.SelectedPortfolio?.PortfolioId; var selectedFund = model.SelectedFund;
        model.ClearSelection(); metrics.Clear(); await model.LoadAsync(filter, stopping.Token);
        if((model.Portfolios.FirstOrDefault(p => p.PortfolioId == selected) ?? model.Portfolios.FirstOrDefault()) is {} p) {
            await model.SelectPortfolioAsync(p, stopping.Token);
            await SelectVisibleFund(selectedFund);
        }
        message = model.Message;
        await LoadInlinePolicy();
    }
    async Task SelectFund(FundMandateReadModel fund)
    {
        if(inlineFundView is not null)dialogs.RemoveAll(d=>d.Key is "financials" or "risk-history");
        await model.SelectFundAsync(fund, stopping.Token); await LoadMetrics(); message = model.Message;
        if(inlineFundView is {} view) await ShowInlineFundView(view);
    }
    /// <summary>Loads the selected Funds subtab while retaining the existing nested financial dialog workflow.</summary>
    async Task ShowInlineFundView(string view)
    {
        if(view is not ("financials" or "risk-history"))throw new ArgumentException("Select Financials or Risk History.");
        if(dialogs.Any(d=>d.Key is not ("financials" or "risk-history")))throw new InvalidOperationException("Close the Fund editor before changing its view.");
        inlineFundView=view;refreshParent="";
        dialogs.RemoveAll(d=>d.Key is "financials" or "risk-history");
        if(model.SelectedFund is null)return;
        if(view=="financials")await OpenFinancials();else await OpenRiskHistory();
    }
    static string FundKey(FundMandateReadModel fund) => JsonSerializer.Serialize(new[] {fund.Name, fund.FundMandateVersion.ToString(CultureInfo.InvariantCulture), fund.OperatingState.ToString()});
    IEnumerable<FundMandateReadModel> VisibleFunds() => model.Funds.Where(f=>fundFilter is null || f.OperatingState==fundFilter)
        .OrderBy(f=>f.Name,StringComparer.OrdinalIgnoreCase).ThenByDescending(f=>f.FundMandateVersion);
    async Task SelectVisibleFund(FundMandateReadModel? preferred=null)
    {
        var visible=VisibleFunds().ToArray();
        var fund=visible.FirstOrDefault(f=>preferred is not null&&FundKey(f)==FundKey(preferred))??visible.FirstOrDefault();
        if(fund is not null)await SelectFund(fund);
        else {model.ClearFundSelection();metrics.Clear();dialogs.RemoveAll(d=>d.Key is "financials" or "risk-history");message="No Funds match the selected state.";}
    }
    bool IsCurrentFund => model.SelectedFund is {} selected && !model.Funds.Any(f=>f.FundId==selected.FundId&&f.FundMandateVersion>selected.FundMandateVersion);
    Task LoadMetrics() => model.SelectedFund is {} fund ? metrics.LoadAsync(fund.PortfolioId, fund.FundId, from, through) : Task.CompletedTask;
    PortfolioReadModel Portfolio => model.SelectedPortfolio ?? throw new InvalidOperationException("Select a Portfolio.");
    FundMandateReadModel Fund => model.SelectedFund ?? throw new InvalidOperationException("Select a Fund.");
    static string F(ContractEditorIntent input, string key, string fallback = "") => input.Fields.GetValueOrDefault(key, fallback);
    static decimal N(ContractEditorIntent input, string key) => decimal.Parse(F(input, key, "0"), CultureInfo.InvariantCulture);
    static bool B(ContractEditorIntent input, string key) => bool.Parse(F(input, key, "false"));
    static string[] Csv(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    static PortfolioChoice[] EnumChoices<T>() where T : struct, Enum => Enum.GetValues<T>().Where(v => Convert.ToInt32(v) != 0).Select(v => new PortfolioChoice(v.ToString(), v.ToString())).ToArray();
    static PortfolioField Field(string key, string caption, object? value, string kind = "text", bool enabled = true, PortfolioChoice[]? choices = null, decimal min = 0, decimal max = 1000000000000, int decimals = 2)
        => new(key, caption, value is DateTime date ? Eastern(date).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "", kind, enabled, choices, (double)min, (double)max, decimals);
    static DateTime Eastern(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
    static DateTime Utc(string eastern) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(DateTime.Parse(eastern, CultureInfo.InvariantCulture), DateTimeKind.Unspecified), TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
    static T Patch<T>(T value, ContractEditorIntent input) where T : class
    {
        var node = JsonSerializer.SerializeToNode(value, Json)!.AsObject();
        foreach(var field in input.Fields) {
            var property = typeof(T).GetProperty(field.Key); if(property is null || !node.ContainsKey(field.Key)) continue;
            var t = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            node[field.Key] = t == typeof(string) || t.IsEnum || t == typeof(Guid) ? JsonValue.Create(field.Value)
                : t == typeof(DateTime) ? JsonValue.Create(Utc(field.Value))
                : t == typeof(string[]) ? JsonSerializer.SerializeToNode(Csv(field.Value))
                : t == typeof(bool) ? JsonValue.Create(bool.Parse(field.Value))
                : t == typeof(int) ? JsonValue.Create(checked((int)decimal.Parse(field.Value, CultureInfo.InvariantCulture)))
                : t == typeof(long) ? JsonValue.Create(checked((long)decimal.Parse(field.Value, CultureInfo.InvariantCulture)))
                : t == typeof(decimal) ? JsonValue.Create(decimal.Parse(field.Value, CultureInfo.InvariantCulture))
                : JsonNode.Parse(field.Value);
        }
        return node.Deserialize<T>(Json)!;
    }
    static void Valid(IReadOnlyCollection<string> errors) { if(errors.Count > 0) throw new ArgumentException(string.Join("; ", errors)); }
    static PortfolioGrid Grid(string key, IEnumerable<object> rows, string[]? columns = null, string? selected = null, Func<object, string>? identity = null)
    {
        var items = rows.Select((row, index) => new ContractEditorRow(identity?.Invoke(row) ?? index.ToString(CultureInfo.InvariantCulture), BridgeDocument.Fields(row, enumNames:true))).ToArray();
        if(key is "balances" or "transactions" or "journal")foreach(var row in items)foreach(var name in new[]{"Debits","Credits","Balance","Amount","Debit","Credit"})
            if(row.Fields.TryGetValue(name,out var text)&&decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var amount))row.Fields[name]=amount.ToString("N2",CultureInfo.InvariantCulture);
        var element = rows.GetType().GetInterfaces().FirstOrDefault(t=>t.IsGenericType&&t.GetGenericTypeDefinition()==typeof(IEnumerable<>)&&t.GetGenericArguments()[0]!=typeof(object))?.GetGenericArguments()[0];
        return new(key, columns ?? items.FirstOrDefault()?.Fields.Keys.ToArray() ?? element?.GetProperties().Select(p=>p.Name).ToArray() ?? [], items, selected);
    }
    static PortfolioGrid Details(string key, object? item) => new(key, ["Property", "Value"], item is null ? [] : BridgeDocument.Fields(item, enumNames:true).Select((p,i) => new ContractEditorRow(i.ToString(CultureInfo.InvariantCulture), new() { ["Property"] = p.Key, ["Value"] = p.Value })).ToArray());
    static PortfolioGrid FundSummary(FundMandateReadModel? f, string portfolioName)
    {
        if(f is null)return new("fund",["Property","Value"],[]);
        (string,string)[] rows=[("Fund ID",f.FundId.ToString()),("Portfolio",portfolioName),("Name",f.Name),("Version",f.FundMandateVersion.ToString()),("Trading year",f.TradingYear.ToString()),("State",f.OperatingState.ToString()),("Effective from",f.EffectiveFromUtc.ToString("yyyy-MM-dd")),("Effective until",f.EffectiveUntilUtc?.ToString("yyyy-MM-dd")??"Open ended"),("Decision horizon",f.DecisionHorizon),("Objective",f.Objective),("Underlyings",string.Join(", ",f.UnderlyingUniverse)),("Asset types",string.Join(", ",f.EligibleAssetTypes)),("Directions",string.Join(", ",f.PermittedDirections)),("Conditions",string.Join(", ",f.PermittedConditions)),("Strategy permissions",$"{f.PermittedTradeStrategyFamilies.Length} configured; see Assignments")];
        return new("fund",["Property","Value"],rows.Select((r,i)=>new ContractEditorRow(i.ToString(),new(){["Property"]=r.Item1,["Value"]=r.Item2})).ToArray());
    }
    ContractEditorResult Publish()
    {
        var p = model.SelectedPortfolio; var f = model.SelectedFund;
        var enabled = new Dictionary<string,bool> {
            ["New Portfolio..."] = model.CanMutate, ["Risk Policy..."] = p is not null, ["Portfolio Actions"] = p is not null,
            ["New Version..."] = p is not null, ["Change State..."] = p is not null, ["Delete Draft..."] = p?.OperatingState == PortfolioOperatingState.Draft,
            ["Create Fund..."] = p is not null, ["Change Fund..."] = f is not null&&IsCurrentFund, ["Change Fund State..."] = f is not null&&IsCurrentFund,
            ["Financials..."] = f is not null, ["Risk History..."] = f is not null, ["Allocation..."] = f is not null&&IsCurrentFund, ["Risk Envelope..."] = f is not null&&IsCurrentFund, ["Trade Assignment..."] = f is not null&&IsCurrentFund
        };
        var context = new Dictionary<string,string> { ["status"] = dialogs.LastOrDefault(d=>d.Key=="posting")?.Status??message, ["portfolioName"]=p?.Name??"", ["fundName"]=f?.Name??"", ["filter"] = filter.ToString(), ["fundFilter"]=fundFilter?.ToString()??"All", ["from"] = from.ToString("yyyy-MM-dd"), ["through"] = through.ToString("yyyy-MM-dd"), ["refreshParent"]=refreshParent,["policyDirty"]=policyDirty.ToString() };
        if(metrics.Report is { HasHistory: true } m) {
            string[] values = [m.WinRate.ToString("P2"),m.AverageProfit.ToString("N2"),m.LossRate.ToString("P2"),m.AverageLoss.ToString("N2"),m.WinLossRatio.ToString("F2"),m.ActualSharpeRatio.ToString("F2"),m.PnlAmount.ToString("N2"),m.PnlPercent.ToString("P2"),m.TradeCommission.ToString("N2"),m.MaximumDrawdownPercent?.ToString("P2")??"N/A"];
            for(int i=0;i<values.Length;i++) context[$"metric{i}"]=values[i];
        }
        PortfolioGrid[] grids = [Grid("portfolios", model.Portfolios, selected:p?.PortfolioId.ToString(CultureInfo.InvariantCulture),identity:o=>((PortfolioReadModel)o).PortfolioId.ToString(CultureInfo.InvariantCulture)),
            Grid("funds",VisibleFunds(),["FundId","Name","Portfolio","OperatingState","FundMandateVersion"],f is null?null:FundKey(f),o=>FundKey((FundMandateReadModel)o)),
            FundSummary(f,p?.Name??""),Details("allocation",model.Allocation),Details("envelope",model.RiskEnvelope),Grid("assignments",model.Assignments)];
        foreach(var row in grids.Single(g=>g.Key=="funds").Rows)row.Fields["Portfolio"]=p?.Name??"";
        var fundView=inlineFundView is null?null:dialogs.FirstOrDefault(d=>d.Key==inlineFundView);
        var modalDialogs=dialogs.Where(d=>!ReferenceEquals(d,fundView)).ToArray();
        return new("portfolio",[],[],Context:context) { Documents = new() { ["portfolio"] = BridgeDocument.Encode(new PortfolioDocument(grids,enabled,context,modalDialogs,inlinePolicyMode?inlinePolicy:null,fundView),camelCase:true) } };
    }
    public void Dispose() { stopping.Cancel(); metrics.Dispose(); scope?.DisposeAsync().AsTask().GetAwaiter().GetResult(); stopping.Dispose(); }
}
