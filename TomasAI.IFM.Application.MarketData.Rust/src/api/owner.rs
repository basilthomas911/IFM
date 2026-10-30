//! One native API owner, with independent GLBX futures, XCBF futures and GLBX
//! options supervisors. No process worker or managed orchestration participates.
#[path = "owner/capture_request.rs"]
mod capture_request;
pub use capture_request::Command as CaptureCommand;
#[path = "owner/capture_wait.rs"]
mod capture_wait;
#[path = "owner/composition.rs"]
mod composition_query;
#[path = "owner/intent.rs"]
mod intent_memo;
#[path = "owner/observation.rs"]
mod observation;
#[path = "owner/prices.rs"]
mod price_query;
#[path = "owner/recovery.rs"]
mod recovery_command;
pub use recovery_command::Request as RecoveryRequest;
#[path = "owner/browser.rs"]
mod browser_query;
#[path = "owner/business.rs"]
pub(crate) mod business;
#[path = "owner/committed.rs"]
mod committed;
#[path = "owner/routes.rs"]
mod routes;

use crate::{
    dataset::{
        futures_owner::{FuturesFactory, Intent as FuturesIntent},
        option_claims::Coordinator,
        options_owner::{Intent as OptionsIntent, OptionsFactory, View},
        supervisor::{Accepted, Supervisor},
        transport::Lane,
    },
    messaging::current::Current,
    runtime::{
        Deadline,
        clock::Clock,
        owner::Owner,
        recovery::{Phase, Proof, Recovery, Requirements, Terminal},
    },
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
struct Futures {
    factory: Arc<FuturesFactory>,
    control: Arc<Supervisor<FuturesIntent>>,
}
impl Drop for Futures {
    fn drop(&mut self) {
        self.control.cancel();
    }
}
struct Options {
    factory: Arc<OptionsFactory>,
    control: Arc<Supervisor<OptionsIntent>>,
}
impl Drop for Options {
    fn drop(&mut self) {
        self.control.cancel();
    }
}
/// Runtime-owned backend. Copied query views never contain this owner or its
/// supervisors; holding a query result cannot delay dataset reconstruction.
pub struct MarketData {
    session_date: Option<time::Date>,
    futures: BTreeMap<Lane, Futures>,
    options: Options,
    claims: Mutex<Option<Arc<Coordinator>>>,
    root: Arc<Owner>,
    clock: Arc<Clock>,
    notifications: Mutex<Option<crate::messaging::recovery_delivery::Delivery>>,
    audit: Mutex<Option<crate::audit::outbox::Sink>>,
    closing: std::sync::atomic::AtomicBool,
    host_identity: [u8; 16],
    status_sequence: Mutex<(Vec<u8>, i64)>,
    recovery_commands: Mutex<BTreeMap<Lane, [u8; 16]>>,
    futures_commands: Mutex<BTreeMap<Lane, [u8; 32]>>,
    raw_catalog_commands: Mutex<intent_memo::Catalog>,
    routes: Mutex<routes::Routes>,
    empty_discovery: Mutex<BTreeMap<u128, tokio::time::Instant>>,
}
impl MarketData {
    /// Authoritative API instant for copied command processing, never OS local time.
    ///
    /// # Errors
    /// An invalid or interrupted native/API clock is explicit.
    pub fn clock_ticks(&self) -> Result<i64, String> {
        self.clock
            .utc_ticks()
            .map_err(|_| "Native API clock unavailable".into())
    }
    /// Starts idle native control and native business-lifetime authority. This
    /// performs no provider or broker I/O; accepted commands start the resources.
    ///
    /// # Errors
    /// Invalid reviewed catalog or exhausted/stopped native root admission.
    pub fn new(
        root: Arc<Owner>,
        clock: Arc<Clock>,
        catalog: Option<Arc<OptionsIntent>>,
    ) -> Result<Self, String> {
        let mut futures = BTreeMap::new();
        for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
            let factory = Arc::new(FuturesFactory::new(clock.clone()));
            let control = Arc::new(
                Supervisor::start(
                    root.clone(),
                    factory.clone(),
                    None,
                    Requirements::new(&[]),
                    Deadline::recovery(),
                )
                .map_err(|e| format!("Native futures control admission: {e:?}"))?,
            );
            futures.insert(lane, Futures { factory, control });
        }
        let factory = Arc::new(OptionsFactory::new(clock.clone()));
        let control = Arc::new(
            Supervisor::start(
                root.clone(),
                factory.clone(),
                None,
                Requirements::new(&[]),
                Deadline::recovery(),
            )
            .map_err(|e| format!("Native options control admission: {e:?}"))?,
        );
        let options = Options { factory, control };
        let owner = Self {
            session_date: None,
            futures,
            options,
            claims: Mutex::new(None),
            root,
            clock,
            notifications: Mutex::new(None),
            audit: Mutex::new(None),
            closing: std::sync::atomic::AtomicBool::new(false),
            host_identity: rand::random::<u128>().max(1).to_le_bytes(),
            status_sequence: Mutex::new((vec![], 0)),
            recovery_commands: Mutex::new(BTreeMap::new()),
            futures_commands: Mutex::new(BTreeMap::new()),
            raw_catalog_commands: Mutex::default(),
            routes: Mutex::default(),
            empty_discovery: Mutex::default(),
        };
        if let Some(catalog) = catalog {
            owner.configure_options_catalog(catalog)?;
        }
        Ok(owner)
    }
    /// Creates a fresh opening epoch pinned exclusively to the API-supplied date.
    /// The caller must join the previous owner before admitting this replacement.
    ///
    /// # Errors
    /// Invalid date or stopped/exhausted root admission prevents replacement.
    pub fn new_session(root: Arc<Owner>, clock: Arc<Clock>, date: time::Date) -> Result<Self, String> {
        if date.to_julian_day() == 1721426 {
            return Err("Native market data requires an explicit API ValueDate".into());
        }
        let mut owner = Self::new(root, clock, None)?;
        owner.session_date = Some(date);
        Ok(owner)
    }
    fn notification_date(
        &self,
        lane: Lane,
        date: time::Date,
        broker: crate::messaging::publisher::BrokerSettings,
        scope: &str,
    ) -> Result<(), String> {
        let mut notifications = self
            .notifications
            .lock()
            .map_err(|_| "Native notification directory interrupted")?;
        if self.closing.load(std::sync::atomic::Ordering::Acquire) {
            return Err("Native owner is stopping".into());
        }
        if notifications.is_none() {
            *notifications = Some(crate::messaging::recovery_delivery::Delivery::start(
                &self.root,
                self.clock.clone(),
                [
                    self.health(Lane::FuturesGlbx),
                    self.health(Lane::FuturesXcbf),
                    self.health(Lane::OptionsGlbx),
                ],
                broker.clone(),
                scope.into(),
            )?);
        }
        // Audit owns copied facts only, outside all dataset resource groups.
        // An unavailable disk/path reports audit health but must not reject live intent.
        if let Ok(mut audit) = self.audit.lock() {
            if audit.is_none() {
                let started = crate::audit::outbox::location(scope).and_then(|directory| {
                    crate::audit::outbox::start(
                        notifications
                            .as_ref()
                            .expect("notification owner")
                            .audit_parent(),
                        broker,
                        scope.into(),
                        self.clock.clone(),
                        directory,
                    )
                });
                match started {
                    Ok(sink) => *audit = Some(sink),
                    Err(error) => eprintln!("RecoveryAuditUnavailable: {error}"),
                }
            }
            if let Some(sink) = audit.as_ref() {
                self.health(lane)
                    .audit_context(crate::audit::journal::Context {
                        runtime: self.host_identity,
                        lane,
                        date,
                        clock: self.clock.clone(),
                        sink: sink.clone(),
                        workloads: vec![],
                    });
            }
        }
        notifications
            .as_ref()
            .expect("initialized native notification owner")
            .date(lane, date, scope)
    }
    /// Accepts exact futures desired intent. Options use native business claims,
    /// not a managed lease/renewal or full-dataset orchestration helper.
    ///
    /// # Errors
    /// Wrong dataset, invalid/stopped control or exhausted revisions.
    pub fn start_futures(&self, intent: Arc<FuturesIntent>) -> Result<Accepted, String> {
        self.futures_commands
            .lock()
            .map_err(|_| "Native futures command memo interrupted")?
            .remove(&intent.source.plan.lane);
        self.apply_futures(intent)
    }
    fn apply_futures(&self, intent: Arc<FuturesIntent>) -> Result<Accepted, String> {
        self.validate_api_date(intent.date)?;
        let lane = intent.source.plan.lane;
        let futures = self
            .futures
            .get(&lane)
            .ok_or("Options cannot be started as a futures dataset")?;
        self.notification_date(lane, intent.date, intent.broker.clone(), &intent.scope)?;
        futures
            .control
            .apply(
                Some(intent),
                Requirements::new(&[Proof::Transport, Proof::Publication]),
            )
            .map_err(|e| format!("Native futures command rejected: {e:?}"))
    }
    /// Explicitly stops one futures dataset's desired work. Its native controller
    /// remains available; no other dataset or option claim is changed.
    ///
    /// # Errors
    /// Unknown dataset or stopped native control.
    pub fn stop_futures(&self, lane: Lane) -> Result<Accepted, String> {
        self.futures
            .get(&lane)
            .ok_or("Expected a futures dataset")?
            .control
            .apply(None, Requirements::new(&[]))
            .map_err(|e| format!("Native futures stop rejected: {e:?}"))
    }
    /// Copied stale-evidence retention/loss accounting. This query cannot retain
    /// a source task or delay an independent dataset retirement.
    ///
    /// # Errors
    /// Interrupted native archive accounting is explicit.
    pub fn retired_evidence_status(
        &self,
    ) -> Result<(usize, crate::messaging::retired_evidence::Accounting), String> {
        self.options.factory.retired_evidence_status()
    }
    /// Native business commands (configure/enable/disable/expiry), not physical
    /// provider handles. Durable feeds require no C# renewal timer.
    pub fn option_claims(&self) -> Option<Arc<Coordinator>> {
        self.claims.lock().ok().and_then(|claims| claims.clone())
    }
    /// Installs native option definitions independently of futures startup.
    /// Replacing a catalog preserves accepted claims and rederives the complete
    /// native subscription/pricing union under the existing recovery budget.
    ///
    /// # Errors
    /// Invalid/incomplete catalog or exhausted root service admission.
    pub fn configure_options_catalog(&self, catalog: Arc<OptionsIntent>) -> Result<(), String> {
        self.clear_catalog_memo()?;
        self.update_options_catalog(catalog, false)
    }
    /// Adds reviewed metadata without replacing another trade's desired members.
    ///
    /// # Errors
    /// Invalid/conflicting native metadata or unavailable native control admission.
    pub fn merge_options_catalog(&self, catalog: Arc<OptionsIntent>) -> Result<(), String> {
        self.clear_catalog_memo()?;
        self.update_options_catalog(catalog, true)
    }
    fn update_options_catalog(
        &self,
        catalog: Arc<OptionsIntent>,
        merge: bool,
    ) -> Result<(), String> {
        self.validate_api_date(catalog.date)?;
        let mut claims = self
            .claims
            .lock()
            .map_err(|_| "Native option catalog control interrupted")?;
        if let Some(current) = claims.as_ref() {
            current.validate_date(catalog.date)?;
        }
        self.notification_date(
            Lane::OptionsGlbx,
            catalog.date,
            catalog.broker.clone(),
            &catalog.scope,
        )?;
        if let Some(current) = claims.as_ref() {
            return if merge {
                current.merge_catalog(catalog)
            } else {
                current.replace_catalog(catalog)
            };
        }
        *claims = Some(Arc::new(Coordinator::start(
            &self.root,
            self.options.control.clone(),
            catalog,
        )?));
        Ok(())
    }
    /// Copied health authority for a specific dataset. This cannot start, renew
    /// or pin provider resources and remains queryable when NATS is unavailable.
    pub fn health(&self, lane: Lane) -> Arc<Recovery> {
        if lane == Lane::OptionsGlbx {
            self.options.control.recovery()
        } else {
            self.futures
                .get(&lane)
                .expect("exhaustive dataset lane")
                .control
                .recovery()
        }
    }
    /// Actual native publication identities for read-only API admission/status
    /// mapping. Unlike current prices, the last identity remains visible while
    /// rebuilding or stopped; callers must inspect health separately.
    pub fn publication_identity(&self, lane: Lane) -> Option<crate::messaging::ordered::Header> {
        if lane == Lane::OptionsGlbx {
            self.options.factory.identity()
        } else {
            self.futures.get(&lane)?.factory.identity()
        }
    }
    /// Whether this lane has accepted non-idle desired work. Healthy idle
    /// controllers must not be shown as a running market-data feed by the Host.
    pub fn has_desired(&self, lane: Lane) -> bool {
        if lane == Lane::OptionsGlbx {
            self.options.control.has_intent()
        } else {
            self.futures
                .get(&lane)
                .is_some_and(|future| future.control.has_intent())
        }
    }
    /// Returns complete native raw-price DTOs for one canonical contract/date.
    /// The caller neither holds a hot reader nor supplies a prepared context.
    ///
    /// # Errors
    /// Interrupted data state or malformed internal source data.
    pub fn raw_prices(
        &self,
        lane: Lane,
        contract: &str,
        date: i32,
    ) -> Result<Option<rmpv::Value>, String> {
        let health = self.health(lane);
        let before = health.status();
        if before.phase != Phase::Running {
            return Ok(None);
        }
        let Some(identity) = self
            .publication_identity(lane)
            .filter(|i| i.value_date == date)
        else {
            return Ok(None);
        };
        let binding = if lane == Lane::OptionsGlbx {
            let Some(view) = self.options_current() else {
                return Ok(None);
            };
            view.registrations
                .iter()
                .find(|r| r.contract == contract)
                .map(|r| (r.instrument, view.queries.clone()))
        } else {
            let source = self.futures.get(&lane).expect("exhaustive native lane");
            source
                .factory
                .registrations()
                .and_then(|rows| {
                    rows.iter()
                        .find(|r| r.contract == contract)
                        .map(|r| r.instrument)
                })
                .zip(source.factory.queries())
        };
        let Some((instrument, queries)) = binding else {
            return Ok(None);
        };
        let Some(raw) = queries.raw_prices(instrument)? else {
            return Ok(None);
        };
        let value = crate::messaging::raw_prices::value(contract, date, raw)?;
        let after = health.status();
        Ok(
            if before.ticket == after.ticket
                && after.phase == Phase::Running
                && self
                    .publication_identity(lane)
                    .is_some_and(|current| current.generation == identity.generation)
            {
                Some(value)
            } else {
                None
            },
        )
    }
    /// Queries the first durably acknowledged calculation only when its exact
    /// trade is still current. This copied data cannot retain an options owner.
    ///
    /// # Errors
    /// Malformed internal evidence or interrupted native query state.
    pub fn trade_greeks(
        &self,
        instrument: u32,
        publisher: u16,
    ) -> Result<Option<rmpv::Value>, String> {
        let health = self.health(Lane::OptionsGlbx);
        let before = health.status();
        let Some(view) = self.options_current() else {
            return Ok(None);
        };
        let Some(registration) = view.registrations.iter().find(|r| {
            r.instrument == instrument
                && r.publisher == publisher
                && r.asset == crate::latest::Asset::Option
        }) else {
            return Ok(None);
        };
        let Some(frame) = view.current.latest(&registration.contract, 6)? else {
            return Ok(None);
        };
        if frame.generation != view.header.generation || frame.value_date != view.header.value_date
        {
            return Ok(None);
        }
        let Some(definition) = view.definitions.iter().find(|d| d.instrument == instrument) else {
            return Ok(None);
        };
        let convention = definition.blueprint.contract.value();
        let underlying = convention
            .as_array()
            .and_then(|v| v.get(9))
            .and_then(rmpv::Value::as_str)
            .ok_or("Missing native underlying identity")?;
        let Some(raw) = view.queries.raw_prices(instrument)? else {
            return Ok(None);
        };
        let physical = raw.generation;
        let raw = crate::messaging::raw_prices::value(
            &registration.contract,
            view.header.value_date,
            raw,
        )?;
        // The payload was generated and bounded by this Rust owner. FFI callers
        // never supply MessagePack for this decoder; no wire length is trusted.
        let mut cursor = std::io::Cursor::new(&frame.payload);
        let evidence = rmpv::decode::read_value(&mut cursor)
            .map_err(|_| "Malformed retained native evidence")?;
        if cursor.position() != frame.payload.len() as u64 {
            return Err("Trailing native evidence bytes".into());
        }
        let projected = crate::messaging::trade_greeks::read(&evidence, underlying, physical)?;
        if raw.as_array().and_then(|v| v.first()) != projected.as_array().and_then(|v| v.first()) {
            return Ok(None);
        }
        let after = health.status();
        let latest = view
            .queries
            .raw_prices(instrument)?
            .map(|raw| {
                crate::messaging::raw_prices::value(
                    &registration.contract,
                    view.header.value_date,
                    raw,
                )
            })
            .transpose()?;
        Ok(
            if before.ticket == after.ticket
                && after.phase == Phase::Running
                && latest
                    .as_ref()
                    .and_then(rmpv::Value::as_array)
                    .and_then(|v| v.first())
                    == raw.as_array().and_then(|v| v.first())
            {
                Some(projected)
            } else {
                None
            },
        )
    }
    /// Copies one bounded current-publication query page. This is explicit
    /// startup/reconnect catch-up; live streaming remains native direct NATS.
    ///
    /// # Errors
    /// Invalid page limits or interrupted native copied-data state.
    pub fn current_frames(
        &self,
        lane: Lane,
        after: i64,
        maximum: usize,
    ) -> Result<Option<rmpv::Value>, String> {
        let health = self.health(lane);
        let before = health.status();
        let current = if lane == Lane::OptionsGlbx {
            self.options_current().map(|v| v.current.clone())
        } else {
            self.futures_current(lane)
        };
        let Some(current) = current else {
            return Ok(None);
        };
        let frames = current.capture_page(after, maximum, 4 * 1024 * 1024)?;
        let values = frames
            .iter()
            .map(crate::messaging::wire::Frame::value)
            .collect::<Result<Vec<_>, _>>()?;
        let now = health.status();
        Ok(
            if before.ticket == now.ticket && now.phase == Phase::Running {
                Some(rmpv::Value::Array(values))
            } else {
                None
            },
        )
    }
    /// Copies exact bootstrap contract DTOs for an active current generation.
    /// An empty explicit-registration template has no invented model projection.
    /// Setup, failure or concurrent replacement returns absence, not stale models.
    pub fn futures_contracts(&self, lane: Lane) -> Option<rmpv::Value> {
        let futures = self.futures.get(&lane)?;
        let before = self.health(lane).status();
        if before.phase != Phase::Running || !futures.control.has_intent() {
            return None;
        }
        let contracts = futures.factory.contracts()?;
        let after = self.health(lane).status();
        (before.ticket == after.ticket && after.phase == Phase::Running)
            .then(|| rmpv::Value::Array(contracts.as_ref().clone()))
    }
    /// Event-driven readiness observation. Dropping/cancelling this waiter does
    /// not cancel accepted desired work or own a physical dataset resource.
    /// The dataset's original deadline, rather than a new caller timer, determines
    /// whether recovery succeeded. Idle readiness is explicit and separate.
    ///
    /// # Errors
    /// Withdrawn required demand, terminal recovery, stopped control or deadline.
    pub async fn wait_ready(&self, lane: Lane, require_active: bool) -> Result<(), String> {
        self.wait_state(lane, require_active, false).await
    }
    /// Waits for provider connectivity without claiming financial readiness.
    /// Missing quotes remain unavailable through the business query methods.
    ///
    /// # Errors
    /// Withdrawn demand, terminal recovery or stopped native control.
    pub async fn wait_connected(&self, lane: Lane, require_active: bool) -> Result<(), String> {
        self.wait_state(lane, require_active, true).await
    }
    async fn wait_state(
        &self,
        lane: Lane,
        require_active: bool,
        connection_only: bool,
    ) -> Result<(), String> {
        let health = self.health(lane);
        let mut changed = health.subscribe();
        loop {
            let status = health.status();
            if require_active && !self.has_desired(lane) {
                return Err("Native dataset desired work was withdrawn".into());
            }
            match status.phase {
                Phase::Running => return Ok(()),
                Phase::Starting if connection_only && health.connection_alive() => return Ok(()),
                Phase::Failed => {
                    return Err(status
                        .latest_error
                        .or(status.root_error)
                        .unwrap_or_else(|| {
                            "Native dataset recovery failed within its original two-minute deadline"
                                .into()
                        }));
                }
                Phase::Stopped => return Err("Native dataset control stopped".into()),
                _ => {}
            }
            let expiry = async {
                if let Some(deadline) = status.deadline {
                    tokio::time::sleep(deadline.remaining()).await
                } else {
                    std::future::pending().await
                }
            };
            tokio::select! {result=changed.changed()=>result.map_err(|_|"Native dataset readiness observation closed")?,()=expiry=>{}}
        }
    }
    /// Observes ordinary feed stop across all datasets after intent withdrawal.
    /// The native controllers remain reusable. No external drain or per-lane
    /// timeout competes with native retirement and its original deadline.
    ///
    /// # Errors
    /// New desired work, terminal retirement failure or closed native control.
    pub async fn wait_feeds_stopped(&self) -> Result<(), String> {
        let lanes = [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx];
        if lanes.iter().any(|lane| self.has_desired(*lane)) {
            return Err("Native feed stop was superseded by accepted desired work".into());
        }
        tokio::try_join!(
            self.wait_ready(lanes[0], false),
            self.wait_ready(lanes[1], false),
            self.wait_ready(lanes[2], false)
        )?;
        if lanes.iter().any(|lane| self.has_desired(*lane)) {
            return Err("Native feed stop was superseded by accepted desired work".into());
        }
        Ok(())
    }
    /// Signals native whole-dataset recovery while retaining accepted desired
    /// work and its earliest original recovery deadline.
    ///
    /// # Errors
    /// A terminal/stopped dataset requires a new explicit command to retry.
    pub fn report_fault(&self, lane: Lane, detail: &str, deadline: Deadline) -> Result<(), String> {
        let result = if lane == Lane::OptionsGlbx {
            self.options.control.report_fault(detail, deadline)
        } else {
            self.futures
                .get(&lane)
                .expect("exhaustive dataset lane")
                .control
                .report_fault(detail, deadline)
        };
        result
            .map(|_| ())
            .map_err(|e| format!("Native dataset fault rejected: {e:?}"))
    }
    /// Reads current copied futures frames only for the same fully healthy
    /// desired ticket. A retired idle view is never returned as live data.
    pub fn futures_current(&self, lane: Lane) -> Option<Arc<Current>> {
        let dataset = self.futures.get(&lane)?;
        let health = dataset.control.recovery();
        let before = health.status();
        if before.phase != Phase::Running || !dataset.control.has_intent() {
            return None;
        }
        let current = dataset.factory.current()?;
        let after = health.status();
        (before.ticket == after.ticket && after.phase == Phase::Running).then_some(current)
    }
    /// Reads detached option query data only for a current healthy claimed scope.
    /// The returned numerical/evidence/cache stores own no provider resources.
    pub fn options_current(&self) -> Option<Arc<View>> {
        let health = self.options.control.recovery();
        let before = health.status();
        if before.phase != Phase::Running || !self.has_desired(Lane::OptionsGlbx) {
            return None;
        }
        let view = self.options.factory.view()?;
        let after = health.status();
        (before.ticket == after.ticket && after.phase == Phase::Running).then_some(view)
    }
    /// Executes the public prepared-cache request policy entirely in Rust.
    /// Reads use the authoritative native/API clock and never acquire data.
    ///
    /// # Errors
    /// Malformed command input, interrupted state or internal encoding failure.
    pub fn prepared_cache_query(&self, input: &serde_json::Value) -> Result<rmpv::Value, String> {
        if input["kind"].as_str() == Some("committed-state") {
            return self.committed_state();
        }
        if input["kind"].as_str() == Some("scope-discovery") {
            let owner = input["owner"]
                .as_array()
                .filter(|values| values.len() == 16)
                .ok_or("Invalid discovery owner")?;
            let mut identity = [0; 16];
            for (target, source) in identity.iter_mut().zip(owner) {
                *target = u8::try_from(source.as_u64().ok_or("Invalid discovery owner")?)
                    .map_err(|_| "Invalid discovery owner")?;
            }
            return self.discovery_scope_result(identity);
        }
        if input["kind"].as_str() == Some("scope-receipt") {
            let scope = input["scope"]
                .as_str()
                .ok_or("Missing native receipt scope")?;
            let lease = input["lease"]
                .as_array()
                .filter(|values| values.len() == 16)
                .ok_or("Invalid native receipt owner")?;
            let mut identity = [0; 16];
            for (target, source) in identity.iter_mut().zip(lease) {
                *target = u8::try_from(source.as_u64().ok_or("Invalid native receipt owner")?)
                    .map_err(|_| "Invalid native receipt owner")?;
            }
            let maturity = input["maturity"]
                .as_i64()
                .filter(|date| (1..=3652058).contains(date))
                .ok_or("Invalid native receipt maturity")?;
            return self.scope_receipt(
                scope,
                identity,
                time::Date::from_julian_day(maturity as i32 + 1721426)
                    .map_err(|_| "Invalid native receipt maturity")?,
            );
        }
        use crate::option_chain::cache_read::{self, Request};
        let request = Request::from_command(input)?;
        let status_only = input["status"]
            .as_bool()
            .ok_or("Invalid native cache query mode")?;
        let now = self
            .clock
            .utc_ticks()
            .map_err(|_| "Native cache query clock unavailable")?;
        let health = self.health(Lane::OptionsGlbx);
        let before = health.status();
        let publication = if let Some(view) = self.options_current() {
            view.caches
                .lock()
                .map_err(|_| "Native cache directory interrupted")?
                .get(&request.scope)
                .cloned()
        } else {
            None
        };
        let result = if status_only {
            cache_read::status(
                &request.scope,
                request.generation,
                publication.as_deref(),
                now,
            )?
        } else {
            cache_read::read(&request, publication.as_deref(), now)?
        };
        let after = health.status();
        if before.ticket != after.ticket || after.phase != Phase::Running {
            return Ok(if status_only {
                cache_read::status(&request.scope, request.generation, None, now)?
            } else {
                cache_read::read(&request, None, now)?
            });
        }
        Ok(result)
    }
    /// Copies a current, unexpired prepared publication for a named native scope.
    /// Readiness and ticket are checked across the copy; an old cache cannot be
    /// returned during rebuilding merely because a caller retained its data view.
    ///
    /// # Errors
    /// Interrupted native cache state is explicit, never a fabricated empty chain.
    pub fn option_cache(
        &self,
        scope: &str,
        at: i64,
    ) -> Result<Option<Arc<crate::messaging::option_cache::Publication>>, String> {
        let health = self.health(Lane::OptionsGlbx);
        let before = health.status();
        let Some(view) = self.options_current() else {
            return Ok(None);
        };
        let cache = view
            .caches
            .lock()
            .map_err(|_| "Native cache query directory interrupted")?
            .get(scope)
            .filter(|cache| at < cache.until)
            .cloned();
        let after = health.status();
        Ok(
            (before.ticket == after.ticket && after.phase == Phase::Running)
                .then_some(cache)
                .flatten(),
        )
    }
    /// Reads one full quote-Greeks projection from its frozen current native
    /// context and source. A replaced source, context or dataset ticket yields
    /// absence; newer raw prices never acquire older risk by accident.
    ///
    /// # Errors
    /// Interrupted native query state or invalid internal pricing data.
    pub fn quote_greeks(
        &self,
        instrument: u32,
        publisher: u16,
        at: i64,
    ) -> Result<Option<rmpv::Value>, String> {
        let health = self.health(Lane::OptionsGlbx);
        let before = health.status();
        let Some(view) = self.options_current() else {
            return Ok(None);
        };
        if !view.registrations.iter().any(|r| {
            r.instrument == instrument
                && r.publisher == publisher
                && r.asset == crate::latest::Asset::Option
        }) {
            return Ok(None);
        }
        let Some((physical, _)) = view.queries.publication_state(instrument)? else {
            return Ok(None);
        };
        let Some(prepared) = view.pricing.trade_context(instrument, physical)? else {
            return Ok(None);
        };
        let Some(context) = prepared.reference else {
            return Ok(None);
        };
        let Some(calculation) = view.pricing.read(instrument, true, at)? else {
            return Ok(None);
        };
        if calculation.source_generation != physical
            || !view
                .pricing
                .matches_observation(instrument, physical, &context, &calculation)?
        {
            return Ok(None);
        }
        let value =
            crate::messaging::quote_greeks::read(&context, &prepared.context, &calculation, at)?;
        let after = health.status();
        Ok(
            if before.ticket == after.ticket
                && after.phase == Phase::Running
                && view
                    .pricing
                    .matches_observation(instrument, physical, &context, &calculation)?
            {
                value
            } else {
                None
            },
        )
    }
    /// Reads exact current aggregate scalars through a native data-only view.
    /// Neither a query result nor its byte representation owns a source client,
    /// dataset task, cancellation token or physical lease.
    ///
    /// # Errors
    /// Interrupted native data state is explicit; recovery races return no data.
    pub fn publication_state(
        &self,
        lane: Lane,
        instrument: u32,
        publisher: u16,
    ) -> Result<Option<(u128, crate::dataset::book::PublicationState)>, String> {
        let health = self.health(lane);
        let before = health.status();
        if before.phase != Phase::Running {
            return Ok(None);
        }
        let queries = if lane == Lane::OptionsGlbx {
            let Some(view) = self.options_current() else {
                return Ok(None);
            };
            if !view
                .registrations
                .iter()
                .any(|r| r.instrument == instrument && r.publisher == publisher)
            {
                return Ok(None);
            }
            view.queries.clone()
        } else {
            let dataset = self.futures.get(&lane).expect("exhaustive dataset lane");
            let Some(intent) = dataset.control.intent() else {
                return Ok(None);
            };
            if !intent
                .source
                .registrations
                .iter()
                .any(|r| r.instrument == instrument && r.publisher == publisher)
            {
                return Ok(None);
            }
            let Some(queries) = dataset.factory.queries() else {
                return Ok(None);
            };
            queries
        };
        let state = queries.publication_state(instrument)?;
        let after = health.status();
        Ok(
            if before.ticket == after.ticket && after.phase == Phase::Running {
                state
            } else {
                None
            },
        )
    }
    /// Copies pending terminal notifications across all datasets, at most 48.
    /// Stops/retries cannot erase these before an explicit delivery acknowledgment.
    pub fn failures(&self) -> Vec<(Lane, Terminal)> {
        [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx]
            .into_iter()
            .flat_map(|lane| {
                self.health(lane)
                    .failures(0, 16)
                    .into_iter()
                    .map(move |record| (lane, record))
            })
            .collect()
    }
    /// Copies the current journal, conditional delivery and actual source
    /// realization fence. Futures-only plans also require their exact native
    /// futures routes; transport liveness alone is never complete realization.
    ///
    /// # Errors
    /// Missing/interrupted business source or copied state.
    pub fn committed_state(&self) -> Result<rmpv::Value, String> {
        use crate::messaging::wire::guid;
        let Some(background) = self
            .options
            .control
            .intent()
            .and_then(|intent| intent.background.clone())
        else {
            return Ok(rmpv::Value::Nil);
        };
        let Some(book) = background.committed_book()? else {
            return Ok(rmpv::Value::Nil);
        };
        let health = self.health(Lane::OptionsGlbx).status();
        let identity = self.publication_identity(Lane::OptionsGlbx);
        let generation = if book.plans.is_empty() {
            self.host_identity
        } else {
            identity.map_or([0; 16], |header| header.generation)
        };
        let ready = background.committed_current()?
            && health.phase == Phase::Running
            && generation != [0; 16]
            && self.committed_futures_ready(&book)?;
        let after = self.health(Lane::OptionsGlbx).status();
        let ready = ready
            && after.ticket == health.ticket
            && after.phase == Phase::Running
            && background.committed_current()?;
        Ok(rmpv::Value::Array(vec![
            book.revision.into(),
            guid(generation).into(),
            ready.into(),
        ]))
    }
    /// Stops desired feeds while retaining reusable native control. This explicit
    /// business command is distinct from final runtime disposal.
    ///
    /// # Errors
    /// Every independent stop is attempted; a control error remains observable.
    pub fn stop_feeds(&self) -> Result<(), String> {
        let first = self.stop_futures(Lane::FuturesGlbx);
        let second = self.stop_futures(Lane::FuturesXcbf);
        let options = self.option_claims().map_or(Ok(()), |claims| claims.clear());
        let routes = self.clear_stream_routes();
        self.empty_discovery
            .lock()
            .map_err(|_| "Native empty result state interrupted")?
            .clear();
        first?;
        second?;
        options?;
        routes
    }
    /// Capture shutdown intent before signaling root cancellation; persistence remains outside datasets.
    pub fn interrupt_audit(&self, reason: &str) {
        self.health(Lane::OptionsGlbx).audit_interrupt(reason);
        for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
            self.health(lane).audit_interrupt(reason);
        }
    }
    /// Signals every native service before awaiting any of them. This path never
    /// waits for a managed consumer, database write or broker acknowledgment.
    pub fn cancel(&self) {
        self.interrupt_audit("Native market-data owner shutdown");
        self.closing
            .store(true, std::sync::atomic::Ordering::Release);
        self.options.control.cancel();
        for futures in self.futures.values() {
            futures.control.cancel();
        }
        if let Ok(notifications) = self.notifications.lock()
            && let Some(notifications) = notifications.as_ref()
        {
            notifications.cancel();
        }
        if let Some(claims) = self.option_claims() {
            claims.cancel();
        }
    }
    /// Joins actual native control/resource retirement under one caller budget.
    /// All independent joins are attempted even if one reports an error.
    ///
    /// # Errors
    /// Retained pending/failed joins prevent a false successful disposal result.
    pub async fn shutdown(&self, deadline: Deadline) -> Result<(), String> {
        self.cancel();
        let mut notifications = self
            .notifications
            .lock()
            .map_err(|_| "Native notification directory interrupted")?
            .take();
        let futures = self.futures.values().collect::<Vec<_>>();
        let claims = self.option_claims();
        let (notification_result, claims, options, first, second) = tokio::join!(
            async {
                if let Some(notifications) = notifications.as_mut() {
                    notifications.shutdown(deadline).await
                } else {
                    Ok(())
                }
            },
            async {
                if let Some(claims) = claims {
                    claims.join(deadline).await
                } else {
                    Ok(())
                }
            },
            self.options.control.join(deadline),
            futures[0].control.join(deadline),
            futures[1].control.join(deadline)
        );
        notification_result?;
        claims.map_err(|e| format!("Native claim service join: {e:?}"))?;
        options?;
        first?;
        second?;
        Ok(())
    }
}
impl Drop for MarketData {
    fn drop(&mut self) {
        self.cancel();
    }
}
