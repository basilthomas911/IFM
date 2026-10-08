using Microsoft.Extensions.Configuration;
using Npgsql;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;

namespace TomasAI.IFM.Application.ServerManager.IntegrationTests;

[Collection(SchedulerHostPostgresCollection.Name)]
public sealed class SchedulerOwnershipLeaseTests(SchedulerHostPostgresFixture fixture)
{
    [Fact]
    public async Task Different_hosts_cannot_own_the_same_Quartz_name_even_with_different_environment_labels()
    {
        await using var source = NpgsqlDataSource.Create(await fixture.CreateDatabaseConnectionStringAsync());
        await using var first = new SchedulerOwnershipLease(source, new() { SchedulerName = "ownership-test" });
        await using var second = new SchedulerOwnershipLease(source, new() { SchedulerName = "ownership-test", Environment = "Other" });
        await first.AcquireAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => second.AcquireAsync(CancellationToken.None));
        Assert.True(first.IsOwned);
        Assert.False(second.IsOwned);
        await first.DisposeAsync();
        await second.AcquireAsync(CancellationToken.None);
        Assert.True(second.IsOwned);
    }

    [Fact]
    public async Task Losing_the_actual_lock_session_fences_new_work_and_releases_database_ownership()
    {
        await using var source = NpgsqlDataSource.Create(await fixture.CreateDatabaseConnectionStringAsync());
        await using var first = new SchedulerOwnershipLease(source, new() { SchedulerName = "loss-test" });
        await first.AcquireAsync(CancellationToken.None);
        await using var connection = await source.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = current_database() AND application_name = 'IFM Scheduler Ownership';";
        await command.ExecuteNonQueryAsync();
        await Assert.ThrowsAnyAsync<Exception>(() => first.EnsureOwnedAsync(CancellationToken.None));
        Assert.False(first.IsOwned);
        await using var replacement = new SchedulerOwnershipLease(source, new() { SchedulerName = "loss-test" });
        await replacement.AcquireAsync(CancellationToken.None);
        Assert.True(replacement.IsOwned);
    }

    [Fact]
    public async Task Generic_host_starts_and_stops_its_persisted_Quartz_scheduler()
    {
        var connection = await fixture.CreateDatabaseConnectionStringAsync();
        var root = Path.Combine(Path.GetTempPath(), "ifm-portable-host", Guid.NewGuid().ToString("N"));
        using var host = SchedulerHostApplication.Create([], configuration =>
        {
            configuration.Sources.Clear();
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SchedulerDbConnection"] = connection,
                ["SchedulerHost:DeploymentRoot"] = root,
                ["SchedulerHost:TaskRunRoot"] = Path.Combine(root, "runs"),
                ["SchedulerHost:SchedulerName"] = "generic-host-test",
                ["SchedulerHost:PipeName"] = $"IFM.HostTest.{Guid.NewGuid():N}",
                ["SchedulerHost:SeedInitialSchedules"] = "false"
            });
        });
        await host.StartAsync();
        var health = (SchedulerHealthState)host.Services.GetService(typeof(SchedulerHealthState))!;
        Assert.True(health.Current.SchedulingStarted, health.Current.Message);
        await host.StopAsync();
    }
}
