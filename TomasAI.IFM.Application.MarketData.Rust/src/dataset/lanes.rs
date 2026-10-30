//! Three isolated logical lane owners and bounded, explicit recovery episodes.
//!
//! Provider receipt/processing happens on the owning task, without an extra raw
//! record mailbox. The native processor must do bounded synchronous work, then
//! yield back here. Transport readiness is not end-to-end trade-plan readiness.

use super::{
    databento::NamedGateway,
    transport::{Event, Failure, Lane, Session, SessionPlan},
};
use crate::{
    dns::Resolver,
    runtime::{Deadline, RECOVERY_BUDGET},
};
use futures_util::FutureExt;
use std::{
    collections::{BTreeMap, BTreeSet},
    panic::AssertUnwindSafe,
    sync::{
        Arc,
        atomic::{AtomicU64, Ordering},
    },
    time::Duration,
};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;

/// One small state machine per lane; failure always remains observable.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum Phase {
    /// No provider resources remain owned by this task.
    Stopped,
    /// First session setup is consuming the caller's original budget.
    Starting,
    /// Native transport acknowledgement/mapping evidence is complete.
    Running,
    /// Retiring/replacing a failed session within one original episode budget.
    Recovering,
    /// The original episode expired or an internal failure was contained.
    Failed,
}

/// Latest reliable control observation, independent of any quote-output capacity.
#[derive(Debug, Clone)]
pub struct Status {
    /// Dataset/role identity.
    pub lane: Lane,
    /// Current lifecycle state.
    pub phase: Phase,
    /// New session attempt identity; never reused within this controller lifetime.
    pub generation: u64,
    /// Incident identity; retries never create a new episode/deadline.
    pub episode: u64,
    /// Attempts in the current incident, including failed connection/setup.
    pub attempts: u64,
    /// Sanitized last failure detail, retained even if the subscriber reconnects.
    pub error: Option<String>,
    /// First failure that started this incident. Later timeouts cannot erase it.
    pub root_error: Option<String>,
    /// Original native setup/recovery deadline, retained on terminal failure.
    /// Consumers must not grant another 120 seconds after this one expires.
    pub deadline: Option<Deadline>,
}

/// Explicit async resolver and provider gateway endpoint, not a worker process.
#[derive(Clone)]
pub struct Gateway {
    /// Provider DNS hostname or literal IP for controlled qualification.
    pub host: String,
    /// Provider TCP port.
    pub port: u16,
    /// One shared bounded resolver owned by this backend.
    pub resolver: Arc<Resolver>,
}

#[derive(Clone)]
struct ResetRequest {
    serial: u64,
    deadline: Option<Deadline>,
    reason: Option<Arc<str>>,
}
struct OwnedLane {
    stop: CancellationToken,
    reset: watch::Sender<ResetRequest>,
    consumed: Arc<AtomicU64>,
    status: watch::Receiver<Status>,
    task: JoinHandle<()>,
}
#[derive(Clone)]
struct StatusWriter {
    sender: watch::Sender<Status>,
    owner: Option<Arc<crate::runtime::owner::Owner>>,
    lane: Lane,
}
struct LaneExit(StatusWriter);
impl Drop for LaneExit {
    fn drop(&mut self) {
        // A nested parent may cancel/drop the entire lane future while it is
        // waiting on provider I/O. Publish retirement even when the normal loop
        // does not get another poll; preserve an already reported terminal error.
        self.0.send_if_modified(|status| {
            if matches!(status.phase, Phase::Stopped | Phase::Failed) {
                return false;
            }
            status.phase = Phase::Stopped;
            true
        });
    }
}
impl StatusWriter {
    fn send_modify(&self, change: impl FnOnce(&mut Status)) {
        self.sender.send_modify(change);
        self.signal();
    }
    fn send_if_modified(&self, change: impl FnOnce(&mut Status) -> bool) {
        if self.sender.send_if_modified(change) {
            self.signal();
        }
    }
    fn borrow(&self) -> watch::Ref<'_, Status> {
        self.sender.borrow()
    }
    fn signal(&self) {
        if let Some(owner) = &self.owner {
            let _ = owner.signal(1 << (self.lane as u32));
        }
    }
}

