using System.Text.Json;
namespace TomasAI.IFM.Storage.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using TomasAI.IFM.Application.Storage.SchemaDb;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

/// <summary>Exports resolved schema metadata without opening a database or running migrations.</summary>
internal static class SchemaInventory
{
    static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    internal static string Export()
    {
        var settings = new DbConnectionSettings();
        string[] names = ["ConfigurationDbConnection","EventSourceActorDbConnection","MarketDataDbConnection",
            "MarketDataServiceDbConnection","OptionPricerDbConnection","PortfolioDbConnection","ReferenceDbConnection",
            "SecuritiesDbConnection","SequenceIdDbConnection","SystemAdminDbConnection","TradeDbConnection",
            "TradePlanDbConnection","ScheduledTaskDbConnection"];
        foreach (var name in names)
            settings.Add(name,"Host=localhost;Database=metadata_only;Username=metadata_only","System.Data.Postgres");
        settings.Add("TradeTickQuoteDbConnection","Contact Points=127.0.0.1;Default Keyspace=metadata_only","System.Data.ScyllaDb");
        var logger = NullLogger<DbProvider>.Instance;
        (string Name, IDbSchemaContext Schema)[] contexts =
        [
            ("ConfigurationDb",new TomasAI.IFM.Application.Storage.ConfigurationDb.Schema.ConfigurationSchemaDb(settings,logger)),
            ("EventSourceDb",new TomasAI.IFM.Application.Storage.EventSourceDb.Schema.EventSourceSchemaDb(settings,logger)),
            ("MarketDataDb",new TomasAI.IFM.Application.Storage.MarketDataDb.Schema.MarketDataSchemaDb(settings,logger)),
            ("MarketDataServiceDb",new TomasAI.IFM.Application.Storage.MarketDataServiceDb.Schema.MarketDataServiceSchemaDb(settings,logger)),
            ("OptionPricerDb",new TomasAI.IFM.Application.Storage.OptionPricerDb.Schema.OptionPricerSchemaDb(settings,logger)),
            ("PortfolioDb",new TomasAI.IFM.Application.Storage.PortfolioDb.Schema.PortfolioSchemaDb(settings,logger)),
            ("ReferenceDb",new TomasAI.IFM.Application.Storage.ReferenceDb.Schema.ReferenceSchemaDb(settings,logger)),
            ("SecuritiesDb",new TomasAI.IFM.Application.Storage.SecuritiesDb.Schema.SecuritiesSchemaDb(settings,logger)),
            ("SequenceIdDb",new TomasAI.IFM.Application.Storage.SequenceIdDb.Schema.SequenceIdSchemaDb(settings,logger)),
            ("SystemAdminDb",new TomasAI.IFM.Application.Storage.SystemAdminDb.Schema.SystemAdminSchemaDb(settings,logger)),
            ("TradeDb",new TomasAI.IFM.Application.Storage.TradeDb.Schema.TradeSchemaDb(settings,logger)),
            ("TradePlanDb",new TomasAI.IFM.Application.Storage.TradePlanDb.Schema.TradePlanSchemaDb(settings,logger)),
            ("ScheduledTaskDb",new TomasAI.IFM.Application.Storage.ScheduledTaskDb.Schema.ScheduledTaskSchemaDb(settings,logger)),
            ("TradeTickQuoteDb",new TomasAI.IFM.Application.Storage.TradeTickQuoteDb.Schema.TradeTickQuoteSchemaDb(settings,logger))
        ];
        var financialTables = System.Text.RegularExpressions.Regex.Matches(
            TomasAI.IFM.Application.Storage.PortfolioDb.Schema.PortfolioSchemaSql.Financial,
            @"\bCREATE\s+TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?<table>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)\s*\(",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.NonBacktracking)
            .Select(match => match.Groups["table"].Value).Distinct(StringComparer.Ordinal);
        var rows = contexts.SelectMany(context => context.Schema.ManagedTables.Select(table => new
        {
            context = context.Name, table,
            schemaSource = "TomasAI.IFM.Application.Storage/" + context.Name + "/Schema",
            target = string.Equals(context.Name,"TradeTickQuoteDb",StringComparison.Ordinal) ? "ScyllaDB" : "PostgreSQL",
            verifiedRuntimeSchema = false
        })).Concat(financialTables.Select(table => new
        {
            context = "PortfolioFinancial", table,
            schemaSource = "TomasAI.IFM.Application.Storage/PortfolioDb/Financial/PortfolioFinancialSchema.cs",
            target = "PostgreSQL", verifiedRuntimeSchema = false
        }));
        return JsonSerializer.Serialize(new
        {
            status = "Resolved checked-in schema inventory; Development runtime reconciliation pending",
            tables = rows.OrderBy(row => row.context,StringComparer.Ordinal).ThenBy(row => row.table,StringComparer.Ordinal)
        },Options);
    }
}
