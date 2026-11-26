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
    runtime::{
        Deadline, capture_updates::{CaptureUpdates, Subscription}, clock::Clock, owner::Owner,
    },
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
    /// Options setup accepts subscription acknowledgements while opportunistic
    /// monitored MBP-1 replay continues. Trade/statistics replay stays required.
    ControlOnlyWithOptionalQuoteReplay,
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

impl Start {
    /// Checks the complete source plan before opening a socket or retiring a live generation.
    pub(super) fn validate(&self, capacity: usize) -> Result<(), String> {
        self.plan.validate().map_err(|error| format!("{error:?}"))?;
        if self.registrations.is_empty() || self.registrations.len() > capacity {
            return Err("Invalid native registration count".into());
        }
        if self.evidence == Evidence::Observations && !self.registrations.iter().any(|row| row.required) {
            return Err("Observation evidence requires explicit required contracts".into());
        }

        let subscribed = self.plan.subscriptions.iter()
            .flat_map(|subscription| subscription.symbols.iter().map(String::as_str))
            .collect::<BTreeSet<_>>();
        let mut contracts = BTreeSet::new();
        let mut instruments = BTreeSet::new();
        for row in &self.registrations {
            let reject = |reason| format!(
                "Invalid native registration: contract={:?}, instrument={}, publisher={}, source={:?}, asset={:?}: {reason}",
                row.contract, row.instrument, row.publisher, row.source, row.asset,
            );
            if row.contract.len() > 256 {
                return Err(reject("contract ID exceeds the native book limit"));
            }
            // Published option IDs may include an immutable provider suffix
            // when two distinct instruments share the same expiry/right/strike.
            let (canonical, provider) = row.contract.split_once('~')
                .map_or((row.contract.as_str(), None), |(id, suffix)| (id, Some(suffix)));
            let identity = crate::catalog::selection::parse(canonical)
                .map_err(|_| reject("invalid canonical contract ID"))?;
            if !contracts.insert(&row.contract) {
                return Err(reject("duplicate canonical contract ID"));
            }
            if row.instrument == 0 || !instruments.insert(row.instrument) {
                return Err(reject("zero or duplicate provider instrument ID"));
            }
            if (identity.kind == 1) != (row.asset == Asset::Futures)
                || (self.plan.lane != Lane::OptionsGlbx && row.asset == Asset::Option)
            {
                return Err(reject("contract kind does not match the registration or dataset lane"));
            }
            if row.vwap && row.asset != Asset::Futures {
                return Err(reject("VWAP registration requires a futures contract"));
            }
            if let Some(provider) = provider {
                let valid_binding = row.asset == Asset::Option && row.publisher != 0
                    && provider.split_once('-').is_some_and(|(publisher, instrument)| {
                        publisher.bytes().all(|byte| byte.is_ascii_digit())
                            && instrument.bytes().all(|byte| byte.is_ascii_digit())
                            && publisher.parse::<u16>() == Ok(row.publisher)
                            && instrument.parse::<u32>() == Ok(row.instrument)
                    });
                if !valid_binding {
                    return Err(reject("provider suffix does not match the option publisher and instrument"));
                }
            }
            if !subscribed.contains(row.source.as_str()) {
                return Err(reject("provider source is missing from subscriptions"));
            }
        }
        Ok(())
    }
}

#[cfg(test)]
mod registration_tests {
    use super::*;
    use crate::dataset::transport::SubscriptionSpec;
    use databento::dbn::{SType, Schema};

    fn option_plan() -> Start {
        let registrations = [
            ("ES20261218C10000", 42474200),
            ("ES20261218C10000~1-42474429", 42474429),
        ].into_iter().map(|(contract, instrument)| Registration {
            instrument,
            publisher: 1,
            contract: contract.into(),
            source: instrument.to_string(),
            asset: Asset::Option,
            tick: None,
            vwap: false,
            required: false,
        }).collect::<Vec<_>>();
        Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: vec![SubscriptionSpec {
                    symbols: registrations.iter().map(|row| row.source.clone()).collect(),
                    symbology: SType::InstrumentId,
                    schema: Schema::Mbp1,
                    replay_start_ns: None,
                }],
            },
            registrations,
            key: String::new(),
            gateway: Gateway {
                host: "127.0.0.1".into(),
                port: 1,
                resolver: Arc::new(crate::dns::Resolver::new(vec!["127.0.0.1:53".parse().unwrap()]).unwrap()),
            },
            evidence: Evidence::ControlOnly,
        }
    }

    #[test]
    fn published_provider_qualified_option_is_distinct_from_legacy_contract() {
        assert!(option_plan().validate(2).is_ok());
    }

    #[test]
    fn malformed_or_mismatched_provider_suffix_reports_the_registration() {
        for suffix in ["2-42474429", "1-42474200", "1-0", "1-42474429~1-42474429", "+1-42474429"] {
            let mut plan = option_plan();
            let contract = format!("ES20261218C10000~{suffix}");
            plan.registrations[1].contract = contract.clone();
            let error = plan.validate(2).unwrap_err();
            assert!(error.contains(&contract), "{error}");
            assert!(error.contains("instrument=42474429, publisher=1"), "{error}");
            assert!(error.contains("provider suffix does not match"), "{error}");
        }
    }

    #[test]
    fn validates_canonical_identity_and_subscription_membership_before_setup() {
        let mut plan = option_plan();
        plan.registrations[1].contract = "ES20261318C10000~1-42474429".into();
        assert!(plan.validate(2).unwrap_err().contains("invalid canonical contract ID"));

        let mut plan = option_plan();
        plan.plan.subscriptions[0].symbols.pop();
        assert!(plan.validate(2).unwrap_err().contains("provider source is missing"));
    }
}

struct BookState {
    attempt: u64,
    generation: u128,
    book: Option<Book<Values>>,
    ready: bool,
    trade_replay_pending: bool,
    statistics_replay_pending: bool,
    heartbeat_diagnostics: HeartbeatDiagnostics,
}

/// One log sample per minute and physical attempt for routine provider heartbeats.
#[derive(Default)]
struct HeartbeatDiagnostics {
    next: Option<tokio::time::Instant>,
    suppressed: u64,
}