struct LaneConfig {
    plan: SessionPlan,
    key: String,
    gateway: Gateway,
    deadline: Deadline,
    provider: Option<watch::Sender<crate::runtime::owner::ProviderState>>,
}
enum Ownership<'a> {
    Untracked,
    Root(crate::runtime::owner::Child),
    Nested(&'a crate::runtime::owner::Child),
}

/// Owns at most the three supported lane tasks. A lane cannot be replaced by a
/// second task while the old task is still owned; reset happens within its task.
#[derive(Default)]
pub struct Lanes {
    owned: BTreeMap<Lane, OwnedLane>,
    retired: BTreeMap<Lane, (u64, u64)>,
}

impl Lanes {
    /// Starts a lane with the production two-minute incident budget. The processor
    /// runs before replacement/coalescing and must retain authoritative aggregate
    /// contributions. Its bool reports workload readiness, not just successful
    /// receipt. Err or panic makes the lane unhealthy. An idle/quiet-session
    /// policy may explicitly prove control-only readiness; never fabricate quotes.
    pub fn start<P>(
        &mut self,
        plan: SessionPlan,
        key: String,
        gateway: Gateway,
        processor: P,
    ) -> Result<watch::Receiver<Status>, Failure>
    where
        P: FnMut(u64, &Event) -> Result<bool, String> + Send + 'static,
    {
        self.start_with_budget(plan, key, gateway, processor, RECOVERY_BUDGET)
    }

    /// Same owner with an explicit initial-setup budget, capped at 120 seconds.
    /// This enables accelerated setup-failure qualification. Once Running, each
    /// independently detected incident gets the production two-minute budget;
    /// an initially short caller budget must not become a permanent short timeout.
    pub fn start_with_budget<P>(
        &mut self,
        plan: SessionPlan,
        key: String,
        gateway: Gateway,
        processor: P,
        budget: Duration,
    ) -> Result<watch::Receiver<Status>, Failure>
    where
        P: FnMut(u64, &Event) -> Result<bool, String> + Send + 'static,
    {
        self.start_with_deadline(plan, key, gateway, processor, Deadline::after(budget))
    }

