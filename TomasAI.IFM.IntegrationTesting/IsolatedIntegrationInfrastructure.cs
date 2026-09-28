using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace TomasAI.IFM.IntegrationTesting;

/// <summary>
/// Owns an isolated PostgreSQL, Redis, NATS/JetStream, and Cassandra-compatible CQL
/// environment whose host ports and data namespaces are unique to one test run.
/// </summary>
public sealed class IsolatedIntegrationInfrastructure : IAsyncDisposable
{
    const string PostgresImage = "postgres:17.2";
    const string RedisImage = "redis:latest";
    const string NatsImage = "nats:2.12.0-alpine";
    const string CqlImage = "cassandra:5.0";

    readonly string scope;
    readonly string runId = Guid.NewGuid().ToString("N")[..12];
    readonly Dictionary<string, string?> previousEnvironment = new(StringComparer.Ordinal);
    bool started;

    /// <summary>Creates an isolated infrastructure owner for the named test scope.</summary>
    public IsolatedIntegrationInfrastructure(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        this.scope = new string(scope.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        if (this.scope.Length == 0)
            throw new ArgumentException("The infrastructure scope must contain a letter or digit.", nameof(scope));
    }

    string PostgresContainer => $"ifm-{scope}-pg-{runId}";
    string RedisContainer => $"ifm-{scope}-redis-{runId}";
    string NatsContainer => $"ifm-{scope}-nats-{runId}";
    string AuxiliaryNatsContainer => $"ifm-{scope}-nats-aux-{runId}";
    /// <summary>Gets the unique run identifier shared by every resource in this fixture.</summary>
    public string RunId => runId;

    string CqlContainer => $"ifm-{scope}-cql-{runId}";

    /// <summary>Gets the PostgreSQL user created for the isolated instance.</summary>
    public string PostgresUser => "postgres";

    /// <summary>Gets the PostgreSQL password created for the isolated instance.</summary>
    public string PostgresPassword => scope == "domainactors"
        ? "ifm-benchmark-only" : "ifm-integration-fixture";

    /// <summary>Gets the unique PostgreSQL database name.</summary>
    public string PostgresDatabase => scope == "domainactors"
        ? $"ifm_eventlog_bench_{runId}_synthetic_host"
        : scope.StartsWith("eventlogbench", StringComparison.Ordinal) ? $"ifm_eventlog_bench_{runId}_{scope}"
        : $"ifm_{scope}_{runId}";

    /// <summary>Gets the unique CQL keyspace name.</summary>
    public string CqlKeyspace => $"ifm_{scope}_{runId}";

    /// <summary>Gets the isolated PostgreSQL connection string.</summary>
    public string PostgresConnectionString { get; private set; } = string.Empty;

    /// <summary>Gets the isolated Redis endpoint.</summary>
    public string RedisConnectionString { get; private set; } = string.Empty;

    /// <summary>Gets the isolated NATS endpoint.</summary>
    public string NatsUrl { get; private set; } = string.Empty;

    /// <summary>Gets the isolated NATS endpoint reserved for separately configured actor hosts.</summary>
    public string AuxiliaryNatsUrl { get; private set; } = string.Empty;

    /// <summary>Gets the isolated CQL connection string.</summary>
    public string CqlConnectionString { get; private set; } = string.Empty;

    /// <summary>Gets the mapped PostgreSQL port.</summary>
    public int PostgresPort { get; private set; }

    /// <summary>Gets the mapped CQL port.</summary>
    public int CqlPort { get; private set; }

    /// <summary>Starts all isolated services and waits until each is ready.</summary>
    public async Task StartAsync()
    {
        if (started)
            throw new InvalidOperationException("The isolated integration infrastructure has already been started.");

        try
        {
            await DockerAsync(["run", "--detach", "--name", PostgresContainer, "--label", $"ifm.integration.run={runId}",
                "--publish", "127.0.0.1::5432", "--env", $"POSTGRES_USER={PostgresUser}",
                "--env", $"POSTGRES_PASSWORD={PostgresPassword}", "--env", $"POSTGRES_DB={PostgresDatabase}", PostgresImage]);
            await DockerAsync(["run", "--detach", "--name", RedisContainer, "--label", $"ifm.integration.run={runId}",
                "--publish", "127.0.0.1::6379", RedisImage]);
            await DockerAsync(["run", "--detach", "--name", NatsContainer, "--label", $"ifm.integration.run={runId}",
                "--publish", "127.0.0.1::4222", NatsImage, "--jetstream"]);
            if (scope == "domainactors")
                await DockerAsync(["run", "--detach", "--name", AuxiliaryNatsContainer, "--label", $"ifm.integration.run={runId}",
                    "--publish", "127.0.0.1::4222", NatsImage, "--jetstream"]);
            await DockerAsync(["run", "--detach", "--name", CqlContainer, "--label", $"ifm.integration.run={runId}",
                "--publish", "127.0.0.1::9042", "--memory", "2g", "--env", "MAX_HEAP_SIZE=512M",
                "--env", "HEAP_NEWSIZE=100M", CqlImage]);

            PostgresPort = await MappedPortAsync(PostgresContainer, "5432/tcp");
            var redisPort = await MappedPortAsync(RedisContainer, "6379/tcp");
            var natsPort = await MappedPortAsync(NatsContainer, "4222/tcp");
            CqlPort = await MappedPortAsync(CqlContainer, "9042/tcp");
            var auxiliaryNatsPort = scope == "domainactors"
                ? await MappedPortAsync(AuxiliaryNatsContainer, "4222/tcp") : natsPort;

            PostgresConnectionString = $"Host=127.0.0.1;Port={PostgresPort};Database={PostgresDatabase};SSL Mode=Disable;Pooling=true";
            RedisConnectionString = $"127.0.0.1:{redisPort},abortConnect=false";
            NatsUrl = $"nats://127.0.0.1:{natsPort}";
            CqlConnectionString = GetCqlConnectionString(CqlKeyspace);

            AuxiliaryNatsUrl = $"nats://127.0.0.1:{auxiliaryNatsPort}";
            SetCredentialEnvironment("POSTGRES_DEV_KEY");
            SetCredentialEnvironment("POSTGRES_TEST_KEY");

            await WaitForPostgresAsync();
            await WaitForTcpAsync(redisPort, "Redis");
            await WaitForTcpAsync(natsPort, "NATS");
            await WaitForCqlAsync();
            await CqlAsync($"CREATE KEYSPACE IF NOT EXISTS {CqlKeyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};");
            await WaitForTcpAsync(auxiliaryNatsPort, "auxiliary NATS");
            started = true;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    /// <summary>Builds a connection string for a keyspace hosted by this isolated CQL container.</summary>
    public string GetCqlConnectionString(string keyspace)
    {
        ValidateCqlIdentifier(keyspace);
        if (CqlPort <= 0)
            throw new InvalidOperationException("The isolated CQL container has not started.");
        return $"Contact Points=127.0.0.1;Port={CqlPort};Default Keyspace={keyspace}";
    }

    /// <summary>Creates a keyspace in this isolated CQL container when it does not exist.</summary>
    public async Task EnsureCqlKeyspaceAsync(string keyspace)
    {
        ValidateCqlIdentifier(keyspace);
        await CqlAsync($"CREATE KEYSPACE IF NOT EXISTS {keyspace} WITH replication = {{'class':'SimpleStrategy','replication_factor':1}};");
    }

    static void ValidateCqlIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("CQL identifiers may contain only ASCII letters, digits, and underscores.", nameof(value));
    /// <inheritdoc />
    }
    public async ValueTask DisposeAsync()
    {
        await DockerAsync(["rm", "--force", CqlContainer], allowFailure: true);
        await DockerAsync(["rm", "--force", NatsContainer], allowFailure: true);
        await DockerAsync(["rm", "--force", RedisContainer], allowFailure: true);
        await DockerAsync(["rm", "--force", PostgresContainer], allowFailure: true);
        await DockerAsync(["rm", "--force", AuxiliaryNatsContainer], allowFailure: true);
        foreach (var (name, value) in previousEnvironment)
            Environment.SetEnvironmentVariable(name, value);
        previousEnvironment.Clear();
        started = false;
    }

    void SetCredentialEnvironment(string name)
    {
        previousEnvironment.TryAdd(name, Environment.GetEnvironmentVariable(name));
        Environment.SetEnvironmentVariable(name, JsonSerializer.Serialize(new
        {
            userid = PostgresUser,
            password = PostgresPassword
        }));
    }

    async Task WaitForPostgresAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(1);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await DockerAsync(["exec", PostgresContainer, "pg_isready", "--username", PostgresUser, "--dbname", PostgresDatabase]);
                return;
            }
            catch (InvalidOperationException)
            {
                await Task.Delay(250);
            }
        }

        throw new TimeoutException("The disposable PostgreSQL container did not become ready.");
    }

    async Task WaitForCqlAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await CqlAsync("SELECT now() FROM system.local;");
                return;
            }
            catch (InvalidOperationException)
            {
                await Task.Delay(500);
            }
        }

        throw new TimeoutException("The disposable CQL node did not become ready.");
    }

    async Task CqlAsync(string cql)
        => _ = await DockerAsync(["exec", CqlContainer, "cqlsh", "-e", cql]);

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
        var endpoint = output.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Single();
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
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        if (!allowFailure && process.ExitCode != 0)
            throw new InvalidOperationException($"Docker failed with exit code {process.ExitCode}: {error}");
        return output.Trim();
    }
}