impl HeartbeatDiagnostics {
    fn admit(&mut self, event: &Event, now: tokio::time::Instant) -> Option<u64> {
        if !matches!(event, Event::System { code: 0, message } if message == "Heartbeat") {
            return Some(0);
        }
        if self.next.is_some_and(|next| now < next) {
            self.suppressed = self.suppressed.saturating_add(1);
            return None;
        }
        self.next = Some(now + std::time::Duration::from_secs(60));
        Some(std::mem::take(&mut self.suppressed))
    }
}
/// Read-only native query view of numeric book state. It contains no provider
/// client, task, cancellation authority or dataset child. Retirement invalidates
/// availability even when a copied view is retained by a query.
#[derive(Clone)]
pub struct Queries {
    counters: Arc<super::counters::Counters>,
    output: Arc<Mutex<BTreeMap<Lane, super::output::LaneOutput>>>,
    lane: Lane,
    capacity: usize,
    registrations: usize,
    lifecycle: watch::Receiver<Status>,
    retired: CancellationToken,
    book: Arc<Mutex<BookState>>,
    changes: watch::Receiver<u64>,
    capture_updates: Arc<CaptureUpdates>,
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
    /// Samples source totals and actual bounded output queues without provider I/O.
    pub fn counters(&self) -> Result<BTreeMap<String, i64>, String> {
        let mut values = self.counters.snapshot();
        let lifecycle = self.lifecycle.borrow().clone();
        values.insert("Provider session attempts".into(), lifecycle.generation.min(i64::MAX as u64) as i64);
        values.insert("Recovery episodes".into(), lifecycle.episode.min(i64::MAX as u64) as i64);
        values.insert("Current episode attempts".into(), lifecycle.attempts.min(i64::MAX as u64) as i64);
        values.insert("Registered instruments".into(), self.registrations as i64);
        values.insert("Instrument capacity".into(), self.capacity as i64);
        if let Some(output) = self.output.lock().map_err(|_| "Native output diagnostics interrupted")?.get(&self.lane) {
            values.extend(output.counters());
        }
        Ok(values)
    }
    /// Event notification only; retaining this receiver cannot retain a source
    /// task, socket or dataset cancellation authority.
    pub fn updates(&self) -> watch::Receiver<u64> {
        self.changes.clone()
    }

    pub(crate) fn capture_updates(&self, instruments: Vec<u32>) -> Option<Subscription> {
        self.capture_updates.subscribe(instruments)
    }

