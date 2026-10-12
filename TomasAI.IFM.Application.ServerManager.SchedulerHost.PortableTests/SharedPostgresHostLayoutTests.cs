using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TomasAI.IFM.Application.ServerManager.SchedulerHost;

namespace TomasAI.IFM.Application.ServerManager.SchedulerHost.PortableTests;

[Collection("Shared PostgreSQL credential environment")]
public sealed class SharedPostgresHostLayoutTests
{
    [Theory]
    [InlineData(true, "shared-layout-test")]
    [InlineData(false, "scheduler-layout-test")]
    public void Scheduler_and_Quartz_use_the_shared_host_connection_when_supplied(bool shared, string expectedDatabase)
    {
        var names = new[] { "DOTNET_ENVIRONMENT", "ASPNETCORE_ENVIRONMENT", "POSTGRES_TEST_KEY" };
        var previous = names.Select(Environment.GetEnvironmentVariable).ToArray();
        Environment.SetEnvironmentVariable(names[0], "Test");
        Environment.SetEnvironmentVariable(names[1], "Test");
        Environment.SetEnvironmentVariable(names[2], "{\"userid\":\"layout-test\",\"password\":\"not-used\"}");
        try
        {
        using var host = SchedulerHostApplication.Create([], configuration =>
        {
            configuration.Sources.Clear();
            var values = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SchedulerDbConnection"] = "Host=localhost;Database=scheduler-layout-test;Username=layout-test",
                ["SchedulerHost:Environment"] = "Development",
                ["SchedulerHost:ActorManaged"] = "false"
            };
            if (shared) values["Storage:PostgresConnection"] = "Host=localhost;Database=shared-layout-test";
            configuration.AddInMemoryCollection(values);
        });
        var source = host.Services.GetRequiredService<NpgsqlDataSource>();
        Assert.Equal(expectedDatabase, new NpgsqlConnectionStringBuilder(source.ConnectionString).Database);
        // Host construction verifies registration without starting scheduled jobs or opening a database connection.
        Assert.NotNull(host.Services.GetRequiredService<Quartz.ISchedulerFactory>());
        }
        finally
        {
            for (var index = 0; index < names.Length; index++) Environment.SetEnvironmentVariable(names[index], previous[index]);
        }
    }
}

[CollectionDefinition("Shared PostgreSQL credential environment", DisableParallelization = true)]
public sealed class SharedPostgresCredentialEnvironmentCollection;
