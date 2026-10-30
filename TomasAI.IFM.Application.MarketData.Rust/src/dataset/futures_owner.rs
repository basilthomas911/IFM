//! Concrete native futures resources: pinned SDK -> native books/assembly ->
//! native NATS. The persistent supervisor can cancel/join this entire group
//! without a managed drain, downstream actor, database or broker acknowledgment.
use super::{
    live::{Diagnostics, Evidence, LiveData, Queries, Start},
    supervisor::{Factory, Readiness, Resources},
    transport::Lane,
};
use crate::messaging::diagnostic_delivery;
use crate::{
    messaging::{
        current::Current,
        futures::Futures,
        futures_delivery::{Configuration, Delivery},
        publisher::BrokerSettings,
    },
    runtime::{Deadline, clock::Clock, owner::Child, recovery::Proof},
};
use futures_util::future::BoxFuture;
use std::sync::{Arc, Mutex};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;
/// Complete copied desired futures inputs, retained outside physical resources.
#[derive(Clone)]
pub struct Intent {
    /// Optional native definition discovery, repeated inside each dataset rebuild.
    pub bootstrap: Option<Arc<super::futures_bootstrap::Discovery>>,
    /// Authoritative API trading date.
    pub date: time::Date,
    /// Exact definition snapshot date.
    pub definition: time::Date,
    /// Verified subscriptions, registrations and explicit source evidence policy.
    pub source: Start,
    /// Explicit native broker/DNS/TLS/authentication configuration.
    pub broker: BrokerSettings,
    /// Existing deployment scope.
    pub scope: String,
    /// Positive native desired-manifest revision.
    pub revision: i64,
}
/// Native factory and detached last-value query directory. Retaining Current
/// never retains a native provider group; it owns copied frames only.
pub struct FuturesFactory {
    bindings: Arc<Mutex<Option<Arc<Vec<super::live::Registration>>>>>,
    contracts: Arc<Mutex<Option<Arc<Vec<rmpv::Value>>>>>,
    public_models: Arc<Mutex<Option<Arc<Vec<rmpv::Value>>>>>,
    clock: Arc<Clock>,
    current: Arc<Mutex<Option<Arc<Current>>>>,
    diagnostics: Arc<Diagnostics>,
    queries: Arc<Mutex<Option<Queries>>>,
    identity: Mutex<Option<crate::messaging::ordered::Header>>,
}
impl FuturesFactory {
    /// Binds the authoritative API clock shared by native source processing.
    pub fn new(clock: Arc<Clock>) -> Self {
        Self {
            bindings: Arc::new(Mutex::new(None)),
            contracts: Arc::new(Mutex::new(None)),
            public_models: Arc::new(Mutex::new(None)),
            clock,
            current: Arc::new(Mutex::new(None)),
            diagnostics: Arc::default(),
            queries: Arc::new(Mutex::new(None)),
            identity: Mutex::new(None),
        }
    }
    /// Detached current canonical/provider mappings, available after native setup.
    pub fn registrations(&self) -> Option<Arc<Vec<super::live::Registration>>> {
        self.bindings.lock().ok().and_then(|v| v.clone())
    }
    /// Exact current contract DTOs from this generation's native bootstrap.
    /// Copied values hold no provider task, connection or dataset child.
    pub fn contracts(&self) -> Option<Arc<Vec<rmpv::Value>>> {
        self.contracts.lock().ok().and_then(|v| v.clone())
    }
    /// Public configured-catalog DTOs from the same native bootstrap source.
    /// Copied metadata cannot retain any physical source resource.
    pub fn public_models(&self) -> Option<Arc<Vec<rmpv::Value>>> {
        self.public_models.lock().ok().and_then(|v| v.clone())
    }
    /// Copies numeric query access without transferring a provider resource.
    pub fn queries(&self) -> Option<Queries> {
        self.queries.lock().ok().and_then(|q| q.clone())
    }
    /// Actual copied native publication identity, available during setup and
    /// retirement. No fabricated worker process or provider handle is exposed.
    pub fn identity(&self) -> Option<crate::messaging::ordered::Header> {
        self.identity
            .lock()
            .ok()
            .and_then(|identity| identity.clone())
    }
    /// Copies the current query store without transferring a resource handle.
    pub fn current(&self) -> Option<Arc<Current>> {
        self.current.lock().ok().and_then(|c| c.clone())
    }
}
struct Group {
    parent: Arc<Child>,
    task: Option<JoinHandle<()>>,
    status: watch::Receiver<Readiness>,
    retirement: Arc<Mutex<Option<Deadline>>>,
}
impl Factory for FuturesFactory {
    fn audit_workloads(&self, intent: &Self::Intent) -> Vec<String> {
        intent
            .source
            .registrations
            .iter()
            .take(256)
            .map(|r| r.contract.clone())
            .collect()
    }
    fn audit_identity(&self) -> Option<crate::messaging::ordered::Header> {
        self.identity()
    }
    type Intent = Intent;
    fn start(
        &self,
        intent: Arc<Intent>,
        parent: Arc<Child>,
        deadline: Deadline,
    ) -> Result<Box<dyn Resources>, String> {
        let lane = intent.source.plan.lane;
        if lane == Lane::OptionsGlbx || intent.revision <= 0 {
            return Err("Invalid native futures dataset intent".into());
        }
        intent
            .source
            .plan
            .validate()
            .map_err(|e| format!("{e:?}"))?;
        if let Some(bootstrap) = &intent.bootstrap {
            bootstrap.validate(lane)?;
        }
        let worker = rand::random::<u128>().max(1).to_le_bytes();
        let generation = rand::random::<u128>().max(1).to_le_bytes();
        *self
            .identity
            .lock()
            .map_err(|_| "Native futures identity directory interrupted")? =
            Some(crate::messaging::ordered::Header {
                lane,
                value_date: intent.date.to_julian_day() - 1721426,
                worker,
                generation,
                revision: intent.revision,
            });
        let data = Arc::new(
            LiveData::new(
                intent.date,
                u128::from_le_bytes(generation),
                2048,
                self.clock.clone(),
                parent.owner().clone(),
            )
            .map_err(|e| format!("{e:?}"))?
            .with_diagnostics(self.diagnostics.clone(), worker)?,
        );
        let (status, observed) = watch::channel(Readiness::default());
        let output = self.current.clone();
        let queries = self.queries.clone();
        *queries
            .lock()
            .map_err(|_| "Native query directory interrupted")? = None;
        // Old copied frames stay readable through an explicitly retained Current,
        // but a new query must not be presented as current-generation evidence.
        *output
            .lock()
            .map_err(|_| "Native futures query directory interrupted")? = None;
        let bindings = self.bindings.clone();
        // Canonical metadata is copied data, not a provider resource. Retain it
        // during replacement so a known contract reports an unavailable price,
        // rather than becoming unknown. Current prices/queries above are fenced.
        let public_models = self.public_models.clone();
        *public_models
            .lock()
            .map_err(|_| "Native public contract directory interrupted")? = None;
        let contracts = self.contracts.clone();
        *contracts
            .lock()
            .map_err(|_| "Native contract directory interrupted")? = None;
        let retirement = Arc::new(Mutex::new(None));
        let retiring = retirement.clone();
        let source = data.clone();
        let owned = parent.clone();
        let task=parent.spawn_joined(move|stop|async move{
            let mut setup=Some(deadline);
            let result=async {
                let intent=if let Some(bootstrap)=&intent.bootstrap {
                    let (source, models, public)=bootstrap.resolve_with_models(&intent.source,intent.date,&stop,deadline).await?;
                    *public_models.lock().map_err(|_|"Native public contract directory interrupted")?=Some(Arc::new(public));
                    *contracts.lock().map_err(|_|"Native contract directory interrupted")?=Some(Arc::new(models));
                    Arc::new(Intent{bootstrap:None,date:intent.date,definition:intent.definition,source,broker:intent.broker.clone(),scope:intent.scope.clone(),revision:intent.revision})
                }else{intent};
                let encoder=Futures::new(lane,intent.date,intent.definition,intent.source.registrations.clone(),64)?;
                *bindings.lock().map_err(|_|"Native futures binding directory interrupted")?=Some(Arc::new(intent.source.registrations.clone()));
                let mut diagnostics=diagnostic_delivery::Delivery::start(source.diagnostics(),intent.broker.clone(),intent.scope.clone(),owned.clone())?;
                let mut logs=diagnostics.changes();
                let mut connection_source=intent.source.clone();
                connection_source.evidence=super::live::Evidence::ControlOnly;
                source.start_in_parent(connection_source,&stop,deadline,&owned).await?;
                *queries.lock().map_err(|_|"Native query directory interrupted")?=source.queries(lane)?;
                let mut lifecycle=source.lifecycle(lane)?.ok_or("Native futures source status absent")?;
                let mut delivery=Delivery::start(source.clone(),encoder,Configuration{
                    broker:intent.broker.clone(),scope:intent.scope.clone(),lane,value_date:intent.date.to_julian_day()-1721426,
                    worker,generation,revision:intent.revision,control_only:intent.source.evidence==Evidence::ControlOnly,queue:256,bytes:4*1024*1024,
                },owned.clone(),deadline).map_err(String::from)?;
                *output.lock().map_err(|_|"Native futures query directory interrupted")?=Some(delivery.current());
                let mut publications=delivery.changes();
                loop {
                    let transport=lifecycle.borrow_and_update().clone();let publication=publications.borrow_and_update().clone();
                    let logging=logs.borrow_and_update().clone();
                    if logging.fatal{return Err(logging.error.unwrap_or_else(||"Native diagnostic component failed".into()));}
                    let budget=match (transport.deadline,publication.deadline) {
                        (Some(a),Some(b))=>a.earliest(b),
                        (Some(d),None)|(None,Some(d))=>d,
                        (None,None)=>Deadline::recovery(),
                    };
                    let failure=transport.error.clone().filter(|_|matches!(transport.phase,super::lanes::Phase::Recovering|super::lanes::Phase::Failed))
                        .or_else(||publication.error.clone().filter(|_|publication.recovering||publication.terminal));
                    if failure.is_some() || matches!(transport.phase,super::lanes::Phase::Stopped|super::lanes::Phase::Failed) || publication.terminal {
                        if let Ok(mut saved)=retiring.lock(){*saved=Some(saved.map_or(budget,|d:Deadline|d.earliest(budget)));}
                        status.send_replace(Readiness{setup_complete:false,proofs:vec![],failure:Some(failure.unwrap_or_else(||"Native futures resources stopped".into())),deadline:Some(budget)});
                        break;
                    }
                    let mut proofs=Vec::new();
                    if transport.phase==super::lanes::Phase::Running {proofs.push(Proof::Transport);}
                    if publication.running {proofs.push(Proof::Publication);}
                    if transport.phase==super::lanes::Phase::Running && publication.connected {setup=None;}
                    status.send_replace(Readiness{setup_complete:transport.phase==super::lanes::Phase::Running && publication.connected,proofs,failure:None,deadline:transport.deadline});
                    tokio::select!{biased;()=stop.cancelled()=>break,
                        changed=lifecycle.changed()=>if changed.is_err(){return Err("Native futures source health closed".into());},
                        changed=logs.changed()=>if changed.is_err(){return Err("Native diagnostic health closed".into());},
                        changed=publications.changed()=>if changed.is_err(){return Err("Native futures publication health closed".into());},
                    }
                }
                // Never await a server ACK to cancel; shutdown joins only owned
                // transport workers after cancellation is already signalled.
                let cleanup=retiring.lock().ok().and_then(|d|*d).unwrap_or_else(Deadline::recovery);
                delivery.shutdown(cleanup).await.map_err(String::from)?;
                diagnostics.shutdown(cleanup).await.map_err(String::from)?;
                Ok::<(),String>(())
            }.await;
            if let Err(error)=result {
                let budget=retiring.lock().ok().and_then(|d|*d).or(setup).unwrap_or_else(Deadline::recovery);
                status.send_modify(|s|{s.proofs.clear();s.failure.get_or_insert(error);s.deadline=Some(s.deadline.map_or(budget,|d|d.earliest(budget)));});
            }
        }).map_err(|e|format!("Native futures task admission failed: {e:?}"))?;
        Ok(Box::new(Group {
            parent,
            task: Some(task),
            status: observed,
            retirement,
        }))
    }
}
impl Resources for Group {
    fn provider(&self) -> Option<crate::runtime::owner::ProviderUpdates> {
        Some(self.parent.provider_state())
    }
    fn cancel(&self, deadline: Deadline) {
        self.parent.cancellation().cancel();
        if let Ok(mut saved) = self.retirement.try_lock() {
            *saved = Some(saved.map_or(deadline, |d| d.earliest(deadline)));
        }
    }
    fn reset(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        Box::pin(async move { self.parent.reset_task(&mut self.task, deadline).await })
    }
    fn changes(&self) -> watch::Receiver<Readiness> {
        self.status.clone()
    }
    fn join(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        Box::pin(async move {
            if let Some(task) = self.task.as_mut() {
                let joined = deadline
                    .run(&CancellationToken::new(), task)
                    .await
                    .map_err(|_| "Native futures original join deadline expired")?;
                self.task.take();
                joined.map_err(|_| "Native futures group task panicked")?;
            }
            self.parent
                .wait_nested(&CancellationToken::new(), deadline)
                .await
                .map_err(|_| "Native futures nested-resource join deadline expired".into())
        })
    }
}
impl Drop for Group {
    fn drop(&mut self) {
        self.parent.cancellation().cancel();
    }
}
