using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.ServerManager.Contracts;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;
using TomasAI.IFM.Application.ServerManager.TestProcess;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SchedulerHostPostgresCollection : ICollectionFixture<SchedulerHostPostgresFixture>
{
    public const string Name = "Server Manager Scheduler Host PostgreSQL";
}

[Collection(SchedulerHostPostgresCollection.Name)]
public sealed class SchedulerHostPersistenceTests(SchedulerHostPostgresFixture fixture)
{
    [Fact]
    public async Task Migrations_are_idempotent_and_create_an_empty_greenfield_scheduler_store()
    {
        var services = await CreateServicesAsync();

        await services.Migrator.MigrateAsync(CancellationToken.None);
        await services.Migrator.MigrateAsync(CancellationToken.None);
        await services.Catalog.SynchronizeSnapshotAsync(CancellationToken.None);

        (await services.Store.GetTaskCatalogAsync(CancellationToken.None)).Should().ContainSingle()
            .Which.TaskKey.Should().Be("helper");
        (await services.Store.GetSchedulesAsync(CancellationToken.None)).Should().BeEmpty();
        (await services.Store.GetRecentRunsAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Schedule_mutations_are_idempotent_audited_and_optimistically_concurrent()
    {
        var services = await CreateServicesAsync();
        await services.Migrator.MigrateAsync(CancellationToken.None);
        await services.Catalog.SynchronizeSnapshotAsync(CancellationToken.None);
        var validator = new ScheduleValidationService(services.Options, services.Catalog);
        var input = new ScheduleDefinitionInputDto(
            null,
            $"schedule-{Guid.NewGuid():N}",
            "integration",
            "helper",
            ScheduleKind.SimpleInterval,
            "300",
            "UTC",
            SchedulerMisfirePolicy.DoNothing,
            20);
        var validation = validator.Validate(input);
        validation.IsValid.Should().BeTrue();
        var requestId = Guid.NewGuid();

        var created = await services.Store.CreateScheduleAsync(
            requestId,
            "integration-test",
            input,
            validation.Explanation,
            "1",
            CancellationToken.None);
        var replay = await services.Store.CreateScheduleAsync(
            requestId,
            "integration-test",
            input,
            validation.Explanation,
            "1",
            CancellationToken.None);

        replay.Replayed.Should().BeTrue();
        replay.EntityId.Should().Be(created.EntityId);
        (await services.Store.GetSchedulesAsync(CancellationToken.None)).Should().ContainSingle();
        var updatedInput = input with
        {
            ScheduleDefinitionId = created.EntityId,
            Description = "updated"
        };
        var stale = async () => await services.Store.UpdateScheduleAsync(
            Guid.NewGuid(),
            "integration-test",
            99,
            updatedInput,
            validation.Explanation,
            "1",
            CancellationToken.None);
        await stale.Should().ThrowAsync<SchedulerConflictException>();

        var updated = await services.Store.UpdateScheduleAsync(
            Guid.NewGuid(),
            "integration-test",
            1,
            updatedInput,
            validation.Explanation,
            "1",
            CancellationToken.None);
        updated.Version.Should().Be(2);
        var deleted = await services.Store.DeleteScheduleAsync(
            Guid.NewGuid(),
            "integration-test",
            2,
            created.EntityId!.Value,
            "test cleanup",
            CancellationToken.None);
        deleted.Version.Should().Be(3);
        (await services.Store.GetSchedulesAsync(CancellationToken.None)).Should().BeEmpty();

        await using var connection = await services.DataSource.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM ifm_scheduler.audit_entry;";
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Recovery_marks_incomplete_run_abandoned_without_retrying_it()
    {
        var services = await CreateServicesAsync();
        await services.Migrator.MigrateAsync(CancellationToken.None);
        await services.Catalog.SynchronizeSnapshotAsync(CancellationToken.None);
        var run = NewRun();
        (await services.Store.TryCreateRunAsync(run, CancellationToken.None)).Should().BeTrue();
        await services.Store.TransitionRunAsync(
            run.RunId,
            ScheduledRunState.Starting,
            null,
            null,
            null,
            null,
            CancellationToken.None);
        await services.Store.TransitionRunAsync(
            run.RunId,
            ScheduledRunState.Running,
            null,
            1234,
            DateTimeOffset.UtcNow,
            null,
            CancellationToken.None);

        (await services.Store.RecoverIncompleteRunsAsync(CancellationToken.None)).Should().Be(1);

        var recovered = (await services.Store.GetRecentRunsAsync(CancellationToken.None))
            .Single(value => value.RunId == run.RunId);
        recovered.State.Should().Be(ScheduledRunState.Abandoned);
        recovered.Detail.Should().Contain("restarted");
    }

    [Fact]
    public async Task Retention_selects_expired_terminal_output_but_preserves_active_and_abandoned_evidence()
    {
        var services = await CreateServicesAsync();
        await services.Migrator.MigrateAsync(CancellationToken.None);
        await services.Catalog.SynchronizeSnapshotAsync(CancellationToken.None);
        var succeeded = NewRun();
        var active = NewRun();
        var abandoned = NewRun();
        await services.Store.RecordTerminalRunAsync(succeeded, ScheduledRunState.Succeeded, "done", CancellationToken.None);
        (await services.Store.TryCreateRunAsync(active, CancellationToken.None)).Should().BeTrue();
        (await services.Store.TryCreateRunAsync(abandoned, CancellationToken.None)).Should().BeTrue();
        await services.Store.TransitionRunAsync(
            abandoned.RunId,
            ScheduledRunState.Abandoned,
            "ambiguous",
            null,
            null,
            null,
            CancellationToken.None);
        await using (var connection = await services.DataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE ifm_scheduler.task_run
                SET finished_at_utc = now() - interval '400 days'
                WHERE run_id = ANY($1);
                """;
            command.Parameters.AddWithValue(new[] { succeeded.RunId, abandoned.RunId });
            await command.ExecuteNonQueryAsync();
        }

        var candidates = await services.Store.GetRetentionCandidatesAsync(CancellationToken.None);

        candidates.Should().ContainSingle(value => value.RunId == succeeded.RunId);
        candidates.Should().NotContain(value => value.RunId == active.RunId || value.RunId == abandoned.RunId);
    }

    [Fact]
    public async Task Database_uniqueness_blocks_overlap_and_skipped_occurrence_is_recorded()
    {
        var services = await CreateServicesAsync();
        await services.Migrator.MigrateAsync(CancellationToken.None);
        await services.Catalog.SynchronizeSnapshotAsync(CancellationToken.None);
        var scheduleId = Guid.NewGuid();
        await using (var connection = await services.DataSource.OpenConnectionAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO ifm_scheduler.schedule_definition
                (schedule_definition_id, name, description, task_key, catalog_manifest_version, enabled,
                 schedule_kind, schedule_expression, schedule_explanation, time_zone_id, misfire_policy,
                 created_by, created_at_utc, updated_by, updated_at_utc)
                VALUES ($1, $2, '', 'helper', '1', false, 'Cron', '0 0 0 * * ?', 'daily', 'UTC',
                        'DoNothing', 'test', now(), 'test', now());
                """;
            command.Parameters.AddWithValue(scheduleId);
            command.Parameters.AddWithValue($"schedule-{scheduleId:N}");
            await command.ExecuteNonQueryAsync();
        }

        var first = NewRun() with { ScheduleDefinitionId = scheduleId };
        var skipped = NewRun() with { ScheduleDefinitionId = scheduleId };
        (await services.Store.TryCreateRunAsync(first, CancellationToken.None)).Should().BeTrue();
        (await services.Store.TryCreateRunAsync(skipped, CancellationToken.None)).Should().BeFalse();
        await services.Store.RecordTerminalRunAsync(
            skipped,
            ScheduledRunState.SkippedOverlap,
            "overlap",
            CancellationToken.None);

        var runs = await services.Store.GetRecentRunsAsync(CancellationToken.None);
        runs.Should().Contain(value => value.RunId == first.RunId && value.State == ScheduledRunState.Planned);
        runs.Should().Contain(value => value.RunId == skipped.RunId && value.State == ScheduledRunState.SkippedOverlap);
    }

    [Fact]
    public async Task Named_pipe_returns_health_catalog_and_empty_schedule_dashboard()
    {
        var services = await CreateServicesAsync();
        await services.Migrator.MigrateAsync(CancellationToken.None);
        await services.Catalog.SynchronizeSnapshotAsync(CancellationToken.None);
        services.Bootstrap.Succeeded = true;
        services.Health.Set(SchedulerServiceState.Ready, true, true, true, "ready");
        var query = new SchedulerDashboardQueryService(
            services.Health,
            services.Bootstrap,
            services.Store,
            NullLogger<SchedulerDashboardQueryService>.Instance);
        var pipeName = $"IFM.SM-S2.Tests.{Guid.NewGuid():N}";
        services.Options.PipeName = pipeName;
        var server = new SchedulerPipeServer(
            services.Options,
            query,
            NullLogger<SchedulerPipeServer>.Instance);
        await server.StartAsync(CancellationToken.None);
        try
        {
            var client = new SchedulerPipeClient(new SchedulerClientOptions
            {
                PipeName = pipeName,
                ConnectTimeoutMilliseconds = 5_000
            });

            var dashboard = await client.GetDashboardAsync(CancellationToken.None);

            dashboard.Health.State.Should().Be(SchedulerServiceState.Ready);
            dashboard.TaskCatalog.Should().Contain(value => value.TaskKey == "helper");
            dashboard.Schedules.Should().BeEmpty();
        }
        finally
        {
            await server.StopAsync(CancellationToken.None);
            server.Dispose();
        }
    }

    [Fact]
    public async Task Production_host_bootstrap_starts_persistent_quartz_and_serves_ready_health()
    {
        var connectionString = await fixture.CreateDatabaseConnectionStringAsync();
        var helper = typeof(TestProcessMarker).Assembly.Location;
        var root = Path.GetDirectoryName(helper)!;
        var pipeName = $"IFM.SM-S2.Host.{Guid.NewGuid():N}";
        var runRoot = Path.Combine(Path.GetTempPath(), "ifm-sm-s2-host", Guid.NewGuid().ToString("N"));
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:SchedulerDbConnection"] = connectionString,
            ["SchedulerHost:Environment"] = "Development",
            ["SchedulerHost:SchedulerName"] = $"IFM-SM-S2-{Guid.NewGuid():N}",
            ["SchedulerHost:PipeName"] = pipeName,
            ["SchedulerHost:TaskRunRoot"] = runRoot,
            ["SchedulerHost:DeploymentRoot"] = root,
            ["SchedulerHost:MaximumConcurrentProcesses"] = "1",
            ["SchedulerHost:ShutdownTimeoutSeconds"] = "5",
            ["SchedulerHost:RecentRunLimit"] = "20",
            ["SchedulerHost:SeedInitialSchedules"] = "false",
            ["SchedulerHost:UseOperatorGroupPipeAcl"] = "false",
            ["SchedulerHost:TaskCatalog:0:TaskKey"] = "helper",
            ["SchedulerHost:TaskCatalog:0:DisplayName"] = "Helper",
            ["SchedulerHost:TaskCatalog:0:Description"] = "Host integration helper",
            ["SchedulerHost:TaskCatalog:0:WorkingDirectory"] = ".",
            ["SchedulerHost:TaskCatalog:0:ExecutablePath"] = Path.GetFileName(helper),
            ["SchedulerHost:TaskCatalog:0:RequiredEnvironment"] = "Development",
            ["SchedulerHost:TaskCatalog:0:MaximumRuntimeSeconds"] = "30",
            ["SchedulerHost:TaskCatalog:0:ManifestVersion"] = "1",
            ["SchedulerHost:TaskCatalog:0:SuccessExitCodes:0"] = "0"
        };
        using var host = SchedulerHostApplication.Create(
            [],
            configuration => configuration.AddInMemoryCollection(values));

        await host.StartAsync();
        try
        {
            var client = new SchedulerPipeClient(new SchedulerClientOptions
            {
                PipeName = pipeName,
                ConnectTimeoutMilliseconds = 10_000
            });

            var dashboard = await client.GetDashboardAsync(CancellationToken.None);

            dashboard.Health.State.Should().Be(SchedulerServiceState.Ready);
            dashboard.Health.DatabaseAvailable.Should().BeTrue();
            dashboard.Health.QuartzAvailable.Should().BeTrue();
            dashboard.Health.SchedulingStarted.Should().BeTrue();
            dashboard.Schedules.Should().BeEmpty();

            var input = new ScheduleDefinitionInputDto(
                null,
                $"pipe-schedule-{Guid.NewGuid():N}",
                "pipe integration",
                "helper",
                ScheduleKind.SimpleInterval,
                "300",
                "UTC",
                SchedulerMisfirePolicy.DoNothing,
                20);
            var validation = await client.ValidateScheduleAsync(input, CancellationToken.None);
            validation.IsValid.Should().BeTrue();
            var created = await client.CreateScheduleAsync(input, CancellationToken.None);
            created.Version.Should().Be(1);
            (await client.GetDashboardAsync(CancellationToken.None)).Schedules.Should().ContainSingle(value =>
                value.ScheduleDefinitionId == created.EntityId && !value.Enabled);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    private async Task<SchedulerTestServices> CreateServicesAsync()
    {
        var helper = typeof(TestProcessMarker).Assembly.Location;
        var root = Path.GetDirectoryName(helper)!;
        var options = new SchedulerHostOptions
        {
            Environment = "Development",
            DeploymentRoot = root,
            TaskRunRoot = Path.Combine(Path.GetTempPath(), "ifm-sm-s2-tests", Guid.NewGuid().ToString("N")),
            TaskCatalog =
            [
                new ScheduledTaskCatalogDefinition
                {
                    TaskKey = "helper",
                    DisplayName = "Helper",
                    Description = "Integration helper",
                    WorkingDirectory = ".",
                    ExecutablePath = Path.GetFileName(helper),
                    MaximumRuntimeSeconds = 30
                }
            ]
        };
        options.Validate();
        var dataSource = NpgsqlDataSource.Create(await fixture.CreateDatabaseConnectionStringAsync());
        var migrator = new SchedulerDatabaseMigrator(
            dataSource,
            NullLogger<SchedulerDatabaseMigrator>.Instance);
        var catalog = new TaskCatalogProvider(options, dataSource);
        var store = new SchedulerStore(dataSource, options);
        return new SchedulerTestServices(
            options,
            dataSource,
            migrator,
            catalog,
            store,
            new SchedulerBootstrapState(),
            new SchedulerHealthState());
    }

    private static NewScheduledRun NewRun()
    {
        var runId = Guid.NewGuid();
        var root = Path.Combine(Path.GetTempPath(), "ifm-sm-s2-run", runId.ToString("N"));
        return new NewScheduledRun(
            runId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            "helper",
            ScheduledRunOrigin.Manual,
            null,
            DateTimeOffset.UtcNow,
            Path.Combine(root, "stdout.log"),
            Path.Combine(root, "stderr.log"));
    }

    private sealed record SchedulerTestServices(
        SchedulerHostOptions Options,
        NpgsqlDataSource DataSource,
        SchedulerDatabaseMigrator Migrator,
        TaskCatalogProvider Catalog,
        SchedulerStore Store,
        SchedulerBootstrapState Bootstrap,
        SchedulerHealthState Health);
}

public sealed class SchedulerHostPostgresFixture : IAsyncLifetime
{
    private const string Image = "postgres:17.2";
    private const string Password = "sm-s2-disposable-password";
    private readonly string _containerName = $"ifm-sm-s2-{Guid.NewGuid():N}"[..30];

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var port = FreePort();
        await DockerAsync([
            "run", "--detach", "--name", _containerName,
            "--publish", $"127.0.0.1:{port}:5432",
            "--env", $"POSTGRES_PASSWORD={Password}",
            Image]);
        ConnectionString = $"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Password={Password};SSL Mode=Disable;Pooling=false";
        await WaitForPostgreSqlAsync();
    }

    public async Task DisposeAsync() => await DockerAsync(["rm", "--force", _containerName], allowFailure: true);

    public async Task<string> CreateDatabaseConnectionStringAsync()
    {
        var database = $"ifm_sm_s2_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE {database};";
        await command.ExecuteNonQueryAsync();
        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database };
        return builder.ConnectionString;
    }

    private async Task WaitForPostgreSqlAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(ConnectionString);
                await connection.OpenAsync();
                return;
            }
            catch (NpgsqlException)
            {
                await Task.Delay(250);
            }
        }

        throw new TimeoutException("The disposable SM-S2 PostgreSQL container did not become ready.");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string> DockerAsync(IReadOnlyList<string> arguments, bool allowFailure = false)
    {
        var start = new ProcessStartInfo
        {
            FileName = "docker",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Docker could not be started.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (!allowFailure && process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Docker failed with exit code {process.ExitCode}: {error}");
        }

        return output.Trim();
    }
}
