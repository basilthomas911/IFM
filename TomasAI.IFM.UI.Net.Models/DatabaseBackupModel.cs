using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;

namespace TomasAI.IFM.UI.Net.Models;

/// <summary>Immutable protection-set state displayed by the database-backup UI.</summary>
public sealed record DatabaseProtectionSetUiState(
    string Id,
    BackupSource Source,
    IReadOnlyList<DatabaseEngine> Engines,
    bool Enabled,
    long PolicyRevision);

/// <summary>Immutable operation state displayed by the database-backup UI.</summary>
public sealed record DatabaseBackupOperationUiState(
    Guid OperationId,
    string ProtectionSet,
    BackupSource Source,
    DatabaseRecoveryPhase Phase,
    DatabaseRecoveryOutcome Outcome,
    int ProgressPercent,
    string SafeDiagnosticReference);

/// <summary>Immutable restore-point summary displayed by the database-backup UI.</summary>
public sealed record DatabaseRestorePointUiState(
    string RestorePointId,
    DateTimeOffset RecoveryPointUtc,
    DatabaseVerificationLevel VerificationLevel,
    DateTimeOffset? VerifiedUtc,
    DateTimeOffset? RestoreTestedUtc,
    bool Eligible);

/// <summary>Represents one bounded query refresh of the database-backup dashboard.</summary>
public sealed record DatabaseBackupDashboardState(
    BackupSource Source,
    IReadOnlyList<DatabaseProtectionSetUiState> ProtectionSets,
    IReadOnlyList<DatabaseBackupOperationUiState> RecentOperations,
    DatabaseRestorePointUiState? LatestVerified,
    DatabaseRestorePointUiState? LatestRestoreTested);

/// <summary>Defines the NATS-only model used by database-backup presentation code.</summary>
public interface IDatabaseBackupModel
{
    /// <summary>Loads bounded dashboard state from DatabaseBackup query actors.</summary>
    ValueTask<ServiceResult<DatabaseBackupDashboardState>> LoadAsync(
        BackupSource source,
        string? selectedProtectionSet,
        CancellationToken cancellationToken = default);

    /// <summary>Requests a coordinated backup and returns immediately after actor acceptance.</summary>
    ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestBackupAsync(
        BackupSource source,
        string protectionSet,
        long expectedPolicyRevision,
        CancellationToken cancellationToken = default);

    /// <summary>Starts public DatabaseBackup domain-event observation.</summary>
    ValueTask StartNotificationsAsync(
        Func<DatabaseBackupEventContract, ValueTask> eventAction,
        CancellationToken cancellationToken = default);

    /// <summary>Stops public DatabaseBackup domain-event observation.</summary>
    ValueTask StopNotificationsAsync();
}

