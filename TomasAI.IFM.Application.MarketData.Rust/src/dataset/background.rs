//! Native background option discovery/planning. All storage facts are copied;
//! optional quote observation runs independently of monitored session startup.
//! No managed lease, context or renewal service is called.
use super::{
    live::Registration,
    options_owner::{CacheScope, Intent},
    transport::{Lane, SessionPlan, SubscriptionSpec},
};
use crate::{
    api::owner::MarketData,
    latest::Asset,
    option_chain::{
        coverage::{Planner, ResultSet, Universe},
        discovery::Discovery,
        preparation::RawPolicy,
        reference_bootstrap::Bootstrap,
        strategy::{self, Ranking},
    },
    reference::calendar::Zone,
    runtime::{Deadline, clock::Clock, owner::Child},
};
use databento::dbn::{SType, Schema};
use futures_util::FutureExt;
use sha2::{Digest, Sha256};
use std::time::Duration;
use std::{
    collections::{BTreeMap, BTreeSet},
    sync::{Arc, Mutex, Weak},
};
use tokio::sync::watch;
use tokio_util::sync::CancellationToken;
#[path = "background/monitoring.rs"]
mod monitoring;
pub use monitoring::Monitor;
#[path = "background/scopes.rs"]
mod scopes;
pub use scopes::Demand as ScopeDemand;
#[path = "background/browser.rs"]
mod browser;
#[path = "background/committed.rs"]
mod committed_refresh;
#[path = "background/quotes.rs"]
mod quotes;
#[path = "background/statistics.rs"]
pub mod statistics;

type FactCache = Arc<Mutex<Option<(time::Date, Arc<Facts>)>>>;
type SetupDiagnostics =
    Arc<Mutex<Option<(Weak<super::live::Diagnostics>, [u8; 16], [u8; 16], u64)>>>;
