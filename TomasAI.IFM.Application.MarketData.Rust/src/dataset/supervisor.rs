//! Persistent native control outside replaceable dataset children. Accepted
//! intent is independent of caller wait lifetime. Every retry, retirement and
//! readiness proof consumes the same original soft-recovery window. At expiry
//! one final reset has a separate retirement/rebuild allowance; it cannot loop.
use crate::runtime::{
    Deadline,
    owner::{Child, Owner, OwnerError},
    recovery::{Phase, Proof, Recovery, Requirements},
};
use futures_util::{FutureExt, future::BoxFuture};
use std::{
    sync::{Arc, Mutex},
    time::Duration,
};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;

/// Copied readiness supplied by the concrete native dataset implementation.
#[derive(Clone, Default)]
pub struct Readiness {
    /// Final source subscriptions and required components have been rebuilt.
    /// Temporary bootstrap activity and missing market quotes cannot supply this.
    pub setup_complete: bool,
    /// Actual proofs for the current physical owner; never include Retired.
    pub proofs: Vec<Proof>,
    /// Classified sanitized failure requiring whole-dataset replacement.
    pub failure: Option<String>,
    /// Earlier source/reference/broker incident deadline, when already observed.
    pub deadline: Option<Deadline>,
}
/// Native resource group. Implementations must own every task/socket they start.
/// This Rust-only interface is never exported as a managed callback.
pub trait Resources: Send {
    /// Signals all native sub-owners synchronously, without waiting for capacity.
    fn cancel(&self, deadline: Deadline);
    /// Retained event-driven health, independent of market output consumption.
    fn changes(&self) -> watch::Receiver<Readiness>;
    /// Connection liveness is independent of quotes and downstream output.
    fn provider(&self) -> Option<crate::runtime::owner::ProviderUpdates> {
        None
    }
    /// Joins actual task/socket retirement under the supplied original budget.
    /// A timeout must retain ownership so explicit retry cannot overlap owners.
    fn join(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>>;
    /// Emergency retirement after soft recovery expires. Concrete groups abort
    /// their main task, then join tracked descendants before releasing resources.
    /// The fresh allowance belongs only to this final reset, not another soft retry.
    fn reset(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        self.cancel(deadline);
        self.join(deadline)
    }
}
/// Native cold factory. Starting performs bounded synchronous validation/spawn;
/// network/reference setup is asynchronous work owned by the returned group.
pub trait Factory: Send + Sync + 'static {
    /// Immutable copied native reconstruction inputs; contains no managed handles.
    type Intent: Send + Sync + 'static;
    /// Detached affected workload identities for recovery audit only.
    fn audit_workloads(&self, _intent: &Self::Intent) -> Vec<String> {
        vec![]
    }
    /// Copied actual source identity; never a resource handle.
    fn audit_identity(&self) -> Option<crate::messaging::ordered::Header> {
        None
    }
    /// Owns one already allocated root dataset child, then starts native tasks.
    ///
    /// # Errors
    /// Input/admission failures are retried under the original recovery deadline.
    fn start(
        &self,
        intent: Arc<Self::Intent>,
        parent: Arc<Child>,
        deadline: Deadline,
    ) -> Result<Box<dyn Resources>, String>;
}
struct Desired<T> {
    revision: u64,
    intent: Option<Arc<T>>,
    required: Requirements,
}
impl<T> Clone for Desired<T> {
    fn clone(&self) -> Self {
        Self {
            revision: self.revision,
            intent: self.intent.clone(),
            required: self.required,
        }
    }
}
/// Accepted command receipt. Dropping a waiter cannot undo this desired state.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct Accepted {
    /// Monotonic desired-state revision, independent of physical retry attempts.
    pub revision: u64,
}
/// Native command/query control and joined service lifetime for one dataset.
pub struct Supervisor<T> {
    desired: watch::Sender<Desired<T>>,
    control: Mutex<()>,
    stop: CancellationToken,
    task: tokio::sync::Mutex<Option<JoinHandle<()>>>,
    recovery: Arc<Recovery>,
}
impl<T: Send + Sync + 'static> Supervisor<T> {
    /// Starts one persistent native controller outside replaceable child slots.
    /// No managed waiter is needed to keep accepted intent alive.
    ///
    /// # Errors
    /// Rejects exhausted/stopping native root service admission.
    pub fn start<F: Factory<Intent = T>>(
        root: Arc<Owner>,
        factory: Arc<F>,
        intent: Option<Arc<T>>,
        required: Requirements,
        deadline: Deadline,
    ) -> Result<Self, OwnerError> {
        let (desired, updates) = watch::channel(Desired {
            revision: 1,
            intent,
            required,
        });
        let recovery = Recovery::new(required, deadline);
        recovery.plan_final_reset(desired.borrow().intent.is_some());
        let stop = CancellationToken::new();
        let stopping = stop.clone();
        let health = recovery.clone();
        let owner = root.clone();
        let task=root.spawn_service(move|root_stop|async move{
            let mut resources:Option<Box<dyn Resources>>=None;
            // Capture the physical group's ticket at construction. A concurrent
            // fault/command must not stamp old resource proofs with a new ticket.
            let mut resource_ticket=health.status().ticket;
            let mut updates=updates;
            let mut active=0;
            let mut rebuild=true;
            let mut final_reset=false;
            let mut retired_failure=None;
            loop {
                if root_stop.is_cancelled() || stopping.is_cancelled(){break;}
                let desired=updates.borrow_and_update().clone();
                health.plan_final_reset(!final_reset && desired.intent.is_some());
                let status=health.status();
                // Health rearms a later independent incident. An explicit retry
                // after terminal cleanup also owns a fresh final-reset allowance;
                // maintenance cannot reopen Failed and cannot rearm this flag.
                if status.phase==Phase::Running || retired_failure.is_some() && status.phase!=Phase::Failed {final_reset=false;retired_failure=None;}
                health.plan_final_reset(!final_reset && desired.intent.is_some());
                if status.phase==Phase::Failed {
                    if retired_failure!=Some(status.ticket) {
                        // An expired soft budget cannot poll a cleanup future.
                        // One independent allowance covers forced retirement and
                        // replacement readiness; no external caller grants it.
                        let reset_deadline=Deadline::recovery();
                        let retry=!final_reset && desired.intent.is_some();
                        if retry {health.audit_reset_started();}
                        final_reset=true;
                        health.plan_final_reset(false);
                        let ticket=if retry {health.replace(desired.required,reset_deadline)} else {None};
                        let result=if let Some(resource)=resources.as_mut(){
                            resource.cancel(reset_deadline);
                            match reset_deadline.run(&stopping,std::panic::AssertUnwindSafe(resource.reset(reset_deadline)).catch_unwind()).await {
                                Ok(Ok(result))=>result,
                                Ok(Err(_))=>Err("Native final dataset reset panicked".into()),
                                Err(_)=>Err("Native final dataset retirement could not be confirmed".into()),
                            }
                        }else{Ok(())};
                        match result {
                            Ok(())=>{
                                resources.take();
                                if ticket.is_some(){active=0;rebuild=true;continue;}
                            },
                            Err(error)=>{
                                health.fail(&error);
                            }
                        }
                        retired_failure=Some(health.status().ticket);
                    }
                    // Final failure remains observable. Explicit user intent can
                    // retry, but maintenance cannot create an endless reset loop.
                    tokio::select!{biased;()=root_stop.cancelled()=>break,()=stopping.cancelled()=>break,result=updates.changed()=>{if result.is_err(){break;}rebuild=true;}}
                    continue;
                }
                let deadline=status.deadline.unwrap_or_else(Deadline::recovery);
                if active!=desired.revision || rebuild {
                    if let Some(resource)=resources.as_mut(){
                        resource.cancel(deadline);
                        // Cancellation bypasses the producer/mailbox. The join
                        // itself is bounded even if a concrete group misbehaves.
                        let joined=deadline.run(&stopping,std::panic::AssertUnwindSafe(resource.join(deadline)).catch_unwind()).await;
                        if !matches!(joined,Ok(Ok(Ok(())))){
                            let error=match joined{Ok(Ok(Err(e)))=>e,Ok(Err(_))=>"Native dataset retirement panicked".into(),_=>"Native dataset retirement did not finish within its original deadline".into()};
                            health.fault(health.status().ticket,&error,deadline);
                            if deadline.remaining().is_zero(){health.status();continue;}
                            if !retry(deadline,&stopping,&root_stop).await{break;}
                            continue;
                        }
                        resources.take();
                    }
                    if stopping.is_cancelled() || root_stop.is_cancelled(){break;}
                    if updates.borrow().revision != desired.revision { continue; }
                    let Some(ticket)=health.replace_passive(desired.required,deadline) else { if health.status().phase==Phase::Failed {continue;} break; };
                    health.prove(ticket,Proof::Retired);
                    if let Some(intent)=desired.intent {
                        health.audit_workloads(factory.audit_workloads(&intent));
                        health.audit_attempt();
                        let start=owner.child().map_err(|e|format!("Native child admission failed: {e:?}")).and_then(|child|std::panic::catch_unwind(std::panic::AssertUnwindSafe(||factory.start(intent,Arc::new(child),deadline))).unwrap_or_else(|_|Err("Native dataset factory panicked".into())));
                        match start {
                            Ok(resource)=>{resources=Some(resource);resource_ticket=ticket;health.audit_owner(factory.audit_identity());},
                            Err(error)=>{
                                health.fault(health.status().ticket,&error,deadline);
                                if !retry(deadline,&stopping,&root_stop).await{health.status();if stopping.is_cancelled()||root_stop.is_cancelled(){break;}}
                                continue;
                            }
                        }
                    }
                    active=desired.revision;rebuild=false;
                }
                if let Some(resource)=resources.as_ref(){
                    let mut changes=resource.changes();
                    let state=changes.borrow_and_update().clone();
                    let mut provider=resource.provider();
                    if provider.is_some(){health.monitor_provider();}
                    let connection=provider.as_mut().and_then(|events|events.borrow_and_update().clone());
                    if let Some(Err(error))=&connection {
                        health.provider_fault(resource_ticket,error);
                        rebuild=true;continue;
                    }
                    if let Some(error)=state.failure {
                        health.fault(resource_ticket,&error,state.deadline.map_or(deadline,|d|d.earliest(deadline)));
                        rebuild=true;continue;
                    }
                    // A nested bootstrap reader can report provider activity.
                    // Only concrete final-source proofs certify reconstruction.
                    let ticket=resource_ticket;
                    if state.setup_complete {health.monitor_provider();}
                    for proof in state.proofs {if proof!=Proof::Retired{health.prove(ticket,proof);}}
                    if state.setup_complete {health.complete_setup(ticket);}
                    let status=health.status();
                    tokio::select!{biased;
                        ()=root_stop.cancelled()=>break,()=stopping.cancelled()=>break,
                        result=updates.changed()=>{if result.is_err(){break;}},
                        result=changes.changed()=>{if result.is_err(){
                            // A healthy resource may have been idle for hours.
                            // Begin this fault now, never with a deadline captured
                            // before that idle wait. Active incidents keep theirs.
                            let current=health.status();
                            health.fault(ticket,"Native dataset health channel closed",current.deadline.unwrap_or_else(Deadline::recovery));
                            rebuild=true;
                        }},
                        _=async {match provider.as_mut(){Some(events)=>{let _=events.changed().await;},None=>std::future::pending().await}}=>{},
                        ()=expires(status.deadline)=>{health.status();}
                    }
                }else{
                    tokio::select!{biased;()=root_stop.cancelled()=>break,()=stopping.cancelled()=>break,result=updates.changed()=>{if result.is_err(){break;}},()=expires(health.status().deadline)=>{health.status();}}
                }
            }
            if let Some(resource)=resources.as_mut(){
                // This is explicit root disposal, not a fresh recovery allowance.
                // Retained failure remains a query result; stopped is not healthy.
                let deadline=Deadline::recovery();
                resource.cancel(deadline);
                if let Err(error)=resource.reset(deadline).await {health.fail(&error);}
            }
            health.stop();
        })?;
        Ok(Self {
            desired,
            control: Mutex::new(()),
            stop,
            task: tokio::sync::Mutex::new(Some(task)),
            recovery,
        })
    }
    /// Atomically accepts new desired intent. Caller cancellation only cancels
    /// waiting for a result elsewhere; it never withdraws this native command.
    /// Failed datasets can explicitly retry with a fresh native recovery window.
    ///
    /// # Errors
    /// Reports stopped control or exhausted identities before changing intent.
    pub fn apply(
        &self,
        intent: Option<Arc<T>>,
        required: Requirements,
    ) -> Result<Accepted, OwnerError> {
        self.apply_before(intent, required, Deadline::recovery())
    }
    /// Applies a native dependency within the originating dataset episode,
    /// without granting a second recovery window for its later setup step.
    ///
    /// # Errors
    /// Closed control or exhausted desired identity before mutation.
    pub fn apply_before(
        &self,
        intent: Option<Arc<T>>,
        required: Requirements,
        deadline: Deadline,
    ) -> Result<Accepted, OwnerError> {
        self.apply_inner(intent, required, deadline, true)
    }
    /// Applies automatic lease expiry without retrying a failed dataset. Desired
    /// membership is still updated so the next explicit retry uses current demand.
    pub fn apply_maintenance(
        &self,
        intent: Option<Arc<T>>,
        required: Requirements,
    ) -> Result<Accepted, OwnerError> {
        self.apply_inner(intent, required, Deadline::recovery(), false)
    }
    fn apply_inner(
        &self,
        intent: Option<Arc<T>>,
        required: Requirements,
        deadline: Deadline,
        explicit: bool,
    ) -> Result<Accepted, OwnerError> {
        let _control = self
            .control
            .lock()
            .map_err(|_| OwnerError::InternalFailure)?;
        if self.stop.is_cancelled() {
            return Err(OwnerError::Closed);
        }
        let revision = self
            .desired
            .borrow()
            .revision
            .checked_add(1)
            .ok_or(OwnerError::Capacity)?;
        if intent.is_none() {
            self.recovery.audit_interrupt(if explicit {
                "Dataset subscriptions disabled by an explicit command"
            } else {
                "Desired subscriptions removed by native lifetime maintenance"
            });
            self.recovery.plan_final_reset(false);
        }
        if explicit {
            self.recovery
                .replace(required, deadline)
                .ok_or(OwnerError::Closed)?;
        } else if self.recovery.replace_passive(required, deadline).is_none()
            && self.recovery.status().phase != Phase::Failed
        {
            return Err(OwnerError::Closed);
        }
        self.desired.send_replace(Desired {
            revision,
            intent,
            required,
        });
        Ok(Accepted { revision })
    }
    /// Native copied readiness and terminal error, available while NATS is down.
    pub fn recovery(&self) -> Arc<Recovery> {
        self.recovery.clone()
    }
    /// Reports an internal native processing/control fault and wakes whole-group
    /// reconstruction without changing accepted business intent. The earliest
    /// incident deadline is retained; terminal failures need an explicit retry.
    ///
    /// # Errors
    /// Stopped/terminal control or exhausted revision identity is explicit.
    pub fn report_fault(&self, detail: &str, deadline: Deadline) -> Result<Accepted, OwnerError> {
        let _control = self
            .control
            .lock()
            .map_err(|_| OwnerError::InternalFailure)?;
        if self.stop.is_cancelled() {
            return Err(OwnerError::Closed);
        }
        let mut desired = self.desired.borrow().clone();
        desired.revision = desired
            .revision
            .checked_add(1)
            .ok_or(OwnerError::Capacity)?;
        self.recovery
            .fault(self.recovery.status().ticket, detail, deadline)
            .ok_or(OwnerError::Closed)?;
        let revision = desired.revision;
        self.desired.send_replace(desired);
        Ok(Accepted { revision })
    }
    /// Last accepted desired revision, independent of a resource rebuild attempt.
    pub fn revision(&self) -> u64 {
        self.desired.borrow().revision
    }
    /// Non-owning desired-work query. Idle control must not expose the last
    /// retired factory view merely because retirement itself is healthy.
    pub fn has_intent(&self) -> bool {
        self.desired.borrow().intent.is_some()
    }
    /// Copies immutable native reconstruction inputs for a command/query. These
    /// inputs own no provider resource, foreign callback or physical lease.
    pub fn intent(&self) -> Option<Arc<T>> {
        self.desired.borrow().intent.clone()
    }
    /// Explicit stop bypasses every command, publication and persistence queue.
    pub fn cancel(&self) {
        self.recovery.plan_final_reset(false);
        self.stop.cancel();
    }
    /// Joins the control service; its resource groups prove their own retirement.
    ///
    /// # Errors
    /// Caller deadline/cancellation does not detach a still-owned task handle.
    pub async fn join(&self, deadline: Deadline) -> Result<(), String> {
        let mut retained = deadline
            .run(&CancellationToken::new(), self.task.lock())
            .await
            .map_err(|_| "Native supervisor join original deadline expired")?;
        if let Some(task) = retained.as_mut() {
            let joined = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native supervisor join original deadline expired")?;
            retained.take();
            joined.map_err(|_| "Native supervisor task failed")?;
        }
        Ok(())
    }
}
async fn expires(deadline: Option<Deadline>) {
    match deadline {
        Some(d) => tokio::time::sleep(d.remaining()).await,
        None => std::future::pending().await,
    }
}
async fn retry(deadline: Deadline, stop: &CancellationToken, root: &CancellationToken) -> bool {
    tokio::select! {biased;()=root.cancelled()=>false,result=deadline.run(stop,tokio::time::sleep(Duration::from_millis(200)))=>result.is_ok()}
}