    /// Starts using the caller's existing absolute deadline, including time already
    /// consumed before lane admission. No scheduling delay restarts the budget.
    pub fn start_with_deadline<P>(
        &mut self,
        plan: SessionPlan,
        key: String,
        gateway: Gateway,
        processor: P,
        deadline: Deadline,
    ) -> Result<watch::Receiver<Status>, Failure>
    where
        P: FnMut(u64, &Event) -> Result<bool, String> + Send + 'static,
    {
        self.start_owned(
            plan,
            key,
            gateway,
            processor,
            deadline,
            Ownership::Untracked,
        )
    }
    /// Links this provider lifetime to the shared native runtime's shutdown and
    /// task tracker. Root completion cannot overtake the dataset resource drop.
    pub fn start_tracked<P>(
        &mut self,
        plan: SessionPlan,
        key: String,
        gateway: Gateway,
        processor: P,
        deadline: Deadline,
        owner: &Arc<crate::runtime::owner::Owner>,
    ) -> Result<watch::Receiver<Status>, Failure>
    where
        P: FnMut(u64, &Event) -> Result<bool, String> + Send + 'static,
    {
        let child = owner
            .child()
            .map_err(|e| Failure::Provider(format!("Runtime dataset admission failed: {e:?}")))?;
        self.start_owned(
            plan,
            key,
            gateway,
            processor,
            deadline,
            Ownership::Root(child),
        )
    }
    /// Owns an independent physical connection below an existing logical
    /// dataset. Options chain/exact-contract sessions can therefore be reset
    /// separately without consuming a fourth logical runtime lane. The parent
    /// retains cancellation/drop tracking even if this controller is abandoned.
    pub fn start_nested<P>(
        &mut self,
        plan: SessionPlan,
        key: String,
        gateway: Gateway,
        processor: P,
        deadline: Deadline,
        parent: &crate::runtime::owner::Child,
    ) -> Result<watch::Receiver<Status>, Failure>
    where
        P: FnMut(u64, &Event) -> Result<bool, String> + Send + 'static,
    {
        self.start_owned(
            plan,
            key,
            gateway,
            processor,
            deadline,
            Ownership::Nested(parent),
        )
    }
    fn start_owned<P>(
        &mut self,
        plan: SessionPlan,
        key: String,
        gateway: Gateway,
        processor: P,
        deadline: Deadline,
        ownership: Ownership<'_>,
    ) -> Result<watch::Receiver<Status>, Failure>
    where
        P: FnMut(u64, &Event) -> Result<bool, String> + Send + 'static,
    {
        plan.validate()?;
        let deadline = deadline.earliest(Deadline::recovery());
        if key.len() != 32 || !key.is_ascii() {
            return Err(Failure::Input("Invalid provider credential format"));
        }
        if self.owned.contains_key(&plan.lane) {
            return Err(Failure::Input(
                "Lane already has an owned task; reset or await shutdown",
            ));
        }
        let lane = plan.lane;
        let (ownership, parent) = match ownership {
            Ownership::Untracked => (None, None),
            Ownership::Root(child) => (Some(child), None),
            Ownership::Nested(parent) => (None, Some(parent)),
        };
        let stop = if let Some(parent) = parent {
            parent.cancellation().child_token()
        } else {
            ownership
                .as_ref()
                .map_or_else(CancellationToken::new, |child| child.cancellation())
        };
        let (reset, reset_rx) = watch::channel(ResetRequest {
            serial: 0,
            deadline: None,
            reason: None,
        });
        let consumed = Arc::new(AtomicU64::new(0));
        let task_consumed = consumed.clone();
        let (status_tx, status) = watch::channel(Status {
            lane,
            phase: Phase::Starting,
            generation: self.retired.get(&lane).map_or(0, |value| value.0),
            episode: self
                .retired
                .get(&lane)
                .map_or(1, |value| value.1.saturating_add(1)),
            attempts: 0,
            error: None,
            root_error: None,
            deadline: Some(deadline),
        });
        let task_stop = stop.clone();
        let status_tx = StatusWriter {
            sender: status_tx,
            owner: ownership.as_ref().or(parent).map(|child| child.owner()),
            lane,
        };
        let exit = LaneExit(status_tx.clone());
        let provider = parent
            .or(ownership.as_ref())
            .map(crate::runtime::owner::Child::provider_reporter);
        let work = async move {
            let _ownership = ownership;
            let _exit = exit;
            let run = run_lane(
                LaneConfig {
                    plan,
                    key,
                    gateway,
                    deadline,
                    provider,
                },
                processor,
                &task_stop,
                reset_rx,
                &task_consumed,
                &status_tx,
            );
            if AssertUnwindSafe(run).catch_unwind().await.is_err() {
                status_tx.send_modify(|state| {
                    state.phase = Phase::Failed;
                    state.error =
                        Some("Native lane processor panicked; owned session unwound".into());
                });
            }
        };
        let task = if let Some(parent) = parent {
            parent.spawn(work).map_err(|error| {
                Failure::Provider(format!(
                    "Dataset physical-session admission failed: {error:?}"
                ))
            })?
        } else {
            tokio::spawn(work)
        };
        self.owned.insert(
            lane,
            OwnedLane {
                stop,
                reset,
                consumed,
                status: status.clone(),
                task,
            },
        );
        Ok(status)
    }

    /// Requests session replacement; repeated requests while recovering do not
    /// extend the current episode. This watch channel is independent of data load.
    pub fn reset(&self, lane: Lane) -> Result<(), Failure> {
        self.reset_with_deadline(lane, Deadline::recovery())
    }

    /// Carries an existing recovery deadline. Pending resets are merged to their
    /// earliest instant; repeated requests can never extend an unconsumed reset.
    pub fn reset_with_deadline(&self, lane: Lane, deadline: Deadline) -> Result<(), Failure> {
        self.request_reset(lane, deadline, None)
    }