/// Immutable native policy/source configuration. The weak market directory
/// observes copied prices without retaining a physical sibling dataset owner.
#[derive(Clone)]
pub struct Configuration {
    /// Raw policy/definition source with native bounded paged IO.
    pub discovery: Arc<Discovery>,
    /// Reviewed dataset-global numerical reference policy.
    pub policy: RawPolicy,
    /// Native primary-futures query directory.
    pub market: Weak<MarketData>,
    /// Bounded native coverage envelopes retained across source rebuilds.
    pub planner: Arc<Planner>,
    enabled: bool,
    browser: Arc<Mutex<browser::State>>,
    statistics: Arc<Mutex<statistics::State>>,
    monitoring: monitoring::Monitoring,
    scopes: scopes::Scopes,
    committed: Option<Arc<crate::option_chain::committed::Directory>>,
    facts: FactCache,
    reference_cache: Arc<Mutex<Option<Arc<Bootstrap>>>>,
    refresh_incident: Arc<Mutex<Option<Deadline>>>,
    setup_diagnostics: SetupDiagnostics,
    quotes: Arc<Mutex<quotes::State>>,
    quote_changes: watch::Sender<u64>,
}
struct Facts {
    fingerprint: std::sync::OnceLock<Result<[u8; 32], String>>,
    policies: Vec<Arc<crate::option_chain::strategy::Parameters>>,
    models: Vec<crate::reference::option_contract::CoverageRow>,
}
fn positive(value: crate::option_chain::qualification::Decimal) -> bool {
    value.compare(Default::default()) == Some(std::cmp::Ordering::Greater)
}
fn merged_registration(
    rows: &mut BTreeMap<u32, Registration>,
    mut row: Registration,
    monitored: &BTreeSet<u32>,
) -> Result<(), String> {
    row.required = row.asset == Asset::Futures || monitored.contains(&row.instrument);
    if let Some(old) = rows.insert(row.instrument, row.clone())
        && old != row
    {
        return Err("Conflicting native background/monitored source binding".into());
    }
    Ok(())
}
impl Configuration {
    /// Creates copied native source configuration. Cached facts are immutable,
    /// date-scoped data; they cannot retain a dataset, task, socket or lease.
    pub fn new(discovery: Arc<Discovery>, policy: RawPolicy, market: Weak<MarketData>) -> Self {
        Self {
            discovery,
            policy,
            market,
            planner: Arc::default(),
            enabled: true,
            browser: Arc::default(),
            statistics: Arc::default(),
            monitoring: Default::default(),
            scopes: Default::default(),
            committed: None,
            facts: Arc::default(),
            reference_cache: Arc::default(),
            refresh_incident: Arc::default(),
            setup_diagnostics: Arc::default(),
            quotes: Arc::default(),
            quote_changes: watch::channel(0).0,
        }
    }
    /// Replaces global reference policy while retaining only immutable raw facts
    /// and coverage envelopes. No old numerical context or physical owner is reused.
    pub fn rebase(&self, policy: RawPolicy) -> Self {
        Self {
            policy,
            ..self.clone()
        }
    }
    /// Reuses copied source metadata for a repeated background command. Global
    /// numerical policy still comes from the new command and is requalified.
    /// Different authenticated storage namespaces never share cached facts.
    pub fn inherit_facts(&self, previous: &Self) -> Result<Self, String> {
        let monitoring = self.monitoring.merged(&previous.monitoring)?;
        let scopes = self
            .provisional_scopes()?
            .merge(&previous.provisional_scopes()?)?;
        if (!self.enabled || self.discovery.environment == previous.discovery.environment)
            && self.discovery.scope == previous.discovery.scope
            && self
                .discovery
                .broker
                .same_data_source(&previous.discovery.broker)
        {
            let result = Self {
                discovery: if self.enabled {
                    self.discovery.clone()
                } else {
                    previous.discovery.clone()
                },
                enabled: self.enabled || previous.enabled,
                monitoring,
                scopes,
                policy: if !self.enabled {
                    previous.policy.clone()
                } else {
                    self.policy.clone()
                },
                committed: previous
                    .committed
                    .clone()
                    .or_else(|| self.committed.clone()),
                browser: previous.browser.clone(),
                statistics: previous.statistics.clone(),
                planner: previous.planner.clone(),
                facts: previous.facts.clone(),
                reference_cache: previous.reference_cache.clone(),
                refresh_incident: previous.refresh_incident.clone(),
                setup_diagnostics: previous.setup_diagnostics.clone(),
                quotes: previous.quotes.clone(),
                quote_changes: previous.quote_changes.clone(),
                ..self.clone()
            };
            result.effective_scopes()?;
            Ok(result)
        } else {
            if !previous.monitors().is_empty() || !previous.scope_demands()?.is_empty() {
                return Err("Cannot transfer native monitors across raw fact sources".into());
            }
            Ok(self.clone())
        }
    }
    pub(in crate::dataset) fn bind_setup_diagnostics(
        &self,
        sink: &Arc<super::live::Diagnostics>,
        worker: [u8; 16],
        generation: [u8; 16],
        revision: u64,
    ) -> Result<(), String> {
        *self
            .setup_diagnostics
            .lock()
            .map_err(|_| "Native setup diagnostics interrupted")? =
            Some((Arc::downgrade(sink), worker, generation, revision));
        Ok(())
    }
    fn waiting_underlyings(&self, contracts: &[String], clock: &Clock) -> Result<(), String> {
        report_waiting(&self.setup_diagnostics, contracts, clock)
    }
}
fn report_waiting(
    diagnostics: &SetupDiagnostics,
    contracts: &[String],
    clock: &Clock,
) -> Result<(), String> {
    let binding = diagnostics
        .lock()
        .map_err(|_| "Native setup diagnostics interrupted")?
        .clone();
    if let Some((sink, worker, generation, revision)) = binding
        && let Some(sink) = sink.upgrade()
    {
        let mut text = format!(
            "Background option coverage pending for {} underlying quotes; monitored subscriptions are independent; generation={}; contracts=",
            contracts.len(),
            crate::messaging::wire::guid(generation)
        );
        for contract in contracts {
            if text.len() + contract.len() + 1 > 3800 {
                text.push_str("[truncated]");
                break;
            }
            text.push_str(contract);
            text.push(',');
        }
        sink.retain(super::live::Diagnostic {
            worker,
            lane: Lane::OptionsGlbx,
            attempt: revision,
            observed: clock
                .utc_ticks()
                .map_err(|_| "Native setup clock unavailable")?,
            replaced: 0,
            event: super::transport::Event::System {
                code: u8::MAX,
                message: text,
            },
        })?;
    }
    Ok(())
}
impl Configuration {
    async fn source_facts(
        &self,
        date: time::Date,
        parent: Arc<Child>,
        stop: &CancellationToken,
        deadline: Deadline,
        refresh: bool,
    ) -> Result<Arc<Facts>, String> {
        let (needs_refresh, deadline) = refresh_budget(&self.refresh_incident, deadline)?;
        if !refresh
            && !needs_refresh
            && let Some((known_date, facts)) = self
                .facts
                .lock()
                .map_err(|_| "Native background fact cache interrupted")?
                .as_ref()
            && *known_date == date
        {
            return Ok(facts.clone());
        }
        if !self.enabled {
            let facts = Arc::new(Facts {
                fingerprint: std::sync::OnceLock::new(),
                policies: vec![],
                models: vec![],
            });
            *self
                .facts
                .lock()
                .map_err(|_| "Native background fact cache interrupted")? =
                Some((date, facts.clone()));
            return Ok(facts);
        }
        let policies = self
            .discovery
            .policies(parent.clone(), stop, deadline)
            .await?;
        let mut roots = BTreeMap::<String, (time::Date, time::Date)>::new();
        for policy in &policies {
            if let Some((from, until)) = crate::option_chain::discovery::range(policy, date)? {
                roots
                    .entry(policy.root().to_owned())
                    .and_modify(|range| {
                        range.0 = range.0.min(from);
                        range.1 = range.1.max(until);
                    })
                    .or_insert((from, until));
            }
        }
        let mut models = BTreeMap::new();
        for (root, (from, until)) in roots {
            let rows = self
                .discovery
                .read_coverage(&root, from, until, parent.clone(), stop, deadline)
                .await?;
            for model in rows {
                let id = model.coverage_definition()?.contract.to_owned();
                if let Some(old) = models.insert(id, model.clone())
                    && old != model
                {
                    return Err("Conflicting native background raw definition".into());
                }
                if models.len() > 100000 {
                    return Err("Native complete background definition capacity exceeded".into());
                }
            }
        }
        if stop.is_cancelled() || deadline.remaining().is_zero() {
            return Err("Native background fact acquisition cancelled/deadline expired".into());
        }
        let facts = Arc::new(Facts {
            fingerprint: std::sync::OnceLock::new(),
            policies,
            models: models.into_values().collect(),
        });
        *self
            .facts
            .lock()
            .map_err(|_| "Native background fact cache interrupted")? = Some((date, facts.clone()));
        *self
            .refresh_incident
            .lock()
            .map_err(|_| "Native background refresh status interrupted")? = None;
        Ok(facts)
    }
    /// Resolves monitored legs and currently available background coverage.
    /// Missing underlying prices start an optional observer and leave only their
    /// coverage groups pending. They never delay monitored subscription setup.
    ///
    /// # Errors
    /// Source/metadata conflict, capacity, cancellation or
    /// original deadline expiry. The dataset supervisor owns retry/escalation.
    pub async fn resolve(
        &self,
        base: Arc<Intent>,
        parent: Arc<Child>,
        clock: Arc<Clock>,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Option<Arc<Intent>>, String> {
        if let Some(directory) = &self.committed {
            directory.rebase_pending(deadline)?;
            directory
                .load(
                    || {
                        self.discovery
                            .read_committed("IFM", parent.clone(), stop, deadline)
                    },
                    stop,
                    deadline,
                )
                .await?;
        }
        if let Some(book) = self.committed_book()? {
            self.market
                .upgrade()
                .ok_or("Native market owner ended")?
                .realize_committed_futures(&book, stop, deadline)
                .await?;
        }
        let facts = self
            .source_facts(base.date, parent.clone(), stop, deadline, false)
            .await?;
        let (mut monitored_models, mut monitored) = self
            .monitored_models(&base, parent.clone(), stop, deadline)
            .await?;
        let (browser_models, browser_monitored) = self
            .resolve_browsers(base.date, parent.clone(), stop, deadline)
            .await?;
        monitored_models.extend(browser_models);
        monitored.extend(browser_monitored);
        monitored_models.extend(
            self.scope_models(parent.clone(), &clock, stop, deadline)
                .await?,
        );

        let mut unique = BTreeMap::new();
        for model in monitored_models {
            let id = model.coverage_definition()?.contract.to_owned();
            if unique.insert(id, model.clone()).is_some_and(
                |old: crate::reference::option_contract::OptionContract| {
                    old.value() != model.value()
                },
            ) {
                return Err("Native monitored/scoped references conflict".into());
            }
        }
        if let Some(book) = self
            .committed
            .as_ref()
            .map(|directory| directory.accepted())
            .transpose()?
            .flatten()
        {
            for plan in book
                .plans
                .values()
                .filter(|plan| !plan.candidates.is_empty())
            {
                if self.resolved_scope(&plan.id, None)?.is_none() {
                    return Err(
                        "Committed option reconstruction is not completely qualified".into(),
                    );
                }
            }
            for lease in book
                .sources
                .values()
                .flat_map(|source| &source.leases)
                .filter(|lease| lease.option)
            {
                let model = unique
                    .get(&lease.contract)
                    .ok_or("Committed monitored contract is unavailable")?;
                monitored.insert(model.convention()?.instrument());
            }
        }
        let monitored_models = unique.into_values().collect();
        monitored.extend(base.monitored.iter().copied());
        let policies = &facts.policies;
        let models = &facts.models;
        if models.is_empty() || base.source.evidence == super::live::Evidence::ControlOnly {
            return self.finish(base, monitored_models, BTreeMap::new(), monitored);
        }
        let mut underlyings = BTreeMap::new();
        for model in models {
            let (instrument, publisher, contract) = model.underlying_source()?;
            let row = Registration {
                instrument,
                publisher,
                contract: contract.into(),
                source: instrument.to_string(),
                asset: Asset::Futures,
                tick: None,
                vwap: false,
                required: true,
            };
            if underlyings
                .insert(instrument, row.clone())
                .is_some_and(|old| old != row)
            {
                return Err("Conflicting compact source underlying".into());
            }
        }
        if underlyings.len() > 64 {
            return Err("Native background underlying capacity exceeded".into());
        }
        let mut forward = BTreeMap::new();
        let mut prices = BTreeMap::new();
        let mut missing = Vec::new();
        {
            let market = self.market.upgrade();
            for row in underlyings.values() {
                let Some(market) = market.as_ref() else {
                    missing.push(row.clone());
                    continue;
                };
                if let Some(price) = market
                    .contract_price(&row.contract, false, 9_000_000_000)?
                    .1
                    .filter(|n| positive(*n))
                {
                    prices.insert(row.contract.clone(), price);
                } else if let Some(price) = market
                    .options_current()
                    .map(|v| v.queries.raw_prices(row.instrument))
                    .transpose()?
                    .flatten()
                    .and_then(|raw| raw.quote)
                    .and_then(|q| super::bootstrap_quotes::midpoint(q, clock.utc_ticks().ok()?))
                {
                    prices.insert(row.contract.clone(), price);
                } else {
                    missing.push(row.clone());
                }
            }
        }
        if !missing.is_empty() {
            if missing.len() > 64 {
                return Err("Native background underlying capacity exceeded".into());
            }
            let symbols = underlyings
                .values()
                .map(|r| r.source.clone())
                .collect::<Vec<_>>();
            let mut source = base.source.clone();
            source.registrations = underlyings.values().cloned().collect();
            // A clean opening has no previous book. Recover the latest quote
            // from this API-selected trading session instead of requiring a new
            // change in a quiet deferred future. This temporary source publishes
            // no executable events; provider event times and validation remain intact.
            let replay = super::futures_bootstrap::session_start(base.date)?;
            source.plan = SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: [Schema::Trades, Schema::Mbp1]
                    .into_iter()
                    .map(|schema| SubscriptionSpec {
                        schema,
                        symbology: SType::InstrumentId,
                        symbols: symbols.clone(),
                        replay_start_ns: (schema == Schema::Mbp1).then_some(replay),
                    })
                    .collect(),
            };
            let diagnostics = self.setup_diagnostics.clone();
            let diagnostic_clock = clock.clone();
            prices.extend(
                self.quotes
                    .lock()
                    .map_err(|_| "Background observer state interrupted")?
                    .prices(
                        source,
                        base.date,
                        clock.clone(),
                        parent.clone(),
                        move |missing| report_waiting(&diagnostics, missing, &diagnostic_clock),
                        self.quote_changes.clone(),
                    )?,
            );
            let missing = underlyings
                .values()
                .filter(|row| !prices.contains_key(&row.contract))
                .map(|row| row.contract.clone())
                .collect::<Vec<_>>();
            self.waiting_underlyings(&missing, &clock)?;
        }
        // Quiet quote acquisition is not a recovery incident. Subsequent finite
        // reference/planning work has its own bounded attempt after data arrives.
        if stop.is_cancelled() {
            return Err("Native background setup cancelled".into());
        }
        let deadline = if deadline.remaining().is_zero() {
            Deadline::recovery()
        } else {
            deadline
        };
        // The planner uses the exact per-universe underlying map below.
        // A separate front-futures admission gate adds no numerical information.
        for policy in policies {
            if let Some(price) = models
                .iter()
                .filter_map(|model| model.coverage_definition().ok())
                .filter(|d| d.root == policy.root())
                .find_map(|d| prices.get(d.underlying).copied())
                .filter(|n| positive(*n))
            {
                forward.insert(policy.root().to_owned(), price);
            }
        }
        let current = self
            .market
            .upgrade()
            .and_then(|market| market.options_current());
        let at = clock
            .utc_ticks()
            .map_err(|_| "Native background coverage clock unavailable")?;
        let mut selected = BTreeSet::new();
        let mut caches = BTreeMap::new();
        for policy in policies {
            if !policy.enabled || !policy.biases.iter().any(|b| b.enabled) {
                continue;
            }
            let definitions = models
                .iter()
                .map(|model| model.coverage_definition())
                .collect::<Result<Vec<_>, _>>()?;
            let Some(first) = models.iter().find(|model| {
                model
                    .coverage_definition()
                    .is_ok_and(|d| d.root == policy.root())
            }) else {
                continue;
            };
            let timezone = first.timezone();
            let Some(initial) = forward.get(policy.root()).copied() else {
                // Coverage remains pending for this root. Known monitored legs
                // and other roots continue independently.
                continue;
            };
            // Only fresh qualified calculations from the current native physical
            // source contribute observed IV. Recovery starts from 20%; retained
            // native envelopes preserve previously acquired coverage safely.
            let policy_contracts = models
                .iter()
                .filter_map(|model| model.coverage_definition().ok())
                .filter(|definition| definition.root == policy.root())
                .map(|definition| definition.contract)
                .collect::<BTreeSet<_>>();
            let observed = current
                .as_ref()
                .map(|view| {
                    let mut observed: Option<f64> = None;
                    for row in view
                        .registrations
                        .iter()
                        .filter(|row| row.asset == Asset::Option)
                    {
                        if policy_contracts.contains(row.contract.as_str())
                            && let Some(value) = view.pricing.read(row.instrument, false, at)?
                            && value.failure.is_none()
                            && value.selection.is_some()
                            && let Some(iv) = value.iv.filter(|iv| {
                                iv.volatility.is_finite()
                                    && iv.volatility > 0.0
                                    && iv.volatility <= 5.0
                            })
                        {
                            observed =
                                Some(observed.map_or(iv.volatility, |old| old.max(iv.volatility)));
                        }
                    }
                    Ok::<_, String>(observed)
                })
                .transpose()?
                .flatten()
                .unwrap_or(0.20);
            let planned = self.planner.plan_available(
                policy,
                base.date,
                &definitions,
                true,
                initial,
                observed,
                Zone::new(timezone).map_err(|_| "Invalid native exchange zone")?,
                Some(&prices),
                stop,
                deadline,
            )?;
            for universe in background_universes(planned)? {
                selected.extend(universe.contracts.iter().copied());
                let dte = (universe.expiration - base.date).whole_days();
                let contracts = universe
                    .contracts
                    .iter()
                    .map(|index| definitions[*index].contract.to_owned())
                    .collect::<BTreeSet<_>>();
                for bias in policy.biases.iter().filter(|b| {
                    b.enabled && dte >= i64::from(b.minimum) && dte <= i64::from(b.maximum)
                }) {
                    for horizon in ["Weekly", "Monthly"] {
                        let key = strategy::scope_key(policy, bias.direction, base.date, horizon)?;
                        let cache = caches.entry(key.clone()).or_insert_with(|| CacheScope {
                            scope: key,
                            horizon: horizon.into(),
                            contracts: BTreeSet::new(),
                            ranking: Some(Ranking {
                                parameters: policy.clone(),
                                direction: bias.direction,
                                date: base.date,
                            }),
                        });
                        cache.contracts.extend(contracts.iter().cloned());
                    }
                }
            }
            if caches.len() > 64 {
                return Err("Native complete ranked scope capacity exceeded".into());
            }
        }
        if selected.len() > 2048 {
            return Err(format!("Native selected background option capacity exceeded: selected={}; maximum=2048", selected.len()));
        }
        let retained = self
            .reference_cache
            .lock()
            .map_err(|_| "Native background reference cache interrupted")?
            .clone();
        let mut full = Vec::new();
        for indices in selected.into_iter().collect::<Vec<_>>().chunks(8) {
            let requests = indices.iter().map(|index| async {
                let compact = &models[*index];
                let contract = compact.coverage_definition()?.contract;
                let known = retained
                    .as_ref()
                    .map(|cache| cache.option_model(contract))
                    .transpose()?
                    .flatten();
                if let Some(known) = known
                    && compact.matches(&known)?
                {
                    return Ok::<_, String>(known);
                }
                let args = compact.selected_model_request()?;
                let bytes = crate::messaging::raw_io::read(
                    self.discovery.broker.clone(),
                    self.discovery.scope.clone(),
                    parent.clone(),
                    6,
                    &args,
                    stop,
                    deadline,
                )
                .await?;
                let value = serde_json::from_slice(&bytes)
                    .map_err(|_| "Malformed selected option source")?;
                let model =
                    crate::reference::option_contract::OptionContract::from_command(&value)?;
                if !compact.matches(&model)? {
                    return Err("Selected option source changed since compact snapshot".into());
                }
                Ok(model)
            });
            full.extend(futures_util::future::try_join_all(requests).await?);
        }
        let mut selected = full;
        // A missing/stale price pauses replanning; it must not unsubscribe
        // already acquired coverage. Retain only current-source background
        // contracts, never removed policies or released monitored legs.
        if let Some(view) = &current {
            let allowed = models
                .iter()
                .map(|model| model.coverage_definition().map(|d| d.contract.to_owned()))
                .collect::<Result<BTreeSet<_>, _>>()?;
            let retained = self
                .reference_cache
                .lock()
                .map_err(|_| "Native background reference cache interrupted")?
                .clone();
            let mut kept = BTreeSet::new();
            for definition in view
                .definitions
                .iter()
                .filter(|d| !view.monitored.contains(&d.instrument))
            {
                let contract = definition.blueprint.contract.contract();
                if !allowed.contains(contract) {
                    continue;
                }
                if let Some(model) = retained
                    .as_ref()
                    .map(|cache| cache.option_model(contract))
                    .transpose()?
                    .flatten()
                    && !prices.contains_key(model.underlying_source()?.2)
                {
                    kept.insert(contract.to_owned());
                    if !selected
                        .iter()
                        .any(|known| known.convention().is_ok_and(|c| c.contract() == contract))
                    {
                        selected.push(model);
                    }
                }
            }
            for old in view.cache_scopes.iter() {
                let contracts = old
                    .contracts
                    .intersection(&kept)
                    .cloned()
                    .collect::<BTreeSet<_>>();
                if contracts.is_empty() {
                    continue;
                }
                if let Some(cache) = caches.get_mut(&old.scope) {
                    cache.contracts.extend(contracts);
                } else {
                    caches.insert(
                        old.scope.clone(),
                        CacheScope {
                            contracts,
                            ..old.clone()
                        },
                    );
                }
            }
        }
        selected.extend(monitored_models);
        self.finish(base, selected, caches, monitored)
    }
    fn finish(
        &self,
        base: Arc<Intent>,
        selected: Vec<crate::reference::option_contract::OptionContract>,
        caches: BTreeMap<String, CacheScope>,
        monitored: BTreeSet<u32>,
    ) -> Result<Option<Arc<Intent>>, String> {
        // Browser, monitored and background policy scopes can cover the same
        // option. Unite identical models before validating physical ownership;
        // different metadata for the same canonical identity remains an error.
        let selected = union_option_models(selected)?;
        if selected.is_empty() {
            return Ok((!base.reviewed.definitions.is_empty()).then_some(base));
        }
        let background = super::raw_options::Plan::from_models(&selected)?;
        let mut rows = BTreeMap::new();
        for row in base
            .source
            .registrations
            .iter()
            .cloned()
            .chain(background.registrations)
        {
            merged_registration(&mut rows, row, &monitored)?;
        }
        if rows.len() > 2112 {
            return Err("Native complete dataset dependency capacity exceeded".into());
        }
        let mut definitions = base
            .reviewed
            .definitions
            .iter()
            .map(|d| (d.instrument, d.clone()))
            .collect::<BTreeMap<_, _>>();
        for mut definition in self.policy.definitions(base.date, &selected)? {
            definition.required = monitored.contains(&definition.instrument);
            if let Some(old) = definitions.insert(definition.instrument, definition.clone())
                && old.blueprint.contract.value() != definition.blueprint.contract.value()
            {
                return Err("Conflicting native background pricing convention".into());
            }
        }
        if definitions.len() > 2048 {
            return Err("Native complete dataset option capacity exceeded".into());
        }
        let mut reviewed = base.reviewed.as_ref().clone();
        reviewed.definitions = definitions.into_values().collect();
        for d in &mut reviewed.definitions {
            d.blueprint.publications = self.policy.publications.clone();
            d.blueprint.conversion = self.policy.conversion.clone();
            d.blueprint.maximum_lead = self.policy.maximum_lead;
        }
        let pinned = self.apply_scope_references(&mut reviewed.definitions)?;
        let bootstrap = Arc::new(Bootstrap::new(base.broker.clone(), base.scope.clone())?);
        let wanted = reviewed
            .definitions
            .iter()
            .map(|definition| definition.blueprint.contract.contract().to_owned())
            .collect::<BTreeSet<_>>();
        for model in selected {
            bootstrap.register_option_model(model)?;
        }
        if let Some(previous) = &base.reference_bootstrap {
            bootstrap.inherit_selected_models(previous, &wanted)?;
        }
        {
            let retained = self
                .reference_cache
                .lock()
                .map_err(|_| "Native background reference cache interrupted")?;
            if let Some(previous) = retained.as_ref() {
                bootstrap.inherit_selected_models(previous, &wanted)?;
            }
            for contract in &pinned {
                bootstrap.pin_calendar(contract)?;
            }
        }
        let mut all_caches = base
            .caches
            .iter()
            .cloned()
            .map(|c| (c.scope.clone(), c))
            .collect::<BTreeMap<_, _>>();
        for (id, cache) in caches {
            if all_caches.insert(id, cache).is_some() {
                return Err("Conflicting native explicit/background lookup scope".into());
            }
        }
        if all_caches.len() > 64 {
            return Err("Native complete cache scope capacity exceeded".into());
        }
        let symbols = rows.values().map(|r| r.source.clone()).collect::<Vec<_>>();
        let mut source = base.source.clone();
        source.registrations = rows.into_values().collect();
        source.plan = SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions: [Schema::Trades, Schema::Mbp1]
                .into_iter()
                .map(|schema| SubscriptionSpec {
                    schema,
                    symbology: SType::InstrumentId,
                    symbols: symbols.clone(),
                    replay_start_ns: None,
                })
                .collect(),
        };
        source
            .plan
            .validate()
            .map_err(|e| format!("Invalid native background source union: {e:?}"))?;
        Ok(Some(Arc::new(Intent {
            reference_bootstrap: Some(bootstrap),
            date: base.date,
            definition: base.definition,
            background: None,
            source,
            reviewed: Arc::new(reviewed),
            monitored,
            caches: all_caches.into_values().collect(),
            broker: base.broker.clone(),
            scope: base.scope.clone(),
            revision: base.revision,
        })))
    }

    pub(super) fn remember_reference_recipe(&self, active: &Intent) -> Result<(), String> {
        *self
            .reference_cache
            .lock()
            .map_err(|_| "Native background reference cache interrupted")? =
            active.reference_bootstrap.clone();
        Ok(())
    }
}

