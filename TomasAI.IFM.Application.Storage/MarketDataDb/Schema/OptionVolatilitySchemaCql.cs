namespace TomasAI.IFM.Application.Storage.MarketDataDb.Schema;

public static class OptionVolatilitySchemaCql
{
    public const string CreateObservationHistory = """
CREATE TABLE IF NOT EXISTS option_iv_observation_history(
 environment text,series_id text,methodology_version text,calendar_bucket int,value_date date,
 sampling_slot text,revision int,observation_id text,available_at_utc timestamp,payload blob,
 PRIMARY KEY((environment,series_id,methodology_version,calendar_bucket),value_date,sampling_slot,revision,observation_id))
WITH CLUSTERING ORDER BY(value_date ASC,sampling_slot ASC,revision DESC,observation_id ASC);
""";
    public const string CreateObservationById = """
CREATE TABLE IF NOT EXISTS option_iv_observation_by_id(
 environment text,observation_id text,payload blob,PRIMARY KEY((environment,observation_id)));
""";
    public const string CreateMetricHistory = """
CREATE TABLE IF NOT EXISTS option_iv_metric_history(
 environment text,series_id text,methodology_version text,metric_policy_version text,calendar_bucket int,
 value_date date,sampling_slot text,revision int,snapshot_id text,available_at_utc timestamp,
 publication_sequence bigint,payload blob,
 PRIMARY KEY((environment,series_id,methodology_version,metric_policy_version,calendar_bucket),
 value_date,sampling_slot,revision,snapshot_id))
WITH CLUSTERING ORDER BY(value_date ASC,sampling_slot ASC,revision DESC,snapshot_id ASC);
""";
    public const string CreateSnapshotById = """
CREATE TABLE IF NOT EXISTS option_iv_snapshot_by_id(
 environment text,snapshot_id text,snapshot_digest text,publication_sequence bigint,payload blob,
 PRIMARY KEY((environment,snapshot_id)));
""";
    public const string CreateLatest = """
CREATE TABLE IF NOT EXISTS option_iv_latest(
 environment text,series_id text,methodology_version text,metric_policy_version text,
 snapshot_id text,snapshot_digest text,publication_sequence bigint,available_at_utc timestamp,
 PRIMARY KEY((environment,series_id,methodology_version,metric_policy_version)));
""";
}
