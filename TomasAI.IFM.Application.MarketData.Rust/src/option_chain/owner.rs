//! Physical option-scope supervision beneath one logical options lane. Imported
//! intent can use native claims/expiry; persistence stores no provider handle. Control never shares a
//! quote mailbox, and emergency cancellation never waits for control capacity.
use crate::{
    dataset::{
        lanes::{Phase, Status},
        live::{Diagnostic, DiagnosticSink, LiveData, Start},
        transport::Lane,
    },
    runtime::{Deadline, clock::Clock, owner::Owner},
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use tokio::sync::{mpsc, oneshot, watch};
use tokio_util::sync::CancellationToken;

struct Scope {
    data: Arc<LiveData>,
    digest: String,
    members: usize,
    option_members: usize,
    retiring: bool,
    intent: PhysicalIntent,
    pricing: Option<Arc<super::pipeline::PricingTask>>,
    pricing_digest: Option<String>,
    engine: Option<Arc<super::coalesced::Coalesced>>,
    pricing_cadence: Option<(u32, u32)>,
    reviewed: Option<Arc<super::preparation::ReviewedScope>>,
    references: Option<Arc<super::reference_task::ReferenceTask>>,
}
struct PreparedPricing {
    reviewed: Option<Arc<super::preparation::ReviewedScope>>,
    engine: Arc<super::coalesced::Coalesced>,
    digest: String,
    selection_ms: u32,
    risk_ms: u32,
    logical: u128,
    statistics: Vec<(u32, super::statistics::Seed)>,
}
/// One application-qualified option context, paired with its exact physical
/// provider registration and current business/display risk demand.
pub struct PricingRegistration {
    /// Exact option instrument in the same physical subscription plan.
    pub instrument: u32,
    /// Immutable reference/calendar/model qualification prepared by application.
    pub context: super::coalesced::Context,
    /// Full risk is needed for trading or an actively monitored display.
    pub required: bool,
    /// Opaque immutable application context retained by actual trade evidence.
    pub manifest: Option<Arc<super::manifest::Manifest>>,
}
/// Reviewed refresh policy and complete bounded scope context batch. Semantic
/// identity must cover this entire immutable configuration, not just symbols.
pub struct PricingConfiguration {
    /// Existing application semantic digest (64 hexadecimal characters).
    pub digest: String,
    /// Exact immutable reviewed refresh-policy version.
    pub policy_version: String,
    /// Every option registration in the physical plan, exactly once.
    pub options: Vec<PricingRegistration>,
    /// Saved application statistics, applied before provider ingress. Unknown
    /// physical instruments are ignored, matching the current seed contract.
    pub statistics: Vec<(u32, super::statistics::Seed)>,
    /// Price/delta cadence in milliseconds.
    pub selection_ms: u32,
    /// IV refresh interval in milliseconds.
    pub iv_ms: u32,
    /// Full risk cadence in milliseconds.
    pub risk_ms: u32,
    /// Maximum numerical work per cooperative batch (1..64).
    pub batch: usize,
}
#[derive(PartialEq, Eq)]
struct PhysicalIntent {
    plan: crate::dataset::transport::SessionPlan,
    registrations: Vec<crate::dataset::live::Registration>,
    evidence: crate::dataset::live::Evidence,
    host: String,
    port: u16,
    dns: Vec<std::net::SocketAddr>,
}
impl PhysicalIntent {
    fn new(request: &Start) -> Self {
        Self {
            plan: request.plan.clone(),
            registrations: request.registrations.clone(),
            evidence: request.evidence,
            host: request.gateway.host.clone(),
            port: request.gateway.port,
            dns: request.gateway.resolver.servers().to_vec(),
        }
    }
}
#[derive(Default)]
struct State {
    scopes: BTreeMap<String, Scope>,
    failure: Option<String>,
    claims: Option<super::claims::Claims>,
    pending_retire: BTreeMap<String, Deadline>,
}
struct SupervisorExit {
    stopping: CancellationToken,
    state: Arc<Mutex<State>>,
    root: Arc<Owner>,
    clean: bool,
}
impl Drop for SupervisorExit {
    fn drop(&mut self) {
        self.stopping.cancel();
        if !self.clean
            && let Ok(mut state) = self.state.lock()
        {
            state.failure=Some("Native options supervisor exited before cleanup proof; physical scopes are retiring".into());
        }
        let _ = self.root.signal(1 << (Lane::OptionsGlbx as u32));
    }
}
enum Action {
    Install {
        id: String,
        digest: String,
        request: Box<Start>,
        pricing: Option<PreparedPricing>,
    },
    Reset(String),
    Remove(String),
}
struct Command {
    action: Action,
    deadline: Deadline,
    reply: oneshot::Sender<Result<(), String>>,
}
struct PendingScope {
    id: String,
    data: Arc<LiveData>,
    state: Arc<Mutex<State>>,
    committed: bool,
}
impl PendingScope {
    fn commit(&mut self) -> Result<(), String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?;
        let scope = state
            .scopes
            .get_mut(&self.id)
            .filter(|scope| Arc::ptr_eq(&scope.data, &self.data))
            .ok_or("Options scope replaced before readiness")?;
        scope.retiring = false;
        self.committed = true;
        Ok(())
    }
}
impl Drop for PendingScope {
    fn drop(&mut self) {
        if !self.committed {
            let _ = self.data.cancel_lane(Lane::OptionsGlbx);
        }
        if !self.committed
            && let Ok(mut state) = self.state.try_lock()
            && let Some(scope) = state.scopes.get_mut(&self.id)
            && Arc::ptr_eq(&scope.data, &self.data)
        {
            // Retain cleanup ownership after cancellation. A retry must join
            // this exact old scope before it can open a replacement connection.
            scope.retiring = true;
            let _ = scope.data.cancel_lane(Lane::OptionsGlbx);
            if let Some(pricing) = &scope.pricing {
                pricing.cancel();
            }
            if let Some(references) = &scope.references {
                references.cancel();
            }
        }
    }
}
/// One options dataset supervisor with bounded independent physical scopes.
/// It owns no process/thread per scope: provider work runs on the shared Tokio
/// runtime. Root shutdown tracks every descendant through actual resource drop.
pub struct Options {
    commands: mpsc::Sender<Command>,
    stopping: CancellationToken,
    state: Arc<Mutex<State>>,
    done: watch::Receiver<bool>,
    task: Mutex<Option<tokio::task::JoinHandle<()>>>,
    generation: u128,
    diagnostics: DiagnosticSink,
    claims_changed: Arc<tokio::sync::Notify>,
}
impl Options {
    /// Starts the in-memory supervisor, consuming one of three logical dataset
    /// slots. No provider connection is opened until install. At most 64 scopes
    /// and 2048 option registrations plus at most 64 underlying dependencies
    /// are admitted. Dependency slots do not consume the advertised option limit.
    pub fn new(
        date: time::Date,
        instance: u128,
        clock: Arc<Clock>,
        root: Arc<Owner>,
    ) -> Result<Self, String> {
        Self::with_diagnostics(date, instance, clock, root, Arc::default())
    }
    pub(crate) fn with_diagnostics(
        date: time::Date,
        instance: u128,
        clock: Arc<Clock>,
        root: Arc<Owner>,
        diagnostics: DiagnosticSink,
    ) -> Result<Self, String> {
        crate::latest::Store::<()>::new(date, instance, 1)
            .map_err(|error| format!("Invalid options date/generation: {error:?}"))?;
        let runtime = tokio::runtime::Handle::try_current()
            .map_err(|_| "Options supervisor requires its Tokio runtime")?;
        let parent = root
            .child()
            .map_err(|error| format!("Options owner admission failed: {error:?}"))?;
        let stopping = parent.cancellation();
        let stop = stopping.clone();
        let (commands, mut incoming) = mpsc::channel::<Command>(16);
        let (finished, done) = watch::channel(false);
        let generation = rand::random::<u128>().max(1);
        let state = Arc::new(Mutex::new(State {
            claims: Some(
                super::claims::Claims::new(generation)
                    .map_err(|e| format!("Native scope claim setup: {e:?}"))?,
            ),
            ..State::default()
        }));
        let owned = state.clone();
        let claims_changed = Arc::new(tokio::sync::Notify::new());
        let claims_wake = claims_changed.clone();
        let sink = diagnostics.clone();
        let exit = SupervisorExit {
            stopping: stop.clone(),
            state: owned.clone(),
            root: root.clone(),
            clean: false,
        };
        let task=runtime.spawn(async move {
            let mut exit=exit;
            loop {
                // Expiry and provider realization share one native owner. Renewal
                // only takes the short state lock and never queues behind I/O.
                let next=match Self::expire_claims(&owned){Ok(next)=>next,Err(error)=>{if let Ok(mut state)=owned.lock(){state.failure=Some(error);}break;}};
                let retired=match owned.lock(){Ok(mut state)=>std::mem::take(&mut state.pending_retire),Err(_)=>break};
                let cleanup_token=CancellationToken::new();
                let mut failed=None;
                for (id,cleanup) in retired {
                    let result=tokio::select!{biased; ()=stop.cancelled()=>Err("Options supervisor stopped".into()),
                        result=cleanup.run(&cleanup_token,Self::execute(Action::Remove(id),cleanup,date,instance,&clock,&root,&parent,&owned,&sink))=>result.map_err(|e|format!("Native expiry cleanup: {e:?}")).and_then(|r|r)};
                    if let Err(error)=result{failed=Some(error);break;}
                }
                if let Some(error)=failed {if let Ok(mut state)=owned.lock(){state.failure=Some(error);}let _=root.signal(1 << (Lane::OptionsGlbx as u32));break;}
                let command=tokio::select!{biased; ()=stop.cancelled()=>break,
                    ()=claims_wake.notified()=>continue,
                    ()=async{match next{Some(at)=>tokio::time::sleep_until(at).await,None=>std::future::pending().await}}=>continue,
                    command=incoming.recv()=>match command{Some(command)=>command,None=>break}};
                let Command{action,deadline,mut reply}=command;
                if reply.is_closed(){continue;}
                let operation=CancellationToken::new();
                let executing=deadline.run(&operation,Self::execute(action,deadline,date,instance,&clock,&root,&parent,&owned,&sink));
                tokio::pin!(executing);
                let result=loop {
                    // Pending provider I/O cannot prevent independent native
                    // expiry/release from cancelling another scope immediately.
                    let next=match Self::expire_claims(&owned){Ok(next)=>next,Err(error)=>break Err(error)};
                    tokio::select!{biased;
                        ()=stop.cancelled()=>break Err("Options supervisor stopped".into()),
                        ()=reply.closed()=>break Err("Options scope caller cancelled".into()),
                        ()=claims_wake.notified()=>continue,
                        ()=async{match next{Some(at)=>tokio::time::sleep_until(at).await,None=>std::future::pending().await}}=>continue,
                        result=&mut executing=>break result.map_err(|error|format!("Options scope original deadline/cancellation: {error:?}")).and_then(|value|value),
                    }
                };
                let _=reply.send(result);
            }
            stop.cancel();
            // No second timeout is introduced. The external operation observes
            // its original deadline while this owner retains cleanup memory.
            parent.join_nested().await;
            if let Ok(mut state)=owned.lock(){state.scopes.clear();}
            drop(parent);
            finished.send_replace(true);
            exit.clean=true;
        });
        Ok(Self {
            commands,
            stopping,
            state,
            done,
            task: Mutex::new(Some(task)),
            generation,
            diagnostics,
            claims_changed,
        })
    }
    fn expire_claims(owned: &Arc<Mutex<State>>) -> Result<Option<tokio::time::Instant>, String> {
        let mut state = owned
            .lock()
            .map_err(|_| "Options scope registry interrupted")?;
        let (retired, next) = state
            .claims
            .as_mut()
            .map(|claims| {
                (
                    claims.retire(tokio::time::Instant::now()),
                    claims.next_expiration(),
                )
            })
            .unwrap_or_default();
        for id in retired {
            state
                .pending_retire
                .entry(id.clone())
                .or_insert_with(Deadline::recovery);
            if let Some(scope) = state.scopes.get_mut(&id) {
                scope.retiring = true;
                scope.data.cancel_lane(Lane::OptionsGlbx)?;
                if let Some(pricing) = &scope.pricing {
                    pricing.cancel();
                }
                if let Some(references) = &scope.references {
                    references.cancel();
                }
            }
        }
        Ok(next)
    }
    /// Imports complete durable or temporary native scope intent after scope
    /// preparation. This synchronous in-memory command never waits for provider
    /// setup, broker publication, a managed semaphore or a storage acknowledgement.
    ///
    /// # Errors
    /// Rejects stopped/retiring/unqualified scopes and bounded claim failures.
    pub fn retain_scopes(
        &self,
        owner: u128,
        scopes: &[String],
        lifetime: crate::subscriptions::leases::Lifetime,
    ) -> Result<crate::subscriptions::Token, String> {
        if self.stopping.is_cancelled() {
            return Err("Options supervisor stopped".into());
        }
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?;
        if scopes.iter().any(|id| {
            state.pending_retire.contains_key(id) || state.scopes.get(id).is_none_or(|s| s.retiring)
        }) {
            return Err("Native scope claim requires active qualified scopes".into());
        }
        let token = state
            .claims
            .as_mut()
            .ok_or("Native scope claims unavailable")?
            .acquire(
                self.generation,
                owner,
                scopes,
                lifetime,
                tokio::time::Instant::now(),
            )
            .map_err(|e| format!("Native scope claim: {e:?}"))?;
        self.claims_changed.notify_one();
        Ok(token)
    }
    /// Renews an exact active temporary claim independently of control I/O.
    ///
    /// # Errors
    /// Expired/stale/ended tokens cannot regain ownership.
    pub fn renew_claim(
        &self,
        token: crate::subscriptions::Token,
        ttl: std::time::Duration,
    ) -> Result<(), String> {
        if self.stopping.is_cancelled() {
            return Err("Options supervisor stopped".into());
        }
        self.state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?
            .claims
            .as_mut()
            .ok_or("Native scope claims unavailable")?
            .renew(token, ttl, tokio::time::Instant::now())
            .map_err(|e| format!("Native scope renewal: {e:?}"))?;
        self.claims_changed.notify_one();
        Ok(())
    }
    /// Releases exact native intent and wakes owned cleanup without caller waits.
    ///
    /// # Errors
    /// An interrupted registry remains an explicit failure, never healthy idle.
    pub fn release_claim(&self, token: crate::subscriptions::Token) -> Result<(), String> {
        self.state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?
            .claims
            .as_mut()
            .ok_or("Native scope claims unavailable")?
            .release(token);
        self.claims_changed.notify_one();
        Ok(())
    }
    /// Logical options generation used by application pricing contexts. Physical
    /// scope books additionally carry their own independently replaced identity.
    pub fn generation(&self) -> u128 {
        self.generation
    }
    /// Exclusive diagnostic consumer for a standalone native supervisor. A Host
    /// uses its shared sink instead. Scope release/reset never drops unread text.
    pub fn drain_diagnostics(&self, capacity: usize) -> Result<Vec<Diagnostic>, String> {
        if capacity > 64 {
            return Err("Options diagnostic batch exceeds capacity".into());
        }
        self.diagnostics.drain(capacity)
    }
    /// Signals emergency options-only retirement without waiting for any queue,
    /// lock spanning I/O, or cleanup completion. Use shutdown to obtain proof.
    pub fn request_stop(&self) {
        self.stopping.cancel();
    }
    /// True only after all physical descendants have dropped and the logical
    /// owner has released its runtime slot, not merely after cancellation.
    pub fn is_stopped(&self) -> bool {
        *self.done.borrow()
    }
    /// Terminal supervisor failure, independent of price/control queue capacity.
    /// A native lane signal wakes the Host so this cannot remain a logged-only
    /// exception. Root recovery/UI routing consumes this reliable observation.
    pub fn terminal_error(&self) -> Result<Option<String>, String> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Options supervisor state interrupted")?
            .failure
            .clone())
    }
    /// Resolves a current physical scope without I/O or allocating a full chain.
    /// Retiring scopes cannot be returned as plausible current data.
    pub fn scope(&self, id: &str) -> Result<Option<Arc<LiveData>>, String> {
        if self.stopping.is_cancelled() {
            return Ok(None);
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?;
        Ok(state
            .scopes
            .get(id)
            .filter(|scope| !scope.retiring)
            .map(|scope| scope.data.clone()))
    }
    /// Reads live native state rather than returning a cached acquisition result.
    pub fn status(&self, id: &str) -> Result<Option<Status>, String> {
        let data = self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?
            .scopes
            .get(id)
            .map(|scope| {
                (
                    scope.data.clone(),
                    scope.pricing.clone(),
                    scope.references.clone(),
                )
            });
        let Some((data, pricing, references)) = data else {
            return Ok(None);
        };
        let mut status = data.status(Lane::OptionsGlbx)?;
        if let (Some(status), Some(pricing)) = (&mut status, pricing) {
            let pricing = pricing.status()?;
            if let Some(original) = pricing.deadline {
                status.deadline = Some(
                    status
                        .deadline
                        .map_or(original, |prior| prior.earliest(original)),
                );
            }
            if pricing.recovering {
                status.phase = Phase::Recovering;
                status.error = pricing.error;
            } else if !pricing.running && pricing.error.is_some() {
                status.phase = Phase::Failed;
                status.error = pricing.error;
            }
        }
        if let (Some(status), Some(references)) = (&mut status, references) {
            let references = references.status()?;
            if references.recovering {
                status.phase = Phase::Recovering;
                status.error = references.error;
            } else if !references.running && references.error.is_some() {
                status.phase = Phase::Failed;
                status.error = references.error;
            }
        }
        Ok(status)
    }
    /// Non-acquiring immutable calculation cache handle for an active scope.
    /// No provider call, lease renewal or calculation occurs on a read.
    pub fn pricing(&self, id: &str) -> Result<Option<Arc<super::coalesced::Coalesced>>, String> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?;
        Ok(state
            .scopes
            .get(id)
            .filter(|scope| !scope.retiring)
            .and_then(|scope| scope.engine.clone()))
    }
    /// Commits a complete qualified reference refresh without reconnecting any
    /// provider or changing ownership. Both scope semantic identity and every
    /// per-option immutable context are compared before the atomic native swap.
    pub fn refresh_pricing(
        &self,
        id: &str,
        expected: &str,
        next: &str,
        updates: &[super::coalesced::Refresh],
        manifests: &BTreeMap<u32, Arc<super::manifest::Manifest>>,
    ) -> Result<(), String> {
        if next.len() != 64 || !next.bytes().all(|b| b.is_ascii_hexdigit()) || next == expected {
            return Err("Invalid pricing refresh digest".into());
        }
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?;
        let scope = state
            .scopes
            .get_mut(id)
            .filter(|scope| !scope.retiring)
            .ok_or("Options scope is unavailable for context refresh")?;
        if scope.pricing_digest.as_deref() != Some(expected) {
            return Err("Pricing scope compare-and-swap conflict".into());
        }
        let engine = scope
            .engine
            .as_ref()
            .ok_or("Options scope has no qualified pricing owner")?;
        engine.refresh_prepared(updates, manifests)?;
        scope.pricing_digest = Some(next.into());
        Ok(())
    }
    /// Retrieves native reconstruction intent for a current scope. These are
    /// copied reference/configuration values; no managed lease or callback exists.
    pub fn reviewed(
        &self,
        id: &str,
    ) -> Result<Option<Arc<super::preparation::ReviewedScope>>, String> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?
            .scopes
            .get(id)
            .and_then(|s| s.reviewed.clone()))
    }
    /// Installs a complete business/display risk union under the existing scope
    /// digest and context identities. Ownership authority stays in application
    /// policy; acknowledgement here does not claim fresh risk is already ready.
    pub fn set_risk_requirements(
        &self,
        id: &str,
        expected: &str,
        updates: &[super::coalesced::RiskRequirement],
    ) -> Result<(), String> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?;
        let scope = state
            .scopes
            .get(id)
            .filter(|scope| !scope.retiring)
            .ok_or("Options scope is unavailable for risk demand")?;
        if self.stopping.is_cancelled() || scope.pricing_digest.as_deref() != Some(expected) {
            return Err("Pricing scope compare-and-swap conflict".into());
        }
        scope
            .engine
            .as_ref()
            .ok_or("Options scope has no qualified pricing owner")?
            .set_risk_requirements(updates)?;
        Ok(())
    }
    /// Installs one physical scope, or verifies an identical existing healthy
    /// scope. Digest is the application's qualified physical-intent identity.
    pub async fn install(
        &self,
        id: &str,
        digest: &str,
        request: Start,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        self.install_inner(id, digest, request, None, token, deadline)
            .await
    }
    /// Installs the complete native pricing data path before provider ingress
    /// begins. The existing plain method remains available for transport-only
    /// qualification; a full API epoch uses this qualified variant.
    pub async fn install_priced(
        &self,
        id: &str,
        digest: &str,
        request: Start,
        configuration: PricingConfiguration,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        self.install_priced_inner(
            id,
            digest,
            request,
            configuration,
            BTreeMap::new(),
            None,
            token,
            deadline,
        )
        .await
    }
    /// Prepares complete pricing/reference inputs inside Rust before opening any
    /// provider resources. Retained blueprints allow later native-only refresh.
    ///
    /// # Errors
    /// A partial reference batch or deadline failure admits no subscriptions.
    pub async fn install_reviewed(
        &self,
        id: &str,
        digest: &str,
        request: Start,
        reviewed: Arc<super::preparation::ReviewedScope>,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        // The cold HTTP preparation future is deliberately heap-owned: nesting
        // it in the ABI command future can overflow a CLR worker's smaller stack.
        let prepared =
            Box::pin(reviewed.prepare(&request.registrations, self.generation, token, deadline))
                .await?;
        self.install_priced_inner(
            id,
            digest,
            request,
            prepared.configuration,
            prepared.references,
            Some(reviewed),
            token,
            deadline,
        )
        .await
    }
    #[allow(clippy::too_many_arguments)] // One atomic cold scope transaction.
    async fn install_priced_inner(
        &self,
        id: &str,
        digest: &str,
        request: Start,
        configuration: PricingConfiguration,
        references: BTreeMap<u32, Arc<super::context::Context>>,
        reviewed: Option<Arc<super::preparation::ReviewedScope>>,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        if configuration.digest.len() != 64
            || !configuration.digest.bytes().all(|b| b.is_ascii_hexdigit())
            || configuration.options.is_empty()
            || configuration.options.len() > 2048
            || !(10..=5000).contains(&configuration.selection_ms)
            || !(10..=60000).contains(&configuration.risk_ms)
            || configuration.iv_ms < configuration.selection_ms
            || configuration.statistics.len() > 2048
        {
            return Err("Invalid qualified pricing configuration".into());
        }
        let engine = Arc::new(super::coalesced::Coalesced::with_policy(
            configuration.options.len(),
            configuration.batch,
            configuration.iv_ms,
            &configuration.policy_version,
        )?);
        if reviewed.is_some() && references.len() != configuration.options.len() {
            return Err("Incomplete native reference batch".into());
        }
        let mut ids = std::collections::BTreeSet::new();
        for option in configuration.options {
            let generation = u128::from(option.context.qualification.generation_low)
                | (u128::from(option.context.qualification.generation_high) << 64);
            if generation != self.generation
                || !ids.insert(option.instrument)
                || !request.registrations.iter().any(|r| {
                    r.instrument == option.instrument && r.asset == crate::latest::Asset::Option
                })
                || !request.registrations.iter().any(|r| {
                    r.instrument == option.context.underlying
                        && r.asset == crate::latest::Asset::Futures
                })
            {
                return Err(
                    "Pricing context does not match the qualified physical options scope".into(),
                );
            }
            engine.register(option.instrument, 1, option.context, option.required)?;
            if let Some(reference) = references.get(&option.instrument) {
                engine.attach_reference(
                    option.instrument,
                    option.context.identity,
                    reference.clone(),
                )?;
            }
            if let Some(manifest) = option.manifest {
                engine.attach_manifest(option.instrument, option.context.identity, manifest)?;
            }
        }
        if request
            .registrations
            .iter()
            .filter(|r| r.asset == crate::latest::Asset::Option)
            .count()
            != ids.len()
        {
            return Err("Every physical option requires a qualified pricing context".into());
        }
        self.install_inner(
            id,
            digest,
            request,
            Some(PreparedPricing {
                reviewed,
                engine,
                digest: configuration.digest,
                selection_ms: configuration.selection_ms,
                risk_ms: configuration.risk_ms,
                logical: self.generation,
                statistics: configuration.statistics,
            }),
            token,
            deadline,
        )
        .await
    }
    async fn install_inner(
        &self,
        id: &str,
        digest: &str,
        request: Start,
        pricing: Option<PreparedPricing>,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        if id.trim().is_empty()
            || id.len() > 512
            || id.encode_utf16().count() > 128
            || digest.len() != 64
            || !digest.bytes().all(|byte| byte.is_ascii_hexdigit())
            || request.plan.lane != Lane::OptionsGlbx
        {
            return Err("Invalid qualified options scope identity".into());
        }
        request
            .plan
            .validate()
            .map_err(|error| format!("Invalid options physical plan: {error:?}"))?;
        if request.registrations.is_empty()
            || request.registrations.len() > 2112
            || request.key.len() != 32
            || !request.key.is_ascii()
        {
            return Err("Invalid options physical registration/credential format".into());
        }
        self.call(
            Action::Install {
                id: id.into(),
                digest: digest.into(),
                request: Box::new(request),
                pricing,
            },
            token,
            deadline,
        )
        .await
    }
    /// Replaces just this scope under the original remaining recovery budget.
    pub async fn reset(
        &self,
        id: &str,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        self.call(Action::Reset(id.into()), token, deadline).await
    }
    /// Releases just this physical scope, joining cleanup before removal.
    pub async fn remove(
        &self,
        id: &str,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        self.call(Action::Remove(id.into()), token, deadline).await
    }
    async fn call(
        &self,
        action: Action,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        if self.stopping.is_cancelled() {
            return Err("Options supervisor stopped".into());
        }
        if token.is_cancelled() {
            return Err("Options scope caller cancelled".into());
        }
        if deadline.remaining().is_zero() {
            return Err("Options scope original deadline expired".into());
        }
        let (reply, received) = oneshot::channel();
        self.commands
            .try_send(Command {
                action,
                deadline,
                reply,
            })
            .map_err(|error| match error {
                mpsc::error::TrySendError::Full(_) => "Options control capacity exhausted",
                mpsc::error::TrySendError::Closed(_) => "Options supervisor stopped",
            })?;
        deadline
            .run(token, received)
            .await
            .map_err(|error| format!("Options scope original deadline/cancellation: {error:?}"))?
            .map_err(|_| "Options supervisor exited before its result".to_owned())?
    }
    /// Cancels the complete options subtree before awaiting proof. This bypasses
    /// all pending commands and leaves sibling futures owners untouched.
    pub async fn shutdown(
        &self,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        self.stopping.cancel();
        let mut done = self.done.clone();
        deadline
            .run(token, async {
                while !*done.borrow_and_update() {
                    done.changed()
                        .await
                        .map_err(|_| "Options supervisor failed before cleanup proof")?;
                }
                Ok::<_, &str>(())
            })
            .await
            .map_err(|error| format!("Options shutdown original deadline/cancellation: {error:?}"))?
            .map_err(str::to_owned)?;
        let task = self
            .task
            .lock()
            .map_err(|_| "Options join owner interrupted")?
            .take();
        if let Some(task) = task {
            deadline
                .run(token, task)
                .await
                .map_err(|error| format!("Options shutdown join deadline: {error:?}"))?
                .map_err(|_| "Options supervisor task failed")?;
        }
        Ok(())
    }
    #[allow(clippy::too_many_arguments)] // One cold supervisor context; no hot per-tick call.
    async fn execute(
        action: Action,
        deadline: Deadline,
        date: time::Date,
        instance: u128,
        clock: &Arc<Clock>,
        root: &Arc<Owner>,
        parent: &crate::runtime::owner::Child,
        state: &Arc<Mutex<State>>,
        diagnostics: &DiagnosticSink,
    ) -> Result<(), String> {
        let id = match &action {
            Action::Install { id, .. } | Action::Reset(id) | Action::Remove(id) => id.clone(),
        };
        let existing = state
            .lock()
            .map_err(|_| "Options scope registry interrupted")?
            .scopes
            .get(&id)
            .map(|scope| {
                (
                    scope.data.clone(),
                    scope.digest.clone(),
                    scope.retiring,
                    scope.pricing.clone(),
                    scope.pricing_digest.clone(),
                    scope.references.clone(),
                )
            });
        match action {
            Action::Install {
                digest,
                request,
                pricing,
                ..
            } => {
                let request = *request;
                let intent = PhysicalIntent::new(&request);
                if let Some((data, prior, retiring, task, pricing_digest, references)) = existing {
                    if !retiring {
                        let same_intent = state
                            .lock()
                            .map_err(|_| "Options scope registry interrupted")?
                            .scopes
                            .get(&id)
                            .is_some_and(|scope| scope.intent == intent);
                        let pricing_prior = state
                            .lock()
                            .map_err(|_| "Options scope registry interrupted")?
                            .scopes
                            .get(&id)
                            .map(|scope| (scope.engine.clone(), scope.pricing_cadence));
                        let same_pricing = match (pricing_prior, pricing.as_ref()) {
                            (Some((Some(engine), cadence)), Some(pricing)) => {
                                cadence == Some((pricing.selection_ms, pricing.risk_ms))
                                    && engine.same_configuration(&pricing.engine)?
                            }
                            (Some((None, None)), None) => true,
                            _ => false,
                        };
                        if digest != prior
                            || !same_intent
                            || !same_pricing
                            || pricing_digest.as_deref()
                                != pricing.as_ref().map(|p| p.digest.as_str())
                        {
                            return Err("Conflicting physical options scope".into());
                        }
                        let phase = data.status(Lane::OptionsGlbx)?.map(|status| status.phase);
                        let calculation = task.as_ref().map(|task| task.status()).transpose()?;
                        if phase == Some(Phase::Running)
                            && calculation
                                .as_ref()
                                .is_none_or(|status| status.running && !status.recovering)
                        {
                            return Ok(());
                        }
                        if matches!(phase, Some(Phase::Starting | Phase::Recovering))
                            || calculation.is_some_and(|status| status.recovering)
                        {
                            return Err("Existing options scope recovery is still running".into());
                        }
                    }
                    if let Some(references) = references {
                        references.stop(&CancellationToken::new(), deadline).await?;
                    }
                    if let Some(task) = task {
                        task.stop(&CancellationToken::new(), deadline).await?;
                    }
                    data.stop(Lane::OptionsGlbx, &CancellationToken::new(), deadline)
                        .await?;
                    state
                        .lock()
                        .map_err(|_| "Options scope registry interrupted")?
                        .scopes
                        .remove(&id);
                }
                let members = request.registrations.len();
                let option_members = request
                    .registrations
                    .iter()
                    .filter(|r| r.asset == crate::latest::Asset::Option)
                    .count();
                let engine = pricing.as_ref().map(|pricing| pricing.engine.clone());
                let data = LiveData::scoped(
                    date,
                    instance,
                    2112,
                    clock.clone(),
                    root.clone(),
                    &id,
                    diagnostics.clone(),
                )?;
                if let Some(pricing) = &pricing {
                    for (instrument, seed) in &pricing.statistics {
                        if request.registrations.iter().any(|r| {
                            r.instrument == *instrument && r.asset == crate::latest::Asset::Option
                        }) {
                            data.seed_option_statistics(*instrument, *seed)?;
                        }
                    }
                }
                let data = Arc::new(if let Some(engine) = &engine {
                    data.with_option_pricing(engine.clone())?
                } else {
                    data
                });
                {
                    let mut state = state
                        .lock()
                        .map_err(|_| "Options scope registry interrupted")?;
                    if state.scopes.len() >= 64
                        || members == 0
                        || members > 2112
                        || state
                            .scopes
                            .values()
                            .map(|scope| scope.members - scope.option_members)
                            .sum::<usize>()
                            + members
                            - option_members
                            > 64
                        || state
                            .scopes
                            .values()
                            .map(|scope| scope.option_members)
                            .sum::<usize>()
                            + option_members
                            > 2048
                        || state
                            .scopes
                            .values()
                            .map(|scope| scope.members)
                            .sum::<usize>()
                            + members
                            > 2112
                    {
                        return Err("Options physical scope capacity exhausted".into());
                    }
                    state.scopes.insert(
                        id.clone(),
                        Scope {
                            data: data.clone(),
                            digest,
                            members,
                            option_members,
                            retiring: true,
                            intent,
                            pricing: None,
                            pricing_digest: pricing.as_ref().map(|pricing| pricing.digest.clone()),
                            engine,
                            pricing_cadence: pricing.as_ref().map(|p| (p.selection_ms, p.risk_ms)),
                            reviewed: pricing.as_ref().and_then(|p| p.reviewed.clone()),
                            references: None,
                        },
                    );
                }
                let mut pending = PendingScope {
                    id,
                    data: data.clone(),
                    state: state.clone(),
                    committed: false,
                };
                let registrations = request.registrations.clone();
                data.start_nested(request, &CancellationToken::new(), deadline, parent)
                    .await?;
                if let Some(pricing) = pricing {
                    if let Some(reviewed) = &pricing.reviewed {
                        let weak = Arc::downgrade(state);
                        let id = pending.id.clone();
                        let expected = pricing.engine.clone();
                        let references = Arc::new(super::reference_task::ReferenceTask::start(
                            reviewed.clone(),
                            registrations,
                            pricing.engine.clone(),
                            data.clone(),
                            clock.clone(),
                            pricing.logical,
                            parent,
                            move |digest| {
                                let state = weak
                                    .upgrade()
                                    .ok_or("Options scope retired during native refresh")?;
                                let mut state = state
                                    .lock()
                                    .map_err(|_| "Options scope registry interrupted")?;
                                let scope = state
                                    .scopes
                                    .get_mut(&id)
                                    .filter(|s| {
                                        !s.retiring
                                            && s.engine.as_ref().is_some_and(|engine| {
                                                Arc::ptr_eq(engine, &expected)
                                            })
                                    })
                                    .ok_or("Options scope replaced during native refresh")?;
                                scope.pricing_digest = Some(digest);
                                Ok(())
                            },
                        )?);
                        state
                            .lock()
                            .map_err(|_| "Options scope registry interrupted")?
                            .scopes
                            .get_mut(&pending.id)
                            .ok_or("Options scope retired during reference admission")?
                            .references = Some(references);
                    }
                    let task = Arc::new(super::pipeline::PricingTask::start(
                        data.clone(),
                        pricing.engine,
                        clock.clone(),
                        pricing.logical,
                        pricing.selection_ms,
                        pricing.risk_ms,
                        parent,
                    )?);
                    state
                        .lock()
                        .map_err(|_| "Options scope registry interrupted")?
                        .scopes
                        .get_mut(&pending.id)
                        .ok_or("Options pricing scope replaced during startup")?
                        .pricing = Some(task);
                }
                pending.commit()
            }
            Action::Reset(_) => {
                let Some((data, _, retiring, _, _, _)) = existing else {
                    return Err("Options scope unavailable".into());
                };
                if retiring {
                    return Err("Options scope cleanup must complete before reset".into());
                }
                if let Some(scope) = state
                    .lock()
                    .map_err(|_| "Options scope registry interrupted")?
                    .scopes
                    .get_mut(&id)
                {
                    scope.retiring = true;
                }
                let mut pending = PendingScope {
                    id,
                    data: data.clone(),
                    state: state.clone(),
                    committed: false,
                };
                data.reset(Lane::OptionsGlbx, &CancellationToken::new(), deadline)
                    .await?;
                pending.commit()
            }
            Action::Remove(_) => {
                let Some((data, _, _, pricing, _, references)) = existing else {
                    return Ok(());
                };
                if let Some(scope) = state
                    .lock()
                    .map_err(|_| "Options scope registry interrupted")?
                    .scopes
                    .get_mut(&id)
                {
                    scope.retiring = true;
                }
                if let Some(references) = references {
                    references.stop(&CancellationToken::new(), deadline).await?;
                }
                if let Some(pricing) = pricing {
                    pricing.stop(&CancellationToken::new(), deadline).await?;
                }
                data.stop(Lane::OptionsGlbx, &CancellationToken::new(), deadline)
                    .await?;
                state
                    .lock()
                    .map_err(|_| "Options scope registry interrupted")?
                    .scopes
                    .remove(&id);
                Ok(())
            }
        }
    }
}
impl Drop for Options {
    fn drop(&mut self) {
        self.stopping.cancel();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn supervisor_exit_reports_failure_and_wakes_control_delivery_independently() {
        for clean in [false, true] {
            let state = Arc::new(Mutex::new(State::default()));
            let root = Arc::new(Owner::default());
            let stopping = CancellationToken::new();
            drop(SupervisorExit {
                stopping: stopping.clone(),
                state: state.clone(),
                root: root.clone(),
                clean,
            });
            assert!(stopping.is_cancelled());
            assert_eq!(state.lock().unwrap().failure.is_some(), !clean);
            assert_eq!(
                root.take_signals().unwrap(),
                1 << (Lane::OptionsGlbx as u32)
            );
        }
    }
}