/// Unite overlapping logical scopes without concealing conflicting references.
fn union_option_models(
    models: Vec<crate::reference::option_contract::OptionContract>,
) -> Result<Vec<crate::reference::option_contract::OptionContract>, String> {
    let mut unique = BTreeMap::new();
    for model in models {
        let contract = model.coverage_definition()?.contract.to_owned();
        if let Some(old) = unique.get(&contract) {
            let old: &crate::reference::option_contract::OptionContract = old;
            if old.value() != model.value() {
                return Err(format!("Conflicting native option references for {contract}"));
            }
        } else {
            unique.insert(contract, model);
        }
    }
    Ok(unique.into_values().collect())
}

#[cfg(test)]
mod model_union_tests {
    use super::*;
    use crate::reference::option_contract::OptionContract;

    fn model() -> serde_json::Value {
        let cases: serde_json::Value = serde_json::from_str(include_str!(
            "../../qualification/ContextWireBridge/reviewed-options.json"
        ))
        .unwrap();
        cases[0]["command"].clone()
    }

    /// Shared browser/policy/monitor contracts need only one physical registration.
    #[test]
    fn overlapping_option_scopes_share_one_physical_model() {
        let model = OptionContract::from_command(&model()).unwrap();
        let merged = union_option_models(vec![model.clone(), model.clone(), model]).unwrap();
        assert_eq!(merged.len(), 1);
        let plan = super::super::raw_options::Plan::from_models(&merged).unwrap();
        assert_eq!(plan.registrations.len(), 2);
        assert_eq!(plan.session.subscriptions.len(), 2);
        assert!(plan.session.subscriptions.iter().all(|s| s.symbols.len() == 2));
    }

