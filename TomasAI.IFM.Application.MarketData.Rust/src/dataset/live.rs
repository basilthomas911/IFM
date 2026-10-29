//! Tracked provider lanes wired to native hot/session/VWAP books. Lifecycle
//! serialization is asynchronous; reads and emergency cancellation do not queue
//! behind an awaited stop. This component does not replace authoritative event
//! publication or downstream trade-plan recovery verification.
use super::{
    book::Book,
    lanes::{Gateway, Lanes, Phase, Status},
    transport::{Event, Lane, SessionPlan},
};
use crate::{
    latest::{Asset, Contract},
    pricing::Values,
    runtime::{Deadline, clock::Clock, owner::Owner},
};
use std::{
    collections::{BTreeMap, BTreeSet, VecDeque},
    sync::{Arc, Mutex},
};
use tokio::sync::{Mutex as AsyncMutex, watch};
use tokio_util::sync::CancellationToken;
/// Application-resolved identity; provider name never substitutes for canonical ID.
#[derive(Clone, PartialEq, Eq)]
pub struct Registration {
    /// Current provider instrument from the verified definition snapshot.
    pub instrument: u32,
    /// Qualified source publisher. Zero is reserved for unqualified transport
    /// fixtures; priced application scopes always supply a verified publisher.
    pub publisher: u16,
    /// Canonical domain ID.
    pub contract: String,
    /// Actual requested provider symbol (or its instrument-ID string).
    pub source: String,
    /// Reviewed asset kind.
    pub asset: Asset,
    /// Exact nanounit tick threshold, when available.
    pub tick: Option<i64>,
    /// Explicit authoritative VWAP source selection.
    pub vwap: bool,
    /// Whether latest observation evidence is required for native readiness.
    pub required: bool,
}
/// Native processing evidence, independent of actor/trade-plan publication proof.
#[derive(Clone, Copy, PartialEq, Eq)]
pub enum Evidence {
    /// Explicit API-session-aware quiet/closed control evidence only.
    ControlOnly,
    /// Every required registration must have a positive trade or usable quote.
    Observations,
}
/// Complete bounded native lane realization, separate from its caller deadline.
/// Credentials intentionally have no Debug/Display implementation.
#[derive(Clone)]
pub struct Start {
    /// Verified provider subscription groups.
    pub plan: SessionPlan,
    /// Exact canonical/provider mappings.
    pub registrations: Vec<Registration>,
    /// Provider credential, redacted at transport failure boundaries.
    pub key: String,
    /// Qualified cancellable gateway resolver.
    pub gateway: Gateway,
    /// API-selected native evidence policy.
    pub evidence: Evidence,
}
struct BookState {
    attempt: u64,
    generation: u128,
    book: Option<Book<Values>>,
    ready: bool,
    trade_replay_pending: bool,
    statistics_replay_pending: bool,
}
/// Read-only native query view of numeric book state. It contains no provider
/// client, task, cancellation authority or dataset child. Retirement invalidates
/// availability even when a copied view is retained by a query.
#[derive(Clone)]
pub struct Queries {
    retired: CancellationToken,
    book: Arc<Mutex<BookState>>,
    changes: watch::Receiver<u64>,
}
/// Detached coherent raw source copy. No member owns a provider or task.
#[derive(Clone, Copy)]
pub struct RawPrices {
    /// Exact physical book generation.
    pub generation: u128,
    /// Latest accepted execution, including repeated same-price trades.
    pub trade: Option<crate::latest::Tick<crate::latest::TradeValue>>,
    /// Latest accepted quote with independently missing sides preserved.
    pub quote: Option<crate::latest::Tick<crate::latest::QuoteValue>>,
}
impl Queries {
    /// Event notification only; retaining this receiver cannot retain a source
    /// task, socket or dataset cancellation authority.
    pub fn updates(&self) -> watch::Receiver<u64> {
        self.changes.clone()
    }

