using FluentAssertions;
using NSubstitute;
using TomasAI.IFM.Application.DatabaseBackup.Console;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Commands;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Contracts;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.Queries;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ReadModels;
using TomasAI.IFM.Domain.SystemAdmin.Shared.DatabaseBackup.ServiceApi;
using TomasAI.IFM.Shared.EventSourcing;

namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests.DatabaseBackup;

public sealed class DatabaseBackupConsoleTests
{
    static readonly DateTimeOffset Now = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Parser_accepts_equals_values_flags_and_source_alias()
    {
        var options = DatabaseBackupConsoleOptions.Parse(
            ["restore", "--source=local", "--operation-id", Guid.NewGuid().ToString(), "--confirm"]);

        options.Verb.Should().Be("restore");
        options.Source.Should().Be(BackupSource.LocalWorkstation);
        options.HasFlag("confirm").Should().BeTrue();
    }

    [Fact]
    public void Parser_rejects_duplicate_or_unstructured_options()
    {
        var duplicate = () => DatabaseBackupConsoleOptions.Parse(
            ["status", "--source", "local", "--source", "aws"]);
        var positional = () => DatabaseBackupConsoleOptions.Parse(["status", "unexpected"]);

        duplicate.Should().Throw<ArgumentException>();
        positional.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Backup_submits_one_public_command_and_prints_operation_identity()
    {
        var commandApi = Substitute.For<IDatabaseBackupCommandApi>();
        var queryApi = Substitute.For<IDatabaseBackupQueryApi>();
        var operationId = new DatabaseRecoveryOperationId(Guid.NewGuid());
        commandApi.RequestBackupAsync(Arg.Any<RequestDatabaseBackupCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseOperationAcceptedResult>>(
                new ServiceOk<DatabaseOperationAcceptedResult>(new DatabaseOperationAcceptedResult
                {
                    OperationId = operationId,
                    Source = BackupSource.LocalWorkstation,
                    InitialPhase = DatabaseRecoveryPhase.Requested
                })));
        using var output = new StringWriter();
        var runner = new DatabaseBackupConsoleRunner(commandApi, queryApi, output, new FixedTimeProvider(Now));
        var options = DatabaseBackupConsoleOptions.Parse(
            ["backup", "--protection-set", "core", "--destination", "online-vault"]);

        var exitCode = await runner.RunAsync(options, CancellationToken.None);

        exitCode.Should().Be(DatabaseBackupConsoleExitCodes.Success);
        output.ToString().Should().Contain(operationId.Value.ToString());
        await commandApi.Received(1).RequestBackupAsync(
            Arg.Is<RequestDatabaseBackupCommand>(command =>
                command.Source == BackupSource.LocalWorkstation &&
                command.ProtectionSetId == new DatabaseProtectionSetId("core") &&
                command.Request.Origin == DatabaseRequestOrigin.Console &&
                command.RequiredDestinations.Single().Name == "online-vault"),
            CancellationToken.None);
    }

    [Fact]
    public async Task Destructive_restore_is_rejected_without_explicit_confirmation()
    {
        var commandApi = Substitute.For<IDatabaseBackupCommandApi>();
        var runner = new DatabaseBackupConsoleRunner(
            commandApi,
            Substitute.For<IDatabaseBackupQueryApi>(),
            new StringWriter(),
            new FixedTimeProvider(Now));
        var options = DatabaseBackupConsoleOptions.Parse(
        [
            "restore", "--protection-set", "core", "--restore-point", "rp-1",
            "--target-profile", "disposable", "--logical-target", "restore-target"
        ]);

        var action = async () => await runner.RunAsync(options, CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*--confirm*");
        await commandApi.DidNotReceiveWithAnyArgs().RequestRestoreAsync(default!, default);
    }

    [Fact]
    public async Task Reconcile_returns_mismatch_when_any_host_is_not_ready()
    {
        var queryApi = Substitute.For<IDatabaseBackupQueryApi>();
        queryApi.GetServiceHealthAsync(Arg.Any<GetDatabaseBackupServiceHealthQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseBackupHealthReadModel[]>>(
                new ServiceOk<DatabaseBackupHealthReadModel[]>(
                [new DatabaseBackupHealthReadModel { Source = BackupSource.LocalWorkstation, Ready = false }])));
        var runner = new DatabaseBackupConsoleRunner(
            Substitute.For<IDatabaseBackupCommandApi>(),
            queryApi,
            new StringWriter(),
            new FixedTimeProvider(Now));

        var exitCode = await runner.RunAsync(
            DatabaseBackupConsoleOptions.Parse(["reconcile"]), CancellationToken.None);

        exitCode.Should().Be(DatabaseBackupConsoleExitCodes.ReconciliationMismatch);
    }

    [Fact]
    public async Task Follow_returns_failure_for_a_terminal_failed_operation()
    {
        var queryApi = Substitute.For<IDatabaseBackupQueryApi>();
        var operationId = Guid.NewGuid();
        queryApi.GetBackupOperationAsync(Arg.Any<GetDatabaseBackupOperationQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<ServiceResult<DatabaseBackupOperationReadModel>>(
                new ServiceOk<DatabaseBackupOperationReadModel>(new DatabaseBackupOperationReadModel
                {
                    OperationId = new DatabaseRecoveryOperationId(operationId),
                    Phase = DatabaseRecoveryPhase.Failed,
                    Outcome = DatabaseRecoveryOutcome.Failed
                })));
        var runner = new DatabaseBackupConsoleRunner(
            Substitute.For<IDatabaseBackupCommandApi>(),
            queryApi,
            new StringWriter(),
            new FixedTimeProvider(Now));

        var exitCode = await runner.RunAsync(DatabaseBackupConsoleOptions.Parse(
            ["follow", "--operation-id", operationId.ToString()]), CancellationToken.None);

        exitCode.Should().Be(DatabaseBackupConsoleExitCodes.FollowedOperationFailed);
    }

    sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