    /// Matching canonical names cannot hide changed pricing or provider metadata.
    #[test]
    fn overlapping_option_scopes_reject_conflicting_metadata() {
        for index in [11, 16, 43] {
            let first = model();
            let mut changed = first.clone();
            changed[index] = match index {
                11 => "6001".into(),
                16 => 44.into(),
                _ => 45.into(),
            };
            if index == 11 {
                changed[9] = format!("{:016x}", 6001.0_f64.to_bits()).into();
            }
            let result = union_option_models(vec![
                OptionContract::from_command(&first).unwrap(),
                OptionContract::from_command(&changed).unwrap(),
            ]);
            assert!(result.err().unwrap().contains("Conflicting native option references"));
        }
    }

    /// Different canonical contracts cannot reuse the same provider instrument.
    #[test]
    fn distinct_option_names_still_reject_conflicting_provider_binding() {
        let first = model();
        let mut changed = first.clone();
        changed[0] = "ES20261110C6001".into();
        let merged = union_option_models(vec![
            OptionContract::from_command(&first).unwrap(),
            OptionContract::from_command(&changed).unwrap(),
        ]).unwrap();
        assert_eq!(merged.len(), 2);
        assert!(super::super::raw_options::Plan::from_models(&merged).err().unwrap()
            .contains("Conflicting native provider binding"));
    }
}

