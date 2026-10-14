using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TomasAI.IFM.Application.Storage.EventSourceDb;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Storage.Tools;

/// <summary>Creates reviewed native context schemas only in an isolated migration staging database.</summary>
internal static class OfflineSchemaPreparation
{
    public static async Task RunAsync(string connection)
    {
        var identity = new NpgsqlConnectionStringBuilder(connection);
        if (identity.Host is not ("localhost" or "127.0.0.1") || !identity.Database.StartsWith("ifm_storage_migration_staging_", StringComparison.Ordinal))
            throw new InvalidOperationException("Schema preparation requires an isolated local migration staging database.");
        var settings = new DbConnectionSettings();
        string[] names = ["ConfigurationDbConnection","EventSourceActorDbConnection","MarketDataDbConnection",
            "MarketDataServiceDbConnection","OptionPricerDbConnection","PortfolioDbConnection","ReferenceDbConnection",
            "SecuritiesDbConnection","SequenceIdDbConnection","SystemAdminDbConnection","TradeDbConnection",
            "TradePlanDbConnection","ScheduledTaskDbConnection"];
        foreach (var name in names) settings.Add(name, connection, "System.Data.Postgres");
        var logger = NullLogger<DbProvider>.Instance;
        IDbSchemaContext[] schemas =
        [
            new TomasAI.IFM.Application.Storage.EventSourceDb.Schema.EventSourceSchemaDb(settings,logger),
            TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb.WithoutLookupDefaults(settings,logger),
            new TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema.MarketDataServiceSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.OptionPricerDb.Schema.OptionPricerSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.PortfolioDb.Schema.PortfolioSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.ReferenceDb.Schema.ReferenceSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.SecuritiesDb.Schema.SecuritiesSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.SequenceIdDb.Schema.SequenceIdSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.SystemAdminDb.Schema.SystemAdminSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.TradeDb.Schema.TradeSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.TradePlanDb.Schema.TradePlanSchemaDb(settings,logger),
            new TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema.ScheduledTaskSchemaDb(settings,logger)
        ];
        foreach (var schema in schemas) await schema.CreateAllAsync().ConfigureAwait(false);
        await new TomasAI.IFM.Domain.Portfolio.Financial.Repository.PortfolioFinancialSchema(new PostgresEventTransaction(settings))
            .InitializeAsync().ConfigureAwait(false);
        foreach (var schema in schemas) await schema.ValidateTablesAsync().ConfigureAwait(false);
    }
}
