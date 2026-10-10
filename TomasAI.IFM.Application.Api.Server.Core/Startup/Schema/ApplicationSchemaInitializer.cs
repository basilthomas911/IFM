using TomasAI.IFM.Application.Api.Server.Core.Startup.Actors;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;
using TomasAI.IFM.Application.Storage.EventSourceDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataDb.Schema;
using TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema;
using TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioDb.Schema;
using TomasAI.IFM.Application.Storage.PortfolioFinancial;
using TomasAI.IFM.Application.Storage.ReferenceDb.Schema;
using TomasAI.IFM.Application.Storage.ReferenceDb;
using TomasAI.IFM.Application.Storage.SecuritiesDb.Schema;
using TomasAI.IFM.Application.Storage.SequenceIdDb.Schema;
using TomasAI.IFM.Application.Storage.SystemAdminDb.Schema;
using TomasAI.IFM.Application.Storage.TradeDb.Schema;
using TomasAI.IFM.Application.Storage.TradePlanDb.Schema;
using TomasAI.IFM.Domain.Reference.StrategyCatalog;

namespace TomasAI.IFM.Application.Api.Server.Core.Startup.Schema;

/// <summary>Runs explicit deployment-time schema and catalog initialization.</summary>
public sealed class ApplicationSchemaInitializer(
    IServiceProvider services,
    StartupOrchestrationOptions options)
{
    /// <summary>Creates every canonical schema and applies idempotent catalog initialization.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var initializers = new Func<Task>[]
        {
            services.GetRequiredService<TradeSchemaDb>().CreateAllAsync,
            services.GetRequiredService<TomasAI.IFM.Application.Storage.ScheduledTaskDb.ScheduledTaskSchemaDb>().CreateAllAsync,
            services.GetRequiredService<TradePlanSchemaDb>().CreateAllAsync,
            services.GetRequiredService<PortfolioSchemaDb>().CreateAllAsync,
            services.GetRequiredService<ReferenceSchemaDb>().CreateAllAsync,
            services.GetRequiredService<SequenceIdSchemaDb>().CreateAllAsync,
            services.GetRequiredService<EventSourceSchemaDb>().CreateAllAsync,
            () => services.GetRequiredService<PortfolioFinancialSchema>()
                .InitializeAsync(cancellationToken),
            services.GetRequiredService<MarketDataSchemaDb>().CreateAllAsync,
            services.GetRequiredService<OptionPricerSchemaDb>().CreateAllAsync,
            services.GetRequiredService<MarketDataServiceSchemaDb>().CreateAllAsync,
            services.GetRequiredService<SecuritiesSchemaDb>().CreateAllAsync,
            services.GetRequiredService<SystemAdminSchemaDb>().CreateAllAsync,
            services.GetRequiredService<ConfigurationSchemaDb>().CreateAllAsync
        };
        await Parallel.ForEachAsync(
            initializers,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = options.MaximumSchemaInitializationConcurrency
            },
            async (initialize, token) =>
            {
                token.ThrowIfCancellationRequested();
                await initialize().ConfigureAwait(false);
            }).ConfigureAwait(false);
        await services.GetRequiredService<TradeStrategyFamilyBootstrapper>().EnsureV1Async();
        await services.GetRequiredService<StrategyCatalogMigration>().EnsureAsync();
    }
}