/// Retained refresh result, independent of provider/broker data queues.
#[derive(Clone, Default)]
pub struct RefreshStatus {
    /// A source change or unclassified acquisition failure requires replacement.
    pub error: Option<String>,
    /// The first acquisition attempt's original dataset budget.
    pub deadline: Option<Deadline>,
    /// Fully resolved copied replacement metadata. No task, socket, quote or
    /// physical generation is retained; the replacement rebuilds all resources.
    pub replacement: Option<Arc<Intent>>,
}
/// Dataset-tracked background fact refresh. Drop cancels immediately. The native
/// parent tracks and joins its task even if the command/consumer has disappeared.
pub struct Refresh {
    stop: CancellationToken,
    changes: watch::Receiver<RefreshStatus>,
}
impl Facts {
    fn fingerprint(&self) -> Result<[u8; 32], String> {
        self.fingerprint
            .get_or_init(|| self.compute_fingerprint())
            .clone()
    }
    fn compute_fingerprint(&self) -> Result<[u8; 32], String> {
        let mut hash = Sha256::new();
        let policies = self
            .policies
            .iter()
            .map(|policy| Ok((policy.id(), policy.hash()?)))
            .collect::<Result<BTreeMap<_, _>, String>>()?;
        for (id, digest) in policies {
            hash.update(id.as_bytes());
            hash.update(digest.as_bytes());
        }
        for model in &self.models {
            hash.update(model.digest());
        }
        Ok(hash.finalize().into())
    }
}
impl Configuration {
    /// Refreshes the same raw source as C# every thirty seconds. This interval is
    /// a maintenance cadence, not a competing timeout. A changed valid source is
    /// committed before waking the supervisor; unchanged facts never reset the SDK.
    /// Failures retain their first deadline and force fresh acquisition on rebuild.
    ///
    /// # Errors
    /// Invalid ownership, poisoned metadata, or failed tracked task admission.
    pub fn start_refresh(
        self: &Arc<Self>,
        base: Arc<Intent>,
        active: Option<Arc<Intent>>,
        clock: Arc<Clock>,
        parent: Arc<Child>,
    ) -> Result<Refresh, String> {
        self.start_refresh_with_period(base, active, clock, parent, Duration::from_secs(30))
    }
    pub(crate) fn start_refresh_with_period(
        self: &Arc<Self>,
        base: Arc<Intent>,
        active: Option<Arc<Intent>>,
        clock: Arc<Clock>,
        parent: Arc<Child>,
        period: Duration,
    ) -> Result<Refresh, String> {
        let date = base.date;
        if period.is_zero() {
            return Err("Invalid native background refresh cadence".into());
        }
        let initial = self
            .facts
            .lock()
            .map_err(|_| "Native background fact cache interrupted")?
            .as_ref()
            .filter(|(known, _)| *known == date)
            .map(|(_, facts)| facts.fingerprint())
            .transpose()?
            .ok_or("Native background source not initialized")?;
        let physical = self
            .committed_book()?
            .map(|book| book.physical_signature())
            .transpose()?;
        let (state, changes) = watch::channel(RefreshStatus::default());
        let stop = parent.cancellation().child_token();
        let stopping = stop.clone();
        let config = self.clone();
        let task_parent = parent.clone();
        parent.spawn(async move{
            let result=std::panic::AssertUnwindSafe(async {
                let mut committed=config.committed.as_ref().map(|directory|directory.changes());
                let health=config.market.upgrade().map(|market|market.health(Lane::OptionsGlbx));
                let mut health_changes=health.as_ref().map(|health|health.subscribe());
                let mut quote_changes=config.quote_changes.subscribe();
                quote_changes.mark_changed();
                // Unrelated health traffic must not renew a maintenance sleep.
                let mut maintenance_at=tokio::time::Instant::now()+period;
                let mut pending_coverage = None;
                let mut capacity_retry_at = None;
                loop {
                    let event=tokio::select!{biased;
                        ()=stopping.cancelled()=>return Ok(()),
                        ()=tokio::time::sleep_until(maintenance_at)=>0,
                        ()=async{match &mut committed{Some(changes)=>{let _=changes.changed().await;},None=>std::future::pending().await}}=>1,
                        ()=async{match &mut health_changes{Some(changes)=>{let _=changes.changed().await;},None=>std::future::pending().await}}=>2,
                        result=quote_changes.changed()=>{result.map_err(|_|"Background quote observation ended")?;3},
                    };
                    let maintenance=event==0;
                    // A rejected optional proposal cannot churn the live feed or
                    // replan once per quote. Explicit committed handoffs still run.
                    if event!=1 && capacity_retry_at.is_some_and(|at|tokio::time::Instant::now()<at){continue;}
                    if event==3 {
                        // Coalesce market ticks; metadata IO and replanning never
                        // run once per quote or hold the provider writer.
                        tokio::select! {biased;()=stopping.cancelled()=>return Ok(()),()=tokio::time::sleep(Duration::from_secs(1))=>{}}
                        quote_changes.borrow_and_update();
                    }
                    if maintenance {maintenance_at=tokio::time::Instant::now()+period;}
                    let pending=config.committed.as_ref().map(|directory|directory.pending_deadline()).transpose()?.flatten();
                    let deadline=pending.unwrap_or_else(Deadline::recovery);
                    let mut replacement = None;
                    let mut replacement_reason = "effective option coverage";
                    let attempt:Result<bool,String>=async{
                        let mut changed = false;
                        if let Some(directory)=&config.committed {
                            let current=directory.current()?;
                            let acknowledged=directory.accepted()?.map(|book|directory.acknowledged(&book)).transpose()?.unwrap_or(false);
                            if !current||!acknowledged {
                                let ready=health.as_ref().is_some_and(|health|health.status().phase==crate::runtime::recovery::Phase::Running);
                                if !config.refresh_committed_in_place(task_parent.clone(),&stopping,deadline,physical,ready).await?{changed=true;}
                            }
                        }
                        if !maintenance && !changed && event!=3{return Ok(true);}
                        let facts=config.source_facts(date,task_parent.clone(),&stopping,deadline,maintenance).await?;
                        let facts_changed=facts.fingerprint()?!=initial;
                        // Browser metadata/selection can change without changing
                        // provider membership. Resolve it, then compare the actual
                        // effective subscriptions and cache policies below.
                        let _ = config.refresh_browsers(date,task_parent.clone(),&stopping,deadline).await?;
                        let next=config.resolve(base.clone(),task_parent.clone(),clock.clone(),&stopping,deadline).await?;
                        let unchanged = !changed && match (&active,&next){
                            (None,None)=>true,
                            (Some(old),Some(new))=>{
                                let definitions_changed = facts_changed && !same_active_definitions(old,new);
                                let background_only = same_monitored_registrations(old,new) && !definitions_changed;
                                let registration_changed = old.source.registrations != new.source.registrations;
                                let caches_changed = old.caches != new.caches;
                                if background_only && (registration_changed || caches_changed) {
                                    // Rebuilding clears the current generation's pending cache work.
                                    // Quiet markets must be able to finish that work on the existing
                                    // subscriptions, even when the next strike estimate is different.
                                    let published = config.market.upgrade()
                                        .and_then(|market| market.options_current())
                                        .map(|view| view.caches.lock().map(|caches|
                                            old.caches.iter().all(|scope| caches.contains_key(&scope.scope)))
                                            .map_err(|_| "Native cache directory interrupted"))
                                        .transpose()?.unwrap_or(false);
                                    if !background_coverage_confirmed(&mut pending_coverage,
                                        &new.source.registrations, &new.caches, maintenance, old.caches.is_empty() || published) {
                                        return Ok(true);
                                    }
                                }
                                if registration_changed {
                                    replacement_reason = "provider registration changed";
                                    false
                                } else if caches_changed {
                                    replacement_reason = "prepared cache scope changed";
                                    false
                                } else if definitions_changed {
                                    replacement_reason = "active pricing definition changed";
                                    false
                                } else {
                                    pending_coverage = None;
                                    true
                                }
                            },
                            _=>false,
                        };
                        if changed {replacement_reason = "committed physical handoff changed";}
                        replacement=next;
                        Ok(unchanged)
                    }.await;
                    if stopping.is_cancelled(){return Ok(());}
                    match attempt {
                        Ok(true)=>{capacity_retry_at=None;state.send_modify(|_|{});},
                        Ok(false)=>{
                            let mut status=replacement_status(replacement,pending);
                            if let Some(error)=&mut status.error {
                                error.push_str(": ");
                                error.push_str(replacement_reason);
                            }
                            state.send_replace(status);return Ok(());
                        },
                        Err(error)=>{
                            // The accepted recipe may still be waiting for quiet
                            // pricing/publication evidence. Its own readiness and
                            // failures remain authoritative; optional expansion
                            // must not destroy it before that evidence arrives.
                            if retain_active_on_capacity(&error,active.is_some(),pending.is_some()) {
                                report_capacity_rejection(&config.setup_diagnostics,&error,&clock)?;
                                capacity_retry_at=Some(tokio::time::Instant::now()+period);
                                maintenance_at=tokio::time::Instant::now()+period;
                                continue;
                            }
                            *config.refresh_incident.lock().map_err(|_|"Native background refresh status interrupted")?=Some(deadline);
                            state.send_replace(RefreshStatus{error:Some(error),deadline:Some(deadline),replacement:None});return Ok(());
                        }
                    }
                }
            }).catch_unwind().await;
            if !stopping.is_cancelled(){
                let error=match result{Ok(Ok(()))=>None,Ok(Err(error))=>Some(error),Err(_)=>Some("Native background refresh panicked".into())};
                if let Some(error)=error{state.send_replace(RefreshStatus{error:Some(error),deadline:Some(Deadline::recovery()),replacement:None});}
            }
        }).map_err(|_|"Native background refresh task admission failed")?;
        Ok(Refresh { stop, changes })
    }
}

