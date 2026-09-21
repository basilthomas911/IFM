using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Explicit, process-local qualification entry point. Never selected by normal settings.</summary>
public sealed class EventLogQualification
{
    public const string Flag = "--event-log-qualification=";
    public const string BrokerUrl = "nats://127.0.0.1:24223";
    public const string HttpUrl = "http://127.0.0.1:25443";
    public static EventLogQualification? Active { get; private set; }
    public string RunId { get; }
    public IReadOnlyDictionary<string, string?> Settings { get; }
    public EventLogQualification(string runId, string environment, string artifactRoot)
    {
        if (environment != "Test" || !Regex.IsMatch(runId, "\\A[a-f0-9]{12}\\z"))
            throw new InvalidOperationException("Qualification requires environment Test and a 12-digit lowercase hexadecimal run ID.");
        RunId = runId;
        var root = Path.GetFullPath(artifactRoot);
        var pg = $"Host=127.0.0.1;Port=25432;Database=ifm_eventlog_bench_{runId}_synthetic_host";
        var settings = new Dictionary<string, string?>
        {
            ["urls"] = HttpUrl,
            ["Telemetry:Metrics:Enabled"] = "false",
            ["Kestrel:Endpoints:Http:Url"] = HttpUrl,
            ["AppSettings:RedisUri"] = "127.0.0.1:26379,abortConnect=false",
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
        foreach (var key in new[] { "Trade", "Fund", "Reference", "OptionPricer", "MarketData", "Securities" })
            settings[$"ConnectionStrings:{key}DbConnection"] =
                $"Contact Points=127.0.0.1;Port=29042;Default Keyspace=ifm_synthetic_{runId}_{key.ToLowerInvariant()}";
        foreach (var key in new[] { "CommandServerBaseUri", "QueryServerBaseUri", "TelemetryServerBaseUri", "PredictiveModelServerBaseUri" })
            settings[$"AppSettings:{key}"] = HttpUrl;
        Settings = settings;
    }

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
            || a.StartsWith("--publish-", StringComparison.Ordinal) || a.StartsWith("--retain-", StringComparison.Ordinal)))
            throw new InvalidOperationException("Qualification cannot combine with maintenance modes.");
        var run = flags[0][Flag.Length..];
        var qualification = new EventLogQualification(run, builder.Environment.EnvironmentName,
            Path.Combine(AppContext.BaseDirectory, "qualification", run));
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
