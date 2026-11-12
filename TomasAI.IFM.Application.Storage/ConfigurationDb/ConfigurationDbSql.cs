namespace TomasAI.IFM.Application.Storage.ConfigurationDb;

/// <summary>Contains the PostgreSQL statements and query templates for ConfigurationDb.</summary>
public static class ConfigurationDbSql
{
    /// <summary>Acquires the named advisory lock.</summary>
    internal const string AcquireNamedAdvisoryLock =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1, 0));
        """;

    /// <summary>Acquires the parameter writer lock.</summary>
    internal const string AcquireParameterWriterLock =
        """
        SELECT pg_advisory_xact_lock(hashtextextended('reference.parameter-sets.writer', 0));
        """;

    /// <summary>Reads an active startup run by identity.</summary>
    internal const string ActiveStartup =
        """
        SELECT run_id
        FROM reference_configuration.parameter_catalog_startup_state
        WHERE run_id = $1
          AND active;
        """;

    /// <summary>Advances the revision fence.</summary>
    internal const string AdvanceRevisionFence =
        """
        UPDATE reference_configuration.parameter_revision_fence
        SET revision = revision + 1
        WHERE singleton = TRUE;
        """;

    /// <summary>Reads the current lookup definition sequence state.</summary>
    internal const string BaselineLookupSequence =
        """
        SELECT last_value, is_called
        FROM reference_configuration.lookup_definition_id_seq;
        """;

    /// <summary>Reads the current source event or the event for the specified command.</summary>
    internal const string CatalogSourceHead =
        """
        SELECT el.streamversion, el.eventversion, names.eventtypename, el.eventpayload
        FROM event_stream_id stream
        JOIN event_log el ON el.eventstreamid = stream.eventstreamid
        JOIN event_name_id names ON names.eventnameid = el.eventnameid
        WHERE stream.eventstream = $1
          AND (el.commandid = $2 OR el.streamversion = stream.currentversion)
        ORDER BY (el.commandid = $2) DESC, el.streamversion DESC
        LIMIT 1;
        """;

    /// <summary>Reads the latest source event for each Strategy Catalog stream.</summary>
    internal const string CatalogSourceHeads =
        """
        SELECT el.eventversion, names.eventtypename, el.eventpayload
        FROM event_stream_id stream
        JOIN event_log el ON el.eventstreamid = stream.eventstreamid AND el.streamversion = stream.currentversion
        JOIN event_name_id names ON names.eventnameid = el.eventnameid
        WHERE stream.eventstream LIKE 'StrategyCatalog.%'
        ORDER BY el.eventversion;
        """;

    /// <summary>Finds catalog versions without an event stream.</summary>
    internal const string CatalogVersionsWithoutSource =
        """
        SELECT v.kind, v.id, v.version
        FROM reference_configuration.strategy_catalog_version v
        WHERE NOT EXISTS (
            SELECT 1
            FROM event_stream_id s
            WHERE s.eventstream = 'StrategyCatalog.' || v.kind::text || '-'
                || replace(v.id::text, '-', '') || '-' || v.version::text
              AND s.currentversion > 0
        )
        ORDER BY v.kind, v.id, v.version;
        """;

    /// <summary>Acquires the Strategy Catalog write lock.</summary>
    internal const string CatalogWriteLock =
        """
        SELECT pg_advisory_xact_lock($1);
        """;

    /// <summary>Completes the parameter projection.</summary>
    internal const string CompleteParameterProjection =
        """
        DELETE FROM reference_configuration.parameter_projection_pending
        WHERE event_id = $1;
        """;

    /// <summary>Deletes the projection rows for one parameter version.</summary>
    internal const string DeleteParameterProjectionRowsTemplate =
        """
        DELETE FROM reference_configuration.{0}
        WHERE owner_set_id = $1
          AND owner_version = $2;
        """;

    /// <summary>Reads the baseline application.</summary>
    internal const string GetBaselineApplication =
        """
        SELECT source_event_id
        FROM reference_configuration.configuration_baseline_application
        WHERE baseline_id = $1;
        """;

    /// <summary>Reads the effective market condition assessment.</summary>
    internal const string GetEffectiveMarketConditionAssessment =
        """
        SELECT parameter_set_id, version, schema_version, market_profile_id, instrument_root,
               target_horizon, status, effective_from_utc, payload_json::text, payload_sha256
        FROM reference_configuration.market_condition_assessment_parameter_set
        WHERE market_profile_id = $1
          AND instrument_root = $2
          AND target_horizon = $3
          AND status = 1
          AND effective_from_utc <= $4
          AND (retired_at_utc IS NULL OR retired_at_utc > $4)
        ORDER BY effective_from_utc DESC, parameter_set_id, version DESC
        LIMIT 2;
        """;

    /// <summary>Reads the effective volatility series definition.</summary>
    internal const string GetEffectiveVolatilitySeriesDefinition =
        """
        SELECT series_id, methodology_version, environment, effective_from_utc, effective_until_utc,
               payload_json::text, payload_sha256
        FROM reference_configuration.volatility_series_definition
        WHERE environment = $1
          AND series_id = $2
          AND effective_from_utc <= $3
          AND (effective_until_utc IS NULL OR effective_until_utc > $3)
        ORDER BY effective_from_utc DESC, methodology_version DESC
        LIMIT 2;
        """;

    /// <summary>Reads the exact Regime Discovery parameter version.</summary>
    internal const string GetExact =
        """
        SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
               payload_json::text, payload_sha256, description, created_utc, created_by
        FROM reference_configuration.regime_discovery_parameter_set
        WHERE parameter_set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the exact Market Condition parameter version.</summary>
    internal const string GetExactMarketCondition =
        """
        SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
               payload_json::text, payload_sha256, description, created_utc, created_by
        FROM reference_configuration.market_condition_parameter_set
        WHERE parameter_set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the latest strategy catalog version.</summary>
    internal const string GetLatestStrategyCatalogVersion =
        """
        SELECT COALESCE(MAX(version), 0)
        FROM reference_configuration.strategy_catalog_version
        WHERE kind = $1
          AND id = $2;
        """;

    /// <summary>Reads the legacy parameter versions.</summary>
    internal const string GetLegacyParameterVersions =
        """
        SELECT parameter_set_id, version, schema_version, payload_json::text, payload_sha256, description, status
        FROM reference_configuration.regime_discovery_parameter_set
        WHERE ($1::uuid IS NULL OR parameter_set_id = $1)
          AND ($2 = 0 OR version = $2)
        ORDER BY parameter_set_id, version
        LIMIT 100 OFFSET $3;
        """;

    /// <summary>Reads the lookup definitions.</summary>
    internal const string GetLookupDefinitions =
        """
        SELECT id, group_name, internal_value, display_name, description, display_order, is_enabled,
               created_utc, updated_utc
        FROM reference_configuration.lookup_definition
        WHERE group_name = $1
        ORDER BY display_order, id
        LIMIT 1025;
        """;

    /// <summary>Reads the market condition assessment.</summary>
    internal const string GetMarketConditionAssessment =
        """
        SELECT parameter_set_id, version, schema_version, market_profile_id, instrument_root,
               target_horizon, status, effective_from_utc, payload_json::text, payload_sha256
        FROM reference_configuration.market_condition_assessment_parameter_set
        WHERE parameter_set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the parameter components.</summary>
    internal const string GetParameterComponents =
        """
        SELECT a.code, a.name, c.code, c.name,
               array_to_json (array_agg(s.schema_version ORDER BY s.schema_version))::text
        FROM reference_configuration.parameter_area a
        JOIN reference_configuration.parameter_component c ON c.area_id = a.area_id
        JOIN reference_configuration.parameter_schema_version s ON s.component_code = c.code
        WHERE a.enabled
          AND c.enabled
        GROUP BY a.code, a.name, c.code, c.name
        ORDER BY a.name, c.name;
        """;

    /// <summary>Reads the parameter schema.</summary>
    internal const string GetParameterSchema =
        """
        SELECT component_code, schema_version, codec, schema_sha256, schema_json::text
        FROM reference_configuration.parameter_schema_version
        WHERE component_code = $1
          AND schema_version = $2;
        """;

    /// <summary>Reads the parameter sets.</summary>
    internal const string GetParameterSets =
        """
        SELECT v.body::text, s.name, s.description, s.revision
        FROM reference_configuration.parameter_set_version v
        JOIN reference_configuration.parameter_set s USING (set_id)
        WHERE s.component_code = $1
          AND ($2::uuid IS NULL OR s.set_id = $2)
          AND ($4::uuid IS NULL OR (s.name, s.set_id, -v.version) > ($3, $4, -$5::integer))
        ORDER BY s.name, s.set_id, v.version DESC
        LIMIT $6;
        """;

    /// <summary>Reads a pipeline policy payload with an optional shared row lock.</summary>
    internal const string GetPipelinePolicySelectionTemplate =
        """
        SELECT payload_sha256, status, effective_from_utc, payload_json::text, schema_version, retired_at_utc
        FROM reference_configuration.{0}
        WHERE parameter_set_id = $1
          AND version = $2{1};
        """;

    /// <summary>Reads the requested pipeline policy and its kind.</summary>
    internal const string GetSelectionPipelinePolicyTemplate =
        """
        SELECT {0} AS parameter_kind, parameter_set_id, version, schema_version, status,
               effective_from_utc, retired_at_utc, payload_json::text, payload_sha256
        FROM reference_configuration.{1}
        WHERE parameter_set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the strategy catalog identity code.</summary>
    internal const string GetStrategyCatalogIdentityCode =
        """
        SELECT code
        FROM reference_configuration.strategy_catalog_identity
        WHERE kind = $1
          AND id = $2;
        """;

    /// <summary>Reads a catalog definition and its lifecycle metadata.</summary>
    internal const string GetStrategyCatalogTemplate =
        """
        SELECT (
            jsonb_build_object(
                'Key', jsonb_build_object('Kind', v.kind, 'Id', v.id, 'Version', v.version),
                'Code', i.code,
                'Name', v.name,
                'Description', v.description,
                'SchemaVersion', v.schema_version,
                'Parent', CASE WHEN v.parent_id IS NULL THEN NULL
                               ELSE jsonb_build_object('Kind', v.parent_kind, 'Id', v.parent_id, 'Version', v.parent_version)
                          END,
                'Horizon', v.horizon,
                'Side', v.side,
                'Bias', v.bias,
                'PremiumMode', v.premium_mode,
                'Settings', v.settings_json
            ){0}
        )::text, v.content_sha256, v.status, v.created_utc, v.created_by,
            v.effective_from_utc, v.published_by, v.retired_at_utc, v.retired_by
        FROM reference_configuration.strategy_catalog_version v
        JOIN reference_configuration.strategy_catalog_identity i USING (kind, id)
        WHERE v.kind = $1
          AND v.id = $2
          AND v.version = $3{1};
        """;

    /// <summary>Reads the strategy catalogs.</summary>
    internal const string GetStrategyCatalogs =
        """
        SELECT v.id, v.version, i.code, v.name, v.status, v.content_sha256
        FROM reference_configuration.strategy_catalog_identity i
        CROSS JOIN LATERAL (
            SELECT *
            FROM reference_configuration.strategy_catalog_version v
            WHERE v.kind = i.kind
              AND v.id = i.id
            ORDER BY version DESC
            LIMIT 1
        ) v
        WHERE i.kind = $1
          AND i.code COLLATE "C" > $2 COLLATE "C"
        ORDER BY i.code COLLATE "C"
        LIMIT $3;
        """;

    /// <summary>Reads the strategy position risk version.</summary>
    internal const string GetStrategyPositionRiskVersion =
        """
        SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
               payload_json::text, payload_sha256
        FROM reference_configuration.strategy_position_risk_parameter_set
        WHERE parameter_set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the trade selection version.</summary>
    internal const string GetTradeSelectionVersion =
        """
        SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
               payload_json::text, payload_sha256
        FROM reference_configuration.trade_selection_parameter_set
        WHERE parameter_set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the volatility series definition.</summary>
    internal const string GetVolatilitySeriesDefinition =
        """
        SELECT series_id, methodology_version, environment, effective_from_utc, effective_until_utc,
               payload_json::text, payload_sha256
        FROM reference_configuration.volatility_series_definition
        WHERE series_id = $1
          AND methodology_version = $2;
        """;

    /// <summary>Checks whether a startup run exists.</summary>
    internal const string HasStartup =
        """
        SELECT EXISTS (SELECT 1 FROM reference_configuration.parameter_catalog_startup_state WHERE run_id = $1);
        """;

    /// <summary>Inserts the baseline application.</summary>
    internal const string InsertBaselineApplication =
        """
        INSERT INTO reference_configuration.configuration_baseline_application
            (baseline_id, source_event_id, source_cut)
        VALUES ($1, $2, $3);
        """;

    /// <summary>Restores the supplied baseline rows into the selected table.</summary>
    internal const string InsertBaselineRowsTemplate =
        """
        INSERT INTO {0} ({1})
        OVERRIDING SYSTEM VALUE
        SELECT {1}
        FROM jsonb_populate_recordset(NULL::{0}, $1::jsonb)
        ON CONFLICT DO NOTHING;
        """;

    /// <summary>Inserts a Regime Discovery draft.</summary>
    internal const string InsertDraft =
        """
        INSERT INTO reference_configuration.regime_discovery_parameter_set
            (parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
            payload_json, payload_sha256, description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, NULL, NULL, CAST($5 AS jsonb), $6, $7, $8, $9);
        """;

    /// <summary>Inserts the legacy parameter reference.</summary>
    internal const string InsertLegacyParameterReference =
        """
        INSERT INTO reference_configuration.parameter_legacy_reference
        VALUES ($1, $2, $3, $4, $5, $6, $7)
        ON CONFLICT DO NOTHING;
        """;

    /// <summary>Inserts the market condition assessment draft.</summary>
    internal const string InsertMarketConditionAssessmentDraft =
        """
        INSERT INTO reference_configuration.market_condition_assessment_parameter_set
            (parameter_set_id, version, schema_version, market_profile_id, instrument_root,
            target_horizon, status, payload_json, payload_sha256, description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, $5, $6, 0, CAST($7 AS jsonb), $8, $9, $10, $11);
        """;

    /// <summary>Inserts the market condition draft.</summary>
    internal const string InsertMarketConditionDraft =
        """
        INSERT INTO reference_configuration.market_condition_parameter_set
            (parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
            payload_json, payload_sha256, description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, NULL, NULL, CAST($5 AS jsonb), $6, $7, $8, $9);
        """;

    /// <summary>Inserts the order composition draft.</summary>
    internal const string InsertOrderCompositionDraft =
        """
        INSERT INTO reference_configuration.order_composition_parameter_set
            (parameter_set_id, version, schema_version, status, payload_json, payload_sha256,
            description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
        """;

    /// <summary>Inserts the receipt.</summary>
    internal const string InsertParameterCatalogReceipt =
        """
        INSERT INTO reference_configuration.parameter_catalog_operation
            (operation_id, entity_id, revision, request_sha256, kind, version, committed_at_ticks,
            fact_sha256)
        VALUES ($1, $2, $3, $4, $5, $6, $7, $8);
        """;

    /// <summary>Inserts the header.</summary>
    internal const string InsertParameterValueHeader =
        """
        INSERT INTO reference_configuration.parameter_value_version
            (set_id, version, component_code, schema_version, content_sha256, codec)
        VALUES ($1, $2, $3, $4, $5, $6);
        """;

    /// <summary>Inserts the risk management draft.</summary>
    internal const string InsertRiskManagementDraft =
        """
        INSERT INTO reference_configuration.risk_management_parameter_set
            (parameter_set_id, version, schema_version, status, payload_json, payload_sha256,
            description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
        """;

    /// <summary>Inserts catalog children from the supplied definition.</summary>
    internal const string InsertStrategyCatalogChildrenTemplate =
        """
        WITH input AS (
            SELECT $1::jsonb AS d
        )
        INSERT INTO reference_configuration.{0} (owner_kind, owner_id, owner_version, {1})
        SELECT (d -> 'Key' ->> 'Kind')::smallint,
               (d -> 'Key' ->> 'Id')::uuid,
               (d -> 'Key' ->> 'Version')::integer,
               {2}
        FROM input
        CROSS JOIN LATERAL jsonb_array_elements(d -> '{3}') j;
        """;

    /// <summary>Inserts the strategy catalog identity.</summary>
    internal const string InsertStrategyCatalogIdentity =
        """
        INSERT INTO reference_configuration.strategy_catalog_identity (kind, id, code, created_utc, created_by)
        VALUES ($1, $2, $3, $4, $5)
        ON CONFLICT (kind, id) DO NOTHING;
        """;

    /// <summary>Inserts the strategy catalog version.</summary>
    internal const string InsertStrategyCatalogVersion =
        """
        WITH input AS (SELECT $1::jsonb d)
        INSERT INTO reference_configuration.strategy_catalog_version
            (kind, id, version, schema_version, name, description, parent_kind, parent_id,
            parent_version, horizon, side, bias, premium_mode, settings_json, content_sha256,
            created_utc, created_by)
        SELECT (d -> 'Key' ->> 'Kind')::smallint, (d -> 'Key' ->> 'Id')::uuid,
               (d -> 'Key' ->> 'Version')::integer, (d ->> 'SchemaVersion')::smallint, d ->> 'Name',
               d ->> 'Description', (d -> 'Parent' ->> 'Kind')::smallint,
               (d -> 'Parent' ->> 'Id')::uuid, (d -> 'Parent' ->> 'Version')::integer,
               (d ->> 'Horizon')::smallint, d ->> 'Side', d ->> 'Bias', d ->> 'PremiumMode',
               d -> 'Settings', $2, $3, $4
        FROM input;
        """;

    /// <summary>Inserts the strategy position risk draft.</summary>
    internal const string InsertStrategyPositionRiskDraft =
        """
        INSERT INTO reference_configuration.strategy_position_risk_parameter_set
            (parameter_set_id, version, schema_version, status, payload_json, payload_sha256,
            description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
        """;

    /// <summary>Inserts the strategy workflow draft.</summary>
    internal const string InsertStrategyWorkflowDraft =
        """
        INSERT INTO reference_configuration.intrinsic_time_strategy_workflow_parameter_set
            (parameter_set_id, version, schema_version, status, payload_json, payload_sha256,
            description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
        """;

    /// <summary>Inserts the trade selection draft.</summary>
    internal const string InsertTradeSelectionDraft =
        """
        INSERT INTO reference_configuration.trade_selection_parameter_set
            (parameter_set_id, version, schema_version, status, payload_json, payload_sha256,
            description, created_utc, created_by)
        VALUES ($1, $2, $3, $4, CAST($5 AS jsonb), $6, $7, $8, $9);
        """;

    /// <summary>Inserts a typed parameter row with its mapped columns and bound values.</summary>
    internal const string InsertTypedParameterRowTemplate =
        """
        INSERT INTO reference_configuration.{0}
            (owner_set_id, owner_version, row_path, parent_path, ordinal, {1})
        VALUES ($1, $2, $3, $4, $5, {2});
        """;

    /// <summary>Inserts the volatility series definition.</summary>
    internal const string InsertVolatilitySeriesDefinition =
        """
        INSERT INTO reference_configuration.volatility_series_definition
            (series_id, methodology_version, environment, effective_from_utc, effective_until_utc,
            payload_json, payload_sha256, approved_configuration_version, owner, approval_evidence_id)
        VALUES ($1, $2, $3, $4, $5, CAST($6 AS jsonb), $7, $8, $9, $10)
        ON CONFLICT (series_id, methodology_version) DO NOTHING;
        """;

    /// <summary>Checks whether a legacy parameter reference matches the stored mapping.</summary>
    internal const string IsLegacyParameterReferenceEquivalent =
        """
        SELECT generic_set_id = $4 AND legacy_sha256 = $5 AND legacy_codec = $6
        FROM reference_configuration.parameter_legacy_reference
        WHERE legacy_kind = $1
          AND legacy_set_id = $2
          AND legacy_version = $3;
        """;

    /// <summary>Locks the baseline tables and the application receipt table.</summary>
    internal const string LockBaselineTablesTemplate =
        """
        LOCK TABLE {0}, reference_configuration.configuration_baseline_application
        IN SHARE ROW EXCLUSIVE MODE;
        """;

    /// <summary>Acquires the typed parameter write lock.</summary>
    internal const string LockParameterValue =
        """
        SELECT pg_advisory_xact_lock(hashtextextended($1, 87281));
        """;

    /// <summary>Marks the parameter projection pending.</summary>
    internal const string MarkParameterProjectionPending =
        """
        INSERT INTO reference_configuration.parameter_projection_pending (event_id)
        VALUES ($1)
        ON CONFLICT DO NOTHING;
        """;

    /// <summary>Reads catalog operations for one parameter owner.</summary>
    internal const string OwnerOperations =
        """
        SELECT operation_id, revision, request_sha256, version
        FROM reference_configuration.parameter_catalog_operation
        WHERE kind = $1
          AND entity_id = $2
        ORDER BY revision;
        """;

    /// <summary>Prepares the baseline catalog drafts.</summary>
    internal const string PrepareBaselineCatalogDrafts =
        """
        SELECT COALESCE(jsonb_agg(j || jsonb_build_object(
            'status', 0,
            'content_sealed', FALSE,
            'effective_from_utc', NULL,
            'published_by', NULL,
            'retired_at_utc', NULL,
            'retired_by', NULL
        )), '[]'::jsonb)::text
        FROM jsonb_array_elements ($1::jsonb) j;
        """;

    /// <summary>Publishes the Regime Discovery parameter version.</summary>
    internal const string Publish =
        """
        UPDATE reference_configuration.regime_discovery_parameter_set
        SET status = $1, effective_from_utc = $2
        WHERE parameter_set_id = $3
          AND version = $4
          AND status = $5;
        """;

    /// <summary>Publishes a version in a selected parameter table.</summary>
    internal const string PublishParameterVersionTemplate =
        """
        UPDATE reference_configuration.{0}
        SET status = $1, effective_from_utc = $2
        WHERE parameter_set_id = $3
          AND version = $4
          AND status = $5;
        """;

    /// <summary>Publishes the strategy catalog.</summary>
    internal const string PublishStrategyCatalog =
        """
        UPDATE reference_configuration.strategy_catalog_version
        SET status = 1, effective_from_utc = $4, published_by = $5
        WHERE kind = $1
          AND id = $2
          AND version = $3
          AND status = 0
          AND content_sha256 = $6;
        """;

    /// <summary>Reads the baseline columns.</summary>
    internal const string ReadBaselineColumns =
        """
        SELECT column_name, udt_schema, udt_name, is_nullable, COALESCE(column_default, ''),
               is_identity, identity_generation
        FROM information_schema.columns
        WHERE table_schema = $1
          AND table_name = $2
          AND is_generated = 'NEVER'
        ORDER BY ordinal_position;
        """;

    /// <summary>Reads the selected baseline table as deterministically ordered JSON.</summary>
    internal const string ReadBaselineRowsTemplate =
        """
        SELECT COALESCE(jsonb_agg(j ORDER BY j::text), '[]'::jsonb)::text
        FROM (
            SELECT to_jsonb(t) j
            FROM {0} t
        ) q;
        """;

    /// <summary>Reads the request and fact hashes for a catalog operation.</summary>
    internal const string ReadParameterCatalogReceipt =
        """
        SELECT request_sha256, fact_sha256
        FROM reference_configuration.parameter_catalog_operation
        WHERE operation_id = $1;
        """;

    /// <summary>Reads the latest revision for a parameter catalog owner.</summary>
    internal const string ReadParameterCatalogRevision =
        """
        SELECT COALESCE(MAX(revision), 0)
        FROM reference_configuration.parameter_catalog_operation
        WHERE kind = $1
          AND entity_id = $2;
        """;

    /// <summary>Reads the parameter projection record hash.</summary>
    internal const string ReadParameterProjectionHash =
        """
        SELECT content_sha256
        FROM reference_configuration.parameter_projection_record
        WHERE kind = $1
          AND owner_id = $2
          AND owner_version = $3;
        """;

    /// <summary>Reads the typed parameter version hash.</summary>
    internal const string ReadParameterValueHash =
        """
        SELECT content_sha256
        FROM reference_configuration.parameter_value_version
        WHERE set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the header.</summary>
    internal const string ReadParameterValueHeader =
        """
        SELECT component_code, schema_version, content_sha256, codec
        FROM reference_configuration.parameter_value_version
        WHERE set_id = $1
          AND version = $2;
        """;

    /// <summary>Reads the projected revision.</summary>
    internal const string ReadProjectedRevision =
        """
        SELECT revision
        FROM reference_configuration.parameter_revision_fence
        WHERE singleton = TRUE
          AND NOT EXISTS (SELECT 1 FROM reference_configuration.parameter_projection_pending);
        """;

    /// <summary>Reads the revision fence.</summary>
    internal const string ReadRevisionFence =
        """
        SELECT revision
        FROM reference_configuration.parameter_revision_fence
        WHERE singleton = TRUE;
        """;

    /// <summary>Reads typed parameter rows for a version in path order.</summary>
    internal const string ReadTypedParameterRowsTemplate =
        """
        SELECT row_path, parent_path, ordinal, {1}
        FROM reference_configuration.{0}
        WHERE owner_set_id = $1
          AND owner_version = $2
        ORDER BY row_path;
        """;

    /// <summary>Resolves the effective Regime Discovery parameter version.</summary>
    internal const string ResolveEffective =
        """
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

    /// <summary>Resolves the effective market condition.</summary>
    internal const string ResolveEffectiveMarketCondition =
        """
        SELECT parameter_set_id, version, schema_version, status, effective_from_utc, retired_at_utc,
               payload_json::text, payload_sha256, description, created_utc, created_by
        FROM reference_configuration.market_condition_parameter_set
        WHERE status = $1
          AND effective_from_utc <= $2
          AND CAST(payload_json ->> 'FundId' AS integer) = $3
          AND payload_json ->> 'InstrumentRoot' = $4
          AND CAST(payload_json ->> 'TargetHorizon' AS smallint) = $5
          AND (retired_at_utc IS NULL OR retired_at_utc > $6)
        ORDER BY effective_from_utc DESC, parameter_set_id, version DESC
        LIMIT 2;
        """;

    /// <summary>Seals restored catalog versions.</summary>
    internal const string RestoreBaselineCatalogLifecycle1 =
        """
        UPDATE reference_configuration.strategy_catalog_version v
        SET content_sealed = TRUE
        WHERE NOT v.content_sealed
          AND EXISTS (
              SELECT 1
              FROM jsonb_populate_recordset(NULL::reference_configuration.strategy_catalog_version, $1::jsonb) b
              WHERE (b.kind, b.id, b.version) = (v.kind, v.id, v.version)
          );
        """;

    /// <summary>Restores publication metadata for sealed catalog versions.</summary>
    internal const string RestoreBaselineCatalogLifecycle2 =
        """
        UPDATE reference_configuration.strategy_catalog_version v
        SET status = 1, effective_from_utc = b.effective_from_utc, published_by = b.published_by
        FROM jsonb_populate_recordset(NULL::reference_configuration.strategy_catalog_version, $1::jsonb) b
        WHERE (b.kind, b.id, b.version) = (v.kind, v.id, v.version)
          AND b.status >= 1;
        """;

    /// <summary>Restores retirement metadata for published catalog versions.</summary>
    internal const string RestoreBaselineCatalogLifecycle3 =
        """
        UPDATE reference_configuration.strategy_catalog_version v
        SET status = 2, retired_at_utc = b.retired_at_utc, retired_by = b.retired_by
        FROM jsonb_populate_recordset(NULL::reference_configuration.strategy_catalog_version, $1::jsonb) b
        WHERE (b.kind, b.id, b.version) = (v.kind, v.id, v.version)
          AND b.status = 2;
        """;

    /// <summary>Restores the baseline lookup sequence.</summary>
    internal const string RestoreBaselineLookupSequence =
        """
        SELECT setval(pg_get_serial_sequence('reference_configuration.lookup_definition', 'id'), $1, $2);
        """;

    /// <summary>Retires the Regime Discovery parameter version.</summary>
    internal const string Retire =
        """
        UPDATE reference_configuration.regime_discovery_parameter_set
        SET status = $1, retired_at_utc = $2
        WHERE parameter_set_id = $3
          AND version = $4
          AND status = $5;
        """;

    /// <summary>Retires a version in a selected parameter table.</summary>
    internal const string RetireParameterVersionTemplate =
        """
        UPDATE reference_configuration.{0}
        SET status = $1, retired_at_utc = $2
        WHERE parameter_set_id = $3
          AND version = $4
          AND status = $5;
        """;

    /// <summary>Retires the strategy catalog.</summary>
    internal const string RetireStrategyCatalog =
        """
        UPDATE reference_configuration.strategy_catalog_version
        SET status = 2, retired_at_utc = $4, retired_by = $5
        WHERE kind = $1
          AND id = $2
          AND version = $3;
        """;

    /// <summary>Rolls back to the catalog acceptance savepoint.</summary>
    internal const string RollbackAcceptance =
        """
        ROLLBACK TO SAVEPOINT catalog_acceptance;
        """;

    /// <summary>Seals the strategy catalog version.</summary>
    internal const string SealStrategyCatalogVersion =
        """
        UPDATE reference_configuration.strategy_catalog_version
        SET content_sealed = TRUE
        WHERE kind = $1
          AND id = $2
          AND version = $3;
        """;

    /// <summary>Sets the parameter writer lock timeout.</summary>
    internal const string SetParameterWriterLockTimeout =
        """
        SET LOCAL lock_timeout = '5s';
        """;

    /// <summary>Reads the snapshot versions for a parameter set.</summary>
    internal const string SetSnapshotVersions =
        """
        SELECT m.owner_version, s.name, s.description, s.revision
        FROM reference_configuration.parameter_catalog_version m
        JOIN reference_configuration.parameter_set s ON s.set_id = m.owner_set_id
        WHERE m.owner_set_id = $1
          AND m.row_path = '$'
        ORDER BY m.owner_version DESC;
        """;

    /// <summary>Creates the catalog acceptance savepoint.</summary>
    internal const string StartAcceptance =
        """
        SAVEPOINT catalog_acceptance;
        """;

    /// <summary>Reads a page of active startup runs.</summary>
    internal const string StartupPage =
        """
        SELECT p.run_id
        FROM reference_configuration.parameter_catalog_startup p
        JOIN reference_configuration.parameter_catalog_startup_state s ON s.run_id = p.run_id
        WHERE s.active
          AND p.row_path = '$'
          AND p.owner_version = 1
          AND ($1::bigint IS NULL OR (p.created_at_utc, p.run_id) > ($1, $2::uuid))
        ORDER BY p.created_at_utc, p.run_id
        LIMIT $3;
        """;

    /// <summary>Upserts the state of a startup run.</summary>
    internal const string StartupState =
        """
        INSERT INTO reference_configuration.parameter_catalog_startup_state (run_id, revision, active)
        VALUES ($1, $2, $3)
        ON CONFLICT (run_id) DO UPDATE SET revision = EXCLUDED.revision, active = EXCLUDED.active;
        """;

    /// <summary>Upserts the parameter set.</summary>
    internal const string UpsertParameterSet =
        """
        INSERT INTO reference_configuration.parameter_set (set_id, component_code, name, description, revision)
        VALUES ($1, $2, $3, $4, $5)
        ON CONFLICT (set_id) DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description, revision = EXCLUDED.revision;
        """;

    /// <summary>Reads a page of parameter catalog versions.</summary>
    internal const string VersionPage =
        """
        SELECT m.owner_set_id, m.owner_version, s.name, s.description, s.revision
        FROM reference_configuration.parameter_catalog_version m
        JOIN reference_configuration.parameter_set s ON s.set_id = m.owner_set_id
        WHERE s.component_code = $1
          AND ($2::uuid IS NULL OR s.set_id = $2)
          AND ($4::uuid IS NULL OR (s.name, s.set_id, -m.owner_version) > ($3, $4, -$5::bigint))
        ORDER BY s.name, s.set_id, m.owner_version DESC
        LIMIT $6;
        """;

    /// <summary>Writes the parameter projection record hash.</summary>
    internal const string WriteParameterProjectionHash =
        """
        INSERT INTO reference_configuration.parameter_projection_record
            (kind, owner_id, owner_version, content_sha256)
        VALUES ($1, $2, $3, $4)
        ON CONFLICT (kind, owner_id, owner_version) DO UPDATE SET content_sha256 = EXCLUDED.content_sha256;
        """;
}
