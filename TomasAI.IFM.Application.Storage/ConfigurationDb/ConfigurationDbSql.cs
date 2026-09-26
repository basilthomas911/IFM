using TomasAI.IFM.Domain.Strategy.Contracts.Shared.Configuration;
using TomasAI.IFM.Domain.Reference.Shared.StrategyCatalog;
using TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

internal static class ConfigurationDbSql
{
    const string CatalogPrefix = "reference_configuration.";
    public const string CatalogWriteLock = "SELECT pg_advisory_xact_lock($1);";
    public const string GetLatestStrategyCatalogVersion = "SELECT COALESCE(MAX(version),0) FROM reference_configuration.strategy_catalog_version WHERE kind=$1 AND id=$2;";
    public const string GetStrategyCatalogIdentityCode = "SELECT code FROM reference_configuration.strategy_catalog_identity WHERE kind=$1 AND id=$2;";
    public const string InsertStrategyCatalogIdentity = "INSERT INTO reference_configuration.strategy_catalog_identity(kind,id,code,created_utc,created_by) VALUES($1,$2,$3,$4,$5) ON CONFLICT(kind,id) DO NOTHING;";
    public const string InsertStrategyCatalogVersion = """
WITH input AS (SELECT $1::jsonb d)
INSERT INTO reference_configuration.strategy_catalog_version(kind,id,version,schema_version,name,description,
 parent_kind,parent_id,parent_version,horizon,side,bias,premium_mode,settings_json,content_sha256,created_utc,created_by)
SELECT (d->'Key'->>'Kind')::smallint,(d->'Key'->>'Id')::uuid,(d->'Key'->>'Version')::integer,
 (d->>'SchemaVersion')::smallint,d->>'Name',d->>'Description',
 (d->'Parent'->>'Kind')::smallint,(d->'Parent'->>'Id')::uuid,(d->'Parent'->>'Version')::integer,
 (d->>'Horizon')::smallint,d->>'Side',d->>'Bias',d->>'PremiumMode',d->'Settings',$2,$3,$4 FROM input;
""";
    public const string SealStrategyCatalogVersion = "UPDATE reference_configuration.strategy_catalog_version SET content_sealed=true WHERE kind=$1 AND id=$2 AND version=$3;";
    public const string GetStrategyCatalogs = """
SELECT v.id,v.version,i.code,v.name,v.status,v.content_sha256
FROM reference_configuration.strategy_catalog_identity i
CROSS JOIN LATERAL (SELECT * FROM reference_configuration.strategy_catalog_version v
 WHERE v.kind=i.kind AND v.id=i.id ORDER BY version DESC LIMIT 1) v
WHERE i.kind=$1 AND i.code COLLATE "C">$2 COLLATE "C" ORDER BY i.code COLLATE "C" LIMIT $3;
""";
    public const string PublishStrategyCatalog = "UPDATE reference_configuration.strategy_catalog_version SET status=1,effective_from_utc=$4,published_by=$5 WHERE kind=$1 AND id=$2 AND version=$3 AND status=0 AND content_sha256=$6;";
    public const string RetireStrategyCatalog = "UPDATE reference_configuration.strategy_catalog_version SET status=2,retired_at_utc=$4,retired_by=$5 WHERE kind=$1 AND id=$2 AND version=$3;";
    public const string AcquireNamedAdvisoryLock = "SELECT pg_advisory_xact_lock(hashtextextended($1,0));";
    public const string GetAssignmentOperationReceipt = "SELECT request_sha256 FROM reference_configuration.parameter_assignment_revision WHERE operation_id=$1;";
    public const string GetAssignmentRevision = "SELECT revision FROM reference_configuration.parameter_assignment WHERE assignment_id=$1;";
    public const string UpsertParameterAssignment = "INSERT INTO reference_configuration.parameter_assignment(assignment_id,revision,body) VALUES($1,$2,$3::jsonb) ON CONFLICT(assignment_id) DO UPDATE SET revision=EXCLUDED.revision,body=EXCLUDED.body;";
    public const string InsertParameterAssignmentRevision = "INSERT INTO reference_configuration.parameter_assignment_revision(assignment_id,revision,operation_id,request_sha256,body) VALUES($1,$2,$3,$4,$5::jsonb);";
    public const string InsertParameterSetAudit = "INSERT INTO reference_configuration.parameter_set_audit(operation_id,entity_id,revision,body) VALUES($1,$2,$3,$4::jsonb);";
    public const string GetParameterOperationReceipt = "SELECT request_sha256 FROM reference_configuration.parameter_operation WHERE operation_id=$1;";
    public const string GetParameterSetRevision = "SELECT revision FROM reference_configuration.parameter_set WHERE set_id=$1;";
    public const string UpsertParameterSet = "INSERT INTO reference_configuration.parameter_set(set_id,component_code,name,description,revision) VALUES($1,$2,$3,$4,$5) ON CONFLICT(set_id) DO UPDATE SET name=EXCLUDED.name,description=EXCLUDED.description,revision=EXCLUDED.revision;";
    public const string GetParameterSetVersionHash = "SELECT payload_sha256 FROM reference_configuration.parameter_set_version WHERE set_id=$1 AND version=$2;";
    public const string UpsertParameterSetVersion = "INSERT INTO reference_configuration.parameter_set_version(set_id,version,payload_sha256,status,body) VALUES($1,$2,$3,$4,$5::jsonb) ON CONFLICT(set_id,version) DO UPDATE SET status=EXCLUDED.status,body=EXCLUDED.body;";
    public const string InsertParameterOperation = "INSERT INTO reference_configuration.parameter_operation(operation_id,set_id,revision,request_sha256,version) VALUES($1,$2,$3,$4,$5);";
    public const string InsertLegacyParameterReference = "INSERT INTO reference_configuration.parameter_legacy_reference VALUES($1,$2,$3,$4,$5,$6,$7) ON CONFLICT DO NOTHING;";
    public const string IsLegacyParameterReferenceEquivalent = "SELECT generic_set_id=$4 AND legacy_sha256=$5 AND legacy_codec=$6 FROM reference_configuration.parameter_legacy_reference WHERE legacy_kind=$1 AND legacy_set_id=$2 AND legacy_version=$3;";
    public const string InsertParameterStartupReport = "INSERT INTO reference_configuration.parameter_startup_report(run_id,revision,body) VALUES($1,$2,$3::jsonb) ON CONFLICT(run_id,revision) DO NOTHING;";
    public const string IsParameterStartupReportEquivalent = "SELECT body=$3::jsonb FROM reference_configuration.parameter_startup_report WHERE run_id=$1 AND revision=$2;";
    public const string InsertParameterStartupRun = "INSERT INTO reference_configuration.parameter_startup_run(run_id,body) VALUES($1,$2::jsonb) ON CONFLICT(run_id) DO NOTHING;";
    public const string IsParameterStartupRunEquivalent = "SELECT body=$2::jsonb FROM reference_configuration.parameter_startup_run WHERE run_id=$1;";
    public const string ReleaseParameterStartupRun = "UPDATE reference_configuration.parameter_startup_run SET active=false WHERE run_id=$1;";
    public const string SetParameterWriterLockTimeout = "SET LOCAL lock_timeout = '5s';";
    public const string AcquireParameterWriterLock = "SELECT pg_advisory_xact_lock(hashtextextended('reference.parameter-sets.writer',0));";