/// Only optional expansion may retain a qualified active recipe; initialization,
/// committed handoffs and identity/reference failures remain strict failures.
fn retain_active_on_capacity(error: &str, active: bool, committed_pending: bool) -> bool {
    active && !committed_pending
        && error.starts_with("Native selected background option capacity exceeded")
}

/// Reports rejected background work without falsely declaring that the live
/// transport or its accepted subscriptions have failed.
fn report_capacity_rejection(diagnostics: &SetupDiagnostics, error: &str, clock: &Clock) -> Result<(), String> {
    let binding = diagnostics.lock().map_err(|_| "Native setup diagnostics interrupted")?.clone();
    if let Some((sink, worker, generation, revision)) = binding
        && let Some(sink) = sink.upgrade()
    {
        sink.retain(super::live::Diagnostic {
            worker, lane: Lane::OptionsGlbx, attempt: revision,
            observed: clock.utc_ticks().map_err(|_| "Native setup clock unavailable")?, replaced: 0,
            event: super::transport::Event::System { code: u8::MAX, message: format!(
                "Background coverage proposal rejected; keeping current qualified subscriptions; {}; generation={}",
                error, crate::messaging::wire::guid(generation)) },
        })?;
    }
    Ok(())
}

#[cfg(test)]
mod capacity_rejection_tests {
    use super::retain_active_on_capacity;