    /// Current physical identity of copied books only. Retiring source tasks
    /// invalidate this view; retaining it never retains a dataset child/socket.
    ///
    /// # Errors
    /// Interrupted copied-state writer is explicit.
    pub fn generation(&self) -> Result<Option<u128>, String> {
        if self.retired.is_cancelled() {
            return Ok(None);
        }
        let state = self
            .book
            .lock()
            .map_err(|_| "Native query identity writer interrupted")?;
        Ok(if !self.retired.is_cancelled() && state.ready {
            state.book.as_ref().map(Book::generation)
        } else {
            None
        })
    }
    /// Copies raw trade/quote under one short native writer lock. Source
    /// retirement invalidates the view even if a caller retained its Arc.
    ///
    /// # Errors
    /// Interrupted numeric state is explicit, never a healthy invented price.
    pub fn raw_prices(&self, instrument: u32) -> Result<Option<RawPrices>, String> {
        if self.retired.is_cancelled() {
            return Ok(None);
        }
        let state = self
            .book
            .lock()
            .map_err(|_| "Native raw query writer interrupted")?;
        if self.retired.is_cancelled() || !state.ready {
            return Ok(None);
        }
        let Some(book) = state
            .book
            .as_ref()
            .filter(|book| book.contract_id(instrument).is_some())
        else {
            return Ok(None);
        };
        Ok(Some(RawPrices {
            generation: book.generation(),
            trade: book
                .raw_trade(instrument)
                .map_err(|e| format!("Native trade query: {e:?}"))?,
            quote: book
                .raw_quote(instrument)
                .map_err(|e| format!("Native quote query: {e:?}"))?,
        }))
    }
    /// Copies one coherent aggregate and its exact physical generation under a
    /// short native lock. No serialization, callback, I/O or await holds the lock.
    ///
    /// # Errors
    /// An interrupted native writer is explicit and cannot certify usable data.
    pub fn publication_state(
        &self,
        instrument: u32,
    ) -> Result<Option<(u128, super::book::PublicationState)>, String> {
        if self.retired.is_cancelled() {
            return Ok(None);
        }
        let state = self
            .book
            .lock()
            .map_err(|_| "Native query book writer interrupted")?;
        if self.retired.is_cancelled() || !state.ready {
            return Ok(None);
        }
        Ok(state.book.as_ref().and_then(|book| {
            book.contract_id(instrument).map(|_| {
                (
                    book.generation(),
                    book.publication_state(instrument, super::book::Update::Unchanged),
                )
            })
        }))
    }
}
struct BookRetirement(
    CancellationToken,
    Option<Arc<crate::option_chain::coalesced::Coalesced>>,
);
impl Drop for BookRetirement {
    fn drop(&mut self) {
        // Detached queries and contract handles share this retirement signal.
        // No book/pricing lock can delay cancellation or future destruction.
        self.0.cancel();
        if let Some(pricing) = &self.1 {
            pricing.fence();
        }
    }
}
/// Complete provider control/diagnostic event, separate from replaceable prices.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Diagnostic {
    /// Native worker at receipt; zero only for the legacy prototype ABI path.
    pub worker: [u8; 16],
    /// Exact dataset/role lane.
    pub lane: Lane,
    /// Physical session attempt identity.
    pub attempt: u64,
    /// API-controlled source processing time, not a later logging/drain time.
    pub observed: i64,
    /// Older diagnostic records replaced by this bounded ring contribution.
    /// Market input and the retained terminal health record are separate.
    pub replaced: u64,
    /// Original system/error event, including unknown codes and full text.
    pub event: Event,
}
/// Native bounded diagnostic inbox, shared across physical scope replacements.
/// It retains copied text only and has its own event-driven wakeup; logging never
/// depends on another scope's market-data watch or a managed polling loop.
pub struct Diagnostics {
    queue: Mutex<VecDeque<Diagnostic>>,
    changed: watch::Sender<u64>,
}
impl Default for Diagnostics {
    fn default() -> Self {
        let (changed, _) = watch::channel(0);
        Self {
            queue: Mutex::new(VecDeque::new()),
            changed,
        }
    }
}
impl Diagnostics {
    /// Signals receipt immediately after copying into the bounded ring.
    ///
    /// # Errors
    /// Invalid event/oversized text or interrupted internal state is explicit.
    pub fn retain(&self, diagnostic: Diagnostic) -> Result<(), String> {
        match &diagnostic.event {
            Event::System { message, .. } | Event::Error { message, .. }
                if message.len() <= 4096 => {}
            _ => return Err("Invalid or oversized native provider diagnostic".into()),
        }
        {
            let mut queue = self
                .queue
                .lock()
                .map_err(|_| "Native diagnostic writer interrupted")?;
            retain_diagnostic(&mut queue, diagnostic);
        }
        self.changed.send_modify(|n| *n = n.saturating_add(1));
        Ok(())
    }
    /// Copies and removes a bounded batch. Draining does not wake its own reader.
    /// No native resource, provider lock or callback is stored in these records.
    ///
    /// # Errors
    /// Reports interrupted internal state instead of silently losing text.
    pub fn drain(&self, maximum: usize) -> Result<Vec<Diagnostic>, String> {
        let mut queue = self
            .queue
            .lock()
            .map_err(|_| "Native diagnostic writer interrupted")?;
        let count = maximum.min(256).min(queue.len());
        Ok(queue.drain(..count).collect())
    }
    /// Event-driven notifications also cover writes from other physical scopes.
    pub fn changes(&self) -> watch::Receiver<u64> {
        self.changed.subscribe()
    }
}
pub(crate) type DiagnosticSink = Arc<Diagnostics>;
pub(crate) fn retain_diagnostic(queue: &mut VecDeque<Diagnostic>, mut diagnostic: Diagnostic) {
    if queue.len() == 256
        && let Some(old) = queue.pop_front()
    {
        diagnostic.replaced = diagnostic
            .replaced
            .saturating_add(old.replaced)
            .saturating_add(1);
    }
    queue.push_back(diagnostic);
}
struct Active {
    status: watch::Receiver<Status>,
    stop: CancellationToken,
    book: Arc<Mutex<BookState>>,
    retired: CancellationToken,
}
/// Exact source record retained until the application drains it. The generation
/// fences replacement data. Pending options quotes may be replaced per contract;
/// authoritative trade/statistics/order contributions are preserved separately.
#[derive(Clone, Copy, Debug)]
pub enum OutputSource {
    /// Exact copied provider record.
    Observation(super::records::Observation),
    /// Provider trade replay ended for this registered instrument. This is a
    /// control transition, not a fabricated trade or statistics record.
    ReplayComplete(u32),
    /// Completed statistics replay checkpoint for one retained reference date.
    StatisticsReplayComplete(u32),
}
/// Ordered source or control transition together with its frozen derived state.
#[derive(Clone, Debug)]
pub struct Output {
    /// API UTC ticks frozen when this source contribution was processed.
    /// Publication must not substitute a later managed-drain wall clock.
    pub observed: i64,
    /// Logical dataset owner.
    pub lane: Lane,
    /// Exact book identity.
    pub generation: u128,
    /// Owned provider observation, without any decoder borrow.
    pub source: OutputSource,
    /// Derived values frozen by the same writer before a later record can enter.
    pub state: super::book::PublicationState,
    /// Source-time option trade inputs, retained independently of later quote
    /// or reference changes. Replay contributes statistics without enrichment.
    pub trade: Option<crate::option_chain::trade::Captured>,
}
/// Native data-plane owner for exactly one API ValueDate/instance. Full output
/// publication and risk calculation remain distinct owners using these snapshots.
pub struct LiveData {
    date: time::Date,
    instance: u128,
    capacity: usize,
    clock: Arc<Clock>,
    owner: Arc<Owner>,
    lanes: AsyncMutex<Lanes>,
    active: Mutex<BTreeMap<Lane, Active>>,
    // Only three tokens; no query/book transformation ever holds this lock.
    signals: Mutex<BTreeMap<Lane, CancellationToken>>,
    updates: watch::Sender<u64>,
    diagnostics: DiagnosticSink,
    diagnostic_scope: Option<Arc<str>>,
    diagnostic_worker: [u8; 16],
    output: Arc<Mutex<BTreeMap<Lane, super::output::LaneOutput>>>,
    option_pricing: Option<Arc<crate::option_chain::coalesced::Coalesced>>,
    option_statistics: crate::option_chain::statistics::Bank,
}
struct Startup {
    stop: CancellationToken,
    ready: bool,
}
impl Drop for Startup {
    fn drop(&mut self) {
        if !self.ready {
            self.stop.cancel();
        }
    }
}
impl LiveData {
    /// Creates only in-memory ownership; it opens no network or worker process.
    pub fn new(
        date: time::Date,
        instance: u128,
        capacity: usize,
        clock: Arc<Clock>,
        owner: Arc<Owner>,
    ) -> Result<Self, String> {
        if capacity == 0 || capacity > 2112 {
            return Err(
                "Native contract capacity must be 1..2112 including underlying dependencies".into(),
            );
        }
        Book::<Values>::new(date, instance, capacity).map_err(|e| format!("{e:?}"))?;
        Ok(Self {
            date,
            instance,
            capacity,
            clock,
            owner,
            lanes: AsyncMutex::new(Lanes::default()),
            active: Mutex::new(BTreeMap::new()),
            signals: Mutex::new(BTreeMap::new()),
            updates: watch::channel(0).0,
            diagnostics: Arc::default(),
            diagnostic_scope: None,
            diagnostic_worker: [0; 16],
            option_pricing: None,
            option_statistics: Arc::new(Mutex::new(BTreeMap::new())),
            output: Arc::new(Mutex::new(
                [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx]
                    .into_iter()
                    .map(|lane| (lane, super::output::LaneOutput::new()))
                    .collect(),
            )),
        })
    }
    /// Shares the runtime-owned bounded diagnostic sink across physical scopes.
    pub(crate) fn diagnostic_sink(&self) -> DiagnosticSink {
        self.diagnostics.clone()
    }
    /// Copied diagnostic ownership for the direct native NATS logging task.
    /// Retaining this inbox cannot retain a dataset connection or task.
    pub fn diagnostics(&self) -> Arc<Diagnostics> {
        self.diagnostics.clone()
    }

