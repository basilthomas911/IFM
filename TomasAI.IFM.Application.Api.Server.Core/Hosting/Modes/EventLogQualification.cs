using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

/// <summary>Explicit, process-local qualification entry point. Never selected by normal settings.</summary>
public sealed class EventLogQualification
{
    public const string Flag = "--event-log-qualification=";
    public string BrokerUrl { get; }
    public const string HttpUrl = "http://127.0.0.1:25443";
    public static EventLogQualification? Active { get; private set; }
    public string RunId { get; }
    public IReadOnlyDictionary<string, string?> Settings { get; }
    public EventLogQualification(string runId, string environment, string artifactRoot, bool useExistingScylla = false)
        : this(
            runId,
            environment,
            artifactRoot,
            RequiredLoopbackPostgres("IFM_QUALIFICATION_POSTGRES_CONNECTION", runId),
            RequiredLoopbackUri("IFM_QUALIFICATION_NATS_URL", "nats"),
            RequiredLoopbackEndpoint("IFM_QUALIFICATION_REDIS_URL"),
            RequiredScyllaPort(),
            useExistingScylla)
    {
    }

    internal EventLogQualification(
        string runId,
        string environment,
        string artifactRoot,
        string postgresConnection,
        string natsUrl,
        string redisEndpoint,
        int scyllaPort,
        bool useExistingScylla = false)
    {
        if (environment != "Test" || !Regex.IsMatch(runId, "\\A[a-f0-9]{12}\\z"))
            throw new InvalidOperationException("Qualification requires environment Test and a 12-digit lowercase hexadecimal run ID.");
        RunId = runId;
        var root = Path.GetFullPath(artifactRoot);
        var pg = ValidateLoopbackPostgres(postgresConnection, runId);
        BrokerUrl = ValidateLoopbackUri(natsUrl, "IFM_QUALIFICATION_NATS_URL", "nats");
        var redis = ValidateLoopbackEndpoint(redisEndpoint, "IFM_QUALIFICATION_REDIS_URL");
        var settings = new Dictionary<string, string?>
        {
            ["urls"] = HttpUrl,
            ["Telemetry:Metrics:Enabled"] = "false",
            ["Kestrel:Endpoints:Http:Url"] = HttpUrl,
            ["AppSettings:RedisUri"] = redis,
            ["AppSettings:Databento:DeploymentProfile"] = "SyntheticCi",
            ["AppSettings:Databento:DataSource"] = "Synthetic",
            ["AppSettings:Databento:Synthetic:RecordCount"] = "1000",
            ["AppSettings:Databento:Synthetic:RecordsPerSecond"] = "10",
            ["AppSettings:Fmp:Enabled"] = "false",
            ["AppSettings:HistoricalAnalyticsWarmup:Enabled"] = "false",
            ["AppSettings:IntrinsicTimeStrategyWorkflow:Enabled"] = "false",
            ["AppSettings:IntrinsicTimeStrategyWorkflow:ProvisionDevelopmentMarketConditionAssessmentDefaults"] = "false",
            ["AppSettings:IntrinsicTimeStrategyWorkflow:DevelopmentPortfolio:Enabled"] = "false",
            ["ApplicationStartup:AutoStartAfterBootstrap"] = "false",
            ["MarketDataRecovery:Enabled"] = "false",
            ["EventLogPersistence:WriteMode"] = "BinaryCopy",
            ["EventLogPersistence:UseLz4Compression"] = "true",
            ["TradeBroker:Emulator:AccountAlias"] = $"QUALIFICATION-{runId}",
            ["TradeBroker:Emulator:LedgerPath"] = Path.Combine(root, "emulator-ledger.json"),
            ["Nats:Consumer:Url"] = BrokerUrl,
            ["Nats:JetStreamConsumer:Url"] = BrokerUrl
        };
        foreach (var key in new[] { "EventSourceActor", "MarketDataService", "Configuration", "SystemAdmin", "Portfolio", "Log", "SequenceId" })
            settings[$"ConnectionStrings:{key}DbConnection"] = pg;
        // A separately approved AIO-constrained run may use uniquely named keyspaces
        // on the existing local Scylla listener; all other qualification routing stays fixed.
        if (scyllaPort is <= 0 or > ushort.MaxValue)
            throw new InvalidOperationException("The isolated qualification CQL port must be valid.");
        foreach (var key in new[] { "Trade", "Fund", "Reference", "OptionPricer", "MarketData", "Securities" })
            settings[$"ConnectionStrings:{key}DbConnection"] =
                $"Contact Points=127.0.0.1;Port={scyllaPort};Default Keyspace=ifm_synthetic_{runId}_{key.ToLowerInvariant()}";
        foreach (var key in new[] { "TelemetryServerBaseUri" })
            settings[$"AppSettings:{key}"] = HttpUrl;
        Settings = settings;
    }

