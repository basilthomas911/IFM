using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Npgsql;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ReferenceIntegrationInfrastructureCollection
    : ICollectionFixture<ReferenceIntegrationInfrastructureFixture>
{
    public const string Name = "Reference isolated infrastructure";
}

/// <summary>
/// Owns the disposable PostgreSQL, Redis, and NATS services used by the Reference
/// integration tests. Docker assigns every loopback port so the fixture neither
/// depends on nor collides with developer infrastructure.
/// </summary>
public sealed class ReferenceIntegrationInfrastructureFixture : IAsyncLifetime
{
    const string PostgresImage = "postgres:17.2";
    const string RedisImage = "redis:latest";
    const string NatsImage = "nats:2.12.0-alpine";
    const string PostgresUser = "postgres";
    const string PostgresPassword = "reference-integration-fixture";
    const string Database = "ifm_reference_integration_tests";

    readonly string _runId = Guid.NewGuid().ToString("N")[..12];
    readonly Dictionary<string, string?> _previousEnvironment = new(StringComparer.Ordinal);
    string PostgresContainer => $"ifm-reference-pg-{_runId}";
    string RedisContainer => $"ifm-reference-redis-{_runId}";
    string NatsContainer => $"ifm-reference-nats-{_runId}";

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string RedisConnectionString { get; private set; } = string.Empty;
    public string NatsUrl { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        try
        {
            await DockerAsync([
                "run", "--detach", "--name", PostgresContainer,
                "--label", $"ifm.reference.integration.run={_runId}",
                "--publish", "127.0.0.1::5432",
                "--env", $"POSTGRES_USER={PostgresUser}",
                "--env", $"POSTGRES_PASSWORD={PostgresPassword}",
                "--env", $"POSTGRES_DB={Database}",
                PostgresImage]);
            await DockerAsync([
                "run", "--detach", "--name", RedisContainer,
                "--label", $"ifm.reference.integration.run={_runId}",
                "--publish", "127.0.0.1::6379",
                RedisImage]);
            await DockerAsync([
                "run", "--detach", "--name", NatsContainer,
                "--label", $"ifm.reference.integration.run={_runId}",
                "--publish", "127.0.0.1::4222",
                NatsImage, "--jetstream"]);

            var postgresPort = await MappedPortAsync(PostgresContainer, "5432/tcp");
            var redisPort = await MappedPortAsync(RedisContainer, "6379/tcp");
            var natsPort = await MappedPortAsync(NatsContainer, "4222/tcp");
            PostgresConnectionString = $"Host=127.0.0.1;Port={postgresPort};Database={Database};SSL Mode=Disable;Pooling=false";
            RedisConnectionString = $"127.0.0.1:{redisPort},abortConnect=false";
            NatsUrl = $"nats://127.0.0.1:{natsPort}";

            SetCredentialEnvironment("POSTGRES_DEV_KEY");
            SetCredentialEnvironment("POSTGRES_TEST_KEY");

            await WaitForPostgresAsync(postgresPort);
            await WaitForTcpAsync(redisPort, "Redis");
            await WaitForTcpAsync(natsPort, "NATS");
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        await DockerAsync(["rm", "--force", NatsContainer], allowFailure: true);
        await DockerAsync(["rm", "--force", RedisContainer], allowFailure: true);
        await DockerAsync(["rm", "--force", PostgresContainer], allowFailure: true);
        foreach (var (name, value) in _previousEnvironment)
            Environment.SetEnvironmentVariable(name, value);
        _previousEnvironment.Clear();
    }

    void SetCredentialEnvironment(string name)
    {
        _previousEnvironment.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, JsonSerializer.Serialize(new
        {
            userid = PostgresUser,
            password = PostgresPassword
        }));
    }

    async Task WaitForPostgresAsync(int port)
    {
        var directConnection = new NpgsqlConnectionStringBuilder(PostgresConnectionString)
        {
            Username = PostgresUser,
            Password = PostgresPassword
        }.ConnectionString;
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var connection = new NpgsqlConnection(directConnection);
                await connection.OpenAsync();
                return;
            }
            catch (NpgsqlException)
            {
                await Task.Delay(250);
            }
        }

        throw new TimeoutException($"The disposable PostgreSQL container on port {port} did not become ready.");
    }

    static async Task WaitForTcpAsync(int port, string service)
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(250);
            }
        }

        throw new TimeoutException($"The disposable {service} container on port {port} did not become ready.");
    }

    static async Task<int> MappedPortAsync(string container, string containerPort)
    {
        var output = await DockerAsync(["port", container, containerPort]);
        var endpoint = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Single();
        return int.Parse(endpoint[(endpoint.LastIndexOf(':') + 1)..]);
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
            throw new InvalidOperationException($"Docker failed with exit code {process.ExitCode}: {error}");
        return output.Trim();
    }
}
