//! Immutable incident identity and exact transition capture, called by the native authority.
use super::outbox::Sink;
use crate::{
    dataset::transport::Lane,
    messaging::wire::guid,
    runtime::{clock::Clock, recovery::Status},
};
use std::sync::Arc;
use tokio::time::Instant;

/// Copied configuration, installed before dataset intent starts. Contains no provider owner.
#[derive(Clone)]
pub struct Context {
    /// Native runtime identity, independent of a dataset replacement.
    pub runtime: [u8; 16],
    /// Dataset role/name.
    pub lane: Lane,
    /// Authoritative API-supplied date.
    pub date: time::Date,
    /// API UTC anchor, never a locally inferred trading date.
    pub clock: Arc<Clock>,
    /// Nonblocking root outbox ingress.
    pub sink: Sink,
    /// Copied workload identities/counts, never business actors or provider leases.
    pub workloads: Vec<String>,
}
/// One incident spanning retries, the final reset and its actual result.
pub struct Incident {
    context: Context,
    operation: String,
    sequence: u64,
    retries: u64,
    reset_record: [u8; 16],
    failure_record: [u8; 16],
    started: i64,
    deadline: i64,
    elapsed: Instant,
    original: String,
    old_owner: String,
    new_owner: String,
    reset_deadline: Option<i64>,
    completed: bool,
    failed: bool,
    monitoring: bool,
    provider_seen: bool,
    notices: Vec<String>,
}
/// Explicit UTC text accepted by DateTimeOffset. Ticks are Unix-relative 100ns values.
pub fn utc(ticks: i64) -> String {
    chrono::DateTime::from_timestamp(
        ticks.div_euclid(10_000_000),
        (ticks.rem_euclid(10_000_000) * 100) as u32,
    )
    .map(|t| t.to_rfc3339_opts(chrono::SecondsFormat::Nanos, true))
    .unwrap_or_default()
}
impl Incident {
    /// Opens a fault once, with immutable start/deadline and original cause.
    pub fn start(context: Context, status: &Status, owner: String) -> Option<Self> {
        let Ok(at) = context.clock.utc_ticks() else {
            // A failed API time source must not fabricate a 1970 incident.
            context.sink.reject_capture();
            return None;
        };
        let remaining = status
            .deadline
            .map_or(std::time::Duration::ZERO, |d| d.remaining());
        let mut incident = Self {
            context,
            operation: guid(rand::random::<u128>().max(1).to_le_bytes()),
            sequence: 0,
            retries: 0,
            reset_record: rand::random::<u128>().max(1).to_le_bytes(),
            failure_record: rand::random::<u128>().max(1).to_le_bytes(),
            started: at,
            deadline: at.saturating_add((remaining.as_nanos() / 100) as i64),
            elapsed: Instant::now(),
            original: status
                .root_error
                .clone()
                .unwrap_or_else(|| "Native dataset fault".into()),
            old_owner: owner.clone(),
            new_owner: owner,
            reset_deadline: None,
            completed: false,
            failed: false,
            monitoring: false,
            provider_seen: false,
            notices: Vec::new(),
        };
        incident.emit("Started", status, false);
        Some(incident)
    }
    /// Whether a later fault must receive a new operation identity.
    pub fn ended(&self) -> bool {
        self.completed || self.failed
    }
    /// Current incident identity, also used by UI correlation.
    pub fn operation(&self) -> &str {
        &self.operation
    }
    /// Takes copied reset outcomes for the independent UI delivery journal.
    pub fn take_notices(&mut self) -> Vec<String> {
        std::mem::take(&mut self.notices)
    }
    /// Preallocated fact identity lets the immediate UI alert correlate without waiting for storage.
    /// ResetStarted is still emitted only when the supervisor actually enters reset.
    pub fn terminal_record(&self, resetting: bool) -> [u8; 16] {
        if resetting {
            self.reset_record
        } else {
            self.failure_record
        }
    }
    /// Reports actual full-reset entry, independently of delivery or the soft deadline notification.
    pub fn reset_started(&mut self, status: &Status) {
        self.reset_deadline = Some(
            self.context
                .clock
                .utc_ticks()
                .unwrap_or(self.started)
                .saturating_add(1_200_000_000),
        );
        self.emit("ResetStarted", status, false);
    }
    /// Counts physical reconstruction attempts after the incident opened.
    pub fn attempted(&mut self) {
        self.retries = self.retries.saturating_add(1);
    }
    /// Tracks each actual replacement, including soft retries inside the final reset window.
    pub fn current_owner(&mut self, owner: String) {
        self.new_owner = owner;
    }
    /// Records successful resource retirement plus the factory's real replacement owner/generation.
    pub fn reset_completed(&mut self, status: &Status, owner: String) {
        // A factory can return at the terminal deadline boundary. Its late
        // identity must not append a success fact after a committed failure.
        if self.ended() {
            return;
        }
        self.new_owner = owner;
        self.emit("ResetCompleted", status, true);
    }
    /// Emits each authoritative phase once. Connection success does not manufacture financial readiness.
    pub fn connection(&mut self, status: &Status, monitoring: bool) {
        if !self.completed && !self.failed {
            self.monitoring = monitoring;
            self.provider_seen = true;
            let retired = !status
                .missing
                .contains(&crate::runtime::recovery::Proof::Retired);
            self.emit("ConnectionRestored", status, retired);
            self.emit("Completed", status, retired);
            self.completed = true;
        }
    }
    /// Full workload evidence may arrive after a transport-only completion.
    pub fn monitoring(&mut self, status: &Status, monitoring: bool) {
        if self.failed || self.monitoring {
            return;
        }
        if !self.completed {
            self.monitoring = monitoring;
            self.emit("Completed", status, true);
            self.completed = true;
        } else if monitoring {
            self.monitoring = true;
            self.emit("MonitoringRestored", status, true);
        }
    }
    /// Explicit failure/interruption; no acknowledgement path can create this fact.
    pub fn finish(&mut self, kind: &str, status: &Status) {
        if !self.ended() {
            self.emit(kind, status, false);
            self.failed = true;
        }
    }
    fn emit(&mut self, kind: &str, status: &Status, retired: bool) {
        self.sequence = self.sequence.saturating_add(1);
        let Ok(at) = self.context.clock.utc_ticks() else {
            self.context.sink.reject_capture();
            return;
        };
        let at = at.max(self.started);
        let ready = self.monitoring && status.missing.is_empty();
        let missing: Vec<_> = status.missing.iter().map(|p| format!("{p:?}")).collect();
        let record = guid(match kind {
            "ResetStarted" => self.reset_record,
            "Failed" => self.failure_record,
            _ => rand::random::<u128>().max(1).to_le_bytes(),
        });
        // Every fault incident gets an explicit outcome. Healthy startup and
        // ordinary quiet market activity never create an incident or a popup.
        if matches!(kind, "Completed" | "MonitoringRestored") && (self.provider_seen || ready) {
            let detail = match (self.reset_deadline.is_some(), ready) {
                (true, true) => "Dataset reset recovered. Live monitoring is verified.",
                (true, false) => {
                    "Dataset reset completed. Feed connection restored. Live monitoring is not yet verified."
                }
                (false, true) => "Dataset recovery completed. Live monitoring is verified.",
                (false, false) => {
                    "Dataset recovery completed. Feed connection restored. Live monitoring is not yet verified."
                }
            };
            self.notices.push(serde_json::json!({"Version":1,"RecordId":record,"OperationId":self.operation,
                "Role":self.context.lane.role(),"Dataset":self.context.lane.dataset(),"ValueDate":self.context.date.to_string(),
                "OccurredAtUtc":utc(at),"Severity":"success","MonitoringReady":ready,
                "Message":format!("{}/{}: {detail} OperationId: {}.",self.context.lane.role(),self.context.lane.dataset(),self.operation)}).to_string());
        }
        let payload=serde_json::json!({"Version":1,"RecordId":record,
            "OperationId":self.operation,"Sequence":self.sequence,"Kind":kind,"RuntimeId":guid(self.context.runtime),
            "Role":self.context.lane.role(),"Dataset":self.context.lane.dataset(),"ValueDate":self.context.date.to_string(),
            "OccurredAtUtc":utc(at),"StartedAtUtc":utc(self.started),"RecoveryDeadlineUtc":utc(self.deadline),
            "ResetDeadlineUtc":self.reset_deadline.map(utc),"ElapsedMilliseconds":self.elapsed.elapsed().as_millis() as u64,
            "OldOwner":self.old_owner,"NewOwner":self.new_owner,"Generation":status.ticket.parts()[2],
            "RetryCount":self.retries,"Revision":status.ticket.parts()[1],"EpisodeCounter":status.ticket.parts()[0],"OriginalError":self.original,"LatestError":status.latest_error.clone().unwrap_or_default(),
            "CompletionScope":if ready {"MonitoringReady"}else if status.missing.is_empty(){"DatasetOperational"}else{"TransportOnly"},"OldResourcesRetired":retired,
            "SubscriptionVerified":false,"ProviderActivityVerified":self.provider_seen,
            "MonitoringReady":ready,"MissingProofs":missing,"AffectedWorkloads":self.context.workloads,"DiagnosticsTruncated":self.original.len()>=4096
        }).to_string();
        self.context.sink.capture(
            payload,
            matches!(
                kind,
                "Failed" | "Completed" | "Interrupted" | "ResetStarted"
            ),
        );
    }
}
