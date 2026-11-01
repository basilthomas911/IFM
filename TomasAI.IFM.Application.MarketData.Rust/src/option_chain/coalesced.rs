//! Latest-state option calculations, ported from CoalescedOptionChainPricing.
//! Ingress only replaces one fixed quote. Selection and full risk have separate
//! sweeps, and numerical work never holds the registry lock. Immutable context
//! identities fence results computed during a refresh or source replacement.
use super::qualification::{self, Quote};
use crate::{
    pricing::{Calculator, Failure as NumericalFailure, PriceDelta, Request, Settings, Values},
    runtime::Deadline,
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
    time::Duration,
};
use tokio::sync::Mutex as AsyncMutex;
use tokio_util::sync::CancellationToken;

/// Qualified immutable application context. Reference/calendar and semantic hash
/// preparation remain application policy; source validation/calculation is native.
#[derive(Clone, Copy, Debug, PartialEq)]
pub struct Context {
    /// Nonzero identity changed whenever any context/reference field changes.
    pub identity: u128,
    /// Underlying provider instrument, resolved by the application catalog.
    pub underlying: u32,
    /// Fixed quote validator template; quotes and valuation time are replaced.
    pub qualification: qualification::Request,
    /// Frozen model conventions/strike/rate; forward and year fraction are replaced.
    pub model: Request,
    /// Exact expiration in UTC ticks since Unix epoch.
    pub expiration: i64,
    /// Reviewed Actual/365 Fixed or Actual/360 denominator.
    pub days_in_year: u16,
    /// Last instant for which immutable reference qualification remains valid.
    pub valid_until: i64,
}
impl Context {
    fn validate(&self) -> Result<(), &'static str> {
        if self.identity == 0
            || self.underlying == 0
            || self.qualification.size != 304
            || !(-621355968000000000..=2534023007999999999).contains(&self.expiration)
            || !(-621355968000000000..=2534023007999999999).contains(&self.valid_until)
            || self.qualification.flags > 31
            || self.qualification.flags & 3 != 3
            || (self.qualification.flags & 8 != 0) != self.model.call
            || self.qualification.reserved != 0
            || (self.qualification.generation_low == 0 && self.qualification.generation_high == 0)
            || !(1..=5000).contains(&self.qualification.maximum_age)
            || self.qualification.maximum_skew > 2000
            || self.qualification.maximum_lead > 2000
            || !self.model.strike.is_finite()
            || self.model.strike <= 0.0
            || !self.model.rate.is_finite()
            || decimal_double(self.qualification.strike) != Some(self.model.strike)
            || self
                .qualification
                .definition_strike
                .compare(qualification::Decimal::default())
                .is_none()
            || !matches!(self.days_in_year, 360 | 365)
        {
            return Err("Invalid prepared option context");
        }
        Ok(())
    }
}
/// Compare-and-swap of one immutable reference context in a complete scope batch.
pub struct Refresh {
    /// Registered physical option identity.
    pub instrument: u32,
    /// Context identity observed by the application before preparing this batch.
    pub expected: u128,
    /// New qualified context, with a distinct immutable identity.
    pub context: Context,
}
/// Complete business/display demand item, fenced by immutable pricing context.
/// Demand changes do not alter physical subscriptions or solve on admission.
pub struct RiskRequirement {
    /// Existing qualified provider instrument.
    pub instrument: u32,
    /// Exact context accepted by the owning application scope.
    pub context: u128,
    /// Whether full risk is required in addition to selection values.
    pub required: bool,
}
/// Immutable source-time reference version retained by one actual trade. Later
/// context refresh/reset cannot relabel its numerical or application metadata.
#[derive(Clone, Debug)]
pub struct TradeContext {
    /// Exact prepared numerical/qualification inputs at source processing.
    pub context: Arc<Context>,
    /// Opaque application context for durable evidence and semantic hashing.
    pub manifest: Option<Arc<super::manifest::Manifest>>,
    /// Complete immutable native references captured at source time. A later
    /// dataset reset or context refresh cannot rewrite an accepted trade.
    pub reference: Option<Arc<super::context::Context>>,
}
/// Explicit calculation failure, distinct from an empty/unobserved input.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Required underlying observation is absent.
    UnderlyingUnavailable,
    /// Qualified reference context has expired and needs application refresh.
    ContextExpired,
    /// Exact native quote validator result, preserving its first-failure order.
    Quote(qualification::Result),
    /// Existing numerical calculation failure.
    Numerical(NumericalFailure),
    /// A cached source observation is no longer current/fresh.
    Stale,
    /// A newer option observation superseded the cached full-risk calculation.
    CalculationPending,
}
/// One immutable coalesced selection or risk result with its exact source inputs.
#[derive(Debug, Clone)]
pub struct Calculation {
    /// Context identity used for this result.
    pub context: u128,
    /// Native physical source generation, separate from logical pricing generation.
    pub source_generation: u128,
    /// Captured API valuation time in UTC ticks.
    pub at: i64,
    /// Exact underlying observation used for calculation.
    pub underlying: Quote,
    /// Exact option observation used for calculation.
    pub option: Quote,
    /// Reused IV observation time, absent when no IV has converged.
    pub iv_event: Option<i64>,
    /// Complete immutable IV provenance, which may precede the current quote.
    pub iv: Option<IvObservation>,
    /// Exact qualified year fraction used by this numerical calculation.
    pub time: f64,
    /// Selection-only price/delta; full risk is a distinct calculation.
    pub selection: Option<PriceDelta>,
    /// Complete IV/Greek calculation, only present on a successful risk pass.
    pub risk: Option<Values>,
    /// Explicit financial/source failure; no manufactured zero Greeks.
    pub failure: Option<Failure>,
}
/// Complete retained IV solve evidence. Selection may reuse this observation
/// while pricing a more recent quote; its original inputs never get rewritten.
#[derive(Clone, Copy, Debug)]
pub struct IvObservation {
    /// Immutable context identity of this IV solve.
    pub context: u128,
    /// Option event time used by the existing refresh/freshness comparisons.
    pub event: i64,
    /// Converged volatility.
    pub volatility: f64,
    /// Exact underlying input used by the solve, not a later replacement.
    pub underlying: Quote,
    /// Exact option input used by the solve.
    pub option: Quote,
    /// Captured API valuation time of the solve.
    pub at: i64,
}
type Iv = IvObservation;
struct Entry {
    reference: Option<Arc<super::context::Context>>,
    manifest: Option<Arc<super::manifest::Manifest>>,
    context: Arc<Context>,
    physical: u128,
    quote: Option<Quote>,
    observed: Option<(i64, i64)>,
    iv: Option<Iv>,
    selection: Option<Arc<Calculation>>,
    risk: Option<Arc<Calculation>>,
    required: bool,
}
struct State {
    entries: BTreeMap<u32, Entry>,
    memberships: Option<[Arc<Vec<u32>>; 2]>,
    selection_cursor: usize,
    risk_cursor: usize,
    observations: u64,
    selections: u64,
    solves: u64,
}
/// Bounded native option scheduler state. It does not create detached tasks;
/// the enclosing dataset owner drives sweeps using its tracked cancellation.
pub struct Coalesced {
    fenced: std::sync::atomic::AtomicBool,
    maximum: usize,
    calculator: Calculator,
    batch: usize,
    iv_ticks: i64,
    policy: [u64; 4],
    state: Mutex<State>,
    selection_pass: AsyncMutex<()>,
    risk_pass: AsyncMutex<()>,
    changes: tokio::sync::watch::Sender<u64>,
    reference_updates: tokio::sync::watch::Sender<u64>,
    reference_incident: Mutex<Option<Deadline>>,
}
impl Coalesced {
    /// Creates a finite registry using the reviewed application refresh limits.
    /// IV interval is milliseconds (selection cadence..60000); scheduling policy
    /// approval is checked before this owner is installed in a production scope.
    pub fn new(maximum: usize, batch: usize, iv_milliseconds: u32) -> Result<Self, &'static str> {
        Self::with_policy(
            maximum,
            batch,
            iv_milliseconds,
            "OptionRefresh/TestDefaults/v1",
        )
    }
    /// Binds the exact reviewed refresh-policy version to retained IV evidence.
    /// Numerical policy remains separately frozen in its application digest.
    pub fn with_policy(
        maximum: usize,
        batch: usize,
        iv_milliseconds: u32,
        version: &str,
    ) -> Result<Self, &'static str> {
        if version.trim().is_empty() || version.len() > 1024 {
            return Err("Invalid refresh policy identity");
        }
        if !(1..=2048).contains(&maximum)
            || !(1..=64).contains(&batch)
            || !(10..=60000).contains(&iv_milliseconds)
        {
            return Err("Invalid option refresh bounds");
        }
        Ok(Self {
            fenced: std::sync::atomic::AtomicBool::new(false),
            maximum,
            calculator: Calculator::new(Settings::default())
                .map_err(|_| "Invalid numerical policy")?,
            batch,
            iv_ticks: i64::from(iv_milliseconds) * 10000,
            policy: policy_identity(version),
            state: Mutex::new(State {
                entries: BTreeMap::new(),
                memberships: None,
                selection_cursor: 0,
                risk_cursor: 0,
                observations: 0,
                selections: 0,
                solves: 0,
            }),
            selection_pass: AsyncMutex::new(()),
            risk_pass: AsyncMutex::new(()),
            changes: tokio::sync::watch::channel(0).0,
            reference_updates: tokio::sync::watch::channel(0).0,
            reference_incident: Mutex::new(None),
        })
    }
    /// Atomically installs/refreshes a qualified context. A new physical source
    /// clears all old observations; a reference refresh invalidates cached IV/risk.
    /// Admission performs no provider I/O and never opens an unbounded quote queue.
    pub fn register(
        &self,
        instrument: u32,
        physical: u128,
        context: Context,
        required: bool,
    ) -> Result<(), &'static str> {
        if instrument == 0 || physical == 0 {
            return Err("Invalid prepared option context");
        }
        context.validate()?;
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        if !state.entries.contains_key(&instrument) && state.entries.len() == self.maximum {
            return Err("Option pricing capacity exceeded");
        }
        if let Some(entry) = state.entries.get_mut(&instrument) {
            if entry.physical != physical {
                entry.quote = None;
                entry.observed = None;
            }
            // Identity is immutable. The application assigns another identity on
            // refresh; even same-identity re-registration clears computed state.
            entry.context = Arc::new(context);
            entry.manifest = None;
            entry.reference = None;
            entry.physical = physical;
            entry.required = required;
            entry.iv = None;
            entry.selection = None;
            entry.risk = None;
        } else {
            state.entries.insert(
                instrument,
                Entry {
                    reference: None,
                    manifest: None,
                    context: Arc::new(context),
                    physical,
                    quote: None,
                    observed: None,
                    iv: None,
                    selection: None,
                    risk: None,
                    required,
                },
            );
        }
        state.memberships = None;
        Ok(())
    }
    /// Event-driven demand to rebuild expired native reference assumptions.
    pub fn reference_updates(&self) -> tokio::sync::watch::Receiver<u64> {
        self.reference_updates.subscribe()
    }
    /// Existing original reference incident, shared with native pricing recovery.
    pub fn reference_deadline(&self) -> Result<Option<Deadline>, &'static str> {
        Ok(*self
            .reference_incident
            .lock()
            .map_err(|_| "Native reference incident interrupted")?)
    }
    /// Starts one native reference incident without renewing a prior deadline.
    pub fn begin_reference_recovery(&self) -> Result<Deadline, &'static str> {
        Ok(*self
            .reference_incident
            .lock()
            .map_err(|_| "Native reference incident interrupted")?
            .get_or_insert_with(Deadline::recovery))
    }
    /// Merges provider/reference recovery without extending either original window.
    pub fn limit_reference_recovery(&self, original: Deadline) -> Result<Deadline, &'static str> {
        let mut incident = self
            .reference_incident
            .lock()
            .map_err(|_| "Native reference incident interrupted")?;
        let deadline = incident.map_or(original, |prior| prior.earliest(original));
        *incident = Some(deadline);
        Ok(deadline)
    }
    /// Clears a reference incident only after fresh complete pricing is proven.
    pub fn complete_reference_recovery(&self, at: i64) -> Result<bool, &'static str> {
        if !self.ready(at)? {
            return Ok(false);
        }
        *self
            .reference_incident
            .lock()
            .map_err(|_| "Native reference incident interrupted")? = None;
        Ok(true)
    }
    /// Bounded complete reference status and next explicit validity boundary.
    pub fn reference_window(&self, at: i64) -> Result<(bool, i64), &'static str> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        let mut valid = true;
        let mut until = i64::MAX;
        for entry in state.entries.values() {
            if let Some(reference) = &entry.reference {
                valid &= reference.validate_at(at).is_ok();
                until = until.min(reference.valid_until());
            }
        }
        Ok((valid, until))
    }
    /// Detached immutable-context fences for one complete native refresh.
    pub fn context_identities(&self) -> Result<BTreeMap<u32, u128>, &'static str> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?
            .entries
            .iter()
            .map(|(id, e)| (*id, e.context.identity))
            .collect())
    }
    /// Attaches complete native reference authority before provider admission.
    /// It must produce the exact already-installed numerical template; a copied
    /// manifest alone cannot qualify calendar/tenor validity on subsequent passes.
    ///
    /// # Errors
    /// Identity mismatches or different model/reference inputs are rejected.
    pub fn attach_reference(
        &self,
        instrument: u32,
        expected: u128,
        reference: Arc<super::context::Context>,
    ) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        let entry = state
            .entries
            .get_mut(&instrument)
            .ok_or("Option registration absent")?;
        let current = &entry.context;
        if current.identity != expected
            || reference.bind(
                current.qualification.at,
                expected,
                current.underlying,
                current.qualification.strike,
                current.model.call,
            )? != **current
        {
            return Err("Native reference does not match numerical context");
        }
        entry.reference = Some(reference);
        Ok(())
    }
    /// Atomically refreshes the entire qualified reference batch. Every compare,
    /// physical-definition check and quote-limit restriction succeeds before any
    /// entry changes. Current quotes stay available; old IV/risk is invalidated.
    /// In-progress calculations fail their immutable-context commit comparison.
    pub fn refresh(&self, updates: &[Refresh]) -> Result<(), &'static str> {
        self.refresh_prepared(updates, &BTreeMap::new())
    }
    /// Atomically refreshes reference assumptions and their complete opaque
    /// evidence manifests. Queued trades retain earlier versions through RAII;
    /// an incomplete replacement cannot associate old metadata with a new model.
    pub fn refresh_prepared(
        &self,
        updates: &[Refresh],
        manifests: &BTreeMap<u32, Arc<super::manifest::Manifest>>,
    ) -> Result<(), &'static str> {
        self.refresh_references(updates, manifests, &BTreeMap::new())
    }
    /// Replaces the complete native reference batch atomically with numerical
    /// templates and queued-trade manifests. A partial/failed rebuild leaves the
    /// old batch intact; it cannot regain validity merely by changing a digest.
    ///
    /// # Errors
    /// Requires complete matching typed references and immutable-context fences.
    pub fn refresh_references(
        &self,
        updates: &[Refresh],
        manifests: &BTreeMap<u32, Arc<super::manifest::Manifest>>,
        references: &BTreeMap<u32, Arc<super::context::Context>>,
    ) -> Result<(), &'static str> {
        if updates.is_empty() || updates.len() > self.maximum {
            return Err("Invalid context refresh batch");
        }
        let mut prepared = BTreeMap::new();
        if !references.is_empty() && references.len() != updates.len() {
            return Err("Native reference replacement must include the complete scope");
        }
        for update in updates {
            update.context.validate()?;
            if let Some(reference) = references.get(&update.instrument) {
                let current = update.context;
                if reference.bind(
                    current.qualification.at,
                    current.identity,
                    current.underlying,
                    current.qualification.strike,
                    current.model.call,
                )? != current
                {
                    return Err("Native reference does not match numerical context");
                }
            }
            if update.expected == 0
                || update.expected == update.context.identity
                || prepared
                    .insert(
                        update.instrument,
                        (update.expected, Arc::new(update.context)),
                    )
                    .is_some()
            {
                return Err("Invalid context refresh identity");
            }
        }
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        if prepared.len() != state.entries.len() {
            return Err("Context refresh must include the complete scope");
        }
        if !manifests.is_empty() && manifests.len() != prepared.len() {
            return Err("Reference manifest replacement must include the complete scope");
        }
        for (instrument, (expected, next)) in &prepared {
            let old = state
                .entries
                .get(instrument)
                .ok_or("Context refresh cannot create a physical registration")?;
            let prior = &old.context;
            if old.reference.is_some() && !references.contains_key(instrument) {
                return Err("Native reference refresh requires complete replacement authority");
            }
            if manifests
                .get(instrument)
                .is_some_and(|manifest| manifest.identity() != next.identity)
                || old.manifest.is_some() && !manifests.contains_key(instrument)
            {
                return Err("Reference manifest context compare-and-swap conflict");
            }
            if prior.identity != *expected {
                return Err("Pricing context compare-and-swap conflict");
            }
            if next.underlying != prior.underlying
                || next.expiration != prior.expiration
                || next.days_in_year != prior.days_in_year
                || next.model.strike != prior.model.strike
                || next.model.call != prior.model.call
                || next.model.american != prior.model.american
                || next.model.futures_style != prior.model.futures_style
                || next.qualification.flags != prior.qualification.flags
                || next.qualification.generation_low != prior.qualification.generation_low
                || next.qualification.generation_high != prior.qualification.generation_high
                || next
                    .qualification
                    .strike
                    .compare(prior.qualification.strike)
                    != Some(std::cmp::Ordering::Equal)
                || next
                    .qualification
                    .definition_strike
                    .compare(prior.qualification.definition_strike)
                    != Some(std::cmp::Ordering::Equal)
            {
                return Err("Context refresh cannot change physical definitions");
            }
            if next.qualification.maximum_age > prior.qualification.maximum_age
                || next.qualification.maximum_skew > prior.qualification.maximum_skew
            {
                return Err("Context refresh cannot relax quote validity limits");
            }
        }
        for (instrument, (_, context)) in prepared {
            let entry = state
                .entries
                .get_mut(&instrument)
                .expect("complete batch validated under writer");
            entry.context = context;
            entry.manifest = manifests.get(&instrument).cloned();
            entry.reference = references.get(&instrument).cloned();
            entry.iv = None;
            entry.selection = None;
            entry.risk = None;
        }
        self.changes
            .send_modify(|version| *version = version.saturating_add(1));
        Ok(())
    }
    /// Attaches cold application evidence before provider admission. It cannot
    /// change the numerical context or attach metadata to a different version.
    pub fn attach_manifest(
        &self,
        instrument: u32,
        expected: u128,
        manifest: Arc<super::manifest::Manifest>,
    ) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        let entry = state
            .entries
            .get_mut(&instrument)
            .ok_or("Option registration absent")?;
        if entry.context.identity != expected || manifest.identity() != expected {
            return Err("Trade manifest context compare-and-swap conflict");
        }
        entry.manifest = Some(manifest);
        Ok(())
    }
    /// Copies current immutable reference data without requiring a quote. Scope
    /// admission may precede an illiquid contract's first market observation.
    ///
    /// # Errors
    /// Interrupted native registry; no provider resource is retained by the copy.
    pub fn reference_context(
        &self,
        instrument: u32,
    ) -> Result<Option<Arc<super::context::Context>>, &'static str> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?
            .entries
            .get(&instrument)
            .and_then(|entry| entry.reference.clone()))
    }
    /// Copies source-time reference identity without solving or allocating a new
    /// context. A retired physical owner cannot populate another generation's trade.
    pub fn trade_context(
        &self,
        instrument: u32,
        physical: u128,
    ) -> Result<Option<TradeContext>, &'static str> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        Ok(state
            .entries
            .get(&instrument)
            .filter(|entry| physical != 0 && entry.physical == physical)
            .map(|entry| TradeContext {
                context: entry.context.clone(),
                manifest: entry.manifest.clone(),
                reference: entry.reference.clone(),
            }))
    }
    /// Timestamp-first quote ingress matches the current option selection path.
    /// Raw last-price storage intentionally has a different sequence-first order.
    /// A late record from a retired physical generation cannot populate this owner.
    pub fn observe(
        &self,
        instrument: u32,
        physical: u128,
        quote: Quote,
    ) -> Result<bool, &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        let Some(entry) = state.entries.get_mut(&instrument) else {
            return Ok(false);
        };
        if physical == 0
            || physical != entry.physical
            || entry
                .observed
                .is_some_and(|old| (quote.event, quote.sequence) <= old)
        {
            return Ok(false);
        }
        entry.quote = Some(quote);
        entry.observed = Some((quote.event, quote.sequence));
        state.observations = state.observations.saturating_add(1);
        Ok(true)
    }
    /// Retires quotes/calculations from an exact physical source while retaining
    /// the application-qualified context for reconnection. Zero means no source
    /// is admitted; old records and delayed releases cannot revive it.
    /// Makes every retained calculated result unavailable without taking the
    /// pricing-state lock. Rebinding a new physical source rearms the engine.
    pub fn fence(&self) {
        self.fenced
            .store(true, std::sync::atomic::Ordering::Release);
    }
    /// Rebinds all entries to a new source after fencing prior calculations.
    pub fn rebind(&self, physical: u128) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        for entry in state.entries.values_mut() {
            if entry.physical != physical {
                entry.physical = physical;
                entry.quote = None;
                entry.observed = None;
                entry.iv = None;
                entry.selection = None;
                entry.risk = None;
            }
        }
        self.fenced
            .store(physical == 0, std::sync::atomic::Ordering::Release);
        Ok(())
    }
    /// Copies one provider quote with no calculation or per-record allocation.
    /// Nullable undefined sides replace the ordering key without manufacturing
    /// a usable quote, matching the current coalesced input store.
    pub fn observe_wire(
        &self,
        physical: u128,
        wire: crate::dataset::records::Quote,
        local: i64,
    ) -> Result<bool, &'static str> {
        if self.fenced.load(std::sync::atomic::Ordering::Acquire) {
            return Ok(false);
        }
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        let Some(entry) = state.entries.get_mut(&wire.header.instrument) else {
            return Ok(false);
        };
        let key = (wire.header.event_ns / 100, i64::from(wire.header.sequence));
        if physical == 0
            || entry.physical != physical
            || entry.observed.is_some_and(|prior| key <= prior)
        {
            return Ok(false);
        }
        entry.observed = Some(key);
        entry.quote = if wire.bid == i64::MAX || wire.ask == i64::MAX {
            None
        } else {
            Some(Quote {
                generation_low: entry.context.qualification.generation_low,
                generation_high: entry.context.qualification.generation_high,
                event: key.0,
                received: local,
                sequence: key.1,
                bid: source_decimal(wire.bid, 9),
                ask: source_decimal(wire.ask, 9),
                bid_size: source_decimal(i64::from(wire.bid_size), 0),
                ask_size: source_decimal(i64::from(wire.ask_size), 0),
                event_offset: 0,
                received_offset: 0,
            })
        };
        state.observations = state.observations.saturating_add(1);
        Ok(true)
    }
    /// Changes business/display full-risk demand without affecting selection or
    /// performing a calculation on ingress.
    pub fn require_risk(&self, instrument: u32, required: bool) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        if let Some(entry) = state.entries.get_mut(&instrument)
            && entry.required != required
        {
            entry.required = required;
            state.memberships = None;
        }
        Ok(())
    }
    /// Atomically replaces the complete business-risk demand union. Incomplete,
    /// duplicate or stale batches change no entry. This is cold control work;
    /// tick ingress and numerical passes never allocate this validation set.
    pub fn set_risk_requirements(&self, updates: &[RiskRequirement]) -> Result<(), &'static str> {
        if updates.is_empty() || updates.len() > 2048 {
            return Err("Invalid complete risk requirement batch");
        }
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        if updates.len() != state.entries.len() {
            return Err("Risk requirements must include the complete scope");
        }
        let mut ids = std::collections::BTreeSet::new();
        for update in updates {
            if !ids.insert(update.instrument)
                || state
                    .entries
                    .get(&update.instrument)
                    .is_none_or(|entry| entry.context.identity != update.context)
            {
                return Err("Risk requirement context compare-and-swap conflict");
            }
        }
        let mut changed = false;
        for update in updates {
            if let Some(entry) = state.entries.get_mut(&update.instrument) {
                changed |= entry.required != update.required;
                entry.required = update.required;
            }
        }
        if changed {
            state.memberships = None;
        }
        Ok(())
    }
    /// Removes an exact physical registration. Stale releases preserve replacements.
    pub fn remove(&self, instrument: u32, physical: u128) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        if state
            .entries
            .get(&instrument)
            .is_some_and(|entry| entry.physical == physical)
        {
            state.entries.remove(&instrument);
            state.memberships = None;
        }
        Ok(())
    }
    /// Current monotonically accumulated observation/selection/IV-solve counters.
    pub fn counters(&self) -> Result<(u64, u64, u64), &'static str> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        Ok((state.observations, state.selections, state.solves))
    }
    /// Event-driven calculation notification. Observations stay in fixed latest
    /// slots; a delayed consumer never accumulates a completion/timer backlog.
    pub fn updates(&self) -> tokio::sync::watch::Receiver<u64> {
        self.changes.subscribe()
    }
    /// Checks typed immutable configuration as well as opaque semantic digests.
    /// A reused digest cannot conceal changed model/reference inputs. Dynamic
    /// risk demand and source observations are not subscription definitions.
    pub fn same_configuration(&self, other: &Self) -> Result<bool, &'static str> {
        if self.maximum != other.maximum
            || self.batch != other.batch
            || self.iv_ticks != other.iv_ticks
            || self.policy != other.policy
        {
            return Ok(false);
        }
        let prior: Vec<_> = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?
            .entries
            .iter()
            .map(|(id, entry)| (*id, entry.context.clone(), entry.manifest.clone()))
            .collect();
        // Release the first writer before taking another, including self/self.
        let other = other
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        Ok(prior.len() == other.entries.len()
            && prior.iter().all(|(id, context, manifest)| {
                other.entries.get(id).is_some_and(|entry| {
                    *entry.context == **context
                        && match (manifest, &entry.manifest) {
                            (None, None) => true,
                            (Some(left), Some(right)) => {
                                left.identity() == right.identity() && left.bytes() == right.bytes()
                            }
                            _ => false,
                        }
                })
            }))
    }
    /// Recovery proof for required live risk. Unmonitored discovery needs at
    /// least one valid selection. This is pricing evidence, not actor readiness.
    pub fn ready(&self, at: i64) -> Result<bool, &'static str> {
        if self.fenced.load(std::sync::atomic::Ordering::Acquire) {
            return Ok(false);
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        let valid = |entry: &Entry, value: &Calculation, risk: bool| {
            entry.physical != 0
                && value.source_generation == entry.physical
                && value.context == entry.context.identity
                && value.failure.is_none()
                && entry
                    .reference
                    .as_ref()
                    .is_none_or(|r| r.validate_at(at).is_ok())
                && invalid(&entry.context, value.underlying, value.option, at).is_none()
                && (!risk || entry.quote.is_some())
                && (if risk {
                    value.risk.is_some()
                } else {
                    value.selection.is_some()
                        && value.iv_event.is_some_and(|event| {
                            i128::from(at) - i128::from(event) <= i128::from(self.iv_ticks)
                        })
                })
        };
        if !state.entries.values().any(|entry| entry.required) {
            Ok(state.entries.values().any(|entry| {
                entry
                    .selection
                    .as_ref()
                    .is_some_and(|value| valid(entry, value, false))
            }))
        } else {
            Ok(state
                .entries
                .values()
                .filter(|entry| entry.required)
                .all(|entry| {
                    entry
                        .risk
                        .as_ref()
                        .is_some_and(|value| valid(entry, value, true))
                }))
        }
    }
    /// SHA-256 of exact UTF-8 refresh-policy version, little-endian words.
    pub fn policy_identity(&self) -> [u64; 4] {
        self.policy
    }
    /// Reads immutable cached results without I/O. Expired quote/IV evidence
    /// returns an explicit stale result, preserving its source/provenance fields.
    pub fn read(
        &self,
        instrument: u32,
        risk: bool,
        at: i64,
    ) -> Result<Option<Arc<Calculation>>, &'static str> {
        self.read_checked(instrument, risk, at, true)
    }
    /// Latest completed full-risk snapshot for event publication. A newer quote
    /// may be pending, but the result retains its own exact source quote, age
    /// checks, reference identity and physical generation. Strict current-quote
    /// queries continue to use `read` and can report `CalculationPending`.
    pub fn completed_risk(
        &self,
        instrument: u32,
        at: i64,
    ) -> Result<Option<Arc<Calculation>>, &'static str> {
        self.read_checked(instrument, true, at, false)
    }
    fn read_checked(
        &self,
        instrument: u32,
        risk: bool,
        at: i64,
        require_latest: bool,
    ) -> Result<Option<Arc<Calculation>>, &'static str> {
        if self.fenced.load(std::sync::atomic::Ordering::Acquire) {
            return Ok(None);
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        let Some(entry) = state.entries.get(&instrument) else {
            return Ok(None);
        };
        let Some(value) = (if risk { &entry.risk } else { &entry.selection }) else {
            return Ok(None);
        };
        let invalid = if entry
            .reference
            .as_ref()
            .is_some_and(|r| r.validate_at(at).is_err())
        {
            Some(Failure::ContextExpired)
        } else {
            invalid(&entry.context, value.underlying, value.option, at)
        };
        let superseded = risk
            && (entry.quote.is_none()
                || require_latest
                    && entry.observed != Some((value.option.event, value.option.sequence)));
        if superseded
            || invalid.is_some()
            || !risk
                && value.iv_event.is_some_and(|event| {
                    i128::from(at) - i128::from(event) > i128::from(self.iv_ticks)
                })
        {
            let mut stale = (**value).clone();
            stale.selection = None;
            stale.risk = None;
            stale.failure = Some(if superseded {
                Failure::CalculationPending
            } else {
                Failure::Stale
            });
            return Ok(Some(Arc::new(stale)));
        }
        Ok(Some(value.clone()))
    }
    /// Fences a completed publication against replacement, reference refresh and
    /// a newer completed result. Pending ingress cannot starve this immutable
    /// source/result pair, and it is never attached to a later source quote.
    pub fn matches_completed_risk(
        &self,
        instrument: u32,
        physical: u128,
        context: &Arc<super::context::Context>,
        calculation: &Calculation,
    ) -> Result<bool, &'static str> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        Ok(state.entries.get(&instrument).is_some_and(|entry| {
            physical != 0
                && entry.physical == physical
                && calculation.source_generation == physical
                && entry.context.identity == calculation.context
                && entry.quote.is_some()
                && entry
                    .risk
                    .as_ref()
                    .is_some_and(|risk| std::ptr::eq(risk.as_ref(), calculation))
                && entry
                    .reference
                    .as_ref()
                    .is_some_and(|current| Arc::ptr_eq(current, context))
        }))
    }
    /// Checks the exact current source/context after a separately owned risk
    /// capture. A newer quote cannot acquire older Greeks or be published under
    /// an older sequence while calculation is still pending.
    ///
    /// # Errors
    /// Interrupted native state is explicit and must trigger recovery.
    pub fn matches_observation(
        &self,
        instrument: u32,
        physical: u128,
        context: &Arc<super::context::Context>,
        calculation: &Calculation,
    ) -> Result<bool, &'static str> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?;
        Ok(state.entries.get(&instrument).is_some_and(|entry| {
            physical != 0
                && entry.physical == physical
                && calculation.source_generation == physical
                && entry.context.identity == calculation.context
                && entry.quote == Some(calculation.option)
                && entry
                    .reference
                    .as_ref()
                    .is_some_and(|current| Arc::ptr_eq(current, context))
        }))
    }
    /// Runs one bounded fair batch. Callers supply API time and a non-I/O reader
    /// of the current underlying quote. Numerical work uses the original caller
    /// deadline; cancellation drops it before any stale result can be committed.
    pub async fn pass(
        &self,
        risk: bool,
        at: i64,
        underlying: impl Fn(u32) -> Option<Quote>,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<usize, &'static str> {
        self.pass_count(
            risk,
            at,
            |id| Ok(underlying(id)),
            token,
            deadline,
            self.batch,
        )
        .await
    }

    /// Runs every eligible registration once per cadence, yielding between
    /// bounded batches. The final short batch does not revisit earlier entries.
    /// A fixed initial count prevents concurrent registration from extending a
    /// sweep indefinitely. No backlog of missed timer ticks is accumulated.
    pub async fn sweep(
        &self,
        risk: bool,
        at: impl Fn() -> Result<i64, &'static str>,
        underlying: impl Fn(u32) -> Option<Quote>,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<usize, &'static str> {
        self.sweep_checked(risk, at, |id| Ok(underlying(id)), token, deadline)
            .await
    }
    async fn sweep_checked(
        &self,
        risk: bool,
        at: impl Fn() -> Result<i64, &'static str>,
        underlying: impl Fn(u32) -> Result<Option<Quote>, &'static str>,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<usize, &'static str> {
        let count = self
            .state
            .lock()
            .map_err(|_| "Option registry writer interrupted")?
            .entries
            .values()
            .filter(|entry| !risk || entry.required)
            .count();
        let mut processed = 0;
        for offset in (0..count).step_by(self.batch) {
            processed += self
                .pass_count(
                    risk,
                    at()?,
                    &underlying,
                    token,
                    deadline,
                    self.batch.min(count - offset),
                )
                .await?;
            tokio::task::yield_now().await;
        }
        Ok(processed)
    }

    /// Runs both cadence loops inside the enclosing tracked dataset future.
    /// Cancellation/parent drop owns both timers and in-progress numerical work;
    /// no detached worker, blocking thread, tick channel or queued sweep exists.
    /// Unexpected calculation/clock failure exits to the dataset recovery owner.
    pub async fn run(
        &self,
        selection_ms: u32,
        risk_ms: u32,
        at: impl Fn() -> Result<i64, &'static str> + Sync,
        underlying: impl Fn(u32) -> Option<Quote> + Sync,
        token: &CancellationToken,
    ) -> Result<(), &'static str> {
        self.run_checked(selection_ms, risk_ms, at, |id| Ok(underlying(id)), token)
            .await
    }
    /// Owned production loop whose source reader reports infrastructure faults.
    /// Such failures escape to dataset recovery; they are never converted into
    /// an ordinary missing quote or silently swallowed by the scheduler.
    pub async fn run_checked(
        &self,
        selection_ms: u32,
        risk_ms: u32,
        at: impl Fn() -> Result<i64, &'static str> + Sync,
        underlying: impl Fn(u32) -> Result<Option<Quote>, &'static str> + Sync,
        token: &CancellationToken,
    ) -> Result<(), &'static str> {
        if !(10..=5000).contains(&selection_ms)
            || !(10..=60000).contains(&risk_ms)
            || i64::from(selection_ms) * 10000 > self.iv_ticks
        {
            return Err("Invalid option scheduler cadence");
        }
        let run = async |risk, ms| {
            let period = Duration::from_millis(ms);
            let mut timer = tokio::time::interval_at(tokio::time::Instant::now() + period, period);
            timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
            loop {
                tokio::select! {biased;()=token.cancelled()=>return Ok(()),_=timer.tick()=>{}}
                match self
                    .sweep_checked(risk, &at, &underlying, token, Deadline::recovery())
                    .await
                {
                    Ok(_) => {}
                    Err(_) if token.is_cancelled() => return Ok(()),
                    Err(error) => return Err(error),
                }
            }
        };
        tokio::try_join!(
            run(false, u64::from(selection_ms)),
            run(true, u64::from(risk_ms))
        )?;
        Ok(())
    }

    /// Test-only rejected parallel-tier experiment: control p99 regressed despite higher throughput.
    #[cfg(test)]
    pub(crate) async fn run_parallel_checked(
        self: &Arc<Self>,
        cadence: [u32; 2],
        at: Arc<dyn Fn() -> Result<i64, &'static str> + Send + Sync>,
        underlying: Arc<dyn Fn(u32) -> Result<Option<Quote>, &'static str> + Send + Sync>,
        parent: &crate::runtime::owner::PricingOwner,
        token: &CancellationToken,
    ) -> Result<(), &'static str> {
        let [selection_ms, risk_ms] = cadence;
        if !(10..=5000).contains(&selection_ms)
            || !(10..=60000).contains(&risk_ms)
            || i64::from(selection_ms) * 10000 > self.iv_ticks
        {
            return Err("Invalid option scheduler cadence");
        }
        let stop = token.child_token();
        let _cancel_on_drop = stop.clone().drop_guard();
        let task = |risk, milliseconds| {
            let engine = self.clone();
            let at = at.clone();
            let read = underlying.clone();
            let stop = stop.clone();
            parent.spawn(async move {
                let period = Duration::from_millis(milliseconds);
                let mut timer = tokio::time::interval_at(tokio::time::Instant::now() + period, period);
                timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
                loop {
                    tokio::select! { biased; ()=stop.cancelled()=>return Ok(()), _=timer.tick()=>{} }
                    match engine.sweep_checked(risk, || at(), |id| read(id), &stop, Deadline::recovery()).await {
                        Ok(_) => {}, Err(_) if stop.is_cancelled() => return Ok(()), Err(error) => return Err(error),
                    }
                }
            }).map_err(|_| "Native pricing task admission failed")
        };
        let mut selection = task(false, u64::from(selection_ms))?;
        let mut risk = match task(true, u64::from(risk_ms)) {
            Ok(task) => task,
            Err(error) => {
                stop.cancel();
                let _ = selection.await;
                return Err(error);
            }
        };
        let outcome =
            |result: Result<Option<Result<(), &'static str>>, tokio::task::JoinError>| match result
            {
                Ok(Some(result)) => result,
                Ok(None) if token.is_cancelled() => Ok(()),
                _ => Err("Native pricing task terminated"),
            };
        tokio::select! { biased;
            ()=token.cancelled()=>{ stop.cancel(); let _=tokio::join!(selection,risk); Ok(()) },
            result=&mut selection=>{ stop.cancel(); let _=risk.await; outcome(result) },
            result=&mut risk=>{ stop.cancel(); let _=selection.await; outcome(result) },
        }
    }

    async fn pass_count(
        &self,
        risk: bool,
        at: i64,
        underlying: impl Fn(u32) -> Result<Option<Quote>, &'static str>,
        token: &CancellationToken,
        deadline: Deadline,
        maximum: usize,
    ) -> Result<usize, &'static str> {
        if self.fenced.load(std::sync::atomic::Ordering::Acquire) {
            return Ok(0);
        }
        let pass = if risk {
            &self.risk_pass
        } else {
            &self.selection_pass
        };
        let _pass = deadline
            .run(token, pass.lock())
            .await
            .map_err(|_| "Option pricing interrupted")?;
        let batch = {
            let mut state = self
                .state
                .lock()
                .map_err(|_| "Option registry writer interrupted")?;
            if state.entries.values().any(|entry| {
                entry
                    .reference
                    .as_ref()
                    .is_some_and(|r| r.validate_at(at).is_err())
            }) {
                self.begin_reference_recovery()?;
                self.reference_updates
                    .send_modify(|version| *version = version.saturating_add(1));
                return Err("Native option reference requires refresh");
            }
            if state.memberships.is_none() {
                state.memberships = Some([
                    Arc::new(state.entries.keys().copied().collect()),
                    Arc::new(
                        state
                            .entries
                            .iter()
                            .filter(|(_, entry)| entry.required)
                            .map(|(id, _)| *id)
                            .collect(),
                    ),
                ]);
            }
            let ids = state.memberships.as_ref().expect("Membership initialized")
                [usize::from(risk)]
            .clone();
            if ids.is_empty() {
                return Ok(0);
            }
            let cursor = if risk {
                &mut state.risk_cursor
            } else {
                &mut state.selection_cursor
            };
            let start = *cursor % ids.len();
            let count = maximum.min(ids.len());
            *cursor = (start + count) % ids.len();
            (0..count)
                .filter_map(|i| {
                    let id = ids[(start + i) % ids.len()];
                    let entry = &state.entries[&id];
                    entry
                        .quote
                        .map(|quote| (id, entry.physical, entry.context.clone(), quote, entry.iv))
                })
                .collect::<Vec<_>>()
        };
        // One coherent copied observation per underlying within this bounded batch.
        let mut underlyings = BTreeMap::new();
        for (id, physical, context, option, prior_iv) in &batch {
            let observed = match underlyings.entry(context.underlying) {
                std::collections::btree_map::Entry::Occupied(entry) => *entry.get(),
                std::collections::btree_map::Entry::Vacant(entry) => {
                    *entry.insert(underlying(context.underlying)?)
                }
            };
            if observed.is_none() {
                let mut state = self
                    .state
                    .lock()
                    .map_err(|_| "Option registry writer interrupted")?;
                if let Some(entry) = state.entries.get_mut(id)
                    && entry.physical == *physical
                    && Arc::ptr_eq(&entry.context, context)
                {
                    entry.selection = None;
                    entry.risk = None;
                }
                continue;
            }
            let source = observed.unwrap_or_default();
            let mut value = Calculation {
                context: context.identity,
                source_generation: *physical,
                at,
                underlying: source,
                option: *option,
                iv_event: None,
                iv: None,
                time: 0.0,
                selection: None,
                risk: None,
                failure: if observed.is_none() {
                    Some(Failure::UnderlyingUnavailable)
                } else {
                    invalid(context, source, *option, at)
                },
            };
            let mut iv = *prior_iv;
            let mut solved = false;
            let mut selected = false;
            if value.failure.is_none() {
                let mut model = context.model;
                model.forward = crate::pricing::midpoint::midpoint(
                    nanounits(source.bid)?,
                    nanounits(source.ask)?,
                )
                .ok_or("Qualified underlying midpoint is invalid")?;
                // Preserve TimeSpan.TotalDays / 365 operation order from C#.
                model.time = ((context.expiration - at) as f64 / 864000000000.0)
                    / f64::from(context.days_in_year);
                value.time = model.time;
                let mark = crate::pricing::midpoint::midpoint(
                    nanounits(option.bid)?,
                    nanounits(option.ask)?,
                )
                .ok_or("Qualified option midpoint is invalid")?;
                let calculator = &self.calculator;
                if risk {
                    match calculator.implied(model, mark, token, deadline).await {
                        Ok(result) => value.risk = Some(result),
                        Err(error) => value.failure = Some(Failure::Numerical(error)),
                    }
                } else {
                    if iv.is_none_or(|iv| {
                        iv.context != context.identity
                            || i128::from(at) - i128::from(iv.event) >= i128::from(self.iv_ticks)
                    }) {
                        solved = true;
                        match calculator.solve(model, mark, token, deadline).await {
                            Ok(result) => {
                                iv = Some(Iv {
                                    context: context.identity,
                                    event: option.event,
                                    volatility: result.volatility,
                                    underlying: source,
                                    option: *option,
                                    at,
                                })
                            }
                            Err(error) => value.failure = Some(Failure::Numerical(error)),
                        }
                    }
                    if value.failure.is_none()
                        && let Some(iv) = iv
                    {
                        value.iv_event = Some(iv.event);
                        value.iv = Some(iv);
                        selected = true;
                        match calculator
                            .price_delta(model, iv.volatility, token, deadline)
                            .await
                        {
                            Ok(result) => value.selection = Some(result),
                            Err(error) => value.failure = Some(Failure::Numerical(error)),
                        }
                    }
                }
            }
            if self.fenced.load(std::sync::atomic::Ordering::Acquire)
                || token.is_cancelled()
                || deadline.remaining().is_zero()
                || matches!(
                    value.failure,
                    Some(Failure::Numerical(NumericalFailure::Interrupted(_)))
                )
            {
                return Err("Option pricing interrupted");
            }
            let mut state = self
                .state
                .lock()
                .map_err(|_| "Option registry writer interrupted")?;
            if solved {
                state.solves = state.solves.saturating_add(1)
            }
            if selected {
                state.selections = state.selections.saturating_add(1)
            }
            if let Some(entry) = state.entries.get_mut(id)
                && entry.physical == *physical
                && Arc::ptr_eq(&entry.context, context)
            {
                if risk {
                    // Keep the completed immutable source/result pair even if
                    // ingress advanced during numerical work. The next sweep
                    // coalesces to that newer quote without starving publication.
                    entry.risk = Some(Arc::new(value));
                } else {
                    entry.iv = iv;
                    entry.selection = Some(Arc::new(value));
                }
                self.changes
                    .send_modify(|version| *version = version.saturating_add(1));
            }
        }
        Ok(batch.len())
    }
}
fn invalid(context: &Context, underlying: Quote, option: Quote, at: i64) -> Option<Failure> {
    if at >= context.valid_until {
        return Some(Failure::ContextExpired);
    }
    let mut request = context.qualification;
    request.at = at;
    request.underlying = underlying;
    request.option = option;
    match qualification::validate(&request) {
        Some(result) if result.failure == 0 => None,
        Some(result) => Some(Failure::Quote(result)),
        None => Some(Failure::Numerical(NumericalFailure::InvalidInput)),
    }
}
fn nanounits(value: qualification::Decimal) -> Result<i64, &'static str> {
    let [lo, mid, hi, flags] = value.bits;
    let scale = (flags >> 16) & 255;
    if flags & 0x80000000 != 0 || flags & 0x7f00ffff != 0 || scale > 9 {
        return Err("Native source price is not exact nanounits");
    }
    let coefficient = u128::from(lo) | (u128::from(mid) << 32) | (u128::from(hi) << 64);
    coefficient
        .checked_mul(10u128.pow(9 - scale))
        .and_then(|value| i64::try_from(value).ok())
        .ok_or("Native source price exceeds nanounits")
}

