//! Concrete HTTP health snapshots, matching UI.Net.Models/MarketData contracts.
use super::cells::{Cell, Cells, ScalarCell};
use serde::Deserialize;

/// Market Data Operations Health Snapshot used by the operations health component.
#[derive(Clone, Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MarketDataOperationsHealthSnapshot {
    /// Optional schema version; `None` represents an absent value.
    pub schema_version: Option<i32>,
    /// Optional revision; `None` represents an absent value.
    pub revision: Option<i64>,
    /// Optional observed on utc; `None` represents an absent value.
    pub observed_on_utc: Option<String>,
    /// Optional overall status; `None` represents an absent value.
    pub overall_status: Option<String>,
    /// Optional session state; `None` represents an absent value.
    pub session_state: Option<String>,
    /// Optional value date; `None` represents an absent value.
    pub value_date: Option<String>,
    /// Optional last probe utc; `None` represents an absent value.
    pub last_probe_utc: Option<String>,
    /// Optional next probe utc; `None` represents an absent value.
    pub next_probe_utc: Option<String>,
    /// Optional rejected stale generation publications; `None` represents an absent value.
    pub rejected_stale_generation_publications: Option<i64>,
    /// Optional stages; `None` represents an absent value.
    pub stages: Option<Vec<MarketDataOperationStageSnapshot>>,
    /// Optional datasets; `None` represents an absent value.
    pub datasets: Option<Vec<MarketDataDatasetHealthSnapshot>>,
}
impl Cells for MarketDataOperationsHealthSnapshot {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Market Data Operation Stage Snapshot used by the operations health component.
#[derive(Clone, Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MarketDataOperationStageSnapshot {
    /// Optional stage; `None` represents an absent value.
    pub stage: Option<String>,
    /// Current status text or state exposed by this component.
    pub status: Option<String>,
    /// Optional reason code; `None` represents an absent value.
    pub reason_code: Option<String>,
    /// Optional reason; `None` represents an absent value.
    pub reason: Option<String>,
    /// Optional required; `None` represents an absent value.
    pub required: Option<bool>,
    /// Optional received; `None` represents an absent value.
    pub received: Option<i64>,
    /// Optional completed; `None` represents an absent value.
    pub completed: Option<i64>,
    /// Optional failed; `None` represents an absent value.
    pub failed: Option<i64>,
    /// Optional coalesced; `None` represents an absent value.
    pub coalesced: Option<i64>,
    /// Optional pending; `None` represents an absent value.
    pub pending: Option<i64>,
    /// Optional capacity; `None` represents an absent value.
    pub capacity: Option<i64>,
    /// Optional high water; `None` represents an absent value.
    pub high_water: Option<i64>,
    /// Optional saturated; `None` represents an absent value.
    pub saturated: Option<i64>,
    /// Optional last observed utc; `None` represents an absent value.
    pub last_observed_utc: Option<String>,
    /// Optional last succeeded utc; `None` represents an absent value.
    pub last_succeeded_utc: Option<String>,
    /// Optional last failed utc; `None` represents an absent value.
    pub last_failed_utc: Option<String>,
    /// Optional market data as of utc; `None` represents an absent value.
    pub market_data_as_of_utc: Option<String>,
    /// Optional oldest pending age; `None` represents an absent value.
    pub oldest_pending_age: Option<String>,
    /// Optional average latency; `None` represents an absent value.
    pub average_latency: Option<String>,
    /// Optional maximum latency; `None` represents an absent value.
    pub maximum_latency: Option<String>,
    /// Optional p50 latency; `None` represents an absent value.
    pub p50_latency: Option<String>,
    /// Optional p95 latency; `None` represents an absent value.
    pub p95_latency: Option<String>,
    /// Optional p99 latency; `None` represents an absent value.
    pub p99_latency: Option<String>,
}
impl Cells for MarketDataOperationStageSnapshot {
    /// Resolves a named display cell; unsupported columns remain missing.
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

/// Market Data Dataset Health Snapshot used by the operations health component.
#[derive(Clone, Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct MarketDataDatasetHealthSnapshot {
    /// Native counters sampled by the host, absent when not yet captured.
    pub counters: Option<std::collections::BTreeMap<String, i64>>,
    /// Resource implementation determines which diagnostic fields apply.
    pub resource_kind: Option<String>,
    /// Optional dataset; `None` represents an absent value.
    pub dataset: Option<String>,
    /// Current status text or state exposed by this component.
    pub status: Option<String>,
    /// Optional reason; `None` represents an absent value.
    pub reason: Option<String>,
    /// Optional session state; `None` represents an absent value.
    pub session_state: Option<String>,
    /// Optional value date; `None` represents an absent value.
    pub value_date: Option<String>,
    /// Optional process id; `None` represents an absent value.
    pub process_id: Option<i32>,
    /// Optional worker instance id; `None` represents an absent value.
    pub worker_instance_id: Option<String>,
    /// Optional generation id; `None` represents an absent value.
    pub generation_id: Option<String>,
    /// Optional started on utc; `None` represents an absent value.
    pub started_on_utc: Option<String>,
    /// Optional last observed utc; `None` represents an absent value.
    pub last_observed_utc: Option<String>,
    /// Optional last healthy utc; `None` represents an absent value.
    pub last_healthy_utc: Option<String>,
    /// Optional incident opened utc; `None` represents an absent value.
    pub incident_opened_utc: Option<String>,
    /// Optional next probe utc; `None` represents an absent value.
    pub next_probe_utc: Option<String>,
    /// Optional incident age; `None` represents an absent value.
    pub incident_age: Option<String>,
    /// Optional cooperative attempts; `None` represents an absent value.
    pub cooperative_attempts: Option<i32>,
    /// Optional process replacement count; `None` represents an absent value.
    pub process_replacement_count: Option<i32>,
    /// Optional process replacement latched; `None` represents an absent value.
    pub process_replacement_latched: Option<bool>,
    /// Optional running; `None` represents an absent value.
    pub running: Option<bool>,
    /// Optional healthy; `None` represents an absent value.
    pub healthy: Option<bool>,
    /// Optional graceful stop succeeded; `None` represents an absent value.
    pub graceful_stop_succeeded: Option<bool>,
    /// Optional forced termination; `None` represents an absent value.
    pub forced_termination: Option<bool>,
    /// Optional records produced; `None` represents an absent value.
    pub records_produced: Option<u64>,
    /// Optional records consumed; `None` represents an absent value.
    pub records_consumed: Option<u64>,
    /// Optional ring used; `None` represents an absent value.
    pub ring_used: Option<u64>,
    /// Optional ring capacity; `None` represents an absent value.
    pub ring_capacity: Option<u64>,
    /// Optional channel batch count; `None` represents an absent value.
    pub channel_batch_count: Option<i32>,
    /// Optional channel batch capacity; `None` represents an absent value.
    pub channel_batch_capacity: Option<i32>,
    /// Optional records started; `None` represents an absent value.
    pub records_started: Option<i64>,
    /// Optional records completed; `None` represents an absent value.
    pub records_completed: Option<i64>,
}
impl Cells for MarketDataDatasetHealthSnapshot {
    /// Resolves a named display cell; unsupported columns remain missing.
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
