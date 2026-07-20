using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SimpleInjector;
using SimpleInjector.Lifestyles;
//using Serilog;
using TomasAI.IFM.Application.Event;
using TomasAI.IFM.Application.Event.SignalR.Hubs;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Application.Storage.EventQueueDb;
using TomasAI.IFM.Application.Storage.LogDb;
using TomasAI.IFM.Application.Event.SignalR.Services;
using TomasAI.IFM.Application.Command.SignalRClient;
using TomasAI.IFM.Application.Command.SignalRClient.Commands;
using TomasAI.IFM.Shared.Storage;
using TomasAI.IFM.MarketDataFeed.SignalRClient;
using TomasAI.IFM.Shared.MarketDataFeed.ServiceApi;
using TomasAI.IFM.StatusConsole.SignalRClient;
using TomasAI.IFM.Shared.Log.ServiceApi;
using TomasAI.IFM.Shared.Trade.WebApi;
using TomasAI.IFM.Shared.Caching;
using TomasAI.IFM.TradeDataFeed.SignalRClient;
using TomasAI.IFM.AutoTraderService.SignalRClient;
using TomasAI.IFM.Shared.EventSourcing;
using TomasAI.IFM.System.JobScheduler;
using TomasAI.IFM.System.ScheduledJobs;
using TomasAI.IFM.Shared.TaskScheduler;

namespace TomasAI.IFM.Application.Event.SignalR
{
    public class Startup
    {
        private Container _container = new Container();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            IntegrateSimpleInjector();
            services.AddSingleton<IDbConnectionSettings>(sp =>
              new DbConnectionSettings()
                .Add("TradeDbConnection", Configuration.GetConnectionString("TradeDbConnection"), "System.Data.SqlClient")
                .Add("FundDbConnection", Configuration.GetConnectionString("FundDbConnection"), "System.Data.SqlClient")
                .Add("ReferenceDbConnection", Configuration.GetConnectionString("ReferenceDbConnection"), "System.Data.SqlClient")
                .Add("OptionPricerDbConnection", Configuration.GetConnectionString("OptionPricerDbConnection"), "System.Data.SqlClient")
                .Add("MarketDataDbConnection", Configuration.GetConnectionString("MarketDataDbConnection"), "System.Data.SqlClient")
                .Add("EventQueueDbConnection", Configuration.GetConnectionString("EventQueueDbConnection"), "System.Data.SqlClient")
                .Add("LogDbConnection", Configuration.GetConnectionString("LogDbConnection"), "System.Data.SqlClient")
            );

            services.AddSingleton<IStorageUrlSettings>(sp =>
                new StorageUrlSettings()
                    .Add("DomainData", Configuration.GetValue<string>("AppSettings:DomainDataStorageBaseUri"))
                    .Add("QueryData", Configuration.GetValue<string>("AppSettings:QueryDataStorageBaseUri"))
            );

            services.AddTransient<IEventQueueDbContext, EventQueueDbContext>();
            services.AddTransient<ILogDbContext, LogDbContext>();
            services.AddSingleton<IEventActionBlock, EventActionBlock>();
            services.AddSingleton<IEventHandlerResolver>(sp =>
                new EventHandlerResolver(eventType => {
                    try { return _container.GetAllInstances(eventType).ToArray(); }
                    catch { return new Object[] { _container.GetInstance(eventType) }; }
                }));

            services.AddSingleton<IScheduledJobTaskResolver>(sp =>
                new ScheduledJobsResolver(() => _container.GetAllInstances<IScheduledJobTask>().ToArray()));

            // register all query handlers...
            services.AddSingleton<ICommandService>((sp) => new SignalRCommandService(Configuration.GetValue<string>("AppSettings:CommandHubBaseUri")));
            services.AddSingleton<IMemoryCache, MemoryCacheManager>();
            services.AddSingleton<ISpreadTradeCommandApi, SpreadTradeCommandApi>();
            services.AddSingleton<ITradePlanCommandApi, TradePlanCommandApi>();
            services.AddSingleton<IAutoTraderServiceApi, SignalRAutoTraderServiceApi>();
            services.AddSingleton<IAutoTraderServiceApiOptions>((sp) => new AutoTraderServiceApiOptions(Configuration.GetValue<string>("AppSettings:AutoTraderHubBaseUri")));
            services.AddSingleton<IMarketDataFeedServiceApi, SignalRMarketDataFeedServiceApi>();
            services.AddSingleton<IMarketDataFeedServiceApiOptions>((sp) => new MarketDataFeedServiceApiOptions(Configuration.GetValue<string>("AppSettings:MarketDataFeedHubBaseUri")));
            services.AddSingleton<IStatusConsoleServiceApi, SignalRStatusConsoleServiceApi>();
            services.AddSingleton<IStatusConsoleServiceApiOptions>((sp) => new StatusConsoleServiceApiOptions(Configuration.GetValue<string>("AppSettings:StatusConsoleHubBaseUri")));
            services.AddSingleton<ITradeDataFeedServiceApi, SignalRTradeDataFeedServiceApi>();
            services.AddSingleton<ITradeDataFeedServiceApiOptions>((sp) => new TradeDataFeedServiceApiOptions(Configuration.GetValue<string>("AppSettings:TradeDataFeedHubBaseUri")));
            services.Configure<CookiePolicyOptions>(options =>
            {
                // This lambda determines whether user consent for non-essential cookies is needed for a given request.
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.None;
            });

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .AddJsonOptions(e => e.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);

            services
                .AddSignalR(e => e.EnableDetailedErrors = true)
                .AddMessagePackProtocol(o => {
                    o.FormatterResolvers = new List<MessagePack.IFormatterResolver>() {
                        MessagePack.Resolvers.StandardResolver.Instance
                    };
                });
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
            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();
            app.UseWebSockets();
            app.UseSignalR(routes => {
                routes.MapHub<EventHub>("/eventHub");
                routes.MapHub<OptionPricerHub>("/optionPricerHub");
                routes.MapHub<MarketDataFeedHub>("/marketDataFeedHub");
                routes.MapHub<StatusConsoleHub>("/statusConsoleHub");
                routes.MapHub<TradeDataFeedHub>("/tradeDataFeedHub");
                routes.MapHub<AutoTraderHub>("/autoTraderHub");
            });
            app.UseMvc();
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

                // register database connections...
                _container.Register<ITradeDbContext, TradeDbContext>();
                _container.Register<IFundDbContext, FundDbContext>();
                _container.Register<IReferenceDbContext, ReferenceDbContext>();
                _container.Register<IOptionPricerDbContext, OptionPricerDbContext>();
                _container.Register<IMarketDataDbContext, MarketDataDbContext>();
                _container.Register<IEventQueueDbContext, EventQueueDbContext>();
                _container.Register<ILogDbContext, LogDbContext>();
                _container.Register(typeof(IAsyncEventHandler<>), EventAssembly.Current);
                _container.Collection.Register(typeof(IScheduledJobTask), ScheduledJobsAssembly.Current);

                // allow Simple Injector to resolve services from ASP.NET Core...
                _container.AutoCrossWireAspNetComponents(app);
                _container.Verify();
            }

        }

        
    }
}
