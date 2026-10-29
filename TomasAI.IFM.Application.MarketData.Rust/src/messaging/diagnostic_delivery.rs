//! Event-driven provider logging beneath a native dataset. One in-flight copied
//! diagnostic and the persistent 256-record ring remain bounded. A stalled log
//! sink cannot block provider processing, cancellation or resource retirement.
use super::publisher::{BrokerSettings, Publisher};
use crate::{
    dataset::live::Diagnostics,
    runtime::{Deadline, owner::Child},
};
use std::{sync::Arc, time::Duration};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;
/// Native diagnostic component health. Known broker I/O retry is separate from
/// an unknown internal logging-task exception, which requires dataset recovery.
#[derive(Clone, Debug, Default)]
pub struct Status {
    /// Task remains owned and event-driven.
    pub running: bool,
    /// Unknown internal failure; never hidden as a transient broker outage.
    pub fatal: bool,
    /// External log-acceptance receipts, not broker-only PONGs or UI display acknowledgments.
    pub accepted: u64,
    /// Sanitized current logging transport/internal detail.
    pub error: Option<String>,
}
struct Exit {
    status: watch::Sender<Status>,
    clean: bool,
}
impl Drop for Exit {
    fn drop(&mut self) {
        self.status.send_modify(|state| {
            state.running = false;
            if !self.clean {
                state.fatal = true;
                state.error = Some("Native diagnostic owner exited unexpectedly".into());
            }
        });
    }
}
/// Explicit native logging task ownership; its input ring contains copied text.
pub struct Delivery {
    stop: CancellationToken,
    task: Option<JoinHandle<()>>,
    status: watch::Receiver<Status>,
}
impl Delivery {
    /// Starts immediately, before provider setup/readiness. Logging retry does
    /// not extend, replace or certify the dataset's independent recovery deadline.
    ///
    /// # Errors
    /// Invalid native parent/task admission is explicit.
    pub fn start(
        sink: Arc<Diagnostics>,
        broker: BrokerSettings,
        scope: String,
        parent: Arc<Child>,
    ) -> Result<Self, &'static str> {
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let (status, observed) = watch::channel(Status {
            running: true,
            ..Default::default()
        });
        let root = parent.clone();
        let task=parent.spawn_joined(move |_|async move {
            let mut exit=Exit{status:status.clone(),clean:false};
            let mut available=sink.changes();let mut pending=None;
            'connection:loop {
                if stopping.is_cancelled(){break;}
                // Diagnostic transport has its own bounded connect/flush attempt,
                // never a managed waiter or an admission condition for prices.
                let budget=Deadline::recovery();
                let (mut publisher,mut replies)=match Publisher::start_diagnostic_exchange(broker.clone(),scope.clone(),budget,&root) {
                    Ok(exchange)=>exchange,
                    Err(error)=>{status.send_modify(|s|{s.fatal=true;s.error=Some(error.into());});break;}
                };
                let mut changed=publisher.changes();let mut sequence=0u64;
                let result:Result<(),String>=async {
                    loop {
                        if stopping.is_cancelled(){return Ok(());}
                        let socket=changed.borrow_and_update().clone();
                        if let Some(error)=socket.failure{return Err(format!("Native diagnostic broker error: {}",socket.detail.unwrap_or_else(||error.into())));}
                        if socket.stopped{return Err("Native diagnostic broker connection stopped".into());}
                        if pending.is_none(){pending=sink.drain(1).map_err(|e|format!("Internal native diagnostic input: {e}"))?.pop();}
                        if socket.connected && let Some(record)=pending.as_ref() {
                            // Source-time worker identity lives in the record;
                            // a replacement must not relabel old pending logs.
                            use sha2::{Digest,Sha256};
                            let identity=format!("{:X}",Sha256::digest(super::diagnostic::encode(record,record.worker)
                                .map_err(|error|format!("Internal native diagnostic encoding: {error}"))?));
                            publisher.try_diagnostic(record,record.worker).map_err(str::to_owned)?;
                            sequence=sequence.checked_add(1).ok_or("Native diagnostic ordinal exhausted")?;
                            Deadline::recovery().run(&stopping,async {
                                let mut accepted=false;
                                loop {
                                    let socket=changed.borrow_and_update().clone();
                                    if let Some(error)=socket.failure{return Err(format!("Native diagnostic broker error: {}",socket.detail.unwrap_or_else(||error.into())));}
                                    if socket.stopped{return Err("Native diagnostic broker connection stopped".into());}
                                    if socket.flushed>=sequence && accepted{return Ok(());}
                                    tokio::select!{biased;
                                        result=changed.changed()=>{result.map_err(|_|"Native diagnostic transport observation closed".to_owned())?;},
                                        reply=replies.recv()=>{
                                            let reply=reply.ok_or("Native diagnostic broker receipt channel closed")?;
                                            let receipt=super::evidence_protocol::decode_receipt(&reply).map_err(|_|"Native diagnostic broker receipt malformed")?;
                                            if receipt.receipt!=1 || receipt.identity!=identity || receipt.digest!=identity || !receipt.persisted || receipt.error.is_some() {
                                                return Err("Native diagnostic broker receipt rejected".into());
                                            }
                                            accepted=true;
                                        }
                                    }
                                }
                            }).await.map_err(|e|format!("Native diagnostic broker flush interrupted: {e:?}"))??;
                            pending=None;status.send_modify(|state|{state.error=None;state.accepted=state.accepted.saturating_add(1);});continue;
                        }
                        tokio::select!{biased;()=stopping.cancelled()=>return Ok(()),
                            result=available.changed()=>{result.map_err(|_|"Internal native diagnostic input observation closed".to_owned())?;},
                            result=changed.changed()=>{result.map_err(|_|"Native diagnostic transport observation closed".to_owned())?;}}
                    }
                }.await;
                let _=publisher.shutdown(Deadline::recovery()).await;
                if stopping.is_cancelled(){break;}
                if let Err(error)=result {
                    let fatal=!(error.starts_with("Native diagnostic broker") || error.starts_with("Native diagnostic transport")
                        || error.starts_with("Native publication") || error.starts_with("Native publisher"));
                    status.send_modify(|state|{state.fatal=fatal;state.error=Some(error);});
                    if fatal {break;}
                }
                tokio::select!{biased;()=stopping.cancelled()=>break 'connection,()=tokio::time::sleep(Duration::from_millis(200))=>{}}
            }
            // Ambiguous broker flush is not proof of display. Preserve this
            // source-time record for replacement/retry rather than losing it.
            if let Some(record)=pending && let Err(error)=sink.retain(record) {
                status.send_modify(|state|{state.fatal=true;state.error=Some(error);});
            }
            exit.clean=true;
        }).map_err(|_|"Native diagnostic task admission failed")?;
        Ok(Self {
            stop,
            task: Some(task),
            status: observed,
        })
    }
    /// Native component observation without a foreign callback.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.status.clone()
    }
    /// Signals before joining; a log consumer/broker is never awaited as cleanup
    /// evidence. Actual native socket/task exit is joined by the resource tree.
    ///
    /// # Errors
    /// Original join expiry retains the task; completed failed joins are consumed.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), &'static str> {
        self.stop.cancel();
        if let Some(task) = self.task.as_mut() {
            let result = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native diagnostic join deadline expired")?;
            self.task.take();
            result.map_err(|_| "Native diagnostic task failed")?;
        }
        Ok(())
    }
}
impl Drop for Delivery {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}