    public static string InsertStrategyCatalogChildren(CatalogChildTable child) => $"""
WITH input AS (SELECT $1::jsonb AS d)
INSERT INTO {CatalogPrefix}{child.Name}(owner_kind,owner_id,owner_version,{child.InsertColumns})
SELECT (d->'Key'->>'Kind')::smallint,(d->'Key'->>'Id')::uuid,(d->'Key'->>'Version')::integer,{child.InsertValues}
FROM input CROSS JOIN LATERAL jsonb_array_elements(d->'{child.Property}') j;
""";

    public static string GetStrategyCatalog(bool lockRow)
        => $"SELECT ({StrategyCatalogDefinitionJson})::text,v.content_sha256,v.status,v.created_utc,v.created_by,v.effective_from_utc,v.published_by,v.retired_at_utc,v.retired_by FROM {CatalogPrefix}strategy_catalog_version v JOIN {CatalogPrefix}strategy_catalog_identity i USING(kind,id) WHERE v.kind=$1 AND v.id=$2 AND v.version=$3" + (lockRow ? " FOR SHARE OF v;" : ";");

    public static string GetPipelinePolicyForUpdate(CatalogPipelineParameterKind kind)
        => $"SELECT payload_sha256,status,effective_from_utc,payload_json::text,schema_version,retired_at_utc FROM reference_configuration.{PipelineTable(kind)} WHERE parameter_set_id=$1 AND version=$2 FOR SHARE;";

