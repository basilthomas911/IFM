namespace TomasAI.IFM.Application.Storage.MarketDataServiceDb;

internal static class MarketDataServiceDbSql
{
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
    internal const string UpsertVxPair = """
        WITH front AS (
          INSERT INTO market_data_service.futures_rollover_contract_assignment
          (contract_role,root_symbol,contract_id,description,local_symbol,security_type,currency,exchange,multiplier,last_trade_date,next_rollover_date,source_contract_hash,row_version,created_on_utc,created_by,updated_on_utc,updated_by)
          VALUES($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$17+1,$13,$14,$15,$16)
          ON CONFLICT(contract_role) DO UPDATE SET root_symbol=EXCLUDED.root_symbol,contract_id=EXCLUDED.contract_id,
          description=EXCLUDED.description,local_symbol=EXCLUDED.local_symbol,security_type=EXCLUDED.security_type,
          currency=EXCLUDED.currency,exchange=EXCLUDED.exchange,multiplier=EXCLUDED.multiplier,last_trade_date=EXCLUDED.last_trade_date,
          next_rollover_date=EXCLUDED.next_rollover_date,source_contract_hash=EXCLUDED.source_contract_hash,
          row_version=market_data_service.futures_rollover_contract_assignment.row_version+1,
          updated_on_utc=EXCLUDED.updated_on_utc,updated_by=EXCLUDED.updated_by
          WHERE market_data_service.futures_rollover_contract_assignment.row_version=$17 RETURNING contract_role
        ), second AS (
          INSERT INTO market_data_service.futures_rollover_contract_assignment
          (contract_role,root_symbol,contract_id,description,local_symbol,security_type,currency,exchange,multiplier,last_trade_date,next_rollover_date,source_contract_hash,row_version,created_on_utc,created_by,updated_on_utc,updated_by)
          VALUES($18,$19,$20,$21,$22,$23,$24,$25,$26,$27,$28,$29,$34+1,$30,$31,$32,$33)
          ON CONFLICT(contract_role) DO UPDATE SET root_symbol=EXCLUDED.root_symbol,contract_id=EXCLUDED.contract_id,
          description=EXCLUDED.description,local_symbol=EXCLUDED.local_symbol,security_type=EXCLUDED.security_type,
          currency=EXCLUDED.currency,exchange=EXCLUDED.exchange,multiplier=EXCLUDED.multiplier,last_trade_date=EXCLUDED.last_trade_date,
          next_rollover_date=EXCLUDED.next_rollover_date,source_contract_hash=EXCLUDED.source_contract_hash,
          row_version=market_data_service.futures_rollover_contract_assignment.row_version+1,
          updated_on_utc=EXCLUDED.updated_on_utc,updated_by=EXCLUDED.updated_by
          WHERE market_data_service.futures_rollover_contract_assignment.row_version=$34 RETURNING contract_role
        ) SELECT ((SELECT count(*) FROM front)+(SELECT count(*) FROM second))::integer;
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
}
