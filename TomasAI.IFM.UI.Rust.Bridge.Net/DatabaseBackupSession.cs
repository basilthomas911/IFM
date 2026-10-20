using System.Globalization;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
namespace TomasAI.IFM.UI.Rust.Bridge.Net;

/// <summary>Adapts the original backup service, owning its notification lifetime and bounded output pages.</summary>
public sealed class DatabaseBackupSession : IDisposable
{
    readonly RustClientServiceScope? scope;
    readonly IDatabaseBackupService service;
    readonly IUiEventSubscription subscription;
    readonly Task ready;
    readonly Dictionary<BackupSource, Dictionary<Guid, DatabaseBackupOperationUiModel>> history = [];
    readonly Dictionary<BackupSource, string> cursors = [];
    readonly Dictionary<BackupSource, DatabaseBackupPolicyUiModel> policies = [];
    BackupSource source = BackupSource.LocalWorkstation;
    DatabaseBackupDashboardUiModel dashboard = new(BackupSource.LocalWorkstation, [], [], null, null);
    DatabaseBackupSetupUiModel? setup;
    DatabaseBackupHealthUiModel[] health = [];
    Guid? selectedOperation;
    BackupSource selectedOperationSource;
    string output = "", status = "";
    long offset, phaseRevision;
    bool initialized;
    int refreshPending;
    /// <summary>Gets whether an actor notification requires a bounded read refresh.</summary>
    public bool RefreshPending => Volatile.Read(ref refreshPending) != 0;
    /// <summary>Creates the live service owner using the configured NATS endpoint.</summary>
    public DatabaseBackupSession()
    {
        scope = new(Environment.GetEnvironmentVariable("IFM_UI_NATS_URL") ?? "nats://localhost:4222");
        service = scope.Container.GetInstance<IDatabaseBackupService>();
        subscription = service.CreateNotificationSubscription(_ => { Interlocked.Exchange(ref refreshPending, 1); return ValueTask.CompletedTask; });
        ready = scope.StartAsync().AsTask();
    }
    /// <summary>Uses a supplied service for isolated workflow tests.</summary>
    public DatabaseBackupSession(IDatabaseBackupService service)
    {
        this.service = service;
        subscription = service.CreateNotificationSubscription(_ => { Interlocked.Exchange(ref refreshPending, 1); return ValueTask.CompletedTask; });
        ready = Task.CompletedTask;
    }
    /// <summary>Validates the closed backup intent contract before admission to the shared editor queue.</summary>
    public static bool Validate(ContractEditorIntent i) => i.Schema == 1 && i.RequestId != Guid.Empty && i.View == "database-backup"
        && i.Intent is "load" or "backup-close" or "backup-clear-output" or "backup-select" or "backup-source" or "backup-refresh" or "backup-older" or "backup-output" or "backup-more-output" or "backup-run" or "backup-cancel" or "backup-policy-load" or "backup-policy-save" or "backup-restore"
        && i.Fields is not null && i.Fields.Count <= 64 && i.Fields.All(x => x.Key.Length <= 64 && x.Value is not null && x.Value.Length <= 2048);
    static string Field(ContractEditorIntent i, string key) => i.Fields.GetValueOrDefault(key)?.Trim() ?? "";
    static T Require<T>(UiOperationResult<T> result) => result.IsSuccess && result.Value is not null ? result.Value : throw new InvalidOperationException(result.Error?.Message ?? "Backup service returned no result.");
    /// <summary>Dispatches the original backup, policy, restore, history and cancellation actions through the service.</summary>
    public async Task<ContractEditorResult> ExecuteAsync(ContractEditorIntent input)
    {
        await ready;
        if (!initialized) { await subscription.StartAsync(); initialized = true; }
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = deadline.Token;
        if (!Enum.TryParse<BackupSource>(Field(input, "source"), out var requested) || requested is not (BackupSource.AwsCloud or BackupSource.LocalWorkstation)) throw new ArgumentException("Select a concrete backup source.");
        source = requested;
        switch (input.Intent)
        {
            case "load": case "backup-source": case "backup-refresh": await RefreshAsync(token); break;
            case "backup-clear-output": selectedOperation=null;output="";offset=0;phaseRevision=0;status="No identified operation for this database in the selected run.";break;
            case "backup-select": dashboard=Require(await service.LoadAsync(source,Field(input,"protectionSet"),token));break;
            case "backup-older":
                if (!Enum.TryParse<BackupSource>(Field(input,"historySource"), out var historySource) || historySource is not (BackupSource.AwsCloud or BackupSource.LocalWorkstation)) throw new ArgumentException("Select a history source.");
                await ReadHistoryAsync(historySource, cursors.GetValueOrDefault(historySource, ""), token); break;
            case "backup-output": case "backup-more-output":
                var operation = FindOperation(input);
                if (input.Intent == "backup-output") { selectedOperation = operation.OperationId; selectedOperationSource = operation.Source; offset = 0; phaseRevision = 0; output = ""; }
                else if (selectedOperation != operation.OperationId) throw new InvalidOperationException("Select the output operation again.");
                await ReadOutputAsync(token); break;
            case "backup-run":
                var sets = Field(input, "protectionSets").Split('|', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
                if (sets.Length == 0) throw new ArgumentException("Select at least one protection set.");
                if (!Enum.TryParse<DatabaseBackupMode>(Field(input, "mode"), out var mode) || mode is not (DatabaseBackupMode.Full or DatabaseBackupMode.Automatic or DatabaseBackupMode.Incremental)) throw new ArgumentException("Select a backup mode.");
                foreach (var id in sets) { var set = dashboard.ProtectionSets.Single(x => x.Source == source && x.Id == id && x.Enabled); Require(await service.RequestBackupAsync(source, id, set.PolicyRevision, mode, token)); }
                status = "Backup accepted; awaiting completion."; await RefreshAsync(token); break;
            case "backup-cancel":
                var cancel = FindOperation(input);
                if (cancel.Kind != DatabaseRecoveryOperationKind.Backup || cancel.Outcome != DatabaseRecoveryOutcome.None) throw new InvalidOperationException("Select a running backup.");
                Require(await service.CancelBackupAsync(cancel, token)); status = "Cancellation accepted; awaiting terminal status."; await RefreshAsync(token); break;
            case "backup-policy-load": policies[source] = Require(await service.LoadPolicyAsync(Field(input, "policyId"), token)); status = "Accepted policy loaded."; break;
            case "backup-policy-save":
                var policy = policies.GetValueOrDefault(source) ?? throw new InvalidOperationException("Load the policy before saving.");
                if (policy.Id != Field(input, "policyId")) throw new InvalidOperationException("Reload the selected policy before saving.");
                int Count(string key) { var v = int.Parse(Field(input, key), CultureInfo.InvariantCulture); return v >= 0 ? v : throw new ArgumentException("Retention cannot be negative."); }
                TimeSpan Duration(string key, bool hours = false) { var v = double.Parse(Field(input, key), CultureInfo.InvariantCulture); if (!double.IsFinite(v) || v <= 0) throw new ArgumentException("Recovery durations must be positive."); return hours ? TimeSpan.FromHours(v) : TimeSpan.FromMinutes(v); }
                Require(await service.SavePolicyAsync(policy with { Daily = Count("daily"), Weekly = Count("weekly"), Monthly = Count("monthly"), Rpo = Duration("rpoMinutes"), Rto = Duration("rtoMinutes"), MaximumVerificationAge = Duration("maximumVerificationHours", true) }, token));
                status = "Policy update accepted; reload to confirm applied revision."; break;
            case "backup-restore":
                var selected = dashboard.ProtectionSets.Single(x => x.Id == Field(input, "protectionSet") && x.Source == source);
                Require(await service.RequestRestoreDrillAsync(source, selected.Id, Field(input, "restorePoint"), Field(input, "targetProfile"), Field(input, "validationProfile"), selected.PolicyRevision, token));
                status = "Restore accepted; awaiting validation."; await RefreshAsync(token); break;
            default: throw new ArgumentException("Unsupported backup action.");
        }
        return Publication();
    }
    DatabaseBackupOperationUiModel FindOperation(ContractEditorIntent i)
    {
        var candidates = Enum.TryParse<BackupSource>(Field(i,"operationSource"),out var operationSource)
            ? history.GetValueOrDefault(operationSource)?.Values.AsEnumerable() ?? [] : history.Values.SelectMany(x=>x.Values);
        return candidates.Single(x=>x.OperationId.ToString("D")==Field(i,"operationId"));
    }
    async Task ReadHistoryAsync(BackupSource target, string cursor, CancellationToken token)
    {
        var result = await service.LoadHistoryAsync(target, cursor, token);
        if(!result.IsSuccess || result.Value is null){status=result.Error?.Message??"Backup history is unavailable.";return;}
        var page=result.Value;
        if (!history.TryGetValue(target, out var rows)) history[target] = rows = [];
        foreach (var item in page.Operations) rows[item.OperationId] = item;
        if (cursor.Length > 0 || !cursors.ContainsKey(target)) cursors[target] = page.Continuation;
    }
    async Task ReadOutputAsync(CancellationToken token)
    {
        var log = Require(await service.LoadLogAsync(selectedOperationSource, selectedOperation!.Value, offset, phaseRevision, token));
        output += string.Join(Environment.NewLine, log.Phases.Select(x => $"{x.ObservedUtc:O} {x.Phase} {x.ProgressPercent}% {x.Outcome}")) + Environment.NewLine + log.Output;
        if (output.Length > 2_000_000) output = output[^2_000_000..];
        offset = log.NextOffset; phaseRevision = Math.Max(phaseRevision, log.Phases.Select(x => x.Revision).DefaultIfEmpty().Max());
    }
    async Task RefreshAsync(CancellationToken token)
    {
        dashboard = Require(await service.LoadAsync(source, null, token));
        var setupResult=await service.LoadSetupAsync(source,token);
        setup=setupResult.IsSuccess&&setupResult.Value is not null ? setupResult.Value
            : new DatabaseBackupSetupUiModel(new Dictionary<string,string>{{"Status",setupResult.Error?.Message??"Host setup metadata unavailable."}},false);
        var readiness=await service.LoadHealthAsync(source,token);
        health=readiness.IsSuccess ? readiness.Value??[] : [];
        if(!readiness.IsSuccess)status=readiness.Error?.Message??"Host readiness is unavailable.";
        foreach (var target in new[] { BackupSource.AwsCloud, BackupSource.LocalWorkstation }) await ReadHistoryAsync(target, "", token);
        if (selectedOperation is not null) await ReadOutputAsync(token);
    }
    /// <summary>Refreshes read state after a notification without submitting backup commands.</summary>
    public async Task<ContractEditorResult> RefreshPublicationAsync()
    { Interlocked.Exchange(ref refreshPending, 0); using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await RefreshAsync(timeout.Token); return Publication(); }
    ContractEditorResult Publication() => new("database-backup", [], [], false,
        new() { ["source"] = source.ToString(), ["status"] = status, ["output"] = output, ["outputOperation"] = selectedOperation?.ToString("D") ?? "" })
        { Documents = new() { ["dashboard"] = BridgeDocument.Encode(dashboard, true, true), ["history"] = BridgeDocument.Encode(history.ToDictionary(x => x.Key.ToString(), x => x.Value.Values.OrderByDescending(y => y.CreatedUtc).ToArray()), true, true), ["continuations"] = BridgeDocument.Encode(cursors.ToDictionary(x => x.Key.ToString(), x => x.Value), true), ["setup"] = BridgeDocument.Encode(setup, true), ["health"] = BridgeDocument.Encode(health, true), ["policyEditor"] = BridgeDocument.Encode(policies.TryGetValue(source, out var policy) ? new Dictionary<string,string> { ["rpoMinutes"]=policy.Rpo.TotalMinutes.ToString(CultureInfo.InvariantCulture),["rtoMinutes"]=policy.Rto.TotalMinutes.ToString(CultureInfo.InvariantCulture),["maximumVerificationHours"]=policy.MaximumVerificationAge.TotalHours.ToString(CultureInfo.InvariantCulture) } : new(), true), ["policy"] = BridgeDocument.Encode(policies.GetValueOrDefault(source), true, true) } };
    /// <summary>Stops the independently owned notification subscription and releases live services.</summary>
    public void Dispose() { subscription.DisposeAsync().AsTask().GetAwaiter().GetResult(); scope?.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
}
