using TomasAI.IFM.Domain.Reference.Repository;
using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.TradeStrategySymbols;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Api.Server.Core.MarketData.InstrumentDefinitions;

/// <summary>Narrow maintenance composition: ReferenceDb, sequence IDs and Databento only.</summary>
internal static class InstrumentDefinitionMaintenance
{
    public static async Task RunAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        using var logs = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
        var logger = logs.CreateLogger<DbProvider>();
        var settings = TomasAI.IFM.Application.Storage.ApplicationStorageConnections.CreatePostgres(
            configuration["IFM_TEST_POSTGRES_CONNECTION"] ?? configuration["Storage:PostgresConnection"]
                ?? configuration.GetConnectionString("EventSourceActorDbConnection")
                ?? throw new InvalidOperationException("The shared PostgreSQL application connection is required."));
        var objects = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => objects[type]));
        var sequenceDb = new SequenceIdDbContext(settings, factory, logger);
        objects.Add(typeof(IObjectRepository<SequenceIdDbContext>), sequenceDb);
        var referenceDb = new ReferenceDbContext(settings, factory, new PostgresSequenceIdGenerator(sequenceDb), logger);
        objects.Add(typeof(IObjectRepository<ReferenceDbContext>), referenceDb);
        await new ReferenceSchemaDb(settings, logger).CreateAsync(
            ["reference"], cancellationToken);
        var options = new DatabentoMarketDataRuntimeOptions
        {
            Contracts = [],
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development,
                configuration.GetValue<string>("AppSettings:Databento:Dataset") ?? "GLBX.MDP3"),
            TradeStrategySymbolDatasets = configuration.GetSection("AppSettings:Databento:TradeStrategySymbolDatasets").Get<string[]>() ?? []
        };
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var definitions = new InstrumentDefinitionStore(referenceDb,new TradeStrategySymbolStore(factory,new PostgresSequenceIdGenerator(sequenceDb)));
        var refresh = new InstrumentDefinitionRefresh(new DatabentoInstrumentDefinitionClient(http), definitions,
            options, TimeProvider.System, logs.CreateLogger<InstrumentDefinitionRefresh>());
        var snapshot = await refresh.RefreshAsync(cancellationToken);
        var catalog = new StoredInstrumentDefinitionSymbolCatalog(definitions);
        foreach (var family in new[] { TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType.Futures,
            TomasAI.IFM.Domain.Reference.Shared.ViewModels.TradeStrategyFamilyType.FuturesOption })
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var result = await catalog.GetAsync(family, cancellationToken);
            if (!result.Success) throw new InvalidOperationException(result.ErrorMessage);
            Console.WriteLine($"Verified {family}: {result.Value!.Length} symbols from ReferenceDb in {started.ElapsedMilliseconds} ms.");
        }
        Console.WriteLine($"Published instrument_definition snapshot {snapshot.Id}: {snapshot.RecordCount} exact records.");
    }
}
