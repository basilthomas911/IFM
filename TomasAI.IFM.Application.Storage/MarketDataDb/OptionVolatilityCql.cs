namespace TomasAI.IFM.Application.Storage.MarketDataDb;

public static class OptionVolatilityCql
{
    public const string InsertObservationHistory = """
INSERT INTO option_iv_observation_history(environment,series_id,methodology_version,calendar_bucket,
 value_date,sampling_slot,revision,observation_id,available_at_utc,payload)
VALUES(:environment,:series,:methodology,:bucket,:date,:slot,:revision,:id,:available,:payload) IF NOT EXISTS;
""";
    public const string InsertObservationById = """
INSERT INTO option_iv_observation_by_id(environment,observation_id,payload)
VALUES(:environment,:id,:payload) IF NOT EXISTS;
""";
    public const string SelectObservationById = """
SELECT payload FROM option_iv_observation_by_id WHERE environment=:environment AND observation_id=:id;
""";
    public const string SelectObservationHistory = """
SELECT payload FROM option_iv_observation_history
WHERE environment=:environment AND series_id=:series AND methodology_version=:methodology
AND calendar_bucket=:bucket AND value_date>=:from_date AND value_date<=:to_date;
""";
    public const string InsertSnapshot = """
INSERT INTO option_iv_snapshot_by_id(environment,snapshot_id,snapshot_digest,publication_sequence,payload)
VALUES(:environment,:id,:digest,:sequence,:payload) IF NOT EXISTS;
""";
    public const string SelectSnapshot = """
SELECT snapshot_digest,publication_sequence,payload FROM option_iv_snapshot_by_id
WHERE environment=:environment AND snapshot_id=:id;
""";
    public const string InsertMetricHistory = """
INSERT INTO option_iv_metric_history(environment,series_id,methodology_version,metric_policy_version,
 calendar_bucket,value_date,sampling_slot,revision,snapshot_id,available_at_utc,publication_sequence,payload)
VALUES(:environment,:series,:methodology,:policy,:bucket,:date,:slot,:revision,:id,:available,:sequence,:payload)
IF NOT EXISTS;
""";
    public const string SelectMetricHistory = """
SELECT payload FROM option_iv_metric_history
WHERE environment=:environment AND series_id=:series AND methodology_version=:methodology
AND metric_policy_version=:policy AND calendar_bucket=:bucket
AND value_date>=:from_date AND value_date<=:to_date;
""";
    public const string InsertLatest = """
INSERT INTO option_iv_latest(environment,series_id,methodology_version,metric_policy_version,
 snapshot_id,snapshot_digest,publication_sequence,available_at_utc)
VALUES(:environment,:series,:methodology,:policy,:id,:digest,:sequence,:available) IF NOT EXISTS;
""";
    public const string AdvanceLatest = """
UPDATE option_iv_latest SET snapshot_id=:id,snapshot_digest=:digest,publication_sequence=:sequence,
 available_at_utc=:available WHERE environment=:environment AND series_id=:series
AND methodology_version=:methodology AND metric_policy_version=:policy
IF publication_sequence < :sequence;
""";
    public const string SelectLatest = """
SELECT snapshot_id,snapshot_digest,publication_sequence,available_at_utc FROM option_iv_latest
WHERE environment=:environment AND series_id=:series AND methodology_version=:methodology
AND metric_policy_version=:policy;
""";
}