fn source_decimal(value: i64, mut scale: u32) -> qualification::Decimal {
    // CLR decimal division by integer 1e9 removes exact fractional trailing
    // zeros. Preserve those words too: hashes/MessagePack include decimal scale.
    let mut magnitude = value.unsigned_abs();
    while scale > 0 && magnitude.is_multiple_of(10) {
        magnitude /= 10;
        scale -= 1;
    }
    qualification::Decimal {
        bits: [
            magnitude as u32,
            (magnitude >> 32) as u32,
            0,
            (scale << 16) | if value < 0 { 0x80000000 } else { 0 },
        ],
    }
}
fn decimal_double(value: qualification::Decimal) -> Option<f64> {
    crate::reference::decimal::to_double(value)
}
fn policy_identity(version: &str) -> [u64; 4] {
    use sha2::{Digest, Sha256};
    let bytes = Sha256::digest(version.as_bytes());
    std::array::from_fn(|index| {
        u64::from_le_bytes(
            bytes[index * 8..index * 8 + 8]
                .try_into()
                .expect("fixed SHA-256 word"),
        )
    })
}
/// Maps a retained native underlying quote to logical pricing-generation units.
/// The physical source is fenced by the enclosing book read; it must not be
/// confused with the separate logical generation of the pricing context.
pub fn underlying_quote(
    tick: crate::latest::Tick<crate::latest::QuoteValue>,
    logical: u128,
) -> Option<Quote> {
    Some(Quote {
        generation_low: logical as u64,
        generation_high: (logical >> 64) as u64,
        event: tick.event_ticks,
        received: tick.value.local_received_ticks,
        sequence: tick.sequence,
        bid: source_decimal(tick.value.bid?, 9),
        ask: source_decimal(tick.value.ask?, 9),
        bid_size: source_decimal(i64::from(tick.value.bid_size), 0),
        ask_size: source_decimal(i64::from(tick.value.ask_size), 0),
        event_offset: 0,
        received_offset: 0,
    })
}
#[cfg(test)]
mod retirement_fence_tests {
    use super::*;
    #[test]
    fn source_retirement_never_waits_for_a_pricing_writer_or_returns_cached_risk() {
        let engine = Coalesced::new(1, 1, 10).unwrap();
        let writer = engine.state.lock().unwrap();
        engine.fence();
        assert!(engine.read(1, true, 0).unwrap().is_none());
        assert!(engine.completed_risk(1, 0).unwrap().is_none());
        assert!(!engine.ready(0).unwrap());
        drop(writer);
    }
}

