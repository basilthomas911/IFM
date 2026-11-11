using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.Storage;
using Xunit;

namespace TomasAI.IFM.Application.Storage.IntegrationTests.EventSourceDb;

public sealed class RedisFreeDatabaseRestoreTests
{
    [Fact]
    public async Task ActualPostgresDumpRestoreReconstructsIdentityAndDataWithFreshCacheOwners()
    {
        await using var infrastructure = new IsolatedIntegrationInfrastructure("redisrestore", includeCql: false);
        await infrastructure.StartAsync();
        var settings = new DbConnectionSettings().Add(EventSourceActorDbContext.EventSourceActorDbConnection,
            infrastructure.PostgresConnectionString, "System.Data.Postgres");
        await new EventSourceSchemaDb(settings, NullLogger<DbProvider>.Instance).CreateAllAsync();
        var authenticated = new NpgsqlConnectionStringBuilder(infrastructure.PostgresConnectionString)
            { Username = infrastructure.PostgresUser, Password = infrastructure.PostgresPassword }.ConnectionString;
        const string stream = "RedisRemoval.ActualRestore";
        var oldOwner = CreateOwner();
        var baselineId = await oldOwner.GetEventStreamIdAsync(stream);
        await ((IAsyncDisposable)oldOwner).DisposeAsync();
        await ExecuteSql("CREATE TABLE redis_removal_restore_probe(value text); INSERT INTO redis_removal_restore_probe VALUES ('baseline');");
        var container = "ifm-redisrestore-pg-" + infrastructure.RunId;
        var dump = await DockerAsync(["exec", container, "pg_dump", "-U", infrastructure.PostgresUser,
            "-d", infrastructure.PostgresDatabase, "--clean", "--if-exists", "--no-owner", "--no-acl"]);
        Assert.Contains("event_stream_id", dump);
        await ExecuteSql("UPDATE event_stream_id SET eventstreamid=eventstreamid+1000; UPDATE redis_removal_restore_probe SET value='newer';");
        var newerOwner = CreateOwner();
        Assert.NotEqual(baselineId, await newerOwner.GetEventStreamIdAsync(stream));
        await ((IAsyncDisposable)newerOwner).DisposeAsync();
        await DockerAsync(["exec", "-i", container, "psql", "-U", infrastructure.PostgresUser,
            "-d", infrastructure.PostgresDatabase, "--set", "ON_ERROR_STOP=1"], dump);
        // A real cutover restarts all processes and therefore all connection pools and cache owners.
        NpgsqlConnection.ClearAllPools();
        await using var restarted = CreateOwner();
        Assert.Equal(baselineId, await restarted.GetEventStreamIdAsync(stream));
        await using var verify = new NpgsqlConnection(authenticated);
        await verify.OpenAsync();
        await using var proof = new NpgsqlCommand("SELECT value FROM redis_removal_restore_probe", verify);
        Assert.Equal("baseline", await proof.ExecuteScalarAsync());

        EventSourceActorDbContext CreateOwner()
        {
            var repositories = new Dictionary<Type, object>();
            var factory = new DbContextFactory(new DbContextResolver(type => repositories[type]));
            var owner = new EventSourceActorDbContext(settings, factory, NullLogger<DbProvider>.Instance);
            repositories.Add(typeof(IObjectRepository<EventSourceActorDbContext>), owner);
            return owner;
        }

        async Task ExecuteSql(string sql)
        {
            await using var db = new NpgsqlConnection(authenticated);
            await db.OpenAsync();
            await using var command = new NpgsqlCommand(sql, db);
            await command.ExecuteNonQueryAsync();
        }
    }

    static async Task<string> DockerAsync(string[] arguments, string? input = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = input is not null
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker did not start.");
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token);
                process.StandardInput.Close();
            }
            await process.WaitForExitAsync(timeout.Token);
            var result = await output;
            var errors = await error;
            Assert.True(process.ExitCode == 0, errors);
            return result;
        }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }
}