    pub(crate) fn scoped(
        date: time::Date,
        instance: u128,
        capacity: usize,
        clock: Arc<Clock>,
        owner: Arc<Owner>,
        scope: &str,
        sink: DiagnosticSink,
    ) -> Result<Self, String> {
        let mut data = Self::new(date, instance, capacity, clock, owner)?;
        data.diagnostics = sink;
        data.diagnostic_scope = Some(Arc::from(scope));
        Ok(data)
    }
    /// Binds a persistent copied diagnostic ring before provider startup. The
    /// captured worker travels with each record across physical replacements.
    ///
    /// # Errors
    /// An already-started provider or invalid worker cannot change provenance.
    pub fn with_diagnostics(
        mut self,
        sink: Arc<Diagnostics>,
        worker: [u8; 16],
    ) -> Result<Self, String> {
        if worker == [0; 16]
            || !self
                .active
                .lock()
                .map_err(|_| "Native diagnostic binding interrupted")?
                .is_empty()
        {
            return Err("Native diagnostic identity must be bound before startup".into());
        }
        self.diagnostics = sink;
        self.diagnostic_worker = worker;
        Ok(self)
    }
    /// Coalesced native state wake-ups; authoritative records remain in the
    /// independent bounded output and diagnostic buffers.
    pub fn updates(&self) -> watch::Receiver<u64> {
        self.updates.subscribe()
    }
    /// Installs native option ingress before any provider task starts. Only the
    /// containing scope owner drives the separate tracked calculation loops.
    /// This hook never calculates prices while holding the provider book writer.
    pub fn with_option_pricing(
        mut self,
        pricing: Arc<crate::option_chain::coalesced::Coalesced>,
    ) -> Result<Self, String> {
        if !self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .is_empty()
        {
            return Err("Option pricing must be installed before provider startup".into());
        }
        self.option_pricing = Some(pricing);
        Ok(self)
    }
    /// Actual application-supplied instance identity for correlation.
    pub fn instance(&self) -> u128 {
        self.instance
    }
    /// Attaches the native logical statistics bank before provider startup. The
    /// bank contains copied counters/watermarks only, never a socket or task;
    /// joined physical replacement can reuse it without replay double-counting.
    ///
    /// # Errors
    /// Active provider ingress, interrupted state or excessive retained capacity.
    pub fn with_option_statistics(
        mut self,
        bank: crate::option_chain::statistics::Bank,
    ) -> Result<Self, String> {
        if !self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .is_empty()
        {
            return Err("Statistics bank must attach before provider startup".into());
        }
        if bank
            .lock()
            .map_err(|_| "Native logical statistics interrupted")?
            .len()
            > self.capacity
        {
            return Err("Native logical statistics capacity exceeded".into());
        }
        self.option_statistics = bank;
        Ok(self)
    }
    /// Restores an application-saved statistic before any provider starts. Only
    /// the explicit epoch ValueDate qualifies dates. It does not invent official
    /// flags, observation times or historical replay watermarks.
    pub fn seed_option_statistics(
        &self,
        instrument: u32,
        seed: crate::option_chain::statistics::Seed,
    ) -> Result<(), String> {
        if instrument == 0
            || self
                .active
                .lock()
                .map_err(|_| "Native lane registry interrupted")?
                .contains_key(&Lane::OptionsGlbx)
        {
            return Err("Option statistics must be seeded before provider startup".into());
        }
        let mut bank = self
            .option_statistics
            .lock()
            .map_err(|_| "Option statistics writer interrupted")?;
        if !bank.contains_key(&instrument) && bank.len() == self.capacity {
            return Err("Option statistics capacity exceeded".into());
        }
        bank.entry(instrument).or_default().seed(seed, self.date);
        Ok(())
    }
    /// Removes already received diagnostics for application logging. Capacity is
    /// reserved independently from price data. Bounded replacement is counted;
    /// the provider failure and terminal health record remain separate.
    pub fn drain_diagnostics(&self) -> Result<Vec<Diagnostic>, String> {
        self.drain_diagnostic_batch(256)
    }
    /// Removes at most a caller-selected batch, retaining the undelivered tail.
    pub fn drain_diagnostic_batch(&self, capacity: usize) -> Result<Vec<Diagnostic>, String> {
        self.diagnostics.drain(capacity)
    }
    /// Removes bounded input records after native accumulation. Pending options
    /// quotes retain the latest per contract; authoritative capacity exhaustion
    /// is a visible recovery fault, never a hidden contribution drop.
    pub fn drain_output(&self, output: &mut [Output]) -> Result<usize, String> {
        let ready: u8 = self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .iter()
            .filter(|(_, v)| v.status.borrow().phase == Phase::Running && !v.stop.is_cancelled())
            .fold(0, |bits, (lane, _)| bits | (1 << (*lane as u8)));
        let mut queue = self
            .output
            .lock()
            .map_err(|_| "Native output writer interrupted")?;
        let mut count = 0;
        while count < output.len() {
            let prior = count;
            for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
                if ready & (1 << (lane as u8)) == 0 {
                    continue;
                }
                if count == output.len() {
                    break;
                }
                if let Some(value) = queue
                    .get_mut(&lane)
                    .and_then(super::output::LaneOutput::pop_front)
                {
                    output[count] = value;
                    count += 1;
                }
            }
            if count == prior {
                break;
            }
        }
        Ok(count)
    }
    /// Copies one ready lane record transactionally. A consumer returning None
    /// leaves it queued (for example, an undersized manifest destination). No
    /// callback may await or perform I/O while this bounded output writer is held.
    pub fn consume_output<T>(
        &self,
        lane: Lane,
        consume: impl FnOnce(&Output) -> Result<Option<T>, String>,
    ) -> Result<Option<T>, String> {
        let ready = self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .get(&lane)
            .is_some_and(|active| {
                active.status.borrow().phase == Phase::Running && !active.stop.is_cancelled()
            });
        if !ready {
            return Ok(None);
        }
        let mut queues = self
            .output
            .lock()
            .map_err(|_| "Native output writer interrupted")?;
        let queue = queues.get_mut(&lane).ok_or("Native output lane absent")?;
        let Some(front) = queue.front() else {
            return Ok(None);
        };
        let Some(value) = consume(front)? else {
            return Ok(None);
        };
        queue.pop_front();
        Ok(Some(value))
    }
    /// Starts one tracked lane and awaits its required native processing evidence.
    /// Cancellation signals a partial start immediately; explicit stop joins it.
    pub async fn start(
        &self,
        request: Start,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Status, String> {
        self.start_owned(request, token, deadline, None).await
    }
    /// Starts an independent options book/session under an existing logical
    /// options owner. Parent cancellation retires its internal readers and all
    /// network resources; a scope reset does not replace sibling books.
    pub async fn start_nested(
        &self,
        request: Start,
        token: &CancellationToken,
        deadline: Deadline,
        parent: &crate::runtime::owner::Child,
    ) -> Result<Status, String> {
        if request.plan.lane != Lane::OptionsGlbx || !Arc::ptr_eq(&self.owner, &parent.owner()) {
            return Err("Nested option scope must belong to this runtime's options owner".into());
        }
        self.start_owned(request, token, deadline, Some(parent))
            .await
    }
    /// Starts a provider beneath the dataset's shared native resource parent so
    /// its publication and provider tasks use one logical slot. Cross-runtime
    /// parents are rejected before any socket or registration is admitted.
    /// Existing option-only `start_nested` validation remains unchanged.
    ///
    /// # Errors
    /// Rejects a foreign parent or invalid provider realization.
    pub async fn start_in_parent(
        &self,
        request: Start,
        token: &CancellationToken,
        deadline: Deadline,
        parent: &crate::runtime::owner::Child,
    ) -> Result<Status, String> {
        if !self.belongs_to(parent) {
            return Err("Dataset resources must belong to this runtime's parent".into());
        }
        self.start_owned(request, token, deadline, Some(parent))
            .await
    }
    pub(crate) fn belongs_to(&self, parent: &crate::runtime::owner::Child) -> bool {
        Arc::ptr_eq(&self.owner, &parent.owner())
    }
    async fn start_owned(
        &self,
        request: Start,
        token: &CancellationToken,
        deadline: Deadline,
        parent: Option<&crate::runtime::owner::Child>,
    ) -> Result<Status, String> {
        let Start {
            plan,
            registrations,
            key,
            gateway,
            evidence,
        } = request;
        let deadline = deadline.earliest(Deadline::recovery());
        let lane = plan.lane;
        plan.validate().map_err(|e| format!("{e:?}"))?;
        if registrations.is_empty() || registrations.len() > self.capacity {
            return Err("Invalid native registration count".into());
        }
        if matches!(evidence, Evidence::Observations) && !registrations.iter().any(|r| r.required) {
            return Err("Observation evidence requires explicit required contracts".into());
        }
        let mut ids = BTreeSet::new();
        let mut instruments = BTreeSet::new();
        for r in &registrations {
            let identity =
                crate::catalog::selection::parse(&r.contract).map_err(|e| format!("{e:?}"))?;
            if !ids.insert(r.contract.clone())
                || !instruments.insert(r.instrument)
                || r.instrument == 0
                || (identity.kind == 1) != (r.asset == Asset::Futures)
                // The options owner also owns its exact-underlying quote
                // dependency. That dependency is distinct from the independent
                // primary GLBX futures owner and is retired with options only.
                || (lane != Lane::OptionsGlbx && r.asset == Asset::Option)
                || !plan
                    .subscriptions
                    .iter()
                    .any(|s| s.symbols.contains(&r.source))
            {
                return Err(
                    "Native mapping is duplicate, unsupported or absent from subscriptions".into(),
                );
            }
        }
        let mut lanes = deadline
            .run(token, self.lanes.lock())
            .await
            .map_err(|e| e.to_string())?;
        // Explicit activation after a terminal failure replaces the exited
        // owner, rather than sending a reset to a receiver that no longer runs.
        // Join first, under the same original budget, before allocating another.
        let ended = self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .get(&lane)
            .is_some_and(|value| {
                value.stop.is_cancelled()
                    || matches!(value.status.borrow().phase, Phase::Failed | Phase::Stopped)
            });
        if ended {
            lanes
                .stop(lane, deadline)
                .await
                .map_err(|e| format!("{e:?}"))?;
            if let Some(old) = self
                .active
                .lock()
                .map_err(|_| "Native lane registry interrupted")?
                .remove(&lane)
                && let Some(book) = old
                    .book
                    .lock()
                    .map_err(|_| "Native book writer interrupted")?
                    .book
                    .as_mut()
            {
                book.retire().map_err(|e| format!("{e:?}"))?;
            }
            if let Some(queue) = self
                .output
                .lock()
                .map_err(|_| "Native output writer interrupted")?
                .get_mut(&lane)
            {
                queue.clear();
            }
        }
        if self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .contains_key(&lane)
        {
            return Err("Native lane already active".into());
        }
        self.output
            .lock()
            .map_err(|_| "Native output writer interrupted")?
            .get_mut(&lane)
            .ok_or("Native output lane absent")?
            .configure(
                registrations
                    .iter()
                    .filter(|_| lane == Lane::OptionsGlbx)
                    .map(|r| r.instrument),
            );
        let retired = CancellationToken::new();
        let processor_retired = retired.clone();
        let book = Arc::new(Mutex::new(BookState {
            attempt: 0,
            generation: 0,
            book: None,
            ready: false,
            trade_replay_pending: false,
            statistics_replay_pending: false,
        }));
        let has_trade_replay = plan
            .subscriptions
            .iter()
            .any(|s| s.schema == databento::dbn::Schema::Trades && s.replay_start_ns.is_some());
        let has_statistics_replay = plan
            .subscriptions
            .iter()
            .any(|s| s.schema == databento::dbn::Schema::Statistics && s.replay_start_ns.is_some());
        let statistics_instruments: BTreeSet<_> = registrations
            .iter()
            .filter(|r| {
                r.asset == Asset::Futures
                    && plan.subscriptions.iter().any(|s| {
                        s.schema == databento::dbn::Schema::Statistics
                            && s.symbols.contains(&r.source)
                    })
            })
            .map(|r| r.instrument)
            .collect();
        let trade_instruments: BTreeSet<_> = registrations
            .iter()
            .filter(|r| {
                r.asset == Asset::Futures
                    && plan.subscriptions.iter().any(|s| {
                        s.schema == databento::dbn::Schema::Trades && s.symbols.contains(&r.source)
                    })
            })
            .map(|r| r.instrument)
            .collect();
        let option_trade_instruments: BTreeSet<_> = registrations
            .iter()
            .filter(|r| {
                r.asset == Asset::Option
                    && plan.subscriptions.iter().any(|s| {
                        s.schema == databento::dbn::Schema::Trades && s.symbols.contains(&r.source)
                    })
            })
            .map(|r| r.instrument)
            .collect();
        let state = book.clone();
        let date = self.date;
        let capacity = self.capacity;
        let clock = self.clock.clone();
        let updates = self.updates.clone();
        let diagnostics = self.diagnostics.clone();
        let diagnostic_worker = self.diagnostic_worker;
        let diagnostic_scope = self.diagnostic_scope.clone();
        let output = self.output.clone();
        let notifier = self.owner.clone();
        let pricing = if lane == Lane::OptionsGlbx {
            self.option_pricing.clone()
        } else {
            None
        };
        let option_statistics = self.option_statistics.clone();
        let retirement = BookRetirement(retired.clone(), pricing.clone());
        let processor = move |attempt, event: &Event| {
            let _retirement = &retirement;
            let mut state = state
                .lock()
                .map_err(|_| "Native book writer interrupted".to_owned())?;
            if state.attempt != attempt {
                output
                    .lock()
                    .map_err(|_| "Native output writer interrupted")?
                    .get_mut(&lane)
                    .ok_or("Native output lane absent")?
                    .clear();
                if let Some(old) = state.book.as_mut() {
                    old.retire().map_err(|e| format!("{e:?}"))?;
                }
                // Opaque fresh identity; deriving it by XOR with a caller GUID
                // can produce zero or overlapping namespaces.
                let generation = (0..4)
                    .map(|_| rand::random::<u128>())
                    .find(|id| *id != 0 && *id != state.generation)
                    .ok_or("Native generation allocation failed")?;
                if let Some(pricing) = &pricing {
                    pricing.rebind(generation)?;
                }
                let mut book =
                    Book::with_stop(date, generation, capacity, processor_retired.clone())
                        .map_err(|e| format!("{e:?}"))?;
                for r in &registrations {
                    book.register_with_policy(
                        r.instrument,
                        &r.contract,
                        r.asset,
                        8,
                        r.tick,
                        r.vwap,
                    )
                    .map_err(|e| format!("{e:?}"))?;
                }
                if lane == Lane::OptionsGlbx {
                    book.bind_option_statistics(option_statistics.clone())
                        .map_err(|e| format!("{e:?}"))?;
                }
                state.book = Some(book);
                state.attempt = attempt;
                state.generation = generation;
                state.ready = false;
                state.trade_replay_pending = has_trade_replay;
                state.statistics_replay_pending = has_statistics_replay;
            }
            let generation = state.generation;
            if matches!(event, Event::System { .. } | Event::Error { .. }) {
                let mut diagnostic = event.clone();
                if let Some(scope) = &diagnostic_scope {
                    match &mut diagnostic {
                        Event::System { message, .. } | Event::Error { message, .. } => {
                            *message =
                                format!("[scope={scope}; generation={generation:032x}] {message}")
                        }
                        _ => {}
                    }
                }
                diagnostics.retain(Diagnostic {
                    worker: diagnostic_worker,
                    lane,
                    attempt,
                    observed: clock.utc_ticks().map_err(|e| format!("{e:?}"))?,
                    replaced: 0,
                    event: diagnostic,
                })?;
                updates.send_modify(|version| *version = version.saturating_add(1));
                notifier
                    .signal(1 << (lane as u32))
                    .map_err(|_| "Native delivery signal interrupted")?;
            }
            let was_ready = state.ready;
            let complete_trades = state.trade_replay_pending
                && matches!(event,Event::System{code:3,message}if message.contains("trades"));
            if complete_trades {
                state.trade_replay_pending = false;
            }
            let complete_statistics = state.statistics_replay_pending
                && matches!(event,Event::System{code:3,message}if message.contains("statistics"));
            if complete_statistics {
                state.statistics_replay_pending = false;
            }
            let book = state.book.as_mut().ok_or("Native book absent")?;
            if complete_trades {
                for instrument in &option_trade_instruments {
                    book.complete_option_replay(generation, *instrument)
                        .map_err(|e| format!("{e:?}"))?;
                }
                let mut queues = output
                    .lock()
                    .map_err(|_| "Native output writer interrupted")?;
                let queue = queues.get_mut(&lane).ok_or("Native output lane absent")?;
                if trade_instruments.len() > queue.available() {
                    return Err("Authoritative application output capacity exhausted".into());
                }
                for instrument in &trade_instruments {
                    let epoch = rand::random::<u128>().max(1);
                    book.complete_replay(generation, *instrument, epoch)
                        .map_err(|e| format!("{e:?}"))?;
                    let state = book.publication_state(*instrument, super::book::Update::Unchanged);
                    queue.push_back(Output {
                        observed: clock.utc_ticks().map_err(|e| format!("{e:?}"))?,
                        lane,
                        generation,
                        source: OutputSource::ReplayComplete(*instrument),
                        state,
                        trade: None,
                    })?;
                }
                updates.send_modify(|version| *version = version.saturating_add(1));
                notifier
                    .signal(1 << (lane as u32))
                    .map_err(|_| "Native delivery signal interrupted")?;
            }
            if complete_statistics {
                let mut queues = output
                    .lock()
                    .map_err(|_| "Native output writer interrupted")?;
                let queue = queues.get_mut(&lane).ok_or("Native output lane absent")?;
                for instrument in &statistics_instruments {
                    for statistics in book.all_statistics(*instrument) {
                        queue.push_back(Output {
                            observed: clock.utc_ticks().map_err(|e| format!("{e:?}"))?,
                            lane,
                            generation,
                            source: OutputSource::StatisticsReplayComplete(*instrument),
                            state: super::book::PublicationState {
                                session: Some(statistics),
                                ..Default::default()
                            },
                            trade: None,
                        })?;
                    }
                }
                updates.send_modify(|version| *version = version.saturating_add(1));
                notifier
                    .signal(1 << (lane as u32))
                    .map_err(|_| "Native delivery signal interrupted")?;
            }
            if let Event::Mapping {
                requested,
                instrument,
                ..
            } = event
                && let Some(r) = registrations.iter().find(|r| r.source == *requested)
                && r.instrument != *instrument
            {
                return Err(
                    "Provider mapping changed; reference requalification is required".into(),
                );
            }
            if let Event::Market(record) = event {
                let header = match record {
                    super::records::Observation::Quote(value) => value.header,
                    super::records::Observation::Trade(value) => value.header,
                    super::records::Observation::Order(value) => value.header,
                    super::records::Observation::Statistic(value) => value.header,
                };
                if let Some(registration) = registrations
                    .iter()
                    .find(|r| r.instrument == header.instrument)
                    && !publisher_matches(lane, registration.publisher, header.publisher)
                {
                    return Err(format!(
                        "Provider publisher mismatch for instrument {}: expected {}, received {}; reference requalification is required",
                        header.instrument, registration.publisher, header.publisher
                    ));
                }
                let local = clock.utc_ticks().map_err(|e| format!("{e:?}"))?;
                let update = book
                    .apply(generation, *record, local)
                    .map_err(|e| format!("{e:?}"))?;
                if let (Some(pricing), super::records::Observation::Quote(quote)) =
                    (&pricing, record)
                {
                    pricing.observe_wire(generation, *quote, local)?;
                }
                let instrument = match record {
                    super::records::Observation::Quote(v) => v.header.instrument,
                    super::records::Observation::Trade(v) => v.header.instrument,
                    super::records::Observation::Order(v) => v.header.instrument,
                    super::records::Observation::Statistic(v) => v.header.instrument,
                };
                let trade = if matches!(record, super::records::Observation::Trade(value) if value.header.flags & 2 == 0)
                {
                    if let Some(prepared) = pricing
                        .as_ref()
                        .map(|engine| engine.trade_context(instrument, generation))
                        .transpose()?
                        .flatten()
                    {
                        let context = &prepared.context;
                        let logical = u128::from(context.qualification.generation_low)
                            | (u128::from(context.qualification.generation_high) << 64);
                        let underlying = book
                            .raw_quote(context.underlying)
                            .map_err(|e| format!("{e:?}"))?
                            .and_then(|value| {
                                crate::option_chain::coalesced::underlying_quote(value, logical)
                            });
                        Some(crate::option_chain::trade::Captured {
                            prepared,
                            underlying,
                            at: clock.utc_ticks().map_err(|e| format!("{e:?}"))?,
                        })
                    } else {
                        None
                    }
                } else {
                    None
                };
                let suppressed_option_quote = lane == Lane::OptionsGlbx
                    && matches!(record, super::records::Observation::Quote(_))
                    && update == super::book::Update::Unchanged;
                // Replay has already contributed to the authoritative native
                // book. Application consumers suppress these historical rows;
                // retaining them until Running would fill the live FIFO before
                // replay completion could qualify startup. Deliver the frozen
                // completion checkpoint instead, preserving every contribution.
                let replay = match record {
                    super::records::Observation::Quote(v) => v.header.flags & 2 != 0,
                    super::records::Observation::Trade(v) => v.header.flags & 2 != 0,
                    super::records::Observation::Order(v) => v.header.flags & 2 != 0,
                    super::records::Observation::Statistic(v) => v.header.flags & 2 != 0,
                };
                if book.contract_id(instrument).is_some() && !suppressed_option_quote && !replay {
                    let state = book.publication_state(instrument, update);
                    let mut queues = output
                        .lock()
                        .map_err(|_| "Native output writer interrupted")?;
                    let queue = queues.get_mut(&lane).ok_or("Native output lane absent")?;
                    queue.push_back(Output {
                        observed: local,
                        lane,
                        generation,
                        source: OutputSource::Observation(*record),
                        state,
                        trade,
                    })?;
                }
                updates.send_modify(|version| *version = version.saturating_add(1));
                notifier
                    .signal(1 << (lane as u32))
                    .map_err(|_| "Native delivery signal interrupted")?;
            }
            if matches!(evidence, Evidence::ControlOnly) {
                // The physical book/identity exists after a valid provider
                // record. Individual trade/quote fields remain None until real
                // observations arrive; this never manufactures market data.
                state.ready = true;
                return Ok(true);
            }
            if was_ready {
                return Ok(true);
            }
            for r in registrations.iter().filter(|r| r.required) {
                let reader = book
                    .reader(&r.contract)
                    .map_err(|e| format!("{e:?}"))?
                    .ok_or("Native reader absent")?;
                let usable = reader
                    .trade()
                    .map_err(|e| format!("{e:?}"))?
                    .is_some_and(|t| t.value.price > 0)
                    || reader.quote().map_err(|e| format!("{e:?}"))?.is_some_and(
                        |q| matches!((q.value.bid,q.value.ask),(Some(b),Some(a))if b>0&&a>=b),
                    );
                if !usable {
                    return Ok(false);
                }
            }
            state.ready = true;
            Ok(true)
        };
        let mut status = if let Some(parent) = parent {
            lanes.start_nested(plan, key, gateway, processor, deadline, parent)
        } else {
            lanes.start_tracked(plan, key, gateway, processor, deadline, &self.owner)
        }
        .map_err(|e| format!("{e:?}"))?;
        let stop = lanes
            .cancellation(lane)
            .ok_or("Native lane cancellation absent")?;
        self.signals
            .lock()
            .map_err(|_| "Native stop signal directory interrupted")?
            .insert(lane, stop.clone());
        self.active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .insert(
                lane,
                Active {
                    status: status.clone(),
                    stop: stop.clone(),
                    book,
                    retired,
                },
            );
        drop(lanes);
        let mut startup = Startup { stop, ready: false };
        // This wait certifies the subscription connection, never a usable quote.
        // SDK heartbeat silence, disconnect and provider errors own its liveness.
        let result = Self::running(&mut status, token, deadline, None).await;
        if result.is_ok() {
            startup.ready = true;
        }
        result
    }
    /// Requests clean session replacement using an existing incident deadline.
    /// This proves native transport/observation readiness only, not actor output.
    pub async fn reset(
        &self,
        lane: Lane,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Status, String> {
        let deadline = deadline.earliest(Deadline::recovery());
        let (mut status, prior) = {
            let active = self
                .active
                .lock()
                .map_err(|_| "Native lane registry interrupted")?;
            let value = active.get(&lane).ok_or("Native lane not registered")?;
            (value.status.clone(), value.status.borrow().generation)
        };
        let lanes = deadline
            .run(token, self.lanes.lock())
            .await
            .map_err(|e| e.to_string())?;
        lanes
            .reset_with_deadline(lane, deadline)
            .map_err(|e| format!("{e:?}"))?;
        drop(lanes);
        Self::running(&mut status, token, deadline, Some(prior)).await
    }
    /// Reports an unexpected independent calculation/delivery failure to the
    /// existing lane state machine. No quotes need arrive to trigger recovery.
    /// Admission uses the supplied original deadline; it is never renewed here.
    pub async fn report_fault(
        &self,
        lane: Lane,
        reason: &str,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        let lanes = deadline
            .run(token, self.lanes.lock())
            .await
            .map_err(|e| e.to_string())?;
        lanes
            .report_fault(lane, reason, deadline)
            .map_err(|e| format!("{e:?}"))
    }
    /// Emergency signal without waiting for any async lifecycle lock. The owner
    /// still retains/join-tracks resources until actual task exit is confirmed.
    pub fn cancel_lane(&self, lane: Lane) -> Result<(), String> {
        // Emergency control never waits for a price reader or book mutation.
        let signals = self
            .signals
            .lock()
            .map_err(|_| "Native stop signal directory interrupted")?;
        if let Some(stop) = signals.get(&lane) {
            stop.cancel();
        }
        Ok(())
    }
    async fn running(
        status: &mut watch::Receiver<Status>,
        token: &CancellationToken,
        deadline: Deadline,
        after: Option<u64>,
    ) -> Result<Status, String> {
        deadline
            .run(token, async {
                loop {
                    let state = status.borrow_and_update().clone();
                    match state.phase {
                        Phase::Running if after.is_none_or(|old| state.generation > old) => {
                            return Ok(state);
                        }
                        Phase::Failed => {
                            return Err(state
                                .error
                                .unwrap_or_else(|| "Native recovery deadline expired".into()));
                        }
                        Phase::Stopped => return Err("Native lane stopped".into()),
                        _ => {}
                    }
                    status
                        .changed()
                        .await
                        .map_err(|_| "Native lane owner ended".to_owned())?;
                }
            })
            .await
            .map_err(|e| e.to_string())?
    }
    /// Event-driven native lifecycle observation. The receiver retains copied
    /// status only; keeping it cannot keep a provider socket or task alive.
    pub fn lifecycle(&self, lane: Lane) -> Result<Option<watch::Receiver<Status>>, String> {
        Ok(self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .get(&lane)
            .map(|active| active.status.clone()))
    }
    /// Reads lifecycle without waiting for provider I/O or the async stop lock.
    pub fn status(&self, lane: Lane) -> Result<Option<Status>, String> {
        Ok(self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .get(&lane)
            .map(|value| value.status.borrow().clone()))
    }
    /// Immediate canonical reader. Unready/stopping lanes return no current data.
    pub fn reader(&self, lane: Lane, id: &str) -> Result<Option<Arc<Contract<Values>>>, String> {
        let active = self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?;
        let Some(value) = active.get(&lane) else {
            return Ok(None);
        };
        if value.status.borrow().phase != Phase::Running || value.stop.is_cancelled() {
            return Ok(None);
        }
        let state = value
            .book
            .lock()
            .map_err(|_| "Native book writer interrupted")?;
        state.book.as_ref().map_or(Ok(None), |book| {
            book.reader(id).map_err(|e| format!("{e:?}"))
        })
    }
    /// Copies a read-only native query view, never the physical source owner.
    /// A subsequent resource-tree cancellation does not wait for these numeric
    /// data copies; they become unavailable when their book retires.
    ///
    /// # Errors
    /// Interrupted native registry state is reported explicitly.
    pub fn queries(&self, lane: Lane) -> Result<Option<Queries>, String> {
        let active = self
            .active
            .lock()
            .map_err(|_| "Native query registry interrupted")?;
        Ok(active.get(&lane).map(|active| Queries {
            book: active.book.clone(),
            retired: active.retired.clone(),
            changes: self.updates.subscribe(),
        }))
    }
    fn with_book<T>(
        &self,
        lane: Lane,
        read: impl FnOnce(&Book<Values>) -> Option<T>,
    ) -> Result<Option<T>, String> {
        let active = self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?;
        let Some(value) = active.get(&lane) else {
            return Ok(None);
        };
        if value.status.borrow().phase != Phase::Running || value.stop.is_cancelled() {
            return Ok(None);
        }
        let state = value
            .book
            .lock()
            .map_err(|_| "Native book writer interrupted")?;
        Ok(state.book.as_ref().and_then(read))
    }
    /// Current book identity only while the lane is ready.
    pub fn generation(&self, lane: Lane) -> Result<Option<u128>, String> {
        self.with_book(lane, |b| Some(b.generation()))
    }
    /// Coherent native underlying input for the option scheduler. Provider
    /// retirement and poisoned writers cannot masquerade as a healthy quote.
    pub fn raw_quote(
        &self,
        lane: Lane,
        instrument: u32,
    ) -> Result<Option<crate::latest::Tick<crate::latest::QuoteValue>>, String> {
        self.with_book(lane, |book| Some(book.raw_quote(instrument)))?
            .transpose()
            .map(Option::flatten)
            .map_err(|e| format!("{e:?}"))
    }
    /// Reads one coherent derived state and its exact book generation under the
    /// same writer lock. Recovery never pairs old values with a new identity.
    pub fn publication_state(
        &self,
        lane: Lane,
        instrument: u32,
    ) -> Result<Option<(u128, super::book::PublicationState)>, String> {
        self.with_book(lane, |book| {
            book.contract_id(instrument).map(|_| {
                (
                    book.generation(),
                    book.publication_state(instrument, super::book::Update::Unchanged),
                )
            })
        })
    }
    /// Reads usable session statistics without waiting for network work.
    pub fn statistics(
        &self,
        lane: Lane,
        instrument: u32,
        date: time::Date,
    ) -> Result<Option<crate::aggregation::session::Snapshot>, String> {
        self.with_book(lane, |b| b.statistics(instrument, date))
    }
    /// Reads the selected authoritative VWAP checkpoint, including incomplete flags.
    pub fn vwap(
        &self,
        lane: Lane,
        instrument: u32,
    ) -> Result<Option<crate::aggregation::vwap::Checkpoint>, String> {
        self.with_book(lane, |b| b.vwap(instrument))
    }
    /// Reads the publication price separately from the raw last observation.
    pub fn market_price(
        &self,
        lane: Lane,
        instrument: u32,
    ) -> Result<Option<crate::latest::market::Snapshot>, String> {
        self.with_book(lane, |b| b.market_price(instrument))
    }
    /// Signals the selected lane before awaiting lifecycle serialization, then
    /// joins actual task exit and retires retained readers. Other lanes continue.
    pub async fn stop(
        &self,
        lane: Lane,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), String> {
        let deadline = deadline.earliest(Deadline::recovery());
        self.cancel_lane(lane)?;
        let mut lanes = deadline
            .run(token, self.lanes.lock())
            .await
            .map_err(|e| e.to_string())?;
        lanes
            .stop(lane, deadline)
            .await
            .map_err(|e| format!("{e:?}"))?;
        if let Some(queue) = self
            .output
            .lock()
            .map_err(|_| "Native output writer interrupted")?
            .get_mut(&lane)
        {
            queue.clear();
        }
        if let Some(value) = self
            .active
            .lock()
            .map_err(|_| "Native lane registry interrupted")?
            .remove(&lane)
            && let Some(book) = value
                .book
                .lock()
                .map_err(|_| "Native book writer interrupted")?
                .book
                .as_mut()
        {
            book.retire().map_err(|e| format!("{e:?}"))?;
        }
        Ok(())
    }
}
impl Drop for LiveData {
    fn drop(&mut self) {
        if let Ok(active) = self.active.get_mut() {
            for value in active.values() {
                value.stop.cancel();
                value.retired.cancel();
            }
        }
    }
}

/// XCBF publishes exchange and off-exchange records for the same instrument in
/// one dataset. Definitions normally name XCBF (105), while legitimate clearing
/// statistics can name XOFF (106). Keep priced options strict and reject every
/// unrelated publisher; the pinned DBN registry defines this exact dataset pair.
fn publisher_matches(lane: Lane, expected: u16, received: u16) -> bool {
    use databento::dbn::Publisher;
    expected == 0
        || expected == received
        || lane == Lane::FuturesXcbf
            && matches!(
                (Publisher::try_from(expected), Publisher::try_from(received)),
                (Ok(Publisher::XcbfPitchXcbf), Ok(Publisher::XcbfPitchXoff))
                    | (Ok(Publisher::XcbfPitchXoff), Ok(Publisher::XcbfPitchXcbf))
            )
}

#[cfg(test)]
mod publisher_tests {
    use super::*;
    #[test]
    fn xcbf_off_exchange_is_same_dataset_but_options_and_unrelated_sources_stay_strict() {
        assert!(publisher_matches(Lane::FuturesXcbf, 105, 106));
        assert!(publisher_matches(Lane::FuturesXcbf, 106, 105));
        for lane in [Lane::FuturesGlbx, Lane::OptionsGlbx] {
            assert!(!publisher_matches(lane, 105, 106));
        }
        assert!(!publisher_matches(Lane::FuturesXcbf, 105, 3));
        assert!(!publisher_matches(Lane::FuturesXcbf, 3, 106));
        assert!(!publisher_matches(Lane::FuturesXcbf, 105, 65535));
        assert!(publisher_matches(Lane::FuturesXcbf, 105, 105));
    }
}