#[cfg(test)]
mod parallel_tier_tests {
    use super::*;
    use crate::runtime::owner::Owner;
    const AT: i64 = 17_000_000_000_000_000;
    fn decimal(value: u32) -> qualification::Decimal {
        qualification::Decimal {
            bits: [value, 0, 0, 0],
        }
    }
    fn quote(price: u32) -> Quote {
        Quote {
            generation_low: 1,
            event: AT,
            received: AT,
            sequence: 1,
            bid: decimal(price),
            ask: decimal(price),
            bid_size: decimal(10),
            ask_size: decimal(10),
            ..Default::default()
        }
    }
    fn engine() -> Arc<Coalesced> {
        let engine = Arc::new(Coalesced::new(1, 1, 5000).unwrap());
        engine
            .register(
                1,
                100,
                Context {
                    identity: 1,
                    underlying: 9,
                    expiration: AT + 30 * 864000000000,
                    days_in_year: 365,
                    valid_until: AT + 864000000000,
                    model: Request {
                        forward: 0.,
                        strike: 5000.,
                        time: 0.,
                        rate: 0.04,
                        call: true,
                        american: false,
                        futures_style: false,
                    },
                    qualification: qualification::Request {
                        size: 304,
                        flags: 3 | 4 | 8 | 16,
                        generation_low: 1,
                        maximum_age: 5000,
                        maximum_skew: 2000,
                        maximum_lead: 0,
                        strike: decimal(5000),
                        definition_strike: decimal(5000),
                        ..Default::default()
                    },
                },
                true,
            )
            .unwrap();
        engine.observe(1, 100, quote(100)).unwrap();
        engine
    }