    /// Routes an asynchronous processing fault into the same dataset recovery
    /// owner as provider errors. It wakes stalled socket/setup work immediately;
    /// repeated faults merge the earliest deadline rather than renewing it.
    /// Independent pricing tasks must report unexpected failures here.
    pub fn report_fault(
        &self,
        lane: Lane,
        reason: &str,
        deadline: Deadline,
    ) -> Result<(), Failure> {
        if reason.trim().is_empty() || reason.len() > 4096 {
            return Err(Failure::Input("Invalid processing fault detail"));
        }
        self.request_reset(lane, deadline, Some(Arc::from(reason)))
    }

    fn request_reset(
        &self,
        lane: Lane,
        deadline: Deadline,
        reason: Option<Arc<str>>,
    ) -> Result<(), Failure> {
        let deadline = deadline.earliest(Deadline::recovery());
        let owned = self
            .owned
            .get(&lane)
            .ok_or(Failure::Input("Lane is not owned"))?;
        if owned.task.is_finished() {
            return Err(Failure::Input(
                "Lane ended; await shutdown before starting again",
            ));
        }
        owned.reset.send_modify(|request| {
            let pending = request.serial > owned.consumed.load(Ordering::Acquire);
            if !pending || request.reason.is_none() {
                request.reason = reason;
            }
            request.deadline = Some(if pending {
                request
                    .deadline
                    .map_or(deadline, |old| old.earliest(deadline))
            } else {
                deadline
            });
            request.serial = request.serial.saturating_add(1);
        });
        Ok(())
    }

    /// Reads reliable control state without starting subscriptions or polling data.
    pub fn status(&self, lane: Lane) -> Option<Status> {
        self.owned
            .get(&lane)
            .map(|owned| owned.status.borrow().clone())
    }
    /// Clones the lane's independent emergency cancellation signal. This permits
    /// stop admission without waiting for an asynchronous lifecycle serialization.
    pub fn cancellation(&self, lane: Lane) -> Option<CancellationToken> {
        self.owned.get(&lane).map(|lane| lane.stop.clone())
    }

    /// Cancels one lane and awaits actual task exit using the existing caller
    /// deadline. Timeout retains the task identity and never claims cleanup.
    /// Futures/options lanes not selected here keep running.
    pub async fn stop(&mut self, lane: Lane, deadline: Deadline) -> Result<(), Failure> {
        let deadline = deadline.earliest(Deadline::recovery());
        let Some(owned) = self.owned.get_mut(&lane) else {
            return Ok(());
        };
        owned.stop.cancel();
        let result = deadline
            .run(&CancellationToken::new(), &mut owned.task)
            .await
            .map_err(Failure::Interrupted)?;
        result.map_err(|_| Failure::Provider("Native lane task failed".into()))?;
        {
            let status = owned.status.borrow();
            self.retired
                .insert(lane, (status.generation, status.episode));
        }
        self.owned.remove(&lane);
        Ok(())
    }

    /// Cancels all lanes before joining any, sharing one original shutdown budget.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), Failure> {
        let deadline = deadline.earliest(Deadline::recovery());
        for lane in self.owned.values() {
            lane.stop.cancel();
        }
        let lanes: Vec<_> = self.owned.keys().copied().collect();
        for lane in lanes {
            self.stop(lane, deadline).await?;
        }
        Ok(())
    }
}

impl Drop for Lanes {
    fn drop(&mut self) {
        // Emergency signal only: no synchronous join, false cleanup claim or new
        // owner is installed. Production composition must await explicit shutdown.
        for lane in self.owned.values() {
            lane.stop.cancel();
        }
    }
}

