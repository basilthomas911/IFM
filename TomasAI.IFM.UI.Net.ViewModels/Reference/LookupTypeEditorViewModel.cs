using TomasAI.IFM.Domain.Reference.Shared;
using TomasAI.IFM.Domain.Reference.Shared.ViewModels;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models;
using TomasAI.IFM.UI.Net.ViewModels.Extensions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

/// <summary>
/// Coordinates lookup maintenance from accepted command through correlated durable completion.
/// </summary>
public sealed class LookupTypeEditorViewModel
    : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly LookupTypeEventModel _eventModel;
    readonly ReferenceCommandModel _commandModel;
    readonly ReferenceQueryModel _queryModel;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly TerminalEventCorrelation _terminalCorrelation = new();
    Dictionary<LookupTypeShortCode, LookupTypeReadModel> _lookupTypes = [];
    List<string> _lookupTypeNames = [];
    List<LookupTypeShortCodeReadModel> _lookupTypeShortCodes = [];
    string _lastStatusMessage = string.Empty;

    public LookupTypeEditorViewModel(IAppRoot appRoot) : base(appRoot)
    {
        _eventModel = appRoot.GetModel<LookupTypeEventModel>();
        _commandModel = appRoot.GetModel<ReferenceCommandModel>();
        _queryModel = appRoot.GetModel<ReferenceQueryModel>();
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    public IDictionary<LookupTypeShortCode, LookupTypeReadModel> LookupTypes => _lookupTypes;
    public ICollection<string> LookupTypeNames => _lookupTypeNames;
    public ICollection<LookupTypeShortCodeReadModel> LookupTypeShortCodes => _lookupTypeShortCodes;
    public Guid CommandId => _terminalCorrelation.CommandId;

    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }

    public Action OnLookupTypeNamesLoaded { get; set; } = () => { };
    public Action OnLookupTypeShortCodesLoaded { get; set; } = () => { };
    public Action<LookupTypeReadModel?> OnLookupTypeLoaded { get; set; } = _ => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };

    public Task AddLookupType(LookupTypeReadModel lookupType, Action onCompleted)
        => ExecuteMutationAsync(
            model => model.AddLookupTypeAsync(lookupType),
            $"Lookup Type {lookupType.Id} Added",
            onCompleted,
            CancellationToken.None);

    public Task ChangeLookupType(
        LookupTypeId lookupTypeId,
        LookupTypeReadModel lookupType,
        bool overwrite,
        Action onCompleted)
        => ExecuteMutationAsync(
            model => model.ChangeLookupTypeAsync(lookupTypeId, lookupType, overwrite),
            $"Lookup Type {lookupTypeId} Changed",
            onCompleted,
            CancellationToken.None);

    public Task RemoveLookupType(LookupTypeId lookupTypeId, bool overwrite)
        => ExecuteMutationAsync(
            model => model.RemoveLookupTypeAsync(lookupTypeId, overwrite),
            $"Lookup Type {lookupTypeId} Removed",
            null,
            CancellationToken.None);

    public async Task LoadLookupTypes()
    {
        await InitializeAsync(CancellationToken.None);
        await LoadLookupTypesCoreAsync(CancellationToken.None);
    }

    public async Task LoadLookupTypeShortCodes(string lookupTypeName)
    {
        LookupTypeShortCodeReadModel[] loaded = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.LoadLookupTypeShortCodesAsync(
                lookupTypeName,
                values => loaded = values ?? []),
            CancellationToken.None);
        _lookupTypeShortCodes = [.. loaded];
        OnLookupTypeShortCodesLoaded?.Invoke();
    }

    public void LoadLookupType(string lookupTypeName, string lookupTypeShortCode)
    {
        _lookupTypes.TryGetValue(
            new LookupTypeShortCode(lookupTypeName, lookupTypeShortCode),
            out var lookupType);
        OnLookupTypeLoaded?.Invoke(lookupType);
    }

    public LookupTypeReadModel? GetLookupType(string lookupTypeName, string lookupTypeShortCode)
        => _lookupTypes.GetValueOrDefault(new LookupTypeShortCode(lookupTypeName, lookupTypeShortCode));

    public LookupTypeReadModel? GetLookupType(string lookupTypeName, int orderId)
        => _lookupTypes.Values.FirstOrDefault(value =>
            string.Equals(value.LookupTypeName, lookupTypeName, StringComparison.Ordinal)
            && value.OrderId == orderId);

    public string GetLookupTypeName(int lookupTypeNameIndex)
        => lookupTypeNameIndex >= 0 && lookupTypeNameIndex < _lookupTypeNames.Count
            ? _lookupTypeNames[lookupTypeNameIndex]
            : string.Empty;

    public string GetLookupTypeShortCode(int lookupTypeShortCodeIndex)
        => lookupTypeShortCodeIndex >= 0 && lookupTypeShortCodeIndex < _lookupTypeShortCodes.Count
            ? _lookupTypeShortCodes[lookupTypeShortCodeIndex].ShortCode
            : string.Empty;

    public int GetNextOrderId(string lookupTypeName)
        => _lookupTypes.Values
            .Where(value => string.Equals(value.LookupTypeName, lookupTypeName, StringComparison.Ordinal))
            .Select(value => value.OrderId)
            .DefaultIfEmpty(-1)
            .Max() + 1;

    public Task InitializeAsync(CancellationToken cancellationToken)
        => _lifecycle.InitializeAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _lifecycle.StopAsync(cancellationToken);
        _terminalCorrelation.EndAttempt();
        OnPropertyChanged(nameof(CommandId));
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(CancellationToken.None);
        await _lifecycle.DisposeAsync();
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StartAsync(HandleTerminalEventAsync).AsTask(),
            cancellationToken);

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _eventModel.ExecuteObservableAsync(
            model => model.StopAsync().AsTask(),
            cancellationToken);

    async Task LoadLookupTypesCoreAsync(CancellationToken cancellationToken)
    {
        ICollection<LookupTypeReadModel> loadedTypes = [];
        string[] loadedNames = [];
        await _queryModel.ExecuteObservableAsync(
            model => model.LoadLookupTypesAsync(values => loadedTypes = values ?? []),
            cancellationToken);
        await _queryModel.ExecuteObservableAsync(
            model => model.LoadLookupTypeNamesAsync(values => loadedNames = values ?? []),
            cancellationToken);

        _lookupTypes = [];
        foreach (var lookupType in loadedTypes)
            _lookupTypes.TryAdd(lookupType.ShortCodeId, lookupType);
        _lookupTypeNames = [.. loadedNames.Distinct(StringComparer.Ordinal)];
        OnLookupTypeNamesLoaded?.Invoke();
    }

    async Task ExecuteMutationAsync(
        Func<ReferenceCommandModel, Task<Guid>> submit,
        string statusMessage,
        Action? onCompleted,
        CancellationToken cancellationToken)
    {
        _terminalCorrelation.BeginAttempt();
        OnWaitCursor?.Invoke();
        try
        {
            Guid commandId = Guid.Empty;
            await _commandModel.ExecuteObservableAsync(
                async model => commandId = await submit(model),
                cancellationToken);
            if (commandId == Guid.Empty)
                throw new InvalidOperationException(
                    "The lookup-type command returned an empty correlation identifier.");

            var terminalTask = _terminalCorrelation.AwaitAsync(commandId, cancellationToken);
            OnPropertyChanged(nameof(CommandId));
            var terminalEvent = await terminalTask;
            if (terminalEvent is IErrorEvent error)
                throw new ModelOperationException(error.ErrorCode, error.ErrorMessage);

            await LoadLookupTypesCoreAsync(cancellationToken);
            LastStatusMessage = statusMessage;
            onCompleted?.Invoke();
        }
        finally
        {
            _terminalCorrelation.EndAttempt();
            OnPropertyChanged(nameof(CommandId));
            OnDefaultCursor?.Invoke();
        }
    }

    ValueTask HandleTerminalEventAsync(IEvent terminalEvent)
    {
        _terminalCorrelation.TryPublish(terminalEvent);
        return ValueTask.CompletedTask;
    }
}