    /// Compare actual scheduler completion rates and timer delay; run explicitly in Release without competing builds.
    #[tokio::test(flavor = "multi_thread", worker_threads = 2)]
    #[ignore = "Explicit Release performance run after compilation ends"]
    async fn actual_pricing_scheduler_before_after_timings() {
        let mut samples = Vec::new();
        for pair in 0..5 {
            for parallel in if pair % 2 == 0 {
                [false, true]
            } else {
                [true, false]
            } {
                let template = engine();
                let mut context = *template.state.lock().unwrap().entries[&1].context;
                context.model.american = true;
                let engine = Arc::new(Coalesced::new(8, 8, 5000).unwrap());
                for id in 1..=8 {
                    engine.register(id, 100, context, true).unwrap();
                    engine.observe(id, 100, quote(100)).unwrap();
                }
                let root = Arc::new(Owner::default());
                let parent = root.child().unwrap();
                let port = parent.pricing_owner();
                let stop = CancellationToken::new();
                engine
                    .sweep_checked(
                        false,
                        || Ok(AT),
                        |_| Ok(Some(quote(5000))),
                        &stop,
                        Deadline::recovery(),
                    )
                    .await
                    .unwrap();
                let selections = engine.counters().unwrap().1;
                let changes = *engine.updates().borrow();
                let running = engine.clone();
                let stopping = stop.clone();
                let started = std::time::Instant::now();
                let task = tokio::spawn(async move {
                    if parallel {
                        running
                            .run_parallel_checked(
                                [10, 10],
                                Arc::new(|| Ok(AT)),
                                Arc::new(|_| Ok(Some(quote(5000)))),
                                &port,
                                &stopping,
                            )
                            .await
                    } else {
                        running
                            .run_checked(10, 10, || Ok(AT), |_| Ok(Some(quote(5000))), &stopping)
                            .await
                    }
                });
                let until = tokio::time::Instant::now() + Duration::from_secs(1);
                let mut timer = tokio::time::interval(Duration::from_millis(1));
                timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
                let mut delays = Vec::new();
                loop {
                    let due = timer.tick().await;
                    delays.push(
                        tokio::time::Instant::now()
                            .saturating_duration_since(due)
                            .as_nanos() as u64,
                    );
                    if due >= until {
                        break;
                    }
                }
                stop.cancel();
                task.await.unwrap().unwrap();
                let elapsed = started.elapsed().as_nanos() as u64;
                let selected = engine.counters().unwrap().1 - selections;
                let completed = *engine.updates().borrow() - changes;
                assert!(selected > 0 && completed > selected);
                assert!(
                    engine
                        .completed_risk(1, AT)
                        .unwrap()
                        .unwrap()
                        .risk
                        .is_some()
                );
                delays.sort_unstable();
                samples.push(serde_json::json!({"parallel":parallel,"elapsedNs":elapsed,"selectionCompletions":selected,"riskCompletions":completed-selected,"timerP99Ns":delays[(delays.len()*99/100).min(delays.len()-1)],"timerSamples":delays.len()}));
                parent
                    .wait_nested(
                        &CancellationToken::new(),
                        Deadline::after(Duration::from_secs(2)),
                    )
                    .await
                    .unwrap();
                drop(parent);
                root.stop().unwrap();
                root.wait_idle(
                    &CancellationToken::new(),
                    Deadline::after(Duration::from_secs(2)),
                )
                .await
                .unwrap();
            }
        }
        println!(
            "SCHEDULER_PERFORMANCE {}",
            serde_json::json!({"scenario":"actual-american-selection-risk-scheduler","cadenceMs":10,"options":8,"runs":samples})
        );
    }

