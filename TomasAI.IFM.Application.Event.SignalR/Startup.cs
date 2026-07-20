using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
//using Serilog;
using TomasAI.IFM.Application.Event.SignalR.Services;
using TomasAI.IFM.Application.Command.RestApi.Client;
using TomasAI.IFM.Application.Command.RestApi.Client.Commands;
using TomasAI.IFM.Application.Query.RestApiClient;
using TomasAI.IFM.Application.Query.RestApiClient.Queries;
using TomasAI.IFM.Application.Domain.EventProducer;
using TomasAI.IFM.Application.Domain.Fund.EventProducers;
using TomasAI.IFM.Shared.MarketData.ServiceApi;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.MarketDataFeed;
using TomasAI.IFM.MarketDataFeed.HostedService;
using TomasAI.IFM.MarketDataFeed.InteractiveBrokers;
using TomasAI.IFM.Shared.MarketDataFeed;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Trade.ServiceApi;
using TomasAI.IFM.Shared.Fund.ServiceApi;
using TomasAI.IFM.Shared.AlgoTrader;
using TomasAI.IFM.Shared.OptionPricer.ServiceApi;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.Shared.Kafka;
using TomasAI.IFM.Shared.Blackboard;
using TomasAI.IFM.AlgoTrader;
using TomasAI.IFM.AlgoTrader.HostedService;
using TomasAI.IFM.AlgoTrader.Model;
using TomasAI.IFM.TradePosition;
using TomasAI.IFM.TradePosition.HostedService;
//using TomasAI.IFM.ErrorConsole;
//using TomasAI.IFM.ErrorConsole.HostedService;

using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.JobScheduler;
using TomasAI.IFM.Shared.TaskScheduler;
using TomasAI.IFM.SystemAdmin.Backup;
using TomasAI.IFM.Shared.SystemAdmin.ServiceApi;
using TomasAI.IFM.Storage.Azure;
using TomasAI.IFM.KafkaClient;
using TomasAI.IFM.StatusConsole;

namespace TomasAI.IFM.Application.Event.SignalR
{
    public class Startup
    {
        private Container _container = new Container();

        private readonly ILogger<Startup> _logger;

        public IConfiguration Configuration { get; }

        public Startup(ILogger<Startup> logger, IConfiguration configuration)
        {
            _logger = logger;
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ILogger>(_logger);
            services.AddSingleton<IDataCacheService, DataCacheService>();
            services.AddSingleton<IBlackboardService, BlackboardService>();
            _logger.LogInformation("IFM EventServer: configuring event server...");
            IntegrateSimpleInjector();
            RegisterEventServices(services);
            RegisterCommandApiServices(services);
            RegisterQueryApiServices(services);
            RegisterEventProducers(services);
            RegisterHostedServices(services);
            // register all query handlers...

            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });
            services.AddMvc();
            services.AddLogging();
            _logger.LogInformation("IFM EventServer: event server configuration completed successfully");
            return;