async fn run_lane<P>(
    config: LaneConfig,
    mut processor: P,
    stop: &CancellationToken,
    mut reset: watch::Receiver<ResetRequest>,
    consumed: &AtomicU64,
    status: &StatusWriter,
) where
    P: FnMut(u64, &Event) -> Result<bool, String>,
{
    let LaneConfig {
        plan,
        key,
        gateway,
        deadline: initial_deadline,
        provider,
    } = config;
    let mut deadline = Some(initial_deadline);
    loop {
        if stop.is_cancelled() {
            status.send_modify(|s| s.phase = Phase::Stopped);
            return;
        }
        if let Some(request) = consume_reset(&reset, consumed, status, &key) {
            deadline = Some(deadline.map_or(request, |old| old.earliest(request)));
        }
        let Some(attempt_deadline) = deadline else {
            return;
        };
        if attempt_deadline.remaining().is_zero() {
            fail(status);
            return;
        }
        status.send_modify(|s| {
            s.generation = s.generation.saturating_add(1);
            s.attempts = s.attempts.saturating_add(1);
        });
        let generation = status.borrow().generation;
        // Dropping setup through either branch abandons its entire partial SDK
        // client. A reset request cannot wait behind an auth or DNS stall.
        let setup = Session::open(
            &plan,
            key.clone(),
            NamedGateway {
                host: &gateway.host,
                port: gateway.port,
                resolver: &gateway.resolver,
            },
            stop,
            attempt_deadline,
        );
        let opened = tokio::select! { biased;
            () = stop.cancelled() => { status.send_modify(|s| s.phase = Phase::Stopped); return; }
            _ = reset.changed() => { continue; }
            result = setup => result,
        };
        let mut session = match opened {
            Ok(session) => session,
            Err(error) => {
                recover_status(status, format!("{error:?}"), &key, Some(attempt_deadline));
                if let Some(request) = consume_reset(&reset, consumed, status, &key) {
                    deadline = Some(attempt_deadline.earliest(request));
                }
                if !retry(stop, &mut reset, deadline.unwrap_or(attempt_deadline)).await {
                    if stop.is_cancelled() {
                        status.send_modify(|s| s.phase = Phase::Stopped);
                    } else {
                        fail(status);
                    }
                    return;
                }
                continue;
            }
        };
        let mut mappings: BTreeMap<String, u32> = plan
            .subscriptions
            .iter()
            .flat_map(|s| {
                s.symbols.iter().map(move |symbol| {
                    (
                        symbol.clone(),
                        if s.symbology == databento::dbn::SType::InstrumentId {
                            symbol.parse().unwrap_or(0)
                        } else {
                            0
                        },
                    )
                })
            })
            .collect();
        let mut acknowledgements = 0u64;
        let expected_acknowledgements = if plan.subscriptions.len() == 64 {
            u64::MAX
        } else {
            (1u64 << plan.subscriptions.len()) - 1
        };
        let mut replay: BTreeSet<u16> = plan
            .subscriptions
            .iter()
            .filter(|s| s.replay_start_ns.is_some())
            .map(|s| s.schema as u16)
            .collect();
        let mut workload_ready = false;
        let immediate_reset = loop {
            let event = session.next(
                stop,
                replay.contains(&(databento::dbn::Schema::Statistics as u16)),
                replay.contains(&(databento::dbn::Schema::Trades as u16)),
            );
            let read = async {
                if let Some(deadline) = deadline {
                    deadline
                        .run(stop, event)
                        .await
                        .map_err(Failure::Interrupted)?
                } else {
                    event.await
                }
            };
            let received = tokio::select! { biased;
                () = stop.cancelled() => { drop(session); status.send_modify(|s| s.phase = Phase::Stopped); return; }
                _ = reset.changed() => Err(Failure::Provider(reset.borrow().reason.as_ref().map_or_else(
                    ||"Explicit session reset requested".into(),|reason|format!("Native asynchronous processing failed: {reason}")))),
                result = read => result,
            };
            let error = match received {
                Ok(Some(event)) => {
                    // Do authoritative processing before replacing latest views.
                    let processed = std::panic::catch_unwind(AssertUnwindSafe(|| {
                        processor(generation, &event)
                    }))
                    .unwrap_or_else(|_| {
                        Err("Native processor panicked; session must be replaced".into())
                    });
                    match processed {
                        Err(error) => Some(format!("Native processing failed: {error}")),
                        Ok(ready) => {
                            workload_ready = ready;
                            if !matches!(&event, Event::Error { .. })
                                && let Some(owner) = &provider
                            {
                                owner.send_if_modified(|state| {
                                    if matches!(state, Some(Ok(()))) {
                                        false
                                    } else {
                                        *state = Some(Ok(()));
                                        true
                                    }
                                });
                            }
                            match &event {
                                Event::Mapping {
                                    requested,
                                    instrument,
                                    ..
                                } => {
                                    if let Some(value) = mappings.get_mut(requested) {
                                        *value = *instrument;
                                    }
                                    None
                                }
                                Event::System { code: 1, message } => {
                                    match acknowledged_subscription(message, &plan) {
                                        Ok(index) => {
                                            acknowledgements |= 1u64 << index;
                                            None
                                        }
                                        Err(reason) => Some(reason.into()),
                                    }
                                }
                                Event::System { code: 3, message } => {
                                    if message.contains("trades") {
                                        replay.remove(&(databento::dbn::Schema::Trades as u16));
                                    } else if message.contains("statistics") {
                                        replay.remove(&(databento::dbn::Schema::Statistics as u16));
                                    } else if message.contains("mbp-1") {
                                        replay.remove(&(databento::dbn::Schema::Mbp1 as u16));
                                    }
                                    None
                                }
                                Event::Error {
                                    code,
                                    last,
                                    message,
                                } => Some(format!(
                                    "Provider ErrorMsg code={code} is_last={last}: {message}"
                                )),
                                _ => None,
                            }
                        }
                    }
                }
                Ok(None) => Some("Provider closed the live session".into()),
                Err(error) => Some(format!("{error:?}")),
            };
            if let Some(error) = error {
                if let Some(owner) = &provider {
                    owner.send_replace(Some(Err(error.chars().take(4096).collect())));
                }
                // Cleanup is immediate, without draining stale observation work.
                drop(session);
                let requested = consume_reset(&reset, consumed, status, &key);
                if deadline.is_none() {
                    deadline = Some(requested.unwrap_or_else(Deadline::recovery));
                    status.send_modify(|s| {
                        s.episode = s.episode.saturating_add(1);
                        s.attempts = 0;
                    });
                }
                if let Some(request) = requested {
                    deadline = Some(deadline.map_or(request, |old| old.earliest(request)));
                }
                recover_status(status, error, &key, deadline);
                break requested.is_some();
            }
            if acknowledgements == expected_acknowledgements
                && workload_ready
                && mappings.values().all(|id| *id != 0)
                && replay.is_empty()
            {
                deadline = None;
                status.send_if_modified(|s| {
                    if s.phase == Phase::Running {
                        false
                    } else {
                        s.phase = Phase::Running;
                        s.error = None;
                        s.root_error = None;
                        s.deadline = None;
                        true
                    }
                });
            }
            tokio::task::yield_now().await;
        };
        if let Some(current) = deadline
            && !immediate_reset
            && !retry(stop, &mut reset, current).await
        {
            if stop.is_cancelled() {
                status.send_modify(|s| s.phase = Phase::Stopped);
            } else {
                fail(status);
            }
            return;
        }
    }
}

