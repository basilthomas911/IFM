using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using StackExchange.Redis;
//using Serilog;
using TomasAI.IFM.Application.Blackboard;
using TomasAI.IFM.Application.Storage;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Application.Storage.YieldCurveRatesDb;
using TomasAI.IFM.Application.Storage.EconomicCalendarsDb;
using TomasAI.IFM.Application.Query.Services;
using TomasAI.IFM.Domain.Fund;
using TomasAI.IFM.Domain.Fund.Transaction;
using TomasAI.IFM.Domain.MarketData.FuturesContract;
using TomasAI.IFM.Domain.MarketData.FuturesOptionContract;
using TomasAI.IFM.Domain.MarketData.YieldCurveRate;
using TomasAI.IFM.Domain.MarketData.Feed;
using TomasAI.IFM.Domain.OptionPricer.SpreadDistribution;
using TomasAI.IFM.Domain.Reference;
using TomasAI.IFM.Domain.SystemAdmin;
using TomasAI.IFM.Domain.Trade;
using TomasAI.IFM.Domain.Trade.Option;
using TomasAI.IFM.Domain.Trade.Order;
using TomasAI.IFM.Domain.Trade.Plan;

using TomasAI.IFM.Framework.Messaging.Kafka;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Framework.Storage.Azure;
using TomasAI.IFM.Framework.Caching;
using TomasAI.IFM.Framework.Caching.Redis;

using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.Shared.Extensions;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Reference.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Log.Model;
using TomasAI.IFM.Shared.EventProducers;
using TomasAI.IFM.Service.MarketDataFeed.InteractiveBrokers;
using TomasAI.IFM.Service.SystemAdmin.Backup;
using TomasAI.IFM.Service.SystemAdmin.HostedService;

namespace TomasAI.IFM.Application.Query.Server
{
    public class Startup
    {
        private Container _container = new Container();

        public IConfiguration Configuration { get; }

        protected Microsoft.Extensions.Logging.ILogger Logger { get; set; }

        private void CreateLogger(IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>() as Microsoft.Extensions.Logging.ILogger;
            services.AddSingleton<Microsoft.Extensions.Logging.ILogger>(logger);
            Logger = logger;
        }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            CreateLogger(services);
            Logger.LogInformationEvent("QueryServer", "configuring server...");

            RegisterBaseServices(services);
            RegisterDatabaseContexts(services);
            RegisterQueryHandlers(services);
            RegisterEventConsumers(services);
            RegisterEventProducers(services);
            RegisterHostedServices(services);