            void IntegrateSimpleInjector()
            {
                _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();
                services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
                services.EnableSimpleInjectorCrossWiring(_container);
                services.UseSimpleInjectorAspNetRequestScoping(_container);
            }

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            InitializeContainer();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }
            /*
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseMvc();
            */
            return;

            void InitializeContainer()
            {
                // register application logging...
                /*
                _container.RegisterSingleton<Serilog.ILogger>(() =>
                    new LoggerConfiguration()
                        .WriteTo.MSSqlServer("Data Source=DEV-SERVER;Initial Catalog=logdb;Integrated Security=True;MultipleActiveResultSets=True", "option_pricer_log")
                        .MinimumLevel.Information()
                        .CreateLogger()
                );
                */

                // register open generic type event handlers..
                _container.Register(typeof(IAsyncEventHandler<>), AlgoTraderAssembly.Current);

                // allow Simple Injector to resolve services from ASP.NET Core...
                _container.AutoCrossWireAspNetComponents(app);
                _container.Verify();
            }

        }

        private void RegisterEventServices(IServiceCollection services)
        {
            services.AddSingleton<IEventActionBlock, EventActionBlock>();
            services.AddSingleton<IEventHandlerResolver>(sp =>
                new EventHandlerResolver(eventType => {
                    try { return _container.GetAllInstances(eventType).ToArray(); }
                    catch { return new Object[] { _container.GetInstance(eventType) }; }
                }));
 
            services.AddSingleton<IScheduledJobTaskResolver>(sp =>
                new ScheduledJobsResolver(() => _container.GetAllInstances<IScheduledJobTask>().ToArray()));
        }

        private void RegisterCommandApiServices(IServiceCollection services)
        {
            services.AddSingleton<ICommandService>(sp => new CommandServiceRestApi(Configuration.GetValue<string>("AppSettings:CommandServerBaseUri")));
            services.AddSingleton<ITradeCommandApi, TradeCommandApi>();
            services.AddSingleton<ITradePlanCommandApi, TradePlanCommandApi>();
            services.AddSingleton<ITradePositionCommandApi, TradePositionCommandApi>();
            services.AddSingleton<IMarketDataFeedCommandApi, MarketDataFeedCommandApi>();
            services.AddSingleton<IFundCommandApi, FundCommandApi>();
        }

        private void RegisterQueryApiServices(IServiceCollection services)
        {
            services.AddSingleton<IQueryService>(sp => new QueryServiceRestApi(Configuration.GetValue<string>("AppSettings:QueryServerBaseUri")));
            services.AddSingleton<IMarketDataFeedQueryApi, MarketDataFeedQueryApi>();
            services.AddSingleton<IMarketDataQueryApi, MarketDataQueryApi>();
            services.AddSingleton<IOptionPricerQueryApi, OptionPricerQueryApi>();
            services.AddSingleton<ITradeQueryApi, TradeQueryApi>();
            services.AddSingleton<ITradePlanQueryApi, TradePlanQueryApi>();
            services.AddSingleton<IFundQueryApi, FundQueryApi>();
        }

        private void RegisterEventProducers(IServiceCollection services)
        {
            services.AddSingleton<IEventProducerOptions>(new KafkaEventProducerOptions(Configuration.GetValue<string>("AppSettings:EventProducer:BootstrapServers")));
            services.AddSingleton<IStatusConsoleServiceApi, StatusConsoleEventProducer>();
            //services.AddSingleton<IErrorConsoleServiceApi, ErrorConsoleEventProducer>();
            services.AddSingleton<IFundEventProducer, FundEventProducer>();
            services.AddSingleton<IMarketDataEventProducer, MarketDataEventProducer>();
            services.AddSingleton<IMarketDataFeedEventProducer, MarketDataFeedEventProducer>();
            services.AddSingleton<IOptionPricerEventProducer, OptionPricerEventProducer>();
            services.AddSingleton<ITradeEventProducer, TradeEventProducer>();
            services.AddSingleton<ITradePositionEventProducer, TradePositionEventProducer>();
        }

        private void RegisterHostedServices(IServiceCollection services)
        {
            services.AddSingleton<IEventConsumerOptions>(new KafkaEventConsumerOptions(null, Configuration.GetValue<string>("AppSettings:EventProducer:BootstrapServers"), true));

            // algo trader hosted service...
            services.AddSingleton<IAlgoTraderService, AlgoTraderService>();
            services.AddSingleton<IAlgoTraderEventConsumer, AlgoTraderEventConsumer>();
            services.AddHostedService<AlgoTraderHostedService>();

            // market data feed hosted service...
            services.AddSingleton<IMarketDataFeedService, MarketDataFeedService>();
            services.AddSingleton<IMarketDataApiOptions>(sp => new IBMarketDataApiOptions(
                host: Configuration.GetValue<string>("AppSettings:MarketDataFeedApi:Host"),
                port: Configuration.GetValue<int>("AppSettings:MarketDataFeedApi:Port"),
                clientId: Configuration.GetValue<int>("AppSettings:MarketDataFeedApi:ClientId")));
            services.AddSingleton<IMarketDataApi, IBMarketDataApi>();
            services.AddSingleton<IMarketDataFeedEventConsumer, MarketDataFeedEventConsumer>();
            services.AddHostedService<MarketDataFeedHostedService>();

            // trade position hosted service...
            services.AddSingleton<ITradePositionService, TradePositionService>();
            services.AddSingleton<ITradePositionEventConsumer, TradePositionEventConsumer>();
            services.AddHostedService<TradePositionHostedService>();

            // order execution hosted service...

            // error console hosted service...
            /*
            services.AddSingleton<IErrorConsoleService, ErrorConsoleService>();
            services.AddSingleton<IErrorConsoleEventConsumer, ErrorConsoleEventConsumer>();
            services.AddHostedService<ErrorConsoleHostedService>();
            */
        }

    }
}