fn recover_status(status: &StatusWriter, error: String, key: &str, deadline: Option<Deadline>) {
    let error = error.replace(key, "[REDACTED]");
    status.send_modify(|s| {
        if s.root_error.is_none() {
            s.root_error = Some(error.clone());
        }
        s.phase = Phase::Recovering;
        s.deadline = match (s.deadline, deadline) {
            (Some(a), Some(b)) => Some(a.earliest(b)),
            (a, b) => a.or(b),
        };
        s.error = Some(error);
    });
}

fn fail(status: &StatusWriter) {
    status.send_modify(|s| {
        s.phase = Phase::Failed;
        s.error = Some(format!(
            "Market-data recovery deadline expired. Root cause: {}. Last failure: {}",
            s.root_error
                .as_deref()
                .unwrap_or("No complete provider readiness evidence"),
            s.error
                .as_deref()
                .unwrap_or("No complete provider readiness evidence")
        ));
    });
}

async fn retry(
    stop: &CancellationToken,
    reset: &mut watch::Receiver<ResetRequest>,
    deadline: Deadline,
) -> bool {
    // Retry pacing is not a step timeout and cannot extend the original budget.
    deadline.run(stop, async { tokio::select! { () = tokio::time::sleep(Duration::from_millis(200)) => {}, _ = reset.changed() => {} } }).await.is_ok()
}

