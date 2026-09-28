using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.IntegrationTesting;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Domain.Reference.IntegrationTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ReferenceIntegrationInfrastructureCollection
    : ICollectionFixture<ReferenceIntegrationInfrastructureFixture>
{
    public const string Name = "Reference isolated infrastructure";
}

/// <summary>
/// Owns the disposable PostgreSQL, Redis, NATS, Cassandra-compatible CQL, and production Kestrel
/// services used by the complete Reference integration-test project.
/// </summary>
public sealed class ReferenceIntegrationInfrastructureFixture : IAsyncLifetime
{
    readonly string _runId = Guid.NewGuid().ToString("N")[..12];
    readonly IsolatedIntegrationInfrastructure _infrastructure = new("reference");
    TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>? _source;
    TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>? _host;

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string RedisConnectionString { get; private set; } = string.Empty;
    public string NatsUrl { get; private set; } = string.Empty;
    public string CqlConnectionString { get; private set; } = string.Empty;
    public HttpClient HttpClient { get; private set; } = default!;
    public IActorProducer ActorProducer { get; private set; } = default!;
    public ReferenceDbContext ReferenceDb { get; private set; } = default!;
    public EventSourceActorDbContext ActorEventSourceDb { get; private set; } = default!;
    public TomasAI.IFM.Application.Blackboard.IBlackboardService BlackboardService { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        try
        {
            await _infrastructure.StartAsync();
            PostgresConnectionString = _infrastructure.PostgresConnectionString;
            RedisConnectionString = _infrastructure.RedisConnectionString;
            NatsUrl = _infrastructure.NatsUrl;
            CqlConnectionString = _infrastructure.CqlConnectionString;

            var logger = NullLogger<DbProvider>.Instance;
            var settings = new DbConnectionSettings()
                .Add("ConfigurationDbConnection", PostgresConnectionString, "System.Data.Postgres")
                .Add("EventSourceActorDbConnection", PostgresConnectionString, "System.Data.Postgres")
                .Add("SequenceIdDbConnection", PostgresConnectionString, "System.Data.Postgres")
                .Add("ReferenceDbConnection", CqlConnectionString, "System.Data.ScyllaDb");
            await new ConfigurationSchemaDb(settings, logger).CreateAllAsync();
            await new EventSourceSchemaDb(settings, logger).CreateAllAsync();
            await new SequenceIdSchemaDb(settings, logger).CreateAllAsync();
            await new ReferenceSchemaDb(settings, logger).CreateAllAsync();

            _source = new TomasAI.IFM.IntegrationTesting.KestrelWebApplicationFactory<TomasAI.IFM.Application.Api.Server.ApiServerEntryPoint>();
            _host = _source.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development")
                    .UseSetting("IFM_TEST_ACTOR_DOMAIN", "TomasAI.IFM.Domain.Reference")
                    .UseSetting("IFM_TEST_NATS_URL", NatsUrl)
                    .UseSetting("IFM_TEST_REDIS_URL", RedisConnectionString)
                    .UseSetting("IFM_TEST_POSTGRES_CONNECTION", PostgresConnectionString);
                foreach (var name in new[] { "MarketData", "OptionPricer", "Reference", "Securities", "Trade" })
                    builder.UseSetting($"ConnectionStrings:{name}DbConnection", CqlConnectionString);
                builder.ConfigureServices(services => services.RemoveAll<IHostedService>());
            });

            HttpClient = _host.CreateClient();
            if (HttpClient.BaseAddress!.Port == 0)
                throw new InvalidOperationException("The Reference integration host did not bind an isolated Kestrel port.");
            using var startupDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await WaitForActorReadinessAsync(HttpClient, startupDeadline.Token);

            ActorProducer = _host.Services.GetRequiredService<IActorProducer>();
            await ActorProducer.StartAsync(new ActorMailboxId(ActorType.Query, "ReferenceIntegrationTests"));
            var dbFactory = _host.Services.GetRequiredService<IDbContextFactory>();
            ReferenceDb = (ReferenceDbContext)dbFactory.ReferenceDb;
            ActorEventSourceDb = (EventSourceActorDbContext)dbFactory.ActorEventSourceDb;
            BlackboardService = _host.Services.GetRequiredService<TomasAI.IFM.Application.Blackboard.IBlackboardService>();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (ActorProducer is not null)
        {
            try { await ActorProducer.StopAsync(); }
            catch { }
        }
        HttpClient?.Dispose();
        if (_host is not null)
            await _host.DisposeAsync();
        if (_source is not null)
            await _source.DisposeAsync();
        await _infrastructure.DisposeAsync();
    }


    static async Task WaitForActorReadinessAsync(HttpClient client, CancellationToken token)
    {
        string responseBody = string.Empty;
        while (!token.IsCancellationRequested)
        {
            using var response = await client.GetAsync("/health/actors", token);
            responseBody = await response.Content.ReadAsStringAsync(token);
            if (response.IsSuccessStatusCode)
                return;
            await Task.Delay(100, token);
        }

        throw new TimeoutException($"The production actor runtime did not become ready: {responseBody}");
    }

}