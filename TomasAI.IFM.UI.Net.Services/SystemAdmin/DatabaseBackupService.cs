using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Models.SystemAdmin;
using TomasAI.IFM.UI.Net.Services.Operations;
using TomasAI.IFM.UI.Net.Services.Subscriptions;

namespace TomasAI.IFM.UI.Net.Services.SystemAdmin;

/// <summary>Implements the database-backup UI boundary with typed NATS APIs and public events.</summary>
public sealed class DatabaseBackupService(
    IDatabaseBackupCommandApi commandApi,
    IDatabaseBackupQueryApi queryApi,
    ISystemAdminUIEventConsumer eventConsumer,
    TimeProvider? timeProvider = null) : IDatabaseBackupService
{
    readonly IDatabaseBackupCommandApi _commandApi =
        commandApi ?? throw new ArgumentNullException(nameof(commandApi));
    readonly IDatabaseBackupQueryApi _queryApi =
        queryApi ?? throw new ArgumentNullException(nameof(queryApi));
    readonly ISystemAdminUIEventConsumer _eventConsumer =
        eventConsumer ?? throw new ArgumentNullException(nameof(eventConsumer));
    readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupDashboardUiModel>> LoadAsync(
        BackupSource source,
        string? selectedProtectionSet,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DatabaseBackupEnumValidation.RequireConcrete(source);
            var protectionSetsTask = _queryApi.GetProtectionSetsAsync(new GetDatabaseProtectionSetsQuery
            {
                Request = CreateRequest(),
                Source = source,
                PageSize = DatabaseBackupContractLimits.MaximumPageSize
            }, cancellationToken).AsTask();
            var operationsTask = _queryApi.ListBackupOperationsAsync(new ListDatabaseBackupOperationsQuery
            {
                Request = CreateRequest(),
                Source = source,
                PageSize = 50
            }, cancellationToken).AsTask();
            await Task.WhenAll(protectionSetsTask, operationsTask).ConfigureAwait(false);

            var protectionSetsResult = await protectionSetsTask.ConfigureAwait(false);
            if (!protectionSetsResult.Success || protectionSetsResult.Value is null)
                return Failed(protectionSetsResult.ErrorCode, protectionSetsResult.ErrorMessage);
            var operationsResult = await operationsTask.ConfigureAwait(false);
            if (!operationsResult.Success || operationsResult.Value is null)
                return Failed(operationsResult.ErrorCode, operationsResult.ErrorMessage);

            var protectionSets = protectionSetsResult.Value.Where(item => item.Source == source).ToList();
            var setup = await _queryApi.GetBackupSetupAsync(new() { Request = CreateRequest(), Source = source }, cancellationToken).ConfigureAwait(false);
            if (setup is { Success: true, Value.Available: true })
                protectionSets = ConfiguredProtectionSets(source, protectionSets, setup.Value.BackupHostSettings);
            var selected = string.IsNullOrWhiteSpace(selectedProtectionSet)
                ? protectionSets.FirstOrDefault(item => item.Source == source && item.Enabled)
                : protectionSets.FirstOrDefault(item =>
                    item.Source == source && item.ProtectionSetId.Value == selectedProtectionSet);
            var latestVerified = selected is null
                ? null
                : await GetLatestAsync(source, selected.ProtectionSetId, false, cancellationToken)
                    .ConfigureAwait(false);
            var latestRestoreTested = selected is null
                ? null
                : await GetLatestAsync(source, selected.ProtectionSetId, true, cancellationToken)
                    .ConfigureAwait(false);

            return UiOperationResult<DatabaseBackupDashboardUiModel>.Success(
                new DatabaseBackupDashboardUiModel(
                    source,
                    protectionSets
                        .Where(item => item.Source == source)
                        .Select(Map)
                        .ToArray(),
                    operationsResult.Value
                        .Where(item => item.Source == source)
                        .OrderByDescending(item => item.CreatedUtc)
                        .Select(Map)
                        .ToArray(),
                    Map(latestVerified),
                    Map(latestRestoreTested)));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Failed(9200, exception.Message);
        }
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> RequestBackupAsync(
        BackupSource source,
        string protectionSet,
        long expectedPolicyRevision,
        DatabaseBackupMode requestedMode = DatabaseBackupMode.Full,
        CancellationToken cancellationToken = default)
    {
        var result = await _commandApi.RequestBackupAsync(new RequestDatabaseBackupCommand
        {
            Request = CreateRequest(),
            Source = source,
            ProtectionSetId = new DatabaseProtectionSetId(protectionSet),
            ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
            RequiredDestinations = [new DatabaseLogicalDestination("online-vault", true)],
            ExpectedPolicyRevision = expectedPolicyRevision,
            RequestedBackupMode = requestedMode
        }, cancellationToken).ConfigureAwait(false);
        return result.ToUiResult(value => new DatabaseBackupAcceptedUiModel(value.OperationId.Value));
    }

    /// <inheritdoc />
    public IUiEventSubscription CreateNotificationSubscription(
        Func<DatabaseBackupNotificationUiModel, ValueTask> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        return new OwnedUiEventSubscription(
            cancellationToken => _eventConsumer.StartDatabaseBackupAsync(
                value => handler(new DatabaseBackupNotificationUiModel(value.EntityId.Value)),
                cancellationToken),
            _eventConsumer.StopAsync);
    }

    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupSetupUiModel>> LoadSetupAsync(BackupSource source, CancellationToken cancellationToken = default)
    {
        var result = await _queryApi.GetBackupSetupAsync(new() { Request = CreateRequest(), Source = source }, cancellationToken);
        return result.ToUiResult(value => new DatabaseBackupSetupUiModel(value.BackupHostSettings, value.Available));
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupHistoryUiModel>> LoadHistoryAsync(BackupSource source, string continuation, CancellationToken cancellationToken = default)
    {
        var result = await _queryApi.ListBackupOperationsAsync(new() { Request = CreateRequest(), Source = source, ContinuationIdentity = continuation, PageSize = 50 }, cancellationToken);
        if (!result.Success || result.Value is null) return UiOperationResult<DatabaseBackupHistoryUiModel>.Failure(result.ErrorCode, result.ErrorMessage);
        var operations = result.Value.Where(item => item.Source == source).Select(Map).ToArray();
        if (operations.Any(item => item.Engine == DatabaseEngine.None))
        {
            var setup = await _queryApi.GetBackupSetupAsync(new() { Request = CreateRequest(), Source = source }, cancellationToken);
            if (setup?.Value is { Available: true } metadata)
                operations = operations.Select(item => item.Engine == DatabaseEngine.None ? item with { Engine = EngineForProtectionSet(metadata.BackupHostSettings, item.ProtectionSet) } : item).ToArray();
        }
        return UiOperationResult<DatabaseBackupHistoryUiModel>.Success(new(operations, result.Value.Length == 50 ? result.Value[^1].OperationId.Value.ToString() : ""));
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupLogUiModel>> LoadLogAsync(BackupSource source, Guid operationId, long offset, long afterRevision, CancellationToken cancellationToken = default)
    {
        var result = await _queryApi.GetBackupLogAsync(new() { Request = CreateRequest(), Source = source, OperationId = new(operationId), OutputOffset = offset, AfterPhaseRevision = afterRevision, PageSize = 100 }, cancellationToken);
        return result.ToUiResult(page => new DatabaseBackupLogUiModel(page.Phases.Select(item => new DatabaseBackupPhaseUiModel(item.Revision, item.ObservedUtc, item.Phase, item.Outcome, item.ProgressPercent)).ToArray(), page.Output, page.NextOutputOffset, page.EndOfOutput, page.OutputAvailable));
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupPolicyUiModel>> LoadPolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        var result = await _queryApi.GetPolicyAsync(new() { Request = CreateRequest(), PolicyId = new(policyId) }, cancellationToken);
        return result.ToUiResult(policy => new DatabaseBackupPolicyUiModel(policy.PolicyId.Value, policy.Revision, policy.Enforced, policy.Definition.EnabledSources, policy.Definition.ProtectedSets.Select(item => item.Value).ToArray(), policy.Definition.RecoveryObjectives.RecoveryPointObjective, policy.Definition.RecoveryObjectives.RecoveryTimeObjective, policy.Definition.Retention.DailyCount, policy.Definition.Retention.WeeklyCount, policy.Definition.Retention.MonthlyCount, policy.Definition.Verification.Levels, policy.Definition.Verification.MaximumVerificationAge));
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> SavePolicyAsync(DatabaseBackupPolicyUiModel policy, CancellationToken cancellationToken = default)
    {
        var definition = new DatabaseBackupPolicyDefinition(policy.EnabledSources, policy.ProtectionSets.Select(item => new DatabaseProtectionSetId(item)).ToArray(), new(policy.Rpo, policy.Rto), new(policy.Daily, policy.Weekly, policy.Monthly), new(policy.VerificationLevels, policy.MaximumVerificationAge));
        var result = await _commandApi.UpdatePolicyAsync(new() { Request = CreateRequest(), PolicyId = new(policy.Id), Policy = definition, ExpectedPolicyRevision = policy.Revision }, cancellationToken);
        return result.ToUiResult(value => new DatabaseBackupAcceptedUiModel(value.OperationId.Value));
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupHealthUiModel[]>> LoadHealthAsync(BackupSource source, CancellationToken cancellationToken = default)
    {
        var result = await _queryApi.GetServiceHealthAsync(new() { Request = CreateRequest(), Source = source }, cancellationToken);
        return result.ToUiResult(items => items.Select(item => new DatabaseBackupHealthUiModel(item.HostId.Value, item.Ready, item.CapabilityState.ToString(), item.ObservedUtc, item.SafeDiagnosticReference)).ToArray());
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> RequestRestoreDrillAsync(BackupSource source, string protectionSet, string restorePoint, string targetProfile, string validationProfile, long policyRevision, CancellationToken cancellationToken = default)
    {
        var result = await _commandApi.RequestRestoreDrillAsync(new() { Request = CreateRequest(), Source = source, ProtectionSetId = new(protectionSet), RestorePointId = new(restorePoint), DisposableTargetProfile = targetProfile, ValidationProfile = validationProfile, FreshTarget = new(targetProfile, validationProfile), RestoreClass = DatabaseRestoreClass.Drill, ExpectedPolicyRevision = policyRevision }, cancellationToken);
        return result.ToUiResult(value => new DatabaseBackupAcceptedUiModel(value.OperationId.Value));
    }
    /// <inheritdoc />
    public async ValueTask<UiOperationResult<DatabaseBackupAcceptedUiModel>> CancelBackupAsync(DatabaseBackupOperationUiModel operation, CancellationToken cancellationToken = default)
    {
        var result = await _commandApi.CancelBackupAsync(new() { Request = CreateRequest(), Source = operation.Source, ProtectionSetId = new(operation.ProtectionSet), EntityId = new(operation.OperationId), SafeReason = "Operator cancelled from Backup Logs." }, cancellationToken);
        return result.ToUiResult(value => new DatabaseBackupAcceptedUiModel(value.OperationId.Value));
    }

    async ValueTask<DatabaseRestorePointReadModel?> GetLatestAsync(
        BackupSource source,
        DatabaseProtectionSetId protectionSetId,
        bool restoreTested,
        CancellationToken cancellationToken)
    {
        ServiceResult<DatabaseRestorePointReadModel> result = restoreTested
            ? await _queryApi.GetLatestRestoreTestedBackupAsync(new GetLatestRestoreTestedDatabaseBackupQuery
            {
                Request = CreateRequest(),
                Source = source,
                ProtectionSetId = protectionSetId
            }, cancellationToken).ConfigureAwait(false)
            : await _queryApi.GetLatestVerifiedBackupAsync(new GetLatestVerifiedDatabaseBackupQuery
            {
                Request = CreateRequest(),
                Source = source,
                ProtectionSetId = protectionSetId
            }, cancellationToken).ConfigureAwait(false);
        return result.Success ? result.Value : null;
    }

    /// <summary>Exposes host-configured sets before the first backup, preserving persisted policy revisions.</summary>
    static List<DatabaseProtectionSetReadModel> ConfiguredProtectionSets(BackupSource source, List<DatabaseProtectionSetReadModel> history, IReadOnlyDictionary<string, string> settings)
    {
        bool Flag(string name) => bool.TryParse(settings.GetValueOrDefault(name), out var enabled) && enabled;
        var sourceEnabled = Flag("Enabled") && (source != BackupSource.AwsCloud || Flag("AcceptBackupRequests"));
        List<DatabaseProtectionSetReadModel> configured = [];
        foreach (var (key, engine, capability) in new[] { ("PostgreSqlProtectionSets", DatabaseEngine.PostgreSql, "PostgreSqlEnabled"), ("ScyllaProtectionSets", DatabaseEngine.ScyllaDb, "ScyllaEnabled") })
        foreach (var id in settings.GetValueOrDefault(key, "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var persisted = history.FirstOrDefault(item => item.ProtectionSetId.Value == id);
            configured.Add(new() { ProtectionSetId = new(id), Source = source, Engines = [engine], Enabled = sourceEnabled && (source == BackupSource.AwsCloud || Flag(capability)), PolicyRevision = persisted?.PolicyRevision ?? 0 });
        }
        configured.AddRange(history.Where(item => !configured.Any(value => value.ProtectionSetId == item.ProtectionSetId)).Select(item => item with { Enabled = false }));
        return configured;
    }

    DatabaseRequestEnvelope CreateRequest()
    {
        var requestId = Guid.NewGuid();
        return new DatabaseRequestEnvelope
        {
            RequestId = requestId,
            CallerIdentity = Environment.UserName,
            AuthorizationReference = "interactive-ui",
            CallerRoles = ["DatabaseRecoveryOperator"],
            Origin = DatabaseRequestOrigin.UI,
            CorrelationId = requestId,
            EnvironmentIdentity = Environment.GetEnvironmentVariable("IFM_ENVIRONMENT")
                ?? (string.Equals(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase) ? "development" : "paper-trading"),
            CreatedUtc = _timeProvider.GetUtcNow()
        };
    }

    static DatabaseProtectionSetUiModel Map(DatabaseProtectionSetReadModel value)
        => new(
            value.ProtectionSetId.Value,
            value.Source,
            value.Engines,
            value.Enabled,
            value.PolicyRevision);

    static DatabaseBackupOperationUiModel Map(DatabaseBackupOperationReadModel value)
        => new(
            value.OperationId.Value,
            value.ProtectionSetId.Value,
            value.Source,
            value.Phase,
            value.Outcome,
            value.ProgressPercent,
            value.SafeDiagnosticReference,
            value.BackupLineage?.RequestedMode ?? DatabaseBackupMode.Full,
            value.BackupLineage?.ResolvedMode ?? DatabaseBackupMode.None, value.CreatedUtc, value.CompletedUtc, value.BackupSetId?.Value, value.Kind,
            value.BackupLineage?.NativeKind switch
            {
                DatabaseNativeBackupKind.PostgreSqlBase or DatabaseNativeBackupKind.PostgreSqlIncremental => DatabaseEngine.PostgreSql,
                DatabaseNativeBackupKind.ScyllaManagerSnapshot or DatabaseNativeBackupKind.ScyllaManagerDeduplicatedSnapshot => DatabaseEngine.ScyllaDb,
                _ => value.Engine
            });

    static DatabaseRestorePointUiModel? Map(DatabaseRestorePointReadModel? value)
        => value is null ? null : new(
            value.RestorePointId.Value,
            value.RecoveryPointUtc,
            value.VerificationLevel,
            value.VerifiedUtc,
            value.RestoreTestedUtc,
            value.Eligible);

    /// <summary>Uses explicit host protection-set mappings when an operation has no native statistics yet.</summary>
    static DatabaseEngine EngineForProtectionSet(IReadOnlyDictionary<string, string> settings, string protectionSet)
    {
        if (settings.GetValueOrDefault("PostgreSqlProtectionSets", "").Split(',').Contains(protectionSet, StringComparer.Ordinal)) return DatabaseEngine.PostgreSql;
        if (settings.GetValueOrDefault("ScyllaProtectionSets", "").Split(',').Contains(protectionSet, StringComparer.Ordinal)) return DatabaseEngine.ScyllaDb;
        return DatabaseEngine.None;
    }

    static UiOperationResult<DatabaseBackupDashboardUiModel> Failed(int code, string message)
        => UiOperationResult<DatabaseBackupDashboardUiModel>.Failure(code, message);
}