fn consume_reset(
    reset: &watch::Receiver<ResetRequest>,
    consumed: &AtomicU64,
    status: &StatusWriter,
    key: &str,
) -> Option<Deadline> {
    // The read guard excludes send_modify while marking its serial consumed.
    // A concurrent caller consequently merges only genuinely pending requests.
    let request = reset.borrow();
    if request.serial <= consumed.load(Ordering::Acquire) {
        None
    } else {
        consumed.store(request.serial, Ordering::Release);
        if let Some(reason) = &request.reason {
            recover_status(
                status,
                format!("Native asynchronous processing failed: {reason}"),
                key,
                request.deadline,
            );
        }
        if let Some(deadline) = request.deadline {
            status.send_modify(|s| {
                s.deadline = Some(
                    s.deadline
                        .map_or(deadline, |prior| prior.earliest(deadline)),
                )
            });
        }
        request.deadline
    }
}

// The pinned SDK assigns IDs 1..N and retains one ID across its 500-symbol
// chunks (is_last marks completion). Provider acknowledgments qualify logical
// subscriptions, not protocol line count. Duplicate IDs must not prove readiness.
fn acknowledged_subscription(message: &str, plan: &SessionPlan) -> Result<usize, &'static str> {
    let (id, schema) = if let Some(id) = message.strip_prefix("subscription_id=") {
        (id, None)
    } else {
        let body = message
            .strip_prefix("Subscription request ")
            .and_then(|text| text.strip_suffix(" succeeded"))
            .ok_or("Unrecognized provider subscription acknowledgement")?;
        if let Some((id, schema)) = body.split_once(" for ") {
            (
                id,
                Some(
                    schema
                        .strip_suffix(" data")
                        .ok_or("Malformed provider acknowledgement schema")?,
                ),
            )
        } else {
            (body, None)
        }
    };
    if id.is_empty() || !id.bytes().all(|b| b.is_ascii_digit()) {
        return Err("Malformed provider subscription acknowledgement identity");
    }
    let index = id
        .parse::<usize>()
        .ok()
        .and_then(|id| id.checked_sub(1))
        .filter(|index| *index < plan.subscriptions.len())
        .ok_or("Provider acknowledged an unrequested subscription")?;
    if schema.is_some_and(|schema| schema != plan.subscriptions[index].schema.as_str()) {
        return Err("Provider acknowledgement schema does not match its subscription");
    }
    Ok(index)
}
#[cfg(test)]
mod acknowledgement_tests {
    use super::*;
    use databento::dbn::{SType, Schema};
    #[test]
    fn ids_and_schema_are_checked_without_counting_chunks_as_subscriptions() {
        let plan = SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions: [Schema::Trades, Schema::Mbp1, Schema::Statistics]
                .into_iter()
                .map(|schema| super::super::transport::SubscriptionSpec {
                    symbols: vec!["43".into(); 2048],
                    schema,
                    symbology: SType::InstrumentId,
                    replay_start_ns: None,
                })
                .collect(),
        };
        for (text, expected) in [
            ("Subscription request 1 for trades data succeeded", 0),
            ("Subscription request 2 for mbp-1 data succeeded", 1),
            ("Subscription request 3 for statistics data succeeded", 2),
            ("Subscription request 2 succeeded", 1),
            ("subscription_id=1", 0),
        ] {
            assert_eq!(acknowledged_subscription(text, &plan), Ok(expected));
        }
        for text in [
            "Subscription request 0 succeeded",
            "Subscription request 4 succeeded",
            "Subscription request -1 succeeded",
            "Subscription request 1 for mbp-1 data succeeded",
            "subscription_id=18446744073709551616",
            "subscription_id=1|id=2",
            "Subscription accepted",
        ] {
            assert!(acknowledged_subscription(text, &plan).is_err(), "{text}");
        }
    }
}
