using TomasAI.IFM.Application.Api.Server.Core.DependencyInjection;
using TomasAI.IFM.Application.Api.Server.Core.Deployment.Identity;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Contracts;
using TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;
using TomasAI.IFM.Application.Api.Server.Core.Http.Endpoints;
using TomasAI.IFM.Application.Api.Server.Core.Recovery.Shutdown;
using Serilog;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Application.MarketData.OperationsHealth;
using TomasAI.IFM.Shared.EventModelActor.Contracts;
using TomasAI.IFM.Shared.EventModelActor;
using TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.Development;
using Microsoft.AspNetCore.OutputCaching;
using TomasAI.IFM.Domain.Supervisor.Shared.ServiceApi;
using TomasAI.IFM.Domain.Supervisor.Shared.Service.Health;
using TomasAI.IFM.Application.MarketData.Databento.Resiliency;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Framework.Messaging.NatsJetStream;
using TomasAI.IFM.Framework.Messaging.NatsJetStream.Contracts;

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting;

/// <summary>Coordinates existing API runtime stages using the built host provider.</summary>
public sealed partial class ApiServerLifecycle : IApiServerLifecycle
{
    public async Task RunAsync(WebApplication app, string[] args, Microsoft.Extensions.Logging.ILogger logger)
    {
        IApiFatalRecoveryShutdown? fatalRecoveryShutdown = app.Services.GetService<IApiFatalRecoveryShutdown>();

        try
        {
            var deploymentIdentity = app.Services.GetRequiredService<DeploymentIdentityMonitor>()
            .EnsureStartupValid();
            Log.Information("Deployment identity verified: {BuildId}", deploymentIdentity.BuildId);
            CoreServiceRegistration.ConfigureRequestPipeline(app, logger);
            ApiOperationalEndpoints.MapOperationalEndpoints(app);
            if (!await ApiServerModes.RunBuiltHostModeAsync(app, args, logger))
            await RunServerAsync(app, args, logger, fatalRecoveryShutdown);
        }
        catch (Exception ex)
        {
            ApiServerProcess.ReportFailure(ex, args, fatalRecoveryShutdown);
        }
    }
}