    #[test]
    fn oversized_optional_expansion_retains_live_subscriptions_only() {
        let error="Native selected background option capacity exceeded: selected=2300; maximum=2048";
        assert!(retain_active_on_capacity(error,true,false));
        assert!(!retain_active_on_capacity(error,false,false));
        assert!(!retain_active_on_capacity(error,true,true));
        assert!(!retain_active_on_capacity("Provider/IFM identity collision",true,false));
        assert!(!retain_active_on_capacity("Conflicting immutable reference recipes for a shared option",true,false));
        assert!(!retain_active_on_capacity("Native reference bootstrap original deadline expired",true,false));
    }
}

fn background_coverage_confirmed(
    pending: &mut Option<(Vec<Registration>, Vec<CacheScope>)>,
    next: &[Registration],
    caches: &[CacheScope],
    maintenance: bool,
    active_coverage_published: bool,
) -> bool {
    if !active_coverage_published {
        *pending = None;
        return false;
    }
    if pending.as_ref().is_some_and(|(registrations, scopes)| registrations == next && scopes == caches) {
        return maintenance;
    }
    *pending = maintenance.then(|| (next.to_vec(), caches.to_vec()));
    false
}

fn same_active_definitions(old: &Intent, new: &Intent) -> bool {
    let old = &old.reviewed.definitions;
    let new = &new.reviewed.definitions;
    old.len() == new.len()
        && old.iter().zip(new).all(|(old, new)| {
            old.instrument == new.instrument
                && old.underlying == new.underlying
                && old.blueprint.contract.value() == new.blueprint.contract.value()
                && old.blueprint.calendar.value() == new.blueprint.calendar.value()
        })
}

fn same_monitored_registrations(old: &Intent, new: &Intent) -> bool {
    old.monitored == new.monitored
        && old
            .source
            .registrations
            .iter()
            .filter(|row| old.monitored.contains(&row.instrument))
            .eq(new.source.registrations.iter().filter(|row| new.monitored.contains(&row.instrument)))
}

impl Refresh {
    /// Read-only event-driven status; never holds source resources or a lease.
    pub fn changes(&self) -> watch::Receiver<RefreshStatus> {
        self.changes.clone()
    }
}
impl Drop for Refresh {
    fn drop(&mut self) {
        self.stop.cancel();
    }
}

// A healthy-provider quiet wait precedes a planned replacement, not a dataset
// failure. Start its budget when the complete replacement is ready. An explicit
// pending business handoff keeps its original deadline, including when expired.
fn replacement_status(
    replacement: Option<Arc<Intent>>,
    pending: Option<Deadline>,
) -> RefreshStatus {
    RefreshStatus {
        error: Some(
            "Native background policy/coverage changed; replacing dataset resources".into(),
        ),
        deadline: Some(pending.unwrap_or_else(Deadline::recovery)),
        replacement,
    }
}

// The supervisor never starts another factory after a deadline expires until an
// explicit command opens a new episode. A positive incoming budget after an
// expired saved incident therefore denotes a new authorized episode, not a retry
// that can renew the original one. Known failed acquisition must still refresh.
fn refresh_budget(
    state: &Mutex<Option<Deadline>>,
    incoming: Deadline,
) -> Result<(bool, Deadline), String> {
    let mut pending = state
        .lock()
        .map_err(|_| "Native background refresh status interrupted")?;
    if pending.is_some_and(|old| old.remaining().is_zero()) && !incoming.remaining().is_zero() {
        *pending = Some(incoming);
    }
    Ok((
        pending.is_some(),
        pending.map_or(incoming, |old| old.earliest(incoming)),
    ))
}
/// A strategy with no listed expiry has no background candidates for this date.
/// Keep monitored positions and other strategies alive; malformed or incomplete
/// source facts still fail qualification rather than admitting partial coverage.
fn background_universes(planned: ResultSet) -> Result<Vec<Universe>, String> {
    if planned.reason.is_empty() || planned.reason == "NoEligibleExpiration" {
        Ok(planned.universes)
    } else {
        Err(format!("Native background coverage unavailable: {}", planned.reason))
    }
}

#[cfg(test)]
mod recovery_budget_tests {
    use super::*;
    #[test]
    fn background_membership_requires_two_scheduled_reads_with_the_same_registrations() {
        let registration = |instrument: u32| Registration {
            instrument,
            publisher: 1,
            contract: instrument.to_string(),
            source: instrument.to_string(),
            asset: Asset::Option,
            tick: None,
            vwap: false,
            required: false,
        };
        let first = [registration(1)];
        let second = [registration(2)];
        let mut pending = None;

        assert!(!background_coverage_confirmed(&mut pending, &first, &[], false, true));
        assert!(!background_coverage_confirmed(&mut pending, &first, &[], true, true));
        assert!(!background_coverage_confirmed(&mut pending, &first, &[], false, true));
        assert!(!background_coverage_confirmed(&mut pending, &second, &[], false, true));
        assert!(!background_coverage_confirmed(&mut pending, &first, &[], true, true));
        assert!(!background_coverage_confirmed(&mut pending, &second, &[], true, true));
        assert!(background_coverage_confirmed(&mut pending, &second, &[], true, true));
        assert!(!background_coverage_confirmed(&mut pending, &second, &[], true, false));
        assert!(!background_coverage_confirmed(&mut pending, &second, &[], true, false));
        assert!(!background_coverage_confirmed(&mut pending, &second, &[], true, true));
    }

