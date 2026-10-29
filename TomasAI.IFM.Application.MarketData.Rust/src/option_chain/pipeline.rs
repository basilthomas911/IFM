//! Tracked pricing-loop supervision. A scheduler failure uses the provider lane's
//! recovery state machine, and only fresh physical-source plus pricing proof can
//! clear its original deadline. No child process or detached blocking task exists.
use super::{
    coalesced::{Coalesced, underlying_quote},
    qualification::Quote,
};
use crate::{
    dataset::{lanes::Phase, live::LiveData, transport::Lane},
    runtime::{Deadline, clock::Clock, owner::Child},
};
use std::sync::{Arc, Mutex};
use tokio_util::sync::CancellationToken;

/// Independent reliable pricing status, merged with provider status by its owner.
#[derive(Clone, Debug)]
pub struct Status {
    /// False after actual loop termination; cancellation alone is not cleanup proof.
    pub running: bool,
    /// An original recovery incident is awaiting fresh physical/pricing evidence.
    pub recovering: bool,
    /// Original failure, retained until proven recovery or explicit replacement.
    pub error: Option<String>,
    /// Original pricing/reference/provider incident, retained on terminal failure.
    pub deadline: Option<Deadline>,
}
struct Exit {
    state: Arc<Mutex<Status>>,
    data: Arc<LiveData>,
    stop: CancellationToken,
    root: Arc<crate::runtime::owner::Owner>,
    clean: bool,
}
impl Drop for Exit {
    fn drop(&mut self) {
        if let Ok(mut status) = self.state.lock() {
            status.running = false;
            status.recovering = false;
            if !self.clean && !self.stop.is_cancelled() {
                status.error = Some("Native pricing supervisor exited unexpectedly".into());
            }
        }
        let _ = self.data.cancel_lane(Lane::OptionsGlbx);
        let _ = self.root.signal(1 << (Lane::OptionsGlbx as u32));
    }
}
/// One tracked asynchronous supervisor for an already-started physical option
/// scope whose LiveData was constructed with the same Coalesced ingress hook.
/// Scope release cancels this owner and joins it before claiming pricing cleanup.
pub struct PricingTask {
    stop: CancellationToken,
    state: Arc<Mutex<Status>>,
    task: tokio::sync::Mutex<Option<tokio::task::JoinHandle<()>>>,
}
impl PricingTask {
    /// Starts native selection/risk loops under the existing logical option owner.
    /// The API clock/logical generation are explicit. Provider socket generations
    /// never substitute for the logical generation in option pricing contexts.
    pub fn start(
        data: Arc<LiveData>,
        pricing: Arc<Coalesced>,
        clock: Arc<Clock>,
        logical: u128,
        selection_ms: u32,
        risk_ms: u32,
        parent: &Child,
    ) -> Result<Self, String> {
        if logical == 0 {
            return Err("Logical pricing generation is required".into());
        }
        Self::start_with_reader(
            data.clone(),
            pricing,
            clock,
            selection_ms,
            risk_ms,
            parent,
            move |instrument| {
                data.raw_quote(Lane::OptionsGlbx, instrument)
                    .map(|tick| tick.and_then(|quote| underlying_quote(quote, logical)))
                    .map_err(|_| "Underlying book writer failed")
            },
        )
    }
    /// Same owned path with an explicitly qualified input reader. Used for
    /// deterministic fault qualification; no alternate runtime/processing loop.
    pub fn start_with_reader(
        data: Arc<LiveData>,
        pricing: Arc<Coalesced>,
        clock: Arc<Clock>,
        selection_ms: u32,
        risk_ms: u32,
        parent: &Child,
        read: impl Fn(u32) -> Result<Option<Quote>, &'static str> + Send + Sync + 'static,
    ) -> Result<Self, String> {
        if !(10..=5000).contains(&selection_ms) || !(10..=60000).contains(&risk_ms) {
            return Err("Invalid option cadence".into());
        }
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let root = parent.owner();
        let state = Arc::new(Mutex::new(Status {
            running: true,
            recovering: false,
            error: None,
            deadline: None,
        }));
        let shared = state.clone();
        let exit = Exit {
            state: shared.clone(),
            data: data.clone(),
            stop: stopping.clone(),
            root: root.clone(),
            clean: false,
        };
        let task=parent.spawn(async move{
            let mut exit=exit;let mut changes=pricing.updates();
            let mut incident:Option<(Deadline,u64,bool)>=None;
            'restart:loop{
                let run=pricing.run_checked(selection_ms,risk_ms,||clock.utc_ticks().map_err(|_|"API clock writer failed"),&read,&stopping);
                tokio::pin!(run);
                loop{
                    let expiry=async{
                        if let Some((deadline,_,_))=incident{
                            let _=deadline.run(&stopping,std::future::pending::<()>()).await;
                        }else{std::future::pending::<()>().await;}
                    };
                    tokio::select!{biased;
                        ()=stopping.cancelled()=>{exit.clean=true;return;}
                        ()=expiry=>{
                            if let Ok(mut state)=shared.lock(){state.error=Some(format!(
                                "Option pricing recovery failed: the original two-minute deadline expired. Root cause: {}",
                                state.error.as_deref().unwrap_or("Fresh pricing evidence unavailable")));}
                            exit.clean=true;return;
                        }
                        update=changes.changed()=>{
                            if update.is_err(){exit.clean=false;return;}
                            // Pricing completion can follow the last provider
                            // quote. Wake the same event-driven DLL receiver;
                            // never wait for another market tick to deliver risk.
                            let _=root.signal(1<<(Lane::OptionsGlbx as u32));
                            if let Some((_,before,reference))=incident{
                                let ready=clock.utc_ticks().ok().and_then(|at|pricing.ready(at).ok()).unwrap_or(false);
                                let provider=data.status(Lane::OptionsGlbx).ok().flatten();
                                if ready && provider.is_some_and(|status|status.phase==Phase::Running&&(status.generation>before || reference && pricing.reference_deadline().ok().flatten().is_none())){
                                    incident=None;
                                    if let Ok(mut state)=shared.lock(){state.recovering=false;state.error=None;state.deadline=None;}
                                    let _=root.signal(1<<(Lane::OptionsGlbx as u32));
                                }
                            }
                        }
                        result=&mut run=>{
                            if stopping.is_cancelled(){exit.clean=true;return;}
                            let detail=result.err().unwrap_or("Native pricing loop ended unexpectedly");
                            let reference=pricing.reference_deadline().ok().flatten();
                            if detail=="Native option reference requires refresh" && reference.is_none(){continue 'restart;}
                            let new_incident=incident.is_none();
                            let (deadline,_,_)=*incident.get_or_insert_with(||{
                                let source=data.status(Lane::OptionsGlbx).ok().flatten();
                                let attempt=source.as_ref().map_or(0,|s|s.generation);
                                let original=reference.or_else(||source.and_then(|s|s.deadline)).unwrap_or_else(Deadline::recovery);
                                (original,attempt,reference.is_some())
                            });
                            if let Ok(mut state)=shared.lock(){state.recovering=true;state.deadline=Some(deadline);if state.error.is_none(){state.error=Some(detail.into());}}
                            let _=root.signal(1<<(Lane::OptionsGlbx as u32));
                            // Admission/cleanup consumes this same incident budget.
                            // Failure is retained while retries continue to expiry.
                            if new_incident && reference.is_none() && let Err(error)=data.report_fault(Lane::OptionsGlbx,detail,&stopping,deadline).await
                                && let Ok(mut state)=shared.lock(){state.error=Some(format!("{detail}; recovery admission failed: {error}"));}
                            if deadline.run(&stopping,tokio::time::sleep(std::time::Duration::from_millis(200))).await.is_err(){
                                // Re-enter the deadline branch to publish the same
                                // terminal outcome rather than renewing a timer.
                            }
                            continue 'restart;
                        }
                    }
                }
            }
        }).map_err(|e|format!("Pricing task admission failed: {e:?}"))?;
        Ok(Self {
            stop,
            state,
            task: tokio::sync::Mutex::new(Some(task)),
        })
    }
    /// Non-consuming failure/recovery observation, independent of quote capacity.
    pub fn status(&self) -> Result<Status, String> {
        self.state
            .lock()
            .map(|state| state.clone())
            .map_err(|_| "Pricing status writer interrupted".into())
    }
    /// Immediate stop signal for scope rollback/drop. Cleanup proof still
    /// requires stop/join; this method never claims that resources are gone.
    pub fn cancel(&self) {
        self.stop.cancel();
    }
    /// Signals immediately, then awaits actual tracked task exit using the caller's
    /// remaining original deadline. The JoinHandle is retained on timeout.
    pub async fn stop(&self, token: &CancellationToken, deadline: Deadline) -> Result<(), String> {
        self.stop.cancel();
        // Do not move the handle into a timeout future: retaining it preserves
        // ownership and permits a later joined cleanup observation.
        let mut owned = deadline
            .run(token, self.task.lock())
            .await
            .map_err(|e| e.to_string())?;
        if let Some(task) = owned.as_mut() {
            match deadline.run(token, task).await {
                Ok(result) => {
                    owned.take();
                    result.map_err(|_| "Native pricing task failed".to_owned())?;
                }
                Err(error) => {
                    return Err(error.to_string());
                }
            }
        }
        owned.take();
        Ok(())
    }
}
impl Drop for PricingTask {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}
