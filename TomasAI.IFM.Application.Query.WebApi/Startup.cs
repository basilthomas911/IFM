using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using SimpleInjector.Integration.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Swagger;
using Serilog;
using TomasAI.IFM.Application.Services;
using TomasAI.IFM.Application.Storage.TradeDb;
using TomasAI.IFM.Application.Storage.FundDb;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.OptionPricerDb;
using TomasAI.IFM.Application.Storage.MarketDataDb;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Query.WebApi
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
            services.AddMvc();
            // Register the Swagger generator, defining 1 or more Swagger documents
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "TomasAI.IFM.Application.Query.WebApi", Version = "v1" });
            });
            IntegrateSimpleInjector(services);

        }

        private void IntegrateSimpleInjector(IServiceCollection services)
        {
            _container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<IControllerActivator>(
                new SimpleInjectorControllerActivator(_container));
            services.AddSingleton<IViewComponentActivator>(
                new SimpleInjectorViewComponentActivator(_container));

            services.EnableSimpleInjectorCrossWiring(_container);
            services.UseSimpleInjectorAspNetRequestScoping(_container);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            InitializeContainer(app);

            // Add custom middleware
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.), 
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                c.RoutePrefix = string.Empty;
            });
            app.UseMvc();
        }

        private void InitializeContainer(IApplicationBuilder app)
        {
            // register application logging...
            _container.RegisterSingleton<Serilog.ILogger>(() =>
                new LoggerConfiguration()
                    .WriteTo.MSSqlServer("Data Source=DEV-SERVER;Initial Catalog=logdb;Integrated Security=True;MultipleActiveResultSets=True", "option_pricer_log")
                    .MinimumLevel.Information()
                    .CreateLogger()
            );

            // register database connections...
            _container.RegisterSingleton<IDbConnectionSettings>(() =>
              new DbConnectionSettings()
                .Add("TradeDbConnection", "Data Source=DEV-SERVER;Initial Catalog=tradedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                .Add("FundDbConnection", "Data Source=DEV-SERVER;Initial Catalog=funddb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                .Add("ReferenceDbConnection", "Data Source=DEV-SERVER;Initial Catalog=referencedb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                .Add("OptionPricerDbConnection", "Data Source=DEV-SERVER;Initial Catalog=optionpricerdb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
                .Add("MarketDataDbConnection", "Data Source=DEV-SERVER;Initial Catalog=marketdatadb;Integrated Security=True;MultipleActiveResultSets=True", "System.Data.SqlClient")
            );
            _container.Register<ITradeDbContext, TradeDbContext>();
            _container.Register<IFundDbContext, FundDbContext>();
            _container.Register<IReferenceDbContext, ReferenceDbContext>();
            _container.Register<IOptionPricerDbContext, OptionPricerDbContext>();
            _container.Register<IMarketDataDbContext, MarketDataDbContext>();

            // register all query handlers...
            _container.Register<IQueryHandlerResolver>(() => new QueryHandlerResolver(qryType => _container.GetInstance(qryType)));
            _container.Register(typeof(IAsyncQueryHandler<,>), typeof(IAsyncQueryHandler<,>).Assembly);
            _container.Register<IQueryService, QueryService>();

            // allow Simple Injector to resolve services from ASP.NET Core...
            _container.AutoCrossWireAspNetComponents(app);
            _container.Verify();
        }
    }
}