    /// Both numerical tiers must make concurrent progress and be joined when their caller stops.
    #[tokio::test(flavor = "multi_thread", worker_threads = 2)]
    async fn parallel_tiers_progress_independently_and_join_on_stop() {
        let root = Arc::new(Owner::default());
        let parent = root.child().unwrap();
        let port = parent.pricing_owner();
        let engine = engine();
        let barrier = Arc::new((Mutex::new(0usize), std::sync::Condvar::new()));
        let gate = barrier.clone();
        let read = Arc::new(move |_| {
            let (count, changed) = (&gate.0, &gate.1);
            let mut entered = count.lock().unwrap();
            if *entered < 2 {
                *entered += 1;
                changed.notify_all();
                let (result, wait) = changed
                    .wait_timeout_while(entered, Duration::from_secs(2), |count| *count < 2)
                    .unwrap();
                entered = result;
                if wait.timed_out() && *entered < 2 {
                    return Err("Pricing tiers did not run concurrently");
                }
            }
            Ok(Some(quote(5000)))
        });
        let stopping = CancellationToken::new();
        let task_stop = stopping.clone();
        let running = engine.clone();
        let task = tokio::spawn(async move {
            running
                .run_parallel_checked([10, 10], Arc::new(|| Ok(AT)), read, &port, &task_stop)
                .await
        });
        tokio::time::timeout(Duration::from_secs(3), async {
            loop {
                if engine
                    .read(1, false, AT)
                    .unwrap()
                    .is_some_and(|value| value.selection.is_some())
                    && engine
                        .completed_risk(1, AT)
                        .unwrap()
                        .is_some_and(|value| value.risk.is_some())
                {
                    break;
                }
                tokio::time::sleep(Duration::from_millis(1)).await;
            }
        })
        .await
        .unwrap();
        stopping.cancel();
        assert!(task.await.unwrap().is_ok());
        parent
            .wait_nested(
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2)),
            )
            .await
            .unwrap();
        drop(parent);
        root.stop().unwrap();
        root.wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    }

    /// One tier's infrastructure failure cancels and joins its sibling without renewing any recovery budget.
    #[tokio::test]
    async fn parallel_tier_failure_joins_the_other_tier() {
        let root = Arc::new(Owner::default());
        let parent = root.child().unwrap();
        let port = parent.pricing_owner();
        let engine = engine();
        let result = tokio::time::timeout(
            Duration::from_secs(2),
            engine.run_parallel_checked(
                [10, 10],
                Arc::new(|| Ok(AT)),
                Arc::new(|_| Err("Deliberate underlying failure")),
                &port,
                &CancellationToken::new(),
            ),
        )
        .await
        .unwrap();
        assert_eq!(result, Err("Deliberate underlying failure"));
        parent
            .wait_nested(
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2)),
            )
            .await
            .unwrap();
        drop(parent);
        root.stop().unwrap();
        root.wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    }
}
