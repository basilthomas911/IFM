using System.Globalization;
using TomasAI.IFM.Domain.MarketData.Shared.ViewModels;
using TomasAI.IFM.UI.Net.Services.MarketData;
using TomasAI.IFM.UI.Net.ViewModels.MarketData;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;
/// <summary>Reads the published stored definition catalog using the existing bounded selector view model. No feed subscriptions or reference writes.</summary>
sealed class ProviderDefinitionSession : IDisposable
{
    readonly RustClientServiceScope scope = new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
    readonly InstrumentDefinitionSelectorViewModel model;
    readonly CancellationTokenSource stopping = new();
    readonly Task ready;
    InstrumentDefinitionPageRequest? search;
    ProviderDraft? draft;
    ContractEditorSession? futureEditor, optionEditor;
    sealed record ProviderDraft(string Token, string SelectedId, InstrumentDefinitionSelection Definition, string Zone,
        FuturesContractV3ReadModel? Future, FuturesContractV3ReadModel? OriginalFuture,
        FuturesOptionContractReadModel? Option, FuturesOptionContractReadModel? OriginalOption,
        FuturesContractV3ReadModel? Underlying);
    static string Key(InstrumentDefinitionSelection row) => $"{row.SnapshotId:N}:{row.Dataset}:{row.PublisherId}:{row.InstrumentId}";

