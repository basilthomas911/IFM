//! Native options resource group. Preparation, source capture, reference refresh,
//! pricing, bounded evidence handoff and direct actor publication live below one
//! cancellable dataset child. No managed lease, renewal or streaming drain exists.
use super::{
    live::{Diagnostics, Evidence, LiveData, Queries, Registration, Start},
    supervisor::{Factory, Readiness, Resources},
    transport::Lane,
};
use crate::messaging::diagnostic_delivery;
use crate::{
    latest::Asset,
    messaging::{
        cache_delivery, codec,
        current::Current,
        events::Identity,
        evidence_delivery,
        evidence_outbox::Outbox,
        option_cache::Publication as CachePublication,
        option_ingress::Ingress,
        options::{Options, Revisions},
        ordered::{Header, Ordered},
        publisher::{BrokerSettings, Publisher},
    },
    option_chain::{
        coalesced::Coalesced,
        composition::{capture, source},
        pipeline::PricingTask,
        preparation::ReviewedScope,
        reference_task::ReferenceTask,
    },
    runtime::{Deadline, clock::Clock, owner::Child, recovery::Proof},
};
use futures_util::future::BoxFuture;
use std::{
    collections::BTreeSet,
    sync::{Arc, Mutex},
};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;

// Diagnose unavailable output only when the existing availability watchdog fires.
// This copies bounded scalar evidence; it changes neither quote qualification nor
// the recovery deadline and never retains a provider or calculation task.
/// Business-data diagnostics only; absence never establishes a dead provider.
pub fn risk_unavailable_detail(
    value: Option<&crate::option_chain::coalesced::Calculation>,
    context: Option<&crate::option_chain::context::Context>,
    published: bool,
    at: i64,
) -> String {
    let calculation = value.map_or_else(
        || "calculation=absent".to_string(),
        |v| {
            format!(
                "calculation_failure={:?}; risk_present={}; option_age_ms={}; underlying_age_ms={}",
                v.failure,
                v.risk.is_some(),
                (i128::from(at) - i128::from(v.option.event)) / 10_000,
                (i128::from(at) - i128::from(v.underlying.event)) / 10_000,
            )
        },
    );
    format!(
        "{calculation}; context_present={}; quote_limit_ms={:?}; context_remaining_ms={:?}; publication_seen={published}",
        context.is_some(),
        context.map(|c| c.quote_limits().0),
        context.map(|c| (i128::from(c.valid_until()) - i128::from(at)) / 10_000),
    )
}
// Wake on actual result expiry, without a polling task or a new failure timeout.
// Controlled business clocks may remain frozen; recovery still uses monotonic time.
fn next_validity_wakeup(next: &mut Option<tokio::time::Instant>, at: i64, until: i64) {
    if let Ok(ticks) = u64::try_from(until.saturating_sub(at)) {
        let expiry = tokio::time::Instant::now()
            + std::time::Duration::from_nanos(ticks.saturating_mul(100).max(100));
        *next = Some(next.map_or(expiry, |prior| prior.min(expiry)));
    }
}
// Cache construction can race the first quote/reference calculation after a
// successful subscription rebuild. These typed financial-input results are not
// provider/component failures. Retry only on existing source/calculation events;
// malformed snapshots, changed generations and unknown errors still escalate.
fn cache_waits_for_market_data(code: &str) -> bool {
    matches!(code, "StaleData" | "QuoteUnavailable" | "UnderlyingQuoteUnavailable"
        | "PricingContextUnavailable" | "SelectionCalculationUnavailable"
        | "InvalidQuote" | "IncoherentQuotes")
}
// No eligible spread is a valid business result only when the complete captured
// scope was actually valued. Missing/invalid inputs remain unavailable and are
// not connection failure; they must never be converted to an empty success.
fn qualified_empty_scope(
    raw: &crate::option_chain::composition::model::Snapshot,
    ranking: &crate::option_chain::strategy::Ranking,
    at: i64,
) -> Result<Option<crate::option_chain::composition::model::Snapshot>, &'static str> {
    if raw.instruments.is_empty() {
        return Ok(None);
    }
    let policy = &ranking.bias()?.policy;
    let mut until = raw.until;
    let mut first = i64::MAX;
    let mut last = i64::MIN;
    for row in &raw.instruments {
        if row.valuation.is_none() || row.instrument.unavailable.is_some() {
            return Ok(None);
        }
        let (Some(context), Some(option), Some(underlying)) = (
            row.instrument.context.as_ref(),
            row.instrument.quote.as_ref(),
            row.instrument.underlying.as_ref(),
        ) else {
            return Ok(None);
        };
        let age = i64::from(context.quote_limits().0).min(i64::from(policy.age)) * 10_000;
        until = until.min(context.valid_until());
        for quote in [option, underlying] {
            first = first.min(quote.quote.event);
            last = last.max(quote.quote.event);
            until = until
                .min(quote.quote.event.saturating_add(age))
                .min(quote.quote.received.saturating_add(age));
        }
    }
    if until <= at || i128::from(last) - i128::from(first) > i128::from(policy.skew) * 10_000 {
        return Ok(None);
    }
    let mut empty = raw.clone();
    empty.instruments.clear();
    empty.until = until;
    empty.ranking = Some(ranking.clone());
    empty.seal()?;
    Ok(Some(empty))
}
/// One accepted native background prepared-chain publication requirement.
/// Business membership survives physical resource replacements in the intent.
#[derive(Clone, PartialEq, Eq)]
pub struct CacheScope {
    /// Optional complete strategy policy and selected direction, owned by Rust.
    pub ranking: Option<crate::option_chain::strategy::Ranking>,
    /// Existing snapshot/lookup scope identity.
    pub scope: String,
    /// Reviewed Daily/Weekly/Monthly horizon.
    pub horizon: String,
    /// Exact complete contract membership, independent of monitored trade legs.
    pub contracts: BTreeSet<String>,
}
/// Complete merged native desired options configuration. Logical business claims
/// are kept by the native command owner outside this replaceable resource group.
#[derive(Clone)]
pub struct Intent {
    /// Optional native discovery/coverage configuration, retained outside the
    /// physical dataset and resolved beneath each replacement's resource parent.
    pub background: Option<Arc<super::background::Configuration>>,
    /// Optional raw database fact lookup owned and cancelled by this dataset.
    pub reference_bootstrap: Option<Arc<crate::option_chain::reference_bootstrap::Bootstrap>>,
    /// Authoritative API trading date.
    pub date: time::Date,
    /// Definition snapshot date selected by the native reference/catalog owner.
    pub definition: time::Date,
    /// Complete dataset subscriptions and verified canonical identities.
    pub source: Start,
    /// Native raw reconstruction inputs and native HTTP reference service.
    pub reviewed: Arc<ReviewedScope>,
    /// Individually monitored option instruments; candidates do not emit these events.
    pub monitored: BTreeSet<u32>,
    /// Background chains published by Rust, without a managed cache event loop.
    pub caches: Vec<CacheScope>,
    /// Explicit native broker authentication, DNS and TLS configuration.
    pub broker: BrokerSettings,
    /// Existing deployment subject scope.
    pub scope: String,
    /// Positive desired-manifest revision.
    pub revision: i64,
}
/// Copied query directory for a current physical dataset. Numerical cache and
/// immutable context graphs contain no dataset child, source socket or task.
pub struct View {
    /// Current native logical dataset identity.
    pub header: Header,
    /// Latest copied actor frames.
    pub current: Arc<Current>,
    /// Native calculated selection/risk and immutable provenance.
    pub pricing: Arc<Coalesced>,
    /// Copied-byte authoritative handoff, not a provider resource.
    pub evidence: Arc<Outbox>,
    /// Read-only numeric queries; contain no physical source ownership.
    pub queries: Queries,
    /// Complete copied identity mapping for this logical dataset.
    pub registrations: Arc<Vec<Registration>>,
    /// Immutable reconstruction metadata, without its acquisition services.
    pub definitions: Arc<Vec<crate::option_chain::preparation::Definition>>,
    /// Reviewed numerical policy provenance.
    pub policy_version: String,
    /// Reviewed selection cadence, copied from the native intent.
    pub iv_ms: u32,
    /// Current immutable source configuration identity, without provider handles.
    pub configuration: Arc<Mutex<String>>,
    cancellation: CancellationToken,
    /// Latest detached cache publications for command/query catch-up.
    pub caches: Arc<Mutex<std::collections::BTreeMap<String, Arc<CachePublication>>>>,
    /// Complete desired lookup membership, including explicitly known no-candidate scopes.
    pub cache_scopes: Arc<Vec<CacheScope>>,
    /// Fully resolved native monitored membership for this generation.
    pub monitored: Arc<BTreeSet<u32>>,
}
impl View {
    /// One-way cancellation observation; no cancellation authority is exported.
    /// The signal itself cannot retain a dataset child or any physical resource.
    pub async fn cancelled(&self) {
        self.cancellation.cancelled().await;
    }
}
/// Native options factory; up to four retired outboxes are retained as copied
/// data with explicit eviction accounting, never replayed into a replacement.
/// The active outbox is bounded to 16 MiB; retired directories total at most 64 MiB.
pub struct OptionsFactory {
    clock: Arc<Clock>,
    view: Arc<Mutex<Option<Arc<View>>>>,
    retired: Arc<Mutex<crate::messaging::retired_evidence::Archive>>,
    diagnostics: Arc<Diagnostics>,
    identity: Mutex<Option<Header>>,
    statistics: Arc<Mutex<LogicalStatistics>>,
    // Detached qualified source recipe only. Never retain View/LiveData/tasks.
    realized: Arc<Mutex<Option<Recipe>>>,
}
// Weak desired identity fences every command change, even if a caller reuses a
// manifest revision. The resolved copy has no background owner or physical data.
struct Recipe {
    desired: std::sync::Weak<Intent>,
    source: Arc<Intent>,
    physical: Option<[u8;32]>,
}
impl Recipe {
    fn matches(&self, desired: &Intent) -> bool {
        let physical=desired.background.as_ref().map(|background|background.committed_book())
            .transpose().and_then(|book|book.flatten().map(|book|book.physical_signature()).transpose());
        self.desired.upgrade().is_some_and(|old| std::ptr::eq(old.as_ref(), desired))
            && self.source.date == desired.date && self.source.revision == desired.revision
            && physical.ok()==Some(self.physical)
    }
}
impl OptionsFactory {
    /// Binds authoritative API time without owning a managed callback.
    pub fn new(clock: Arc<Clock>) -> Self {
        Self {
            clock,
            view: Arc::new(Mutex::new(None)),
            retired: Arc::default(),
            diagnostics: Arc::default(),
            identity: Mutex::new(None),
            statistics: Arc::default(),
            realized: Arc::default(),
        }
    }
    /// Current copied/native numerical query data; no physical owner escapes.
    pub fn view(&self) -> Option<Arc<View>> {
        self.view.lock().ok().and_then(|v| v.clone())
    }
    /// Actual copied native publication identity, including setup/recovery.
    /// The directory owns no provider, task, socket or native lease.
    pub fn identity(&self) -> Option<Header> {
        self.identity
            .lock()
            .ok()
            .and_then(|identity| identity.clone())
    }
    /// Native copied-data retention/eviction counters. A successful rebuild does
    /// not hide stale evidence loss at the bounded archive boundary.
    ///
    /// # Errors
    /// Interrupted archive control is explicit.
    pub fn retired_evidence_status(
        &self,
    ) -> Result<(usize, crate::messaging::retired_evidence::Accounting), String> {
        Ok(self
            .retired
            .lock()
            .map_err(|_| "Native retired evidence accounting interrupted")?
            .status())
    }
    /// Last retired copied evidence. Old generations are never relabelled or
    /// replayed ahead of current prices after emergency dataset reconstruction.
    pub fn retired_evidence(&self) -> Option<Arc<Outbox>> {
        self.retired.lock().ok().and_then(|v| v.latest())
    }
}
#[derive(Default)]
struct LogicalStatistics {
    date: Option<time::Date>,
    identities: std::collections::BTreeMap<u32, (u16, String)>,
    values: crate::option_chain::statistics::Bank,
}
struct Group {
    parent: Arc<Child>,
    task: Option<JoinHandle<()>>,
    health: watch::Receiver<Readiness>,
    retirement: Arc<Mutex<Option<Deadline>>>,
}
struct Run {
    parent: Arc<Child>,
    intent: Arc<Intent>,
    clock: Arc<Clock>,
    directory: Arc<Mutex<Option<Arc<View>>>>,
    health: watch::Sender<Readiness>,
    retirement: Arc<Mutex<Option<Deadline>>>,
    initial: Deadline,
    diagnostics: Arc<Diagnostics>,
    worker: [u8; 16],
    generation: [u8; 16],
    statistics: Arc<Mutex<LogicalStatistics>>,
    realized: Arc<Mutex<Option<Recipe>>>,
}
impl LogicalStatistics {
    fn retain(
        &mut self,
        date: time::Date,
        desired: std::collections::BTreeMap<u32, (u16, String)>,
    ) -> Result<crate::option_chain::statistics::Bank, String> {
        if desired.len() > 2048 {
            return Err("Native logical statistics capacity exceeded".into());
        }
        if self.date != Some(date) {
            *self = Self {
                date: Some(date),
                ..Default::default()
            };
        }
        self.values
            .lock()
            .map_err(|_| "Native logical statistics interrupted")?
            .retain(|instrument, _| {
                desired
                    .get(instrument)
                    .is_some_and(|identity| self.identities.get(instrument) == Some(identity))
            });
        self.identities = desired;
        Ok(self.values.clone())
    }
}
impl Factory for OptionsFactory {
    fn audit_workloads(&self, intent: &Self::Intent) -> Vec<String> {
        let retained = self.realized.lock().ok();
        let intent = retained.as_ref().and_then(|known| known.as_ref())
            .filter(|known| known.matches(intent)).map_or(intent, |known| known.source.as_ref());
        let mut scopes: Vec<_> = intent
            .monitored
            .iter()
            .take(255)
            .map(|id| format!("option-instrument/{id}"))
            .collect();
        scopes.push(format!(
            "background-chains/{};monitored-total/{}",
            intent.caches.len(),
            intent.monitored.len()
        ));
        if let Some(background) = &intent.background {
            for monitor in background.monitors().into_iter().take(255 - scopes.len().min(255)) {
                scopes.push(format!("monitor/{}/{}", monitor.scope, monitor.contract));
            }
        }
        scopes
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
        if intent.source.plan.lane != Lane::OptionsGlbx
            || intent.revision <= 0
            || intent.source.registrations.len() > 2112
            || intent.monitored.len() > 2048
            || intent.caches.len() > 64
        {
            return Err("Invalid native options desired configuration".into());
        }
        if intent.background.is_none() {
            intent
                .source
                .plan
                .validate()
                .map_err(|e| format!("{e:?}"))?;
        }
        if intent.monitored.iter().any(|id| {
            !intent
                .source
                .registrations
                .iter()
                .any(|r| r.instrument == *id && r.asset == Asset::Option)
        }) {
            return Err("Unknown individually monitored native option".into());
        }
        let mut scopes = BTreeSet::new();
        for cache in &intent.caches {
            if let Some(ranking) = &cache.ranking {
                ranking.bias()?;
                if ranking.date != intent.date {
                    return Err("Strategy cache ValueDate differs from dataset intent".into());
                }
            }
            if cache.scope.trim().is_empty()
                || cache.scope.encode_utf16().count() > 128
                || !scopes.insert(cache.scope.clone())
                || !matches!(cache.horizon.as_str(), "Daily" | "Weekly" | "Monthly")
                || cache.contracts.is_empty()
                || cache.contracts.len() > 2048
                || cache.contracts.iter().any(|id| {
                    !intent
                        .source
                        .registrations
                        .iter()
                        .any(|r| r.asset == Asset::Option && r.contract == *id)
                })
            {
                return Err("Invalid native prepared-cache membership".into());
            }
        }
        if intent.monitored.iter().any(|id| {
            !intent
                .reviewed
                .definitions
                .iter()
                .any(|d| d.instrument == *id && d.required)
        }) {
            return Err("Individually monitored options require native full-risk demand".into());
        }
        let reset_window_iv = self
            .identity()
            .is_some_and(|previous| previous.revision == intent.revision);
        let previous = self
            .view
            .lock()
            .map_err(|_| "Native options query directory interrupted")?
            .take();
        if let Some(previous) = previous
            && previous.evidence.retained_bytes()? != 0
        {
            self.retired
                .lock()
                .map_err(|_| "Native options retired evidence directory interrupted")?
                .retain(previous.evidence.clone())?;
        }
        let (health, observed) = watch::channel(Readiness::default());
        let retirement = Arc::new(Mutex::new(None));
        let worker = rand::random::<u128>().max(1).to_le_bytes();
        let generation = rand::random::<u128>().max(1).to_le_bytes();
        if let Some(background) = &intent.background {
            background.bind_browser_generation(generation, reset_window_iv)?;
            background.bind_setup_diagnostics(&self.diagnostics, worker, generation, intent.revision as u64)?;
        }

        *self
            .identity
            .lock()
            .map_err(|_| "Native options identity directory interrupted")? = Some(Header {
            lane: Lane::OptionsGlbx,
            value_date: intent.date.to_julian_day() - 1721426,
            worker,
            generation,
            revision: intent.revision,
        });
        let run = Run {
            parent: parent.clone(),
            intent,
            clock: self.clock.clone(),
            directory: self.view.clone(),
            health,
            retirement: retirement.clone(),
            initial: deadline,
            diagnostics: self.diagnostics.clone(),
            statistics: self.statistics.clone(),
            realized: self.realized.clone(),
            worker,
            generation,
        };
        let task = parent
            .spawn_joined(move |stop| Box::pin(run.execute(stop)))
            .map_err(|e| format!("Native options group admission failed: {e:?}"))?;
        Ok(Box::new(Group {
            parent,
            task: Some(task),
            health: observed,
            retirement,
        }))
    }
}
impl Run {
    fn stage(&self, current: &mut String, message: &str) -> Result<(), String> {
        *current = message.to_owned();
        self.diagnostics.retain(super::live::Diagnostic {
            worker: self.worker,
            lane: Lane::OptionsGlbx,
            attempt: self.intent.revision as u64,
            observed: self.clock.utc_ticks().map_err(|_| "Native setup clock unavailable")?,
            replaced: 0,
            event: super::transport::Event::System {
                code: u8::MAX,
                message: format!("Native Options setup: {message}; generation={}; ValueDate={}; monitored={}; caches={}",
                    crate::messaging::wire::guid(self.generation), self.intent.date,
                    self.intent.monitored.len(), self.intent.caches.len()),
            },
        })
    }
    async fn execute(self, stop: CancellationToken) {
        let mut setup = Some(self.initial);
        // Logging starts before discovery. An unavailable sink never gates the
        // resource task; its own copied queue and cancellation remain bounded.
        let mut delivery = match diagnostic_delivery::Delivery::start(
            self.diagnostics.clone(), self.intent.broker.clone(), self.intent.scope.clone(), self.parent.clone()) {
            Ok(delivery) => delivery,
            Err(error) => {
                self.health.send_modify(|s| {s.failure=Some(error.into());s.deadline=setup;});
                return;
            }
        };
        let mut stage = String::from("starting reconstruction");
        let result = self.run(&stop, &mut setup, delivery.changes(), &mut stage).await;
        if let Err(error) = result {
            let error = format!("{error}; last native setup step: {stage}");
            // Retain the copied failure before cancellation. A replacement's
            // independent diagnostic sender can deliver it after this task exits.
            let safe = error.replace(&self.intent.source.key, "[REDACTED]");
            let _ = self.stage(&mut stage, &format!("Failed: {}", safe.chars().take(3000).collect::<String>()));
            let deadline = self
                .retirement
                .lock()
                .ok()
                .and_then(|d| *d)
                .or(setup)
                .unwrap_or_else(Deadline::recovery);
            self.health.send_modify(|s| {
                s.proofs.clear();
                s.failure.get_or_insert(error);
                s.deadline = Some(s.deadline.map_or(deadline, |old| old.earliest(deadline)));
            });
            self.parent.cancellation().cancel();
        }
        let _ = delivery.shutdown(Deadline::recovery()).await;
    }
    async fn run(
        &self,
        stop: &CancellationToken,
        setup: &mut Option<Deadline>,
        mut logs: watch::Receiver<diagnostic_delivery::Status>,
        stage: &mut String,
    ) -> Result<(), String> {
        let retained = self.realized.lock()
            .map_err(|_| "Native reconstruction recipe interrupted")?
            .as_ref().filter(|known| known.matches(&self.intent))
            .map(|known| known.source.clone());
        // Reopen the exact qualified membership before background replanning.
        // The recipe contains no quotes or physical generation; every task,
        // subscription, reference context and output is recreated below.
        let wanted = if let Some(retained) = retained {
            self.stage(stage, "Reopening retained qualified subscriptions before background discovery")?;
            retained
        } else if let Some(background) = &self.intent.background {
            self.stage(stage, "Resolving background and monitored subscription metadata")?;
            let Some(resolved) = background
                .resolve(
                    self.intent.clone(),
                    self.parent.clone(),
                    self.clock.clone(),
                    stop,
                    self.initial,
                )
                .await?
            else {
                let physical = background
                    .committed_book()?
                    .map(|book| book.physical_signature())
                    .transpose()?;
                let mut business_ready = false;
                let refresh = background.start_refresh(
                    self.intent.clone(),
                    None,
                    self.clock.clone(),
                    self.parent.clone(),
                )?;
                let mut changes = refresh.changes();
                loop {
                    if !business_ready {
                        business_ready = background
                            .acknowledge_committed(
                                self.parent.clone(),
                                stop,
                                self.initial,
                                physical,
                            )
                            .await?;
                        if business_ready {
                            self.health.send_replace(Readiness {
                                setup_complete: true,
                                proofs: vec![
                                    Proof::Transport,
                                    Proof::Reference,
                                    Proof::Pricing,
                                    Proof::Publication,
                                    Proof::Persistence,
                                ],
                                failure: None,
                                deadline: None,
                            });
                            *setup = None;
                        }
                    }
                    let state = changes.borrow_and_update().clone();
                    if let Some(error) = state.error {
                        // Pending background coverage can become ready while
                        // there is no physical option session yet. Carry its
                        // resolved recipe across the idle-to-active handoff;
                        // never restart quote acquisition from an empty snapshot.
                        if let Some(source)=state.replacement {
                            let physical=background.committed_book()?.map(|book|book.physical_signature()).transpose()?;
                            *self.realized.lock().map_err(|_|"Native reconstruction recipe interrupted")?=Some(Recipe {
                                desired:Arc::downgrade(&self.intent),
                                source:Arc::new(Intent {background:None,..(*source).clone()}),
                                physical,
                            });
                        }
                        *self
                            .retirement
                            .lock()
                            .map_err(|_| "Native options retirement interrupted")? = state.deadline;
                        return Err(error);
                    }
                    tokio::select! {biased;()=stop.cancelled()=>return Ok(()),changed=changes.changed()=>{changed.map_err(|_|"Native background refresh observation ended")?;}}
                }
            };
            resolved
        } else {
            self.intent.clone()
        };
        let initial = if self.initial.remaining().is_zero()
            && matches!(&*self.parent.provider_state().borrow(), Some(Ok(())))
        {
            Deadline::recovery()
        } else {
            self.initial
        };
        if stop.is_cancelled() {
            return Ok(());
        }
        *setup = Some(initial);
        let initial_physical = self
            .intent
            .background
            .as_ref()
            .map(|background| background.committed_book())
            .transpose()?
            .flatten()
            .map(|book| book.physical_signature())
            .transpose()?;
        let mut business_ready = false;
        let background_refresh = self
            .intent
            .background
            .as_ref()
            .map(|background| {
                background.start_refresh(
                    self.intent.clone(),
                    Some(wanted.clone()),
                    self.clock.clone(),
                    self.parent.clone(),
                )
            })
            .transpose()?;
        let mut background_changes = background_refresh.as_ref().map(|refresh| refresh.changes());
        let intent = if let Some(bootstrap) = &wanted.reference_bootstrap {
            self.stage(stage, "Loading immutable option reference recipes")?;
            let reviewed = bootstrap
                .load(
                    &wanted.reviewed,
                    wanted.date,
                    &self.clock,
                    self.parent.clone(),
                    stop,
                    initial,
                )
                .await?;
            Arc::new(Intent {
                background: None,
                reference_bootstrap: None,
                date: wanted.date,
                definition: wanted.definition,
                source: wanted.source.clone(),
                reviewed,
                monitored: wanted.monitored.clone(),
                caches: wanted.caches.clone(),
                broker: wanted.broker.clone(),
                scope: wanted.scope.clone(),
                revision: wanted.revision,
            })
        } else {
            wanted.clone()
        };
        self.stage(stage, "Preparing new-generation pricing contexts")?;
        let prepared = Box::pin(intent.reviewed.prepare(
            &intent.source.registrations,
            u128::from_le_bytes(self.generation),
            stop,
            initial,
        ))
        .await?;
        let config = prepared.configuration;
        let configuration = Arc::new(Mutex::new(config.digest.clone()));
        let engine = Arc::new(Coalesced::with_policy(
            config.options.len(),
            config.batch,
            config.iv_ms,
            &config.policy_version,
        )?);
        for option in config.options {
            engine.register(option.instrument, 1, option.context, option.required)?;
            let reference = prepared
                .references
                .get(&option.instrument)
                .ok_or("Incomplete native reference batch")?;
            engine.attach_reference(
                option.instrument,
                option.context.identity,
                reference.clone(),
            )?;
            if let Some(manifest) = option.manifest {
                engine.attach_manifest(option.instrument, option.context.identity, manifest)?;
            }
        }
        let bank = {
            let mut statistics = self
                .statistics
                .lock()
                .map_err(|_| "Native logical statistics directory interrupted")?;
            let desired = intent
                .reviewed
                .definitions
                .iter()
                .map(|definition| {
                    (
                        definition.instrument,
                        (
                            definition.blueprint.contract.publisher(),
                            definition.blueprint.contract.contract().to_owned(),
                        ),
                    )
                })
                .collect::<std::collections::BTreeMap<_, _>>();
            statistics.retain(intent.date, desired)?
        };
        let mut data = LiveData::new(
            intent.date,
            u128::from_le_bytes(self.generation),
            2112,
            self.clock.clone(),
            self.parent.owner().clone(),
        )
        .map_err(|e| format!("{e:?}"))?
        .with_option_statistics(bank)?;
        if let Some(background) = &wanted.background {
            for (id, seed) in background
                .saved_statistics(&intent, self.parent.clone(), stop, initial)
                .await?
            {
                data.seed_option_statistics(id, seed)?;
            }
        }
        for (id, seed) in config.statistics {
            data.seed_option_statistics(id, seed)?;
        }
        data = data
            .with_option_pricing(engine.clone())?
            .with_diagnostics(self.diagnostics.clone(), self.worker)?;
        let data = Arc::new(data);
        let mut source = intent.source.clone();
        if wanted.background.is_some() {
            // Reconstruct session volume and official OI using the same native
            // session boundary as futures. Replay records cannot overwrite live
            // last prices or become executable actor events.
            let replay = super::futures_bootstrap::session_start(intent.date)?;
            for subscription in &mut source.plan.subscriptions {
                if matches!(
                    subscription.schema,
                    databento::dbn::Schema::Trades | databento::dbn::Schema::Statistics
                ) {
                    subscription.replay_start_ns = Some(replay);
                }
            }
            if !source
                .plan
                .subscriptions
                .iter()
                .any(|subscription| subscription.schema == databento::dbn::Schema::Statistics)
            {
                let symbols = source
                    .registrations
                    .iter()
                    .filter(|registration| registration.asset == Asset::Option)
                    .map(|registration| registration.source.clone())
                    .collect::<Vec<_>>();
                if !symbols.is_empty() {
                    source
                        .plan
                        .subscriptions
                        .push(super::transport::SubscriptionSpec {
                            symbols,
                            schema: databento::dbn::Schema::Statistics,
                            symbology: databento::dbn::SType::InstrumentId,
                            replay_start_ns: Some(replay),
                        });
                }
            }
        }
        source.evidence = Evidence::ControlOnly;
        self.stage(stage, "Opening final option subscriptions and awaiting acknowledgements")?;
        data.start_in_parent(source, stop, initial, &self.parent)
            .await?;
        // Subscription setup is complete. Quotes do not own a recovery timer.
        *setup = None;
        let next_configuration = configuration.clone();
        let references = ReferenceTask::start(
            intent.reviewed.clone(),
            intent.source.registrations.clone(),
            engine.clone(),
            data.clone(),
            self.clock.clone(),
            u128::from_le_bytes(self.generation),
            &self.parent,
            move |digest| {
                *next_configuration
                    .lock()
                    .map_err(|_| "Native cache configuration interrupted")? = digest;
                Ok(())
            },
        )?;
        let pricing = PricingTask::start(
            data.clone(),
            engine.clone(),
            self.clock.clone(),
            u128::from_le_bytes(self.generation),
            config.selection_ms,
            config.risk_ms,
            &self.parent,
        )?;
        let mut actor = Publisher::start_owned(
            intent.broker.clone(),
            intent.scope.clone(),
            256,
            4 * 1024 * 1024,
            initial,
            &self.parent,
        )?;
        let mut cache_writer = cache_delivery::Delivery::start(
            intent.broker.clone(),
            intent.scope.clone(),
            &self.parent,
            initial,
        )?;
        let cache_port = cache_writer.port();
        let mut cache_status = cache_writer.changes();
        let cache_directory = Arc::new(Mutex::new(std::collections::BTreeMap::<
            String,
            Arc<CachePublication>,
        >::new()));
        let mut cache_version = 0i64;
        let mut cache_dirty = true;
        let mut cache_evaluated = false;
        let mut cache_cadence = crate::option_chain::cadence::Cadence::new();
        let header = Header {
            lane: Lane::OptionsGlbx,
            value_date: intent.date.to_julian_day() - 1721426,
            worker: self.worker,
            generation: self.generation,
            revision: intent.revision,
        };
        let ordered = Ordered::new(actor.port(), header.clone())?;
        let outbox = Arc::new(Outbox::new(2048, 16 * 1024 * 1024)?);
        let mut evidence = evidence_delivery::Delivery::start(
            outbox.clone(),
            evidence_delivery::Configuration {
                broker: intent.broker.clone(),
                scope: intent.scope.clone(),
                worker: self.worker,
                revision: intent.revision,
                actor: Some(ordered.clone()),
            },
            self.parent.clone(),
            initial,
        )?;
        let ingress = Ingress::new(
            intent.date,
            self.generation,
            intent.source.registrations.clone(),
        )?;
        let monitored = intent
            .source
            .registrations
            .iter()
            .filter(|r| intent.monitored.contains(&r.instrument))
            .cloned()
            .collect();
        let mut observations = Options::new(
            intent.date,
            intent.definition,
            monitored,
            Arc::new(Revisions::default()),
        )?;
        let ids: Vec<_> = observations.instruments().collect();
        *self
            .directory
            .lock()
            .map_err(|_| "Native options query directory interrupted")? = Some(Arc::new(View {
            header,
            current: ordered.current(),
            pricing: engine.clone(),
            evidence: outbox.clone(),
            queries: data
                .queries(Lane::OptionsGlbx)?
                .ok_or("Native option queries unavailable")?,
            registrations: Arc::new(intent.source.registrations.clone()),
            definitions: Arc::new(intent.reviewed.definitions.clone()),
            policy_version: intent.reviewed.policy_version.clone(),
            iv_ms: intent.reviewed.iv_ms,
            configuration: configuration.clone(),
            cancellation: self.parent.cancellation(),
            caches: cache_directory.clone(),
            cache_scopes: Arc::new(intent.caches.clone()),
            monitored: Arc::new(intent.monitored.clone()),
        }));
        *self.realized.lock().map_err(|_| "Native reconstruction recipe interrupted")? = Some(Recipe {
            // Preserve the pre-bootstrap recipe identity: feeding an already
            // substituted calendar back into its raw-fact cache can force an
            // unnecessary storage lookup during recovery.
            desired: Arc::downgrade(&self.intent),
            source: Arc::new(Intent { background: None, ..(*wanted).clone() }),
            physical: initial_physical,
        });
        self.stage(stage, "Final source installed; waiting for component readiness and current-generation market data")?;
        let mut source = data.updates();
        let mut lifecycle = data
            .lifecycle(Lane::OptionsGlbx)?
            .ok_or("Native option source status absent")?;
        let mut calculations = engine.updates();
        let mut reference_changes = engine.reference_updates();
        let mut publication = ordered.changes();
        let mut storage = evidence.changes();
        let mut persisted = outbox.changes();
        let mut clocks = self.clock.changes();
        let mut cursor = 0;
        let mut dirty = true;
        let mut threshold = 0;
        let mut published_instruments = BTreeSet::new();
        let mut qualified_threshold = None;
        let risk_workloads: Vec<_> = intent
            .reviewed
            .definitions
            .iter()
            .filter(|definition| definition.required)
            .map(|definition| {
                (
                    definition.instrument,
                    format!("option/{}", definition.instrument),
                )
            })
            .collect();
        let processed:Result<(),String>=async {
            loop {
                if stop.is_cancelled(){break;}
                let at=self.clock.utc_ticks().map_err(|_|"Native options API clock unavailable")?;
                let transport=lifecycle.borrow_and_update().clone();let broker=publication.borrow_and_update().clone();let io=storage.borrow_and_update().clone();
                let numerical=pricing.status()?;let reference=references.status()?;
                let background=background_changes.as_mut().map(|changes|changes.borrow_and_update().clone()).unwrap_or_default();
                let logging=logs.borrow_and_update().clone();
                if logging.fatal{return Err(logging.error.unwrap_or_else(||"Native diagnostic component failed".into()));}
                let cache_broker=cache_status.borrow_and_update().clone();
                // Only failed components contribute a failure deadline. A
                // pending publication may retain an old business-data budget
                // while heartbeats remain healthy; it cannot expire a new fault.
                let failure=active_failure([
                    (transport.error.clone().filter(|_|matches!(transport.phase,super::lanes::Phase::Recovering|super::lanes::Phase::Failed)),transport.deadline),
                    (numerical.error.clone(),numerical.deadline),
                    (reference.error.clone(),engine.reference_deadline()?),
                    (broker.failure.map(|text|broker.detail.clone().unwrap_or_else(||text.into())),broker.pending_since.map(Deadline::recovery_from)),
                    (io.error.clone().filter(|_|io.recovering||io.terminal),io.deadline),
                    (cache_broker.error.clone(),cache_broker.deadline),
                    (background.error,background.deadline),
                ],*setup);
                if let Some((error,deadline))=failure {
                    *self.retirement.lock().map_err(|_|"Native options retirement interrupted")?=Some(deadline);
                    if error.contains("Native background policy/coverage changed") {
                        // Refresh resolves new membership while the existing feed
                        // is still live. Carry only that copied recipe across
                        // retirement; do not reacquire its quiet underlying prices.
                        let replacement = background.replacement.clone().map(|source| {
                            let physical=self.intent.background.as_ref().map(|configuration|configuration.committed_book())
                                .transpose()?.flatten().map(|book|book.physical_signature()).transpose()?;
                            Ok::<_,String>(Recipe {
                                desired:Arc::downgrade(&self.intent),
                                source:Arc::new(Intent { background:None,..(*source).clone() }),
                                physical,
                            })
                        }).transpose()?;
                        *self.realized.lock().map_err(|_| "Native reconstruction recipe interrupted")? = replacement;
                    }
                    self.health.send_replace(Readiness{setup_complete:false,proofs:vec![],failure:Some(error.clone()),deadline:Some(deadline)});return Err(error);
                }
                if broker.stopped || io.terminal || !numerical.running || !reference.running || matches!(transport.phase,super::lanes::Phase::Failed|super::lanes::Phase::Stopped){return Err("Native options resource terminated before complete readiness".into());}
                let budget=[*setup,transport.deadline,numerical.deadline,engine.reference_deadline()?,io.deadline,broker.pending_since.map(Deadline::recovery_from),cache_broker.deadline,background.deadline].into_iter().flatten().reduce(Deadline::earliest).unwrap_or_else(Deadline::recovery);
                let mut drained=0;
                for _ in 0..64 {
                    let Some(output)=data.consume_output(Lane::OptionsGlbx,|v|Ok(Some(v.clone())))? else {break;};
                    drained+=1;
                    if data.generation(Lane::OptionsGlbx)?!=Some(output.generation){continue;}
                    ingress.retain(&output,&outbox,stop,budget).await?;
                }
                if dirty && broker.connected && let Some(physical)=data.generation(Lane::OptionsGlbx)?{
                        let end=(cursor+64).min(ids.len());
                        for id in &ids[cursor..end]{
                            if let Some(event)=observations.quote(&engine,*id,physical,at,true,Identity::fresh(at))?{
                                let contract=intent.source.registrations.iter().find(|r|r.instrument==*id).ok_or("Native monitored option disappeared")?;
                                threshold=ordered.submit(event.kind,codec::encode(&event.value)?,self.generation,Some(&contract.contract))?;
                                published_instruments.insert(*id);
                            }
                        }
                        cursor=end;if cursor==ids.len(){cursor=0;dirty=false;
                            if qualified_threshold.is_none() && threshold>0 && published_instruments.len()==ids.len(){qualified_threshold=Some(threshold);}
                        }
                }
                let cache_eligible=cache_broker.connected && !intent.caches.is_empty()
                    && transport.phase==super::lanes::Phase::Running && engine.reference_window(at)?.0;
                if cache_dirty && cache_eligible && cache_cadence.ready() {
                    cache_cadence.attempted();
                    let token=configuration.lock().map_err(|_|"Native cache configuration interrupted")?.clone();
                    let complete=source::read(&data,&engine,&intent.reviewed,self.generation,&token,at)
                        .map_err(|error|format!("Native cache source failed: {}: {}",error.code,error.input))?;
                    let mut next=std::collections::BTreeMap::new();let mut retained=0usize;
                    for cache in &intent.caches {
                        let mut prepared=None;
                        let mut no_candidate:Option<crate::option_chain::composition::model::Snapshot>=None;
                        let mut complete_choices=true;
                        for membership in crate::option_chain::cache_scopes::choices(cache,&intent.reviewed,intent.date)? {
                        let items=complete.instruments.iter().filter(|i|membership.contains(&i.contract)).cloned().collect::<Vec<_>>();
                        if items.len()!=membership.len(){return Err("Native cache complete membership changed".into());}
                        // Source/evidence draining and preceding cache calculations may
                        // consume the earlier loop timestamp's entire validity.
                        // Freeze this request immediately before capture; no
                        // recovery deadline or source timestamp is extended.
                        let capture_at=self.clock.utc_ticks().map_err(|_|"Native cache API clock unavailable")?;
                        let request=capture::Request{id:rand::random::<u128>().max(1).to_le_bytes(),scope:cache.scope.clone(),generation:self.generation,
                            horizon:cache.horizon.clone(),at:capture_at,until:budget.utc_until(capture_at).ok_or("Native cache validity overflow")?,maximum:2048,
                            age:5000,skew:2000,options:true,selection:false,allow_missing:cache.ranking.is_some(),risk:Default::default()};
                        let page=capture::Page{generation:self.generation,token:token.clone(),total:items.len(),instruments:items,continuation:None};
                        let snapshot=match capture::capture(request,vec![page],&self.clock,stop,budget).await {
                            Ok(snapshot)=>snapshot,
                            Err(error) if cache_waits_for_market_data(&error.code)=>{complete_choices=false;continue;},
                            Err(error)=>return Err(format!("Native cache capture failed: {}: {}; horizon={}; scope_chars={}; capture_age_ms={}",error.code,error.input,cache.horizon,cache.scope.encode_utf16().count(),self.clock.utc_ticks().unwrap_or(capture_at).saturating_sub(capture_at)/10_000)),
                        };
                        let snapshot=if let Some(ranking)=&cache.ranking {
                            match crate::option_chain::strategy::prepare(&snapshot,ranking.clone(),capture_at,stop,budget){
                                Ok(snapshot)=>snapshot,
                                Err("CandidateCoverageUnavailable")=>{
                                    match qualified_empty_scope(&snapshot,ranking,capture_at)? {
                                        Some(mut empty)=>{
                                            if let Some(previous)=&no_candidate {empty.until=empty.until.min(previous.until);empty.seal()?;}
                                            no_candidate=Some(empty);
                                        },
                                        None=>complete_choices=false,
                                    }
                                    // A later expiry choice may have executable
                                    // candidates. Empty is a fallback, never an
                                    // early exit that hides those candidates.
                                    continue;
                                },
                                Err("QuoteStale")=>{complete_choices=false;continue;},
                                Err(error)=>return Err(format!("Native candidate preparation failed: {error}")),
                            }
                        }else{snapshot};
                            prepared=Some(snapshot);break;
                        }
                        let Some(snapshot)=prepared.or(if complete_choices{no_candidate}else{None}) else{continue;};
                        cache_version=cache_version.checked_add(1).ok_or("Native cache version overflow")?;
                        let digest=cache.ranking.as_ref().map(|r|r.parameters.hash()).transpose()?.unwrap_or_else(||token.clone());
                        let publication=Arc::new(CachePublication::new(&snapshot,intent.date.to_julian_day()-1721426,&digest,cache_version,&cache.scope)?);
                        retained=retained.checked_add(publication.len()).ok_or("Native cache size overflow")?;
                        if retained>16*1024*1024{return Err("Native cache retained-byte capacity exceeded".into());}
                        next.insert(cache.scope.clone(),publication);
                    }
                    // Each lookup is complete independently. An explicitly known
                    // no-candidate result must not gate unrelated ready scopes.
                    let complete=next.len()==intent.caches.len();
                    cache_port.submit(next.clone())?;
                    *cache_directory.lock().map_err(|_|"Native cache directory interrupted")?=next;
                    cache_dirty=false;cache_evaluated=complete;
                }
                let mut proofs=Vec::new();
                if transport.phase==super::lanes::Phase::Running{proofs.push(Proof::Transport);}
                let valid=engine.reference_window(at)?.0;
                if valid&&!reference.recovering{proofs.push(Proof::Reference);}
                let quiet=intent.source.evidence==Evidence::ControlOnly;
                // Connection liveness is reported by the provider reader. Missing
                // quotes, expired financial context and absent cache results are
                // business unavailability, never evidence of a dead socket.
                let mut validity_wakeup=None;
                for (id,_) in &risk_workloads {
                    if let (Some(value),Some(context))=(engine.completed_risk(*id,at)?,engine.reference_context(*id)?) {
                        let age=i64::from(context.quote_limits().0)*10_000;
                        let until=context.valid_until().min(value.option.event.saturating_add(age))
                            .min(value.underlying.event.saturating_add(age));
                        next_validity_wakeup(&mut validity_wakeup,at,until);
                    }
                }
                let ready=quiet||engine.ready(at)?;
                if ready&&!numerical.recovering{proofs.push(Proof::Pricing);}
                if io.running&&outbox.retained_bytes()?==0{proofs.push(Proof::Persistence);}
                let quotes_published=intent.monitored.is_empty() || qualified_threshold.is_some_and(|threshold|broker.flushed>=threshold);
                // Loaded-trade monitoring is certified by its own delivered
                // current-generation leg quotes. Optional chain composition
                // cannot block that proof. Background-only demand must have
                // actual published results, never an empty pending directory.
                let cache_published=intent.caches.is_empty() || !intent.monitored.is_empty()
                    || cache_evaluated && cache_broker.published;
                if broker.connected && cache_broker.connected && (quiet || quotes_published&&cache_published){proofs.push(Proof::Publication);}
                if proofs.len()==5{
                    if !business_ready {business_ready=match &self.intent.background{Some(background)=>background.acknowledge_committed(self.parent.clone(),stop,if initial.remaining().is_zero(){Deadline::recovery()}else{initial},initial_physical).await?,None=>true};}
                    if business_ready{*setup=None;}else{proofs.retain(|proof|*proof!=Proof::Persistence);}
                }
                // Setup proof may have cleared its deadline in this iteration.
                // Never retain that spent setup timer in a healthy notification.
                let published_deadline=[*setup,transport.deadline,numerical.deadline,engine.reference_deadline()?,io.deadline,broker.pending_since.map(Deadline::recovery_from),cache_broker.deadline,background.deadline].into_iter().flatten().reduce(Deadline::earliest);
                // These are component/setup facts, independent of whether an
                // option currently has a quote or produces an executable plan.
                let setup_complete=transport.phase==super::lanes::Phase::Running
                    && broker.connected && cache_broker.connected && io.running
                    && !reference.recovering && !numerical.recovering;
                self.health.send_replace(Readiness{setup_complete,proofs,failure:None,deadline:published_deadline});
                if drained==64 || dirty&&cursor!=0 {tokio::task::yield_now().await;continue;}
                tokio::select!{biased;()=stop.cancelled()=>break,
                    ()=crate::runtime::availability::Availability::pending(validity_wakeup)=>{},
                    ()=cache_cadence.pending(cache_dirty && cache_eligible && !cache_cadence.ready())=>{},
                    result=async{match background_changes.as_mut(){Some(changes)=>changes.changed().await,None=>std::future::pending().await}}=>{result.map_err(|_|"Native background refresh observation ended")?;},
                    result=logs.changed()=>{result.map_err(|_|"Native diagnostic health closed")?;},
                    result=source.changed()=>{result.map_err(|_|"Native options source observation closed")?;dirty=true;cache_dirty=true;},
                    result=calculations.changed()=>{result.map_err(|_|"Native options calculation observation closed")?;dirty=true;cache_dirty=true;},
                    result=reference_changes.changed()=>{result.map_err(|_|"Native options reference observation closed")?;dirty=true;cache_dirty=true;},
                    result=lifecycle.changed()=>{result.map_err(|_|"Native options lifecycle observation closed")?;dirty=true;cache_dirty=true;},
                    result=cache_status.changed()=>{result.map_err(|_|"Native cache publication observation closed")?;},
                    result=publication.changed()=>{result.map_err(|_|"Native options publication observation closed")?;},
                    result=storage.changed()=>{result.map_err(|_|"Native options persistence observation closed")?;},
                    result=persisted.changed()=>{result.map_err(|_|"Native options outbox observation closed")?;},
                    result=clocks.changed()=>{result.map_err(|_|"Native options API clock observation closed")?;dirty=true;cache_dirty=true;},
                }
            }
            Ok(())
        }.await;
        // Publish the first failure before awaiting cleanup. A stalled join or
        // broker cannot postpone incident admission or grant a later deadline.
        if let Err(error) = &processed {
            let original = self
                .retirement
                .lock()
                .map_err(|_| "Native options retirement interrupted")?
                .or(*setup)
                .unwrap_or_else(Deadline::recovery);
            *self
                .retirement
                .lock()
                .map_err(|_| "Native options retirement interrupted")? = Some(original);
            self.health.send_replace(Readiness {
                setup_complete: false,
                proofs: vec![],
                failure: Some(error.clone()),
                deadline: Some(original),
            });
        }
        // Signal the whole subtree before awaiting any one descendant. These
        // joins never wait for a managed consumer, durable receipt or broker ACK.
        self.parent.cancellation().cancel();
        let _ = data.cancel_lane(Lane::OptionsGlbx);
        references.cancel();
        pricing.cancel();
        let cleanup = self
            .retirement
            .lock()
            .ok()
            .and_then(|d| *d)
            .or(*setup)
            .unwrap_or_else(Deadline::recovery);
        // Attempt every independent cleanup, retaining errors instead of hiding
        // them behind a successful processing result or the first failed join.
        let cleanup_token = CancellationToken::new();
        let (a, b, c, e, f) = tokio::join!(
            evidence.shutdown(cleanup),
            actor.shutdown(cleanup),
            cache_writer.shutdown(cleanup),
            references.stop(&cleanup_token, cleanup),
            pricing.stop(&cleanup_token, cleanup)
        );
        let mut errors = Vec::new();
        if let Err(error) = processed {
            errors.push(error);
        }
        if let Err(error) = a {
            errors.push(format!("evidence cleanup: {error}"));
        }
        if let Err(error) = b {
            errors.push(format!("actor publisher cleanup: {error}"));
        }
        if let Err(error) = c {
            errors.push(format!("cache publisher cleanup: {error}"));
        }
        if let Err(error) = e {
            errors.push(format!("reference cleanup: {error}"));
        }
        if let Err(error) = f {
            errors.push(format!("pricing cleanup: {error}"));
        }
        if errors.is_empty() {
            Ok(())
        } else {
            Err(errors.join("; "))
        }
    }
}
impl Resources for Group {
    // Final-source lifecycle is reported through Readiness below. The parent
    // also owns temporary/bootstrap readers, so its aggregate activity must
    // never certify that this generation's option subscriptions are installed.
    fn cancel(&self, deadline: Deadline) {
        // Signal before optional diagnostics; lock contention cannot delay stop.
        self.parent.cancellation().cancel();
        if let Ok(mut saved) = self.retirement.try_lock() {
            *saved = Some(saved.map_or(deadline, |old| old.earliest(deadline)));
        }
    }
    fn reset(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        Box::pin(async move { self.parent.reset_task(&mut self.task, deadline).await })
    }
    fn changes(&self) -> watch::Receiver<Readiness> {
        self.health.clone()
    }
    fn join(&mut self, deadline: Deadline) -> BoxFuture<'_, Result<(), String>> {
        Box::pin(async move {
            if let Some(task) = self.task.as_mut() {
                let joined = deadline
                    .run(&CancellationToken::new(), task)
                    .await
                    .map_err(|_| "Native options original group join deadline expired")?;
                self.task.take();
                joined.map_err(|_| "Native options group task panicked")?;
            }
            self.parent
                .wait_nested(&CancellationToken::new(), deadline)
                .await
                .map_err(|_| "Native options original nested join deadline expired".into())
        })
    }
}
impl Drop for Group {
    fn drop(&mut self) {
        self.parent.cancellation().cancel();
    }
}

