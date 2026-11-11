using Microsoft.Extensions.DependencyInjection.Extensions;
using TomasAI.IFM.Domain.MarketData.Securities.Persistence;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using ISequenceIdDbContext = TomasAI.IFM.Shared.Identity.ISequenceIdDbContext;
using TomasAI.IFM.Shared.Identity;
using TomasAI.IFM.Framework.MarketData.FinancialModelingPrep;
using System.Collections.Immutable;
using TomasAI.IFM.Application.MarketData.OptionChainCache;
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

namespace TomasAI.IFM.Application.MarketData.Host;

/// <summary>Standalone backend composition. No actor runtime or API server is instantiated in this process.</summary>
public static class BackendServices
{

    extension(IServiceCollection services)
    {
        public void AddMarketDataBackend(IConfiguration config)
        {
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth>();
            var profile = Enum.Parse<FeedDeploymentProfile>(config["MarketDataHost:DeploymentProfile"]
                ?? config["AppSettings:Databento:DeploymentProfile"] ?? "Development", true);
            var feed = DatabentoFeedOptions.ForProfile(profile, config["AppSettings:Databento:Dataset"] ?? "GLBX.MDP3") with
            {
                DataSource = Enum.Parse<FeedDataSourceMode>(config["MarketDataHost:DataSource"]
                    ?? config["AppSettings:Databento:DataSource"] ?? "DatabentoLive", true)
            };
            SyntheticPersistenceIsolationGuard.Validate(feed,
                config["IFM_TEST_POSTGRES_CONNECTION"] ?? config["Storage:PostgresConnection"] ?? config.GetConnectionString("EventSourceActorDbConnection"),
                config.GetConnectionString("TradeTickQuoteDbConnection"));
            services.AddDatabentoMarketDataServices();
            services.AddApplicationMarketDataApi(new()
            {
                FeedOptions = feed,
                TradeStrategySymbolDatasets = config.GetSection("AppSettings:Databento:TradeStrategySymbolDatasets").Get<string[]>() ?? [],
                Contracts = config.GetSection("AppSettings:Databento:Contracts").Get<DatabentoContractRegistration[]>() ?? [],
                OptionPricingRefresh = config.GetSection("AppSettings:Databento:OptionPricingRefresh").Get<OptionPricingRefreshPolicy>() ?? new()
            });
            services.AddSingleton<DatasetWorkerCurrentValues>();
            if (feed.DataSource == FeedDataSourceMode.DatabentoLive) services.AddHostedService<VxBootstrapPriceService>();
            Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.RemoveAll<IDatabentoMarketDataEpochFactory>(services);
            services.AddSingleton<IDatabentoMarketDataEpochFactory, WorkerOnlyEpochFactory>();
            services.AddSingleton<DatasetWorkerAdmissionRegistry>();
            services.AddSingleton<DatasetDesiredSubscriptionRegistry>();
            services.AddSingleton<TomasAI.IFM.Domain.Trade.Shared.Recovery.ITradeFeedRecoveryStore, TomasAI.IFM.Application.Storage.TradeDb.TradeFeedRecoveryStore>();
            services.AddSingleton(provider => new MarketDataHostState(provider.GetRequiredService<DatasetWorkerCurrentValues>(),
                provider.GetRequiredService<DatasetWorkerAdmissionRegistry>())
            {
                EnabledTrades = date => provider.GetRequiredService<MarketDataApiBackend>().EnabledTrades(date),
                ProcessingHealth = provider.GetRequiredService<TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth>(),
                RequalifyOptions = async (lease, token) =>
                {
                    var builder = ImmutableArray.CreateBuilder<WorkerOptionDefinition>(lease.Options.Length);
                    foreach (var option in lease.Options)
                    {
                        var prepared = await provider.GetRequiredService<IOptionPricingContextProvider>().PrepareAsync(
                            option.Pricing.Contract, option.Pricing.Calendar, provider.GetRequiredService<TreasuryPublicationPolicy>(),
                            provider.GetRequiredService<TreasuryRateConversionPolicy>(), lease.GenerationId,
                            Black76PricingModel.EngineFor(option.Pricing.Contract), DateTimeOffset.UtcNow, token).ConfigureAwait(false);
                        // A refresh may have completed after the first frozen valuation.
                        // Retry with a fresh live valuation without admitting look-ahead.
                        if (prepared.Failure?.Code is "TreasuryStale" or "TreasuryUnavailable")
                            prepared = await provider.GetRequiredService<IOptionPricingContextProvider>().PrepareAsync(
                                option.Pricing.Contract, option.Pricing.Calendar, provider.GetRequiredService<TreasuryPublicationPolicy>(),
                                provider.GetRequiredService<TreasuryRateConversionPolicy>(), lease.GenerationId,
                                Black76PricingModel.EngineFor(option.Pricing.Contract), DateTimeOffset.UtcNow, token).ConfigureAwait(false);
                        if (prepared.Failure is { } failure)
                            throw new InvalidOperationException($"Option recovery context preparation failed: {failure.Code}: {failure.Detail}");
                        builder.Add(option with
                        {
                            Pricing = prepared.Context! with
                            {
                                MaximumQuoteAgeMilliseconds = Math.Min(prepared.Context!.MaximumQuoteAgeMilliseconds, option.Pricing.MaximumQuoteAgeMilliseconds),
                                MaximumQuoteSkewMilliseconds = Math.Min(prepared.Context.MaximumQuoteSkewMilliseconds, option.Pricing.MaximumQuoteSkewMilliseconds)
                            }
                        });
                    }
                    return lease with { Options = builder.MoveToImmutable() };
                },
                RetireOptions = async token =>
                {
                    await provider.GetRequiredService<QualifiedIndividualOptionFeeds>().RetireSessionAsync(token).ConfigureAwait(false);
                    provider.GetRequiredService<QualifiedCompositionDiscovery>().CloseSession();
                    provider.GetRequiredService<IndividualOptionRiskReader>().CloseSession();
                },
                AdoptOptions = async (leases, token) =>
                {
                    provider.GetRequiredService<QualifiedCompositionDiscovery>().AdoptRestoredLeases(leases);
                    await provider.GetRequiredService<QualifiedIndividualOptionFeeds>().AdoptRestoredLeasesAsync(leases, token).ConfigureAwait(false);
                }
            });

            services.AddSingleton<IDbConnectionSettings>(_ => ApplicationStorageConnections.Create(
                config["IFM_TEST_POSTGRES_CONNECTION"] ?? config["Storage:PostgresConnection"]
                    ?? config.GetConnectionString("EventSourceActorDbConnection") ?? throw new InvalidOperationException("Host PostgreSQL connection is required."),
                config.GetConnectionString("TradeTickQuoteDbConnection") ?? throw new InvalidOperationException("Raw quote database connection is required.")));
            services.AddSingleton<IDbContextResolver>(provider => new DbContextResolver(type => provider.GetRequiredService(type.GenericTypeArguments[0])));
            services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<SequenceIdDbContext>();
            services.AddSingleton<ISequenceIdDbContext>(p => p.GetRequiredService<SequenceIdDbContext>());
            services.AddSingleton<TomasAI.IFM.Application.Storage.SequenceIdDb.ISequenceIdDbContext>(provider =>
            (TomasAI.IFM.Application.Storage.SequenceIdDb.ISequenceIdDbContext)provider.GetRequiredService<ISequenceIdDbContext>());
        services.AddSingleton<TomasAI.IFM.Application.Storage.SequenceIdDb.ISequenceIdDbReadContext>(provider =>
            provider.GetRequiredService<TomasAI.IFM.Application.Storage.SequenceIdDb.ISequenceIdDbContext>().DbReader);
        services.AddSingleton<TomasAI.IFM.Application.Storage.SequenceIdDb.ISequenceIdDbWriteContext>(provider =>
            provider.GetRequiredService<TomasAI.IFM.Application.Storage.SequenceIdDb.ISequenceIdDbContext>().DbWriter);
        services.AddSingleton<ISequenceIdGenerator, PostgresSequenceIdGenerator>();


            services.AddSingleton<IJsonSerializer, SystemTextJsonSerializer>();

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
            services.AddSingleton<IMarketDataServiceStore>(provider =>
            {
                var context = provider.GetRequiredService<MarketDataServiceDbContext>();
                return new DatabentoMarketDataServiceStore(context, context,
                    provider.GetRequiredService<TomasAI.IFM.Shared.Identity.ISequenceIdDbContext>());
            });
            services.AddSingleton<ICurrentFuturesContractCatalog>(p => p.GetRequiredService<SecuritiesDbContext>());
            services.TryAddSingleton<IActorProducer>(p => new NatsActorProducer(
                new NatsProducerOptions { Url = config["MarketDataHost:NatsUrl"] ?? "nats://127.0.0.1:4222" },
                p.GetRequiredService<ILogger<ActorFuturesRolloverStore>>(), p.GetRequiredService<NatsConnectionManager>(),
                p.GetRequiredService<TimeProvider>()));
            services.AddSingleton<IFuturesContractRolloverStore, ActorFuturesRolloverStore>();
            services.AddSingleton(p => new FuturesDefinitionPublisher(p.GetRequiredService<ISecuritiesDbContext>().DbReader,
                p.GetRequiredService<IActorProducer>()));
            services.AddSingleton<IDatabentoContractAuthority, DatabentoContractAuthority>();
            services.AddSingleton(p => new SecuritiesOptionDefinitionPublisher(p.GetRequiredService<ISecuritiesDbContext>().DbWriter,
                p.GetRequiredService<TimeProvider>()));
            services.AddSingleton(p => new SecuritiesOptionDefinitionReader(p.GetRequiredService<ISecuritiesDbContext>().DbReader));
            services.AddSingleton(p => new SecuritiesReferenceReader(p.GetRequiredService<ISecuritiesDbContext>().DbReader));
            services.AddSingleton<IOptionPricingConventionStore>(p => p.GetRequiredService<SecuritiesReferenceReader>());
            services.AddSingleton<ICompositionPreparationStore>(p => p.GetRequiredService<MarketDataDbContext>());
            services.AddSingleton<ICompositionRoutePlanStore>(p => p.GetRequiredService<MarketDataServiceDbContext>());
            services.AddSingleton<IDurableSubscriptionIntentStore>(p => new DurableSubscriptionIntentStore(p.GetRequiredService<MarketDataServiceDbContext>()));
            services.AddSingleton<DurableSubscriptionDelivery>();
            services.AddSingleton<ICommittedOptionOwnership, HostCommittedOptionOwnership>();
            services.AddSingleton<DurableCompositionRuntime>();
            services.AddHostedService(p => p.GetRequiredService<DurableCompositionRuntime>());
            services.AddSingleton<IOptionChainStatisticsStore>(p => p.GetRequiredService<MarketDataDbContext>());
            services.AddSingleton<IOptionTradeEvidenceWriter>(p => new QualifiedOptionTradeEvidenceWriter(p.GetRequiredService<MarketDataDbContext>()));
            services.AddFinancialModelingPrepMarketData(options =>
            {
                options.Enabled = config.GetValue("AppSettings:Fmp:Enabled", true);
                options.LatestTreasuryLookbackDays = config.GetValue("AppSettings:Fmp:LatestTreasuryLookbackDays", 14);
                options.MaximumProviderWindowDays = config.GetValue("AppSettings:Fmp:MaximumProviderWindowDays", 90);
                options.MaximumRequestRangeDays = config.GetValue("AppSettings:Fmp:MaximumRequestRangeDays", 3660);
                options.MaximumConcurrentRequests = config.GetValue("AppSettings:Fmp:MaximumConcurrentRequests", 2);
            });
            services.AddSingleton((config.GetSection("AppSettings:Databento:OptionExpiryCalendar").Get<OptionChains.OptionContractExpiryCalendarOptions>() ?? new()).Validate());
            services.AddSingleton<OptionChains.OptionContractExpiryCalendarRefreshService>();
            services.AddHttpClient("USTreasury", client => client.Timeout = TimeSpan.FromSeconds(30));
            services.AddSingleton<ITreasuryCurve>(p => new UsTreasuryCurve(p.GetRequiredService<IHttpClientFactory>().CreateClient("USTreasury"), TimeProvider.System));
            services.AddSingleton(UsTreasuryCurve.ConversionPolicy);
            services.AddSingleton(UsTreasuryPublicationCalendar.Default2026);
            services.AddSingleton<EuropeanOptionUniverse>();
            services.AddSingleton<TreasuryPricingProvider>();
            // Preserve the explicitly qualified Development clock tolerance from the API composition.
            // Live provider clocks can lead this workstation; production and synthetic feeds remain strict.
            services.AddSingleton(new PricingSourceClockPolicy(
                profile == FeedDeploymentProfile.Development && feed.DataSource == FeedDataSourceMode.DatabentoLive
                && config.GetValue<bool>("MarketDataRecovery:Stage3:Enabled")
                && config.GetValue<bool>("MarketDataRecovery:Stage3:AllowDevelopmentLiveQualification") ? 2000 : 0));
            services.AddSingleton<IOptionPricingContextProvider, OptionPricingContextProvider>();
            services.AddSingleton<ICompositionMarketDataApi, HostCompositionMarketDataApi>();
            services.AddSingleton<QualifiedCompositionDiscovery>();
            services.AddSingleton<CompositionPreparationService>();
            services.AddSingleton<CompositionMarketPreparation>();
            services.AddSingleton<IndividualOptionRiskReader>();
            services.AddSingleton(p => new QualifiedIndividualOptionFeeds(
                p.GetRequiredService<QualifiedCompositionDiscovery>(), p.GetRequiredService<DatasetWorkerAdmissionRegistry>(),
                p.GetRequiredService<IDbContextFactory>(), p.GetRequiredService<TreasuryPublicationPolicy>(), p.GetRequiredService<TreasuryRateConversionPolicy>(),
                p.GetRequiredService<IndividualOptionRiskReader>(), p.GetRequiredService<Microsoft.Extensions.Logging.ILogger<QualifiedIndividualOptionFeeds>>(),
                p.GetRequiredService<TomasAI.IFM.Application.MarketData.Recovery.OptionFeedProcessingHealth>()));
            services.AddDatabentoHistoricalMarketDataServices(new() { UseSyntheticProvider = feed.DataSource == FeedDataSourceMode.Synthetic });
            services.AddApplicationMarketDataHistoricalApi(new()
            {
                StagingRoot = Path.Combine(AppContext.BaseDirectory, "market-data-history"),
                SeriesProfiles = new[] { (Root: "ES", Rule: "calendar-front", Symbol: "ES.c.0"),
                    (Root: "VX", Rule: "calendar-front", Symbol: "VX.c.0"), (Root: "VX", Rule: "calendar-second", Symbol: "VX.c.1") }
                    .Select(series => new DatabentoHistoricalSeriesProfile
                    {
                        Dataset = feed.Dataset,
                        MarketSeriesIdentity = MarketSeriesIdentity.ForFuturesSeries(new FuturesSeriesId(series.Root, series.Rule, "unadjusted", 1)).Format(),
                        Symbols = [series.Symbol],
                        Symbology = HistoricalSymbology.Continuous
                    }).ToArray()
            });
            services.AddSingleton<IHistoricalDataLoaderStore, HistoricalDataLoaderStore>();
            services.AddSingleton<IHistoricalObservationStore>(p => p.GetRequiredService<MarketDataDbContext>());
            services.AddSingleton<Seeding.FourHourDatabentoSeedReplay>();
            services.AddSingleton<OptionChains.HostOptionChainContext>();
            services.AddTradeStrategySymbolCatalog();
            services.AddStrategyOptionChainCache<OptionChains.StoredOptionUniverseSource>();
            services.AddHostedService<OptionChainPublicationRelay>();
            services.AddSingleton<MarketDataApiBackend>();
            services.AddSingleton<IMarketDataApi>(p => p.GetRequiredService<MarketDataApiBackend>());
        }
    }
}

sealed class WorkerOnlyEpochFactory : IDatabentoMarketDataEpochFactory
{
    public IDatabentoMarketDataEpoch Create(DateOnly valueDate)
        => throw new InvalidOperationException("Host epochs run exclusively in its owned dataset processes.");
}
