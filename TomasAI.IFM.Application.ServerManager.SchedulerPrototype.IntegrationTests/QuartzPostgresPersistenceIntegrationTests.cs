using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using FluentAssertions;
using Npgsql;
using Quartz;
using Quartz.Impl;
using Xunit;

namespace TomasAI.IFM.Application.ServerManager.SchedulerPrototype.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QuartzPostgresPrototypeCollection : ICollectionFixture<QuartzPostgresPersistenceFixture>
{
    public const string Name = "Server Manager Quartz PostgreSQL prototype";
}

[Collection(QuartzPostgresPrototypeCollection.Name)]
public sealed class QuartzPostgresPersistenceIntegrationTests(QuartzPostgresPersistenceFixture fixture)
{
    [Fact]
    [Trait("Category", "SM-S0-NativeIntegration")]
    public async Task Job_and_trigger_survive_a_scheduler_restart()
    {
        var schedulerName = $"IFM-SM-S0-{Guid.NewGuid():N}";
        var jobKey = new JobKey("external-process", "prototype");
        var triggerKey = new TriggerKey("future-fire", "prototype");

        var first = await fixture.CreateSchedulerAsync(schedulerName);
        try
        {
            var job = JobBuilder.Create<PersistenceProbeJob>()
                .WithIdentity(jobKey)
                .UsingJobData("taskDefinitionId", "prototype-task")
                .StoreDurably()
                .Build();
            var trigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .ForJob(job)
                .StartAt(DateTimeOffset.UtcNow.AddHours(1))
                .Build();

            await first.ScheduleJob(job, trigger);
            (await first.CheckExists(jobKey)).Should().BeTrue();
            (await first.CheckExists(triggerKey)).Should().BeTrue();
        }
        finally
        {
            await first.Shutdown(waitForJobsToComplete: true);
        }

        var second = await fixture.CreateSchedulerAsync(schedulerName);
        try
        {
            var restoredJob = await second.GetJobDetail(jobKey);
            var restoredTrigger = await second.GetTrigger(triggerKey);

            restoredJob.Should().NotBeNull();
            restoredJob!.JobDataMap.GetString("taskDefinitionId").Should().Be("prototype-task");
            restoredTrigger.Should().NotBeNull();
            restoredTrigger!.JobKey.Should().Be(jobKey);
            (await second.DeleteJob(jobKey)).Should().BeTrue();
        }
        finally
        {
            await second.Shutdown(waitForJobsToComplete: true);
        }
    }
}

public sealed class QuartzPostgresPersistenceFixture : IAsyncLifetime
{
    const string Image = "postgres:17.2";
    const string Password = "sm-s0-disposable-password";
    readonly string _containerName = $"ifm-sm-s0-quartz-{Guid.NewGuid():N}"[..31];
    int _port;

    internal string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _port = FreePort();
        await DockerAsync([
            "run", "--detach", "--name", _containerName,
            "--publish", $"127.0.0.1:{_port}:5432",
            "--env", $"POSTGRES_PASSWORD={Password}",
            Image]);
        ConnectionString = $"Host=127.0.0.1;Port={_port};Database=postgres;Username=postgres;Password={Password};SSL Mode=Disable;Pooling=false";
        await WaitForPostgreSqlAsync();
        await InstallSchemaAsync();
    }

    internal async Task<IScheduler> CreateSchedulerAsync(string schedulerName)
    {
        var factory = new StdSchedulerFactory(
            QuartzPostgresPrototypeConfiguration.Create(ConnectionString, schedulerName));
        return await factory.GetScheduler();
    }

    public async Task DisposeAsync()
    {
        await DockerAsync(["rm", "--force", _containerName], allowFailure: true);
    }

    async Task InstallSchemaAsync()
    {
        var assembly = typeof(QuartzPostgresPersistenceFixture).Assembly;
        var resourceName = assembly.GetManifestResourceNames().Single(name =>
            name.EndsWith("Database.tables_postgres.sql", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("The embedded Quartz PostgreSQL schema could not be opened.");
        using var reader = new StreamReader(stream);
        var sql = await reader.ReadToEndAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE SCHEMA ifm_quartz;" + Environment.NewLine + sql;
        await command.ExecuteNonQueryAsync();
    }

    async Task WaitForPostgreSqlAsync()
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

        throw new TimeoutException("The disposable SM-S0 PostgreSQL container did not become ready.");
    }

    static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static async Task<string> DockerAsync(IReadOnlyList<string> arguments, bool allowFailure = false)
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
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Docker could not be started.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (!allowFailure && process.ExitCode != 0)
            throw new InvalidOperationException(
                $"The disposable SM-S0 PostgreSQL operation failed with exit code {process.ExitCode}: {error}");
        return output.Trim();
    }
}

public sealed class PersistenceProbeJob : IJob
{
    public Task Execute(IJobExecutionContext context) => Task.CompletedTask;
}
