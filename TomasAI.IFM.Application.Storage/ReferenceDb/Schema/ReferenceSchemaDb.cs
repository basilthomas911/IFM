using Microsoft.Extensions.Logging;
using TomasAI.IFM.Application.Storage.Schema;
using TomasAI.IFM.Framework.Storage;
using TomasAI.IFM.Shared.Storage;

namespace TomasAI.IFM.Application.Storage.ReferenceDb.Schema;

public sealed class ReferenceSchemaDb(IDbConnectionSettings connectionSettings, ILogger<DbProvider> logger)
    : SchemaDbContext<ReferenceSchemaDb>(connectionSettings[ReferenceDbContext.ReferenceDbConnection], logger)
{
    static readonly SchemaObjectDefinition[] Objects =
    [
        new("economic_calendar", ReferenceSchemaCql.CreateEconomicCalendarTable, "DROP TABLE IF EXISTS economic_calendar;"),
        new("lookup_type", ReferenceSchemaCql.CreateLookupTypeTable, "DROP TABLE IF EXISTS lookup_type;"),
        new("mdi_forward_loss_ratio", ReferenceSchemaCql.CreateMDIForwardLossRatioTable, "DROP TABLE IF EXISTS mdi_forward_loss_ratio;"),
        new("scheduled_job_days", ReferenceSchemaCql.CreateScheduledJobDaysTable, "DROP TABLE IF EXISTS scheduled_job_days;"),
        new("scheduled_job", ReferenceSchemaCql.CreateScheduledJobTable, "DROP TABLE IF EXISTS scheduled_job;"),
        new("seed_id", ReferenceSchemaCql.CreateSeedIdTable, "DROP TABLE IF EXISTS seed_id;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