    pub(crate) async fn cancelled(&self) {
        self.retired.cancelled().await;
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
    counters: Arc<super::counters::Counters>,
    registrations: usize,
    status: watch::Receiver<Status>,
    stop: CancellationToken,
    book: Arc<Mutex<BookState>>,
    retired: CancellationToken,
    capture_updates: Arc<CaptureUpdates>,
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
    /// Stable per-generation ordering token, assigned once before mapping.
    pub capture_serial: u64,
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
    /// Journal identity; its monotonic row ID is not the ITI trade ordinal.
    pub raw: Option<super::raw_capture::Stamp>,
}
/// Source capture is synchronous and receives only frozen source-time values.
/// Queue ownership is held before the mapper owner, never the reverse.
pub(crate) type RequiredOutputCapture = Arc<dyn Fn(&[Output]) -> Result<(), String> + Send + Sync>;
type CheckpointProcessor=Arc<Mutex<Box<dyn FnMut(u64,&Event,bool)->Result<bool,String>+Send>>>;

fn retain_required_output(
    queue: &mut super::output::LaneOutput,
    mut value: Output,
    captures: &Mutex<BTreeMap<Lane, RequiredOutputCapture>>,
    capture_enabled: bool,
) -> Result<(), String> {
    let required = matches!(value.source,
        OutputSource::ReplayComplete(_) | OutputSource::StatisticsReplayComplete(_)
        | OutputSource::Observation(super::records::Observation::Statistic(_)));
    if !required { return queue.push_back(value); }
    let capture = captures.lock().map_err(|_| "Required source capture registry interrupted")?
        .get(&value.lane).cloned();
    let Some(capture) = capture else {
        if value.lane != Lane::OptionsGlbx && capture_enabled {
            return Err("Required futures source capture must be bound before provider startup".into());
        }
        return queue.push_back(value);
    };
    queue.assign_capture_serial(&mut value)?;
    let mut original = queue.ordered_prefix(usize::MAX);
    let prior = original.len();
    original.push(value.clone());
    if let Err(error) = capture(&original) {
        queue.retain_failed_capture(value);
        return Err(error);
    }
    // The current value bypasses removable admission; the prior prefix leaves
    // only after the same durable owner has captured every original frame.
    for _ in 0..prior { queue.pop_front(); }
    Ok(())
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
    raw_capture: Option<(std::path::PathBuf, time::Date)>,
    required_output_capture: Arc<Mutex<BTreeMap<Lane, RequiredOutputCapture>>>,
    capture_enabled: bool,
    checkpoint_processors: Mutex<BTreeMap<Lane,CheckpointProcessor>>,
    checkpoint_imports: Arc<Mutex<BTreeMap<Lane,rmpv::Value>>>,
    checkpoint_raw:Arc<Mutex<BTreeMap<Lane,rmpv::Value>>>,
    checkpoint_books:Arc<Mutex<BTreeMap<Lane,Arc<Mutex<BookState>>>>>,
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
    #[cfg(test)]
    pub(crate) fn seed_checkpoint_book_for_test(&self,lane:Lane,book:Book<Values>,attempt:u64)->Result<(),String>{
        let image=book.checkpoint()?;let fields=crate::checkpoint::fields(&image,7)?;
        let instruments=fields[5].as_array().ok_or("Fixture book registrations absent")?.iter().map(|value|crate::checkpoint::unsigned(&crate::checkpoint::fields(value,11)?[0])?.try_into().map_err(|_|"Fixture instrument overflow".to_owned())).collect::<Result<Vec<u32>,String>>()?;
        let registrations=instruments.len();let generation=book.generation();
        self.output.lock().map_err(|_|"Fixture output owner interrupted")?.get_mut(&lane).ok_or("Fixture output lane absent")?.configure(instruments.into_iter());
        self.active.lock().map_err(|_|"Fixture active owner interrupted")?.insert(lane,Active{counters:Arc::default(),registrations,status:watch::channel(Status{lane,phase:Phase::Running,generation:attempt,episode:0,attempts:1,error:None,root_error:None,deadline:None}).1,stop:CancellationToken::new(),book:Arc::new(Mutex::new(BookState{attempt,generation,book:Some(book),ready:true,trade_replay_pending:false,statistics_replay_pending:false,heartbeat_diagnostics:Default::default()})),retired:CancellationToken::new(),capture_updates:Arc::default()});
        self.checkpoint_processors.lock().map_err(|_|"Fixture processor owner interrupted")?.insert(lane,Arc::new(Mutex::new(Box::new(|_,event,_|if matches!(event,Event::Flush){Ok(true)}else{Err("Fixture processor only flushes already-applied explicit test records".into())}))));Ok(())
    }
    pub(crate) fn checkpoint_prepare_lane(&self,lane:Lane,mut capture:impl FnMut(&[Output])->Result<(),String>)->Result<(),String>{
        let processor=self.checkpoint_processors.lock().map_err(|_|"Checkpoint processor registry interrupted")?.get(&lane).cloned();
        let Some(processor)=processor else{if self.checkpoint_books.lock().map_err(|_|"Checkpoint original book registry interrupted")?.contains_key(&lane){return Ok(());}return Err("Original checkpoint source processor absent".into());};
        processor.lock().map_err(|_|"Checkpoint source processor interrupted")?(0,&Event::Flush,true)?;
        let mut queues=self.output.lock().map_err(|_|"Checkpoint output owner interrupted")?;let queue=queues.get_mut(&lane).ok_or("Checkpoint output lane absent")?;
        for _ in 0..257{let prefix=queue.ordered_prefix(64);if prefix.is_empty(){queue.checkpoint_empty()?;return Ok(());}capture(&prefix)?;for _ in 0..prefix.len(){queue.pop_front();}}
        Err("Checkpoint original output drain exceeds finite bound".into())
    }
    pub(crate) fn checkpoint_lane(&self,lane:Lane)->Result<rmpv::Value,String>{
        use rmpv::Value as V;
        if !self.active.lock().map_err(|_|"Checkpoint active registry interrupted")?.contains_key(&lane){if let Some(original)=self.checkpoint_imports.lock().map_err(|_|"Checkpoint original image registry interrupted")?.get(&lane){return Ok(original.clone());}}
        // All owner prepare passes ran before the managed snapshot. Refresh
        // shared raw-root prefix evidence after the final lane's flush; there
        // are no new source callbacks while the common state lease is held.
        if let Some(processor)=self.checkpoint_processors.lock().map_err(|_|"Checkpoint processor registry interrupted")?.get(&lane).cloned(){processor.lock().map_err(|_|"Checkpoint source processor interrupted")?(0,&Event::Flush,true)?;}
        let (book,available)=self.active.lock().map_err(|_|"Checkpoint active registry interrupted")?.get(&lane).map(|active|(active.book.clone(),active.status.borrow().phase==Phase::Running&&!active.stop.is_cancelled()&&!active.retired.is_cancelled())).ok_or("Original checkpoint lane absent")?;
        let state=book.lock().map_err(|_|"Checkpoint book owner interrupted")?;
        let book=state.book.as_ref().ok_or("Original checkpoint book has not been initialized")?;
        let serial=self.output.lock().map_err(|_|"Checkpoint output owner interrupted")?.get(&lane).ok_or("Original checkpoint output absent")?.checkpoint_empty()?;
        let raw=self.checkpoint_raw.lock().map_err(|_|"Raw checkpoint registry interrupted")?.get(&lane).cloned().unwrap_or(V::Nil);
        Ok(V::Array(vec![1.into(),crate::checkpoint::day(self.date),crate::checkpoint::wide(self.instance),(self.capacity as u64).into(),(lane as u8).into(),state.attempt.into(),crate::checkpoint::wide(state.generation),(state.ready&&available).into(),state.trade_replay_pending.into(),state.statistics_replay_pending.into(),book.checkpoint()?,serial.into(),self.clock.checkpoint()?,raw]))
    }
    pub(crate) fn import_checkpoint_lane(&self,lane:Lane,value:&rmpv::Value)->Result<(),String>{
        use crate::checkpoint as c;let f=c::fields(value,14)?;
        if c::unsigned(&f[0])?!=1||c::date(&f[1])?!=self.date||c::read_wide(&f[2])?!=self.instance||c::unsigned(&f[3])?!=self.capacity as u64||c::lane(&f[4])?!=lane{return Err("Native checkpoint book date/owner/configuration differs".into());}
        let book=Book::<Values>::restore_checkpoint(&f[10],CancellationToken::new())?;
        if book.generation()!=c::read_wide(&f[6])?{return Err("Native checkpoint physical book generation differs".into());}
        if !self.active.lock().map_err(|_|"Checkpoint active registry interrupted")?.is_empty(){return Err("Native checkpoint cannot overwrite an active provider".into());}
        let mut pending=self.checkpoint_imports.lock().map_err(|_|"Checkpoint staged book registry interrupted")?;
        if let Some(existing)=pending.get(&lane){if existing==value{return Ok(());}return Err("Native checkpoint staged original book differs".into());}
        self.clock.restore_checkpoint_controlled(&f[12])?;
        self.checkpoint_books.lock().map_err(|_|"Checkpoint original book registry interrupted")?.insert(lane,Arc::new(Mutex::new(BookState{attempt:c::unsigned(&f[5])?,generation:book.generation(),book:Some(book),ready:c::boolean(&f[7])?,trade_replay_pending:c::boolean(&f[8])?,statistics_replay_pending:c::boolean(&f[9])?,heartbeat_diagnostics:Default::default()})));
        pending.insert(lane,value.clone());Ok(())
    }
    /// Creates only in-memory ownership; it opens no network or worker process.
    pub fn new(
        date: time::Date,
        instance: u128,
        capacity: usize,
        clock: Arc<Clock>,
        owner: Arc<Owner>,
    ) -> Result<Self, String> {
        if capacity == 0 || capacity > crate::option_chain::MAX_OPTION_REGISTRATIONS {
            return Err(
                format!("Native contract capacity must be 1..{} including underlying dependencies", crate::option_chain::MAX_OPTION_REGISTRATIONS),
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
            raw_capture: None,
            required_output_capture: Arc::new(Mutex::new(BTreeMap::new())),
            capture_enabled: crate::messaging::source_outbox::enabled(),
            checkpoint_processors:Mutex::new(BTreeMap::new()),
            checkpoint_imports:Arc::new(Mutex::new(BTreeMap::new())),
            checkpoint_raw:Arc::new(Mutex::new(BTreeMap::new())),
            checkpoint_books:Arc::new(Mutex::new(BTreeMap::new())),
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
    pub fn with_raw_capture(mut self, definition: time::Date) -> Self {
        self.raw_capture = std::env::var_os("IFM_RAW_FUTURES_STORAGE_ROOT")
            .filter(|root| !root.is_empty()).map(|root| (root.into(), definition));
        self
    }
    /// Cached original-storage root; no environment read occurs during capture.
    pub(crate) fn raw_capture_root(&self) -> Option<&std::path::Path> {
        self.raw_capture.as_ref().map(|(root, _)| root.as_path())
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
        let _checkpoint=crate::checkpoint::transaction()?;
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
        let _checkpoint=crate::checkpoint::transaction()?;
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
    /// Durable mode maps and captures a finite original prefix while queue
    /// ownership is held. A later required producer cannot overtake a popped
    /// trade while racing for the shared mapper lock.
    pub(crate) fn capture_output_prefix<T>(
        &self, lane: Lane, maximum: usize,
        capture: impl FnOnce(&[Output]) -> Result<T, String>,
    ) -> Result<Option<T>, String> {
        let _checkpoint=crate::checkpoint::transaction()?;
        if !(1..=64).contains(&maximum) { return Err("Native capture prefix exceeds bound".into()); }
        let mut queues = self.output.lock().map_err(|_| "Native output writer interrupted")?;
        let queue = queues.get_mut(&lane).ok_or("Native output lane absent")?;
        let prefix = queue.ordered_prefix(maximum);
        if prefix.is_empty() { return Ok(None); }
        let accepted = match capture(&prefix) {
            Ok(value) => value,
            Err(error) => { queue.mark_capture_failed(); return Err(error); }
        };
        for _ in 0..prefix.len() { queue.pop_front(); }
        Ok(Some(accepted))
    }
    pub(crate) fn bind_required_output_capture(&self, lane: Lane, capture: RequiredOutputCapture) -> Result<(), String> {
        if !self.active.lock().map_err(|_| "Native lane registry interrupted")?.is_empty() {
            return Err("Required source capture must bind before provider startup".into());
        }
        let mut captures = self.required_output_capture.lock().map_err(|_| "Required source capture registry interrupted")?;
        if captures.contains_key(&lane) { return Err("Required source capture already bound".into()); }
        captures.insert(lane, capture);
        Ok(())
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
        request.validate(self.capacity)?;
        if self.output.lock().map_err(|_| "Native output writer interrupted")?
            .get(&request.plan.lane).is_some_and(|queue| queue.capture_failed()) {
            return Err("Pending failed required source capture must drain before provider restart".into());
        }
        if self.capture_enabled && request.plan.lane != Lane::OptionsGlbx {
            if self.raw_capture.is_none() {
                return Err("Durable futures source capture requires the original raw trade/quote journal before provider startup".into());
            }
            if !self.required_output_capture.lock().map_err(|_| "Required source capture registry interrupted")?.contains_key(&request.plan.lane) {
                return Err("Durable futures source capture requires its frozen output mapper before provider startup".into());
            }
        }
        let Start {
            plan,
            registrations,
            key,
            gateway,
            evidence,
        } = request;
        let deadline = deadline.earliest(Deadline::recovery());
        let lane = plan.lane;
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
        let capture_updates = Arc::new(CaptureUpdates::default());
        let capture_signal = capture_updates.clone();
        let book = Arc::new(Mutex::new(BookState {
            attempt: 0,
            generation: 0,
            book: None,
            ready: false,
            trade_replay_pending: false,
            statistics_replay_pending: false,
            heartbeat_diagnostics: HeartbeatDiagnostics::default(),
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
        // Immutable generation directory keeps publisher qualification off the linear hot path.
        let registration_indices: BTreeMap<_, _> = registrations
            .iter()
            .enumerate()
            .map(|(index, registration)| (registration.instrument, index))
            .collect();
        let retirement = BookRetirement(retired.clone(), pricing.clone());
        let registration_count = registrations.len();
        let original_raw=self.checkpoint_imports.lock().map_err(|_|"Checkpoint staged book registry interrupted")?.get(&lane).map(|value|crate::checkpoint::fields(value,14).map(|fields|fields[13].clone())).transpose()?;
        let mut raw_capture = match &self.raw_capture {
            Some((root, definition)) if lane != Lane::OptionsGlbx => Some(match original_raw.as_ref().filter(|value|!value.is_nil()){
                Some(value)=>{crate::checkpoint::validate_raw_restore_path(root,value)?;super::raw_capture::RawCapture::restore_checkpoint(root,value,lane,self.date,*definition,&registrations)?},
                None=>super::raw_capture::RawCapture::open(root, lane, self.date, *definition, &registrations)?,
            }),
            _ => None,
        };
        let counters = Arc::new(super::counters::Counters::default());
        let processing_counters = counters.clone();
        let required_output_capture = self.required_output_capture.clone();
        let capture_enabled = self.capture_enabled;
        let checkpoint_imports=self.checkpoint_imports.clone();
        let checkpoint_books=self.checkpoint_books.clone();
        let process_one = move |attempt, event: &Event, raw: Option<super::raw_capture::Stamp>| {
            processing_counters.measure(event, || {
            let _retirement = &retirement;
            let mut state = state
                .lock()
                .map_err(|_| "Native book writer interrupted".to_owned())?;
            if state.attempt != attempt {
                let restored=checkpoint_imports.lock().map_err(|_|"Checkpoint staged book registry interrupted")?.remove(&lane);
                if let Some(restored)=restored{
                    use crate::checkpoint as c;let f=c::fields(&restored,14)?;
                    let mut book=Book::<Values>::restore_checkpoint(&f[10],processor_retired.clone())?;
                    if lane==Lane::OptionsGlbx{book.rebind_checkpoint_statistics(option_statistics.clone())?;}
                    state.book=Some(book);state.attempt=attempt;state.generation=c::read_wide(&f[6])?;state.ready=c::boolean(&f[7])?;state.trade_replay_pending=c::boolean(&f[8])?;state.statistics_replay_pending=c::boolean(&f[9])?;
                    output.lock().map_err(|_|"Checkpoint output writer interrupted")?.get_mut(&lane).ok_or("Checkpoint output lane absent")?.restore_empty_checkpoint(c::unsigned(&f[11])?)?;
                    if let Some(original)=checkpoint_books.lock().map_err(|_|"Checkpoint original book registry interrupted")?.remove(&lane){if let Some(book)=original.lock().map_err(|_|"Checkpoint original book interrupted")?.book.as_mut(){book.retire().map_err(|error|format!("Checkpoint prior reader retirement: {error:?}"))?;}}
                }else{
                if output.lock().map_err(|_| "Native output writer interrupted")?
                    .get(&lane).is_some_and(|queue| queue.capture_failed()) {
                    return Err("Pending required capture prevents physical generation replacement".into());
                }
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
                state.heartbeat_diagnostics = HeartbeatDiagnostics::default();
                capture_signal.notify(None);
                }
            }
            let generation = state.generation;
            if matches!(event, Event::System { .. } | Event::Error { .. }) {
                if let Some(suppressed) = state
                    .heartbeat_diagnostics
                    .admit(event, tokio::time::Instant::now())
                {
                    let mut diagnostic = event.clone();
                    if let Event::System { message, .. } | Event::Error { message, .. } =
                        &mut diagnostic
                    {
                        if let Some(scope) = &diagnostic_scope {
                            *message =
                                format!("[scope={scope}; generation={generation:032x}] {message}");
                        }
                        if suppressed > 0 {
                            message.push_str(&format!(" [routine heartbeats suppressed: {suppressed}]"));
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
                }
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
                    capture_signal.notify(Some(*instrument));
                }
                let mut queues = output
                    .lock()
                    .map_err(|_| "Native output writer interrupted")?;
                let queue = queues.get_mut(&lane).ok_or("Native output lane absent")?;
                if trade_instruments.len() > queue.available()
                    && !required_output_capture.lock().map_err(|_| "Required source capture registry interrupted")?.contains_key(&lane) {
                    return Err("Authoritative application output capacity exhausted".into());
                }
                // Paired analytics must be able to prove both contracts came
                // from this same replay boundary. Per-instrument epochs made
                // every VX front/back pair permanently incompatible.
                let epoch = rand::random::<u128>().max(1);
                for instrument in &trade_instruments {
                    book.complete_replay(generation, *instrument, epoch)
                        .map_err(|e| format!("{e:?}"))?;
                    let state = book.publication_state(*instrument, super::book::Update::Unchanged);
                    retain_required_output(queue, Output {
                        capture_serial: 0, observed: clock.utc_ticks().map_err(|e| format!("{e:?}"))?,
                        lane,
                        generation,
                        source: OutputSource::ReplayComplete(*instrument),
                        state,
                        trade: None, raw: None,
                    }, &required_output_capture, capture_enabled)?;
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
                        retain_required_output(queue, Output {
                            capture_serial: 0, observed: clock.utc_ticks().map_err(|e| format!("{e:?}"))?,
                            lane,
                            generation,
                            source: OutputSource::StatisticsReplayComplete(*instrument),
                            state: super::book::PublicationState {
                                session: Some(statistics),
                                ..Default::default()
                            },
                            trade: None, raw: None,
                        }, &required_output_capture, capture_enabled)?;
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
                let registration = registration_indices
                    .get(&header.instrument)
                    .map(|index| &registrations[*index]);
                if let Some(registration) = registration
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
                let instrument = header.instrument;
                // Options capture also reads the logical statistics book.
                // Replay executions and provider statistics can change it
                // independently of the raw quote and trade ordering result.
                let capture_changed = update == super::book::Update::Quote
                    || lane == Lane::OptionsGlbx
                        && matches!(
                            record,
                            super::records::Observation::Trade(_)
                                | super::records::Observation::Statistic(_)
                        )
                        && registration.is_some_and(|row| row.asset == Asset::Option);
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
                    retain_required_output(queue, Output {
                        capture_serial: 0, observed: local,
                        lane,
                        generation,
                        source: OutputSource::Observation(*record),
                        state,
                        trade, raw,
                    }, &required_output_capture, capture_enabled)?;
                }
                if was_ready {
                    // Numeric state and the bounded output are committed. Wake consumers only after releasing the book.
                    drop(state);
                    if capture_changed {
                        capture_signal.notify(Some(instrument));
                    }
                    updates.send_modify(|version| *version = version.saturating_add(1));
                    notifier
                        .signal(1 << (lane as u32))
                        .map_err(|_| "Native delivery signal interrupted")?;
                    return Ok(true);
                }
                updates.send_modify(|version| *version = version.saturating_add(1));
                notifier
                    .signal(1 << (lane as u32))
                    .map_err(|_| "Native delivery signal interrupted")?;
            }
            if matches!(
                evidence,
                Evidence::ControlOnly | Evidence::ControlOnlyWithOptionalQuoteReplay
            ) {
                // The physical book/identity exists after a valid provider
                // record. Individual trade/quote fields remain None until real
                // observations arrive; this never manufactures market data.
                state.ready = true;
                if !was_ready {
                    capture_signal.notify(None);
                }
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
            capture_signal.notify(None);
            Ok(true)
            })
        };
        let capture_clock = self.clock.clone();
        let checkpoint_raw=self.checkpoint_raw.clone();let checkpoint_enabled=crate::checkpoint::enabled();
        let mut pending = Vec::<(u64, Event, Option<super::raw_capture::Stamp>)>::with_capacity(64);
        let mut last_ready = false;
        let processor = move |attempt: u64, event: &Event,checkpoint_capture:bool| -> Result<bool, String> {
            let buffered = match event {
                Event::Market(super::records::Observation::Trade(trade)) => trade.header.flags & 2 == 0,
                Event::Market(super::records::Observation::Quote(quote)) => quote.header.flags & 2 == 0,
                _ => false,
            };
            if let Some(capture) = &mut raw_capture {
                if let Event::Market(record) = event && buffered {
                    let observed = capture_clock.utc_ticks().map_err(|e| format!("{e:?}"))?;
                    let stamp = capture.append(u128::from(attempt), observed, *record)?;
                    pending.push((attempt, event.clone(), stamp));
                    if pending.len() < 64 { return Ok(last_ready); }
                }
                if !pending.is_empty() {
                    capture.flush()?;
                    for (accepted_attempt, accepted, stamp) in pending.drain(..) {
                        last_ready = process_one(accepted_attempt, &accepted, stamp)?;
                    }
                }
                if buffered || matches!(event, Event::Flush) {
                    if checkpoint_enabled&&checkpoint_capture{capture.flush()?;checkpoint_raw.lock().map_err(|_|"Raw checkpoint registry interrupted")?.insert(lane,capture.checkpoint()?);}
                    return Ok(last_ready);
                }
            } else if matches!(event, Event::Flush) { return Ok(last_ready); }
            last_ready = process_one(attempt, event, None)?;
            Ok(last_ready)
        };
        let mut local_processor=Some(processor);
        let checkpoint_processor=if crate::checkpoint::enabled(){
            let shared:CheckpointProcessor=Arc::new(Mutex::new(Box::new(local_processor.take().unwrap())));
            self.checkpoint_processors.lock().map_err(|_|"Checkpoint processor registry interrupted")?.insert(lane,shared.clone());Some(shared)
        }else{None};
        let processor=move|attempt,event:&Event|{
            if let Some(processor)=&checkpoint_processor{let _checkpoint=crate::checkpoint::transaction()?;processor.lock().map_err(|_|"Checkpoint source processor interrupted")?(attempt,event,false)}
            else{local_processor.as_mut().unwrap()(attempt,event,false)}
        };
        let mut status = if let Some(parent) = parent {
            lanes.start_nested_with_quote_replay(
                plan,
                key,
                gateway,
                processor,
                deadline,
                parent,
                evidence == Evidence::ControlOnlyWithOptionalQuoteReplay,
            )
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
                    counters,
                    registrations: registration_count,
                    status: status.clone(),
                    stop: stop.clone(),
                    book,
                    retired,
                    capture_updates,
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
        drop(signals);
        // Imported read-only owners have no provider stop token. Retire their
        // actual shared book so retained query handles cannot outlive the owner.
        if crate::checkpoint::replay_import_only() {
            if let Some(original) = self.checkpoint_books.lock().map_err(|_| "Checkpoint original book registry interrupted")?.remove(&lane) {
                let mut state = original.lock().map_err(|_| "Checkpoint original book interrupted")?;
                state.ready = false;
                if let Some(book) = state.book.as_mut() {
                    book.retire().map_err(|error| format!("Checkpoint reader retirement: {error:?}"))?;
                }
            }
            self.checkpoint_imports.lock().map_err(|_| "Checkpoint import registry interrupted")?.remove(&lane);
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
            drop(active);
            let original=self.checkpoint_books.lock().map_err(|_|"Checkpoint original book registry interrupted")?.get(&lane).cloned();
            return match original{Some(original)=>{let state=original.lock().map_err(|_|"Checkpoint original book interrupted")?;if !state.ready{return Ok(None);}state.book.as_ref().map_or(Ok(None),|book|book.reader(id).map_err(|error|format!("Checkpoint original reader: {error:?}")))},None=>Ok(None)};
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
        if !active.contains_key(&lane){
            drop(active);let book=self.checkpoint_books.lock().map_err(|_|"Checkpoint original book registry interrupted")?.get(&lane).cloned();
            return book.map(|book|{
                let registrations=book.lock().map_err(|_|"Checkpoint original book interrupted")?.book.as_ref().ok_or("Checkpoint original book absent")?.checkpoint()?.as_array().and_then(|fields|fields[5].as_array()).map(Vec::len).ok_or("Checkpoint original registrations absent")?;
                Ok(Queries{counters:Arc::default(),output:self.output.clone(),lane,capacity:self.capacity,registrations,lifecycle:watch::channel(Status{lane,phase:Phase::Stopped,generation:0,episode:0,attempts:0,error:Some("Original checkpoint read-only state; no provider is running".into()),root_error:None,deadline:None}).1,book,retired:CancellationToken::new(),changes:self.updates.subscribe(),capture_updates:Arc::default()})
            }).transpose();
        }
        Ok(active.get(&lane).map(|active| Queries {
            counters: active.counters.clone(),
            output: self.output.clone(),
            lane,
            capacity: self.capacity,
            registrations: active.registrations,
            lifecycle: active.status.clone(),
            book: active.book.clone(),
            retired: active.retired.clone(),
            changes: self.updates.subscribe(),
            capture_updates: active.capture_updates.clone(),
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
            drop(active);let original=self.checkpoint_books.lock().map_err(|_|"Checkpoint original book registry interrupted")?.get(&lane).cloned();
            return match original{Some(original)=>{let state=original.lock().map_err(|_|"Checkpoint original book interrupted")?;Ok(if state.ready{state.book.as_ref().and_then(read)}else{None})},None=>Ok(None)};
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
    #[test]
    #[ignore = "Explicit paired diagnostic admission benchmark"]
    fn heartbeat_diagnostic_before_after_timings() {
        use super::{Diagnostic, Event, HeartbeatDiagnostics, Lane};
        use std::{hint::black_box, time::{Duration, Instant}};

        let heartbeat = Event::System { code: 0, message: "Heartbeat".into() };
        let error = Event::Error { code: 199, last: true, message: "Provider failure".into() };
        let now = tokio::time::Instant::now();
        for pair in 1..=5 {
            for sampled in if pair % 2 == 1 { [false, true] } else { [true, false] } {
                let started = Instant::now();
                let mut sent = 0;
                let mut errors = 0;
                let mut bytes = 0;
                for _ in 0..8 {
                    let mut sampler = HeartbeatDiagnostics::default();
                    for second in 0..600 {
                        for event in [&heartbeat, &error] {
                            if event == &error && second % 20 != 0 { continue; }
                            let sample = if sampled {
                                sampler.admit(event, now + Duration::from_secs(second))
                            } else {
                                Some(0)
                            };
                            if let Some(count) = sample {
                                let mut event = event.clone();
                                if count > 0 && let Event::System { message, .. } = &mut event {
                                    message.push_str(&format!(" [routine heartbeats suppressed: {count}]"));
                                }
                                errors += usize::from(matches!(event, Event::Error { .. }));
                                let diagnostic = Diagnostic {
                                    worker: [1; 16], lane: Lane::OptionsGlbx, attempt: 1,
                                    observed: second as i64 * 10_000_000, replaced: 0, event,
                                };
                                bytes += black_box(crate::messaging::diagnostic::encode(&diagnostic, [1; 16]).unwrap()).len();
                                sent += 1;
                            }
                        }
                    }
                }
                assert_eq!(errors, 240);
                println!("HEARTBEAT_DIAGNOSTICS pair={pair} sampled={sampled} frames={sent} bytes={bytes} elapsed_ns={}", started.elapsed().as_nanos());
            }
        }
    }

    #[test]
    fn diagnostic_sampling_only_suppresses_routine_heartbeats() {
        use super::{Event, HeartbeatDiagnostics};
        use std::time::Duration;
        use tokio::time::Instant;

        let heartbeat = Event::System { code: 0, message: "Heartbeat".into() };
        let transition = Event::System { code: 3, message: "Replay completed".into() };
        let unknown = Event::System { code: 255, message: "Heartbeat".into() };
        let error = Event::Error { code: 0, last: true, message: "Heartbeat".into() };
        let mut diagnostics = HeartbeatDiagnostics::default();
        let now = Instant::now();
        assert_eq!(diagnostics.admit(&heartbeat, now), Some(0));
        assert_eq!(diagnostics.admit(&heartbeat, now), None);
        assert_eq!(diagnostics.admit(&heartbeat, now + Duration::from_secs(59)), None);
        assert_eq!(diagnostics.admit(&transition, now), Some(0));
        assert_eq!(diagnostics.admit(&unknown, now), Some(0));
        assert_eq!(diagnostics.admit(&error, now), Some(0));
        assert_eq!(diagnostics.admit(&heartbeat, now + Duration::from_secs(60)), Some(2));
        assert_eq!(HeartbeatDiagnostics::default().admit(&heartbeat, now), Some(0));
    }
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


#[cfg(test)]
mod required_capture_tests {
    use super::*;
    use crate::aggregation::session::{Snapshot, VolumeQuality};
    use crate::dataset::{book::{PublicationState, Update}, records::{Header, Observation, Statistic}};
    use std::io::Write;

    fn date() -> time::Date { time::Date::from_calendar_date(2026, time::Month::November, 23).unwrap() }
    fn official(sequence: u32) -> Output {
        let snapshot = Snapshot { value_date: date(), open: 6_000_000_000_000, high: 6_025_000_000_000,
            low: 5_975_000_000_000, sequence, event_ns: 1_795_467_599_000_000_000 + i64::from(sequence),
            volume: 1_234_567, quality: VolumeQuality::OfficialFinal };
        Output { capture_serial: 0, observed: 17_954_675_990_000_000, lane: Lane::FuturesGlbx, generation: 55,
            source: OutputSource::Observation(Observation::Statistic(Statistic {
                header: Header { instrument: 42, publisher: 1, flags: 0, event_ns: snapshot.event_ns,
                    received_ns: snapshot.event_ns + 100, sequence, schema: 9 },
                price: 0, quantity: snapshot.volume, reference_ns: snapshot.event_ns, kind: 6, channel: 2, action: 1, flags: 0,
            })), state: PublicationState { update: Some(Update::Statistics(snapshot)), session: Some(snapshot), ..Default::default() },
            trade: None, raw: None }
    }
    #[test]
    fn official_output_captures_the_exact_prior_prefix_before_capacity_or_generation_discard() {
        let mut queue = super::super::output::LaneOutput::new();
        for sequence in 1..=8192 { queue.push_back(official(sequence)).unwrap(); }
        assert_eq!(queue.available(), 0);
        let accepted = Arc::new(Mutex::new(Vec::new()));
        let observed = accepted.clone();
        let mut captures = BTreeMap::<Lane, RequiredOutputCapture>::new();
        captures.insert(Lane::FuturesGlbx, Arc::new(move |prefix: &[Output]| {
            *observed.lock().unwrap() = prefix.to_vec(); Ok(())
        }));
        retain_required_output(&mut queue, official(8193), &Mutex::new(captures), true).unwrap();
        let accepted = accepted.lock().unwrap();
        assert_eq!(accepted.len(), 8193);
        for (index, original) in accepted.iter().enumerate() {
            let OutputSource::Observation(Observation::Statistic(statistic)) = original.source else { panic!("wrong source"); };
            assert_eq!(statistic.header.sequence, index as u32 + 1);
            assert_eq!(original.observed, 17_954_675_990_000_000);
            assert_eq!(original.generation, 55);
            assert_eq!(original.state.session.unwrap().volume, 1_234_567);
        }
        assert!(queue.pop_front().is_none());
        queue.clear(); // A replacement cannot discard what the durable owner already accepted.
        assert_eq!(accepted.last().unwrap().state.session.unwrap().quality, VolumeQuality::OfficialFinal);
    }
    #[test]
    fn capture_refusal_never_removes_the_prior_prefix_or_reports_success() {
        let mut queue = super::super::output::LaneOutput::new(); queue.push_back(official(1)).unwrap();
        let mut captures = BTreeMap::<Lane, RequiredOutputCapture>::new();
        captures.insert(Lane::FuturesGlbx, Arc::new(|_: &[Output]| Err("Injected required capture refusal".into())));
        assert!(retain_required_output(&mut queue, official(2), &Mutex::new(captures), true).is_err());
        let retained = queue.pop_front().unwrap();
        let OutputSource::Observation(Observation::Statistic(statistic)) = retained.source else { panic!("wrong source"); };
        assert_eq!(statistic.header.sequence, 1);
        queue.clear(); // Failed capture ownership survives generation cleanup.
        let second = queue.pop_front().unwrap();
        assert_eq!(second.capture_serial, 2);
        let OutputSource::Observation(Observation::Statistic(statistic)) = second.source else { panic!("wrong source"); };
        assert_eq!(statistic.header.sequence, 2); assert!(!queue.capture_failed());
        assert!(queue.pop_front().is_none());
        assert!(retain_required_output(&mut queue, official(3), &Mutex::new(BTreeMap::new()), true).is_err());
    }
    #[test]
    fn failed_capture_preserves_stable_ordering_tokens_until_confirmed_drain() {
        let mut queue = super::super::output::LaneOutput::new();
        queue.push_back(official(1)).unwrap();
        let first = Arc::new(Mutex::new(Vec::<Output>::new())); let seen = first.clone();
        let mut captures = BTreeMap::<Lane, RequiredOutputCapture>::new();
        captures.insert(Lane::FuturesGlbx, Arc::new(move |prefix: &[Output]| {
            *seen.lock().unwrap() = prefix.to_vec(); Err("Injected unknown capture outcome".into())
        }));
        assert!(retain_required_output(&mut queue, official(2), &Mutex::new(captures), true).is_err());
        queue.clear(); queue.configure([99].into_iter());
        assert!(queue.capture_failed());
        let retry = queue.ordered_prefix(64); let original = first.lock().unwrap();
        assert_eq!(retry.len(), 2);
        for (before, after) in original.iter().zip(&retry) {
            assert_eq!((before.generation, before.capture_serial, before.observed),
                (after.generation, after.capture_serial, after.observed));
            assert_eq!(before.state.session, after.state.session);
        }
        assert_ne!(retry[0].capture_serial, retry[1].capture_serial);
        for _ in retry { queue.pop_front(); }
        assert!(!queue.capture_failed());
    }
    #[test]
    fn frozen_official_wire_survives_lost_capture_reply_and_replacement_without_restamping() {
        use crate::messaging::{codec, futures::Futures, wire::Frame};
        let root = std::env::temp_dir().join(format!("ifm-required-official-{:032x}", rand::random::<u128>()));
        std::fs::create_dir_all(&root).unwrap(); let path = root.join("captured.bin");
        let registration = Registration { instrument: 42, publisher: 1, contract: "ES20261218".into(), source: "ESZ6".into(),
            asset: Asset::Futures, tick: Some(250_000_000), vwap: false, required: true };
        let encoder = Arc::new(Mutex::new(Futures::new(Lane::FuturesGlbx, date(), date(), vec![registration], 64).unwrap()));
        let captured = Arc::new(Mutex::new(Vec::<u8>::new())); let accepted = captured.clone(); let original_path = path.clone();
        let mut captures = BTreeMap::<Lane, RequiredOutputCapture>::new();
        captures.insert(Lane::FuturesGlbx, Arc::new(move |prefix: &[Output]| {
            let mut encoder = encoder.lock().map_err(|_| "mapper poisoned")?;
            let mut records = Vec::new();
            for source in prefix {
                for event in encoder.process(source).map_err(String::from)? {
                    let frame = Frame { lane: source.lane, value_date: date().to_julian_day()-1_721_426, worker: [1;16], generation: [2;16],
                        manifest_revision: 7, sequence: records.len() as i64 + 1, kind: event.kind,
                        payload: codec::encode(&event.value)?, origin_ticks: Some(source.observed) };
                    records.push(rmpv::Value::Array(vec![rmpv::Value::Binary(frame.encode().map_err(String::from)?),
                        frame.origin_json().map_err(String::from)?.unwrap().into()]));
                }
            }
            let bytes = codec::encode(&rmpv::Value::Array(records))?;
            let mut file = std::fs::File::create(&original_path).map_err(|e| e.to_string())?;
            file.write_all(&bytes).and_then(|_| file.sync_all()).map_err(|e| e.to_string())?;
            *accepted.lock().unwrap() = bytes;
            Err("Capture committed; original caller died before success".into())
        }));
        let mut queue = super::super::output::LaneOutput::new();
        assert!(retain_required_output(&mut queue, official(77), &Mutex::new(captures), true).is_err());
        queue.clear(); drop(queue);
        let saved = std::fs::read(&path).unwrap(); assert_eq!(saved, *captured.lock().unwrap());
        let rows = rmpv::decode::read_value(&mut std::io::Cursor::new(saved)).unwrap();
        let row = &rows.as_array().unwrap()[0].as_array().unwrap();
        let framed = rmpv::decode::read_value(&mut std::io::Cursor::new(row[0].as_slice().unwrap())).unwrap();
        let compressed = framed.as_array().unwrap();
        let rmpv::Value::Ext(98, length) = &compressed[0] else { panic!("Expected original LZ4 framing"); };
        let length = rmpv::decode::read_value(&mut std::io::Cursor::new(length)).unwrap().as_i64().unwrap() as usize;
        let content = lz4_flex::block::decompress(compressed[1].as_slice().unwrap(), length).unwrap();
        let wire = rmpv::decode::read_value(&mut std::io::Cursor::new(content)).unwrap();
        let fields = wire.as_array().unwrap(); assert_eq!(fields[6].as_u64(), Some(4));
        let payload = rmpv::decode::read_value(&mut std::io::Cursor::new(fields[7].as_slice().unwrap())).unwrap();
        let statistics = payload.as_array().unwrap();
        assert!(format!("{:?}", statistics[2]).contains("ES20261218"));
        assert!(row[1].as_str().unwrap().contains("2026-11-23"));
        assert_eq!(statistics.last().unwrap().as_array().unwrap()[7].as_i64(), Some(1_234_567));
        std::fs::remove_dir_all(root).unwrap();
    }
}
