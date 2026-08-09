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
        new("economic_calendar_by_country_month_v2", ReferenceSchemaCql.CreateEconomicCalendarByCountryMonthV2Table, "DROP TABLE IF EXISTS economic_calendar_by_country_month_v2;"),
        new("reference_projection_state_v3", ReferenceSchemaCql.CreateReferenceProjectionStateV3Table, "DROP TABLE IF EXISTS reference_projection_state_v3;"),
        new("reference_projection_mutation_v3", ReferenceSchemaCql.CreateReferenceProjectionMutationV3Table, "DROP TABLE IF EXISTS reference_projection_mutation_v3;"),
        new("reference_projection_ownership_v3", ReferenceSchemaCql.CreateReferenceProjectionOwnershipV3Table, "DROP TABLE IF EXISTS reference_projection_ownership_v3;"),
        new("lookup_type", ReferenceSchemaCql.CreateLookupTypeTable, "DROP TABLE IF EXISTS lookup_type;"),
        new("mdi_forward_loss_ratio", ReferenceSchemaCql.CreateMDIForwardLossRatioTable, "DROP TABLE IF EXISTS mdi_forward_loss_ratio;"),
        new("scheduled_job_days", ReferenceSchemaCql.CreateScheduledJobDaysTable, "DROP TABLE IF EXISTS scheduled_job_days;"),
        new("scheduled_job", ReferenceSchemaCql.CreateScheduledJobTable, "DROP TABLE IF EXISTS scheduled_job;"),
        new("scheduled_job_by_name_v3", ReferenceSchemaCql.CreateScheduledJobByNameV3Table, "DROP TABLE IF EXISTS scheduled_job_by_name_v3;"),
        new("scheduled_job_write_ownership_v3", ReferenceSchemaCql.CreateScheduledJobWriteOwnershipV3Table, "DROP TABLE IF EXISTS scheduled_job_write_ownership_v3;")
    ];

    protected override IReadOnlyList<SchemaObjectDefinition> Definitions => Objects;
}
