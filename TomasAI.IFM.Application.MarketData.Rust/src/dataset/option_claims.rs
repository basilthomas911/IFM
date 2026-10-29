//! Persistent native business claims over a reviewed dataset catalog. Physical
//! rebuilds consume the derived union; they never recreate, renew or erase these
//! claims. Commands perform bounded native work, with no provider/database await.
use super::{
    options_owner::{CacheScope, Intent},
    supervisor::Supervisor,
};
use crate::{
    latest::Asset,
    option_chain::{claims::Claims, preparation::ReviewedScope},
    runtime::{
        Deadline,
        owner::{Owner, OwnerError},
        recovery::{Proof, Requirements},
    },
    subscriptions::{Token, leases::Lifetime},
};
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::{Arc, Mutex},
};
use tokio::{sync::watch, task::JoinHandle, time::Instant};
use tokio_util::sync::CancellationToken;

/// One native business scope's complete membership and publication requirements.
#[derive(Clone, PartialEq, Eq)]
pub struct Scope {
    /// Exact option instruments from the reviewed native dataset catalog.
    pub options: BTreeSet<u32>,
    /// Trade/display options requiring individual full-risk actor events.
    pub monitored: BTreeSet<u32>,
    /// Optional complete background-chain publications.
    pub caches: Vec<CacheScope>,
}
#[derive(Clone)]
struct State {
    generation: u128,
    base: Arc<Intent>,
    scopes: BTreeMap<String, Scope>,
    claims: Claims,
    revision: i64,
}
impl State {
    fn active(&self) -> BTreeSet<String> {
        self.scopes
            .keys()
            .filter(|id| self.claims.contains(id))
            .cloned()
            .collect()
    }
    fn desired(&self) -> Result<Option<Arc<Intent>>, String> {
        let mut ids = BTreeSet::new();
        let mut monitored = BTreeSet::new();
        let mut caches = BTreeMap::new();
        for (id, scope) in &self.scopes {
            if !self.claims.contains(id) {
                continue;
            }
            ids.extend(scope.options.iter().copied());
            monitored.extend(scope.monitored.iter().copied());
            for cache in &scope.caches {
                if let Some(prior) = caches.insert(cache.scope.clone(), cache.clone())
                    && prior != *cache
                {
                    return Err("Conflicting native cache scope definitions".into());
                }
            }
        }
        if ids.is_empty()
            && self
                .base
                .background
                .as_ref()
                .is_none_or(|background| !background.has_work())
        {
            return Ok(None);
        }
        if ids.len() > 2048 || caches.len() > 64 {
            return Err("Native desired options union exceeds capacity".into());
        }
        let mut definitions = self
            .base
            .reviewed
            .definitions
            .iter()
            .filter(|d| ids.contains(&d.instrument))
            .cloned()
            .collect::<Vec<_>>();
        if definitions.len() != ids.len() {
            return Err("Native desired option definition unavailable".into());
        }
        for definition in &mut definitions {
            definition.required = monitored.contains(&definition.instrument);
        }
        let mut needed = ids;
        needed.extend(definitions.iter().map(|d| d.underlying));
        if needed.len() > 2112 {
            return Err("Native underlying dependency union exceeds capacity".into());
        }
        let mut registrations = self
            .base
            .source
            .registrations
            .iter()
            .filter(|r| needed.contains(&r.instrument))
            .cloned()
            .collect::<Vec<_>>();
        if registrations.len() != needed.len() {
            return Err("Native desired dependency registration unavailable".into());
        }
        for registration in &mut registrations {
            registration.required = registration.asset == Asset::Futures
                || monitored.contains(&registration.instrument);
        }
        let symbols = registrations
            .iter()
            .map(|r| r.source.as_str())
            .collect::<BTreeSet<_>>();
        let mut source = self.base.source.clone();
        source.plan.subscriptions = source
            .plan
            .subscriptions
            .into_iter()
            .filter_map(|mut subscription| {
                subscription
                    .symbols
                    .retain(|symbol| symbols.contains(symbol.as_str()));
                (!subscription.symbols.is_empty()).then_some(subscription)
            })
            .collect();
        if self.base.background.is_none() {
            source
                .plan
                .validate()
                .map_err(|e| format!("Invalid native desired subscription union: {e:?}"))?;
        }
        source.registrations = registrations;
        let reviewed = &self.base.reviewed;
        let statistics = reviewed
            .statistics
            .iter()
            .filter(|(id, _)| needed.contains(id))
            .copied()
            .collect();
        let reviewed = Arc::new(ReviewedScope {
            manifests: reviewed.manifests.clone(),
            references: reviewed.references.clone(),
            definitions,
            production: reviewed.production,
            production_approved: reviewed.production_approved,
            review_evidence: reviewed.review_evidence.clone(),
            policy_version: reviewed.policy_version.clone(),
            selection_ms: reviewed.selection_ms,
            iv_ms: reviewed.iv_ms,
            risk_ms: reviewed.risk_ms,
            maximum_contracts: reviewed.maximum_contracts,
            batch: reviewed.batch,
            statistics,
        });
        Ok(Some(Arc::new(Intent {
            background: self.base.background.clone(),
            reference_bootstrap: self.base.reference_bootstrap.clone(),
            date: self.base.date,
            definition: self.base.definition,
            source,
            reviewed,
            monitored,
            caches: caches.into_values().collect(),
            broker: self.base.broker.clone(),
            scope: self.base.scope.clone(),
            revision: self.revision,
        })))
    }
}
struct Inner {
    state: Mutex<State>,
    supervisor: Arc<Supervisor<Intent>>,
    changed: watch::Sender<u64>,
    error: Mutex<Option<String>>,
}
impl Inner {
    fn commit(&self, old: &mut State, candidate: State) -> Result<(), String> {
        self.commit_inner(old, candidate, false)
    }
    fn commit_inner(
        &self,
        old: &mut State,
        mut candidate: State,
        maintenance: bool,
    ) -> Result<(), String> {
        if candidate.active() != old.active() || !Arc::ptr_eq(&candidate.base, &old.base) {
            candidate.revision = old
                .revision
                .checked_add(1)
                .ok_or("Native desired revision overflow")?;
            let desired = candidate.desired()?;
            let required = if desired.is_some() {
                Requirements::new(&[
                    Proof::Transport,
                    Proof::Reference,
                    Proof::Pricing,
                    Proof::Publication,
                    Proof::Persistence,
                ])
            } else {
                Requirements::new(&[])
            };
            let accepted = if maintenance {
                self.supervisor.apply_maintenance(desired, required)
            } else {
                self.supervisor.apply(desired, required)
            };
            accepted.map_err(|e| format!("Native desired control unavailable: {e:?}"))?;
        }
        *old = candidate;
        Ok(())
    }
    fn expire(&self, now: Instant) -> Result<Option<Instant>, String> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        let mut candidate = state.clone();
        candidate.claims.retire(now);
        if let Some(background) = candidate
            .base
            .background
            .as_ref()
            .map(|background| background.expire_scopes(now))
            .transpose()?
            .flatten()
        {
            let mut base = candidate.base.as_ref().clone();
            base.background = Some(Arc::new(background));
            candidate.base = Arc::new(base);
        }
        self.commit_inner(&mut state, candidate, true)?;
        Ok(state
            .claims
            .next_expiration()
            .into_iter()
            .chain(
                state
                    .base
                    .background
                    .as_ref()
                    .and_then(|background| background.next_scope_expiration()),
            )
            .min())
    }
}
/// Native lifetime authority outside the replaceable options child. One root
/// service waits for the exact next ephemeral expiry or a command notification.
/// Durable trade intent needs no heartbeat/renewal callback from C#.
pub struct Coordinator {
    inner: Arc<Inner>,
    stop: CancellationToken,
    task: tokio::sync::Mutex<Option<JoinHandle<()>>>,
}
impl Coordinator {
    /// Binds one immutable reviewed dataset catalog and the native supervisor.
    /// Commands select catalog members; individual scopes cannot override shared
    /// credentials, connection, reference provider or pricing refresh policy.
    ///
    /// # Errors
    /// Invalid/duplicate catalog membership or exhausted root service admission.
    pub fn start(
        root: &Arc<Owner>,
        supervisor: Arc<Supervisor<Intent>>,
        base: Arc<Intent>,
    ) -> Result<Self, String> {
        if base.source.plan.lane != super::transport::Lane::OptionsGlbx
            || base.reviewed.definitions.len() > 2048
            || base.source.registrations.len() > 2112
            || base.reviewed.definitions.is_empty() && base.background.is_none()
        {
            return Err("Invalid native business claim catalog".into());
        }
        let definitions = base
            .reviewed
            .definitions
            .iter()
            .map(|d| d.instrument)
            .collect::<BTreeSet<_>>();
        let instruments = base
            .source
            .registrations
            .iter()
            .map(|r| r.instrument)
            .collect::<BTreeSet<_>>();
        let contracts = base
            .source
            .registrations
            .iter()
            .map(|r| &r.contract)
            .collect::<BTreeSet<_>>();
        if definitions.len() != base.reviewed.definitions.len()
            || instruments.len() != base.source.registrations.len()
            || contracts.len() != instruments.len()
            || !definitions.is_subset(&instruments)
            || base
                .reviewed
                .definitions
                .iter()
                .any(|d| !instruments.contains(&d.underlying))
        {
            return Err("Incomplete or conflicting native business claim catalog".into());
        }
        let generation = rand::random::<u128>().max(1);
        let initial_revision = if base.background.is_some() { 1 } else { 0 };
        let (changed, mut changes) = watch::channel(0);
        let inner = Arc::new(Inner {
            state: Mutex::new(State {
                generation,
                base,
                scopes: BTreeMap::new(),
                claims: Claims::new(generation).map_err(|e| format!("{e:?}"))?,
                revision: initial_revision,
            }),
            supervisor,
            changed,
            error: Mutex::new(None),
        });
        let running = inner.clone();
        let stop = CancellationToken::new();
        let stopping = stop.clone();
        let task=root.spawn_service(move |root_stop|async move {
            loop {
                changes.borrow_and_update();
                let next=match running.expire(Instant::now()) {
                    Ok(next)=>next,Err(error)=>{
                        if let Ok(mut retained)=running.error.lock(){*retained=Some(error.clone());}
                        let _=running.supervisor.report_fault(&error,Deadline::recovery());
                        // A broken internal invariant is visible to recovery;
                        // wait for explicit control or shutdown, never busy-spin.
                        None
                    }
                };
                tokio::select!{biased;()=root_stop.cancelled()=>break,()=stopping.cancelled()=>break,
                    changed=changes.changed()=>{if changed.is_err(){break;}},
                    ()=async {match next {Some(at)=>tokio::time::sleep_until(at).await,None=>std::future::pending().await}}=>{}}
            }
        }).map_err(|e|format!("Native business claim service admission: {e:?}"))?;
        let coordinator = Self {
            inner,
            stop,
            task: tokio::sync::Mutex::new(Some(task)),
        };
        {
            let state = coordinator
                .inner
                .state
                .lock()
                .map_err(|_| "Native initial claim state interrupted")?;
            if state.base.background.is_some() {
                coordinator
                    .inner
                    .supervisor
                    .apply(
                        state.desired()?,
                        Requirements::new(&[
                            Proof::Transport,
                            Proof::Reference,
                            Proof::Pricing,
                            Proof::Publication,
                            Proof::Persistence,
                        ]),
                    )
                    .map_err(|e| format!("Native background control unavailable: {e:?}"))?;
            }
        }
        Ok(coordinator)
    }
    /// Validates ValueDate before changing terminal-notification routing. Native
    /// catalog commands serialize through the API owner; physical expiry cannot
    /// change the catalog date. Market close must explicitly release old demand.
    ///
    /// # Errors
    /// Active old-date intent or interrupted native control rejects the command.
    pub fn validate_date(&self, date: time::Date) -> Result<(), String> {
        let state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        if state.base.date != date && !state.active().is_empty() {
            return Err("Native market-close must clear old-date option claims first".into());
        }
        Ok(())
    }
    /// Replaces reviewed native catalog inputs atomically while preserving all
    /// business claims. An incomplete new catalog cannot silently drop a live leg.
    ///
    /// # Errors
    /// New-date rollover with active claims, missing/conflicting membership,
    /// invalid plans or failed desired-control admission leave the old catalog.
    pub fn replace_catalog(&self, base: Arc<Intent>) -> Result<(), String> {
        if base.background.is_none() {
            base.source
                .plan
                .validate()
                .map_err(|e| format!("Invalid native option catalog plan: {e:?}"))?;
        }
        if base.source.plan.lane != super::transport::Lane::OptionsGlbx
            || base.reviewed.definitions.len() > 2048
            || base.source.registrations.len() > 2112
            || base.reviewed.definitions.is_empty() && base.background.is_none()
        {
            return Err("Invalid native replacement option catalog".into());
        }
        let definitions = base
            .reviewed
            .definitions
            .iter()
            .map(|d| d.instrument)
            .collect::<BTreeSet<_>>();
        let instruments = base
            .source
            .registrations
            .iter()
            .map(|r| r.instrument)
            .collect::<BTreeSet<_>>();
        let contracts = base
            .source
            .registrations
            .iter()
            .map(|r| &r.contract)
            .collect::<BTreeSet<_>>();
        if definitions.len() != base.reviewed.definitions.len()
            || instruments.len() != base.source.registrations.len()
            || contracts.len() != instruments.len()
            || !definitions.is_subset(&instruments)
            || base
                .reviewed
                .definitions
                .iter()
                .any(|d| !instruments.contains(&d.underlying))
        {
            return Err("Incomplete or conflicting native replacement option catalog".into());
        }
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        if state.base.date != base.date && !state.active().is_empty() {
            return Err("Native market-close must clear old-date option claims first".into());
        }
        let mut candidate = state.clone();
        let active = state.active();
        // Old unclaimed templates may be pruned as expiry dates roll forward.
        // Active business membership must remain complete and identically named.
        let mut invalid_active = false;
        candidate.scopes.retain(|id, scope| {
            let valid = scope.options.is_subset(&definitions)
                && scope.caches.iter().all(|cache| {
                    cache.contracts.iter().all(|id| {
                        base.source
                            .registrations
                            .iter()
                            .any(|r| r.contract == *id && scope.options.contains(&r.instrument))
                    })
                });
            if !valid && active.contains(id) {
                invalid_active = true;
            }
            valid || active.contains(id)
        });
        if invalid_active {
            return Err(
                "Native replacement catalog cannot drop or rename an active business scope".into(),
            );
        }
        candidate.base = base;
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Adds raw catalog members atomically without dropping another trade's
    /// accepted subscriptions. This bounded command performs no IO or provider await.
    ///
    /// # Errors
    /// Cross-date active claims, source identity conflicts, capacity or closed control.
    pub fn merge_catalog(&self, input: Arc<Intent>) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        if state.base.date != input.date {
            if !state.active().is_empty() {
                return Err("Native market-close must clear old-date option claims first".into());
            }
            drop(state);
            return self.replace_catalog(input);
        }
        if input.source.plan.lane != super::transport::Lane::OptionsGlbx
            || input.reviewed.definitions.is_empty() && input.background.is_none()
        {
            return Err("Invalid native option catalog addition".into());
        }
        let mut definitions = state
            .base
            .reviewed
            .definitions
            .iter()
            .map(|d| (d.instrument, d.clone()))
            .collect::<BTreeMap<_, _>>();
        for definition in &input.reviewed.definitions {
            if let Some(old) = definitions.get(&definition.instrument)
                && old.blueprint.contract.contract() != definition.blueprint.contract.contract()
            {
                return Err("Conflicting native option source identity".into());
            }
            definitions.insert(definition.instrument, definition.clone());
        }
        if definitions.len() > 2048 {
            return Err("Native option catalog capacity exceeded".into());
        }
        // Background configuration is valid before any contract is known. Its
        // explicit global policy, rather than a fabricated first option, rebases
        // retained raw contracts when a previously used session is restarted.
        let global = input
            .background
            .as_ref()
            .map(|background| background.policy.clone())
            .or_else(|| {
                input.reviewed.definitions.first().map(|definition| {
                    crate::option_chain::preparation::RawPolicy {
                        publications: definition.blueprint.publications.clone(),
                        conversion: definition.blueprint.conversion.clone(),
                        maximum_lead: definition.blueprint.maximum_lead,
                    }
                })
            })
            .ok_or("Native catalog addition lacks explicit global reference policy")?;
        for definition in definitions.values_mut() {
            definition.blueprint.publications = global.publications.clone();
            definition.blueprint.conversion = global.conversion.clone();
            definition.blueprint.maximum_lead = global.maximum_lead;
        }
        let mut registrations = state
            .base
            .source
            .registrations
            .iter()
            .map(|r| (r.instrument, r.clone()))
            .collect::<BTreeMap<_, _>>();
        for registration in &input.source.registrations {
            if let Some(old) = registrations.get(&registration.instrument)
                && (old.contract != registration.contract
                    || old.publisher != registration.publisher
                    || old.asset != registration.asset)
            {
                return Err("Conflicting native catalog dependency identity".into());
            }
            registrations.insert(registration.instrument, registration.clone());
        }
        if registrations.len() > 2112
            || registrations
                .values()
                .map(|r| r.contract.as_str())
                .collect::<BTreeSet<_>>()
                .len()
                != registrations.len()
        {
            return Err(
                "Native option dependency catalog exceeds capacity or has conflicting identities"
                    .into(),
            );
        }
        if definitions.values().any(|d| {
            !registrations.contains_key(&d.instrument) || !registrations.contains_key(&d.underlying)
        }) {
            return Err("Incomplete native option catalog dependencies".into());
        }
        let mut source = input.source.clone();
        let mut schemas: Vec<super::transport::SubscriptionSpec> = Vec::new();
        for spec in state
            .base
            .source
            .plan
            .subscriptions
            .iter()
            .chain(&input.source.plan.subscriptions)
        {
            if !schemas.iter().any(|old| {
                old.schema == spec.schema
                    && old.symbology == spec.symbology
                    && old.replay_start_ns == spec.replay_start_ns
            }) {
                schemas.push(spec.clone());
            }
        }
        let symbols = registrations
            .values()
            .map(|r| r.source.clone())
            .collect::<Vec<_>>();
        source.plan.subscriptions = schemas
            .into_iter()
            .map(|mut spec| {
                spec.symbols = symbols.clone();
                spec
            })
            .collect();
        if input.background.is_none() && state.base.background.is_none()
            || !source.plan.subscriptions.is_empty()
        {
            source
                .plan
                .validate()
                .map_err(|e| format!("Invalid merged native option plan: {e:?}"))?;
        }
        source.registrations = registrations.into_values().collect();
        let mut statistics = state
            .base
            .reviewed
            .statistics
            .iter()
            .copied()
            .collect::<BTreeMap<_, _>>();
        statistics.extend(input.reviewed.statistics.iter().copied());
        let reviewed = &input.reviewed;
        let reviewed = Arc::new(ReviewedScope {
            manifests: reviewed.manifests.clone(),
            references: reviewed.references.clone(),
            definitions: definitions.into_values().collect(),
            production: reviewed.production,
            production_approved: reviewed.production_approved,
            review_evidence: reviewed.review_evidence.clone(),
            policy_version: reviewed.policy_version.clone(),
            selection_ms: reviewed.selection_ms,
            iv_ms: reviewed.iv_ms,
            risk_ms: reviewed.risk_ms,
            maximum_contracts: reviewed.maximum_contracts,
            batch: reviewed.batch,
            statistics: statistics.into_iter().collect(),
        });
        if let (Some(next), Some(old)) =
            (&input.reference_bootstrap, &state.base.reference_bootstrap)
        {
            next.inherit_models(old)?;
        }
        let mut candidate = state.clone();
        candidate.base = Arc::new(Intent {
            background: input
                .background
                .as_ref()
                .map(|background| {
                    state
                        .base
                        .background
                        .as_ref()
                        .map_or_else(
                            || Ok(background.as_ref().clone()),
                            |previous| background.inherit_facts(previous),
                        )
                        .map(Arc::new)
                })
                .transpose()?
                .or_else(|| {
                    state.base.background.as_ref().map(|background| {
                        input.reviewed.definitions.first().map_or_else(
                            || background.clone(),
                            |d| {
                                Arc::new(background.rebase(
                                    crate::option_chain::preparation::RawPolicy {
                                        publications: d.blueprint.publications.clone(),
                                        conversion: d.blueprint.conversion.clone(),
                                        maximum_lead: d.blueprint.maximum_lead,
                                    },
                                ))
                            },
                        )
                    })
                }),
            reference_bootstrap: input.reference_bootstrap.clone(),
            date: input.date,
            definition: input.definition,
            source,
            reviewed,
            monitored: Default::default(),
            caches: vec![],
            broker: input.broker.clone(),
            scope: input.scope.clone(),
            revision: input.revision,
        });
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Registers a bounded complete scope before claiming it. Active membership
    /// cannot change beneath another owner; release/reconfigure is explicit.
    ///
    /// # Errors
    /// Unknown inputs, conflicting/oversized membership or active replacement.
    pub fn configure(&self, id: &str, scope: Scope) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        Self::configure_state(&mut state, id, scope)
    }
    /// Accepts complete scope membership and its lifetime as one native mutation.
    /// Validation, capacity and union failures leave both membership and demand
    /// unchanged. A managed caller never has to compensate a half-created scope.
    ///
    /// # Errors
    /// Unknown metadata, active membership replacement, invalid lifetime or limits.
    pub fn configure_and_acquire(
        &self,
        owner: u128,
        id: &str,
        scope: Scope,
        lifetime: Lifetime,
    ) -> Result<Token, String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        let mut candidate = state.clone();
        let now = Instant::now();
        candidate.claims.retire(now);
        Self::configure_state(&mut candidate, id, scope)?;
        let token = candidate
            .claims
            .acquire(candidate.generation, owner, &[id.into()], lifetime, now)
            .map_err(|error| format!("Native scope claim rejected: {error:?}"))?;
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(token)
    }
    fn configure_state(state: &mut State, id: &str, scope: Scope) -> Result<(), String> {
        if id.trim().is_empty()
            || id.encode_utf16().count() > 128
            || scope.options.is_empty()
            || scope.options.len() > 2048
            || !scope.monitored.is_subset(&scope.options)
            || scope.caches.len() > 64
        {
            return Err("Invalid native business scope".into());
        }
        if scope.options.iter().any(|id| {
            !state
                .base
                .reviewed
                .definitions
                .iter()
                .any(|d| d.instrument == *id)
        }) {
            return Err("Native business scope contains unknown option".into());
        }
        let contracts = state
            .base
            .source
            .registrations
            .iter()
            .filter(|r| scope.options.contains(&r.instrument))
            .map(|r| r.contract.clone())
            .collect::<BTreeSet<_>>();
        if scope.caches.iter().any(|c| {
            c.scope.trim().is_empty()
                || c.scope.encode_utf16().count() > 128
                || c.contracts.is_empty()
                || !c.contracts.is_subset(&contracts)
                || !matches!(c.horizon.as_str(), "Daily" | "Weekly" | "Monthly")
        }) {
            return Err("Invalid native background cache scope".into());
        }
        if let Some(prior) = state.scopes.get(id) {
            if *prior == scope {
                return Ok(());
            }
            if state.claims.contains(id) {
                return Err("Active native scope must be released before reconfiguration".into());
            }
        } else if state.scopes.len() == 64 {
            let unused = state
                .scopes
                .keys()
                .find(|id| !state.claims.contains(id))
                .cloned()
                .ok_or("Native business scope capacity exceeded")?;
            state.scopes.remove(&unused);
        }
        state.scopes.insert(id.into(), scope);
        Ok(())
    }
    /// Atomically configures and claims one monitored option. The stable owner
    /// and membership are native business intent, independent of physical resets.
    /// No provider, database or broker await occurs while accepting this command.
    ///
    /// # Errors
    /// Unknown metadata, conflicting membership or exhausted bounded capacity
    /// leave every previously accepted claim unchanged.
    pub fn monitor(&self, owner: u128, id: &str, instrument: u32) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        if owner == 0
            || id.is_empty()
            || id.len() > 128
            || !state
                .base
                .reviewed
                .definitions
                .iter()
                .any(|d| d.instrument == instrument)
        {
            return Err("Invalid native monitored option intent".into());
        }
        let scope = Scope {
            options: BTreeSet::from([instrument]),
            monitored: BTreeSet::from([instrument]),
            caches: vec![],
        };
        let mut candidate = state.clone();
        if let Some(previous) = candidate.scopes.get(id) {
            if *previous != scope {
                return Err("Conflicting native monitored option membership".into());
            }
        } else if candidate.scopes.len() == 64 {
            let unused = candidate
                .scopes
                .keys()
                .find(|key| !candidate.claims.contains(key))
                .cloned()
                .ok_or("Native business scope capacity exceeded")?;
            candidate.scopes.remove(&unused);
        }
        candidate.scopes.insert(id.into(), scope);
        let now = Instant::now();
        candidate.claims.retire(now);
        candidate
            .claims
            .acquire(
                candidate.generation,
                owner,
                &[id.into()],
                Lifetime::Durable,
                now,
            )
            .map_err(|e| format!("Native monitored claim rejected: {e:?}"))?;
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Accepts durable or native-expiring intent synchronously. The returned token
    /// owns no connection and remains valid through physical dataset replacement.
    ///
    /// # Errors
    /// A rejected union/capacity command does not partially change accepted intent.
    pub fn acquire(
        &self,
        owner: u128,
        scopes: &[String],
        lifetime: Lifetime,
    ) -> Result<Token, String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        if scopes.iter().any(|id| !state.scopes.contains_key(id)) {
            return Err("Unconfigured native business scope".into());
        }
        let mut candidate = state.clone();
        let now = Instant::now();
        candidate.claims.retire(now);
        let token = candidate
            .claims
            .acquire(candidate.generation, owner, scopes, lifetime, now)
            .map_err(|e| format!("Native claim rejected: {e:?}"))?;
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(token)
    }
    /// Explicit native command for temporary query/display owners only. Durable
    /// trade feeds do not require this and cannot expire behind a C# semaphore.
    ///
    /// # Errors
    /// Expired/stale tokens cannot revive old intent.
    pub fn renew(&self, token: Token, ttl: std::time::Duration) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        state
            .claims
            .renew(token, ttl, Instant::now())
            .map_err(|e| format!("Native claim renewal rejected: {e:?}"))?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Ends only the exact claim. Other owners' shared option dependencies and
    /// physical requirements remain in the derived union.
    ///
    /// # Errors
    /// Unexpected internal/control failure is explicit; no provider await occurs.
    pub fn release(&self, token: Token) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        let mut candidate = state.clone();
        candidate.claims.release(token);
        candidate.claims.retire(Instant::now());
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Complete copied active membership for status/catch-up only. It contains
    /// no source, pricing service, publisher or native resource owner.
    ///
    /// # Errors
    /// Interrupted business-intent state is explicit.
    pub fn active_scopes(&self) -> Result<Vec<(String, Scope)>, String> {
        let state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        Ok(state
            .scopes
            .iter()
            .filter(|(id, _)| state.claims.contains(id))
            .map(|(id, scope)| (id.clone(), scope.clone()))
            .collect())
    }
    /// Copied active scope names for diagnostics; this does not renew any claim.
    pub fn active(&self) -> Result<BTreeSet<String>, String> {
        Ok(self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?
            .active())
    }
    /// Copies a scope only for its exact unexpired native business owner.
    ///
    /// # Errors
    /// Interrupted copied ownership state.
    pub fn owned_scope(&self, owner: u128, id: &str) -> Result<Option<Scope>, String> {
        let state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        Ok(state
            .claims
            .owns(owner, id, Instant::now())
            .then(|| state.scopes.get(id).cloned())
            .flatten())
    }
    /// Disables a business owner by its stable identity. C# need not retain a
    /// native token or orchestrate physical option-lease cleanup.
    ///
    /// # Errors
    /// Internal/control failure is explicit and leaves accepted intent unchanged.
    pub fn disable(&self, owner: u128) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        let token = state.claims.token(owner);
        let background = state
            .base
            .background
            .as_ref()
            .and_then(|background| background.without_monitor(owner));
        if token.is_none() && background.is_none() {
            return Ok(());
        }
        let mut candidate = state.clone();
        if let Some(token) = token {
            candidate.claims.release(token);
        }
        if let Some(background) = background {
            let mut base = candidate.base.as_ref().clone();
            base.background = Some(Arc::new(background));
            candidate.base = Arc::new(base);
        }
        candidate.claims.retire(Instant::now());
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Requests committed facts inside native dataset control. A later business
    /// handoff fences an older in-flight read. Replacements share the original
    /// recovery deadline and cancellation never waits on storage or consumers.
    ///
    /// # Errors
    /// No committed source, interrupted state or stopped native control.
    pub fn refresh_committed(&self) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native committed command state interrupted")?;
        state
            .base
            .background
            .as_ref()
            .ok_or("Native committed source is not configured")?
            .refresh_committed()?;
        // Healthy resources watch this native fact notification. Only a
        // terminal explicit retry needs immediate physical reconstruction.
        if matches!(
            self.inner.supervisor.recovery().status().phase,
            crate::runtime::recovery::Phase::Failed | crate::runtime::recovery::Phase::Stopped
        ) {
            let mut candidate = state.clone();
            candidate.base = Arc::new(candidate.base.as_ref().clone());
            self.inner.commit(&mut state, candidate)?;
        }
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Accepts copied native view demand. Touching an unchanged view only wakes
    /// its exact expiry service; no source reset, renewal callback or recovery
    /// allowance is created. Membership changes execute beneath dataset control.
    ///
    /// # Errors
    /// Unconfigured background source, invalid bounded demand or stopped control.
    pub fn browser(
        &self,
        command: crate::option_chain::browser::Command,
        live: Option<crate::option_chain::qualification::Decimal>,
    ) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native browser command state interrupted")?;
        let background = state
            .base
            .background
            .as_ref()
            .ok_or("Native browser source is not configured")?;
        let (background, changed) = background.accept_browser(command, live)?;
        if changed {
            let mut candidate = state.clone();
            let mut base = candidate.base.as_ref().clone();
            base.background = Some(Arc::new(background));
            candidate.base = Arc::new(base);
            self.inner.commit(&mut state, candidate)?;
        }
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Explicit market-close/stop command. All business tokens are fenced; the
    /// coordinator remains available for a later explicit enable command.
    ///
    /// # Errors
    /// A failed control mutation cannot partially discard accepted intent.
    pub fn clear(&self) -> Result<(), String> {
        let mut state = self
            .inner
            .state
            .lock()
            .map_err(|_| "Native business claim state interrupted")?;
        let mut candidate = state.clone();
        if candidate.base.background.is_some() {
            let mut base = candidate.base.as_ref().clone();
            base.background = None;
            candidate.base = Arc::new(base);
        }
        candidate.generation = rand::random::<u128>().max(1);
        candidate.claims = Claims::new(candidate.generation)
            .map_err(|e| format!("Native claim clear rejected: {e:?}"))?;
        self.inner.commit(&mut state, candidate)?;
        drop(state);
        self.inner.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Native control shutdown; dataset shutdown remains independently signalled
    /// by the runtime root/supervisor, never by dropping a copied business token.
    pub fn cancel(&self) {
        self.stop.cancel();
    }
    /// Joins the native expiry service under an original caller budget.
    ///
    /// # Errors
    /// Pending timeouts retain ownership; completed failed handles are consumed.
    pub async fn join(&self, deadline: Deadline) -> Result<(), OwnerError> {
        let mut owned = deadline
            .run(&CancellationToken::new(), self.task.lock())
            .await
            .map_err(|_| OwnerError::InternalFailure)?;
        if let Some(task) = owned.as_mut() {
            let result = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| OwnerError::InternalFailure)?;
            owned.take();
            result.map_err(|_| OwnerError::InternalFailure)?;
        }
        Ok(())
    }
}
