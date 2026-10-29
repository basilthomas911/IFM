//! Event-driven native futures-to-NATS delivery beneath a dataset task owner.
//! Provider output, event construction and broker writes never enter the FFI.
//! Broker retry uses one incident deadline; source reset receives that same
//! deadline and never waits for a managed receiver or database acknowledgement.

use super::{
    codec,
    current::Current,
    futures::Futures,
    publisher::{BrokerSettings, Publisher},
    wire::Frame,
};
use crate::{
    dataset::{live::LiveData, transport::Lane},
    runtime::{Deadline, owner::Child},
};
use std::{
    sync::{Arc, Mutex},
    time::Duration,
};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;

/// Native publication availability, distinct from provider source readiness.
#[derive(Clone, Debug, Default)]
pub struct Status {
    /// A current native frame has completed a broker protocol flush.
    pub running: bool,
    /// One original setup/recovery deadline is active.
    pub recovering: bool,
    /// Original deadline expired; query remains available while NATS is down.
    pub terminal: bool,
    /// Earliest original setup/recovery deadline, cleared only on full readiness.
    pub deadline: Option<Deadline>,
    /// First publication failure in the current episode, without credentials.
    pub root_error: Option<String>,
    /// Latest sanitized failure detail.
    pub error: Option<String>,
}
/// Complete imported broker/envelope identity. Contains no managed callbacks.
pub struct Configuration {
    /// Shared validated broker settings, including explicit DNS/TLS policy.
    pub broker: BrokerSettings,
    /// Existing deployment subject scope.
    pub scope: String,
    /// Exact logical dataset role.
    pub lane: Lane,
    /// API ValueDate as CLR day number.
    pub value_date: i32,
    /// Logical owner Guid bytes in CLR order.
    pub worker: [u8; 16],
    /// Logical generation Guid bytes in CLR order.
    pub generation: [u8; 16],
    /// Positive desired-subscription revision.
    pub revision: i64,
    /// Explicit API-session quiet/closed eligibility. Broker protocol readiness
    /// may suffice without inventing a fresh trade or market-price observation.
    pub control_only: bool,
    /// Bounded queued/in-flight frames.
    pub queue: usize,
    /// Bounded queued/in-flight encoded bytes.
    pub bytes: usize,
}
/// Owned native publication task and detached latest-value query storage.
pub struct Delivery {
    stop: CancellationToken,
    task: Option<JoinHandle<()>>,
    status: watch::Receiver<Status>,
    current: Arc<Current>,
    retirement: Arc<Mutex<Option<Deadline>>>,
}
struct Exit {
    status: watch::Sender<Status>,
    clean: bool,
    data: Arc<LiveData>,
    lane: Lane,
}
impl Drop for Exit {
    fn drop(&mut self) {
        if !self.clean {
            let _ = self.data.cancel_lane(self.lane);
        }
        self.status.send_modify(|status| {
            status.running = false;
            if !self.clean {
                status.terminal = true;
                status.recovering = false;
                status.error = Some("Native publication owner exited before joined cleanup".into());
            }
        });
    }
}
impl Delivery {
    /// Starts beneath the same native dataset parent as the provider resources.
    /// There is exactly one exclusive output consumer for this lane.
    ///
    /// # Errors
    /// Rejects invalid identity/capacity or stopped/exhausted native admission.
    pub fn start(
        data: Arc<LiveData>,
        mut events: Futures,
        config: Configuration,
        parent: Arc<Child>,
        deadline: Deadline,
    ) -> Result<Self, &'static str> {
        if config.lane == Lane::OptionsGlbx
            || !data.belongs_to(&parent)
            || !(1..=4096).contains(&config.queue)
            || !(1..=64 * 1024 * 1024).contains(&config.bytes)
        {
            return Err("Futures delivery requires a futures lane");
        }
        Frame {
            lane: config.lane,
            value_date: config.value_date,
            worker: config.worker,
            generation: config.generation,
            manifest_revision: config.revision,
            sequence: 1,
            kind: 3,
            payload: vec![0xc0],
        }
        .subject(&config.scope)?;
        let current = Arc::new(Current::new(128, 16 * 1024 * 1024)?);
        let retained = current.clone();
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let retirement = Arc::new(Mutex::new(None));
        let retiring = retirement.clone();
        let (status, observed) = watch::channel(Status {
            recovering: true,
            deadline: Some(deadline),
            ..Status::default()
        });
        let owned = parent.clone();
        let exit = Exit {
            status: status.clone(),
            clean: false,
            data: data.clone(),
            lane: config.lane,
        };
        let task = parent
            .spawn_joined(move |_| async move {
                let mut exit = exit;
                let mut episode = Some(deadline);
                let mut fault_reported = false;
                let mut sequence = 0i64;
                loop {
                    if stopping.is_cancelled() {
                        break;
                    }
                    let budget = episode.unwrap_or_else(Deadline::recovery);
                    let publisher = Publisher::start_owned(
                        config.broker.clone(),
                        config.scope.clone(),
                        config.queue,
                        config.bytes,
                        budget,
                        &owned,
                    );
                    let mut publisher = match publisher {
                        Ok(p) => p,
                        Err(error) => {
                            let _ = data.cancel_lane(config.lane);
                            status.send_modify(|s| {
                                s.error = Some(error.into());
                                s.terminal = true;
                                s.running = false;
                                s.recovering = false;
                            });
                            break;
                        }
                    };
                    let failure = Self::pump(
                        &data,
                        &mut events,
                        &config,
                        &mut sequence,
                        &retained,
                        &mut publisher,
                        &stopping,
                        &mut episode,
                        &status,
                    )
                    .await;
                    if stopping.is_cancelled() {
                        let cleanup = retiring
                            .lock()
                            .ok()
                            .and_then(|d| *d)
                            .unwrap_or_else(Deadline::recovery);
                        let _ = publisher.shutdown(cleanup).await;
                        break;
                    }
                    let error =
                        failure.unwrap_or_else(|| "Native futures publication stopped".into());
                    if episode.is_none() {
                        fault_reported = false;
                    }
                    let budget = *episode.get_or_insert_with(Deadline::recovery);
                    status.send_modify(|s| {
                        s.running = false;
                        s.recovering = true;
                        s.deadline = Some(budget);
                        s.root_error.get_or_insert(error.clone());
                        s.error = Some(error.clone());
                    });
                    // Join the old broker workers independently of any server ACK.
                    // No new publisher starts while old transport ownership remains.
                    let joined = publisher.shutdown(budget).await;
                    if !fault_reported && !budget.remaining().is_zero() {
                        fault_reported = true;
                        if let Err(fault) = data
                            .report_fault(config.lane, &error, &stopping, budget)
                            .await
                        {
                            status.send_modify(|s| s.error = Some(fault));
                        }
                    }
                    events.retire();
                    if joined.is_err() || budget.remaining().is_zero() {
                        let _ = data.cancel_lane(config.lane);
                        status.send_modify(|s| {
                            s.terminal = true;
                            s.recovering = false;
                            s.error = Some(
                                "Native publication original two-minute deadline expired".into(),
                            );
                        });
                        break;
                    }
                    if budget
                        .run(&stopping, tokio::time::sleep(Duration::from_millis(200)))
                        .await
                        .is_err()
                    {
                        if !stopping.is_cancelled() {
                            let _ = data.cancel_lane(config.lane);
                            status.send_modify(|s| {
                                s.terminal = true;
                                s.recovering = false;
                            });
                        }
                        break;
                    }
                }
                status.send_modify(|s| s.running = false);
                exit.clean = true;
            })
            .map_err(|_| "Native futures delivery parent admission failed")?;
        Ok(Self {
            stop,
            task: Some(task),
            status: observed,
            current,
            retirement,
        })
    }
    #[allow(clippy::too_many_arguments)] // One cold native task context, not an FFI surface.
    async fn pump(
        data: &LiveData,
        events: &mut Futures,
        config: &Configuration,
        sequence: &mut i64,
        current: &Current,
        publisher: &mut Publisher,
        stop: &CancellationToken,
        episode: &mut Option<Deadline>,
        status: &watch::Sender<Status>,
    ) -> Option<String> {
        let mut source = data.updates();
        let mut broker = publisher.changes();
        let mut lifecycle = match data.lifecycle(config.lane) {
            Ok(Some(status)) => status,
            Ok(None) => return Some("Native source owner absent".into()),
            Err(error) => return Some(error),
        };
        let mut submitted = 0u64;
        let mut first_current: Option<(u128, u64)> = None;
        loop {
            if stop.is_cancelled() {
                return None;
            }
            let provider = lifecycle.borrow().clone();
            if let Some(original) = provider.deadline {
                *episode = Some(episode.map_or(original, |prior| prior.earliest(original)));
            }
            match provider.phase {
                crate::dataset::lanes::Phase::Failed | crate::dataset::lanes::Phase::Stopped => {
                    return Some(
                        provider
                            .error
                            .unwrap_or_else(|| "Native source owner stopped".into()),
                    );
                }
                crate::dataset::lanes::Phase::Starting
                | crate::dataset::lanes::Phase::Recovering => status.send_modify(|s| {
                    s.running = false;
                    s.recovering = true;
                    s.deadline = *episode;
                    if let Some(error) = &provider.error {
                        s.root_error.get_or_insert(error.clone());
                        s.error = Some(error.clone());
                    }
                }),
                crate::dataset::lanes::Phase::Running => {}
            }
            let health = publisher.status();
            let pending = health.pending_since.map(Deadline::recovery_from);
            if let Some(error) = health.failure {
                if let Some(pending) = pending {
                    episode.get_or_insert(pending);
                }
                return Some(
                    health
                        .detail
                        .map_or_else(|| error.into(), |detail| format!("{error}: {detail}")),
                );
            }
            if health.stopped {
                if let Some(pending) = pending {
                    episode.get_or_insert(pending);
                }
                return Some("Native broker transport stopped".into());
            }
            let physical = data.generation(config.lane).ok().flatten();
            let fresh = first_current.is_some_and(|(generation, threshold)| {
                Some(generation) == physical && health.flushed >= threshold
            }) || config.control_only && health.connected;
            if provider.phase == crate::dataset::lanes::Phase::Running
                && fresh
                && episode.take().is_some()
            {
                status.send_modify(|s| {
                    s.running = true;
                    s.recovering = false;
                    s.deadline = None;
                    s.root_error = None;
                    s.error = None;
                });
            }
            let mut drained = 0;
            if health.connected {
                for _ in 0..64 {
                    let next =
                        match data.consume_output(config.lane, |value| Ok(Some(value.clone()))) {
                            Ok(value) => value,
                            Err(error) => return Some(error),
                        };
                    let Some(next) = next else {
                        break;
                    };
                    drained += 1;
                    if data.generation(config.lane).ok().flatten() != Some(next.generation) {
                        continue;
                    }
                    let batch = match events.process(&next) {
                        Ok(batch) => batch,
                        Err(error) => return Some(error.into()),
                    };
                    for event in batch {
                        let Some(next_sequence) = sequence.checked_add(1) else {
                            return Some("Native publication sequence exhausted".into());
                        };
                        *sequence = next_sequence;
                        let contract = if matches!(event.kind, 3 | 4) {
                            event
                                .value
                                .as_array()
                                .and_then(|v| v.get(2))
                                .and_then(ValueContract::contract)
                        } else {
                            None
                        };
                        let payload = match codec::encode(&event.value) {
                            Ok(payload) => payload,
                            Err(error) => return Some(error.into()),
                        };
                        let frame = Frame {
                            lane: config.lane,
                            value_date: config.value_date,
                            worker: config.worker,
                            generation: config.generation,
                            manifest_revision: config.revision,
                            sequence: *sequence,
                            kind: event.kind,
                            payload,
                        };
                        if let Some(contract) = contract
                            && let Err(error) = current.retain(contract, &frame)
                        {
                            return Some(error.into());
                        }
                        if let Err(error) = publisher.try_publish(&frame) {
                            return Some(error.into());
                        }
                        submitted = submitted.saturating_add(1);
                        if first_current.is_none_or(|(physical, _)| physical != next.generation) {
                            first_current = Some((next.generation, submitted));
                        }
                    }
                }
            }
            if drained == 64 {
                tokio::task::yield_now().await;
                continue;
            }
            // Notification only, never a managed read/drain loop or timer poll.
            let deadline = match (*episode, pending) {
                (Some(a), Some(b)) => Some(a.earliest(b)),
                (a, b) => a.or(b),
            };
            tokio::select! {biased; ()=stop.cancelled()=>return None,
            ()=async{match deadline{Some(d)=>{let _=d.run(stop,std::future::pending::<()>()).await;},None=>std::future::pending().await}}=>{
                // Preserve the first pending batch's deadline. The retry owner
                // must report failure now, rather than start another 120 seconds.
                if let Some(deadline)=deadline {*episode=Some(deadline);}
                return Some("Native publication original two-minute deadline expired".into());
            },
            changed=broker.changed()=>if changed.is_err(){return Some("Native broker observation ended".into());},
            changed=source.changed()=>if changed.is_err(){return Some("Native source observation ended".into());},
            changed=lifecycle.changed()=>if changed.is_err(){
                let provider=lifecycle.borrow().clone();
                if let Some(original)=provider.deadline {*episode=Some(episode.map_or(original,|prior|prior.earliest(original)));}
                return Some(provider.error.unwrap_or_else(||"Native lifecycle observation ended".into()));
            }}
        }
    }
    /// Non-consuming native availability status even when the broker is down.
    pub fn status(&self) -> Status {
        self.status.borrow().clone()
    }
    /// Event-driven availability observations, independent of publication capacity.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.status.clone()
    }
    /// Detached latest-value query owner; retaining it cannot retain a dataset.
    pub fn current(&self) -> Arc<Current> {
        self.current.clone()
    }
    /// Signals cancellation and proves producer/socket worker exit under the
    /// caller's existing deadline. Repeated calls never extend that deadline.
    ///
    /// # Errors
    /// An expired or failed join is never presented as successful cleanup.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), &'static str> {
        let deadline = {
            let mut saved = self
                .retirement
                .lock()
                .map_err(|_| "Native publication retirement interrupted")?;
            let d = saved.map_or(deadline, |prior| prior.earliest(deadline));
            *saved = Some(d);
            d
        };
        self.stop.cancel();
        if let Some(task) = self.task.as_mut() {
            let joined = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native publication join deadline expired")?;
            self.task.take();
            joined.map_err(|_| "Native publication task failed")?;
        }
        Ok(())
    }
}
struct ValueContract;
impl ValueContract {
    fn contract(value: &rmpv::Value) -> Option<&str> {
        value.as_array()?.first()?.as_str()
    }
}
impl Drop for Delivery {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}
