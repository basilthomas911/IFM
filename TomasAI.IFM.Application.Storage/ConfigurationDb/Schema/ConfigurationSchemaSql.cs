namespace TomasAI.IFM.Application.Storage.ConfigurationDb.Schema;

/// <summary>Contains PostgreSQL schema and seed statements for ConfigurationDb.</summary>
public static class ConfigurationSchemaSql
{
    /// <summary>Creates the configuration baseline application schema objects.</summary>
    internal const string CreateConfigurationBaselineApplication =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.configuration_baseline_application (
            baseline_id uuid PRIMARY KEY,
            source_event_id bigint NOT NULL UNIQUE,
            source_cut bigint NOT NULL
        );
        """;

    /// <summary>Creates the intrinsic time strategy workflow parameter set schema objects.</summary>
    internal const string CreateIntrinsicTimeStrategyWorkflowParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.intrinsic_time_strategy_workflow_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version smallint NOT NULL,
            status smallint NOT NULL,
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL,
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            CONSTRAINT pk_intrinsic_time_strategy_workflow_parameter_set PRIMARY KEY (parameter_set_id, version),
            CONSTRAINT ck_intrinsic_time_strategy_workflow_parameter_set_version CHECK (version > 0),
            CONSTRAINT ck_intrinsic_time_strategy_workflow_parameter_set_schema_version CHECK (schema_version > 0),
            CONSTRAINT ck_intrinsic_time_strategy_workflow_parameter_set_hash CHECK (length(payload_sha256) = 64)
        );
        CREATE INDEX IF NOT EXISTS ix_intrinsic_time_strategy_workflow_parameter_set_effective
        ON reference_configuration.intrinsic_time_strategy_workflow_parameter_set (status, effective_from_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_intrinsic_time_strategy_workflow_parameter_set_target_horizon_effective
        ON reference_configuration.intrinsic_time_strategy_workflow_parameter_set
        ((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the intrinsic time strategy workflow parameter set lifecycle guard schema objects.</summary>
    internal const string CreateIntrinsicTimeStrategyWorkflowParameterSetLifecycleGuard =
        """
        DO $migration$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_intrinsic_time_strategy_workflow_parameter_set_status'
                  AND conrelid = 'reference_configuration.intrinsic_time_strategy_workflow_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.intrinsic_time_strategy_workflow_parameter_set
                ADD CONSTRAINT ck_intrinsic_time_strategy_workflow_parameter_set_status CHECK (status IN (0, 1, 2));
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_intrinsic_time_strategy_workflow_parameter_set_lifecycle'
                  AND conrelid = 'reference_configuration.intrinsic_time_strategy_workflow_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.intrinsic_time_strategy_workflow_parameter_set
                ADD CONSTRAINT ck_intrinsic_time_strategy_workflow_parameter_set_lifecycle CHECK (
                    (status = 0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR
                    (status = 1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR
                    (status = 2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL
                        AND retired_at_utc >= effective_from_utc)
                );
            END IF;
        END
        $migration$;CREATE OR REPLACE FUNCTION reference_configuration.guard_intrinsic_time_strategy_workflow_parameter_set()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $function$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Market Condition parameter-set versions cannot be deleted.';
            END IF;

            IF OLD.parameter_set_id IS DISTINCT FROM NEW.parameter_set_id OR
               OLD.version IS DISTINCT FROM NEW.version OR
               OLD.schema_version IS DISTINCT FROM NEW.schema_version OR
               OLD.payload_json IS DISTINCT FROM NEW.payload_json OR
               OLD.payload_sha256 IS DISTINCT FROM NEW.payload_sha256 OR
               OLD.description IS DISTINCT FROM NEW.description OR
               OLD.created_utc IS DISTINCT FROM NEW.created_utc OR
               OLD.created_by IS DISTINCT FROM NEW.created_by THEN
                RAISE EXCEPTION 'Market Condition parameter-set version content is immutable.';
            END IF;

            IF OLD.status = 0 AND NEW.status = 1 AND
               OLD.effective_from_utc IS NULL AND NEW.effective_from_utc IS NOT NULL AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NULL THEN
                RETURN NEW;
            END IF;

            IF OLD.status = 1 AND NEW.status = 2 AND
               NEW.effective_from_utc IS NOT DISTINCT FROM OLD.effective_from_utc AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NOT NULL AND
               NEW.retired_at_utc >= NEW.effective_from_utc THEN
                RETURN NEW;
            END IF;

            RAISE EXCEPTION 'Invalid Market Condition parameter-set lifecycle transition.';
        END
        $function$;

        DROP TRIGGER IF EXISTS trg_guard_intrinsic_time_strategy_workflow_parameter_set
        ON reference_configuration.intrinsic_time_strategy_workflow_parameter_set;

        CREATE TRIGGER trg_guard_intrinsic_time_strategy_workflow_parameter_set
        BEFORE UPDATE OR DELETE ON reference_configuration.intrinsic_time_strategy_workflow_parameter_set
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_intrinsic_time_strategy_workflow_parameter_set();
        """;

    /// <summary>Creates the ix market condition parameter set effective schema objects.</summary>
    internal const string CreateIxMarketConditionParameterSetEffective =
        """
        CREATE INDEX IF NOT EXISTS ix_market_condition_parameter_set_effective
        ON reference_configuration.market_condition_parameter_set
        ((CAST(payload_json ->> 'FundId' AS integer)), (payload_json ->> 'InstrumentRoot'),
         (CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the lookup definition schema objects.</summary>
    internal const string CreateLookupDefinition =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.lookup_definition (
            id integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            group_name varchar(64) NOT NULL CHECK(group_name ~ '^[A-Za-z][A-Za-z0-9]*$'),
            internal_value varchar(64) NOT NULL CHECK(internal_value ~ '^[A-Za-z][A-Za-z0-9]*$'),
            display_name varchar(100) NOT NULL CHECK(length(trim(display_name))>0),
            description text NOT NULL DEFAULT '',
            display_order integer NOT NULL DEFAULT 0 CHECK(display_order>=0),
            is_enabled boolean NOT NULL DEFAULT true,
            created_utc timestamptz NOT NULL DEFAULT now(),
            updated_utc timestamptz NOT NULL DEFAULT now(),
            UNIQUE(group_name,internal_value)
        );
        CREATE INDEX IF NOT EXISTS ix_lookup_definition_group_order
            ON reference_configuration.lookup_definition(group_name,display_order,id);
        CREATE OR REPLACE FUNCTION reference_configuration.guard_lookup_definition() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF (NEW.id,NEW.group_name,NEW.internal_value,NEW.created_utc) IS DISTINCT FROM (OLD.id,OLD.group_name,OLD.internal_value,OLD.created_utc)
                THEN RAISE EXCEPTION 'Lookup identity and internal value are immutable'; END IF;
            NEW.updated_utc = clock_timestamp();
            RETURN NEW;
        END $$;
        CREATE OR REPLACE TRIGGER guard_lookup_definition BEFORE UPDATE ON reference_configuration.lookup_definition
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_lookup_definition();
        """;

    /// <summary>Creates the market condition assessment parameter set schema objects.</summary>
    internal const string CreateMarketConditionAssessmentParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.market_condition_assessment_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL CHECK (version > 0),
            schema_version smallint NOT NULL CHECK (schema_version > 0),
            market_profile_id text NOT NULL,
            instrument_root text NOT NULL,
            target_horizon smallint NOT NULL,
            status smallint NOT NULL CHECK (status IN (0,1,2)),
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL CHECK (length(payload_sha256)=64),
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            PRIMARY KEY (parameter_set_id, version),
            CHECK ((status=0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR (status=1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR (status=2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL AND retired_at_utc >= effective_from_utc))
        );
        CREATE INDEX IF NOT EXISTS ix_market_condition_assessment_effective
        ON reference_configuration.market_condition_assessment_parameter_set
        (market_profile_id,instrument_root,target_horizon,status,effective_from_utc DESC);CREATE OR REPLACE FUNCTION reference_configuration.guard_market_condition_assessment_parameter_set()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $function$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Market Condition parameter-set versions cannot be deleted.';
            END IF;

            IF OLD.market_profile_id IS DISTINCT FROM NEW.market_profile_id OR
               OLD.instrument_root IS DISTINCT FROM NEW.instrument_root OR
               OLD.target_horizon IS DISTINCT FROM NEW.target_horizon OR
               OLD.parameter_set_id IS DISTINCT FROM NEW.parameter_set_id OR
               OLD.version IS DISTINCT FROM NEW.version OR
               OLD.schema_version IS DISTINCT FROM NEW.schema_version OR
               OLD.payload_json IS DISTINCT FROM NEW.payload_json OR
               OLD.payload_sha256 IS DISTINCT FROM NEW.payload_sha256 OR
               OLD.description IS DISTINCT FROM NEW.description OR
               OLD.created_utc IS DISTINCT FROM NEW.created_utc OR
               OLD.created_by IS DISTINCT FROM NEW.created_by THEN
                RAISE EXCEPTION 'Market Condition parameter-set version content is immutable.';
            END IF;

            IF OLD.status = 0 AND NEW.status = 1 AND
               OLD.effective_from_utc IS NULL AND NEW.effective_from_utc IS NOT NULL AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NULL THEN
                RETURN NEW;
            END IF;

            IF OLD.status = 1 AND NEW.status = 2 AND
               NEW.effective_from_utc IS NOT DISTINCT FROM OLD.effective_from_utc AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NOT NULL AND
               NEW.retired_at_utc >= NEW.effective_from_utc THEN
                RETURN NEW;
            END IF;

            RAISE EXCEPTION 'Invalid Market Condition parameter-set lifecycle transition.';
        END
        $function$;

        DROP TRIGGER IF EXISTS trg_guard_market_condition_assessment_parameter_set
        ON reference_configuration.market_condition_assessment_parameter_set;

        CREATE TRIGGER trg_guard_market_condition_assessment_parameter_set
        BEFORE UPDATE OR DELETE ON reference_configuration.market_condition_assessment_parameter_set
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_market_condition_assessment_parameter_set();
        """;

    /// <summary>Creates the market condition parameter set schema objects.</summary>
    internal const string CreateMarketConditionParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.market_condition_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version smallint NOT NULL,
            status smallint NOT NULL,
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL,
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            CONSTRAINT pk_market_condition_parameter_set PRIMARY KEY (parameter_set_id, version),
            CONSTRAINT ck_market_condition_parameter_set_version CHECK (version > 0),
            CONSTRAINT ck_market_condition_parameter_set_schema_version CHECK (schema_version > 0),
            CONSTRAINT ck_market_condition_parameter_set_hash CHECK (length(payload_sha256) = 64)
        );
        CREATE INDEX IF NOT EXISTS ix_market_condition_parameter_set_effective
        ON reference_configuration.market_condition_parameter_set (status, effective_from_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_market_condition_parameter_set_target_horizon_effective
        ON reference_configuration.market_condition_parameter_set
        ((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the market condition parameter set lifecycle constraints schema objects.</summary>
    internal const string CreateMarketConditionParameterSetLifecycleConstraints =
        """
        DO $migration$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_market_condition_parameter_set_status'
                  AND conrelid = 'reference_configuration.market_condition_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.market_condition_parameter_set
                ADD CONSTRAINT ck_market_condition_parameter_set_status CHECK (status IN (0, 1, 2));
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_market_condition_parameter_set_lifecycle'
                  AND conrelid = 'reference_configuration.market_condition_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.market_condition_parameter_set
                ADD CONSTRAINT ck_market_condition_parameter_set_lifecycle CHECK (
                    (status = 0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR
                    (status = 1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR
                    (status = 2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL
                        AND retired_at_utc >= effective_from_utc)
                );
            END IF;
        END
        $migration$;
        """;

    /// <summary>Creates the market condition parameter set lifecycle guard schema objects.</summary>
    internal const string CreateMarketConditionParameterSetLifecycleGuard =
        """
        CREATE OR REPLACE FUNCTION reference_configuration.guard_market_condition_parameter_set()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $function$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Market Condition parameter-set versions cannot be deleted.';
            END IF;

            IF OLD.parameter_set_id IS DISTINCT FROM NEW.parameter_set_id OR
               OLD.version IS DISTINCT FROM NEW.version OR
               OLD.schema_version IS DISTINCT FROM NEW.schema_version OR
               OLD.payload_json IS DISTINCT FROM NEW.payload_json OR
               OLD.payload_sha256 IS DISTINCT FROM NEW.payload_sha256 OR
               OLD.description IS DISTINCT FROM NEW.description OR
               OLD.created_utc IS DISTINCT FROM NEW.created_utc OR
               OLD.created_by IS DISTINCT FROM NEW.created_by THEN
                RAISE EXCEPTION 'Market Condition parameter-set version content is immutable.';
            END IF;

            IF OLD.status = 0 AND NEW.status = 1 AND
               OLD.effective_from_utc IS NULL AND NEW.effective_from_utc IS NOT NULL AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NULL THEN
                RETURN NEW;
            END IF;

            IF OLD.status = 1 AND NEW.status = 2 AND
               NEW.effective_from_utc IS NOT DISTINCT FROM OLD.effective_from_utc AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NOT NULL AND
               NEW.retired_at_utc >= NEW.effective_from_utc THEN
                RETURN NEW;
            END IF;

            RAISE EXCEPTION 'Invalid Market Condition parameter-set lifecycle transition.';
        END
        $function$;

        DROP TRIGGER IF EXISTS trg_guard_market_condition_parameter_set
        ON reference_configuration.market_condition_parameter_set;

        CREATE TRIGGER trg_guard_market_condition_parameter_set
        BEFORE UPDATE OR DELETE ON reference_configuration.market_condition_parameter_set
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_market_condition_parameter_set();
        """;

    /// <summary>Creates the order composition parameter set schema objects.</summary>
    internal const string CreateOrderCompositionParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.order_composition_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version smallint NOT NULL,
            status smallint NOT NULL,
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL,
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            CONSTRAINT pk_order_composition_parameter_set PRIMARY KEY (parameter_set_id, version),
            CONSTRAINT ck_order_composition_parameter_set_version CHECK (version > 0),
            CONSTRAINT ck_order_composition_parameter_set_schema_version CHECK (schema_version > 0),
            CONSTRAINT ck_order_composition_parameter_set_hash CHECK (length(payload_sha256) = 64)
        );
        CREATE INDEX IF NOT EXISTS ix_order_composition_parameter_set_effective
        ON reference_configuration.order_composition_parameter_set (status, effective_from_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_order_composition_parameter_set_target_horizon_effective
        ON reference_configuration.order_composition_parameter_set
        ((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the order composition parameter set lifecycle guard schema objects.</summary>
    internal const string CreateOrderCompositionParameterSetLifecycleGuard =
        """
        DO $migration$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_order_composition_parameter_set_status'
                  AND conrelid = 'reference_configuration.order_composition_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.order_composition_parameter_set
                ADD CONSTRAINT ck_order_composition_parameter_set_status CHECK (status IN (0, 1, 2));
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_order_composition_parameter_set_lifecycle'
                  AND conrelid = 'reference_configuration.order_composition_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.order_composition_parameter_set
                ADD CONSTRAINT ck_order_composition_parameter_set_lifecycle CHECK (
                    (status = 0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR
                    (status = 1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR
                    (status = 2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL
                        AND retired_at_utc >= effective_from_utc)
                );
            END IF;
        END
        $migration$;CREATE OR REPLACE FUNCTION reference_configuration.guard_order_composition_parameter_set()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $function$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Market Condition parameter-set versions cannot be deleted.';
            END IF;

            IF OLD.parameter_set_id IS DISTINCT FROM NEW.parameter_set_id OR
               OLD.version IS DISTINCT FROM NEW.version OR
               OLD.schema_version IS DISTINCT FROM NEW.schema_version OR
               OLD.payload_json IS DISTINCT FROM NEW.payload_json OR
               OLD.payload_sha256 IS DISTINCT FROM NEW.payload_sha256 OR
               OLD.description IS DISTINCT FROM NEW.description OR
               OLD.created_utc IS DISTINCT FROM NEW.created_utc OR
               OLD.created_by IS DISTINCT FROM NEW.created_by THEN
                RAISE EXCEPTION 'Market Condition parameter-set version content is immutable.';
            END IF;

            IF OLD.status = 0 AND NEW.status = 1 AND
               OLD.effective_from_utc IS NULL AND NEW.effective_from_utc IS NOT NULL AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NULL THEN
                RETURN NEW;
            END IF;

            IF OLD.status = 1 AND NEW.status = 2 AND
               NEW.effective_from_utc IS NOT DISTINCT FROM OLD.effective_from_utc AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NOT NULL AND
               NEW.retired_at_utc >= NEW.effective_from_utc THEN
                RETURN NEW;
            END IF;

            RAISE EXCEPTION 'Invalid Market Condition parameter-set lifecycle transition.';
        END
        $function$;

        DROP TRIGGER IF EXISTS trg_guard_order_composition_parameter_set
        ON reference_configuration.order_composition_parameter_set;

        CREATE TRIGGER trg_guard_order_composition_parameter_set
        BEFORE UPDATE OR DELETE ON reference_configuration.order_composition_parameter_set
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_order_composition_parameter_set();
        """;

    /// <summary>Creates the parameter catalog assignment schema objects.</summary>
    internal const string CreateParameterCatalogAssignment =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_assignment (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_assignment' CHECK(owner_kind='parameter_catalog_assignment'),
            assignment_id uuid,
            has_scope boolean,
            revision bigint,
            has_reference boolean,
            enabled boolean,
            application_policy integer,
            created_at_utc bigint,
            created_by text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter catalog assignment reference schema objects.</summary>
    internal const string CreateParameterCatalogAssignmentReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_assignment_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_assignment' CHECK(owner_kind='parameter_catalog_assignment'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_assignment(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog assignment scope schema objects.</summary>
    internal const string CreateParameterCatalogAssignmentScope =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_assignment_scope (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_assignment' CHECK(owner_kind='parameter_catalog_assignment'),
            consumer_kind_code text,
            consumer_id text,
            role text,
            component_code text,
            scope_json text,
            scope_sha256 text,
            target_horizon integer,
            strategy text,
            codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_assignment(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit schema objects.</summary>
    internal const string CreateParameterCatalogAudit =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            operation_id uuid,
            entity_id uuid,
            revision bigint,
            action text,
            actor_identity text,
            recorded_at_utc bigint,
            before_json text,
            after_json text,
            has_before boolean,
            has_after boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter catalog audit after schema objects.</summary>
    internal const string CreateParameterCatalogAuditAfter =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_after (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            has_reference boolean,
            name text,
            description text,
            schema_version integer,
            status integer,
            has_assignment boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit after assignment schema objects.</summary>
    internal const string CreateParameterCatalogAuditAfterAssignment =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_after_assignment (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            assignment_id uuid,
            has_scope boolean,
            revision bigint,
            has_reference boolean,
            enabled boolean,
            application_policy integer,
            created_at_utc bigint,
            created_by text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_after(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit after assignment reference schema objects.</summary>
    internal const string CreateParameterCatalogAuditAfterAssignmentReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_after_assignment_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_after_assignment(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit after assignment scope schema objects.</summary>
    internal const string CreateParameterCatalogAuditAfterAssignmentScope =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_after_assignment_scope (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            consumer_kind_code text,
            consumer_id text,
            role text,
            component_code text,
            scope_json text,
            scope_sha256 text,
            target_horizon integer,
            strategy text,
            codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_after_assignment(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit after reference schema objects.</summary>
    internal const string CreateParameterCatalogAuditAfterReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_after_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_after(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit before schema objects.</summary>
    internal const string CreateParameterCatalogAuditBefore =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_before (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            has_reference boolean,
            name text,
            description text,
            schema_version integer,
            status integer,
            has_assignment boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit before assignment schema objects.</summary>
    internal const string CreateParameterCatalogAuditBeforeAssignment =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_before_assignment (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            assignment_id uuid,
            has_scope boolean,
            revision bigint,
            has_reference boolean,
            enabled boolean,
            application_policy integer,
            created_at_utc bigint,
            created_by text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_before(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit before assignment reference schema objects.</summary>
    internal const string CreateParameterCatalogAuditBeforeAssignmentReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_before_assignment_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_before_assignment(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit before assignment scope schema objects.</summary>
    internal const string CreateParameterCatalogAuditBeforeAssignmentScope =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_before_assignment_scope (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            consumer_kind_code text,
            consumer_id text,
            role text,
            component_code text,
            scope_json text,
            scope_sha256 text,
            target_horizon integer,
            strategy text,
            codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_before_assignment(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog audit before reference schema objects.</summary>
    internal const string CreateParameterCatalogAuditBeforeReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_audit_before_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_audit' CHECK(owner_kind='parameter_catalog_audit'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_audit_before(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup schema objects.</summary>
    internal const string CreateParameterCatalogStartup =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            run_id uuid,
            has_scopes boolean,
            has_versions boolean,
            has_plan boolean,
            created_at_utc bigint,
            created_by text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter catalog startup plan schema objects.</summary>
    internal const string CreateParameterCatalogStartupPlan =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_plan (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            startup_run_id uuid,
            assignment_fingerprint text,
            fingerprint text,
            has_steps boolean,
            has_issues boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup plan issues schema objects.</summary>
    internal const string CreateParameterCatalogStartupPlanIssues =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_plan_issues (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            value text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_plan(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup plan steps schema objects.</summary>
    internal const string CreateParameterCatalogStartupPlanSteps =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_plan_steps (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            has_key boolean,
            prepare boolean,
            monitor boolean,
            has_consumers boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_plan(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup plan steps consumers schema objects.</summary>
    internal const string CreateParameterCatalogStartupPlanStepsConsumers =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_plan_steps_consumers (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            has_key boolean,
            consumer text,
            requirement_id uuid,
            prepare boolean,
            monitor boolean,
            maximum_age_seconds integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_plan_steps(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup plan steps consumers key schema objects.</summary>
    internal const string CreateParameterCatalogStartupPlanStepsConsumersKey =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_plan_steps_consumers_key (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            producer integer,
            interval integer,
            period integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_plan_steps_consumers(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup plan steps key schema objects.</summary>
    internal const string CreateParameterCatalogStartupPlanStepsKey =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_plan_steps_key (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            producer integer,
            interval integer,
            period integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_plan_steps(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup report schema objects.</summary>
    internal const string CreateParameterCatalogStartupReport =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_report (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup_report' CHECK(owner_kind='parameter_catalog_startup_report'),
            run_id uuid,
            plan_fingerprint text,
            value_date date,
            contract_id text,
            recorded_at_utc bigint,
            has_outcomes boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter catalog startup report outcomes schema objects.</summary>
    internal const string CreateParameterCatalogStartupReportOutcomes =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_report_outcomes (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup_report' CHECK(owner_kind='parameter_catalog_startup_report'),
            has_key boolean,
            status integer,
            detail text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_report(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup report outcomes key schema objects.</summary>
    internal const string CreateParameterCatalogStartupReportOutcomesKey =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_report_outcomes_key (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup_report' CHECK(owner_kind='parameter_catalog_startup_report'),
            producer integer,
            interval integer,
            period integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_report_outcomes(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup scopes schema objects.</summary>
    internal const string CreateParameterCatalogStartupScopes =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_scopes (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            assignment_id uuid,
            has_scope boolean,
            revision bigint,
            has_reference boolean,
            enabled boolean,
            application_policy integer,
            created_at_utc bigint,
            created_by text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup scopes reference schema objects.</summary>
    internal const string CreateParameterCatalogStartupScopesReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_scopes_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_scopes(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup scopes scope schema objects.</summary>
    internal const string CreateParameterCatalogStartupScopesScope =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_scopes_scope (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            consumer_kind_code text,
            consumer_id text,
            role text,
            component_code text,
            scope_json text,
            scope_sha256 text,
            target_horizon integer,
            strategy text,
            codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_scopes(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup versions schema objects.</summary>
    internal const string CreateParameterCatalogStartupVersions =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_versions (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            has_reference boolean,
            name text,
            description text,
            schema_version integer,
            status integer,
            created_at_utc bigint,
            created_by text,
            published_at_utc bigint,
            retired_at_utc bigint,
            catalog_revision bigint,
            has_legacy_source boolean,
            reference_codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup versions legacy source schema objects.</summary>
    internal const string CreateParameterCatalogStartupVersionsLegacySource =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_versions_legacy_source (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            kind text,
            set_id uuid,
            version integer,
            payload_sha256 text,
            codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_versions(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog startup versions reference schema objects.</summary>
    internal const string CreateParameterCatalogStartupVersionsReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_versions_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_startup' CHECK(owner_kind='parameter_catalog_startup'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_startup_versions(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog version schema objects.</summary>
    internal const string CreateParameterCatalogVersion =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_version (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_version' CHECK(owner_kind='parameter_catalog_version'),
            has_reference boolean,
            name text,
            description text,
            schema_version integer,
            status integer,
            created_at_utc bigint,
            created_by text,
            published_at_utc bigint,
            retired_at_utc bigint,
            catalog_revision bigint,
            has_legacy_source boolean,
            reference_codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter catalog version legacy source schema objects.</summary>
    internal const string CreateParameterCatalogVersionLegacySource =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_version_legacy_source (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_version' CHECK(owner_kind='parameter_catalog_version'),
            kind text,
            set_id uuid,
            version integer,
            payload_sha256 text,
            codec text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_version(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter catalog version reference schema objects.</summary>
    internal const string CreateParameterCatalogVersionReference =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_version_reference (
            owner_set_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            owner_kind text NOT NULL DEFAULT 'parameter_catalog_version' CHECK(owner_kind='parameter_catalog_version'),
            set_id uuid,
            version integer,
            component_code text,
            payload_sha256 text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_kind,owner_set_id,owner_version) REFERENCES reference_configuration.parameter_projection_record(kind,owner_id,owner_version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_catalog_version(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter futures iti signal schema objects.</summary>
    internal const string CreateParameterFuturesItiSignal =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_futures_iti_signal (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            parameter_set_id uuid,
            version integer,
            schema_version integer,
            has_default_trading_days boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter futures iti signal default trading days schema objects.</summary>
    internal const string CreateParameterFuturesItiSignalDefaultTradingDays =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_futures_iti_signal_default_trading_days (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            daily integer,
            weekly integer,
            monthly integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_futures_iti_signal(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter iron condor market selection schema objects.</summary>
    internal const string CreateParameterIronCondorMarketSelection =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_iron_condor_market_selection (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            schema_version integer,
            parameter_set_id uuid,
            version integer,
            default_symbol text,
            has_symbols boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter iron condor market selection symbols schema objects.</summary>
    internal const string CreateParameterIronCondorMarketSelectionSymbols =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_iron_condor_market_selection_symbols (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            symbol text,
            short_call_delta integer,
            call_spread_width numeric,
            short_put_delta integer,
            put_spread_width numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_iron_condor_market_selection(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter option volatility consumer rules schema objects.</summary>
    internal const string CreateParameterOptionVolatilityConsumerRules =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_consumer_rules (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            schema_version integer,
            parameter_set_id uuid,
            version integer,
            series_id text,
            methodology_version text,
            metric_policy_version text,
            open_market_evidence integer,
            live_maximum_age_minutes integer,
            closed_market_evidence integer,
            maximum_session_lag integer,
            unavailable_policy integer,
            exact_decision_snapshot_required boolean,
            allow_downstream_silent_replacement boolean,
            has_numerical_rules boolean,
            iron_condor integer,
            vertical_spread integer,
            futures_outright integer,
            held_options integer,
            protective_close integer,
            protective_cancel integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter option volatility consumer rules numerical rules schema objects.</summary>
    internal const string CreateParameterOptionVolatilityConsumerRulesNumericalRules =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_consumer_rules_numerical_rules (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            minimum_iv_rank numeric,
            maximum_iv_rank numeric,
            minimum_iv_percentile numeric,
            maximum_iv_percentile numeric,
            delta_target_adjustment numeric,
            spread_width_adjustment numeric,
            risk_limit_adjustment numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_option_volatility_consumer_rules(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter option volatility retention schema objects.</summary>
    internal const string CreateParameterOptionVolatilityRetention =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_retention (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            schema_version integer,
            parameter_set_id uuid,
            version integer,
            source_observation_years integer,
            finalized_daily_metric_years integer,
            intraday_checkpoint_days integer,
            decision_snapshot_years_after_completion integer,
            referenced_evidence_policy integer,
            latest_pointer_lifetime integer,
            definition_lifetime integer,
            published_parameter_lifetime integer,
            correction_policy integer,
            backfill_staging_days integer,
            failed_publication_days integer,
            partition_bucket integer,
            maximum_page_size integer,
            has_historical_sources boolean,
            trade_iv_backfill integer,
            allow_unqualified_backfill_publication boolean,
            has_isolated_environments boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter option volatility retention historical sources schema objects.</summary>
    internal const string CreateParameterOptionVolatilityRetentionHistoricalSources =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_retention_historical_sources (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_option_volatility_retention(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter option volatility retention isolated environments schema objects.</summary>
    internal const string CreateParameterOptionVolatilityRetentionIsolatedEnvironments =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_retention_isolated_environments (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_option_volatility_retention(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter option volatility series schema objects.</summary>
    internal const string CreateParameterOptionVolatilitySeries =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_series (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            schema_version integer,
            parameter_set_id uuid,
            version integer,
            series_id text,
            methodology_version text,
            underlying_root text,
            venue text,
            currency text,
            environment integer,
            market_data_provider text,
            dataset text,
            has_eligible_product_families boolean,
            target_maturity_calendar_days integer,
            atm_convention integer,
            option_combination integer,
            expiry_selection integer,
            interpolation integer,
            underlying_match integer,
            quote_mark integer,
            minimum_displayed_size integer,
            crossed_market_policy integer,
            locked_market_policy integer,
            maximum_quote_age_seconds integer,
            maximum_iv_age_seconds integer,
            maximum_quote_skew_milliseconds integer,
            has_pricers boolean,
            roll_policy integer,
            exchange_calendar text,
            time_zone text,
            daily_sampling_policy integer,
            intraday_coalescing_minutes integer,
            maximum_intraday_checkpoints integer,
            historical_lookback_sessions integer,
            minimum_valid_observations integer,
            minimum_coverage numeric,
            gap_policy integer,
            rank_bounds integer,
            percentile_convention integer,
            implied_volatility_unit integer,
            rank_and_percentile_unit integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter option volatility series eligible product families schema objects.</summary>
    internal const string CreateParameterOptionVolatilitySeriesEligibleProductFamilies =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_series_eligible_product_families (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_option_volatility_series(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter option volatility series pricers schema objects.</summary>
    internal const string CreateParameterOptionVolatilitySeriesPricers =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_series_pricers (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            exercise_style integer,
            has_pricer_versions boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_option_volatility_series(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter option volatility series pricers pricer versions schema objects.</summary>
    internal const string CreateParameterOptionVolatilitySeriesPricersPricerVersions =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_option_volatility_series_pricers_pricer_versions (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_option_volatility_series_pricers(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter projection pending schema objects.</summary>
    internal const string CreateParameterProjectionPending =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_projection_pending (
            event_id bigint PRIMARY KEY CHECK(event_id > 0)
        );
        """;

    /// <summary>Creates the parameter projection record schema objects.</summary>
    internal const string CreateParameterProjectionRecord =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_projection_record (
            kind text NOT NULL,
            owner_id uuid NOT NULL,
            owner_version bigint NOT NULL,
            content_sha256 text NOT NULL,
            PRIMARY KEY(kind,owner_id,owner_version),
            CHECK(length(content_sha256)=64)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_operation (
            operation_id uuid PRIMARY KEY,
            entity_id uuid NOT NULL,
            revision bigint NOT NULL,
            request_sha256 text NOT NULL,
            kind text NOT NULL,
            version integer NOT NULL,
            committed_at_ticks bigint NOT NULL,
            fact_sha256 text NOT NULL,
            UNIQUE(kind,entity_id,revision)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_migration (
            migration_id smallint PRIMARY KEY,
            source_cut bigint NOT NULL
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_catalog_startup_state (
            run_id uuid PRIMARY KEY,
            revision bigint NOT NULL,
            active boolean NOT NULL
        );
        """;

    /// <summary>Creates the parameter regime discovery schema objects.</summary>
    internal const string CreateParameterRegimeDiscovery =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            parameter_set_id uuid,
            version integer,
            schema_version integer,
            strategy_parameter_set_id uuid,
            strategy_parameter_set_version integer,
            target_horizon integer,
            has_horizon boolean,
            has_trend boolean,
            has_volatility boolean,
            has_market_structure boolean,
            has_fusion boolean,
            has_freshness boolean,
            has_data_quality boolean,
            has_signal_requirements boolean,
            has_signal_metrics boolean,
            has_observation_metrics boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter regime discovery data quality schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryDataQuality =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_data_quality (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            has_supported_signal_schema_versions boolean,
            has_approved_calculation_versions boolean,
            snapshot_capture_attempts integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery data quality approved c 65f3d75001b7 schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryDataQualityApprovedC65f3d75001b7 =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_data_quality_approved_c_65f3d75001b7 (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value text,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery_data_quality(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery data quality supported  450c80e543a1 schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryDataQualitySupported450c80e543a1 =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_data_quality_supported__450c80e543a1 (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery_data_quality(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery freshness schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryFreshness =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_freshness (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            future_clock_skew_seconds integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery fusion schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryFusion =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_fusion (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            trend_directional_weight numeric,
            market_structure_directional_weight numeric,
            trend_confidence_weight numeric,
            volatility_confidence_weight numeric,
            market_structure_confidence_weight numeric,
            volatility_conviction_penalty numeric,
            direction_threshold numeric,
            low_confidence_restriction_threshold numeric,
            acceptable_quality_threshold numeric,
            high_quality_threshold numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery horizon schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryHorizon =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_horizon (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            target_horizon integer,
            has_time_frames boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery horizon time frames schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryHorizonTimeFrames =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_horizon_time_frames (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            time_frame integer,
            weight numeric,
            is_required boolean,
            maximum_age_seconds integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery_horizon(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery market structure schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryMarketStructure =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_market_structure (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            bollinger_weight numeric,
            ema_interaction_weight numeric,
            atr_range_weight numeric,
            breakout_weight numeric,
            iti_weight numeric,
            breakout_atr_threshold numeric,
            compression_width_ratio numeric,
            compression_atr_ratio numeric,
            expansion_width_ratio numeric,
            expansion_atr_ratio numeric,
            trending_organization_threshold numeric,
            trending_persistence_threshold numeric,
            ranging_organization_threshold numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery observation metrics schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryObservationMetrics =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_observation_metrics (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            requirement_id uuid,
            metric integer,
            enabled boolean,
            maximum_age_seconds integer,
            prepare_at_startup boolean,
            monitor boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery signal metrics schema objects.</summary>
    internal const string CreateParameterRegimeDiscoverySignalMetrics =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_signal_metrics (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            requirement_id uuid,
            metric integer,
            time_frame integer,
            period_length integer,
            enabled boolean,
            maximum_age_seconds integer,
            prepare_at_startup boolean,
            monitor boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery signal requirements schema objects.</summary>
    internal const string CreateParameterRegimeDiscoverySignalRequirements =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_signal_requirements (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            requirement_id uuid,
            metric integer,
            time_frame integer,
            enabled boolean,
            is_required boolean,
            maximum_age_seconds integer,
            calculation_configuration_id text,
            prepare_at_startup boolean,
            monitor boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery trend schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryTrend =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_trend (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            ema_alignment_weight numeric,
            ema_slope_weight numeric,
            rsi_weight numeric,
            adx_weight numeric,
            macd_weight numeric,
            iti_weight numeric,
            ema_slope_scale numeric,
            rsi_slope_scale numeric,
            macd_atr_scale numeric,
            direction_threshold numeric,
            moderate_threshold numeric,
            strong_threshold numeric,
            extreme_threshold numeric,
            exhausting_reversal_threshold numeric,
            reversing_threshold numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter regime discovery volatility schema objects.</summary>
    internal const string CreateParameterRegimeDiscoveryVolatility =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_regime_discovery_volatility (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            vix_weight numeric,
            atr_ratio_weight numeric,
            term_structure_weight numeric,
            realized_volatility_weight numeric,
            vix_normal_boundary numeric,
            vix_high_boundary numeric,
            vix_extreme_boundary numeric,
            vix_maximum numeric,
            expansion_threshold numeric,
            severe_backwardation_ratio numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_regime_discovery(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter registry schema objects.</summary>
    internal const string CreateParameterRegistry =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_area (
            area_id uuid PRIMARY KEY,
            code text NOT NULL UNIQUE,
            name text NOT NULL,
            enabled boolean NOT NULL DEFAULT true
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_component (
            component_id uuid PRIMARY KEY,
            code text NOT NULL UNIQUE,
            area_id uuid NOT NULL REFERENCES reference_configuration.parameter_area(area_id),
            name text NOT NULL,
            editor_code text NOT NULL,
            enabled boolean NOT NULL DEFAULT true
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_schema_version (
            component_code text NOT NULL REFERENCES reference_configuration.parameter_component(code),
            schema_version integer NOT NULL CHECK(schema_version>0),
            codec text NOT NULL,
            schema_sha256 text NOT NULL CHECK(schema_sha256 ~ '^[0-9a-f]{64}$'),
            schema_json jsonb NOT NULL CHECK(jsonb_typeof(schema_json)='object'),
            PRIMARY KEY(component_code,schema_version)
        );
        INSERT INTO reference_configuration.parameter_area(area_id,code,name)
        VALUES(md5('parameter-area:strategy-workflow')::uuid,'strategy-workflow','Strategy Workflow') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_area(area_id,code,name)
        VALUES(md5('parameter-area:market-data-analytics')::uuid,'market-data-analytics','Market Data Analytics') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_area(area_id,code,name)
        VALUES(md5('parameter-area:option-volatility')::uuid,'option-volatility','Option Volatility') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_area(area_id,code,name)
        VALUES(md5('parameter-area:option-spread-strategy')::uuid,'option-spread-strategy','Option Spread Strategy Defaults') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:strategy-workflow.regime-discovery')::uuid,'strategy-workflow.regime-discovery',md5('parameter-area:strategy-workflow')::uuid,'Regime Discovery','regime-discovery') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:market-data-analytics.futures-iti-signal')::uuid,'market-data-analytics.futures-iti-signal',md5('parameter-area:market-data-analytics')::uuid,'Future ITI Signal','scalar-properties') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:option-volatility.series')::uuid,'option-volatility.series',md5('parameter-area:option-volatility')::uuid,'Series','scalar-properties') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:option-volatility.consumer-rules')::uuid,'option-volatility.consumer-rules',md5('parameter-area:option-volatility')::uuid,'Consumer Rules','scalar-properties') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:option-volatility.retention')::uuid,'option-volatility.retention',md5('parameter-area:option-volatility')::uuid,'Retention','scalar-properties') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:option-spread-strategy.iron-condor-defaults')::uuid,'option-spread-strategy.iron-condor-defaults',md5('parameter-area:option-spread-strategy')::uuid,'Iron Condor','scalar-properties') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:option-spread-strategy.vertical-spread-defaults')::uuid,'option-spread-strategy.vertical-spread-defaults',md5('parameter-area:option-spread-strategy')::uuid,'Vertical Spreads','scalar-properties') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_area(area_id,code,name)
        VALUES(md5('parameter-area:market-data')::uuid,'market-data','Market Data') ON CONFLICT DO NOTHING;
        INSERT INTO reference_configuration.parameter_component(component_id,code,area_id,name,editor_code)
        VALUES(md5('parameter-component:market-data.strategy-option-chain-cache')::uuid,'market-data.strategy-option-chain-cache',md5('parameter-area:market-data')::uuid,'Strategy Option Chain Cache','scalar-properties') ON CONFLICT DO NOTHING;
        CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_schema() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'PARAM.SCHEMA_IMMUTABLE'; END; $$;
        DROP TRIGGER IF EXISTS parameter_schema_guard ON reference_configuration.parameter_schema_version;
        CREATE TRIGGER parameter_schema_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_schema_version
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_schema();
        """;

    /// <summary>Creates the parameter revision fence schema objects.</summary>
    internal const string CreateParameterRevisionFence =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_revision_fence (
            singleton boolean PRIMARY KEY DEFAULT true CHECK(singleton),
            revision bigint NOT NULL CHECK(revision>=0)
        );
        INSERT INTO reference_configuration.parameter_revision_fence(singleton,revision) VALUES(true,0) ON CONFLICT(singleton) DO NOTHING;
        """;

    /// <summary>Creates the parameter sets schema objects.</summary>
    internal const string CreateParameterSets =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_legacy_reference (
            legacy_kind text NOT NULL,
            legacy_set_id uuid NOT NULL,
            legacy_version integer NOT NULL,
            generic_set_id uuid NOT NULL,
            generic_version integer NOT NULL,
            legacy_sha256 text NOT NULL,
            legacy_codec text NOT NULL,
            PRIMARY KEY(legacy_kind,legacy_set_id,legacy_version)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_startup_report (
            run_id uuid NOT NULL,
            revision bigint NOT NULL CHECK(revision>0),
            body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'),
            PRIMARY KEY(run_id,revision)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_startup_run (
            run_id uuid PRIMARY KEY,
            body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'),
            active boolean NOT NULL DEFAULT true
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_set_audit (
            operation_id uuid PRIMARY KEY,
            entity_id uuid NOT NULL,
            revision bigint NOT NULL CHECK(revision>0),
            body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'),
            UNIQUE(entity_id,revision)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_set (
            set_id uuid PRIMARY KEY,
            component_code text NOT NULL,
            name text NOT NULL,
            description text NOT NULL,
            revision bigint NOT NULL CHECK(revision>0)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_set_version (
            set_id uuid NOT NULL REFERENCES reference_configuration.parameter_set(set_id),
            version integer NOT NULL CHECK(version>0),
            payload_sha256 text NOT NULL CHECK(length(payload_sha256)=64),
            status smallint NOT NULL CHECK(status IN(0,1,2)),
            body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'),
            PRIMARY KEY(set_id,version)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_operation (
            operation_id uuid PRIMARY KEY,
            set_id uuid NOT NULL REFERENCES reference_configuration.parameter_set(set_id),
            revision bigint NOT NULL,
            request_sha256 text NOT NULL,
            version integer NOT NULL,
            committed_at_utc timestamptz NOT NULL DEFAULT now(),
            UNIQUE(set_id,revision)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_assignment (
            assignment_id uuid PRIMARY KEY,
            revision bigint NOT NULL CHECK(revision>0),
            body jsonb NOT NULL CHECK(jsonb_typeof(body)='object')
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_assignment_revision (
            assignment_id uuid NOT NULL REFERENCES reference_configuration.parameter_assignment(assignment_id),
            revision bigint NOT NULL CHECK(revision>0),
            operation_id uuid NOT NULL UNIQUE,
            request_sha256 text NOT NULL CHECK(request_sha256 ~ '^[0-9a-f]{64}$'),
            body jsonb NOT NULL CHECK(jsonb_typeof(body)='object'),
            PRIMARY KEY(assignment_id,revision)
        );
        CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_version() RETURNS trigger
        LANGUAGE plpgsql AS $$
        BEGIN
          IF TG_OP = 'DELETE' THEN
            RAISE EXCEPTION 'PARAM.VERSION_IMMUTABLE';
          END IF;
          IF TG_OP = 'UPDATE' THEN
            IF NEW.set_id <> OLD.set_id OR NEW.version <> OLD.version
              OR NEW.payload_sha256 <> OLD.payload_sha256
              OR NEW.body->'PayloadJson' IS DISTINCT FROM OLD.body->'PayloadJson'
              OR NEW.body->'SchemaVersion' IS DISTINCT FROM OLD.body->'SchemaVersion'
              OR COALESCE(NEW.body->'LegacySource','null'::jsonb) IS DISTINCT FROM COALESCE(OLD.body->'LegacySource','null'::jsonb)
              OR NEW.body->'Reference' IS DISTINCT FROM OLD.body->'Reference'
              OR NEW.body->'CreatedAtUtc' IS DISTINCT FROM OLD.body->'CreatedAtUtc'
              OR NEW.body->'CreatedBy' IS DISTINCT FROM OLD.body->'CreatedBy' THEN
              RAISE EXCEPTION 'PARAM.VERSION_IMMUTABLE';
            END IF;
            IF NEW.status <> OLD.status AND NOT ((OLD.status=0 AND NEW.status=1) OR (OLD.status=1 AND NEW.status=2)) THEN
              RAISE EXCEPTION 'PARAM.LIFECYCLE_INVALID';
            END IF;
          END IF;
          IF NEW.payload_sha256 !~ '^[0-9a-f]{64}$'
            OR (NEW.body->'Reference'->>'SetId') IS DISTINCT FROM NEW.set_id::text
            OR (NEW.body->'Reference'->>'Version')::integer IS DISTINCT FROM NEW.version
            OR (NEW.body->'Reference'->>'PayloadSha256') IS DISTINCT FROM NEW.payload_sha256
            OR (NEW.body->>'Status')::integer IS DISTINCT FROM NEW.status::integer THEN
            RAISE EXCEPTION 'PARAM.VERSION_ENVELOPE_INVALID';
          END IF;
          IF NOT EXISTS (SELECT 1 FROM reference_configuration.parameter_set p
             JOIN reference_configuration.parameter_schema_version s ON s.component_code=p.component_code
             WHERE p.set_id=NEW.set_id AND s.schema_version=(NEW.body->>'SchemaVersion')::integer
              AND p.component_code=NEW.body->'Reference'->>'ComponentCode') THEN
             RAISE EXCEPTION 'PARAM.SCHEMA_UNREGISTERED';
          END IF;
          RETURN NEW;
        END;
        $$;
        DROP TRIGGER IF EXISTS parameter_version_guard ON reference_configuration.parameter_set_version;
        CREATE TRIGGER parameter_version_guard BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.parameter_set_version
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_version();
        CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_startup() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
         IF TG_OP='DELETE' THEN RAISE EXCEPTION 'PARAM.STARTUP_IMMUTABLE'; END IF;
         IF NEW.run_id IS DISTINCT FROM OLD.run_id OR NEW.body IS DISTINCT FROM OLD.body OR (NOT OLD.active AND NEW.active) THEN
          RAISE EXCEPTION 'PARAM.STARTUP_IMMUTABLE'; END IF;
         RETURN NEW;
        END; $$;
        DROP TRIGGER IF EXISTS parameter_startup_guard ON reference_configuration.parameter_startup_run;
        CREATE TRIGGER parameter_startup_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_startup_run
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_startup();
        CREATE OR REPLACE FUNCTION reference_configuration.guard_parameter_history() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'PARAM.HISTORY_IMMUTABLE'; END; $$;
        DROP TRIGGER IF EXISTS parameter_audit_guard ON reference_configuration.parameter_set_audit;
        CREATE TRIGGER parameter_audit_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_set_audit
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
        DROP TRIGGER IF EXISTS parameter_assignment_revision_guard ON reference_configuration.parameter_assignment_revision;
        CREATE TRIGGER parameter_assignment_revision_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_assignment_revision
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
        DROP TRIGGER IF EXISTS parameter_startup_report_guard ON reference_configuration.parameter_startup_report;
        CREATE TRIGGER parameter_startup_report_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_startup_report
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
        DROP TRIGGER IF EXISTS parameter_legacy_guard ON reference_configuration.parameter_legacy_reference;
        CREATE TRIGGER parameter_legacy_guard BEFORE UPDATE OR DELETE ON reference_configuration.parameter_legacy_reference
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_parameter_history();
        CREATE INDEX IF NOT EXISTS ix_parameter_set_component ON reference_configuration.parameter_set(component_code,name,set_id);
        """;

    /// <summary>Creates the parameter strategy option chain schema objects.</summary>
    internal const string CreateParameterStrategyOptionChain =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_strategy_option_chain (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            parameter_set_id uuid,
            version integer,
            name text,
            strategy_definition_id uuid,
            strategy_definition_version integer,
            instrument_root text,
            environment text,
            enabled boolean,
            has_bias_rows boolean,
            cache_range_multiplier double precision,
            maximum_contracts integer,
            maximum_expirations integer,
            schema_version integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter strategy option chain bias rows schema objects.</summary>
    internal const string CreateParameterStrategyOptionChainBiasRows =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            market_bias integer,
            enabled boolean,
            minimum_dte integer,
            maximum_dte integer,
            preferred_dte integer,
            has_put_delta boolean,
            has_call_delta boolean,
            has_put_wing_widths boolean,
            has_call_wing_widths boolean,
            target_net_delta numeric,
            net_delta_tolerance numeric,
            maximum_quote_age_milliseconds integer,
            maximum_quote_skew_milliseconds integer,
            minimum_net_credit numeric,
            maximum_net_debit numeric,
            minimum_quote_size integer,
            maximum_leg_spread_points numeric,
            maximum_candidates_per_side integer,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_strategy_option_chain(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter strategy option chain bias rows call delta schema objects.</summary>
    internal const string CreateParameterStrategyOptionChainBiasRowsCallDelta =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_call_delta (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            minimum numeric,
            target numeric,
            maximum numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_strategy_option_chain_bias_rows(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter strategy option chain bias rows call wing widths schema objects.</summary>
    internal const string CreateParameterStrategyOptionChainBiasRowsCallWingWidths =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_call_wing_widths (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_strategy_option_chain_bias_rows(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter strategy option chain bias rows put delta schema objects.</summary>
    internal const string CreateParameterStrategyOptionChainBiasRowsPutDelta =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_put_delta (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            minimum numeric,
            target numeric,
            maximum numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_strategy_option_chain_bias_rows(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter strategy option chain bias rows put wing widths schema objects.</summary>
    internal const string CreateParameterStrategyOptionChainBiasRowsPutWingWidths =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_put_wing_widths (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            value numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_strategy_option_chain_bias_rows(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the parameter value version schema objects.</summary>
    internal const string CreateParameterValueVersion =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_value_version (
            set_id uuid NOT NULL,
            version integer NOT NULL CHECK(version>0),
            component_code text NOT NULL,
            schema_version integer NOT NULL CHECK(schema_version>0),
            content_sha256 text NOT NULL CHECK(length(content_sha256)=64),
            codec text NOT NULL,
            PRIMARY KEY(set_id,version)
        );
        """;

    /// <summary>Creates the parameter vertical spread market selection schema objects.</summary>
    internal const string CreateParameterVerticalSpreadMarketSelection =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_vertical_spread_market_selection (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            schema_version integer,
            parameter_set_id uuid,
            version integer,
            default_symbol text,
            has_symbols boolean,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            CHECK(row_path='$' AND parent_path IS NULL AND ordinal=0)
        );
        """;

    /// <summary>Creates the parameter vertical spread market selection symbols schema objects.</summary>
    internal const string CreateParameterVerticalSpreadMarketSelectionSymbols =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.parameter_vertical_spread_market_selection_symbols (
            owner_set_id uuid NOT NULL,
            owner_version integer NOT NULL,
            row_path text NOT NULL,
            parent_path text NULL,
            ordinal integer NOT NULL CHECK(ordinal>=0),
            symbol text,
            short_leg_delta integer,
            spread_width numeric,
            PRIMARY KEY(owner_set_id,owner_version,row_path),
            FOREIGN KEY(owner_set_id,owner_version) REFERENCES reference_configuration.parameter_value_version(set_id,version),
            FOREIGN KEY(owner_set_id,owner_version,parent_path) REFERENCES reference_configuration.parameter_vertical_spread_market_selection(owner_set_id,owner_version,row_path),
            UNIQUE(owner_set_id,owner_version,parent_path,ordinal)
        );
        """;

    /// <summary>Creates the reference configuration schema objects.</summary>
    internal const string CreateReferenceConfiguration =
        """
        CREATE SCHEMA IF NOT EXISTS reference_configuration;
        """;

    /// <summary>Creates the regime discovery parameter set schema objects.</summary>
    internal const string CreateRegimeDiscoveryParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.regime_discovery_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version smallint NOT NULL,
            status smallint NOT NULL,
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL,
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            CONSTRAINT pk_regime_discovery_parameter_set PRIMARY KEY (parameter_set_id, version),
            CONSTRAINT ck_regime_discovery_parameter_set_version CHECK (version > 0),
            CONSTRAINT ck_regime_discovery_parameter_set_schema_version CHECK (schema_version > 0),
            CONSTRAINT ck_regime_discovery_parameter_set_hash CHECK (length(payload_sha256) = 64)
        );
        CREATE INDEX IF NOT EXISTS ix_regime_discovery_parameter_set_effective
        ON reference_configuration.regime_discovery_parameter_set (status, effective_from_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_regime_discovery_parameter_set_target_horizon_effective
        ON reference_configuration.regime_discovery_parameter_set
        ((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the risk management parameter set schema objects.</summary>
    internal const string CreateRiskManagementParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.risk_management_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version smallint NOT NULL,
            status smallint NOT NULL,
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL,
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            CONSTRAINT pk_risk_management_parameter_set PRIMARY KEY (parameter_set_id, version),
            CONSTRAINT ck_risk_management_parameter_set_version CHECK (version > 0),
            CONSTRAINT ck_risk_management_parameter_set_schema_version CHECK (schema_version > 0),
            CONSTRAINT ck_risk_management_parameter_set_hash CHECK (length(payload_sha256) = 64)
        );
        CREATE INDEX IF NOT EXISTS ix_risk_management_parameter_set_effective
        ON reference_configuration.risk_management_parameter_set (status, effective_from_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_risk_management_parameter_set_target_horizon_effective
        ON reference_configuration.risk_management_parameter_set
        ((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the strategy catalog schema objects.</summary>
    internal const string CreateStrategyCatalog =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_catalog_identity (
            kind smallint NOT NULL CHECK(kind BETWEEN 1 AND 7),
            id uuid NOT NULL CHECK(id<>'00000000-0000-0000-0000-000000000000'),
            code text NOT NULL CHECK(code ~ '^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$'),
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL CHECK(length(trim(created_by))>0),
            PRIMARY KEY(kind,id),
            UNIQUE(kind,code)
        );
        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_catalog_version (
            kind smallint NOT NULL,
            id uuid NOT NULL,
            version integer NOT NULL CHECK(version>0),
            schema_version smallint NOT NULL CHECK(schema_version=1),
            name text NOT NULL CHECK(length(trim(name))>0),
            description text NOT NULL,
            status smallint NOT NULL DEFAULT 0 CHECK(status IN (0,1,2)),
            content_sealed boolean NOT NULL DEFAULT false,
            parent_kind smallint NULL,
            parent_id uuid NULL,
            parent_version integer NULL,
            horizon smallint NOT NULL DEFAULT 0,
            side text NOT NULL,
            bias text NOT NULL,
            premium_mode text NOT NULL,
            settings_json jsonb NOT NULL CHECK(jsonb_typeof(settings_json)='object'),
            content_sha256 text NOT NULL CHECK(content_sha256 ~ '^[0-9a-f]{64}$'),
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL CHECK(length(trim(created_by))>0),
            effective_from_utc timestamptz NULL,
            published_by text NULL,
            retired_at_utc timestamptz NULL,
            retired_by text NULL,
            PRIMARY KEY(kind,id,version),
            UNIQUE(kind,id,version,parent_kind,parent_id,parent_version),
            FOREIGN KEY(kind,id) REFERENCES reference_configuration.strategy_catalog_identity(kind,id),
            FOREIGN KEY(parent_kind,parent_id,parent_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version) MATCH FULL,
            CHECK(((kind=4 AND parent_kind=3 AND parent_id IS NOT NULL AND parent_version>0) OR (kind=6 AND parent_kind=5 AND parent_id IS NOT NULL AND parent_version>0) OR (kind=7 AND parent_kind=2 AND parent_id IS NOT NULL AND parent_version>0) OR (kind IN (1,2,3,5) AND parent_kind IS NULL AND parent_id IS NULL AND parent_version IS NULL)) IS TRUE),
            CHECK((kind=7 AND horizon IN (1,2,3)) OR (kind<>7 AND horizon=0)),
            CHECK((kind=4 AND side<>'' AND bias<>'' AND premium_mode<>'') OR (kind<>4 AND side='' AND bias='' AND premium_mode='')),
            CHECK(((status=0 AND effective_from_utc IS NULL AND published_by IS NULL AND retired_at_utc IS NULL AND retired_by IS NULL) OR (status=1 AND effective_from_utc IS NOT NULL AND length(trim(published_by))>0 AND retired_at_utc IS NULL AND retired_by IS NULL) OR (status=2 AND effective_from_utc IS NOT NULL AND length(trim(published_by))>0 AND retired_at_utc>=effective_from_utc AND length(trim(retired_by))>0)) IS TRUE)
        );
        CREATE INDEX IF NOT EXISTS ix_strategy_catalog_effective ON reference_configuration.strategy_catalog_version(kind,status,horizon,effective_from_utc);

        CREATE OR REPLACE FUNCTION reference_configuration.guard_strategy_catalog_identity() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN RAISE EXCEPTION 'Catalog identities are immutable and cannot be deleted'; END $$;
        CREATE OR REPLACE TRIGGER guard_strategy_catalog_identity BEFORE UPDATE OR DELETE ON reference_configuration.strategy_catalog_identity
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_identity();

        CREATE OR REPLACE FUNCTION reference_configuration.guard_strategy_catalog_version() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            PERFORM pg_advisory_xact_lock(497003170627);
            IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Catalog versions cannot be deleted'; END IF;
            IF TG_OP='INSERT' THEN
                IF NEW.status<>0 OR NEW.content_sealed THEN RAISE EXCEPTION 'Catalog versions must be inserted as unsealed Draft'; END IF;
                RETURN NEW;
            END IF;
            IF (to_jsonb(NEW)-ARRAY['status','content_sealed','effective_from_utc','published_by','retired_at_utc','retired_by'])
               IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['status','content_sealed','effective_from_utc','published_by','retired_at_utc','retired_by'])
               THEN RAISE EXCEPTION 'Catalog version content is immutable'; END IF;
            IF NOT OLD.content_sealed AND NEW.content_sealed AND NEW.status=0 AND OLD.status=0 THEN RETURN NEW; END IF;
            IF NOT OLD.content_sealed OR NOT NEW.content_sealed THEN RAISE EXCEPTION 'Catalog content must be sealed'; END IF;
            IF OLD.status=0 AND NEW.status=1 THEN RETURN NEW; END IF;
            IF OLD.status=1 AND NEW.status=2 AND NEW.effective_from_utc=OLD.effective_from_utc AND NEW.published_by=OLD.published_by THEN RETURN NEW; END IF;
            RAISE EXCEPTION 'Invalid catalog lifecycle transition';
        END $$;
        CREATE OR REPLACE TRIGGER guard_strategy_catalog_version BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_catalog_version
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_version();

        CREATE OR REPLACE FUNCTION reference_configuration.guard_strategy_catalog_child() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE sealed boolean;
        BEGIN
            PERFORM pg_advisory_xact_lock(497003170627);
            IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'Catalog child content is immutable'; END IF;
            SELECT content_sealed INTO sealed FROM reference_configuration.strategy_catalog_version
              WHERE kind=NEW.owner_kind AND id=NEW.owner_id AND version=NEW.owner_version FOR UPDATE;
            IF sealed IS DISTINCT FROM false THEN RAISE EXCEPTION 'Catalog child requires an unsealed owning draft'; END IF;
            RETURN NEW;
        END $$;

        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_family_membership (
            owner_kind smallint NOT NULL CHECK(owner_kind=2),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            target_kind smallint NOT NULL CHECK(target_kind=1),
            target_id uuid NOT NULL,
            target_version integer NOT NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,target_kind,target_id,target_version),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version),
            FOREIGN KEY(target_kind,target_id,target_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_strategy_family_membership BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_family_membership
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_structure_assignment (
            owner_kind smallint NOT NULL CHECK(owner_kind=2),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            target_kind smallint NOT NULL CHECK(target_kind=3),
            target_id uuid NOT NULL,
            target_version integer NOT NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,target_kind,target_id,target_version),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version),
            FOREIGN KEY(target_kind,target_id,target_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_strategy_structure_assignment BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_structure_assignment
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_deployment_structure_variant (
            owner_kind smallint NOT NULL CHECK(owner_kind=7),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            target_kind smallint NOT NULL CHECK(target_kind=4),
            target_id uuid NOT NULL,
            target_version integer NOT NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,target_kind,target_id,target_version),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version),
            FOREIGN KEY(target_kind,target_id,target_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_strategy_deployment_structure_variant BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_deployment_structure_variant
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_capability_requirement (
            owner_kind smallint NOT NULL CHECK(owner_kind <> 1),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            role text NOT NULL CHECK(role IN ('evaluator','builder','validator','risk','data')),
            code text NOT NULL,
            capability_version integer NOT NULL CHECK(capability_version>0),
            PRIMARY KEY(owner_kind,owner_id,owner_version,role,code,capability_version),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_strategy_capability_requirement BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_capability_requirement
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.structure_expiry_group (
            owner_kind smallint NOT NULL CHECK(owner_kind=3),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            group_key text NOT NULL,
            after_group text NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,group_key),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version),
            FOREIGN KEY(owner_kind,owner_id,owner_version,after_group) REFERENCES reference_configuration.structure_expiry_group(owner_kind,owner_id,owner_version,group_key) DEFERRABLE INITIALLY DEFERRED
        );
        CREATE OR REPLACE TRIGGER guard_structure_expiry_group BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.structure_expiry_group
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.structure_leg_definition (
            owner_kind smallint NOT NULL CHECK(owner_kind=3),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            leg_key text NOT NULL,
            instrument_class text NOT NULL CHECK(instrument_class IN ('Futures','FuturesOption')),
            side text NOT NULL CHECK(side IN ('Buy','Sell')),
            option_right text NOT NULL,
            ratio numeric NOT NULL CHECK(ratio>0 AND ratio<=1000000),
            expiry_group text NOT NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,leg_key),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version),
            CHECK((instrument_class='Futures' AND option_right='None') OR (instrument_class='FuturesOption' AND option_right IN ('Call','Put'))),
            FOREIGN KEY(owner_kind,owner_id,owner_version,expiry_group) REFERENCES reference_configuration.structure_expiry_group(owner_kind,owner_id,owner_version,group_key)
        );
        CREATE OR REPLACE TRIGGER guard_structure_leg_definition BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.structure_leg_definition
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.structure_variant_leg_rule (
            owner_kind smallint NOT NULL CHECK(owner_kind=4),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            leg_key text NOT NULL,
            side text NOT NULL CHECK(side IN ('Buy','Sell')),
            ratio numeric NOT NULL CHECK(ratio>0 AND ratio<=1000000),
            structure_kind smallint NOT NULL CHECK(structure_kind=3),
            structure_id uuid NOT NULL,
            structure_version integer NOT NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,leg_key),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version),
            FOREIGN KEY(structure_kind,structure_id,structure_version,leg_key) REFERENCES reference_configuration.structure_leg_definition(owner_kind,owner_id,owner_version,leg_key),
            FOREIGN KEY(owner_kind,owner_id,owner_version,structure_kind,structure_id,structure_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version,parent_kind,parent_id,parent_version)
        );
        CREATE OR REPLACE TRIGGER guard_structure_variant_leg_rule BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.structure_variant_leg_rule
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_deployment_product (
            owner_kind smallint NOT NULL CHECK(owner_kind=7),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            product_id integer NOT NULL CHECK(product_id>0),
            symbol text NOT NULL,
            exchange text NOT NULL,
            currency text NOT NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,product_id),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_strategy_deployment_product BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_deployment_product
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_deployment_parameter_binding (
            owner_kind smallint NOT NULL CHECK(owner_kind=7),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            role text NOT NULL,
            parameter_kind smallint NOT NULL CHECK(parameter_kind BETWEEN 1 AND 7),
            parameter_id uuid NOT NULL,
            parameter_version integer NOT NULL CHECK(parameter_version>0),
            payload_sha256 text NOT NULL CHECK(payload_sha256 ~ '^[0-9a-f]{64}$'),
            PRIMARY KEY(owner_kind,owner_id,owner_version,role),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_strategy_deployment_parameter_binding BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_deployment_parameter_binding
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_deployment_catalog_parameter_binding (
            owner_kind smallint NOT NULL CHECK(owner_kind=7),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            role text NOT NULL,
            parameter_kind smallint NOT NULL CHECK(parameter_kind=6),
            parameter_id uuid NOT NULL,
            parameter_version integer NOT NULL,
            PRIMARY KEY(owner_kind,owner_id,owner_version,role),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version),
            FOREIGN KEY(parameter_kind,parameter_id,parameter_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_strategy_deployment_catalog_parameter_binding BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.strategy_deployment_catalog_parameter_binding
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();

        CREATE TABLE IF NOT EXISTS reference_configuration.legacy_trade_strategy_family_mapping (
            owner_kind smallint NOT NULL CHECK(owner_kind=7),
            owner_id uuid NOT NULL,
            owner_version integer NOT NULL,
            legacy_id integer NOT NULL CHECK(legacy_id>0),
            legacy_version bigint NOT NULL CHECK(legacy_version>0),
            PRIMARY KEY(owner_kind,owner_id,owner_version,legacy_id,legacy_version),
            FOREIGN KEY(owner_kind,owner_id,owner_version) REFERENCES reference_configuration.strategy_catalog_version(kind,id,version)
        );
        CREATE OR REPLACE TRIGGER guard_legacy_trade_strategy_family_mapping BEFORE INSERT OR UPDATE OR DELETE ON reference_configuration.legacy_trade_strategy_family_mapping
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_catalog_child();
        CREATE OR REPLACE FUNCTION reference_configuration.require_strategy_catalog_sealed() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NOT EXISTS(SELECT 1 FROM reference_configuration.strategy_catalog_version WHERE kind=NEW.kind AND id=NEW.id AND version=NEW.version AND content_sealed)
              THEN RAISE EXCEPTION 'Incomplete unsealed catalog draft cannot commit'; END IF;
            RETURN NULL;
        END $$;
        DO $$ BEGIN
            IF NOT EXISTS(SELECT 1 FROM pg_trigger WHERE tgname='require_strategy_catalog_sealed' AND tgrelid='reference_configuration.strategy_catalog_version'::regclass) THEN
                CREATE CONSTRAINT TRIGGER require_strategy_catalog_sealed AFTER INSERT ON reference_configuration.strategy_catalog_version
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION reference_configuration.require_strategy_catalog_sealed();
            END IF;
        END $$;
        """;

    /// <summary>Creates the strategy position risk parameter set schema objects.</summary>
    internal const string CreateStrategyPositionRiskParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.strategy_position_risk_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version smallint NOT NULL,
            status smallint NOT NULL,
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL,
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            CONSTRAINT pk_strategy_position_risk_parameter_set PRIMARY KEY (parameter_set_id, version),
            CONSTRAINT ck_strategy_position_risk_parameter_set_version CHECK (version > 0),
            CONSTRAINT ck_strategy_position_risk_parameter_set_schema_version CHECK (schema_version > 0),
            CONSTRAINT ck_strategy_position_risk_parameter_set_hash CHECK (length(payload_sha256) = 64)
        );
        CREATE INDEX IF NOT EXISTS ix_strategy_position_risk_parameter_set_effective
        ON reference_configuration.strategy_position_risk_parameter_set (status, effective_from_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_strategy_position_risk_parameter_set_target_horizon_effective
        ON reference_configuration.strategy_position_risk_parameter_set
        ((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the strategy position risk parameter set lifecycle guard schema objects.</summary>
    internal const string CreateStrategyPositionRiskParameterSetLifecycleGuard =
        """
        DO $migration$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_strategy_position_risk_parameter_set_status'
                  AND conrelid = 'reference_configuration.strategy_position_risk_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.strategy_position_risk_parameter_set
                ADD CONSTRAINT ck_strategy_position_risk_parameter_set_status CHECK (status IN (0, 1, 2));
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_strategy_position_risk_parameter_set_lifecycle'
                  AND conrelid = 'reference_configuration.strategy_position_risk_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.strategy_position_risk_parameter_set
                ADD CONSTRAINT ck_strategy_position_risk_parameter_set_lifecycle CHECK (
                    (status = 0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR
                    (status = 1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR
                    (status = 2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL
                        AND retired_at_utc >= effective_from_utc)
                );
            END IF;
        END
        $migration$;CREATE OR REPLACE FUNCTION reference_configuration.guard_strategy_position_risk_parameter_set()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $function$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Market Condition parameter-set versions cannot be deleted.';
            END IF;

            IF OLD.parameter_set_id IS DISTINCT FROM NEW.parameter_set_id OR
               OLD.version IS DISTINCT FROM NEW.version OR
               OLD.schema_version IS DISTINCT FROM NEW.schema_version OR
               OLD.payload_json IS DISTINCT FROM NEW.payload_json OR
               OLD.payload_sha256 IS DISTINCT FROM NEW.payload_sha256 OR
               OLD.description IS DISTINCT FROM NEW.description OR
               OLD.created_utc IS DISTINCT FROM NEW.created_utc OR
               OLD.created_by IS DISTINCT FROM NEW.created_by THEN
                RAISE EXCEPTION 'Market Condition parameter-set version content is immutable.';
            END IF;

            IF OLD.status = 0 AND NEW.status = 1 AND
               OLD.effective_from_utc IS NULL AND NEW.effective_from_utc IS NOT NULL AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NULL THEN
                RETURN NEW;
            END IF;

            IF OLD.status = 1 AND NEW.status = 2 AND
               NEW.effective_from_utc IS NOT DISTINCT FROM OLD.effective_from_utc AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NOT NULL AND
               NEW.retired_at_utc >= NEW.effective_from_utc THEN
                RETURN NEW;
            END IF;

            RAISE EXCEPTION 'Invalid Market Condition parameter-set lifecycle transition.';
        END
        $function$;

        DROP TRIGGER IF EXISTS trg_guard_strategy_position_risk_parameter_set
        ON reference_configuration.strategy_position_risk_parameter_set;

        CREATE TRIGGER trg_guard_strategy_position_risk_parameter_set
        BEFORE UPDATE OR DELETE ON reference_configuration.strategy_position_risk_parameter_set
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_strategy_position_risk_parameter_set();
        """;

    /// <summary>Creates the trade selection parameter set schema objects.</summary>
    internal const string CreateTradeSelectionParameterSet =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.trade_selection_parameter_set (
            parameter_set_id uuid NOT NULL,
            version integer NOT NULL,
            schema_version smallint NOT NULL,
            status smallint NOT NULL,
            effective_from_utc timestamptz NULL,
            retired_at_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 text NOT NULL,
            description text NOT NULL DEFAULT '',
            created_utc timestamptz NOT NULL,
            created_by text NOT NULL,
            CONSTRAINT pk_trade_selection_parameter_set PRIMARY KEY (parameter_set_id, version),
            CONSTRAINT ck_trade_selection_parameter_set_version CHECK (version > 0),
            CONSTRAINT ck_trade_selection_parameter_set_schema_version CHECK (schema_version > 0),
            CONSTRAINT ck_trade_selection_parameter_set_hash CHECK (length(payload_sha256) = 64)
        );
        CREATE INDEX IF NOT EXISTS ix_trade_selection_parameter_set_effective
        ON reference_configuration.trade_selection_parameter_set (status, effective_from_utc DESC);
        CREATE INDEX IF NOT EXISTS ix_trade_selection_parameter_set_target_horizon_effective
        ON reference_configuration.trade_selection_parameter_set
        ((CAST(payload_json ->> 'TargetHorizon' AS smallint)), status, effective_from_utc DESC);
        """;

    /// <summary>Creates the trade selection parameter set lifecycle guard schema objects.</summary>
    internal const string CreateTradeSelectionParameterSetLifecycleGuard =
        """
        DO $migration$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_trade_selection_parameter_set_status'
                  AND conrelid = 'reference_configuration.trade_selection_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.trade_selection_parameter_set
                ADD CONSTRAINT ck_trade_selection_parameter_set_status CHECK (status IN (0, 1, 2));
            END IF;

            IF NOT EXISTS (
                SELECT 1 FROM pg_constraint
                WHERE conname = 'ck_trade_selection_parameter_set_lifecycle'
                  AND conrelid = 'reference_configuration.trade_selection_parameter_set'::regclass
            ) THEN
                ALTER TABLE reference_configuration.trade_selection_parameter_set
                ADD CONSTRAINT ck_trade_selection_parameter_set_lifecycle CHECK (
                    (status = 0 AND effective_from_utc IS NULL AND retired_at_utc IS NULL) OR
                    (status = 1 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NULL) OR
                    (status = 2 AND effective_from_utc IS NOT NULL AND retired_at_utc IS NOT NULL
                        AND retired_at_utc >= effective_from_utc)
                );
            END IF;
        END
        $migration$;CREATE OR REPLACE FUNCTION reference_configuration.guard_trade_selection_parameter_set()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $function$
        BEGIN
            IF TG_OP = 'DELETE' THEN
                RAISE EXCEPTION 'Market Condition parameter-set versions cannot be deleted.';
            END IF;

            IF OLD.parameter_set_id IS DISTINCT FROM NEW.parameter_set_id OR
               OLD.version IS DISTINCT FROM NEW.version OR
               OLD.schema_version IS DISTINCT FROM NEW.schema_version OR
               OLD.payload_json IS DISTINCT FROM NEW.payload_json OR
               OLD.payload_sha256 IS DISTINCT FROM NEW.payload_sha256 OR
               OLD.description IS DISTINCT FROM NEW.description OR
               OLD.created_utc IS DISTINCT FROM NEW.created_utc OR
               OLD.created_by IS DISTINCT FROM NEW.created_by THEN
                RAISE EXCEPTION 'Market Condition parameter-set version content is immutable.';
            END IF;

            IF OLD.status = 0 AND NEW.status = 1 AND
               OLD.effective_from_utc IS NULL AND NEW.effective_from_utc IS NOT NULL AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NULL THEN
                RETURN NEW;
            END IF;

            IF OLD.status = 1 AND NEW.status = 2 AND
               NEW.effective_from_utc IS NOT DISTINCT FROM OLD.effective_from_utc AND
               OLD.retired_at_utc IS NULL AND NEW.retired_at_utc IS NOT NULL AND
               NEW.retired_at_utc >= NEW.effective_from_utc THEN
                RETURN NEW;
            END IF;

            RAISE EXCEPTION 'Invalid Market Condition parameter-set lifecycle transition.';
        END
        $function$;

        DROP TRIGGER IF EXISTS trg_guard_trade_selection_parameter_set
        ON reference_configuration.trade_selection_parameter_set;

        CREATE TRIGGER trg_guard_trade_selection_parameter_set
        BEFORE UPDATE OR DELETE ON reference_configuration.trade_selection_parameter_set
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_trade_selection_parameter_set();
        """;

    /// <summary>Creates the volatility series definition schema objects.</summary>
    internal const string CreateVolatilitySeriesDefinition =
        """
        CREATE TABLE IF NOT EXISTS reference_configuration.volatility_series_definition (
            series_id text NOT NULL,
            methodology_version text NOT NULL,
            environment text NOT NULL,
            effective_from_utc timestamptz NOT NULL,
            effective_until_utc timestamptz NULL,
            payload_json jsonb NOT NULL,
            payload_sha256 char(64) NOT NULL,
            approved_configuration_version text NOT NULL,
            owner text NOT NULL,
            approval_evidence_id text NOT NULL,
            created_utc timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
            PRIMARY KEY(series_id,methodology_version),
            CHECK(effective_until_utc IS NULL OR effective_until_utc > effective_from_utc)
        );
        CREATE INDEX IF NOT EXISTS ix_volatility_series_definition_effective
        ON reference_configuration.volatility_series_definition(environment,series_id,effective_from_utc DESC);
        CREATE OR REPLACE FUNCTION reference_configuration.guard_volatility_series_definition()
        RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Volatility series definitions are immutable and append-only.';
        END $$;
        DROP TRIGGER IF EXISTS trg_guard_volatility_series_definition
        ON reference_configuration.volatility_series_definition;
        CREATE TRIGGER trg_guard_volatility_series_definition
        BEFORE UPDATE OR DELETE ON reference_configuration.volatility_series_definition
        FOR EACH ROW EXECUTE FUNCTION reference_configuration.guard_volatility_series_definition();
        """;

    /// <summary>Drops the configuration baseline application schema objects.</summary>
    internal const string DropConfigurationBaselineApplication =
        """
        DROP TABLE IF EXISTS reference_configuration.configuration_baseline_application;
        """;

    /// <summary>Drops the intrinsic time strategy workflow parameter set schema objects.</summary>
    internal const string DropIntrinsicTimeStrategyWorkflowParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.intrinsic_time_strategy_workflow_parameter_set;
        """;

    /// <summary>Drops the intrinsic time strategy workflow parameter set lifecycle guard schema objects.</summary>
    internal const string DropIntrinsicTimeStrategyWorkflowParameterSetLifecycleGuard =
        """
        DROP TRIGGER IF EXISTS trg_guard_intrinsic_time_strategy_workflow_parameter_set ON reference_configuration.intrinsic_time_strategy_workflow_parameter_set;
        DROP FUNCTION IF EXISTS reference_configuration.guard_intrinsic_time_strategy_workflow_parameter_set();
        """;

    /// <summary>Drops the ix market condition parameter set effective schema objects.</summary>
    internal const string DropIxMarketConditionParameterSetEffective =
        """
        DROP INDEX IF EXISTS reference_configuration.ix_market_condition_parameter_set_effective;
        """;

    /// <summary>Drops the lookup definition schema objects.</summary>
    internal const string DropLookupDefinition =
        """
        DROP TABLE IF EXISTS reference_configuration.lookup_definition;
        DROP FUNCTION IF EXISTS reference_configuration.guard_lookup_definition();
        """;

    /// <summary>Drops the market condition assessment parameter set schema objects.</summary>
    internal const string DropMarketConditionAssessmentParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.market_condition_assessment_parameter_set;
        """;

    /// <summary>Drops the market condition parameter set schema objects.</summary>
    internal const string DropMarketConditionParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.market_condition_parameter_set;
        """;

    /// <summary>Drops the market condition parameter set lifecycle constraints schema objects.</summary>
    internal const string DropMarketConditionParameterSetLifecycleConstraints =
        """
        ALTER TABLE IF EXISTS reference_configuration.market_condition_parameter_set
        DROP CONSTRAINT IF EXISTS ck_market_condition_parameter_set_lifecycle,
        DROP CONSTRAINT IF EXISTS ck_market_condition_parameter_set_status;
        """;

    /// <summary>Drops the market condition parameter set lifecycle guard schema objects.</summary>
    internal const string DropMarketConditionParameterSetLifecycleGuard =
        """
        DROP TRIGGER IF EXISTS trg_guard_market_condition_parameter_set
        ON reference_configuration.market_condition_parameter_set;
        DROP FUNCTION IF EXISTS reference_configuration.guard_market_condition_parameter_set();
        """;

    /// <summary>Drops the order composition parameter set schema objects.</summary>
    internal const string DropOrderCompositionParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.order_composition_parameter_set;
        """;

    /// <summary>Drops the order composition parameter set lifecycle guard schema objects.</summary>
    internal const string DropOrderCompositionParameterSetLifecycleGuard =
        """
        DROP TRIGGER IF EXISTS trg_guard_order_composition_parameter_set ON reference_configuration.order_composition_parameter_set;
        DROP FUNCTION IF EXISTS reference_configuration.guard_order_composition_parameter_set();
        """;

    /// <summary>Drops the parameter catalog assignment schema objects.</summary>
    internal const string DropParameterCatalogAssignment =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_assignment;
        """;

    /// <summary>Drops the parameter catalog assignment reference schema objects.</summary>
    internal const string DropParameterCatalogAssignmentReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_assignment_reference;
        """;

    /// <summary>Drops the parameter catalog assignment scope schema objects.</summary>
    internal const string DropParameterCatalogAssignmentScope =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_assignment_scope;
        """;

    /// <summary>Drops the parameter catalog audit schema objects.</summary>
    internal const string DropParameterCatalogAudit =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit;
        """;

    /// <summary>Drops the parameter catalog audit after schema objects.</summary>
    internal const string DropParameterCatalogAuditAfter =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_after;
        """;

    /// <summary>Drops the parameter catalog audit after assignment schema objects.</summary>
    internal const string DropParameterCatalogAuditAfterAssignment =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_after_assignment;
        """;

    /// <summary>Drops the parameter catalog audit after assignment reference schema objects.</summary>
    internal const string DropParameterCatalogAuditAfterAssignmentReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_after_assignment_reference;
        """;

    /// <summary>Drops the parameter catalog audit after assignment scope schema objects.</summary>
    internal const string DropParameterCatalogAuditAfterAssignmentScope =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_after_assignment_scope;
        """;

    /// <summary>Drops the parameter catalog audit after reference schema objects.</summary>
    internal const string DropParameterCatalogAuditAfterReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_after_reference;
        """;

    /// <summary>Drops the parameter catalog audit before schema objects.</summary>
    internal const string DropParameterCatalogAuditBefore =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_before;
        """;

    /// <summary>Drops the parameter catalog audit before assignment schema objects.</summary>
    internal const string DropParameterCatalogAuditBeforeAssignment =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_before_assignment;
        """;

    /// <summary>Drops the parameter catalog audit before assignment reference schema objects.</summary>
    internal const string DropParameterCatalogAuditBeforeAssignmentReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_before_assignment_reference;
        """;

    /// <summary>Drops the parameter catalog audit before assignment scope schema objects.</summary>
    internal const string DropParameterCatalogAuditBeforeAssignmentScope =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_before_assignment_scope;
        """;

    /// <summary>Drops the parameter catalog audit before reference schema objects.</summary>
    internal const string DropParameterCatalogAuditBeforeReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_audit_before_reference;
        """;

    /// <summary>Drops the parameter catalog startup schema objects.</summary>
    internal const string DropParameterCatalogStartup =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup;
        """;

    /// <summary>Drops the parameter catalog startup plan schema objects.</summary>
    internal const string DropParameterCatalogStartupPlan =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_plan;
        """;

    /// <summary>Drops the parameter catalog startup plan issues schema objects.</summary>
    internal const string DropParameterCatalogStartupPlanIssues =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_plan_issues;
        """;

    /// <summary>Drops the parameter catalog startup plan steps schema objects.</summary>
    internal const string DropParameterCatalogStartupPlanSteps =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_plan_steps;
        """;

    /// <summary>Drops the parameter catalog startup plan steps consumers schema objects.</summary>
    internal const string DropParameterCatalogStartupPlanStepsConsumers =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_plan_steps_consumers;
        """;

    /// <summary>Drops the parameter catalog startup plan steps consumers key schema objects.</summary>
    internal const string DropParameterCatalogStartupPlanStepsConsumersKey =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_plan_steps_consumers_key;
        """;

    /// <summary>Drops the parameter catalog startup plan steps key schema objects.</summary>
    internal const string DropParameterCatalogStartupPlanStepsKey =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_plan_steps_key;
        """;

    /// <summary>Drops the parameter catalog startup report schema objects.</summary>
    internal const string DropParameterCatalogStartupReport =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_report;
        """;

    /// <summary>Drops the parameter catalog startup report outcomes schema objects.</summary>
    internal const string DropParameterCatalogStartupReportOutcomes =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_report_outcomes;
        """;

    /// <summary>Drops the parameter catalog startup report outcomes key schema objects.</summary>
    internal const string DropParameterCatalogStartupReportOutcomesKey =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_report_outcomes_key;
        """;

    /// <summary>Drops the parameter catalog startup scopes schema objects.</summary>
    internal const string DropParameterCatalogStartupScopes =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_scopes;
        """;

    /// <summary>Drops the parameter catalog startup scopes reference schema objects.</summary>
    internal const string DropParameterCatalogStartupScopesReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_scopes_reference;
        """;

    /// <summary>Drops the parameter catalog startup scopes scope schema objects.</summary>
    internal const string DropParameterCatalogStartupScopesScope =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_scopes_scope;
        """;

    /// <summary>Drops the parameter catalog startup versions schema objects.</summary>
    internal const string DropParameterCatalogStartupVersions =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_versions;
        """;

    /// <summary>Drops the parameter catalog startup versions legacy source schema objects.</summary>
    internal const string DropParameterCatalogStartupVersionsLegacySource =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_versions_legacy_source;
        """;

    /// <summary>Drops the parameter catalog startup versions reference schema objects.</summary>
    internal const string DropParameterCatalogStartupVersionsReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_versions_reference;
        """;

    /// <summary>Drops the parameter catalog version schema objects.</summary>
    internal const string DropParameterCatalogVersion =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_version;
        """;

    /// <summary>Drops the parameter catalog version legacy source schema objects.</summary>
    internal const string DropParameterCatalogVersionLegacySource =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_version_legacy_source;
        """;

    /// <summary>Drops the parameter catalog version reference schema objects.</summary>
    internal const string DropParameterCatalogVersionReference =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_version_reference;
        """;

    /// <summary>Drops the parameter futures iti signal schema objects.</summary>
    internal const string DropParameterFuturesItiSignal =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_futures_iti_signal;
        """;

    /// <summary>Drops the parameter futures iti signal default trading days schema objects.</summary>
    internal const string DropParameterFuturesItiSignalDefaultTradingDays =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_futures_iti_signal_default_trading_days;
        """;

    /// <summary>Drops the parameter iron condor market selection schema objects.</summary>
    internal const string DropParameterIronCondorMarketSelection =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_iron_condor_market_selection;
        """;

    /// <summary>Drops the parameter iron condor market selection symbols schema objects.</summary>
    internal const string DropParameterIronCondorMarketSelectionSymbols =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_iron_condor_market_selection_symbols;
        """;

    /// <summary>Drops the parameter option volatility consumer rules schema objects.</summary>
    internal const string DropParameterOptionVolatilityConsumerRules =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_consumer_rules;
        """;

    /// <summary>Drops the parameter option volatility consumer rules numerical rules schema objects.</summary>
    internal const string DropParameterOptionVolatilityConsumerRulesNumericalRules =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_consumer_rules_numerical_rules;
        """;

    /// <summary>Drops the parameter option volatility retention schema objects.</summary>
    internal const string DropParameterOptionVolatilityRetention =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_retention;
        """;

    /// <summary>Drops the parameter option volatility retention historical sources schema objects.</summary>
    internal const string DropParameterOptionVolatilityRetentionHistoricalSources =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_retention_historical_sources;
        """;

    /// <summary>Drops the parameter option volatility retention isolated environments schema objects.</summary>
    internal const string DropParameterOptionVolatilityRetentionIsolatedEnvironments =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_retention_isolated_environments;
        """;

    /// <summary>Drops the parameter option volatility series schema objects.</summary>
    internal const string DropParameterOptionVolatilitySeries =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_series;
        """;

    /// <summary>Drops the parameter option volatility series eligible product families schema objects.</summary>
    internal const string DropParameterOptionVolatilitySeriesEligibleProductFamilies =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_series_eligible_product_families;
        """;

    /// <summary>Drops the parameter option volatility series pricers schema objects.</summary>
    internal const string DropParameterOptionVolatilitySeriesPricers =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_series_pricers;
        """;

    /// <summary>Drops the parameter option volatility series pricers pricer versions schema objects.</summary>
    internal const string DropParameterOptionVolatilitySeriesPricersPricerVersions =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_option_volatility_series_pricers_pricer_versions;
        """;

    /// <summary>Drops the parameter projection pending schema objects.</summary>
    internal const string DropParameterProjectionPending =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_projection_pending;
        """;

    /// <summary>Drops the parameter projection record schema objects.</summary>
    internal const string DropParameterProjectionRecord =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_startup_state;
        DROP TABLE IF EXISTS reference_configuration.parameter_catalog_operation;
        DROP TABLE IF EXISTS reference_configuration.parameter_projection_record;
        """;

    /// <summary>Drops the parameter regime discovery schema objects.</summary>
    internal const string DropParameterRegimeDiscovery =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery;
        """;

    /// <summary>Drops the parameter regime discovery data quality schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryDataQuality =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_data_quality;
        """;

    /// <summary>Drops the parameter regime discovery data quality approved c 65f3d75001b7 schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryDataQualityApprovedC65f3d75001b7 =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_data_quality_approved_c_65f3d75001b7;
        """;

    /// <summary>Drops the parameter regime discovery data quality supported  450c80e543a1 schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryDataQualitySupported450c80e543a1 =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_data_quality_supported__450c80e543a1;
        """;

    /// <summary>Drops the parameter regime discovery freshness schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryFreshness =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_freshness;
        """;

    /// <summary>Drops the parameter regime discovery fusion schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryFusion =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_fusion;
        """;

    /// <summary>Drops the parameter regime discovery horizon schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryHorizon =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_horizon;
        """;

    /// <summary>Drops the parameter regime discovery horizon time frames schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryHorizonTimeFrames =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_horizon_time_frames;
        """;

    /// <summary>Drops the parameter regime discovery market structure schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryMarketStructure =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_market_structure;
        """;

    /// <summary>Drops the parameter regime discovery observation metrics schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryObservationMetrics =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_observation_metrics;
        """;

    /// <summary>Drops the parameter regime discovery signal metrics schema objects.</summary>
    internal const string DropParameterRegimeDiscoverySignalMetrics =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_signal_metrics;
        """;

    /// <summary>Drops the parameter regime discovery signal requirements schema objects.</summary>
    internal const string DropParameterRegimeDiscoverySignalRequirements =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_signal_requirements;
        """;

    /// <summary>Drops the parameter regime discovery trend schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryTrend =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_trend;
        """;

    /// <summary>Drops the parameter regime discovery volatility schema objects.</summary>
    internal const string DropParameterRegimeDiscoveryVolatility =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_regime_discovery_volatility;
        """;

    /// <summary>Drops the parameter registry schema objects.</summary>
    internal const string DropParameterRegistry =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_schema_version;
        DROP TABLE IF EXISTS reference_configuration.parameter_component;
        DROP TABLE IF EXISTS reference_configuration.parameter_area;
        """;

    /// <summary>Drops the parameter revision fence schema objects.</summary>
    internal const string DropParameterRevisionFence =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_revision_fence;
        """;

    /// <summary>Drops the parameter sets schema objects.</summary>
    internal const string DropParameterSets =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_assignment_revision;
        DROP TABLE IF EXISTS reference_configuration.parameter_assignment;
        DROP TABLE IF EXISTS reference_configuration.parameter_operation;
        DROP TABLE IF EXISTS reference_configuration.parameter_set_version;
        DROP TABLE IF EXISTS reference_configuration.parameter_set;
        """;

    /// <summary>Drops the parameter strategy option chain schema objects.</summary>
    internal const string DropParameterStrategyOptionChain =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_strategy_option_chain;
        """;

    /// <summary>Drops the parameter strategy option chain bias rows schema objects.</summary>
    internal const string DropParameterStrategyOptionChainBiasRows =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows;
        """;

    /// <summary>Drops the parameter strategy option chain bias rows call delta schema objects.</summary>
    internal const string DropParameterStrategyOptionChainBiasRowsCallDelta =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_call_delta;
        """;

    /// <summary>Drops the parameter strategy option chain bias rows call wing widths schema objects.</summary>
    internal const string DropParameterStrategyOptionChainBiasRowsCallWingWidths =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_call_wing_widths;
        """;

    /// <summary>Drops the parameter strategy option chain bias rows put delta schema objects.</summary>
    internal const string DropParameterStrategyOptionChainBiasRowsPutDelta =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_put_delta;
        """;

    /// <summary>Drops the parameter strategy option chain bias rows put wing widths schema objects.</summary>
    internal const string DropParameterStrategyOptionChainBiasRowsPutWingWidths =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_strategy_option_chain_bias_rows_put_wing_widths;
        """;

    /// <summary>Drops the parameter value version schema objects.</summary>
    internal const string DropParameterValueVersion =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_value_version;
        """;

    /// <summary>Drops the parameter vertical spread market selection schema objects.</summary>
    internal const string DropParameterVerticalSpreadMarketSelection =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_vertical_spread_market_selection;
        """;

    /// <summary>Drops the parameter vertical spread market selection symbols schema objects.</summary>
    internal const string DropParameterVerticalSpreadMarketSelectionSymbols =
        """
        DROP TABLE IF EXISTS reference_configuration.parameter_vertical_spread_market_selection_symbols;
        """;

    /// <summary>Drops the reference configuration schema objects.</summary>
    internal const string DropReferenceConfiguration =
        """
        DROP SCHEMA IF EXISTS reference_configuration CASCADE;
        """;

    /// <summary>Drops the regime discovery parameter set schema objects.</summary>
    internal const string DropRegimeDiscoveryParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.regime_discovery_parameter_set;
        """;

    /// <summary>Drops the risk management parameter set schema objects.</summary>
    internal const string DropRiskManagementParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.risk_management_parameter_set;
        """;

    /// <summary>Drops the strategy catalog schema objects.</summary>
    internal const string DropStrategyCatalog =
        """
        DROP TABLE IF EXISTS reference_configuration.legacy_trade_strategy_family_mapping;
        DROP TABLE IF EXISTS reference_configuration.strategy_deployment_catalog_parameter_binding;
        DROP TABLE IF EXISTS reference_configuration.strategy_deployment_parameter_binding;
        DROP TABLE IF EXISTS reference_configuration.strategy_deployment_product;
        DROP TABLE IF EXISTS reference_configuration.structure_variant_leg_rule;
        DROP TABLE IF EXISTS reference_configuration.structure_leg_definition;
        DROP TABLE IF EXISTS reference_configuration.structure_expiry_group;
        DROP TABLE IF EXISTS reference_configuration.strategy_capability_requirement;
        DROP TABLE IF EXISTS reference_configuration.strategy_deployment_structure_variant;
        DROP TABLE IF EXISTS reference_configuration.strategy_structure_assignment;
        DROP TABLE IF EXISTS reference_configuration.strategy_family_membership;
        DROP TABLE IF EXISTS reference_configuration.strategy_catalog_version;
        DROP TABLE IF EXISTS reference_configuration.strategy_catalog_identity;
        DROP FUNCTION IF EXISTS reference_configuration.guard_strategy_catalog_child();
        DROP FUNCTION IF EXISTS reference_configuration.guard_strategy_catalog_version();
        DROP FUNCTION IF EXISTS reference_configuration.guard_strategy_catalog_identity();
        DROP FUNCTION IF EXISTS reference_configuration.require_strategy_catalog_sealed();
        """;

    /// <summary>Drops the strategy position risk parameter set schema objects.</summary>
    internal const string DropStrategyPositionRiskParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.strategy_position_risk_parameter_set;
        """;

    /// <summary>Drops the strategy position risk parameter set lifecycle guard schema objects.</summary>
    internal const string DropStrategyPositionRiskParameterSetLifecycleGuard =
        """
        DROP TRIGGER IF EXISTS trg_guard_strategy_position_risk_parameter_set ON reference_configuration.strategy_position_risk_parameter_set;
        DROP FUNCTION IF EXISTS reference_configuration.guard_strategy_position_risk_parameter_set();
        """;

    /// <summary>Drops the trade selection parameter set schema objects.</summary>
    internal const string DropTradeSelectionParameterSet =
        """
        DROP TABLE IF EXISTS reference_configuration.trade_selection_parameter_set;
        """;

    /// <summary>Drops the trade selection parameter set lifecycle guard schema objects.</summary>
    internal const string DropTradeSelectionParameterSetLifecycleGuard =
        """
        DROP TRIGGER IF EXISTS trg_guard_trade_selection_parameter_set ON reference_configuration.trade_selection_parameter_set;
        DROP FUNCTION IF EXISTS reference_configuration.guard_trade_selection_parameter_set();
        """;

    /// <summary>Drops the volatility series definition schema objects.</summary>
    internal const string DropVolatilitySeriesDefinition =
        """
        DROP TRIGGER IF EXISTS trg_guard_volatility_series_definition
        ON reference_configuration.volatility_series_definition;
        DROP FUNCTION IF EXISTS reference_configuration.guard_volatility_series_definition();
        DROP TABLE IF EXISTS reference_configuration.volatility_series_definition;
        """;

    /// <summary>Seeds default lookup values during ordinary initialization.</summary>
    internal const string SeedLookupDefaults =
        """
        INSERT INTO reference_configuration.lookup_definition(group_name,internal_value,display_name,description,display_order) VALUES
        ('AssetTypes','Futures','Futures','Futures contracts.',10),
        ('AssetTypes','FuturesOption','Futures Options','Options on futures contracts.',20),
        ('Directions','Bullish','Bullish','Upward market direction.',10),
        ('Directions','Bearish','Bearish','Downward market direction.',20),
        ('Directions','Neutral','Neutral','Neutral market direction.',30),
        ('MarketConditions','Directional','Directional','Directional market condition.',10),
        ('MarketConditions','RangeBound','Range Bound','Market trading within a range.',20),
        ('MarketConditions','Transition','Transition','Market transitioning between conditions.',30),
        ('MarketConditions','VolatilityExpansion','Volatility Expansion','Expanding market volatility.',40),
        ('MarketConditions','VolatilityContraction','Volatility Contraction','Contracting market volatility.',50),
        ('MarketConditions','Dislocated','Dislocated','Dislocated market; tradeability rules still apply.',60),
        ('MarketConditions','NoOpportunity','No Opportunity','No market opportunity; this permission does not override tradeability.',70)
        ON CONFLICT(group_name,internal_value) DO NOTHING;
        """;

    /// <summary>Seeds one version from the registered parameter schema.</summary>
    internal const string SeedRegisteredParameterSchema =
        """
        INSERT INTO reference_configuration.parameter_schema_version
            (component_code,schema_version,codec,schema_sha256,schema_json)
        VALUES ('{0}',{1},'{2}','{3}','{4}'::jsonb)
        ON CONFLICT(component_code,schema_version) DO NOTHING;
        """;
}