    public ProviderDefinitionSession() { model = new(scope.Container.GetInstance<MarketDataQueryService>()); ready = scope.StartAsync(stopping.Token).AsTask(); }
    static string Field(ContractEditorIntent input, string name) => input.Fields.GetValueOrDefault(name)?.Trim() ?? "";
    static string? Optional(ContractEditorIntent input, string name) => Field(input,name) is { Length: > 0 } value ? value : null;
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        await ready;
        if(input.Intent is "preview" or "publish") return await MapAsync(input);
        draft = null;
        var request = input.Intent == "next" && search is not null && model.Page is { ContinuationToken: not null } previous
            ? search with { SnapshotId = previous.SnapshotId, ContinuationToken = previous.ContinuationToken }
            : new InstrumentDefinitionPageRequest {
                Dataset = Field(input,"dataset"), Root = Field(input,"root"), PageSize = 100,
                Options = ParseBool(input,"options"), Exchange = Optional(input,"exchange"),
                Expiry = Optional(input,"expiry") is { } expiry ? DateOnly.ParseExact(expiry,"yyyy-MM-dd",CultureInfo.InvariantCulture) : null,
                Right = Optional(input,"right") is { } right ? Enum.Parse<ReferenceOptionRight>(right,false) : ReferenceOptionRight.Unknown,
                UnderlyingInstrumentId = Optional(input,"underlyingInstrumentId") is { } underlying ? uint.Parse(underlying,CultureInfo.InvariantCulture) : null,
                MinimumStrike = Optional(input,"minimumStrike") is { } min ? decimal.Parse(min,CultureInfo.InvariantCulture) : null,
                MaximumStrike = Optional(input,"maximumStrike") is { } max ? decimal.Parse(max,CultureInfo.InvariantCulture) : null,
                IncludeExpiredOrDeleted = ParseBool(input,"includeHistory")
            };
        if (!await model.SearchAsync(request,stopping.Token) || model.Page is not { } page) throw new OperationCanceledException("Definition search superseded");
        if (request.SnapshotId != Guid.Empty && page.SnapshotId != request.SnapshotId) throw new InvalidDataException("Definition page changed the pinned snapshot");
        search = request;
        return Result(input.View);
    }
    ContractEditorResult Result(string view)
    {
        var page = model.Page ?? throw new InvalidOperationException("Search for a stored definition first");
        var rows = page.Items.Select(row => new ContractEditorRow(Key(row),
            BridgeDocument.Fields(row,camelCase:true))).ToArray();
        var documents=new Dictionary<string,byte[]>();
        var context = new Dictionary<string,string> { ["snapshotId"] = page.SnapshotId.ToString("D"), ["snapshotCompletedUtc"] = DateTime.SpecifyKind(page.SnapshotCompletedUtc,DateTimeKind.Utc).ToString("O",CultureInfo.InvariantCulture) };
        if(draft is { } preview)
        {
            context["previewToken"] = preview.Token;
            context["previewSelectedId"] = preview.SelectedId;
            context["previewMode"] = preview.OriginalFuture is not null || preview.OriginalOption is not null ? "Change" : "Add";
            documents["previewContract"] = BridgeDocument.Encode((object?)preview.Future??preview.Option,camelCase:true);
        }
        return new(view,rows,new(){["right"] = Enum.GetNames<ReferenceOptionRight>()},page.ContinuationToken is not null,context){Documents=documents};
    }
    async Task<ContractEditorResult> MapAsync(ContractEditorIntent input)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        var query = scope.Container.GetInstance<TomasAI.IFM.Domain.MarketData.Shared.ServiceApi.IMarketDataQueryApi>();
        if(input.Intent == "preview")
        {
            draft = null;
            var definition = model.Page?.Items.SingleOrDefault(row => Key(row) == input.OriginalId)
                ?? throw new ArgumentException("Select a definition from the current stored page");
            var zone = Field(input,"exchangeTimeZoneId");
            var target = Optional(input,"existingContractId");
            var token = Guid.NewGuid().ToString("N");
            if(definition.InstrumentClass == "F")
            {
                FuturesContractV3ReadModel? original = null;
                if(target is not null)
                {
                    var read = await query.GetFuturesContractAsync(target).WaitAsync(timeout.Token);
                    if(!read.Success || read.Value is null)throw new ArgumentException("The existing futures contract was not found");
                    original = read.Value;
                }
                var mapped = ProviderReferenceMapping.Future(definition,zone,DateTimeOffset.UtcNow,original);
                draft = new(token,input.OriginalId!,definition,zone,mapped,original,null,null,null);
            }
            else
            {
                var underlying = await model.ResolveUnderlyingAsync(Field(input,"underlyingContractId"),timeout.Token);
                FuturesOptionContractReadModel? original = null;
                if(target is not null)
                {
                    var read = await query.GetFuturesOptionContractAsync(target).WaitAsync(timeout.Token);
                    if(!read.Success || read.Value is null)throw new ArgumentException("The existing option contract was not found");
                    original = read.Value;
                }
                var mapped = ProviderReferenceMapping.Option(definition,underlying,zone,DateTimeOffset.UtcNow,original);
                draft = new(token,input.OriginalId!,definition,zone,null,null,mapped,original,underlying);
            }
        }
        else
        {
            var saved = draft ?? throw new ArgumentException("Preview the provider mapping before saving");
            if(saved.Token != Field(input,"previewToken") || saved.SelectedId != input.OriginalId)
                throw new ArgumentException("The mapping preview no longer matches this selection");
            // Consume before dispatch. Unknown completion cannot be retried using the same preview.
            draft = null;
            if(saved.Future is { } future)
            {
                if(saved.OriginalFuture is { } original)
                {
                    var current = await query.GetFuturesContractAsync(original.ContractId).WaitAsync(timeout.Token);
                    if(!current.Success || current.Value != original)throw new ArgumentException("The existing contract changed. Preview again.");
                }
                _ = ProviderReferenceMapping.Future(saved.Definition,saved.Zone,DateTimeOffset.UtcNow,saved.OriginalFuture);
                await (futureEditor ??= new(false)).SaveProviderAsync(future,saved.OriginalFuture);
            }
            else
            {
                var underlying = await model.ResolveUnderlyingAsync(saved.Underlying!.ContractId,timeout.Token);
                if(underlying != saved.Underlying)throw new ArgumentException("The underlying changed. Preview again.");
                if(saved.OriginalOption is { } original)
                {
                    var current = await query.GetFuturesOptionContractAsync(original.ContractId).WaitAsync(timeout.Token);
                    if(!current.Success || current.Value != original)throw new ArgumentException("The existing contract changed. Preview again.");
                }
                _ = ProviderReferenceMapping.Option(saved.Definition,underlying,saved.Zone,DateTimeOffset.UtcNow,saved.OriginalOption);
                await (optionEditor ??= new(true)).SaveProviderAsync(saved.Option!,saved.OriginalOption);
            }
        }
        return Result(input.View);
    }
    static bool ParseBool(ContractEditorIntent input,string name) => bool.TryParse(Field(input,name),out var value) ? value : Field(input,name).Length == 0 ? false : throw new ArgumentException($"Invalid {name}");
    public void Dispose() { stopping.Cancel(); model.Dispose(); futureEditor?.Dispose(); optionEditor?.Dispose(); scope.DisposeAsync().AsTask().GetAwaiter().GetResult(); stopping.Dispose(); }
}
