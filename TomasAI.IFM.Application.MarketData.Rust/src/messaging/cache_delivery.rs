//! Latest-wins prepared-chain delivery. An in-flight copied cache batch and one
//! replaceable latest batch are bounded independently of the source. Slow broker
//! writes never block source capture or cancellation of its dataset resource tree.
use super::{
    option_cache::Publication,
    publisher::{BrokerSettings, Publisher},
};
use crate::runtime::{Deadline, owner::Child};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;
/// Complete latest publications; aggregate encoded content is capped at 16 MiB.
pub type Batch = BTreeMap<String, Arc<Publication>>;
/// Native cache writer availability, independent of actor publication sequencing.
#[derive(Clone, Debug, Default)]
pub struct Status {
    /// Broker socket is connected and its initial protocol handshake completed.
    pub connected: bool,
    /// At least one complete current-generation batch was flushed.
    pub published: bool,
    /// The task is alive and remains independently cancellable.
    pub running: bool,
    /// Original pending-batch/setup deadline; replacement inputs never extend it.
    pub deadline: Option<Deadline>,
    /// Sanitized failure to be handled by the persistent dataset supervisor.
    pub error: Option<String>,
}
/// Nonblocking latest-batch command port. It retains copied bytes only and cannot
/// keep a dataset child, broker socket or native worker task alive.
#[derive(Clone)]
pub struct Port {
    incoming: watch::Sender<Arc<Batch>>,
    stop: CancellationToken,
}
impl Port {
    /// Replaces pending cache work with the newest complete batch. This is a
    /// display/cache stream; authoritative trade evidence uses its separate spool.
    ///
    /// # Errors
    /// Rejects stopped writers, invalid membership and excessive copied bytes.
    pub fn submit(&self, batch: Batch) -> Result<(), &'static str> {
        if self.stop.is_cancelled() || self.incoming.is_closed() {
            return Err("Native cache writer stopped");
        }
        if batch.len() > 64
            || batch.iter().any(|(scope, p)| scope != &p.scope)
            || batch
                .values()
                .try_fold(0usize, |sum, p| sum.checked_add(p.len()))
                .is_none_or(|sum| sum > 16 * 1024 * 1024)
        {
            return Err("Native cache batch exceeds bounded capacity");
        }
        self.incoming.send_replace(Arc::new(batch));
        Ok(())
    }
}
/// Explicit task ownership. Dropping ports never transfers this join responsibility.
pub struct Delivery {
    stop: CancellationToken,
    input: Port,
    status: watch::Receiver<Status>,
    task: Option<JoinHandle<()>>,
    retirement: Arc<Mutex<Option<Deadline>>>,
}
impl Delivery {
    /// Starts one separately owned native cache connection beneath a dataset.
    ///
    /// # Errors
    /// Invalid broker settings or exhausted/stopped native parent admission.
    pub fn start(
        broker: BrokerSettings,
        scope: String,
        parent: &Child,
        deadline: Deadline,
    ) -> Result<Self, &'static str> {
        let mut publisher = Publisher::start_owned(broker, scope, 2, 256 * 1024, deadline, parent)?;
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let (input, mut incoming) = watch::channel(Arc::new(Batch::new()));
        let (status, observed) = watch::channel(Status {
            running: true,
            deadline: Some(deadline),
            ..Default::default()
        });
        let retirement = Arc::new(Mutex::new(None));
        let retiring = retirement.clone();
        let task=parent.spawn_joined(move |_|async move {
            let mut changes=publisher.changes();let mut sequence=0u64;let mut pending=Some(deadline);
            let result:Result<(),String>=async {
                loop {
                    if stopping.is_cancelled(){return Ok(());}
                    let socket=changes.borrow_and_update().clone();
                    if let Some(error)=socket.failure {return Err(socket.detail.unwrap_or_else(||error.into()));}
                    if socket.stopped {return Err("Native cache connection stopped".into());}
                    status.send_modify(|state|{state.connected=socket.connected;state.deadline=pending;});
                    if socket.connected {
                        // One immutable in-flight batch; new submissions replace
                        // the watch slot while this batch awaits broker progress.
                        let batch=incoming.borrow_and_update().clone();
                        if !batch.is_empty() {
                            let budget=*pending.get_or_insert_with(Deadline::recovery);
                            status.send_modify(|state|state.deadline=Some(budget));
                            for publication in batch.values() {
                                for index in 0..publication.chunks() {
                                    publisher.try_option_cache(publication,index)?;
                                    sequence=sequence.checked_add(1).ok_or("Native cache flush ordinal overflow")?;
                                    budget.run(&stopping,async {
                                        loop {
                                            let socket=changes.borrow_and_update().clone();
                                            if let Some(error)=socket.failure {return Err(socket.detail.unwrap_or_else(||error.into()));}
                                            if socket.stopped {return Err("Native cache writer terminated".into());}
                                            if socket.flushed>=sequence {return Ok(());}
                                            changes.changed().await.map_err(|_|"Native cache writer observation closed".to_owned())?;
                                        }
                                    }).await.map_err(|e|format!("Native cache original deadline/cancellation: {e:?}"))??;
                                }
                            }
                            pending=None;
                            status.send_modify(|state|{state.published=true;state.deadline=None;});
                            if incoming.has_changed().unwrap_or(false){continue;}
                        } else {pending=None;status.send_modify(|state|state.deadline=None);}
                    }
                    let wait=async {
                        tokio::select!{biased;()=stopping.cancelled()=>Ok(()),
                            changed=incoming.changed()=>changed.map_err(|_|"Native cache input closed".to_owned()),
                            changed=changes.changed()=>changed.map_err(|_|"Native cache socket observation closed".to_owned())}
                    };
                    match pending {
                        Some(budget)=>budget.run(&stopping,wait).await.map_err(|e|format!("Native cache setup deadline/cancellation: {e:?}"))??,
                        None=>wait.await?,
                    }
                }
            }.await;
            if !stopping.is_cancelled() && let Err(error)=result {
                let budget=pending.unwrap_or_else(Deadline::recovery);
                status.send_modify(|state|{state.error=Some(error);state.deadline=Some(budget);});
            }
            stopping.cancel();
            let cleanup=retiring.lock().ok().and_then(|d|*d).or(pending).unwrap_or_else(Deadline::recovery);
            let _=publisher.shutdown(cleanup).await;
            status.send_modify(|state|{state.running=false;state.connected=false;});
        }).map_err(|_|"Native cache owner admission failed")?;
        Ok(Self {
            input: Port {
                incoming: input,
                stop: stop.clone(),
            },
            stop,
            status: observed,
            task: Some(task),
            retirement,
        })
    }
    /// Copied queue command port; contains no resource/task ownership.
    pub fn port(&self) -> Port {
        self.input.clone()
    }
    /// Event-driven native health, including the original pending deadline.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.status.clone()
    }
    /// Signals cancellation before attempting a join. No broker PONG is needed
    /// to end a blocked write, request or flush.
    ///
    /// # Errors
    /// Deadline expiry retains a pending handle; completed failures are consumed.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), &'static str> {
        *self
            .retirement
            .lock()
            .map_err(|_| "Native cache retirement interrupted")? = Some(deadline);
        self.stop.cancel();
        if let Some(task) = self.task.as_mut() {
            let result = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native cache join deadline expired")?;
            self.task.take();
            result.map_err(|_| "Native cache task failed")?;
        }
        Ok(())
    }
}
impl Drop for Delivery {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}
