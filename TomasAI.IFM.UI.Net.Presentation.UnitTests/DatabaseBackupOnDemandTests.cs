using NSubstitute;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.UI.EventConsumer;
using TomasAI.IFM.UI.Net.Services.SystemAdmin;

namespace TomasAI.IFM.UI.Net.Presentation.UnitTests;

public sealed class DatabaseBackupOnDemandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Configured_development_sets_are_available_before_any_backup_has_run(bool scyllaEnabled)
    {
        var queries = Substitute.For<IDatabaseBackupQueryApi>();
        queries.GetProtectionSetsAsync(Arg.Any<GetDatabaseProtectionSetsQuery>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<DatabaseProtectionSetReadModel[]>([]));
        queries.ListBackupOperationsAsync(Arg.Any<ListDatabaseBackupOperationsQuery>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<DatabaseBackupOperationReadModel[]>([]));
        queries.GetBackupSetupAsync(Arg.Any<GetDatabaseBackupSetupQuery>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<DatabaseBackupSetupReadModel>(new()
        {
            Available = true, BackupHostSettings = new()
            {
                ["Enabled"] = "true", ["PostgreSqlEnabled"] = "true", ["ScyllaEnabled"] = scyllaEnabled.ToString(),
                ["PostgreSqlProtectionSets"] = "core-postgresql", ["ScyllaProtectionSets"] = "read-model-scylla"
            }
        }));
        queries.GetLatestVerifiedBackupAsync(Arg.Any<GetLatestVerifiedDatabaseBackupQuery>(), Arg.Any<CancellationToken>()).Returns(new ServiceFailed<DatabaseRestorePointReadModel>(404, "No backup yet"));
        queries.GetLatestRestoreTestedBackupAsync(Arg.Any<GetLatestRestoreTestedDatabaseBackupQuery>(), Arg.Any<CancellationToken>()).Returns(new ServiceFailed<DatabaseRestorePointReadModel>(404, "No backup yet"));
        var service = new DatabaseBackupService(Substitute.For<IDatabaseBackupCommandApi>(), queries, Substitute.For<ISystemAdminUIEventConsumer>());
        var result = await service.LoadAsync(BackupSource.LocalWorkstation, null);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(result.Value!.ProtectionSets.Single(item => item.Id == "core-postgresql").Enabled);
        Assert.Equal(scyllaEnabled, result.Value.ProtectionSets.Single(item => item.Id == "read-model-scylla").Enabled);
        Assert.Empty(result.Value.RecentOperations);
    }

    [Fact]
    public async Task Restore_sends_the_native_fresh_target_without_a_production_cutover_request()
    {
        var commands = Substitute.For<IDatabaseBackupCommandApi>();
        commands.RequestRestoreDrillAsync(Arg.Any<RequestDatabaseRestoreDrillCommand>(), Arg.Any<CancellationToken>()).Returns(new ServiceOk<DatabaseOperationAcceptedResult>(new() { OperationId = new(Guid.NewGuid()) }));
        var service = new DatabaseBackupService(commands, Substitute.For<IDatabaseBackupQueryApi>(), Substitute.For<ISystemAdminUIEventConsumer>());
        var result = await service.RequestRestoreDrillAsync(BackupSource.LocalWorkstation, "core-postgresql", "verified-development-backup", "development-on-demand", "development-restore", 0);
        Assert.True(result.IsSuccess);
        await commands.Received(1).RequestRestoreDrillAsync(Arg.Is<RequestDatabaseRestoreDrillCommand>(command => command.FreshTarget != null && command.FreshTarget.Profile == "development-on-demand" && command.FreshTarget.LogicalTarget == "development-restore" && command.RestoreClass == DatabaseRestoreClass.Drill), Arg.Any<CancellationToken>());
        await commands.DidNotReceive().ApproveCutoverAsync(Arg.Any<ApproveDatabaseCutoverCommand>(), Arg.Any<CancellationToken>());
    }
}
