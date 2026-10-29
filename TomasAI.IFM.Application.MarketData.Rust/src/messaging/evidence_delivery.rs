//! Native copied-data persistence and publication. This task never owns a
//! provider, pricing gate, lease or dataset stop callback. It may be cancelled
//! while storage is stalled; the immutable outbox remains independently readable.
use super::{
    evidence_outbox::{Outbox, Record},
    evidence_protocol,
    publisher::{BrokerSettings, Publisher},
    wire::Frame,
};
use crate::{
    dataset::transport::Lane,
    runtime::{Deadline, owner::Child},
};
use std::{
    sync::{Arc, Mutex},
    time::Duration,
};
use tokio::{
    sync::{mpsc, watch},
    task::JoinHandle,
};
use tokio_util::sync::CancellationToken;

/// Observable native IO status. Availability is not evidence of provider/pricing
/// readiness; the dataset owner combines those independent proofs.
#[derive(Clone, Debug, Default)]
pub struct Status {
    /// Broker receipt subscription is ready and no current IO failure is known.
    pub running: bool,
    /// A reported failure is retrying under one original deadline.
    pub recovering: bool,
    /// Original deadline expired; copied evidence remains retained for inspection.
    pub terminal: bool,
    /// Earliest original IO incident deadline, never a renewed request timeout.
    pub deadline: Option<Deadline>,
    /// Original bounded failure identity.
    pub error: Option<String>,
}
/// Native broker and publication identity. The original source date/generation
/// comes from each immutable record, rather than this task's current session.
pub struct Configuration {
    /// Explicit native broker/resolver/TLS settings.
    pub broker: BrokerSettings,
    /// Existing deployment scope token.
    pub scope: String,
    /// Logical worker in CLR Guid byte order.
    pub worker: [u8; 16],
    /// Positive manifest revision owned by the native dataset coordinator.
    pub revision: i64,
    /// Shared actor writer for the revised dataset owner. Storage requests stay
    /// on this task's separate connection; actor events share quote ordering.
    /// None preserves the isolated legacy qualification path only.
    pub actor: Option<Arc<super::ordered::Ordered>>,
}
/// Joined native evidence IO task and copied status receiver.
pub struct Delivery {
    stop: CancellationToken,
    task: Option<JoinHandle<()>>,
    status: watch::Receiver<Status>,
    retirement: Arc<Mutex<Option<Deadline>>>,
}
struct Exit {
    status: watch::Sender<Status>,
    stop: CancellationToken,
}
impl Drop for Exit {
    fn drop(&mut self) {
        self.status.send_modify(|s| {
            s.running = false;
            if !self.stop.is_cancelled() && !s.terminal {
                s.terminal = true;
                s.recovering = false;
                s.error
                    .get_or_insert("Native evidence owner exited unexpectedly".into());
            }
        });
    }
}
impl Delivery {
    /// Starts beneath a native ownership tree without opening any managed stream.
    /// The bounded outbox stores copied bytes only. No writer acknowledgment is
    /// needed for cancellation or connection shutdown.
    ///
    /// # Errors
    /// Rejects invalid publication identity or exhausted parent task admission.
    pub fn start(
        outbox: Arc<Outbox>,
        config: Configuration,
        parent: Arc<Child>,
        setup: Deadline,
    ) -> Result<Self, &'static str> {
        Frame {
            lane: Lane::OptionsGlbx,
            value_date: 1,
            worker: config.worker,
            generation: [1; 16],
            manifest_revision: config.revision,
            sequence: 1,
            kind: 6,
            payload: vec![0xc0],
        }
        .subject(&config.scope)?;
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let owned = parent.clone();
        let (status, observed) = watch::channel(Status::default());
        let retirement = Arc::new(Mutex::new(None));
        let retiring = retirement.clone();
        let task = parent
            .spawn_joined(move |_| async move {
                let _exit = Exit {
                    status: status.clone(),
                    stop: stopping.clone(),
                };
                let mut episode = Some(setup);
                let mut sequence = 0i64;
                let mut changes = outbox.changes();
                loop {
                    if stopping.is_cancelled() {
                        break;
                    }
                    let budget = episode.unwrap_or_else(Deadline::recovery);
                    let (mut publisher, mut receipts) = match Publisher::start_evidence_exchange(
                        config.broker.clone(),
                        config.scope.clone(),
                        2,
                        512 * 1024,
                        budget,
                        &owned,
                    ) {
                        Ok(p) => p,
                        Err(e) => {
                            status.send_modify(|s| {
                                s.error = Some(e.into());
                                s.terminal = true;
                            });
                            break;
                        }
                    };
                    let mut progress = publisher.changes();
                    let mut submitted = 0u64;
                    let result = Self::pump(
                        &outbox,
                        &config,
                        &mut publisher,
                        &mut receipts,
                        &mut progress,
                        &mut changes,
                        &stopping,
                        &mut episode,
                        &status,
                        &mut sequence,
                        &mut submitted,
                    )
                    .await;
                    if stopping.is_cancelled() {
                        let cleanup = retiring
                            .lock()
                            .ok()
                            .and_then(|d| *d)
                            .or(episode)
                            .unwrap_or_else(Deadline::recovery);
                        let _ = publisher.shutdown(cleanup).await;
                        break;
                    }
                    let error = result.err().unwrap_or("Native evidence IO stopped");
                    let detail = publisher.status().detail.unwrap_or_else(|| error.into());
                    let deadline = *episode.get_or_insert_with(Deadline::recovery);
                    status.send_modify(|s| {
                        s.running = false;
                        s.recovering = true;
                        s.deadline = Some(deadline);
                        s.error.get_or_insert(detail);
                    });
                    let joined = publisher.shutdown(deadline).await;
                    if joined.is_err() || deadline.remaining().is_zero() {
                        status.send_modify(|s| {
                            s.terminal = true;
                            s.recovering = false;
                        });
                        break;
                    }
                    if deadline
                        .run(&stopping, tokio::time::sleep(Duration::from_millis(200)))
                        .await
                        .is_err()
                    {
                        if !stopping.is_cancelled() {
                            status.send_modify(|s| {
                                s.terminal = true;
                                s.recovering = false;
                            });
                        }
                        break;
                    }
                }
                status.send_modify(|s| s.running = false);
            })
            .map_err(|_| "Native evidence delivery task admission failed")?;
        Ok(Self {
            stop,
            task: Some(task),
            status: observed,
            retirement,
        })
    }
    #[allow(clippy::too_many_arguments)]
    async fn pump(
        outbox: &Outbox,
        config: &Configuration,
        publisher: &mut Publisher,
        receipts: &mut mpsc::Receiver<Vec<u8>>,
        progress: &mut watch::Receiver<super::publisher::Status>,
        changes: &mut watch::Receiver<u64>,
        stop: &CancellationToken,
        episode: &mut Option<Deadline>,
        status: &watch::Sender<Status>,
        sequence: &mut i64,
        submitted: &mut u64,
    ) -> Result<(), &'static str> {
        loop {
            let broker = progress.borrow_and_update().clone();
            if broker.failure.is_some() || broker.stopped {
                return Err("Native evidence broker unavailable");
            }
            if !broker.connected {
                let d = episode.unwrap_or_else(Deadline::recovery);
                d.run(stop, progress.changed())
                    .await
                    .map_err(|_| "Native evidence setup original deadline expired")?
                    .map_err(|_| "Native evidence broker status closed")?;
                continue;
            }
            let persisted = outbox.capture(true, 1)?;
            let pending = outbox.capture(false, 1)?;
            let record = persisted.first().or_else(|| pending.first()).cloned();
            let Some(record) = record else {
                *episode = None;
                status.send_modify(|s| {
                    s.running = true;
                    s.recovering = false;
                    s.deadline = None;
                    s.error = None;
                });
                tokio::select! {biased;()=stop.cancelled()=>return Ok(()),result=changes.changed()=>result.map_err(|_|"Native evidence outbox notifications closed")?,result=progress.changed()=>result.map_err(|_|"Native evidence broker status closed")?};
                continue;
            };
            // IO retry, publication and acknowledgment share the first original
            // source submission window. Broker reconnection cannot renew it.
            let original = Deadline::recovery_from(record.submitted);
            let deadline = episode.map_or(original, |d| d.earliest(original));
            *episode = Some(deadline);
            if persisted.is_empty() {
                publisher.try_evidence(&record)?;
                *submitted = submitted
                    .checked_add(1)
                    .ok_or("Native evidence publication count exhausted")?;
                loop {
                    let received = Self::receipt(receipts, progress, stop, deadline).await?;
                    let ack = evidence_protocol::decode_receipt(&received)?;
                    if ack.receipt != record.receipt
                        || ack.identity != record.identity
                        || ack.digest != record.digest
                    {
                        continue;
                    }
                    if !ack.persisted {
                        return Err("External durable evidence persistence failed");
                    }
                    outbox.acknowledge(&ack.identity, &ack.digest, ack.receipt)?;
                    break;
                }
            }
            if let Some(actor) = &config.actor {
                let submitted = actor.submit(
                    6,
                    record.bytes.to_vec(),
                    record.generation,
                    Some(&record.contract),
                )?;
                actor.confirmed(submitted, stop, deadline).await?;
            } else {
                *sequence = sequence
                    .checked_add(1)
                    .ok_or("Native evidence publication sequence exhausted")?;
                let frame = Self::frame(&record, config, *sequence);
                publisher.try_publish(&frame)?;
                *submitted = submitted
                    .checked_add(1)
                    .ok_or("Native evidence publication count exhausted")?;
                loop {
                    let broker = progress.borrow_and_update().clone();
                    if broker.failure.is_some() || broker.stopped {
                        return Err("Native acknowledged evidence publication failed");
                    }
                    if broker.flushed >= *submitted {
                        break;
                    }
                    // Retransmission may produce duplicate durable receipts. Drain
                    // them without letting the bounded response channel block cleanup.
                    tokio::select! {biased;()=stop.cancelled()=>return Ok(()),
                        result=deadline.run(stop,progress.changed())=>{result.map_err(|_|"Native evidence original publication deadline expired")?.map_err(|_|"Native evidence broker status closed")?;},
                        reply=receipts.recv()=>{if reply.is_none(){return Err("Native evidence receipt stream closed");}}
                    }
                }
            }
            outbox.complete(&record.identity, record.receipt)?;
            // A completed durable write plus confirmed actor publication closes
            // this IO incident even while more ordinary work is ready. Keeping
            // the startup budget until the queue became empty would falsely fail
            // a healthy continuous feed two minutes after startup.
            *episode = None;
            status.send_modify(|s| {
                s.running = true;
                s.recovering = false;
                s.deadline = None;
                s.error = None;
            });
        }
    }
    fn frame(record: &Record, config: &Configuration, sequence: i64) -> Frame {
        Frame {
            lane: Lane::OptionsGlbx,
            value_date: record.value_date,
            worker: config.worker,
            generation: record.generation,
            manifest_revision: config.revision,
            sequence,
            kind: 6,
            payload: record.bytes.to_vec(),
        }
    }
    async fn receipt(
        receipts: &mut mpsc::Receiver<Vec<u8>>,
        progress: &mut watch::Receiver<super::publisher::Status>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Vec<u8>, &'static str> {
        loop {
            let broker = progress.borrow_and_update().clone();
            if broker.failure.is_some() || broker.stopped {
                return Err("Native evidence receipt broker failed");
            }
            tokio::select! {biased;()=stop.cancelled()=>return Err("Native evidence IO cancelled"),
                result=deadline.run(stop,receipts.recv())=>return result.map_err(|_|"Native evidence original persistence deadline expired")?.ok_or("Native evidence receipt stream closed"),
                result=progress.changed()=>result.map_err(|_|"Native evidence broker status closed")?
            }
        }
    }
    /// Copied lifecycle observation; it contains no database task or provider lock.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.status.clone()
    }
    /// Cancels the independent IO task and joins it under the original budget.
    ///
    /// # Errors
    /// Missing cleanup proof is never reported as successful dataset disposal.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), &'static str> {
        {
            let mut retirement = self
                .retirement
                .lock()
                .map_err(|_| "Native evidence retirement writer interrupted")?;
            *retirement = Some(retirement.map_or(deadline, |prior| prior.earliest(deadline)));
        }
        self.stop.cancel();
        if let Some(task) = self.task.as_mut() {
            let joined = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native evidence delivery join deadline expired")?;
            self.task.take();
            joined.map_err(|_| "Native evidence delivery task failed")?;
        }
        Ok(())
    }
}
impl Drop for Delivery {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}