#[cfg(test)]
mod logical_statistics_tests {
    #[test]
    fn unavailable_risk_diagnostic_distinguishes_absence_from_stale_evidence() {
        use crate::option_chain::{
            coalesced::{Calculation, Failure},
            qualification::Quote,
        };
        let absent = super::risk_unavailable_detail(None, None, false, 0);
        assert!(absent.contains("calculation=absent"));
        assert!(absent.contains("publication_seen=false"));
        let value = Calculation {
            context: 1,
            source_generation: 1,
            at: 0,
            underlying: Quote {
                event: i64::MIN,
                ..Default::default()
            },
            option: Quote {
                event: i64::MAX,
                ..Default::default()
            },
            iv_event: None,
            iv: None,
            time: 0.1,
            selection: None,
            risk: None,
            failure: Some(Failure::Stale),
        };
        let detail = super::risk_unavailable_detail(Some(&value), None, true, i64::MAX);
        assert!(detail.contains("calculation_failure=Some(Stale)"));
        assert!(detail.contains("option_age_ms=0"));
        assert!(detail.contains("underlying_age_ms=1844674407370955"));
        assert!(detail.contains("publication_seen=true"));
        assert!(detail.len() < 512);
    }
    use super::*;
    fn identity(publisher: u16, contract: &str) -> std::collections::BTreeMap<u32, (u16, String)> {
        [(43, (publisher, contract.into()))].into()
    }
    #[test]
    fn physical_reset_retains_only_same_date_publisher_and_canonical_contract() {
        let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
        let mut logical = LogicalStatistics::default();
        let bank = logical.retain(date, identity(3, "put")).unwrap();
        bank.lock().unwrap().insert(
            43,
            crate::option_chain::statistics::Statistics {
                observed: 123,
                ..Default::default()
            },
        );
        assert_eq!(
            logical
                .retain(date, identity(3, "put"))
                .unwrap()
                .lock()
                .unwrap()[&43]
                .observed,
            123
        );
        assert!(
            logical
                .retain(date, identity(3, "call"))
                .unwrap()
                .lock()
                .unwrap()
                .is_empty()
        );
        logical
            .values
            .lock()
            .unwrap()
            .insert(43, Default::default());
        assert!(
            logical
                .retain(date, identity(4, "call"))
                .unwrap()
                .lock()
                .unwrap()
                .is_empty()
        );
        logical
            .values
            .lock()
            .unwrap()
            .insert(43, Default::default());
        assert!(
            logical
                .retain(date.next_day().unwrap(), identity(4, "call"))
                .unwrap()
                .lock()
                .unwrap()
                .is_empty()
        );
        logical
            .values
            .lock()
            .unwrap()
            .insert(43, Default::default());
        assert!(
            logical
                .retain(date.next_day().unwrap(), Default::default())
                .unwrap()
                .lock()
                .unwrap()
                .is_empty()
        );
    }
    #[test]
    fn rejected_capacity_preserves_existing_logical_statistics() {
        let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
        let mut logical = LogicalStatistics::default();
        logical.retain(date, identity(3, "put")).unwrap();
        let invalid = (1..=2049)
            .map(|id| (id, (3, format!("option-{id}"))))
            .collect();
        assert!(logical.retain(date, invalid).is_err());
        assert_eq!(logical.identities, identity(3, "put"));
    }
}

