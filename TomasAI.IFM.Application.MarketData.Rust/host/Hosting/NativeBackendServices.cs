using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
using StackExchange.Redis;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.MarketData.Contracts;
using TomasAI.IFM.Application.MarketData.Databento;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.MarketData.Databento.Workers;
using TomasAI.IFM.Application.MarketData.Pricing;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb;
using TomasAI.IFM.Application.Storage.SequenceIdDb;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;
using TomasAI.IFM.Shared.MarketData.Contracts;
using TomasAI.IFM.Shared.MarketData.Contracts.Pricing;
using TomasAI.IFM.Framework.MarketData.DataBento;
using TomasAI.IFM.Framework.MarketData.ReferenceData;
using TomasAI.IFM.Framework.SequenceId;
using TomasAI.IFM.Framework.SequenceId.Postgres;
using TomasAI.IFM.Framework.Serialization;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Domain.MarketData.Shared.ServiceApi;
using TomasAI.IFM.Application.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Databento.Historical;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Common;
using TomasAI.IFM.Shared.MarketData.Contracts.Historical;
using TomasAI.IFM.Application.MarketData.Subscriptions;
using TomasAI.IFM.Application.MarketData.Subscriptions.Persistence;

using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Application.MarketData.Rust.Api;
using TomasAI.IFM.Application.MarketData.Rust.OptionChains;
using TomasAI.IFM.Application.MarketData.Rust.Host.Seeding;
using TomasAI.IFM.Framework.MarketData.TickAggregation;
using TomasAI.IFM.Shared.MarketData.Contracts.TickAggregation;
namespace TomasAI.IFM.Application.MarketData.Rust.Host;
/// <summary>Opt-in native Host composition. The current Host registration and
/// provider binaries are unchanged. Rust owns all selected live datafeed lifecycle
/// and recovery; managed services supply cold queries and copied business facts.</summary>
public static class NativeBackendServices
{
    /// <summary>Registers the complete native backend without starting a feed.</summary>
    public static void AddNativeMarketDataBackend(this IServiceCollection services,IConfiguration config)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth>();
        var profile=Enum.Parse<FeedDeploymentProfile>(config["MarketDataHost:DeploymentProfile"]??config["AppSettings:Databento:DeploymentProfile"]??"Development",true);
        var feed=DatabentoFeedOptions.ForProfile(profile,config["AppSettings:Databento:Dataset"]??"GLBX.MDP3") with{DataSource=FeedDataSourceMode.DatabentoLive};
        if(!string.Equals(config["MarketDataHost:DataSource"]??config["AppSettings:Databento:DataSource"]??"DatabentoLive","DatabentoLive",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The native live backend does not select a synthetic/emulator provider.");
        var options=new DatabentoMarketDataRuntimeOptions{FeedOptions=feed,
            Contracts=config.GetSection("AppSettings:Databento:Contracts").Get<DatabentoContractRegistration[]>()??[],
            TradeStrategySymbolDatasets=config.GetSection("AppSettings:Databento:TradeStrategySymbolDatasets").Get<string[]>()??[],
            OptionPricingRefresh=config.GetSection("AppSettings:Databento:OptionPricingRefresh").Get<OptionPricingRefreshPolicy>()??new()};
        var registry=new DatabentoContractRegistrationRegistry(options.Contracts,options);
        services.AddSingleton<IDatabentoContractRegistrationRegistry>(registry);
        services.AddSingleton(options with{Contracts=registry});
        services.AddSingleton(new DatabentoMarketDataApiOptions());
        services.AddSingleton<IDatabentoCurrentFuturesContractResolver>(p=>new NativeCurrentFuturesContractResolver(()=>p.GetRequiredService<NativeColdBackend>().HistoricalRuntime,p.GetRequiredService<DatabentoMarketDataRuntimeOptions>()));
        services.AddSingleton<TomasAI.IFM.Domain.Trade.Shared.Recovery.ITradeFeedRecoveryStore,TomasAI.IFM.Application.Storage.TradeDb.TradeFeedRecoveryStore>();
        services.AddSingleton<IDbConnectionSettings>(_ => ApplicationStorageConnections.Create(
            config["IFM_TEST_POSTGRES_CONNECTION"] ?? config["Storage:PostgresConnection"]
                ?? config.GetConnectionString("EventSourceActorDbConnection") ?? throw new InvalidOperationException("Host PostgreSQL connection is required."),
            config.GetConnectionString("TradeTickQuoteDbConnection") ?? throw new InvalidOperationException("Raw quote database connection is required.")));
        services.AddSingleton<IDbContextResolver>(provider => new DbContextResolver(type => provider.GetRequiredService(type.GenericTypeArguments[0])));
        services.AddSingleton<IDbContextFactory, DbContextFactory>();
        services.AddSingleton<SequenceIdDbContext>();
        services.AddSingleton<ISequenceIdDbContext>(p => p.GetRequiredService<SequenceIdDbContext>());
        services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(config["Storage:RedisConnection"] ?? "localhost:6379"));
        services.AddSingleton<IRedisCache, RedisCache>();
        services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();
        services.AddSingleton<IBlackboardService, BlackboardService>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.TradeTickQuoteDb.TradeTickQuoteDbContext>();
        services.AddSingleton<MarketDataDbContext>();
        services.AddSingleton<IMarketDataDbContext>(p => p.GetRequiredService<MarketDataDbContext>());
        services.AddSingleton<SecuritiesDbContext>();
        services.AddSingleton<TomasAI.IFM.Application.Storage.TradeDb.TradeDbContext>();
        services.AddSingleton<ISecuritiesDbContext>(p => p.GetRequiredService<SecuritiesDbContext>());
        services.AddSingleton<ReferenceDbContext>();
        services.AddSingleton<IReferenceDbContext>(p => p.GetRequiredService<ReferenceDbContext>());
        services.AddSingleton<ITradeStrategySymbolStore, OptionChains.TradeStrategySymbolStore>();
        services.AddSingleton<IInstrumentDefinitionStore, OptionChains.InstrumentDefinitionStore>();
        services.AddSingleton<MarketDataServiceDbContext>();
        services.AddSingleton<IMarketDataServiceStore>(p => p.GetRequiredService<MarketDataServiceDbContext>());
        services.AddSingleton<ICurrentFuturesContractCatalog>(p => p.GetRequiredService<SecuritiesDbContext>());
        services.AddSingleton<IFuturesContractRolloverStore>(p => p.GetRequiredService<SecuritiesDbContext>());
        services.AddSingleton<IDatabentoContractAuthority, DatabentoContractAuthority>();
        services.AddSingleton<IOptionPricingConventionStore>(p => p.GetRequiredService<SecuritiesDbContext>());
        services.AddSingleton<ICompositionPreparationStore>(p => p.GetRequiredService<MarketDataDbContext>());
        services.AddSingleton<ICompositionRoutePlanStore>(p => p.GetRequiredService<MarketDataServiceDbContext>());
        services.AddSingleton<IDurableSubscriptionIntentStore>(p => p.GetRequiredService<MarketDataServiceDbContext>());
        services.AddSingleton<IOptionChainStatisticsStore>(p => p.GetRequiredService<MarketDataDbContext>());
        services.AddSingleton<IOptionTradeEvidenceWriter>(p => p.GetRequiredService<MarketDataDbContext>());
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Rust.Persistence.NativeEvidenceStorage>();
        services.AddHostedService<NativeEvidenceStorageService>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Rust.Persistence.INativeReferenceFacts,NativeReferenceFacts>();
        services.AddSingleton<TomasAI.IFM.Application.MarketData.Rust.Persistence.NativeReferenceStorage>();
        services.AddHostedService<NativeReferenceStorageService>();

        services.AddSingleton<NativeColdBackend>();
        services.AddSingleton<NativeDatasetBackend>();
        services.AddSingleton<NativeColdRolloverOperations>();
        services.AddSingleton<NativeOwnerTradeRecovery>();
        services.AddSingleton<NativeCompositionPlanPersistence>();
        services.AddSingleton<NativeOwnedOptionChainCache>(p=>new(p.GetRequiredService<NativeDatasetBackend>().Runtime));
        services.AddSingleton<IOptionChainCache>(p=>p.GetRequiredService<NativeOwnedOptionChainCache>());
        services.AddSingleton<NativeOwnerApplicationServices>();
        services.AddSingleton<IRustMarketDataApplicationServices>(p=>p.GetRequiredService<NativeOwnerApplicationServices>());
        services.AddSingleton<NativeOwnerBusinessOperations>();
        services.AddSingleton<NativeOwnerRuntimeOperations>();
        services.AddSingleton<CurrentLeaseContract>();
        services.AddSingleton<IRustMarketDataHostServices,NativeOwnerHostServices>();
        services.AddSingleton<RustMarketDataApi>(p=>new(p.GetRequiredService<IRustMarketDataApplicationServices>(),p.GetRequiredService<IRustMarketDataHostServices>()));
        services.AddSingleton<IMarketDataApi>(p=>p.GetRequiredService<RustMarketDataApi>());
        services.AddSingleton<IInstrumentDefinitionProvider>(p=>new NativeInstrumentDefinitionProvider(p.GetRequiredService<NativeColdBackend>().HistoricalRuntime,TimeSpan.FromMinutes(30)));
        services.AddTradeStrategySymbolCatalog();
        services.AddSingleton<ITreasuryCurve>(p=>new NativeTreasuryCurve(p.GetRequiredService<NativeColdBackend>().Runtime,TimeSpan.FromMinutes(2)));
        var fmp=config.GetSection("AppSettings:Fmp").Get<FinancialModelingPrepOptions>()??new();services.AddSingleton(fmp);
        services.AddSingleton<IEconomicCalendar>(p=>{var cold=p.GetRequiredService<NativeColdBackend>();cold.Runtime.ConfigureFmp(fmp,cold.Dns);return new NativeFmpReferenceData(cold.Runtime,fmp);});
        services.AddSingleton(UsTreasuryCurve.ConversionPolicy);services.AddSingleton(UsTreasuryPublicationCalendar.Default2026);
        services.AddSingleton<EuropeanOptionUniverse>();services.AddSingleton<TreasuryPricingProvider>();
        services.AddSingleton((config.GetSection("AppSettings:Databento:OptionExpiryCalendar").Get<OptionChains.OptionContractExpiryCalendarOptions>()??new()).Validate());
        services.AddSingleton<OptionChains.OptionContractExpiryCalendarRefreshService>();
        services.AddSingleton<IMarketDataHistoricalProvider>(p=>new NativeHistoricalProvider(p.GetRequiredService<NativeColdBackend>().HistoricalRuntime,TimeSpan.FromMinutes(30)));
        services.AddApplicationMarketDataHistoricalApi(new(){StagingRoot=Path.Combine(AppContext.BaseDirectory,"market-data-history"),SeriesProfiles=new[]{(Root:"ES",Rule:"calendar-front",Symbol:"ES.c.0",Dataset:"GLBX.MDP3"),(Root:"VX",Rule:"calendar-front",Symbol:"VX.c.0",Dataset:"XCBF.PITCH"),(Root:"VX",Rule:"calendar-second",Symbol:"VX.c.1",Dataset:"XCBF.PITCH")}.Select(series=>new DatabentoHistoricalSeriesProfile{Dataset=series.Dataset,MarketSeriesIdentity=MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId(series.Root,series.Rule,"unadjusted",1)).Format(),Symbols=[series.Symbol],Symbology=HistoricalSymbology.Continuous}).ToArray()});
        services.AddSingleton<IHistoricalDataLoaderStore,HistoricalDataLoaderStore>();services.AddSingleton<IHistoricalObservationStore>(p=>p.GetRequiredService<MarketDataDbContext>());
        services.AddSingleton(p=>{var cold=p.GetRequiredService<NativeColdBackend>();return new NativeSeedReplaySource(cold.HistoricalRuntime,cold.ProviderKey,cold.Dns,Endpoints(config).ToDictionary(x=>x.Lane==TomasAI.IFM.Application.MarketData.Rust.Delivery.MarketDataLane.FuturesGlbx?"GLBX.MDP3":"XCBF.PITCH",StringComparer.Ordinal));});
        services.AddSingleton<NativeFuturesSeedReplay>();
        services.AddSingleton<NativeMarketDataNatsService>();services.AddHostedService(p=>p.GetRequiredService<NativeMarketDataNatsService>());
        services.AddHostedService<NativeOwnerBackendLifetime>();
        services.AddHostedService<NativeProviderDiagnosticLogging>();
    }
    static NativeDatasetEndpoint[] Endpoints(IConfiguration config)=>[
        new(TomasAI.IFM.Application.MarketData.Rust.Delivery.MarketDataLane.FuturesGlbx,config["MarketDataHost:Rust:GlbxHost"]??"glbx-mdp3.lsg.databento.com",config.GetValue<ushort>("MarketDataHost:Rust:GlbxPort",13000)),
        new(TomasAI.IFM.Application.MarketData.Rust.Delivery.MarketDataLane.FuturesXcbf,config["MarketDataHost:Rust:XcbfHost"]??"xcbf-pitch.lsg.databento.com",config.GetValue<ushort>("MarketDataHost:Rust:XcbfPort",13000))];
}