            Logger.LogInformationEvent("QueryServer", "server configured successfully");
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
                
            });
            services.AddSimpleInjector(_container);
            return;
        }

        void RegisterBaseServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<IDbCache, DbCache>();
            services.AddSingleton<IDataCacheService, LocalDataCacheService>();
            services.AddSingleton<IMarketDataSnapshotApiOptions>(sp => new IBMarketDataSnapshotApiOptions(
                host: Configuration.GetValue<string>("AppSettings:MarketDataFeedSnapshotApi:Host"),
                port: Configuration.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:Port"),
                clientId: Configuration.GetValue<int>("AppSettings:MarketDataFeedSnapshotApi:ClientId")));
            services.AddSingleton<IMarketDataSnapshotApi, IBMarketDataSnapshotApi>();
            services.AddSingleton<IStatusConsoleWriter, StatusConsoleWriter>();
            var redisUri = Configuration.GetValue<string>("AppSettings:RedisUri");
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUri));
            services.AddSingleton<IRedisCache, RedisCache>();
            services.AddSingleton<IBlackboardService, BlackboardService>();
        }

        private void RegisterDatabaseContexts(IServiceCollection services)
        {
            services.AddSingleton(_ =>
              new DbConnectionSettings()
                .Add("EventDbConnection", Configuration.GetConnectionString("EventDbConnection"), "System.Data.SqlClient")
                .Add("TradeDbConnection", Configuration.GetConnectionString("TradeDbConnection"), "System.Data.SqlClient")
                .Add("FundDbConnection", Configuration.GetConnectionString("FundDbConnection"), "System.Data.SqlClient")
                .Add("ReferenceDbConnection", Configuration.GetConnectionString("ReferenceDbConnection"), "System.Data.SqlClient")
                .Add("OptionPricerDbConnection", Configuration.GetConnectionString("OptionPricerDbConnection"), "System.Data.SqlClient")
                .Add("MarketDataDbConnection", Configuration.GetConnectionString("MarketDataDbConnection"), "System.Data.SqlClient")
                .Add("LogDbConnection", Configuration.GetConnectionString("LogDbConnection"), "System.Data.SqlClient")
                .Add("YieldCurveRatesDbConnection", Configuration.GetConnectionString("YieldCurveRatesDbConnection"), "TomasAI.IFM.Storage")
                .Add("EconomicCalendarsDbConnection", Configuration.GetConnectionString("EconomicCalendarsDbConnection"), "TomasAI.IFM.Storage")
            );
            services.AddSingleton<IDbContextResolver>(_ => new DbContextResolver(e => GetContainerInstance(e)));
            services.AddSingleton<IDbContextFactory, DbContextFactory>();
            services.AddSingleton<ITradeDbContext, TradeDbContext>();
            services.AddSingleton<IFundDbContext, FundDbContext>();
            services.AddSingleton<IReferenceDbContext, ReferenceDbContext>();
            services.AddSingleton<IOptionPricerDbContext, OptionPricerDbContext>();
            services.AddSingleton<IMarketDataDbContext, MarketDataDbContext>();
            services.AddSingleton<ILogDbContext, LogDbContext>();
            services.AddSingleton<IYieldCurveRatesDbContext, YieldCurveRatesDbContext>();
            services.AddSingleton<IEconomicCalendarsDbContext, EconomicCalendarsDbContext>();
        }

        private void RegisterQueryHandlers(IServiceCollection services)
        {
            services.AddSingleton<IQueryHandlerResolver>(_ => new QueryHandlerResolver(handlerType => GetContainerInstance(handlerType)));
            services.AddSingleton<IQueryService, QueryService>();
        }
     
        private void RegisterEventConsumers(IServiceCollection services)
        { 
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(
                groupId: null,
                bootstrapServers: Configuration.GetValue<string>("AppSettings:EventConsumer:BootstrapServers"),
                enableAutoCommit: true));
        }

        private void RegisterEventProducers(IServiceCollection services)
        {
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(Configuration.GetValue<string>("AppSettings:EventProducer:BootstrapServers")));
            services.AddSingleton<IFundEventProducer, FundEventProducer>();
            services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
            services.AddSingleton<IMarketDataFeedEventProducer, MarketDataFeedEventProducer>();
            services.AddSingleton<IOptionPricerEventProducer, OptionPricerEventProducer>();
            services.AddSingleton<IReferenceEventProducer, ReferenceEventProducer>();
            services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
            services.AddSingleton<IStatusConsoleEventProducer, StatusConsoleEventProducer>();
        }

        private void RegisterHostedServices(IServiceCollection services)
        {
            // system admin hosted service...
            services.AddSingleton<ISystemAdminEventProducer, SystemAdminEventProducer>();
            services.AddSingleton<IAzureStorageOptions>(sp => Configuration.GetSection("AzureStorage").Get<AzureStorageOptions>());
            services.AddSingleton<IAzureStorage, AzureStorage>();
            services.AddSingleton<IDatabaseBackupService, QueryDatabaseBackupService>();
            services.AddSingleton<IDatabaseBackupEventConsumer, DatabaseBackupEventConsumer>();
            services.AddHostedService<SystemAdminHostedService>();
        }

            private object GetContainerInstance(Type handlerType)
        {
            var containerInstance = default(object);
            try
            {
                containerInstance = _container.GetInstance(handlerType);
            }
            catch(Exception ex)
            {
                containerInstance = null;
            }
            return containerInstance;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            InitializeContainer(app);
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void InitializeContainer(IApplicationBuilder app)
        {
            // register open generic handlers...
            var assemblies = new List<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
            assemblies.AddRange(new Assembly[] {
                FundDomainAssembly.Current,
                FundTransactionDomainAssembly.Current,
                FuturesContractDomainAssembly.Current,
                FuturesOptionContractDomainAssembly.Current,
                YieldCurveRateDomainAssembly.Current,
                MarketDataFeedDomainAssembly.Current,
                SpreadDistributionDomainAssembly.Current,
                ReferenceDomainAssembly.Current,
                SystemAdminDomainAssembly.Current,
                TradeDomainAssembly.Current,
                OptionTradeDomainAssembly.Current,
                TradeOrderDomainAssembly.Current,
                TradePlanDomainAssembly.Current
            });
            _container.Register(typeof(IObjectRepository<>), assemblies, Lifestyle.Transient);
            _container.Register(typeof(IAsyncQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IQueryHandler<,>), assemblies, Lifestyle.Singleton);
            _container.Register(typeof(IAsyncEventHandler<>), assemblies, Lifestyle.Singleton);

            // allow Simple Injector to resolve services from ASP.NET Core...
            app.ApplicationServices.UseSimpleInjector(_container);
            _container.Verify();
        }
    }
}
