using TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
using TomasAI.IFM.Application.Api.Server.Core.Hosting;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Contracts;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
namespace TomasAI.IFM.Application.Api.Server;

/// <summary>Executable composition facade for registered Core host services.</summary>
public static class Startup
{
    public static void ConfigureRunMode(WebApplicationBuilder builder, string[] args) => EventLogQualification.Configure(builder, args);

    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder ConfigureApiServer(out ILogger logger)
            => CoreServiceRegistration.ConfigureApiServer(builder, out logger);
    }

    extension(IServiceCollection services)
    {
        public IServiceCollection RegisterServices(ConfigurationManager config, ILogger logger, IHostEnvironment? hostEnvironment = null)
        {
            CoreServiceRegistration.RegisterServices(services, config, logger, hostEnvironment);
            services.AddSingleton<IApiServerLifecycle, ApiServerLifecycle>();
            return services;
        }
    }
}
