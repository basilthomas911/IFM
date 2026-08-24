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

            var selected = string.IsNullOrWhiteSpace(selectedProtectionSet)
                ? protectionSetsResult.Value.FirstOrDefault(item => item.Source == source && item.Enabled)
                : protectionSetsResult.Value.FirstOrDefault(item =>
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
                    protectionSetsResult.Value
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
            EnvironmentIdentity = Environment.GetEnvironmentVariable("IFM_ENVIRONMENT") ?? "paper-trading",
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
            value.BackupLineage?.ResolvedMode ?? DatabaseBackupMode.None);

    static DatabaseRestorePointUiModel? Map(DatabaseRestorePointReadModel? value)
        => value is null ? null : new(
            value.RestorePointId.Value,
            value.RecoveryPointUtc,
            value.VerificationLevel,
            value.VerifiedUtc,
            value.RestoreTestedUtc,
            value.Eligible);

    static UiOperationResult<DatabaseBackupDashboardUiModel> Failed(int code, string message)
        => UiOperationResult<DatabaseBackupDashboardUiModel>.Failure(code, message);
}
