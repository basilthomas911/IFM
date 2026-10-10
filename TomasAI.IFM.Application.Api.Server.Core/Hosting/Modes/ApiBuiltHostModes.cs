using TomasAI.IFM.Application.Api.Server.Core.Startup.Schema;
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
    public static async Task<bool> RunBuiltHostModeAsync(WebApplication app, string[] args, Microsoft.Extensions.Logging.ILogger logger)
    {
        var bootstrapTradeStrategyFamiliesOnly = args.Contains(
        "--bootstrap-trade-strategy-families-only",
        StringComparer.OrdinalIgnoreCase);
        var migrateStrategyCatalogOnly = args.Contains("--migrate-strategy-catalog-only", StringComparer.OrdinalIgnoreCase);
        var initializeSchemaOnly = args.Contains("--initialize-schema-only", StringComparer.OrdinalIgnoreCase);
        var refreshInstrumentDefinitionsOnly = args.Contains("--refresh-instrument-definitions-only", StringComparer.OrdinalIgnoreCase);
        var verifyStartupOnly = args.Contains("--verify-startup-only", StringComparer.OrdinalIgnoreCase);
        var qualifyRecoveryInfrastructureOnly = args.Contains("--qualify-recovery-infrastructure-only", StringComparer.OrdinalIgnoreCase);
        if (verifyStartupOnly)
        {
            if (app.Configuration.GetValue<bool>("MarketDataRecovery:HardRecovery:Pipeline:Enabled"))
            _ = app.Services.GetRequiredService<IDatabentoRecoveryRequester>();
            Console.WriteLine("IFM startup verification completed; no schemas, actors, feeds or HTTP listeners started.");
            // Run the real composition root/container checks, then exit before any schema,
            // seed, HTTP listener, hosted service, actor or feed startup. This takes precedence
            // over bootstrap mode so a verification request cannot accidentally write data.
            Log.Information("IFM startup verification completed; no schemas, actors, feeds or HTTP listeners started.");
            // The executable disposes the supplied host after this mode completes.
        }
        else if (qualifyRecoveryInfrastructureOnly)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await app.Services.GetRequiredService<MarketDataSchemaDb>()
            .CreateAsync(["ifm_recovery_probe"], deadline.Token);
            var result = await app.Services.GetRequiredService<DatabentoSoftRecoveryGate>()
            .QualifyAsync(deadline.Token);
            foreach (var probe in result.Probes)
            Console.WriteLine($"{probe.Kind}/{probe.Name}: {(probe.Qualified ? "Healthy" : "Failed")} - {probe.Detail}");
            if (!result.Qualified)
            throw new InvalidOperationException("Recovery infrastructure qualification failed.");
            Console.WriteLine($"Recovery infrastructure qualified in {result.Rounds} round(s); no actors, feeds or HTTP listeners started.");
            // The executable disposes the supplied host after this mode completes.
        }
        else if (args.Contains("--initialize-iron-condor-risk-only", StringComparer.OrdinalIgnoreCase))
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.TradePlanDb.Schema.TradePlanSchemaDb>()
            .CreateAllAsync().WaitAsync(deadline.Token);
            await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb>()
            .CreateAllAsync().WaitAsync(deadline.Token);
            var policy = await app.Services.GetRequiredService<TomasAI.IFM.Domain.Trade.Futures.Option.Position.IronCondor.Plan.Model.StrategyRiskParameterSetResolver>()
            .ResolveAsync(deadline.Token);
            Log.Information("Iron Condor daily-risk schema initialized; ParameterSetId={ParameterSetId} Version={Version} Hash={Hash}; no actor or feed startup.",
            policy.ParameterSetId, policy.Version, policy.Hash());
            Console.WriteLine($"Iron Condor daily-risk schema initialized; policy {policy.ParameterSetId}/{policy.Version}, hash {policy.Hash()}.");
            // The executable disposes the supplied host after this mode completes.
        }
        else if (initializeSchemaOnly)
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            await app.Services.GetRequiredService<ApplicationSchemaInitializer>()
            .InitializeAsync(deadline.Token);
            Log.Information("Canonical schemas and catalogs initialized; no HTTP listener or actor runtime started.");
            // The executable disposes the supplied host after this mode completes.
        }
        else if (args.Contains("--backfill-risk-history-only", StringComparer.OrdinalIgnoreCase))
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(30));
            await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.TradeDb.Schema.TradeSchemaDb>().CreateAllAsync();
            var cursorText = args.SingleOrDefault(x => x.StartsWith("--risk-after=", StringComparison.OrdinalIgnoreCase))?.Split('=', 2)[1];
            long cursor = cursorText is null ? 0 : long.Parse(cursorText, System.Globalization.CultureInfo.InvariantCulture);
            if (cursor < 0) throw new ArgumentException("Risk cursor must be nonnegative.");
            var recovery = app.Services.GetRequiredService<TomasAI.IFM.Domain.Trade.Strategy.Workflow.IntrinsicTime.RiskManager.Realtime.RiskObservationRecoveryService>();
            do { cursor = await recovery.ProjectPageAsync(cursor, false, deadline.Token); Console.WriteLine($"Risk history next cursor: {cursor}"); } while (cursor != 0);
            // The executable disposes the supplied host after this mode completes.
        }
        else if (migrateStrategyCatalogOnly)
        {
            await app.Services.GetRequiredService<TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb>().CreateAllAsync();
            var migration = await app.Services.GetRequiredService<TomasAI.IFM.Domain.Reference.StrategyCatalog.StrategyCatalogMigration>().EnsureAsync(importLegacy: true);
            Console.WriteLine($"Verified catalog migration: {migration.StarterDefinitions} starter definitions, {migration.ImportedDeployments} legacy deployment imports, {migration.DeploymentsRequiringProductResolution} imports needing product resolution. No automatic publication or Fund permission changes.");
            // The executable disposes the supplied host after this mode completes.
        }
        else if (bootstrapTradeStrategyFamiliesOnly)
        {
            // Deliberately avoid HTTP binding and actor startup. This narrow process mode
            // lets deployment/startup qualification race independent initializers against
            // the same ReferenceDb and PostgreSQL sequence infrastructure.
            await app.Services.GetRequiredService<TradeStrategyFamilyBootstrapper>().EnsureV1Async();
            Log.Information("TradeStrategyFamily bootstrap-only process completed.");
        }
        else return false;
        return true;
    }
}
