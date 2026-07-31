using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.OptionPricerDb.Schema;

public sealed class OptionPricerSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<OptionPricerSchemaDb>(connectionSettings[OptionPricerDbContext.ConnectionName], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("option_pricer_device", OptionPricerSchemaCql.CreateOptionPricerDeviceTable, "DROP TABLE IF EXISTS option_pricer_device;"),
        new("spread_distribution_job", OptionPricerSchemaCql.CreateSpreadDistributionJobTable, "DROP TABLE IF EXISTS spread_distribution_job;"),
        new("spread_distribution", OptionPricerSchemaCql.CreateSpreadDistributionTable, "DROP TABLE IF EXISTS spread_distribution;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