/// <summary>
/// Implements the database-backup presentation boundary using typed NATS command/query APIs and public events only.
/// </summary>
public sealed class DatabaseBackupModel(
    IDatabaseBackupCommandApi commandApi,
    IDatabaseBackupQueryApi queryApi,
    ISystemAdminUIEventConsumer eventConsumer,
    TimeProvider? timeProvider = null)
    : BaseModel<DatabaseBackupModel>, IDatabaseBackupModel
{
    readonly IDatabaseBackupCommandApi _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
    readonly IDatabaseBackupQueryApi _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
    readonly ISystemAdminUIEventConsumer _eventConsumer = eventConsumer ?? throw new ArgumentNullException(nameof(eventConsumer));
    readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public async ValueTask<ServiceResult<DatabaseBackupDashboardState>> LoadAsync(
        BackupSource source,
        string? selectedProtectionSet,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DatabaseBackupEnumValidation.RequireConcrete(source);
            var protectionSetsTask = _queryApi.GetProtectionSetsAsync(new GetDatabaseProtectionSetsQuery
            {
                Request = CreateRequest(), Source = source, PageSize = DatabaseBackupContractLimits.MaximumPageSize
            }, cancellationToken).AsTask();
            var operationsTask = _queryApi.ListBackupOperationsAsync(new ListDatabaseBackupOperationsQuery
            {
                Request = CreateRequest(), Source = source, PageSize = 50
            }, cancellationToken).AsTask();
            await Task.WhenAll(protectionSetsTask, operationsTask).ConfigureAwait(false);

            var protectionSetsResult = await protectionSetsTask.ConfigureAwait(false);
            if (!protectionSetsResult.Success || protectionSetsResult.Value is null)
                return Failed(protectionSetsResult.ErrorCode, protectionSetsResult.ErrorMessage);
            var operationsResult = await operationsTask.ConfigureAwait(false);
            if (!operationsResult.Success || operationsResult.Value is null)
                return Failed(operationsResult.ErrorCode, operationsResult.ErrorMessage);

            var selected = string.IsNullOrWhiteSpace(selectedProtectionSet)
                ? protectionSetsResult.Value.FirstOrDefault(item => item.Source == source && item.Enabled)
                : protectionSetsResult.Value.FirstOrDefault(item =>
                    item.Source == source && item.ProtectionSetId.Value == selectedProtectionSet);
            var latestVerified = selected is null
                ? null
                : await GetLatestAsync(source, selected.ProtectionSetId, restoreTested: false, cancellationToken)
                    .ConfigureAwait(false);
            var latestRestoreTested = selected is null
                ? null
                : await GetLatestAsync(source, selected.ProtectionSetId, restoreTested: true, cancellationToken)
                    .ConfigureAwait(false);

            return new ServiceOk<DatabaseBackupDashboardState>(new DatabaseBackupDashboardState(
                source,
                protectionSetsResult.Value
                    .Where(item => item.Source == source)
                    .Select(item => new DatabaseProtectionSetUiState(
                        item.ProtectionSetId.Value,
                        item.Source,
                        item.Engines,
                        item.Enabled,
                        item.PolicyRevision))
                    .ToArray(),
                operationsResult.Value
                    .Where(item => item.Source == source)
                    .OrderByDescending(item => item.CreatedUtc)
                    .Select(item => new DatabaseBackupOperationUiState(
                        item.OperationId.Value,
                        item.ProtectionSetId.Value,
                        item.Source,
                        item.Phase,
                        item.Outcome,
                        item.ProgressPercent,
                        item.SafeDiagnosticReference))
                    .ToArray(),
                Map(latestVerified),
                Map(latestRestoreTested)));
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return Failed(9200, exception.Message);
        }
    }

    /// <inheritdoc />
    public ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> RequestBackupAsync(
        BackupSource source,
        string protectionSet,
        long expectedPolicyRevision,
        CancellationToken cancellationToken = default)
        => _commandApi.RequestBackupAsync(new RequestDatabaseBackupCommand
        {
            Request = CreateRequest(),
            Source = source,
            ProtectionSetId = new DatabaseProtectionSetId(protectionSet),
            ConsistencyMode = DatabaseConsistencyMode.CoordinatedProtectionSet,
            RequiredDestinations = [new DatabaseLogicalDestination("online-vault", true)],
            ExpectedPolicyRevision = expectedPolicyRevision
        }, cancellationToken);

    /// <inheritdoc />
    public ValueTask StartNotificationsAsync(
        Func<DatabaseBackupEventContract, ValueTask> eventAction,
        CancellationToken cancellationToken = default)
        => _eventConsumer.StartDatabaseBackupAsync(eventAction, cancellationToken);

    /// <inheritdoc />
    public ValueTask StopNotificationsAsync() => _eventConsumer.StopAsync();

    async ValueTask<DatabaseRestorePointReadModel?> GetLatestAsync(
        BackupSource source,
        DatabaseProtectionSetId protectionSetId,
        bool restoreTested,
        CancellationToken cancellationToken)
    {
        ServiceResult<DatabaseRestorePointReadModel> result = restoreTested
            ? await _queryApi.GetLatestRestoreTestedBackupAsync(new GetLatestRestoreTestedDatabaseBackupQuery
            {
                Request = CreateRequest(), Source = source, ProtectionSetId = protectionSetId
            }, cancellationToken).ConfigureAwait(false)
            : await _queryApi.GetLatestVerifiedBackupAsync(new GetLatestVerifiedDatabaseBackupQuery
            {
                Request = CreateRequest(), Source = source, ProtectionSetId = protectionSetId
            }, cancellationToken).ConfigureAwait(false);
        return result.Success ? result.Value : null;
    }

    DatabaseRequestEnvelope CreateRequest()
    {
        var requestId = Guid.NewGuid();
        return new()
        {
            RequestId = requestId,
            CallerIdentity = Environment.UserName,
            AuthorizationReference = "interactive-ui",
            CallerRoles = ["DatabaseRecoveryOperator"],
            Origin = DatabaseRequestOrigin.UI,
            CorrelationId = requestId,
            EnvironmentIdentity = Environment.GetEnvironmentVariable("IFM_ENVIRONMENT") ?? "paper-trading",
            CreatedUtc = _timeProvider.GetUtcNow()
        };
    }

    static DatabaseRestorePointUiState? Map(DatabaseRestorePointReadModel? value)
        => value is null ? null : new(
            value.RestorePointId.Value,
            value.RecoveryPointUtc,
            value.VerificationLevel,
            value.VerifiedUtc,
            value.RestoreTestedUtc,
            value.Eligible);

    static ServiceFailed<DatabaseBackupDashboardState> Failed(int errorCode, string errorMessage)
        => new(errorCode, errorMessage);
}