    static string RequiredLoopbackPostgres(string name, string runId)
    {
        var value = Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} is required.");
        return ValidateLoopbackPostgres(value, runId);
    }

    static string ValidateLoopbackPostgres(string value, string runId)
    {
        var connection = new NpgsqlConnectionStringBuilder(value);
        if (connection.Host != "127.0.0.1" || connection.Port <= 0
            || connection.Database != $"ifm_eventlog_bench_{runId}_synthetic_host")
            throw new InvalidOperationException("Qualification PostgreSQL must be loopback and run-scoped.");
        return value;
    }

    static string RequiredLoopbackUri(string name, string scheme)
    {
        var value = Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} is required.");
        return ValidateLoopbackUri(value, name, scheme);
    }

    static string ValidateLoopbackUri(string value, string name, string scheme)
    {
        var uri = new Uri(value);
        if (uri.Scheme != scheme || uri.Host != "127.0.0.1" || uri.Port <= 0)
            throw new InvalidOperationException($"{name} must be a loopback endpoint.");
        return value;
    }

    static string RequiredLoopbackEndpoint(string name)
    {
        var value = Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} is required.");
        return ValidateLoopbackEndpoint(value, name);
    }

    static string ValidateLoopbackEndpoint(string value, string name)
    {
        if (!System.Net.IPEndPoint.TryParse(value.Split(',')[0], out var endpoint)
            || !System.Net.IPAddress.IsLoopback(endpoint.Address) || endpoint.Port <= 0)
            throw new InvalidOperationException($"{name} must be a loopback endpoint.");
        return value;
    }

    static int RequiredScyllaPort() =>
        int.Parse(Environment.GetEnvironmentVariable("IFM_QUALIFICATION_SCYLLA_PORT")
            ?? throw new InvalidOperationException("The isolated qualification CQL port is required."));

    public void Validate(IConfiguration config)
    {
        foreach (var pair in Settings)
            if (config[pair.Key] != pair.Value)
                throw new InvalidOperationException($"Qualification setting changed: {pair.Key}");
        foreach (var connection in config.GetSection("ConnectionStrings").GetChildren())
            if (!Settings.ContainsKey("ConnectionStrings:" + connection.Key))
                throw new InvalidOperationException($"Unreviewed qualification connection: {connection.Key}");
        foreach (var endpoint in config.GetSection("Kestrel:Endpoints").GetChildren())
            if (endpoint.Key != "Http")
                throw new InvalidOperationException("Qualification permits only its loopback HTTP endpoint.");
    }

    public static void Configure(WebApplicationBuilder builder, string[] args)
    {
        if (args.Any(a => a.StartsWith("--event-log-qualification", StringComparison.Ordinal)
            && !a.StartsWith(Flag, StringComparison.Ordinal)))
            throw new InvalidOperationException("Use --event-log-qualification=<run-id>; malformed qualification flags cannot fall back to normal startup.");
        var flags = args.Where(a => a.StartsWith(Flag, StringComparison.Ordinal)).ToArray();
        if (flags.Length == 0) return;
        if (flags.Length != 1 || args.Any(a => a.StartsWith("--bootstrap-", StringComparison.Ordinal)
            || a.StartsWith("--migrate-", StringComparison.Ordinal) || a.StartsWith("--refresh-", StringComparison.Ordinal)
            || a.StartsWith("--initialize-", StringComparison.Ordinal)
            || a.StartsWith("--publish-", StringComparison.Ordinal) || a.StartsWith("--retain-", StringComparison.Ordinal)))
            throw new InvalidOperationException("Qualification cannot combine with maintenance modes.");
        var run = flags[0][Flag.Length..];
        var useExistingScylla = Environment.GetEnvironmentVariable("IFM_QUALIFICATION_EXISTING_SCYLLA") == "1";
        var qualification = new EventLogQualification(run, builder.Environment.EnvironmentName,
            Path.Combine(AppContext.BaseDirectory, "qualification", run), useExistingScylla);
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABENTO_API_KEY")))
            throw new InvalidOperationException("Remove live Databento credentials from the qualification child environment.");
        builder.Configuration.AddInMemoryCollection(qualification.Settings);
        qualification.Validate(builder.Configuration);
        Active = qualification;
    }

    public async Task InitializeCandidateAsync(CancellationToken token)
    {
        // Dedicated test server credentials only; never resolve application secrets here.
        var builder = new NpgsqlConnectionStringBuilder(Settings["ConnectionStrings:EventSourceActorDbConnection"])
        { Username = "postgres", Password = "ifm-benchmark-only", Pooling = false };
        await using var db = new NpgsqlConnection(builder.ConnectionString);
        await db.OpenAsync(token);
        await using var transaction = await db.BeginTransactionAsync(token);
        await using var command = new NpgsqlCommand("""
            SET LOCAL lock_timeout='2s';
            SET LOCAL statement_timeout='30s';
            LOCK TABLE public.event_log IN ACCESS EXCLUSIVE MODE;
            DO $candidate$
            DECLARE shape text;
            BEGIN
                SELECT pg_get_constraintdef(oid) INTO shape FROM pg_constraint
                    WHERE conrelid='event_log'::regclass AND contype='p';
                IF shape='PRIMARY KEY (eventstreamid, eventnameid, eventversion)' THEN
                    ALTER TABLE event_log DROP CONSTRAINT event_log_pkey;
                    ALTER TABLE event_log ADD CONSTRAINT ux_event_log_stream_version_v3
                        PRIMARY KEY USING INDEX ux_event_log_stream_version_v3;
                ELSIF shape IS DISTINCT FROM 'PRIMARY KEY (eventstreamid, streamversion)' THEN
                    RAISE EXCEPTION 'Unsupported qualification event-log shape';
                END IF;
                IF (SELECT count(*) FROM pg_indexes WHERE schemaname='public' AND tablename='event_log') <> 3 THEN
                    RAISE EXCEPTION 'Qualification requires exactly three event-log indexes';
                END IF;
            END $candidate$;
            """, db, transaction);
        await command.ExecuteNonQueryAsync(token);
        await transaction.CommitAsync(token);
    }
}

/// <summary>Qualification factory clients may contact only the isolated API, never external providers.</summary>
public sealed class QualificationHttpFilter : Microsoft.Extensions.Http.IHttpMessageHandlerBuilderFilter
{
    public Action<Microsoft.Extensions.Http.HttpMessageHandlerBuilder> Configure(
        Action<Microsoft.Extensions.Http.HttpMessageHandlerBuilder> next) => builder =>
    {
        next(builder);
        builder.AdditionalHandlers.Insert(0, new LocalOnlyHandler());
    };

    sealed class LocalOnlyHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is not { Scheme: "http", Host: "127.0.0.1", Port: 25443 })
                throw new InvalidOperationException("Qualification rejected an outbound HTTP request.");
            return base.SendAsync(request, cancellationToken);
        }
    }
}
