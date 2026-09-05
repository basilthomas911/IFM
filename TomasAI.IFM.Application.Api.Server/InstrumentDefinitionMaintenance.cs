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

namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Narrow maintenance composition: ReferenceDb, sequence IDs and Databento only.</summary>
internal static class InstrumentDefinitionMaintenance
{
    public static async Task RunAsync(IConfiguration configuration, CancellationToken cancellationToken)
    {
        using var logs = LoggerFactory.Create(builder => builder.AddSimpleConsole(options => options.SingleLine = true));
        var logger = logs.CreateLogger<DbProvider>();
        var settings = new DbConnectionSettings()
            .Add(ReferenceDbContext.ReferenceDbConnection, configuration.GetConnectionString(ReferenceDbContext.ReferenceDbConnection)
                ?? throw new InvalidOperationException("ReferenceDbConnection is required."), "System.Data.ScyllaDb")
            .Add(SequenceIdDbContext.SequenceIdDbConnection, configuration.GetConnectionString(SequenceIdDbContext.SequenceIdDbConnection)
                ?? throw new InvalidOperationException("SequenceIdDbConnection is required."), "System.Data.Postgres");
        var objects = new Dictionary<Type, object>();
        var factory = new DbContextFactory(new DbContextResolver(type => objects[type]));
        var sequenceDb = new SequenceIdDbContext(settings, factory, logger);
        objects.Add(typeof(IObjectRepository<SequenceIdDbContext>), sequenceDb);
        var referenceDb = new ReferenceDbContext(settings, factory, new PostgresSequenceIdGenerator(sequenceDb), logger);
        objects.Add(typeof(IObjectRepository<ReferenceDbContext>), referenceDb);
        await new ReferenceSchemaDb(settings, logger).CreateAsync(
            ["instrument_definition", "instrument_definition_product", "instrument_definition_snapshot", "trade_strategy_symbol_v1"], cancellationToken);
        var options = new DatabentoMarketDataRuntimeOptions
        {
            Contracts = [],
            FeedOptions = DatabentoFeedOptions.ForProfile(FeedDeploymentProfile.Development,
                configuration.GetValue<string>("AppSettings:Databento:Dataset") ?? "GLBX.MDP3"),
            TradeStrategySymbolDatasets = configuration.GetSection("AppSettings:Databento:TradeStrategySymbolDatasets").Get<string[]>() ?? []
        };
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var refresh = new InstrumentDefinitionRefresh(new DatabentoInstrumentDefinitionClient(http), referenceDb.InstrumentDefinitions,
            options, TimeProvider.System, logs.CreateLogger<InstrumentDefinitionRefresh>());
        var snapshot = await refresh.RefreshAsync(cancellationToken);
        var catalog = new StoredInstrumentDefinitionSymbolCatalog(referenceDb.InstrumentDefinitions);
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