/// Selects the earliest budget of actual failures, retaining a still-active setup
/// budget. Healthy pending output must not grant or exhaust a recovery episode.
fn active_failure<const N: usize>(
    failures: [(Option<String>, Option<Deadline>); N],
    setup: Option<Deadline>,
) -> Option<(String, Deadline)> {
    let mut error = None;
    let mut budget = setup;
    for (failure, deadline) in failures {
        if let Some(failure) = failure {
            error.get_or_insert(failure);
            if let Some(deadline) = deadline {
                budget = Some(budget.map_or(deadline, |old| old.earliest(deadline)));
            }
        }
    }
    error.map(|error| (error, budget.unwrap_or_else(Deadline::recovery)))
}
#[cfg(test)]
mod failure_budget_tests {
    use super::*;
    #[test]
    fn known_pending_market_inputs_do_not_hide_structural_or_component_failures() {
        for code in ["QuoteUnavailable", "UnderlyingQuoteUnavailable", "PricingContextUnavailable", "StaleData", "InvalidQuote", "IncoherentQuotes"] {
            assert!(cache_waits_for_market_data(code), "{code}");
        }
        for code in ["SnapshotTimeout", "InvalidSnapshotRequest", "IncompleteChain", "ConflictingDefinition", "Recovering", "GreeksCalculationFailed", "UnknownFailure"] {
            assert!(!cache_waits_for_market_data(code), "{code}");
        }
    }
    #[tokio::test(start_paused = true)]
    async fn unrelated_expired_output_does_not_expire_a_new_policy_fault() {
        let old = Deadline::recovery();
        tokio::time::advance(std::time::Duration::from_secs(600)).await;
        let fresh = Deadline::recovery();
        let (_, budget) = active_failure(
            [
                (None, Some(old)),
                (Some("policy changed".into()), Some(fresh)),
            ],
            None,
        )
        .unwrap();
        assert_eq!(budget.remaining(), std::time::Duration::from_secs(120));
        assert!(active_failure([(None, Some(old))], None).is_none());
    }
    #[tokio::test(start_paused = true)]
    async fn actual_failed_components_keep_the_original_and_expired_budget() {
        let original = Deadline::recovery();
        tokio::time::advance(std::time::Duration::from_secs(10)).await;
        let (_, budget) = active_failure(
            [
                (Some("first".into()), Some(original)),
                (Some("second".into()), Some(Deadline::recovery())),
            ],
            None,
        )
        .unwrap();
        assert_eq!(budget.remaining(), std::time::Duration::from_secs(110));
        tokio::time::advance(std::time::Duration::from_secs(110)).await;
        let (_, budget) = active_failure(
            [(Some("expired actual failure".into()), Some(original))],
            None,
        )
        .unwrap();
        assert!(budget.remaining().is_zero());
    }
}
