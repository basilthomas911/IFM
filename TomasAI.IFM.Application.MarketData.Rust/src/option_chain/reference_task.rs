//! Native reference refresh supervision beneath the dataset ownership tree.
//! Reference acquisition, numerical admission and cleanup share the same native
//! incident deadline. No lease renewal or context rebuild calls into managed code.
use super::{
    coalesced::{Coalesced, Refresh},
    preparation::ReviewedScope,
};
use crate::{
    dataset::{
        live::{LiveData, Registration},
        transport::Lane,
    },
    runtime::{Deadline, clock::Clock, owner::Child},
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
    time::Duration,
};
use tokio_util::sync::CancellationToken;

/// Reliable native state, independent of market publication and diagnostic queues.
#[derive(Clone, Debug)]
pub struct Status {
    /// True while the reference supervisor is owned and running.
    pub running: bool,
    /// An original two-minute incident is awaiting full fresh pricing proof.
    pub recovering: bool,
    /// Sanitized native preparation failure, retained until proven recovery.
    pub error: Option<String>,
}
/// One tracked scope task. Drop signals cancellation; stop additionally joins.
pub struct ReferenceTask {
    stop: CancellationToken,
    state: Arc<Mutex<Status>>,
    task: tokio::sync::Mutex<Option<tokio::task::JoinHandle<()>>>,
}
struct Exit {
    state: Arc<Mutex<Status>>,
    data: Arc<LiveData>,
    clean: bool,
}
impl Drop for Exit {
    fn drop(&mut self) {
        if let Ok(mut state) = self.state.lock() {
            state.running = false;
            if !self.clean {
                state.recovering = false;
                state.error = Some("Native reference supervisor exited unexpectedly".into());
            }
        }
        if !self.clean {
            let _ = self.data.cancel_lane(Lane::OptionsGlbx);
        }
    }
}
impl ReferenceTask {
    /// Starts after the complete native reference batch is installed. Each scope
    /// owns its own cancellation, so an HTTP/cache stall cannot retain its feed.
    ///
    /// # Errors
    /// Invalid/stopped parent ownership cannot admit an untracked task.
    #[allow(clippy::too_many_arguments)] // One cold native task context.
    pub fn start(
        reviewed: Arc<ReviewedScope>,
        registrations: Vec<Registration>,
        engine: Arc<Coalesced>,
        data: Arc<LiveData>,
        clock: Arc<Clock>,
        generation: u128,
        parent: &Child,
        on_digest: impl Fn(String) -> Result<(), String> + Send + Sync + 'static,
    ) -> Result<Self, String> {
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let state = Arc::new(Mutex::new(Status {
            running: true,
            recovering: false,
            error: None,
        }));
        let shared = state.clone();
        let root = parent.owner();
        let exit = Exit {
            state: shared.clone(),
            data: data.clone(),
            clean: false,
        };
        let task=parent.spawn(async move {
            let mut exit=exit;let mut clocks=clock.changes();let mut demands=engine.reference_updates();
            let mut source_changes=data.lifecycle(Lane::OptionsGlbx).ok().flatten();
            let result:Result<(),String>=async {
                loop {
                    if stopping.is_cancelled(){return Ok(());}
                    let at=clock.utc_ticks().map_err(|_|"API reference clock unavailable")?;
                    let (valid,until)=engine.reference_window(at)?;
                    if valid {
                        let wait=Duration::from_nanos(u64::try_from(i128::from(until)-i128::from(at)).unwrap_or(u64::MAX).saturating_mul(100));
                        tokio::select!{biased;()=stopping.cancelled()=>return Ok(()),changed=clocks.changed()=>{changed.map_err(|_|"API reference clock observation ended")?;},changed=demands.changed()=>{changed.map_err(|_|"Native reference observation ended")?;},()=tokio::time::sleep(wait.min(Duration::from_secs(86400)))=>{}}
                        continue;
                    }
                    let provider=data.status(Lane::OptionsGlbx)?.ok_or("Native reference source owner absent")?;
                    let before=provider.generation;let require_new=provider.phase==crate::dataset::lanes::Phase::Running;
                    let original=engine.begin_reference_recovery()?;
                    let deadline=engine.limit_reference_recovery(provider.deadline.map_or(original,|other|other.earliest(original)))?;
                    if let Ok(mut status)=shared.lock(){status.recovering=true;status.error.get_or_insert("Native reference assumptions require refresh".into());}
                    let _=root.signal(1<<(Lane::OptionsGlbx as u32));
                    if require_new
                        && let Err(error)=data.report_fault(Lane::OptionsGlbx,"Native reference assumptions require refresh",&stopping,deadline).await
                        && let Ok(mut status)=shared.lock(){status.error=Some(error);}
                    loop {
                        let attempt:Result<(),String>=async {
                            let next=Box::pin(reviewed.prepare(&registrations,generation,&stopping,deadline)).await?;
                            let prior=engine.context_identities()?;
                            let mut updates=Vec::with_capacity(next.configuration.options.len());let mut manifests=BTreeMap::new();
                            for option in next.configuration.options {
                                updates.push(Refresh{instrument:option.instrument,expected:*prior.get(&option.instrument).ok_or("Native reference membership changed")?,context:option.context});
                                manifests.insert(option.instrument,option.manifest.ok_or("Native reference manifest missing")?);
                            }
                            engine.refresh_references(&updates,&manifests,&next.references)?;
                            on_digest(next.configuration.digest)?;
                            let mut pricing=engine.updates();
                            deadline.run(&stopping,async {
                                loop {
                                    let at=clock.utc_ticks().map_err(|_|"API reference clock unavailable")?;
                                    let source=data.status(Lane::OptionsGlbx)?;
                                    let replaced=source.is_some_and(|s|s.phase==crate::dataset::lanes::Phase::Running && if require_new{s.generation>before}else{s.generation>=before});
                                    if replaced && engine.complete_reference_recovery(at)? {return Ok::<(),String>(());}
                                    tokio::select!{biased;()=stopping.cancelled()=>return Err("Native reference recovery cancelled".into()),changed=pricing.changed()=>{changed.map_err(|_|"Native pricing observation ended")?;},changed=clocks.changed()=>{changed.map_err(|_|"API reference clock observation ended")?;},
                                    changed=async {match &mut source_changes {Some(source)=>source.changed().await,None=>std::future::pending().await}}=>{changed.map_err(|_|"Native reference lifecycle observation ended")?;}}
                                }
                            }).await.map_err(|e|format!("Native reference original deadline/cancellation: {e:?}"))??;
                            Ok(())
                        }.await;
                        if stopping.is_cancelled(){return Ok(());}
                        match attempt {
                            Ok(())=>{if let Ok(mut status)=shared.lock(){status.recovering=false;status.error=None;}let _=root.signal(1<<(Lane::OptionsGlbx as u32));break;},
                            Err(error)=>{if let Ok(mut status)=shared.lock(){status.error=Some(error);}}
                        }
                        if deadline.run(&stopping,tokio::time::sleep(Duration::from_millis(200))).await.is_err(){
                            if stopping.is_cancelled(){return Ok(());}
                            return Err("Native reference recovery failed: the original two-minute deadline expired".into());
                        }
                    }
                }
            }.await;
            if let Err(error)=result {if let Ok(mut status)=shared.lock(){status.error=Some(error);status.recovering=false;}let _=data.cancel_lane(Lane::OptionsGlbx);}
            exit.clean=true;let _=root.signal(1<<(Lane::OptionsGlbx as u32));
        }).map_err(|e|format!("Native reference task admission: {e:?}"))?;
        Ok(Self {
            stop,
            state,
            task: tokio::sync::Mutex::new(Some(task)),
        })
    }
    /// Reliable native status; reading cannot acquire a dataset lease.
    pub fn status(&self) -> Result<Status, String> {
        self.state
            .lock()
            .map(|s| s.clone())
            .map_err(|_| "Native reference status interrupted".into())
    }
    /// Immediate independent cancellation, including source-cache gate waiters.
    pub fn cancel(&self) {
        self.stop.cancel();
    }
    /// Proves task exit using the original caller deadline; timeout retains handle.
    ///
    /// # Errors
    /// An unfinished/failed join is never reported as successful cleanup.
    pub async fn stop(&self, stop: &CancellationToken, deadline: Deadline) -> Result<(), String> {
        self.cancel();
        let mut owned = deadline
            .run(stop, self.task.lock())
            .await
            .map_err(|e| e.to_string())?;
        if let Some(task) = owned.as_mut() {
            let result = deadline.run(stop, task).await.map_err(|e| e.to_string())?;
            // A completed failed join is consumed before reporting failure. A
            // retry must not poll an already completed JoinHandle.
            owned.take();
            result.map_err(|_| "Native reference task failed")?;
        }
        Ok(())
    }
}
impl Drop for ReferenceTask {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[tokio::test]
    async fn failed_reference_join_is_consumed_before_a_cleanup_retry() {
        let handle = tokio::spawn(std::future::pending::<()>());
        handle.abort();
        while !handle.is_finished() {
            tokio::task::yield_now().await;
        }
        let task = ReferenceTask {
            stop: CancellationToken::new(),
            state: Arc::new(Mutex::new(Status {
                running: false,
                recovering: false,
                error: None,
            })),
            task: tokio::sync::Mutex::new(Some(handle)),
        };
        assert!(
            task.stop(&CancellationToken::new(), Deadline::recovery())
                .await
                .is_err()
        );
        assert!(task.task.lock().await.is_none());
        task.stop(&CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
    }
}
