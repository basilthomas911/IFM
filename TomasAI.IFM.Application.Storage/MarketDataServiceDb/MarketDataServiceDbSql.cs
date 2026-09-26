namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

internal static class MarketDataServiceDbSql
{
    internal const string ConfigureDurableTransaction = """
        SET LOCAL lock_timeout = '5s';
        SET LOCAL statement_timeout = '10s';
        SET LOCAL idle_in_transaction_session_timeout = '15s';
        """;
    internal const string EnsureDurableCurrent = """
        INSERT INTO market_data_service.stage4_intent_current(scope,dataset,revision,snapshot)
        VALUES($1,$2,0,$3::jsonb) ON CONFLICT(scope,dataset) DO NOTHING;
        """;
    internal const string CheckDurableLeaseIdentity = """
        SELECT EXISTS(SELECT 1 FROM market_data_service.stage4_lease_identity
        WHERE scope=$1 AND dataset=$2 AND lease_id IN
          (SELECT jsonb_array_elements_text($3::jsonb)::uuid));
        """;
    internal const string UpdateDurableCurrent = """
        UPDATE market_data_service.stage4_intent_current
        SET revision=$3,snapshot=$4::jsonb WHERE scope=$1 AND dataset=$2 AND revision=$5;
        """;
    internal const string ReserveDurableLeaseIdentity = """
        INSERT INTO market_data_service.stage4_lease_identity
        (scope,dataset,lease_id,source_id,owner_digest,lease_digest,created_revision)
        VALUES($1,$2,$3,$4,$5,$6,$7);
        """;
    internal const string RetireDurableLeaseIdentity = """
        UPDATE market_data_service.stage4_lease_identity SET released_revision=$3
        WHERE scope=$1 AND dataset=$2 AND released_revision IS NULL AND lease_id IN
          (SELECT jsonb_array_elements_text($4::jsonb)::uuid);
        """;
    internal const string InsertDurableOperation = """
        INSERT INTO market_data_service.stage4_intent_operation
        (scope,dataset,operation_id,request_digest,result,created_at_utc) VALUES($1,$2,$3,$4,$5::jsonb,$6);
        """;
    internal const string InsertDurableOutbox = """
        INSERT INTO market_data_service.stage4_intent_outbox
        (scope,dataset,transition_id,operation_id,revision,payload,created_at_utc)
        VALUES($1,$2,$3,$4,$5,$6::jsonb,$7);
        """;
    internal const string UpdateDurableWatermark = """
        INSERT INTO market_data_service.stage4_authority_watermark
        (scope,dataset,source_id,source_version,source_event_id,fact_digest,owner_digest)
        VALUES($1,$2,$3,$4,$5,$6,$7)
        ON CONFLICT(scope,dataset,source_id) DO UPDATE SET
          source_version=EXCLUDED.source_version,source_event_id=EXCLUDED.source_event_id,
          fact_digest=EXCLUDED.fact_digest,owner_digest=EXCLUDED.owner_digest;
        """;
    internal const string ReadDurableOutbox = """
        SELECT payload::text FROM market_data_service.stage4_intent_outbox
        WHERE scope=$1 AND dataset=$2 AND delivered_at_utc IS NULL ORDER BY revision LIMIT $3;
        """;
    internal const string AcknowledgeDurableOutbox = """
        UPDATE market_data_service.stage4_intent_outbox SET delivered_at_utc=COALESCE(delivered_at_utc,$4)
        WHERE scope=$1 AND dataset=$2 AND transition_id=$3;
        """;
    internal const string ReadDurableCurrent =
        "SELECT snapshot::text,revision FROM market_data_service.stage4_intent_current WHERE scope=$1 AND dataset=$2;";
    internal const string ReadDurableCurrentForUpdate =
        "SELECT snapshot::text,revision FROM market_data_service.stage4_intent_current WHERE scope=$1 AND dataset=$2 FOR UPDATE;";
    internal const string ReadDurableOperation = """
        SELECT request_digest,result::text FROM market_data_service.stage4_intent_operation
        WHERE scope=$1 AND dataset=$2 AND operation_id=$3;
        """;
    internal const string GetCompositionRoutePlan =
        "SELECT payload::text FROM market_data_service.composition_route_plan WHERE plan_id=$1;";
    internal const string InsertCompositionRoutePlan =
        "INSERT INTO market_data_service.composition_route_plan(plan_id,payload) VALUES($1,$2::jsonb) ON CONFLICT(plan_id) DO NOTHING;";
    internal const string AssignmentColumns = "contract_role,root_symbol,contract_id,description,local_symbol,security_type,currency,exchange,multiplier,last_trade_date,next_rollover_date,source_contract_hash,row_version,created_on_utc,created_by,updated_on_utc,updated_by";
    internal static readonly string GetAssignment = $"SELECT {AssignmentColumns} FROM market_data_service.futures_rollover_contract_assignment WHERE contract_role=$1;";
    internal static readonly string ListAssignments = $"SELECT {AssignmentColumns} FROM market_data_service.futures_rollover_contract_assignment ORDER BY contract_role;";
    internal static readonly string InsertAssignment = $"""
        INSERT INTO market_data_service.futures_rollover_contract_assignment ({AssignmentColumns})
        SELECT $1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,1,$13,$14,$15,$16
        WHERE $17=0 ON CONFLICT DO NOTHING RETURNING {AssignmentColumns};
        """;
    internal static readonly string UpdateAssignment = $"""
        UPDATE market_data_service.futures_rollover_contract_assignment SET
        root_symbol=$2,contract_id=$3,description=$4,local_symbol=$5,security_type=$6,currency=$7,
        exchange=$8,multiplier=$9,last_trade_date=$10,next_rollover_date=$11,source_contract_hash=$12,
        row_version=row_version+1,updated_on_utc=$15,updated_by=$16
        WHERE contract_role=$1 AND row_version=$17 RETURNING {AssignmentColumns};
        """;
    internal const string DeleteAssignment = "DELETE FROM market_data_service.futures_rollover_contract_assignment WHERE contract_role=$1 AND row_version=$2;";
    internal const string DeleteVxPair = """
        DELETE FROM market_data_service.futures_rollover_contract_assignment
        WHERE (contract_role=$1 AND row_version=$2)
           OR (contract_role=$3 AND row_version=$4);
        """;
    internal const string InsertVxPair = """
        INSERT INTO market_data_service.futures_rollover_contract_assignment
        (contract_role,root_symbol,contract_id,description,local_symbol,security_type,currency,exchange,multiplier,last_trade_date,next_rollover_date,source_contract_hash,row_version,created_on_utc,created_by,updated_on_utc,updated_by)
        VALUES
        ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$17+1,$13,$14,$15,$16),
        ($18,$19,$20,$21,$22,$23,$24,$25,$26,$27,$28,$29,$34+1,$30,$31,$32,$33);
        """;
    internal const string InsertObservation = """
        INSERT INTO market_data_service.watchdog_status_log
        (watchdog_status_log_id,observation_id,correlation_id,value_date,observed_on_utc,operation_reason,
         major_status,display_health,core_contracts_ready,recovery_attempt,native_backend,native_abi_version,
         native_generation,failure_stage,failure_detail,feed_status_details,row_version,created_on_utc,created_by,updated_on_utc,updated_by)
        VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16::jsonb,1,$5,$17,$5,$17)
        ON CONFLICT(observation_id) DO NOTHING;
        """;
    internal const string ObservationColumns = "watchdog_status_log_id,observation_id,correlation_id,value_date,observed_on_utc,operation_reason,major_status,display_health,core_contracts_ready,recovery_attempt,native_backend,native_abi_version,native_generation,failure_stage,failure_detail,feed_status_details,row_version";
    internal static readonly string GetObservation = $"SELECT {ObservationColumns} FROM market_data_service.watchdog_status_log WHERE watchdog_status_log_id=$1;";
    internal static readonly string GetObservationByIdentity = $"SELECT {ObservationColumns} FROM market_data_service.watchdog_status_log WHERE observation_id=$1;";
    internal static readonly string ListObservations = $"SELECT {ObservationColumns} FROM market_data_service.watchdog_status_log WHERE ($1::date IS NULL OR value_date=$1) AND ($2::text IS NULL OR major_status=$2) ORDER BY observed_on_utc DESC LIMIT $3;";
    internal static readonly string UpdateObservation = $"""
        UPDATE market_data_service.watchdog_status_log SET correlation_id=$3,value_date=$4,observed_on_utc=$5,
        operation_reason=$6,major_status=$7,display_health=$8,core_contracts_ready=$9,recovery_attempt=$10,
        native_backend=$11,native_abi_version=$12,native_generation=$13,failure_stage=$14,failure_detail=$15,
        feed_status_details=$16::jsonb,row_version=row_version+1,updated_on_utc=now(),updated_by=$18
        WHERE watchdog_status_log_id=$1 AND observation_id=$2 AND row_version=$17 RETURNING {ObservationColumns};
        """;
    internal const string DeleteObservation = "DELETE FROM market_data_service.watchdog_status_log WHERE watchdog_status_log_id=$1 AND row_version=$2;";
    internal const string PersistDatasetIncident = """
        WITH history AS (
          INSERT INTO market_data_service.dataset_incident_transition
          (transition_id,incident_id,correlation_id,dataset,value_date,observed_on_utc,is_open,snapshot)
          VALUES($1,$2,$3,$4,$5,$6,$7,$8::jsonb)
          ON CONFLICT(transition_id) DO NOTHING
          RETURNING transition_id
        )
        INSERT INTO market_data_service.dataset_incident_current
        (dataset,value_date,incident_id,transition_id,correlation_id,observed_on_utc,is_open,snapshot,row_version)
        VALUES($4,$5,$2,$1,$3,$6,$7,$8::jsonb,1)
        ON CONFLICT(dataset) DO UPDATE SET value_date=EXCLUDED.value_date,
          incident_id=EXCLUDED.incident_id,transition_id=EXCLUDED.transition_id,
          correlation_id=EXCLUDED.correlation_id,observed_on_utc=EXCLUDED.observed_on_utc,
          is_open=EXCLUDED.is_open,snapshot=EXCLUDED.snapshot,
          row_version=market_data_service.dataset_incident_current.row_version+1
        WHERE EXISTS(SELECT 1 FROM history)
        RETURNING row_version;
        """;
    internal const string ListOpenDatasetIncidents = """
        SELECT transition_id,correlation_id,snapshot,row_version
        FROM market_data_service.dataset_incident_current
        WHERE is_open=true ORDER BY dataset;
        """;
}
