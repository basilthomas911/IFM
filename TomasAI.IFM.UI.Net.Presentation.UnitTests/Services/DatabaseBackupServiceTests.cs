using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Events.Domain;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;
using TomasAI.IFM.UI.Net.ViewModels.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests.Services;

/// <summary>Verifies database-backup service mapping, commands, and subscriptions.</summary>
public sealed class DatabaseBackupServiceTests
{
    static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Service_maps_bounded_query_results_to_immutable_ui_state()
    {
        var commandApi = Substitute.For<IDatabaseBackupCommandApi>();
        var queryApi = CreateQueryApi();
        var service = new DatabaseBackupService(
            commandApi,
            queryApi,
            Substitute.For<ISystemAdminUIEventConsumer>(),
            new FixedTimeProvider(Now));

        var result = await service.LoadAsync(BackupSource.LocalWorkstation, "core");

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProtectionSets.Should().ContainSingle(item =>
            item.Id == "core" && item.Engines.SequenceEqual(
                new[] { DatabaseEngine.PostgreSql, DatabaseEngine.ScyllaDb }));
        result.Value.RecentOperations.Should().ContainSingle(item =>
            item.ProtectionSet == "core" && item.Phase == DatabaseRecoveryPhase.Started);
        result.Value.LatestVerified!.RestorePointId.Should().Be("verified-1");
        result.Value.LatestRestoreTested!.RestorePointId.Should().Be("tested-1");
    }

    [Fact]
    public async Task View_model_returns_control_while_command_acceptance_is_pending()
    {
        var commandApi = Substitute.For<IDatabaseBackupCommandApi>();
        var pending = new TaskCompletionSource<ServiceResult<DatabaseOperationAcceptedResult>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        commandApi.RequestBackupAsync(Arg.Any<RequestDatabaseBackupCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseOperationAcceptedResult>>(pending.Task));
        var service = new DatabaseBackupService(
            commandApi,
            CreateQueryApi(),
            Substitute.For<ISystemAdminUIEventConsumer>(),
            new FixedTimeProvider(Now));
        var viewModel = new DatabaseBackupViewModel(service);
        await viewModel.RefreshAsync();
        viewModel.SelectBackupMode(DatabaseBackupMode.Incremental);
        Guid refreshOperationId = default;
        viewModel.RefreshRequested += operationId => refreshOperationId = operationId;

        var submission = viewModel.RequestBackupsAsync(["core"]);

        submission.IsCompleted.Should().BeFalse();
        viewModel.IsBusy.Should().BeTrue();
        var acceptedId = Guid.NewGuid();
        pending.SetResult(new ServiceOk<DatabaseOperationAcceptedResult>(new DatabaseOperationAcceptedResult
        {
            OperationId = new DatabaseRecoveryOperationId(acceptedId),
            Source = BackupSource.LocalWorkstation,
            InitialPhase = DatabaseRecoveryPhase.Requested
        }));
        await submission;
        viewModel.IsBusy.Should().BeFalse();
        refreshOperationId.Should().Be(acceptedId);
        await commandApi.Received(1).RequestBackupAsync(
            Arg.Is<RequestDatabaseBackupCommand>(command =>
                command.Request.Origin == DatabaseRequestOrigin.UI &&
                command.RequestedBackupMode == DatabaseBackupMode.Incremental &&
                command.ProtectionSetId == new DatabaseProtectionSetId("core") &&
                command.ExpectedPolicyRevision == 7),
            CancellationToken.None);
    }

    [Fact]
    public async Task Duplicate_or_out_of_order_notifications_only_request_bounded_refresh()
    {
        Func<DatabaseBackupEventContract, ValueTask>? callback = null;
        var eventConsumer = Substitute.For<ISystemAdminUIEventConsumer>();
        eventConsumer.StartDatabaseBackupAsync(
                Arg.Do<Func<DatabaseBackupEventContract, ValueTask>>(value => callback = value),
                Arg.Any<CancellationToken>())
            .Returns(ValueTask.CompletedTask);
        var service = new DatabaseBackupService(
            Substitute.For<IDatabaseBackupCommandApi>(),
            CreateQueryApi(),
            eventConsumer,
            new FixedTimeProvider(Now));
        var viewModel = new DatabaseBackupViewModel(service);
        var refreshes = new List<Guid>();
        viewModel.RefreshRequested += refreshes.Add;
        await viewModel.InitializeAsync(CancellationToken.None);
        var initialState = viewModel.State;
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());

        await callback!(new DatabaseOperationCompletedEvent { EntityId = operationId });
        await callback(new DatabaseOperationStartedEvent { EntityId = operationId });

        viewModel.State.Should().BeSameAs(initialState);
        refreshes.Should().Equal(operationId.Value, operationId.Value);
        await viewModel.DisposeAsync();
    }

    static IDatabaseBackupQueryApi CreateQueryApi()
    {
        var queryApi = Substitute.For<IDatabaseBackupQueryApi>();
        queryApi.GetProtectionSetsAsync(Arg.Any<GetDatabaseProtectionSetsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseProtectionSetReadModel[]>>(
                new ServiceOk<DatabaseProtectionSetReadModel[]>(
                [new DatabaseProtectionSetReadModel
                {
                    ProtectionSetId = new DatabaseProtectionSetId("core"),
                    Source = BackupSource.LocalWorkstation,
                    Engines = [DatabaseEngine.PostgreSql, DatabaseEngine.ScyllaDb],
                    Enabled = true,
                    PolicyRevision = 7
                }])));
        queryApi.ListBackupOperationsAsync(Arg.Any<ListDatabaseBackupOperationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseBackupOperationReadModel[]>>(
                new ServiceOk<DatabaseBackupOperationReadModel[]>(
                [new DatabaseBackupOperationReadModel
                {
                    OperationId = new DatabaseRecoveryOperationId(Guid.NewGuid()),
                    ProtectionSetId = new DatabaseProtectionSetId("core"),
                    Source = BackupSource.LocalWorkstation,
                    Phase = DatabaseRecoveryPhase.Started,
                    CreatedUtc = Now
                }])));
        queryApi.GetLatestVerifiedBackupAsync(
                Arg.Any<GetLatestVerifiedDatabaseBackupQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseRestorePointReadModel>>(
                new ServiceOk<DatabaseRestorePointReadModel>(new DatabaseRestorePointReadModel
                {
                    RestorePointId = new DatabaseRestorePointId("verified-1"),
                    ProtectionSetId = new DatabaseProtectionSetId("core"),
                    Source = BackupSource.LocalWorkstation,
                    RecoveryPointUtc = Now,
                    VerifiedUtc = Now,
                    VerificationLevel = DatabaseVerificationLevel.Native,
                    Eligible = true
                })));
        queryApi.GetLatestRestoreTestedBackupAsync(
                Arg.Any<GetLatestRestoreTestedDatabaseBackupQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseRestorePointReadModel>>(
                new ServiceOk<DatabaseRestorePointReadModel>(new DatabaseRestorePointReadModel
                {
                    RestorePointId = new DatabaseRestorePointId("tested-1"),
                    ProtectionSetId = new DatabaseProtectionSetId("core"),
                    Source = BackupSource.LocalWorkstation,
                    RecoveryPointUtc = Now,
                    VerifiedUtc = Now,
                    RestoreTestedUtc = Now,
                    VerificationLevel = DatabaseVerificationLevel.ApplicationValidation,
                    Eligible = true
                })));
        return queryApi;
    }

    sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
