using TomasAI.IFM.Application.Api.Server.Core.Development.Verification;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.InstrumentDefinitions;
using TomasAI.IFM.Application.Api.Server.Core.MarketData.OptionPricing;
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

namespace TomasAI.IFM.Application.Api.Server.Core.Hosting.Modes;

public static partial class ApiServerModes
{
    public static async Task<bool> RunBeforeBuildAsync(WebApplicationBuilder builder, string[] args)
    {
        var verifyStartupOnly = args.Contains("--verify-startup-only", StringComparer.OrdinalIgnoreCase);
        var refreshInstrumentDefinitionsOnly = args.Contains("--refresh-instrument-definitions-only", StringComparer.OrdinalIgnoreCase);
        if (args.Contains("--publish-oct1-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase) && !verifyStartupOnly)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await Oct1OptionPricingReferenceMaintenance.RunAsync(builder.Configuration, deadline.Token);
            return true;
        }
        if (args.Contains("--publish-option-pricing-reference-only", StringComparer.OrdinalIgnoreCase) && !verifyStartupOnly)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            await OptionPricingReferenceMaintenance.RunAsync(builder.Configuration, deadline.Token);
            return true;
        }
        if (refreshInstrumentDefinitionsOnly && !verifyStartupOnly)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
            await InstrumentDefinitionMaintenance.RunAsync(builder.Configuration, cancellation.Token);
            return true;
        }

        return false;
    }
}