    #[tokio::test]
    async fn unlisted_strategy_expiry_does_not_fail_other_background_coverage() {
        use crate::option_chain::coverage::Definition;
        use crate::reference::decimal;
        let cases: serde_json::Value = serde_json::from_str(include_str!(
            "../../qualification/ContextWireBridge/strategy-policy.json"
        )).unwrap();
        let mut policy = strategy::from_command(&cases[0]["command"]["parameters"]).unwrap();
        policy.enabled = true;
        for bias in &mut policy.biases {
            bias.enabled = true;
            bias.minimum = 5;
            bias.maximum = 10;
            bias.preferred = 5;
        }
        let date = time::Date::from_calendar_date(2026, time::Month::November, 2).unwrap();
        let expiry_ns = |day| chrono::NaiveDate::from_ymd_opt(2026, 11, day).unwrap()
            .and_hms_opt(21, 0, 0).unwrap().and_utc().timestamp_nanos_opt().unwrap() as u64;
        let forward = decimal::parse("6000").unwrap();
        let definitions = [
            Definition { contract: "first-friday", root: "ES", underlying: "ES20261218",
                strike: forward, expiration: Some(expiry_ns(6)) },
            Definition { contract: "second-friday", root: "ES", underlying: "ES20261218",
                strike: forward, expiration: Some(expiry_ns(13)) },
        ];
        let prices = BTreeMap::from([("ES20261218".to_owned(), forward)]);
        let stop = CancellationToken::new();
        let planner = Planner::default();
        let plan = |policy: &strategy::Parameters| planner.plan_available(
            policy, date, &definitions, true, forward, 0.2,
            Zone::new("Eastern Standard Time").unwrap(), Some(&prices), &stop, Deadline::recovery()
        ).unwrap();
        let missing = plan(&policy);
        assert_eq!(missing.reason, "NoEligibleExpiration");
        assert!(background_universes(missing).unwrap().is_empty());
        for bias in &mut policy.biases {
            bias.minimum = 4;
            bias.maximum = 11;
        }
        assert!(!background_universes(plan(&policy)).unwrap().is_empty());
        assert!(background_universes(ResultSet {
            universes: vec![], reason: "IncompleteDefinitions"
        }).is_err());
    }
    #[tokio::test(start_paused = true)]
    async fn planned_change_starts_budget_after_quiet_wait_but_preserves_explicit_handoff() {
        let explicit = Deadline::recovery();
        tokio::time::advance(Duration::from_secs(180)).await;
        let planned = replacement_status(None, None);
        assert_eq!(
            planned.deadline.unwrap().remaining(),
            Duration::from_secs(120)
        );
        let handoff = replacement_status(None, Some(explicit));
        assert!(handoff.deadline.unwrap().remaining().is_zero());
        assert!(planned.replacement.is_none());
    }
    #[tokio::test(start_paused = true)]
    async fn automatic_refresh_reuses_original_deadline_and_explicit_new_episode_can_retry() {
        let state = Mutex::new(Some(Deadline::recovery()));
        tokio::time::advance(Duration::from_secs(10)).await;
        let (fresh, remaining) = refresh_budget(&state, Deadline::recovery()).unwrap();
        assert!(fresh);
        assert_eq!(remaining.remaining(), Duration::from_secs(110));
        tokio::time::advance(Duration::from_secs(110)).await;
        let (_, expired) = refresh_budget(&state, Deadline::after(Duration::ZERO)).unwrap();
        assert!(expired.remaining().is_zero());
        let (fresh, new_episode) = refresh_budget(&state, Deadline::recovery()).unwrap();
        assert!(fresh);
        assert_eq!(new_episode.remaining(), Duration::from_secs(120));
    }
}

#[cfg(test)]
mod fingerprint_performance {
    use super::*;

    /// The cached result must retain the exact original ordered source fingerprint.
    #[test]
    fn immutable_fact_fingerprint_keeps_original_identity() {
        let cases: serde_json::Value = serde_json::from_str(include_str!(
            "../../qualification/ContextWireBridge/strategy-policy.json"
        ))
        .unwrap();
        let facts = Facts {
            fingerprint: std::sync::OnceLock::new(),
            models: vec![],
            policies: cases
                .as_array()
                .unwrap()
                .iter()
                .map(|row| {
                    Arc::new(
                        crate::option_chain::strategy::from_command(&row["command"]["parameters"])
                            .unwrap(),
                    )
                })
                .collect(),
        };
        assert_eq!(
            facts.fingerprint().unwrap(),
            facts.compute_fingerprint().unwrap()
        );
        assert_eq!(
            facts.fingerprint().unwrap(),
            facts.compute_fingerprint().unwrap()
        );
    }

    /// Run explicitly without competing compilers; compare the actual old computation and its immutable cache.
    #[test]
    #[ignore = "Explicit performance run after compilation ends"]
    fn immutable_fact_fingerprint_before_after_timings() {
        use std::{hint::black_box, time::Instant};
        let cases: serde_json::Value = serde_json::from_str(include_str!(
            "../../qualification/ContextWireBridge/strategy-policy.json"
        ))
        .unwrap();
        let facts = Facts {
            fingerprint: std::sync::OnceLock::new(),
            models: vec![],
            policies: cases
                .as_array()
                .unwrap()
                .iter()
                .map(|row| {
                    Arc::new(
                        crate::option_chain::strategy::from_command(&row["command"]["parameters"])
                            .unwrap(),
                    )
                })
                .collect(),
        };
        facts.fingerprint().unwrap();
        let mut samples = [Vec::new(), Vec::new()];
        for pair in 0..5 {
            let order = if pair % 2 == 0 { [0, 1] } else { [1, 0] };
            for variant in order {
                let start = Instant::now();
                for _ in 0..10000 {
                    black_box(if variant == 0 {
                        facts.compute_fingerprint()
                    } else {
                        facts.fingerprint()
                    })
                    .unwrap();
                }
                samples[variant].push(start.elapsed().as_nanos() as u64);
            }
        }
        println!(
            "FACTS_PERFORMANCE {}",
            serde_json::json!({"scenario":"background-fingerprint","operationsPerSample":10000,"policies":facts.policies.len(),"note":"Uncached versus cached fingerprint with current policy hashing in both arms","beforeNs":samples[0],"afterNs":samples[1]})
        );
    }
}