    static string StrategyCatalogDefinitionJson => """
jsonb_build_object('Key',jsonb_build_object('Kind',v.kind,'Id',v.id,'Version',v.version),
'Code',i.code,'Name',v.name,'Description',v.description,'SchemaVersion',v.schema_version,
'Parent',CASE WHEN v.parent_id IS NULL THEN NULL ELSE jsonb_build_object('Kind',v.parent_kind,'Id',v.parent_id,'Version',v.parent_version) END,
'Horizon',v.horizon,'Side',v.side,'Bias',v.bias,'PremiumMode',v.premium_mode,'Settings',v.settings_json)
""" + string.Concat(StrategyCatalogSchemaSql.Children.Select(child => $" || jsonb_build_object('{child.Property}',COALESCE((SELECT jsonb_agg({child.ReadJson}) FROM {CatalogPrefix}{child.Name} c WHERE c.owner_kind=v.kind AND c.owner_id=v.id AND c.owner_version=v.version),'[]'::jsonb))"));

    static string PipelineTable(CatalogPipelineParameterKind kind) => kind switch
    {
        CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow => "intrinsic_time_strategy_workflow_parameter_set",
        CatalogPipelineParameterKind.RegimeDiscovery => "regime_discovery_parameter_set",
        CatalogPipelineParameterKind.MarketCondition => "market_condition_parameter_set",
        CatalogPipelineParameterKind.TradeSelection => "trade_selection_parameter_set",
        CatalogPipelineParameterKind.OrderComposition => "order_composition_parameter_set",
        CatalogPipelineParameterKind.RiskManagement => "risk_management_parameter_set",
        CatalogPipelineParameterKind.MarketConditionAssessment => "market_condition_assessment_parameter_set",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    public const string GetLookupDefinitions = """
SELECT id, group_name, internal_value, display_name, description, display_order, is_enabled, created_utc, updated_utc
FROM reference_configuration.lookup_definition
WHERE group_name = $1
ORDER BY display_order, id
LIMIT 1025;
""";

    public const string InsertMarketConditionAssessmentDraft = """
INSERT INTO reference_configuration.market_condition_assessment_parameter_set
    (parameter_set_id, version, schema_version, market_profile_id, instrument_root, target_horizon, status,
     payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, $5, $6, 0, CAST($7 AS jsonb), $8, $9, $10, $11);
""";

    public const string GetMarketConditionAssessment = """
SELECT parameter_set_id, version, schema_version, market_profile_id, instrument_root, target_horizon,
       status, effective_from_utc, payload_json::text, payload_sha256
FROM reference_configuration.market_condition_assessment_parameter_set
WHERE parameter_set_id = $1 AND version = $2;
""";

    public const string GetEffectiveMarketConditionAssessment = """
SELECT parameter_set_id, version, schema_version, market_profile_id, instrument_root, target_horizon,
       status, effective_from_utc, payload_json::text, payload_sha256
FROM reference_configuration.market_condition_assessment_parameter_set
WHERE market_profile_id = $1 AND instrument_root = $2 AND target_horizon = $3 AND status = 1
  AND effective_from_utc <= $4 AND (retired_at_utc IS NULL OR retired_at_utc > $4)
ORDER BY effective_from_utc DESC, parameter_set_id, version DESC
LIMIT 2;
""";

    public const string InsertVolatilitySeriesDefinition = """
INSERT INTO reference_configuration.volatility_series_definition
    (series_id, methodology_version, environment, effective_from_utc, effective_until_utc, payload_json,
     payload_sha256, approved_configuration_version, owner, approval_evidence_id)
VALUES ($1, $2, $3, $4, $5, CAST($6 AS jsonb), $7, $8, $9, $10)
ON CONFLICT (series_id, methodology_version) DO NOTHING;
""";

    public const string GetVolatilitySeriesDefinition = """
SELECT series_id, methodology_version, environment, effective_from_utc, effective_until_utc,
       payload_json::text, payload_sha256
FROM reference_configuration.volatility_series_definition
WHERE series_id = $1 AND methodology_version = $2;
""";

    public const string GetEffectiveVolatilitySeriesDefinition = """
SELECT series_id, methodology_version, environment, effective_from_utc, effective_until_utc,
       payload_json::text, payload_sha256
FROM reference_configuration.volatility_series_definition
WHERE environment = $1 AND series_id = $2 AND effective_from_utc <= $3
  AND (effective_until_utc IS NULL OR effective_until_utc > $3)
ORDER BY effective_from_utc DESC, methodology_version DESC
LIMIT 2;
""";

    public const string InsertTradeSelectionDraft = """
INSERT INTO reference_configuration.trade_selection_parameter_set
    (parameter_set_id, version, schema_version, status, payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
""";

    public const string InsertOrderCompositionDraft = """
INSERT INTO reference_configuration.order_composition_parameter_set
    (parameter_set_id, version, schema_version, status, payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
""";

    public const string InsertStrategyWorkflowDraft = """
INSERT INTO reference_configuration.intrinsic_time_strategy_workflow_parameter_set
    (parameter_set_id, version, schema_version, status, payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
""";

    public const string InsertRiskManagementDraft = """
INSERT INTO reference_configuration.risk_management_parameter_set
    (parameter_set_id, version, schema_version, status, payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
""";

    public const string GetTradeSelectionVersion = """
SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
       payload_json::text, payload_sha256
FROM reference_configuration.trade_selection_parameter_set
WHERE parameter_set_id = $1 AND version = $2;
""";

    public static string GetSelectionPipelinePolicyFor(CatalogPipelineParameterKind kind)
    {
        var table = kind switch
        {
            CatalogPipelineParameterKind.IntrinsicTimeStrategyWorkflow => "intrinsic_time_strategy_workflow_parameter_set",
            CatalogPipelineParameterKind.RegimeDiscovery => "regime_discovery_parameter_set",
            CatalogPipelineParameterKind.MarketCondition => "market_condition_parameter_set",
            CatalogPipelineParameterKind.MarketConditionAssessment => "market_condition_assessment_parameter_set",
            CatalogPipelineParameterKind.TradeSelection => "trade_selection_parameter_set",
            CatalogPipelineParameterKind.OrderComposition => "order_composition_parameter_set",
            CatalogPipelineParameterKind.RiskManagement => "risk_management_parameter_set",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        return $"""
SELECT {(short)kind} AS parameter_kind, parameter_set_id, version, schema_version, status,
       effective_from_utc, retired_at_utc, payload_json::text, payload_sha256
FROM reference_configuration.{table}
WHERE parameter_set_id = $1 AND version = $2;
""";
    }

    public const string GetLegacyParameterVersions = """
SELECT parameter_set_id, version, schema_version, payload_json::text, payload_sha256, description, status
FROM reference_configuration.regime_discovery_parameter_set
WHERE ($1::uuid IS NULL OR parameter_set_id = $1) AND ($2 = 0 OR version = $2)
ORDER BY parameter_set_id, version
LIMIT 100 OFFSET $3;
""";

    public const string GetParameterComponents = """
SELECT a.code, a.name, c.code, c.name,
       array_to_json(array_agg(s.schema_version ORDER BY s.schema_version))::text
FROM reference_configuration.parameter_area a
JOIN reference_configuration.parameter_component c ON c.area_id = a.area_id
JOIN reference_configuration.parameter_schema_version s ON s.component_code = c.code
WHERE a.enabled AND c.enabled
GROUP BY a.code, a.name, c.code, c.name
ORDER BY a.name, c.name;
""";

    public const string GetParameterSchema = """
SELECT component_code, schema_version, codec, schema_sha256, schema_json::text
FROM reference_configuration.parameter_schema_version
WHERE component_code = $1 AND schema_version = $2;
""";

    public const string GetParameterSets = """
SELECT v.body::text, s.name, s.description, s.revision
FROM reference_configuration.parameter_set_version v
JOIN reference_configuration.parameter_set s USING(set_id)
WHERE s.component_code = $1 AND ($2::uuid IS NULL OR s.set_id = $2)
  AND ($4::uuid IS NULL OR (s.name, s.set_id, -v.version) > ($3, $4, -$5::integer))
ORDER BY s.name, s.set_id, v.version DESC
LIMIT $6;
""";

    public const string InsertDraft = """
INSERT INTO reference_configuration.regime_discovery_parameter_set
    (parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
     payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, NULL, NULL,
        CAST($5 AS jsonb), $6, $7, $8, $9);
""";

    public const string Publish = """
UPDATE reference_configuration.regime_discovery_parameter_set
SET status = $1, effective_from_utc = $2
WHERE parameter_set_id = $3 AND version = $4 AND status = $5;
""";

    public const string Retire = """
UPDATE reference_configuration.regime_discovery_parameter_set
SET status = $1, retired_at_utc = $2
WHERE parameter_set_id = $3 AND version = $4 AND status = $5;
""";

    public const string GetExact = """
SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
       payload_json::text, payload_sha256, description, created_utc, created_by
FROM reference_configuration.regime_discovery_parameter_set
WHERE parameter_set_id = $1 AND version = $2;
""";

    public const string ResolveEffective = """
SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
       payload_json::text, payload_sha256, description, created_utc, created_by
FROM reference_configuration.regime_discovery_parameter_set
WHERE status = $1
  AND effective_from_utc <= $2
  AND CAST(payload_json ->> 'TargetHorizon' AS smallint) = $3
  AND (retired_at_utc IS NULL OR retired_at_utc > $4)
ORDER BY effective_from_utc DESC, parameter_set_id, version DESC
LIMIT 2;
""";

    public const string InsertMarketConditionDraft = """
INSERT INTO reference_configuration.market_condition_parameter_set
    (parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
     payload_json, payload_sha256, description, created_utc, created_by)
VALUES ($1, $2, $3, $4, NULL, NULL, CAST($5 AS jsonb), $6, $7, $8, $9);
""";

    public const string GetExactMarketCondition = """
SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
       payload_json::text, payload_sha256, description, created_utc, created_by
FROM reference_configuration.market_condition_parameter_set
WHERE parameter_set_id = $1 AND version = $2;
""";

    public const string ResolveEffectiveMarketCondition = """
SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
       payload_json::text, payload_sha256, description, created_utc, created_by
FROM reference_configuration.market_condition_parameter_set
WHERE status = $1 AND effective_from_utc <= $2
  AND CAST(payload_json ->> 'FundId' AS integer) = $3
  AND payload_json ->> 'InstrumentRoot' = $4
  AND CAST(payload_json ->> 'TargetHorizon' AS smallint) = $5
  AND (retired_at_utc IS NULL OR retired_at_utc > $6)
ORDER BY effective_from_utc DESC, parameter_set_id, version DESC
LIMIT 2;
""";

    public static string PublishFor(StrategyParameterSetKind kind) => LifecycleFor(kind, """
UPDATE reference_configuration.{0}
SET status = $1, effective_from_utc = $2
WHERE parameter_set_id = $3 AND version = $4 AND status = $5;
""");

    public static string RetireFor(StrategyParameterSetKind kind) => LifecycleFor(kind, """
UPDATE reference_configuration.{0}
SET status = $1, retired_at_utc = $2
WHERE parameter_set_id = $3 AND version = $4 AND status = $5;
""");

    static string LifecycleFor(StrategyParameterSetKind kind, string template)
    {
        var table = kind switch
        {
            StrategyParameterSetKind.IntrinsicTimeStrategyWorkflow => "intrinsic_time_strategy_workflow_parameter_set",
            StrategyParameterSetKind.OrderComposition => "order_composition_parameter_set",
            StrategyParameterSetKind.RiskManagement => "risk_management_parameter_set",
            StrategyParameterSetKind.TradeSelection => "trade_selection_parameter_set",
            StrategyParameterSetKind.RegimeDiscovery => "regime_discovery_parameter_set",
            StrategyParameterSetKind.MarketCondition => "market_condition_parameter_set",
            StrategyParameterSetKind.MarketConditionAssessment => "market_condition_assessment_parameter_set",
            _ => throw new NotSupportedException($"The typed lifecycle for {kind} is not defined yet.")
        };
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, template, table);
    }
}
