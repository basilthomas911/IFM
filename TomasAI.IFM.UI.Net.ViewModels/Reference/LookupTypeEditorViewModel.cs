using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.Operations;
using TomasAI.IFM.UI.Net.Models.Reference;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Reference;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;
using TomasAI.IFM.UI.Net.ViewModels.Operations.Domain;

namespace TomasAI.IFM.UI.Net.ViewModels.Reference;

/// <summary>
/// Coordinates lookup maintenance from accepted command through correlated durable completion.
/// </summary>
public sealed class LookupTypeEditorViewModel
    : BaseEditorViewModel, IAsyncLifecycle, IAsyncDisposable
{
    readonly IReferenceDataService _service;
    readonly IUiEventSubscription _subscription;
    readonly AsyncLifecycleCoordinator _lifecycle;
    readonly TerminalNotificationCorrelation _terminalCorrelation = new();
    Dictionary<(string Name, string ShortCode), LookupTypeUiModel> _lookupTypes = [];
    List<string> _lookupTypeNames = [];
    List<LookupTypeShortCodeUiModel> _lookupTypeShortCodes = [];
    string _lastStatusMessage = string.Empty;

    /// <summary>Creates the lookup editor with its explicit Reference service.</summary>
    public LookupTypeEditorViewModel(IAppRoot appRoot, IReferenceDataService service) : base(appRoot)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _subscription = _service.CreateLookupSubscription(HandleTerminalNotificationAsync);
        _lifecycle = new AsyncLifecycleCoordinator(StartListenerCoreAsync, StopListenerCoreAsync);
    }

    /// <summary>Gets lookup definitions keyed by category and short code.</summary>
    public IReadOnlyDictionary<(string Name, string ShortCode), LookupTypeUiModel> LookupTypes => _lookupTypes;

    /// <summary>Gets the available lookup category names.</summary>
    public ICollection<string> LookupTypeNames => _lookupTypeNames;

    /// <summary>Gets short-code selectors for the selected lookup category.</summary>
    public ICollection<LookupTypeShortCodeUiModel> LookupTypeShortCodes => _lookupTypeShortCodes;

    /// <summary>Gets the command identifier while a mutation awaits completion.</summary>
    public Guid CommandId => _terminalCorrelation.CommandId;

    public string LastStatusMessage
    {
        get => _lastStatusMessage;
        private set => SetProperty(ref _lastStatusMessage, value);
    }

    public Action OnLookupTypeNamesLoaded { get; set; } = () => { };
    public Action OnLookupTypeShortCodesLoaded { get; set; } = () => { };
    public Action<LookupTypeUiModel?> OnLookupTypeLoaded { get; set; } = _ => { };
    public Action OnWaitCursor = () => { };
    public Action OnDefaultCursor = () => { };

    /// <summary>Adds a lookup definition and waits for its correlated terminal notification.</summary>
    public Task AddLookupType(LookupTypeUiModel lookupType, Action onCompleted)
        => ExecuteMutationAsync(
            cancellationToken => _service.AddLookupTypeAsync(lookupType, cancellationToken),
            $"Lookup Type {lookupType.LookupTypeName}:{lookupType.OrderId} Added",
            onCompleted,
            CancellationToken.None);

    public Task ChangeLookupType(
        string lookupTypeName,
        int orderId,
        LookupTypeUiModel lookupType,
        bool overwrite,
        Action onCompleted)
        => ExecuteMutationAsync(
            cancellationToken => _service.ChangeLookupTypeAsync(
                lookupTypeName, orderId, lookupType, overwrite, cancellationToken),
            $"Lookup Type {lookupTypeName}:{orderId} Changed",
            onCompleted,
            CancellationToken.None);

    /// <summary>Removes a lookup definition and waits for its correlated terminal notification.</summary>
    public Task RemoveLookupType(string lookupTypeName, int orderId, bool overwrite)
        => ExecuteMutationAsync(
            cancellationToken => _service.RemoveLookupTypeAsync(
                lookupTypeName, orderId, overwrite, cancellationToken),
            $"Lookup Type {lookupTypeName}:{orderId} Removed",
            null,
            CancellationToken.None);

    public async Task LoadLookupTypes()
    {
        await InitializeAsync(CancellationToken.None);
        await LoadLookupTypesCoreAsync(CancellationToken.None);
    }

    public async Task LoadLookupTypeShortCodes(string lookupTypeName)
    {
        _lookupTypeShortCodes = [.. (await _service.GetLookupTypeShortCodesAsync(
            lookupTypeName,
            CancellationToken.None)).RequireValue()];
        OnLookupTypeShortCodesLoaded?.Invoke();
    }

    public void LoadLookupType(string lookupTypeName, string lookupTypeShortCode)
    {
        _lookupTypes.TryGetValue(
            (lookupTypeName, lookupTypeShortCode),
            out var lookupType);
        OnLookupTypeLoaded?.Invoke(lookupType);
    }

    public LookupTypeUiModel? GetLookupType(string lookupTypeName, string lookupTypeShortCode)
        => _lookupTypes.GetValueOrDefault((lookupTypeName, lookupTypeShortCode));

    public LookupTypeUiModel? GetLookupType(string lookupTypeName, int orderId)
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
        await _subscription.DisposeAsync();
    }

    Task StartListenerCoreAsync(CancellationToken cancellationToken)
        => _subscription.StartAsync(cancellationToken).AsTask();

    Task StopListenerCoreAsync(CancellationToken cancellationToken)
        => _subscription.StopAsync(cancellationToken).AsTask();

    async Task LoadLookupTypesCoreAsync(CancellationToken cancellationToken)
    {
        var loadedTypes = (await _service.GetLookupTypesAsync(cancellationToken)).RequireValue();
        var loadedNames = (await _service.GetLookupTypeNamesAsync(cancellationToken)).RequireValue();

        _lookupTypes = [];
        foreach (var lookupType in loadedTypes)
            _lookupTypes.TryAdd((lookupType.LookupTypeName, lookupType.ShortCode), lookupType);
        _lookupTypeNames = [.. loadedNames.Distinct(StringComparer.Ordinal)];
        OnLookupTypeNamesLoaded?.Invoke();
    }

    async Task ExecuteMutationAsync(
        Func<CancellationToken, ValueTask<UiOperationResult<Guid>>> submit,
        string statusMessage,
        Action? onCompleted,
        CancellationToken cancellationToken)
    {
        _terminalCorrelation.BeginAttempt();
        OnWaitCursor?.Invoke();
        try
        {
            var commandId = (await submit(cancellationToken)).RequireValue();
            if (commandId == Guid.Empty)
                throw new InvalidOperationException(
                    "The lookup-type command returned an empty correlation identifier.");

            var terminalTask = _terminalCorrelation.AwaitAsync(commandId, cancellationToken);
            OnPropertyChanged(nameof(CommandId));
            var terminalNotification = await terminalTask;
            if (terminalNotification.IsFailure)
                throw new UiOperationException(new UiOperationError(
                    terminalNotification.ErrorCode,
                    terminalNotification.ErrorMessage));

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

    ValueTask HandleTerminalNotificationAsync(TerminalNotificationUiModel terminalNotification)
    {
        _terminalCorrelation.TryPublish(terminalNotification);
        return ValueTask.CompletedTask;
    }
}
