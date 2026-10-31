//! Concrete HTTP health snapshots, matching UI.Net.Models/MarketData contracts.
use super::cells::{Cell, Cells, ScalarCell};
use serde::Deserialize;
#[derive(Clone, Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MarketDataOperationsHealthSnapshot {
    pub schema_version: Option<i32>,
    pub revision: Option<i64>,
    pub observed_on_utc: Option<String>,
    pub overall_status: Option<String>,
    pub session_state: Option<String>,
    pub value_date: Option<String>,
    pub last_probe_utc: Option<String>,
    pub next_probe_utc: Option<String>,
    pub rejected_stale_generation_publications: Option<i64>,
    pub stages: Option<Vec<MarketDataOperationStageSnapshot>>,
    pub datasets: Option<Vec<MarketDataDatasetHealthSnapshot>>,
}
impl Cells for MarketDataOperationsHealthSnapshot {
    fn cell(&self, column: &str) -> Cell<'_> {
        match column {
            "schemaVersion" => self.schema_version.scalar_cell(),
            "revision" => self.revision.scalar_cell(),
            "observedOnUtc" => self.observed_on_utc.scalar_cell(),
            "overallStatus" => self.overall_status.scalar_cell(),
            "sessionState" => self.session_state.scalar_cell(),
            "valueDate" => self.value_date.scalar_cell(),
            "lastProbeUtc" => self.last_probe_utc.scalar_cell(),
            "nextProbeUtc" => self.next_probe_utc.scalar_cell(),
            "rejectedStaleGenerationPublications" => {
                self.rejected_stale_generation_publications.scalar_cell()
            }
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MarketDataOperationStageSnapshot {
    pub stage: Option<String>,
    pub status: Option<String>,
    pub reason_code: Option<String>,
    pub reason: Option<String>,
    pub required: Option<bool>,
    pub received: Option<i64>,
    pub completed: Option<i64>,
    pub failed: Option<i64>,
    pub coalesced: Option<i64>,
    pub pending: Option<i64>,
    pub capacity: Option<i64>,
    pub high_water: Option<i64>,
    pub saturated: Option<i64>,
    pub last_observed_utc: Option<String>,
    pub last_succeeded_utc: Option<String>,
    pub last_failed_utc: Option<String>,
    pub market_data_as_of_utc: Option<String>,
    pub oldest_pending_age: Option<String>,
    pub average_latency: Option<String>,
    pub maximum_latency: Option<String>,
    pub p50_latency: Option<String>,
    pub p95_latency: Option<String>,
    pub p99_latency: Option<String>,
}
impl Cells for MarketDataOperationStageSnapshot {
    fn cell(&self, column: &str) -> Cell<'_> {
        match column {
            "stage" => self.stage.scalar_cell(),
            "status" => self.status.scalar_cell(),
            "reasonCode" => self.reason_code.scalar_cell(),
            "reason" => self.reason.scalar_cell(),
            "required" => self.required.scalar_cell(),
            "received" => self.received.scalar_cell(),
            "completed" => self.completed.scalar_cell(),
            "failed" => self.failed.scalar_cell(),
            "coalesced" => self.coalesced.scalar_cell(),
            "pending" => self.pending.scalar_cell(),
            "capacity" => self.capacity.scalar_cell(),
            "highWater" => self.high_water.scalar_cell(),
            "saturated" => self.saturated.scalar_cell(),
            "lastObservedUtc" => self.last_observed_utc.scalar_cell(),
            "lastSucceededUtc" => self.last_succeeded_utc.scalar_cell(),
            "lastFailedUtc" => self.last_failed_utc.scalar_cell(),
            "marketDataAsOfUtc" => self.market_data_as_of_utc.scalar_cell(),
            "oldestPendingAge" => self.oldest_pending_age.scalar_cell(),
            "averageLatency" => self.average_latency.scalar_cell(),
            "maximumLatency" => self.maximum_latency.scalar_cell(),
            "p50Latency" => self.p50_latency.scalar_cell(),
            "p95Latency" => self.p95_latency.scalar_cell(),
            "p99Latency" => self.p99_latency.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
#[derive(Clone, Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MarketDataDatasetHealthSnapshot {
    pub dataset: Option<String>,
    pub status: Option<String>,
    pub reason: Option<String>,
    pub session_state: Option<String>,
    pub value_date: Option<String>,
    pub process_id: Option<i32>,
    pub worker_instance_id: Option<String>,
    pub generation_id: Option<String>,
    pub started_on_utc: Option<String>,
    pub last_observed_utc: Option<String>,
    pub last_healthy_utc: Option<String>,
    pub incident_opened_utc: Option<String>,
    pub next_probe_utc: Option<String>,
    pub incident_age: Option<String>,
    pub cooperative_attempts: Option<i32>,
    pub process_replacement_count: Option<i32>,
    pub process_replacement_latched: Option<bool>,
    pub running: Option<bool>,
    pub healthy: Option<bool>,
    pub graceful_stop_succeeded: Option<bool>,
    pub forced_termination: Option<bool>,
    pub records_produced: Option<u64>,
    pub records_consumed: Option<u64>,
    pub ring_used: Option<u64>,
    pub ring_capacity: Option<u64>,
    pub channel_batch_count: Option<i32>,
    pub channel_batch_capacity: Option<i32>,
    pub records_started: Option<i64>,
    pub records_completed: Option<i64>,
}
impl Cells for MarketDataDatasetHealthSnapshot {
    fn cell(&self, column: &str) -> Cell<'_> {
        match column {
            "dataset" => self.dataset.scalar_cell(),
            "status" => self.status.scalar_cell(),
            "reason" => self.reason.scalar_cell(),
            "sessionState" => self.session_state.scalar_cell(),
            "valueDate" => self.value_date.scalar_cell(),
            "processId" => self.process_id.scalar_cell(),
            "workerInstanceId" => self.worker_instance_id.scalar_cell(),
            "generationId" => self.generation_id.scalar_cell(),
            "startedOnUtc" => self.started_on_utc.scalar_cell(),
            "lastObservedUtc" => self.last_observed_utc.scalar_cell(),
            "lastHealthyUtc" => self.last_healthy_utc.scalar_cell(),
            "incidentOpenedUtc" => self.incident_opened_utc.scalar_cell(),
            "nextProbeUtc" => self.next_probe_utc.scalar_cell(),
            "incidentAge" => self.incident_age.scalar_cell(),
            "cooperativeAttempts" => self.cooperative_attempts.scalar_cell(),
            "processReplacementCount" => self.process_replacement_count.scalar_cell(),
            "processReplacementLatched" => self.process_replacement_latched.scalar_cell(),
            "running" => self.running.scalar_cell(),
            "healthy" => self.healthy.scalar_cell(),
            "gracefulStopSucceeded" => self.graceful_stop_succeeded.scalar_cell(),
            "forcedTermination" => self.forced_termination.scalar_cell(),
            "recordsProduced" => self.records_produced.scalar_cell(),
            "recordsConsumed" => self.records_consumed.scalar_cell(),
            "ringUsed" => self.ring_used.scalar_cell(),
            "ringCapacity" => self.ring_capacity.scalar_cell(),
            "channelBatchCount" => self.channel_batch_count.scalar_cell(),
            "channelBatchCapacity" => self.channel_batch_capacity.scalar_cell(),
            "recordsStarted" => self.records_started.scalar_cell(),
            "recordsCompleted" => self.records_completed.scalar_cell(),
            _ => Cell::Missing,
        }
    }
}
