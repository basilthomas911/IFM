using System.Text.Json;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Application.DatabaseBackup.Console;

/// <summary>
/// Executes parsed console operations exclusively through the public NATS command and query APIs.
/// </summary>
internal sealed class DatabaseBackupConsoleRunner(
    IDatabaseBackupCommandApi commandApi,
    IDatabaseBackupQueryApi queryApi,
    TextWriter output,
    TimeProvider timeProvider)
{
    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    readonly IDatabaseBackupCommandApi _commandApi = commandApi ?? throw new ArgumentNullException(nameof(commandApi));
    readonly IDatabaseBackupQueryApi _queryApi = queryApi ?? throw new ArgumentNullException(nameof(queryApi));
    readonly TextWriter _output = output ?? throw new ArgumentNullException(nameof(output));
    readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));

    internal async ValueTask<int> RunAsync(
        DatabaseBackupConsoleOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        return options.Verb switch
        {
            "status" => await WriteQueryAsync(
                await _queryApi.GetServiceHealthAsync(
                    new GetDatabaseBackupServiceHealthQuery
                    {
                        Request = CreateRequest(options), Source = options.Source
                    },
                    cancellationToken).ConfigureAwait(false)),
            "list-operations" => await WriteQueryAsync(
                await _queryApi.ListBackupOperationsAsync(new ListDatabaseBackupOperationsQuery
                {
                    Request = CreateRequest(options), Source = options.Source, PageSize = options.PageSize
                }, cancellationToken).ConfigureAwait(false)),
            "show-operation" => await ShowOperationAsync(options, cancellationToken).ConfigureAwait(false),
            "list-restore-points" => await WriteQueryAsync(
                await _queryApi.ListRestorePointsAsync(new ListDatabaseRestorePointsQuery
                {
                    Request = CreateRequest(options), Source = options.Source,
                    ProtectionSetId = new DatabaseProtectionSetId(options.Require("protection-set")),
                    PageSize = options.PageSize
                }, cancellationToken).ConfigureAwait(false)),
            "verify" => await WriteQueryAsync(
                await _queryApi.GetLatestVerifiedBackupAsync(new GetLatestVerifiedDatabaseBackupQuery
                {
                    Request = CreateRequest(options), Source = options.Source,
                    ProtectionSetId = new DatabaseProtectionSetId(options.Require("protection-set"))
                }, cancellationToken).ConfigureAwait(false)),
            "reconcile" => await ReconcileAsync(options, cancellationToken).ConfigureAwait(false),
            "follow" => await FollowAsync(options, cancellationToken).ConfigureAwait(false),
            "backup" => await SendAsync(
                _commandApi.RequestBackupAsync(CreateBackupCommand(options), cancellationToken)).ConfigureAwait(false),
            "cancel" => await SendAsync(
                _commandApi.CancelBackupAsync(new CancelDatabaseBackupCommand
                {
                    Request = CreateRequest(options),
                    EntityId = new DatabaseRecoveryOperationId(options.RequireGuid("operation-id")),
                    SafeReason = options.Require("reason")
                }, cancellationToken)).ConfigureAwait(false),
            "restore" => await SendAsync(
                _commandApi.RequestRestoreAsync(CreateRestoreCommand(options), cancellationToken)).ConfigureAwait(false),
            "restore-drill" => await SendAsync(
                _commandApi.RequestRestoreDrillAsync(CreateRestoreDrillCommand(options), cancellationToken)).ConfigureAwait(false),
            "approve-restore" => await SendAsync(
                _commandApi.ApproveRestoreAsync(CreateRestoreApproval(options), cancellationToken)).ConfigureAwait(false),
            "approve-cutover" => await SendAsync(
                _commandApi.ApproveCutoverAsync(CreateCutoverApproval(options), cancellationToken)).ConfigureAwait(false),
            "retention-evaluate" => await SendAsync(
                _commandApi.RequestRetentionEvaluationAsync(new RequestBackupRetentionEvaluationCommand
                {
                    Request = CreateRequest(options), Source = options.Source,
                    EvaluationBoundaryUtc = GetUtcNow()
                }, cancellationToken)).ConfigureAwait(false),
            "retention-execute" => await SendAsync(
                _commandApi.ExecuteRetentionPlanAsync(CreateRetentionExecution(options), cancellationToken)).ConfigureAwait(false),
            _ => throw new ArgumentException(
                $"Unknown database-backup verb '{options.Verb}'.{Environment.NewLine}{DatabaseBackupConsoleOptions.Usage}")
        };
    }

    async ValueTask<int> ShowOperationAsync(
        DatabaseBackupConsoleOptions options,
        CancellationToken cancellationToken)
    {
        var operationId = new DatabaseRecoveryOperationId(options.RequireGuid("operation-id"));
        return await WriteQueryAsync(await _queryApi.GetBackupOperationAsync(
            new GetDatabaseBackupOperationQuery
            {
                Request = CreateRequest(options), Source = options.Source, OperationId = operationId
            }, cancellationToken).ConfigureAwait(false)).ConfigureAwait(false);
    }

    async ValueTask<int> ReconcileAsync(
        DatabaseBackupConsoleOptions options,
        CancellationToken cancellationToken)
    {
        var result = await _queryApi.GetServiceHealthAsync(
            new GetDatabaseBackupServiceHealthQuery { Request = CreateRequest(options), Source = options.Source },
            cancellationToken).ConfigureAwait(false);
        var exitCode = await WriteQueryAsync(result).ConfigureAwait(false);
        if (exitCode != DatabaseBackupConsoleExitCodes.Success)
            return exitCode;
        return result.Value is { Length: > 0 } health && health.All(item => item.Ready)
            ? DatabaseBackupConsoleExitCodes.Success
            : DatabaseBackupConsoleExitCodes.ReconciliationMismatch;
    }

    async ValueTask<int> FollowAsync(
        DatabaseBackupConsoleOptions options,
        CancellationToken cancellationToken)
    {
        var operationId = new DatabaseRecoveryOperationId(options.RequireGuid("operation-id"));
        var interval = TimeSpan.FromMilliseconds(options.GetInt32("interval-ms", 1000, 100, 60_000));
        while (true)
        {
            var result = await _queryApi.GetBackupOperationAsync(new GetDatabaseBackupOperationQuery
            {
                Request = CreateRequest(options), Source = options.Source, OperationId = operationId
            }, cancellationToken).ConfigureAwait(false);
            var exitCode = await WriteQueryAsync(result).ConfigureAwait(false);
            if (exitCode != DatabaseBackupConsoleExitCodes.Success)
                return exitCode;
            if (result.Value is not { } operation)
                return DatabaseBackupConsoleExitCodes.QueryTargetNotFound;
            if (operation.Phase is DatabaseRecoveryPhase.Completed)
                return DatabaseBackupConsoleExitCodes.Success;
            if (operation.Phase is DatabaseRecoveryPhase.Failed or DatabaseRecoveryPhase.Cancelled or DatabaseRecoveryPhase.Rejected)
                return DatabaseBackupConsoleExitCodes.FollowedOperationFailed;
            await Task.Delay(interval, _timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    RequestDatabaseBackupCommand CreateBackupCommand(DatabaseBackupConsoleOptions options)
        => new()
        {
            Request = CreateRequest(options), Source = options.Source,
            ProtectionSetId = new DatabaseProtectionSetId(options.Require("protection-set")),
            RequestedBackupMode = ParseBackupMode(options.GetOptional("mode")),
            ConsistencyMode = ParseConsistency(options.GetOptional("consistency")),
            RequiredDestinations = options.Require("destination")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => new DatabaseLogicalDestination(name, true)).ToArray(),
            ExpectedPolicyRevision = options.GetInt64("policy-revision")
        };

    RequestDatabaseRestoreCommand CreateRestoreCommand(DatabaseBackupConsoleOptions options)
    {
        RequireConfirmation(options);
        return new()
        {
            Request = CreateRequest(options), Source = options.Source,
            ProtectionSetId = new DatabaseProtectionSetId(options.Require("protection-set")),
            RestorePointId = new DatabaseRestorePointId(options.Require("restore-point")),
            FreshTarget = new DatabaseFreshTargetDescriptor(
                options.Require("target-profile"), options.Require("logical-target")),
            RestoreClass = DatabaseRestoreClass.ProductionRecovery,
            ExpectedManifestRevision = options.GetInt64("manifest-revision")
        };
    }

    RequestDatabaseRestoreDrillCommand CreateRestoreDrillCommand(DatabaseBackupConsoleOptions options)
        => new()
        {
            Request = CreateRequest(options), Source = options.Source,
            ProtectionSetId = new DatabaseProtectionSetId(options.Require("protection-set")),
            RestorePointId = new DatabaseRestorePointId(options.Require("restore-point")),
            RestoreClass = DatabaseRestoreClass.Drill,
            DisposableTargetProfile = options.Require("target-profile"),
            ValidationProfile = options.Require("validation-profile"),
            ExpectedManifestRevision = options.GetInt64("manifest-revision")
        };

    ApproveDatabaseRestoreCommand CreateRestoreApproval(DatabaseBackupConsoleOptions options)
    {
        RequireConfirmation(options);
        return new()
        {
            Request = CreateRequest(options),
            EntityId = new DatabaseRecoveryOperationId(options.RequireGuid("operation-id")),
            ApprovalIdentity = options.Require("approval-identity"),
            ApprovalReference = options.Require("approval-reference"),
            ExpectedStateRevision = options.GetInt64("state-revision")
        };
    }

    ApproveDatabaseCutoverCommand CreateCutoverApproval(DatabaseBackupConsoleOptions options)
    {
        RequireConfirmation(options);
        return new()
        {
            Request = CreateRequest(options),
            EntityId = new DatabaseRecoveryOperationId(options.RequireGuid("operation-id")),
            ApprovalIdentity = options.Require("approval-identity"),
            ApprovalReference = options.Require("approval-reference"),
            ValidationRevision = options.RequirePositiveInt64("validation-revision")
        };
    }

    ExecuteBackupRetentionPlanCommand CreateRetentionExecution(DatabaseBackupConsoleOptions options)
    {
        RequireConfirmation(options);
        return new()
        {
            Request = CreateRequest(options), Source = options.Source,
            RetentionPlanId = new DatabaseRetentionPlanId(options.RequireGuid("plan-id")),
            RetentionPlanRevision = options.RequirePositiveInt64("plan-revision"),
            ApprovalReference = options.Require("approval-reference")
        };
    }

    DatabaseRequestEnvelope CreateRequest(DatabaseBackupConsoleOptions options)
    {
        var requestId = Guid.NewGuid();
        return new()
        {
            RequestId = requestId,
            CallerIdentity = options.GetOptional("caller") ?? Environment.UserName,
            AuthorizationReference = options.GetOptional("authorization") ?? "interactive-console",
            CallerRoles = ["DatabaseRecoveryOperator"],
            Origin = DatabaseRequestOrigin.Console,
            CorrelationId = requestId,
            EnvironmentIdentity = options.GetOptional("environment")
                ?? Environment.GetEnvironmentVariable("IFM_ENVIRONMENT")
                ?? "paper-trading",
            CreatedUtc = GetUtcNow()
        };
    }

    async ValueTask<int> SendAsync(
        ValueTask<ServiceResult<DatabaseOperationAcceptedResult>> pendingResult)
    {
        var result = await pendingResult.ConfigureAwait(false);
        if (!result.Success)
        {
            await WriteErrorAsync(result.ErrorCode, result.ErrorMessage).ConfigureAwait(false);
            return DatabaseBackupConsoleExitCodes.CommandRejected;
        }
        await WriteJsonAsync(result.Value).ConfigureAwait(false);
        return DatabaseBackupConsoleExitCodes.Success;
    }

    async ValueTask<int> WriteQueryAsync<T>(ServiceResult<T> result)
    {
        if (!result.Success)
        {
            await WriteErrorAsync(result.ErrorCode, result.ErrorMessage).ConfigureAwait(false);
            return result.ErrorCode is 404 or -404
                ? DatabaseBackupConsoleExitCodes.QueryTargetNotFound
                : DatabaseBackupConsoleExitCodes.CommandRejected;
        }
        await WriteJsonAsync(result.Value).ConfigureAwait(false);
        return DatabaseBackupConsoleExitCodes.Success;
    }

    ValueTask WriteJsonAsync<T>(T value)
        => new(_output.WriteLineAsync(JsonSerializer.Serialize(value, JsonOptions)));

    ValueTask WriteErrorAsync(int errorCode, string errorMessage)
        => new(_output.WriteLineAsync(JsonSerializer.Serialize(new { errorCode, errorMessage }, JsonOptions)));

    DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();

    static void RequireConfirmation(DatabaseBackupConsoleOptions options)
    {
        if (!options.HasFlag("confirm"))
            throw new ArgumentException(
                $"The '{options.Verb}' operation requires explicit --confirm after reviewing its target and approval reference.");
    }

    static DatabaseConsistencyMode ParseConsistency(string? value)
        => (value ?? "coordinated").ToLowerInvariant() switch
        {
            "coordinated" or "protection-set" => DatabaseConsistencyMode.CoordinatedProtectionSet,
            "engine" or "engine-consistent" => DatabaseConsistencyMode.EngineConsistent,
            var unsupported => throw new ArgumentException($"Unsupported consistency mode '{unsupported}'.")
        };

    static DatabaseBackupMode ParseBackupMode(string? value)
        => (value ?? "full").ToLowerInvariant() switch
        {
            "automatic" or "auto" => DatabaseBackupMode.Automatic,
            "full" => DatabaseBackupMode.Full,
            "incremental" => DatabaseBackupMode.Incremental,
            var unsupported => throw new ArgumentException($"Unsupported backup mode '{unsupported}'.")
        };
}
