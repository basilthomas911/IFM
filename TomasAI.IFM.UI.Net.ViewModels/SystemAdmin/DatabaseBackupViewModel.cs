using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Contracts;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.Lifecycle;

namespace TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

/// <summary>
/// Coordinates the NATS-only database-backup dashboard without waiting for native backup completion.
/// </summary>
public sealed class DatabaseBackupViewModel : IAsyncLifecycle, IAsyncDisposable
{
    readonly IDatabaseBackupService _service;
    readonly IUiEventSubscription _subscription;
    readonly AsyncLifecycleCoordinator _lifecycle;
    BackupSource _source = BackupSource.LocalWorkstation;
    DatabaseBackupMode _requestedMode = DatabaseBackupMode.Full;
    string? _selectedProtectionSet;

    /// <summary>Creates a database-backup dashboard view model.</summary>
    public DatabaseBackupViewModel(IDatabaseBackupService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _subscription = _service.CreateNotificationSubscription(OnNotificationAsync);
        _lifecycle = new AsyncLifecycleCoordinator(StartCoreAsync, StopCoreAsync);
    }

    /// <summary>Gets the latest immutable dashboard snapshot.</summary>
    public DatabaseBackupDashboardUiModel State { get; private set; } = new(
        BackupSource.LocalWorkstation, [], [], null, null);

    /// <summary>Gets whether an API operation is currently in progress.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>Raised after immutable dashboard state is replaced.</summary>
    public event Action? StateChanged;

    /// <summary>Raised with a safe client or service error summary.</summary>
    public event Action<string>? Error;

    /// <summary>
    /// Raised by the NATS callback as a refresh signal. The view must marshal refresh work to its UI thread.
    /// </summary>
    public event Action<Guid>? RefreshRequested;

    /// <summary>Selects the source used by subsequent queries and commands.</summary>
    public void SelectSource(BackupSource source)
    {
        DatabaseBackupEnumValidation.RequireConcrete(source);
        _source = source;
        _selectedProtectionSet = null;
    }

    /// <summary>Selects the protection set used for targeted restore-point queries.</summary>
    public void SelectProtectionSet(string? protectionSet)
        => _selectedProtectionSet = string.IsNullOrWhiteSpace(protectionSet) ? null : protectionSet;

    /// <summary>Selects the mode used by subsequent backup requests.</summary>
    public void SelectBackupMode(DatabaseBackupMode mode)
    {
        DatabaseBackupEnumValidation.RequireDefined(mode, nameof(mode));
        _requestedMode = mode;
    }

    /// <summary>Refreshes bounded dashboard state through query actors.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        StateChanged?.Invoke();
        try
        {
            var result = await _service.LoadAsync(
                _source, _selectedProtectionSet, cancellationToken).ConfigureAwait(true);
            if (!result.IsSuccess || result.Value is null)
            {
                Error?.Invoke(result.Error?.Message ?? "The dashboard refresh failed.");
                return;
            }
            State = result.Value;
            _selectedProtectionSet ??= State.ProtectionSets.FirstOrDefault(item => item.Enabled)?.Id;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Error?.Invoke(exception.Message);
        }
        finally
        {
            IsBusy = false;
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    /// Submits one coordinated backup command per selected protection set and returns after actor acceptance.
    /// </summary>
    public async Task RequestBackupsAsync(
        IEnumerable<string> protectionSets,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectionSets);
        var selected = protectionSets.Distinct(StringComparer.Ordinal).ToArray();
        if (selected.Length == 0)
        {
            Error?.Invoke("Select at least one protection set.");
            return;
        }

        IsBusy = true;
        StateChanged?.Invoke();
        try
        {
            foreach (var protectionSet in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var policyRevision = State.ProtectionSets
                    .FirstOrDefault(item => item.Id == protectionSet)?.PolicyRevision ?? 0;
                var result = await _service.RequestBackupAsync(
                    _source, protectionSet, policyRevision, _requestedMode, cancellationToken).ConfigureAwait(true);
                if (!result.IsSuccess || result.Value is null)
                {
                    Error?.Invoke(result.Error?.Message ?? "The backup request failed.");
                    continue;
                }
                RefreshRequested?.Invoke(result.Value.OperationId);
            }
        }
        finally
        {
            IsBusy = false;
            StateChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken cancellationToken) => _lifecycle.InitializeAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _lifecycle.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _lifecycle.DisposeAsync();
        await _subscription.DisposeAsync();
    }

    async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        await _subscription.StartAsync(cancellationToken).ConfigureAwait(false);
        await RefreshAsync(cancellationToken).ConfigureAwait(true);
    }

    async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        await _subscription.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    ValueTask OnNotificationAsync(DatabaseBackupNotificationUiModel notification)
    {
        // The NATS callback never mutates bound state. The WinForms view observes this signal,
        // marshals it through Control.BeginInvoke, and initiates a bounded query refresh.
        RefreshRequested?.Invoke(notification.EntityId);
        return ValueTask.CompletedTask;
    }
}
