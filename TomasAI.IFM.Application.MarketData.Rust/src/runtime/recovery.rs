//! Dataset-wide readiness and one original recovery deadline. This authority
//! stores copied control evidence only; it never holds a provider resource or
//! awaits a database, broker, managed callback, or downstream consumer.
use super::{
    Deadline,
    owner::{Owner, OwnerError},
};
use std::{
    collections::VecDeque,
    sync::{Arc, Mutex},
};
use tokio::{sync::watch, task::JoinHandle};

/// Independent evidence required before declaring a workload healthy.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
#[repr(u8)]
pub enum Proof {
    /// All previous physical resources have actually joined.
    Retired = 1,
    /// Replacement provider acknowledged the desired subscription.
    Transport = 2,
    /// Current reviewed reference context is qualified.
    Reference = 4,
    /// Current-generation native pricing completed.
    Pricing = 8,
    /// The broker acknowledged a current-generation publication.
    Publication = 16,
    /// Required copied evidence has been durably acknowledged.
    Persistence = 32,
}
/// A bounded set of workload-specific readiness evidence, not admission gates.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Requirements(u8);
impl Requirements {
    /// Creates a set from explicit native workload requirements. Empty means
    /// control-only idle, which still requires retirement proof.
    pub fn new(proofs: &[Proof]) -> Self {
        Self(
            proofs
                .iter()
                .fold(Proof::Retired as u8, |bits, p| bits | *p as u8),
        )
    }
}
/// Stale resource completions cannot certify a new intent or physical owner.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Ticket {
    episode: u64,
    revision: u64,
    generation: u64,
}
impl Ticket {
    /// Copied opaque episode/revision/generation counters for native API queries.
    /// They identify control evidence, not provider tasks or CLR object handles.
    pub fn parts(self) -> [u64; 3] {
        [self.episode, self.revision, self.generation]
    }
}
/// Externally observable recovery outcome.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub enum Phase {
    /// Acquiring and proving the first physical owner.
    Starting,
    /// Every required proof for the current ticket is present.
    Running,
    /// Retrying within the original deadline, including retirement.
    Recovering,
    /// Deadline expired; the failure remains queryable until explicit retry.
    Failed,
    /// Explicit stop; no automatic recovery was requested.
    Stopped,
}
/// Bounded copied status retained independently of message delivery.
#[derive(Clone, Debug)]
pub struct Status {
    /// Current outcome.
    pub phase: Phase,
    /// Identity required on all asynchronous readiness/failure reports.
    pub ticket: Ticket,
    /// Original episode deadline, preserved on failure.
    pub deadline: Option<Deadline>,
    /// Missing evidence, at most six elements.
    pub missing: Vec<Proof>,
    /// First sanitized failure; callers must redact credentials before reporting.
    pub root_error: Option<String>,
    /// Most recent sanitized failure, bounded to 4096 UTF-8 bytes.
    pub latest_error: Option<String>,
}
/// Retained terminal notification, independent of the current recovery state and
/// replaceable dataset resources. Stop/retry cannot erase an undisplayed failure.
#[derive(Clone, Debug)]
pub struct Terminal {
    /// Monotonic runtime-local notification identity, for idempotent UI delivery.
    pub ordinal: u64,
    /// Complete original failed episode and bounded causal text.
    pub status: Status,
    /// Older undisplayed notifications replaced by this bounded journal record.
    pub replaced: u64,
    /// The supervisor has committed to one automatic full reset on this expiry.
    /// Captured with the failure so delayed delivery cannot mislabel a final failure.
    pub resetting: bool,
}
/// Copied notification identity/time retained independently of publication retries.
#[derive(Clone, Default)]
pub struct NotificationMetadata {
    /// Shared authoritative incident identity.
    pub operation: Option<String>,
    /// Original terminal occurrence in API Unix-relative UTC ticks.
    pub occurred: Option<i64>,
    /// Exact audit reset/failure fact identity, in CLR Guid byte order.
    pub record: Option<[u8; 16]>,
}
struct State {
    status: Status,
    required: Requirements,
    proven: u8,
    terminal: VecDeque<Terminal>,
    ordinal: u64,
    reset_available: bool,
    transport_driven: bool,
    audit_context: Option<crate::audit::journal::Context>,
    incident: Option<crate::audit::journal::Incident>,
    physical_owner: String,
    reset_pending: bool,
    audit_acquiring: bool,
    monitoring_recorded: bool,
    audit_notices: VecDeque<(u64, NotificationMetadata)>,
    outcome_notices: VecDeque<String>,
}
/// One authority per independently resettable dataset/role.
pub struct Recovery {
    state: Mutex<State>,
    changed: watch::Sender<Status>,
}
const PROOFS: [Proof; 6] = [
    Proof::Retired,
    Proof::Transport,
    Proof::Reference,
    Proof::Pricing,
    Proof::Publication,
    Proof::Persistence,
];
fn bounded(s: &str) -> String {
    let mut end = s.len().min(4096);
    while !s.is_char_boundary(end) {
        end -= 1;
    }
    s[..end].to_owned()
}
impl Recovery {
    /// Starts one original setup window. Requirements must describe actual
    /// desired work; quiet/idle scopes must not fabricate market-data evidence.
    pub fn new(required: Requirements, deadline: Deadline) -> Arc<Self> {
        let status = Status {
            phase: Phase::Starting,
            ticket: Ticket {
                episode: 1,
                revision: 1,
                generation: 1,
            },
            deadline: Some(deadline),
            missing: PROOFS
                .into_iter()
                .filter(|p| required.0 & *p as u8 != 0)
                .collect(),
            root_error: None,
            latest_error: None,
        };
        let (changed, _) = watch::channel(status.clone());
        Arc::new(Self {
            state: Mutex::new(State {
                status,
                required,
                proven: 0,
                terminal: VecDeque::with_capacity(16),
                ordinal: 0,
                reset_available: false,
                transport_driven: false,
                audit_context: None,
                incident: None,
                physical_owner: String::new(),
                reset_pending: false,
                audit_acquiring: false,
                monitoring_recorded: false,
                audit_notices: VecDeque::new(),
                outcome_notices: VecDeque::new(),
            }),
            changed,
        })
    }
    /// Installs copied audit identity before starting desired dataset resources.
    pub fn audit_context(&self, context: crate::audit::journal::Context) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        if state.audit_context.is_none() {
            state.audit_context = Some(context);
        }
    }
    fn audit_start(state: &mut State) {
        if state.incident.as_ref().is_none_or(|i| i.ended())
            && let Some(context) = state.audit_context.clone()
        {
            state.incident = crate::audit::journal::Incident::start(
                context,
                &state.status,
                state.physical_owner.clone(),
            );
            state.monitoring_recorded = false;
        }
    }
    /// Updates copied desired membership before a physical acquisition begins.
    pub fn audit_workloads(&self, workloads: Vec<String>) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        if let Some(context) = state.audit_context.as_mut() {
            context.workloads = workloads
                .into_iter()
                .take(256)
                .map(|s| s.chars().take(256).collect())
                .collect();
        }
    }
    /// Captures an intentional interruption without certifying cleanup or changing desired intent.
    pub fn audit_interrupt(&self, reason: &str) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        let mut status = state.status.clone();
        status.latest_error = Some(bounded(reason));
        if let Some(incident) = state.incident.as_mut() {
            incident.finish("Interrupted", &status);
        }
    }
    /// Copied reconstruction attempt count, with no publication or persistence work.
    pub fn audit_attempt(&self) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        state.audit_acquiring = true;
        if let Some(incident) = state.incident.as_mut() {
            incident.attempted();
        }
    }
    /// Actual entry to the supervisor's emergency reset, not a publication intention.
    pub fn audit_reset_started(&self) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        Self::audit_start(&mut state);
        let status = state.status.clone();
        if let Some(incident) = state.incident.as_mut() {
            incident.reset_started(&status);
        }
        state.reset_pending = true;
    }
    /// Factory-generated real worker/generation identity, after old resources joined.
    pub fn audit_owner(&self, identity: Option<crate::messaging::ordered::Header>) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        state.audit_acquiring = false;
        let Some(identity) = identity else {
            Self::audit_proofs(&mut state);
            return;
        };
        state.physical_owner = format!(
            "{}/{}",
            crate::messaging::wire::guid(identity.worker),
            crate::messaging::wire::guid(identity.generation)
        );
        let current_owner = state.physical_owner.clone();
        if let Some(incident) = state.incident.as_mut() {
            incident.current_owner(current_owner);
        }
        if state.reset_pending {
            let status = state.status.clone();
            let owner = state.physical_owner.clone();
            if let Some(incident) = state.incident.as_mut() {
                incident.reset_completed(&status, owner);
            }
            state.reset_pending = false;
        }
        Self::audit_proofs(&mut state);
    }
    // Factory tasks may report proofs before synchronous start() returns its owner.
    // Delay only audit facts until that identity/reset fact is installed; feed
    // readiness and cancellation keep progressing independently.
    fn audit_proofs(state: &mut State) {
        if state.audit_acquiring || state.reset_pending {
            return;
        }
        let status = state.status.clone();
        let financial = Proof::Reference as u8
            | Proof::Pricing as u8
            | Proof::Publication as u8
            | Proof::Persistence as u8;
        let monitoring = state.required.0 & financial == financial && status.missing.is_empty();
        if state.transport_driven
            && state.proven & Proof::Transport as u8 != 0
            && let Some(incident) = state.incident.as_mut()
        {
            incident.connection(&status, monitoring);
        }
        if status.missing.is_empty()
            && !state.monitoring_recorded
            && let Some(incident) = state.incident.as_mut()
        {
            incident.monitoring(&status, monitoring);
            state.monitoring_recorded = true;
        }
        if let Some(incident) = state.incident.as_mut() {
            for notice in incident.take_notices() {
                if state.outcome_notices.len() == 16 {
                    state.outcome_notices.pop_front();
                }
                state.outcome_notices.push_back(notice);
            }
        }
    }
    /// Bounded retained reset outcomes; broker flush never acknowledges UI display.
    pub fn outcomes(&self) -> Vec<String> {
        self.state
            .lock()
            .unwrap_or_else(|e| e.into_inner())
            .outcome_notices
            .iter()
            .cloned()
            .collect()
    }
    /// Stable native incident correlation for operator notifications.
    pub fn audit_operation(&self) -> Option<String> {
        self.state
            .lock()
            .unwrap_or_else(|e| e.into_inner())
            .incident
            .as_ref()
            .map(|i| i.operation().to_owned())
    }
    /// Original occurrence/correlation survives every terminal publication retry.
    pub fn audit_notice(&self, ordinal: u64) -> NotificationMetadata {
        self.state
            .lock()
            .unwrap_or_else(|e| e.into_inner())
            .audit_notices
            .iter()
            .find(|entry| entry.0 == ordinal)
            .map(|entry| entry.1.clone())
            .unwrap_or_default()
    }
    fn retain_notice(state: &mut State, resetting: bool) {
        if state.audit_notices.len() == 16 {
            state.audit_notices.pop_front();
        }
        state.audit_notices.push_back((
            state.ordinal,
            NotificationMetadata {
                operation: state.incident.as_ref().map(|i| i.operation().to_owned()),
                occurred: state
                    .audit_context
                    .as_ref()
                    .and_then(|c| c.clock.utc_ticks().ok()),
                record: state
                    .incident
                    .as_ref()
                    .map(|i| i.terminal_record(resetting)),
            },
        ));
    }
    /// Reads current copied state. Reading also observes expiry, even if no task
    /// has polled the watchdog since the monotonic deadline elapsed.
    pub fn status(&self) -> Status {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        self.expire(&mut state);
        state.status.clone()
    }
    /// Event-driven control subscription; latest status is retained if full.
    pub fn subscribe(&self) -> watch::Receiver<Status> {
        self.changed.subscribe()
    }
    /// Separates provider connectivity from the remaining financial readiness
    /// evidence. Quiet data is pending business work, not a recovery episode.
    pub fn monitor_provider(&self) {
        self.state
            .lock()
            .unwrap_or_else(|e| e.into_inner())
            .transport_driven = true;
    }
    /// Actual current-generation transport evidence, without asserting a quote,
    /// valuation or delivered trading result exists.
    pub fn connection_alive(&self) -> bool {
        let state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        state.transport_driven
            && state.proven & Proof::Transport as u8 != 0
            && !matches!(state.status.phase, Phase::Failed | Phase::Stopped)
    }
    /// Declares the supervisor's next expiry action. This is copied notification
    /// metadata only: publication, acknowledgment and consumers never execute or
    /// gate the reset. Withdraw it before final-reset readiness or explicit stop.
    pub fn plan_final_reset(&self, available: bool) {
        self.state
            .lock()
            .unwrap_or_else(|e| e.into_inner())
            .reset_available = available;
    }
    /// Records an actual unsafe cleanup failure without claiming retirement or
    /// waiting for a timer. Delivery remains independent of the resource tree.
    pub fn fail(&self, detail: &str) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        state.status.phase = Phase::Failed;
        state
            .status
            .root_error
            .get_or_insert_with(|| bounded(detail));
        state.status.latest_error = Some(bounded(detail));
        Self::audit_start(&mut state);
        let status = state.status.clone();
        if let Some(incident) = state.incident.as_mut() {
            incident.finish("Failed", &status);
        }
        state.ordinal = state.ordinal.saturating_add(1);
        Self::retain_notice(&mut state, false);
        if state.terminal.len() == 16 {
            state.terminal.pop_front();
        }
        let terminal = Terminal {
            ordinal: state.ordinal,
            status: state.status.clone(),
            replaced: 0,
            resetting: false,
        };
        state.terminal.push_back(terminal);
        self.changed.send_replace(state.status.clone());
    }
    fn expire(&self, state: &mut State) {
        if matches!(state.status.phase, Phase::Starting | Phase::Recovering)
            && state
                .status
                .deadline
                .is_some_and(|d| d.remaining().is_zero())
        {
            state.status.phase = Phase::Failed;
            if state.status.root_error.is_none() {
                state.status.root_error = Some("Dataset readiness deadline expired".into());
            }
            state.status.latest_error =
                Some("The two-minute dataset recovery deadline expired".into());
            Self::audit_start(state);
            if !state.reset_available {
                let status = state.status.clone();
                if let Some(incident) = state.incident.as_mut() {
                    incident.finish("Failed", &status);
                }
            }
            state.ordinal = state.ordinal.saturating_add(1);
            let resetting = state.reset_available;
            Self::retain_notice(state, resetting);
            let replaced = if state.terminal.len() == 16 {
                state
                    .terminal
                    .pop_front()
                    .map_or(0, |old| old.replaced.saturating_add(1))
            } else {
                0
            };
            let resetting = state.reset_available;
            state.terminal.push_back(Terminal {
                ordinal: state.ordinal,
                status: state.status.clone(),
                replaced,
                resetting,
            });
            self.changed.send_replace(state.status.clone());
        }
    }
    /// Copies up to sixteen pending terminal messages. Reading current status,
    /// explicitly stopping or starting a new episode does not consume these.
    pub fn failures(&self, after: u64, maximum: usize) -> Vec<Terminal> {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        self.expire(&mut state);
        state
            .terminal
            .iter()
            .filter(|record| record.ordinal > after)
            .take(maximum.min(16))
            .cloned()
            .collect()
    }
    /// Acknowledges terminal messages only after an external recipient confirms
    /// delivery. Broker flush alone is not evidence that the UI showed a dialog.
    /// A future/stale ordinal cannot discard a notification not in this journal.
    pub fn acknowledge_failures(&self, through: u64) -> bool {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        if !state
            .terminal
            .iter()
            .any(|record| record.ordinal == through)
        {
            return false;
        }
        while state
            .terminal
            .front()
            .is_some_and(|record| record.ordinal <= through)
        {
            state.terminal.pop_front();
        }
        true
    }
    /// Records actual completion for this exact ticket. Late old-generation,
    /// stopped, failed, or deadline-expired completion is ignored.
    pub fn prove(&self, ticket: Ticket, proof: Proof) -> bool {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        self.expire(&mut state);
        if ticket != state.status.ticket
            || !matches!(state.status.phase, Phase::Starting | Phase::Recovering)
        {
            return false;
        }
        state.proven |= proof as u8;
        state.status.missing = PROOFS
            .into_iter()
            .filter(|p| state.required.0 & *p as u8 != 0 && state.proven & *p as u8 == 0)
            .collect();
        if state.status.missing.is_empty() {
            state.status.phase = Phase::Running;
            state.status.deadline = None;
            state.status.root_error = None;
            state.status.latest_error = None;
        }
        Self::audit_proofs(&mut state);
        self.changed.send_replace(state.status.clone());
        true
    }
    /// Ends bounded reconstruction only after the concrete final source and
    /// required components are installed. Financial readiness can then await
    /// genuine market data without treating a quiet connection as a failure.
    /// Bootstrap heartbeats alone cannot call this successfully.
    pub fn complete_setup(&self, ticket: Ticket) -> bool {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        self.expire(&mut state);
        if ticket != state.status.ticket
            || !matches!(state.status.phase, Phase::Starting | Phase::Recovering)
            || state.proven & Proof::Transport as u8 == 0
            || state.proven & Proof::Retired as u8 == 0
        {
            return false;
        }
        if state.status.deadline.is_none() && state.status.phase != Phase::Recovering {
            return true;
        }
        state.transport_driven = true;
        state.status.deadline = None;
        state.status.phase = Phase::Starting;
        Self::audit_proofs(&mut state);
        self.changed.send_replace(state.status.clone());
        true
    }
    /// Reports failure and retains the earliest original budget. An independent
    /// failure after complete recovery starts a new episode; retries never do.
    /// The returned ticket fences all pre-incident reports.
    pub fn fault(&self, ticket: Ticket, detail: &str, deadline: Deadline) -> Option<Ticket> {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        self.expire(&mut state);
        if ticket != state.status.ticket
            || matches!(state.status.phase, Phase::Failed | Phase::Stopped)
        {
            return None;
        }
        if state.status.phase == Phase::Running {
            state.status.ticket.episode = state.status.ticket.episode.checked_add(1)?;
            state.status.root_error = None;
        }
        // A fault during partial recovery invalidates earlier evidence too. A
        // successful quote before a later transport failure is not readiness.
        state.proven = 0;
        state.status.missing = PROOFS
            .into_iter()
            .filter(|p| state.required.0 & *p as u8 != 0)
            .collect();
        state.status.phase = Phase::Recovering;
        state.status.deadline = Some(
            state
                .status
                .deadline
                .map_or(deadline, |d| d.earliest(deadline)),
        );
        state
            .status
            .root_error
            .get_or_insert_with(|| bounded(detail));
        state.status.latest_error = Some(bounded(detail));
        Self::audit_start(&mut state);
        self.expire(&mut state);
        self.changed.send_replace(state.status.clone());
        Some(state.status.ticket)
    }
    /// First connection silence during startup begins a full recovery incident
    /// at detection, rather than consuming time already spent initializing.
    /// Repeated failures retain the first incident deadline.
    pub fn provider_fault(&self, ticket: Ticket, detail: &str) -> Option<Ticket> {
        {
            let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
            if state.status.ticket == ticket
                && state.status.phase == Phase::Starting
                && state.status.root_error.is_none()
            {
                state.status.deadline = None;
            }
        }
        self.fault(ticket, detail, Deadline::recovery())
    }
    /// Registers replacement resources or changed intent without extending an
    /// active episode. Every proof is invalidated, including retirement proof.
    /// Explicit retry after terminal failure is the only way to reopen a window.
    pub fn replace(&self, required: Requirements, deadline: Deadline) -> Option<Ticket> {
        self.replace_inner(required, deadline, true)
    }
    /// Replaces resources for automatic maintenance without reopening a terminal
    /// episode. Expiry and this decision share the same lock, including the exact
    /// deadline boundary; only an explicit command may grant another allowance.
    pub fn replace_passive(&self, required: Requirements, deadline: Deadline) -> Option<Ticket> {
        self.replace_inner(required, deadline, false)
    }
    fn replace_inner(
        &self,
        required: Requirements,
        deadline: Deadline,
        explicit: bool,
    ) -> Option<Ticket> {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        self.expire(&mut state);
        if state.status.phase == Phase::Stopped
            || (!explicit && state.status.phase == Phase::Failed)
        {
            return None;
        }
        // Automatic lifetime maintenance is not evidence that a fault recovered.
        // If its final view expires while repair is incomplete, retain every
        // outstanding proof until success or the original terminal deadline.
        let required = if !explicit
            && state.status.phase == Phase::Recovering
            && state.status.root_error.is_some()
        {
            Requirements(required.0 | state.required.0)
        } else {
            required
        };
        let fresh = matches!(state.status.phase, Phase::Running | Phase::Failed);
        state.status.ticket.revision = state.status.ticket.revision.checked_add(1)?;
        state.status.ticket.generation = state.status.ticket.generation.checked_add(1)?;
        if fresh {
            state.status.ticket.episode = state.status.ticket.episode.checked_add(1)?;
            state.status.root_error = None;
            state.status.latest_error = None;
        }
        state.status.deadline = Some(if fresh {
            deadline
        } else {
            state.status.deadline.unwrap_or(deadline).earliest(deadline)
        });
        // Capturing initial intent is setup, not evidence of a failed provider.
        state.status.phase =
            if state.status.phase == Phase::Starting && state.status.root_error.is_none() {
                Phase::Starting
            } else {
                Phase::Recovering
            };
        state.required = required;
        state.proven = 0;
        state.status.missing = PROOFS
            .into_iter()
            .filter(|p| required.0 & *p as u8 != 0)
            .collect();
        self.expire(&mut state);
        self.changed.send_replace(state.status.clone());
        Some(state.status.ticket)
    }
    /// Stops control independently of consumer capacity. Resource retirement is
    /// performed by the resource supervisor, never inferred from this status.
    pub fn stop(&self) {
        let mut state = self.state.lock().unwrap_or_else(|e| e.into_inner());
        let status = state.status.clone();
        if let Some(incident) = state.incident.as_mut() {
            incident.finish("Interrupted", &status);
        }
        state.status.phase = Phase::Stopped;
        state.reset_available = false;
        state.status.deadline = None;
        self.changed.send_replace(state.status.clone());
    }
    /// Owns an event-driven monotonic watchdog outside replaceable dataset
    /// resources. A silent provider/consumer cannot suppress deadline expiry.
    pub fn watchdog(self: &Arc<Self>, owner: &Arc<Owner>) -> Result<JoinHandle<()>, OwnerError> {
        let this = Arc::clone(self);
        owner.spawn_service(move|stop|async move {
            let mut changed = this.subscribe();
            loop {
                let status = this.status();
                if status.phase == Phase::Stopped { break; }
                let remaining = status.deadline.filter(|_|matches!(status.phase, Phase::Starting | Phase::Recovering)).map(|d|d.remaining());
                tokio::select! {
                    biased;
                    () = stop.cancelled() => { this.stop(); break; }
                    result = changed.changed() => { if result.is_err() { break; } }
                    () = async { match remaining { Some(wait)=>tokio::time::sleep(wait).await, None=>std::future::pending().await } } => { this.status(); }
                }
            }
        })
    }
}
