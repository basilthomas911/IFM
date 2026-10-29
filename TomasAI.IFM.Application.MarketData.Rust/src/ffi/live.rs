//! Live lifecycle ABI. Cold control JSON is copied and bounded to 2 MiB; hot
//! prices use fixed records and never serialize. No caller memory survives a call.
use super::runtime::{Status, boundary, instance, valid};
use crate::{
    dataset::{
        lanes::{Gateway, Phase},
        live::{Evidence, LiveData, Registration, Start},
        transport::{Lane, SessionPlan, SubscriptionSpec},
    },
    dns::Resolver,
    latest::Asset,
    runtime::{
        clock::Clock,
        owner::{Outcome, foreign_deadline},
    },
};
use serde_json::Value;
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use tokio_util::sync::CancellationToken;

pub(super) struct Configured {
    pub clock: Arc<Clock>,
    pub live: Arc<LiveData>,
    pub date: time::Date,
    pub identity: u128,
}
/// Application-supplied date/clock/identity. The library never guesses ValueDate.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct NativeLiveConfiguration {
    /// DTO size, 40 bytes.
    pub size: u32,
    /// Calendar date in YYYYMMDD form.
    pub date: u32,
    /// UTC ticks since Unix epoch, not .NET ticks since year one.
    pub utc_ticks: i64,
    /// Opaque identity low bits.
    pub identity_low: u64,
    /// Opaque identity high bits.
    pub identity_high: u64,
    /// Maximum registered contracts, 1..2112 including underlying dependencies.
    pub capacity: u32,
    /// 0 explicitly controlled clock; 1 advance from API anchor.
    pub advancing: u32,
}
/// Owned terminal detail. Cancellation also makes the reservation readable.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeControlResult {
    /// DTO size, 4112 bytes.
    pub size: u32,
    /// 0 success, 1 operation failed, 2 interrupted before terminal work.
    pub failure: u32,
    /// UTF-8 detail length; at most 4096, no NUL terminator required.
    pub length: u32,
    /// Always zero.
    pub reserved: u32,
    /// Sanitized diagnostic bytes, zero filled after length.
    pub message: [u8; 4096],
}
impl Default for NativeControlResult {
    fn default() -> Self {
        Self {
            size: 4112,
            failure: 2,
            length: 0,
            reserved: 0,
            message: [0; 4096],
        }
    }
}
impl NativeControlResult {
    pub(super) fn payload(value: &str) -> Result<Self, Status> {
        if value.len() > 4096 {
            return Err(Status::Capacity);
        }
        let mut output = Self {
            failure: 0,
            length: value.len() as u32,
            ..Default::default()
        };
        output.message[..value.len()].copy_from_slice(value.as_bytes());
        Ok(output)
    }
    pub(super) fn finished(result: Result<(), String>) -> Self {
        let mut output = Self {
            failure: 0,
            ..Default::default()
        };
        if let Err(error) = result {
            output.failure = 1;
            let mut n = error.len().min(4096);
            while !error.is_char_boundary(n) {
                n -= 1;
            }
            output.message[..n].copy_from_slice(&error.as_bytes()[..n]);
            output.length = n as u32;
        }
        output
    }
}
pub(super) struct Reservation {
    pub(super) slots: Arc<Mutex<BTreeMap<u64, Option<NativeControlResult>>>>,
    pub(super) operation: u64,
    pub(super) result: NativeControlResult,
}
impl Drop for Reservation {
    fn drop(&mut self) {
        if let Ok(mut slots) = self.slots.lock()
            && let Some(slot) = slots.get_mut(&self.operation)
        {
            *slot = Some(self.result);
        }
    }
}
/// Stable fixed status; lane process IDs are intentionally absent.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct NativeLaneStatus {
    /// DTO size, 64 bytes.
    pub size: u32,
    /// 0 absent, 1 stopped, 2 starting, 3 running, 4 recovering, 5 failed.
    pub phase: u32,
    /// Session attempt identity.
    pub attempt: u64,
    /// Recovery incident identity.
    pub episode: u64,
    /// Attempts within the current incident.
    pub attempts: u64,
    /// Ready book identity low bits; zero when unavailable.
    pub generation_low: u64,
    /// Ready book identity high bits.
    pub generation_high: u64,
    /// Always zero.
    pub reserved: [u64; 2],
}
/// Raw last observation, all prices exact nanounits and timestamps 100 ns ticks.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct NativeLast {
    /// DTO size, 104 bytes.
    pub size: u32,
    /// Bit 0 present; bit 1 bid present; bit 2 ask present.
    pub flags: u32,
    /// Exact owner identity low bits.
    pub generation_low: u64,
    /// Exact owner identity high bits.
    pub generation_high: u64,
    /// Source sequence.
    pub sequence: i64,
    /// UTC event ticks since Unix epoch.
    pub event_ticks: i64,
    /// Provider receive ticks.
    pub receive_ticks: i64,
    /// Trade price, zero for quote reads.
    pub price: i64,
    /// Trade size, zero for quote reads.
    pub quantity: u32,
    /// Always zero.
    pub reserved: u32,
    /// Quote bid when flags bit 1 is set.
    pub bid: i64,
    /// Quote ask when flags bit 2 is set.
    pub ask: i64,
    /// Quote bid contracts.
    pub bid_quantity: u32,
    /// Quote ask contracts.
    pub ask_quantity: u32,
    /// Local API receive time, zero for trades.
    pub local_ticks: i64,
    /// Number of bid orders, preserved independently from bid quantity.
    pub bid_count: u32,
    /// Number of ask orders, preserved independently from ask quantity.
    pub ask_count: u32,
}
pub(super) fn lane(value: u32) -> Result<Lane, Status> {
    match value {
        1 => Ok(Lane::FuturesGlbx),
        2 => Ok(Lane::FuturesXcbf),
        3 => Ok(Lane::OptionsGlbx),
        _ => Err(Status::InvalidInput),
    }
}
fn date(value: u32) -> Result<time::Date, Status> {
    time::Date::from_calendar_date(
        (value / 10000) as i32,
        (((value / 100) % 100) as u8)
            .try_into()
            .map_err(|_| Status::InvalidInput)?,
        (value % 100) as u8,
    )
    .map_err(|_| Status::InvalidInput)
}
pub(super) fn live(handle: u64) -> Result<Arc<LiveData>, Status> {
    instance(handle)?
        .live
        .lock()
        .map_err(|_| Status::InternalFailure)?
        .as_ref()
        .map(|v| v.live.clone())
        .ok_or(Status::InvalidInput)
}
/// Installs one immutable epoch per runtime; replacement requires explicit disposal.
/// # Safety
/// config is aligned readable storage for one DTO for the duration of this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_configure_v1(
    handle: u64,
    config: *const NativeLiveConfiguration,
) -> i32 {
    boundary(|| {
        if !valid(config.cast_mut()) {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Alignment checked and caller guarantees readable storage.
        let config = unsafe { config.read() };
        if config.size != 40 {
            return Err(Status::AbiMismatch);
        }
        if config.advancing > 1 {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let owner_mode = instance
            .market_owner
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if owner_mode.is_some() {
            return Err(Status::Busy);
        }
        let mut slot = instance.live.lock().map_err(|_| Status::InternalFailure)?;
        if slot.is_some() {
            return Err(Status::Busy);
        }
        let candidate = Arc::new(
            Clock::new(config.utc_ticks, config.advancing == 1)
                .map_err(|_| Status::InvalidInput)?,
        );
        let mut shared = instance
            .api_clock
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        let clock = shared.clone().unwrap_or(candidate);
        let identity = (u128::from(config.identity_high) << 64) | u128::from(config.identity_low);
        let data = LiveData::new(
            date(config.date)?,
            identity,
            config.capacity as usize,
            clock.clone(),
            instance.owner.clone(),
        )
        .map_err(|_| Status::InvalidInput)?;
        clock
            .update(config.utc_ticks, config.advancing == 1)
            .map_err(|_| Status::InvalidInput)?;
        *shared = Some(clock.clone());
        *slot = Some(Configured {
            clock,
            live: Arc::new(data),
            date: date(config.date)?,
            identity,
        });
        Ok(())
    })
}
/// Configures or updates the explicit API clock independently of live ownership.
/// Reference/historical administration can run before a trading ValueDate or live
/// epoch exists. A later live configuration shares this same clock; no local
/// business date is inferred or synthesized. Invalid inputs leave it unchanged.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_clock_v1(handle: u64, ticks: i64, advancing: u32) -> i32 {
    boundary(|| {
        if advancing > 1 {
            return Err(Status::InvalidInput);
        }
        let candidate = Clock::new(ticks, advancing == 1).map_err(|_| Status::InvalidInput)?;
        let instance = instance(handle)?;
        let mut clock = instance
            .api_clock
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if let Some(clock) = clock.as_ref() {
            clock
                .update(ticks, advancing == 1)
                .map_err(|_| Status::InvalidInput)?;
        } else {
            *clock = Some(Arc::new(candidate));
        }
        Ok(())
    })
}
/// Updates the explicit API clock without deriving a local calendar date.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_live_clock_v1(handle: u64, ticks: i64, advancing: u32) -> i32 {
    boundary(|| {
        if advancing > 1 {
            return Err(Status::InvalidInput);
        }
        instance(handle)?
            .live
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .as_ref()
            .ok_or(Status::InvalidInput)?
            .clock
            .update(ticks, advancing == 1)
            .map_err(|_| Status::InvalidInput)
    })
}
fn string(value: &Value, key: &str) -> Result<String, Status> {
    value
        .get(key)
        .and_then(Value::as_str)
        .map(str::to_owned)
        .ok_or(Status::InvalidInput)
}
fn integer(value: &Value, key: &str) -> Result<u32, Status> {
    value
        .get(key)
        .and_then(Value::as_u64)
        .and_then(|n| u32::try_from(n).ok())
        .ok_or(Status::InvalidInput)
}
fn boolean(value: &Value, key: &str) -> Result<bool, Status> {
    value
        .get(key)
        .and_then(Value::as_bool)
        .ok_or(Status::InvalidInput)
}
pub(super) fn parse(
    input: &[u8],
    selected: Lane,
    dns_slots: Arc<tokio::sync::Semaphore>,
) -> Result<Start, Status> {
    parse_template(input, selected, dns_slots, false)
}
/// Native bootstrap may have no resolved registrations yet. Its subscriptions
/// remain validated templates and cannot reach the SDK until discovery succeeds.
pub(super) fn parse_template(
    input: &[u8],
    selected: Lane,
    dns_slots: Arc<tokio::sync::Semaphore>,
    bootstrap: bool,
) -> Result<Start, Status> {
    let v: Value = serde_json::from_slice(input).map_err(|_| Status::InvalidInput)?;
    let key = string(&v, "key")?;
    if key.len() != 32 || !key.is_ascii() || key.bytes().any(|b| b.is_ascii_control()) {
        return Err(Status::InvalidInput);
    }
    let host = string(&v, "host")?;
    if host.is_empty() || host.len() > 253 {
        return Err(Status::InvalidInput);
    }
    let port = u16::try_from(integer(&v, "port")?).map_err(|_| Status::InvalidInput)?;
    if port == 0 {
        return Err(Status::InvalidInput);
    }
    let dns = v["dns"].as_array().ok_or(Status::InvalidInput)?;
    if dns.is_empty() || dns.len() > 4 {
        return Err(Status::InvalidInput);
    }
    let servers = dns
        .iter()
        .map(|v| {
            v.as_str()
                .ok_or(Status::InvalidInput)?
                .parse()
                .map_err(|_| Status::InvalidInput)
        })
        .collect::<Result<Vec<_>, _>>()?;
    let resolver =
        Arc::new(Resolver::with_slots(servers, dns_slots).map_err(|_| Status::InvalidInput)?);
    let subs = v["subscriptions"].as_array().ok_or(Status::InvalidInput)?;
    if (!bootstrap && subs.is_empty()) || subs.len() > 64 {
        return Err(Status::InvalidInput);
    }
    let mut subscriptions = Vec::with_capacity(subs.len());
    for s in subs {
        let symbols = s["symbols"]
            .as_array()
            .ok_or(Status::InvalidInput)?
            .iter()
            .map(|v| v.as_str().map(str::to_owned).ok_or(Status::InvalidInput))
            .collect::<Result<Vec<_>, _>>()?;
        let schema = string(s, "schema")?
            .parse()
            .map_err(|_| Status::InvalidInput)?;
        let symbology = string(s, "symbology")?
            .parse()
            .map_err(|_| Status::InvalidInput)?;
        let replay_start_ns = if s["replay_start_ns"].is_null() {
            None
        } else {
            Some(s["replay_start_ns"].as_i64().ok_or(Status::InvalidInput)?)
        };
        subscriptions.push(SubscriptionSpec {
            symbols,
            schema,
            symbology,
            replay_start_ns,
        });
    }
    let regs = v["registrations"].as_array().ok_or(Status::InvalidInput)?;
    if (!bootstrap && regs.is_empty()) || regs.len() > 2112 {
        return Err(Status::InvalidInput);
    }
    let mut registrations = Vec::with_capacity(regs.len());
    for r in regs {
        let asset = match string(r, "asset")?.as_str() {
            "futures" => Asset::Futures,
            "option" => Asset::Option,
            _ => return Err(Status::InvalidInput),
        };
        let tick = if r["tick"].is_null() {
            None
        } else {
            Some(r["tick"].as_i64().ok_or(Status::InvalidInput)?)
        };
        registrations.push(Registration {
            instrument: integer(r, "instrument")?,
            publisher: if r["publisher"].is_null() {
                0
            } else {
                u16::try_from(integer(r, "publisher")?).map_err(|_| Status::InvalidInput)?
            },
            contract: string(r, "contract")?,
            source: string(r, "source")?,
            asset,
            tick,
            vwap: boolean(r, "vwap")?,
            required: boolean(r, "required")?,
        });
    }
    let evidence = match string(&v, "evidence")?.as_str() {
        "observations" => Evidence::Observations,
        "control" => Evidence::ControlOnly,
        _ => return Err(Status::InvalidInput),
    };
    let plan = SessionPlan {
        lane: selected,
        subscriptions,
    };
    if !bootstrap || !plan.subscriptions.is_empty() {
        plan.validate().map_err(|_| Status::InvalidInput)?;
    }
    Ok(Start {
        plan,
        registrations,
        key,
        gateway: Gateway {
            host,
            port,
            resolver,
        },
        evidence,
    })
}
/// Admits start (0), reset (1) or stop (2). Only start accepts a JSON control body.
/// Every result must be taken after completion, including cancelled operations.
/// # Safety
/// input points to length readable bytes during this call; length zero permits null.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_submit_v1(
    handle: u64,
    operation: u64,
    action: u32,
    selected: u32,
    input: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = foreign_deadline(budget_ms);
    boundary(|| {
        let selected = lane(selected)?;
        if action > 2
            || length > 2 * 1024 * 1024
            || (length > 0 && input.is_null())
            || (action != 0 && length != 0)
        {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let request = if action == 0 {
            if length == 0 {
                return Err(Status::InvalidInput);
            }
            // SAFETY: Caller guarantees length readable bytes; cap checked before parsing.
            Some(parse(
                unsafe { std::slice::from_raw_parts(input, length as usize) },
                selected,
                instance.dns_slots.clone(),
            )?)
        } else {
            None
        };
        let live = instance
            .live
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .as_ref()
            .ok_or(Status::InvalidInput)?
            .live
            .clone();
        let mut reservation = instance.reserve_control(operation)?;
        let scopes = instance.scopes.clone();
        let work = async move {
            let token = CancellationToken::new();
            // Flat compatibility and scoped options modes cannot own two
            // publishers concurrently. Stop remains independent of this gate.
            let _mode = if selected == Lane::OptionsGlbx && action != 2 {
                Some(deadline.run(&token, scopes.replacement.lock()).await)
            } else {
                None
            };
            let mode_error = if let Some(Err(error)) = &_mode {
                Some(error.to_string())
            } else if selected == Lane::OptionsGlbx && action != 2 {
                match scopes.flat_available() {
                    Ok(true) => None,
                    Ok(false) => Some(
                        "Scoped options ownership must be stopped before flat activation".into(),
                    ),
                    Err(error) => Some(format!("Options ownership state failure: {error:?}")),
                }
            } else {
                None
            };
            let result = if let Some(error) = mode_error {
                Err(error)
            } else {
                match action {
                    0 => live
                        .start(request.expect("validated start"), &token, deadline)
                        .await
                        .map(|_| ()),
                    1 => live.reset(selected, &token, deadline).await.map(|_| ()),
                    _ => live.stop(selected, &token, deadline).await,
                }
            };
            reservation.result = NativeControlResult::finished(result);
            drop(reservation);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok::<_, Outcome>(0.0)
            }
        };
        if let Err(status) = instance.submit(operation, deadline, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}
/// Consumes exactly one ready control result; pending/invalid calls consume nothing.
/// # Safety
/// output points to one aligned, exclusive writable result for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_take_v1(
    handle: u64,
    operation: u64,
    size: u32,
    output: *mut NativeControlResult,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 4112 {
            return Err(Status::AbiMismatch);
        }
        let instance = instance(handle)?;
        let mut slots = instance
            .control
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        let result = slots
            .get(&operation)
            .ok_or(Status::InvalidInput)?
            .ok_or(Status::Busy)?;
        // SAFETY: Caller guarantees aligned exclusive output; validated before consumption.
        unsafe { output.write(result) };
        slots.remove(&operation);
        Ok(())
    })
}
/// Reads current lifecycle synchronously without waiting for provider I/O.
/// # Safety
/// output is aligned exclusive writable storage for a 64-byte status.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_status_v1(
    handle: u64,
    selected: u32,
    size: u32,
    output: *mut NativeLaneStatus,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 64 {
            return Err(Status::AbiMismatch);
        }
        let selected = lane(selected)?;
        let live = live(handle)?;
        let mut out = NativeLaneStatus {
            size: 64,
            ..Default::default()
        };
        if let Some(state) = live.status(selected).map_err(|_| Status::InternalFailure)? {
            out.phase = match state.phase {
                Phase::Stopped => 1,
                Phase::Starting => 2,
                Phase::Running => 3,
                Phase::Recovering => 4,
                Phase::Failed => 5,
            };
            out.attempt = state.generation;
            out.episode = state.episode;
            out.attempts = state.attempts;
        }
        if let Some(generation) = live
            .generation(selected)
            .map_err(|_| Status::InternalFailure)?
        {
            out.generation_low = generation as u64;
            out.generation_high = (generation >> 64) as u64;
        }
        // SAFETY: Caller guarantees writable storage; size/alignment checked.
        unsafe { output.write(out) };
        Ok(())
    })
}
/// Reads last raw trade (1) or quote (2). Absent/unready data returns flags zero.
/// # Safety
/// id is length readable UTF-8 bytes; output is aligned exclusive 104-byte storage.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_last_v1(
    handle: u64,
    selected: u32,
    kind: u32,
    id: *const u8,
    length: u32,
    size: u32,
    output: *mut NativeLast,
) -> i32 {
    boundary(|| {
        if !valid(output) || id.is_null() || length == 0 || length > 256 || !(1..=2).contains(&kind)
        {
            return Err(Status::InvalidInput);
        }
        if size != 104 {
            return Err(Status::AbiMismatch);
        }
        // SAFETY: Caller guarantees readable bytes and length is capped before access.
        let id = std::str::from_utf8(unsafe { std::slice::from_raw_parts(id, length as usize) })
            .map_err(|_| Status::InvalidInput)?;
        let live = live(handle)?;
        let selected = lane(selected)?;
        let mut out = NativeLast {
            size: 104,
            ..Default::default()
        };
        if let Some(reader) = live
            .reader(selected, id)
            .map_err(|_| Status::InternalFailure)?
        {
            let generation = reader.generation();
            out.generation_low = generation as u64;
            out.generation_high = (generation >> 64) as u64;
            if kind == 1 {
                if let Some(t) = reader.trade().map_err(|_| Status::InternalFailure)? {
                    out.flags = 1;
                    out.sequence = t.sequence;
                    out.event_ticks = t.event_ticks;
                    out.receive_ticks = t.receive_ticks;
                    out.price = t.value.price;
                    out.quantity = t.value.size;
                }
            } else if let Some(q) = reader.quote().map_err(|_| Status::InternalFailure)? {
                out.flags = 1
                    | if q.value.bid.is_some() { 2 } else { 0 }
                    | if q.value.ask.is_some() { 4 } else { 0 };
                out.sequence = q.sequence;
                out.event_ticks = q.event_ticks;
                out.receive_ticks = q.receive_ticks;
                out.bid = q.value.bid.unwrap_or(0);
                out.ask = q.value.ask.unwrap_or(0);
                out.bid_quantity = q.value.bid_size;
                out.ask_quantity = q.value.ask_size;
                out.local_ticks = q.value.local_received_ticks;
                out.bid_count = q.value.bid_count;
                out.ask_count = q.value.ask_count;
            }
        }
        // SAFETY: Validated DTO size/alignment and caller guarantees writable storage.
        unsafe { output.write(out) };
        Ok(())
    })
}

/// Atomically consumes wake-up bits; data itself remains in independent queues.
/// # Safety
/// output is aligned exclusive writable storage for one u32.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_signals_v1(handle: u64, output: *mut u32) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        let bits = instance(handle)?
            .owner
            .take_signals()
            .map_err(Status::from)?;
        // SAFETY: Alignment checked and caller guarantees exclusive writable storage.
        unsafe { output.write(bits) };
        Ok(())
    })
}
/// Fixed normalized authoritative record; no strings or borrowed DBN pointers.
#[repr(C)]
#[derive(Clone, Copy, Default)]
pub struct NativeMarketEvent {
    /// DTO size, 128 bytes.
    pub size: u32,
    /// Logical lane 1..3.
    pub lane: u32,
    /// 1 quote, 2 trade, 3 order, 4 statistic, 5 trade replay completion.
    pub kind: u32,
    /// Provider instrument identity.
    pub instrument: u32,
    /// Opaque owner identity low bits.
    pub generation_low: u64,
    /// Opaque owner identity high bits.
    pub generation_high: u64,
    /// Source-exact tagged payload; see ABI-OWNERSHIP.md for slot definitions.
    pub fields: [i64; 12],
}
impl From<&crate::dataset::live::Output> for NativeMarketEvent {
    fn from(value: &crate::dataset::live::Output) -> Self {
        use crate::dataset::{live::OutputSource, records::Observation};
        let mut out = Self {
            size: 128,
            lane: value.lane as u32,
            generation_low: value.generation as u64,
            generation_high: (value.generation >> 64) as u64,
            ..Default::default()
        };
        let record = match value.source {
            OutputSource::Observation(record) => record,
            OutputSource::ReplayComplete(instrument) => {
                out.kind = 5;
                out.instrument = instrument;
                return out;
            }
            OutputSource::StatisticsReplayComplete(instrument) => {
                out.kind = 6;
                out.instrument = instrument;
                return out;
            }
        };
        let (header, tail) = match record {
            Observation::Quote(v) => {
                out.kind = 1;
                (
                    v.header,
                    [
                        v.bid,
                        v.ask,
                        i64::from(v.bid_size),
                        i64::from(v.ask_size),
                        i64::from(v.bid_count),
                        i64::from(v.ask_count),
                        0,
                    ],
                )
            }
            Observation::Trade(v) => {
                out.kind = 2;
                (
                    v.header,
                    [
                        v.price,
                        i64::from(v.size),
                        i64::from(v.action),
                        i64::from(v.side),
                        i64::from(v.dbn_flags),
                        i64::from(v.depth),
                        i64::from(v.delta_ns),
                    ],
                )
            }
            Observation::Order(v) => {
                out.kind = 3;
                (
                    v.header,
                    [
                        v.price,
                        i64::from(v.size),
                        i64::from(v.action),
                        i64::from(v.side),
                        i64::from(v.dbn_flags) | (i64::from(v.channel) << 8),
                        v.order_id as i64,
                        i64::from(v.delta_ns),
                    ],
                )
            }
            Observation::Statistic(v) => {
                out.kind = 4;
                (
                    v.header,
                    [
                        v.price,
                        v.quantity,
                        v.reference_ns,
                        i64::from(v.kind),
                        i64::from(v.channel),
                        i64::from(v.action),
                        i64::from(v.flags),
                    ],
                )
            }
        };
        out.instrument = header.instrument;
        out.fields[0] = i64::from(header.sequence);
        out.fields[1] = header.event_ns;
        out.fields[2] = header.received_ns;
        out.fields[3] = i64::from(header.schema);
        out.fields[4] = i64::from(header.publisher) | (i64::from(header.flags) << 16);
        out.fields[5..].copy_from_slice(&tail);
        out
    }
}
/// Complete source diagnostic; unknown codes and end-of-batch markers survive.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct NativeDiagnostic {
    /// DTO size, 4128 bytes.
    pub size: u32,
    /// Logical owner 1..3.
    pub lane: u32,
    /// 1 system, 2 error.
    pub kind: u32,
    /// Unmodified provider code.
    pub code: u32,
    /// Physical session attempt identity.
    pub attempt: u64,
    /// End of error batch, zero for system messages.
    pub last: u32,
    /// UTF-8 bytes used, no terminator needed.
    pub length: u32,
    /// Complete redacted provider text; bounded DBN text fits this buffer.
    pub message: [u8; 4096],
}
/// Drains at most 64 records, round-robin across independent lane buffers.
/// # Safety
/// output holds capacity aligned exclusive writable records; count is separate
/// aligned writable storage. Null output is allowed only at zero capacity.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_events_v1(
    handle: u64,
    output: *mut NativeMarketEvent,
    capacity: u32,
    count: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(count) || capacity > 64 || (capacity > 0 && !valid(output)) {
            return Err(Status::InvalidInput);
        }
        let empty = empty_output();
        let mut batch: [_; 64] = std::array::from_fn(|_| empty.clone());
        let copied = live(handle)?
            .drain_output(&mut batch[..capacity as usize])
            .map_err(|_| Status::InternalFailure)?;
        for (i, value) in batch[..copied].iter().enumerate() {
            // SAFETY: Copied count cannot exceed validated caller capacity.
            unsafe { output.add(i).write(value.into()) };
        }
        // SAFETY: Caller guarantees exclusive count storage, independent of records.
        unsafe { count.write(copied as u32) };
        Ok(())
    })
}
pub(super) fn empty_output() -> crate::dataset::live::Output {
    use crate::dataset::{
        live::{Output, OutputSource},
        records::{Header, Observation, Quote},
    };
    Output {
        observed: 0,
        lane: Lane::FuturesGlbx,
        generation: 0,
        state: Default::default(),
        trade: None,
        source: OutputSource::Observation(Observation::Quote(Quote {
            header: Header {
                instrument: 0,
                publisher: 0,
                flags: 0,
                event_ns: 0,
                received_ns: 0,
                sequence: 0,
                schema: 0,
            },
            bid: 0,
            ask: 0,
            bid_size: 0,
            ask_size: 0,
            bid_count: 0,
            ask_count: 0,
        })),
    }
}
/// Drains at most 64 diagnostics without discarding the undelivered tail.
/// # Safety
/// output holds capacity aligned exclusive writable records; count is separate
/// aligned writable storage. Null output is allowed only at zero capacity.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_diagnostics_v1(
    handle: u64,
    output: *mut NativeDiagnostic,
    capacity: u32,
    count: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(count) || capacity > 64 || (capacity > 0 && !valid(output)) {
            return Err(Status::InvalidInput);
        }
        use crate::dataset::transport::Event;
        let batch = live(handle)?
            .drain_diagnostic_batch(capacity as usize)
            .map_err(|_| Status::InternalFailure)?;
        for (i, value) in batch.iter().enumerate() {
            let (kind, code, last, message) = match &value.event {
                Event::System { code, message } => (1, *code, 0, message),
                Event::Error {
                    code,
                    last,
                    message,
                } => (2, *code, u32::from(*last), message),
                _ => return Err(Status::InternalFailure),
            };
            // DBN record text is smaller than 4096, checked before any payload copy.
            if message.len() > 4096 {
                return Err(Status::InternalFailure);
            }
            let mut record = NativeDiagnostic {
                size: 4128,
                lane: value.lane as u32,
                kind,
                code: u32::from(code),
                attempt: value.attempt,
                last,
                length: message.len() as u32,
                message: [0; 4096],
            };
            record.message[..message.len()].copy_from_slice(message.as_bytes());
            // SAFETY: Every destination lies within validated caller capacity.
            unsafe { output.add(i).write(record) };
        }
        // SAFETY: Caller guarantees separate aligned exclusive count storage.
        unsafe { count.write(batch.len() as u32) };
        Ok(())
    })
}

/// Copies the retained current failure detail without consuming provider logs.
/// # Safety
/// output is aligned exclusive writable storage for a 4112-byte control result.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_live_failure_v1(
    handle: u64,
    selected: u32,
    size: u32,
    output: *mut NativeControlResult,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 4112 {
            return Err(Status::AbiMismatch);
        }
        let state = live(handle)?
            .status(lane(selected)?)
            .map_err(|_| Status::InternalFailure)?;
        let result = NativeControlResult::finished(state.and_then(|s| s.error).map_or(Ok(()), Err));
        // SAFETY: Validated layout/alignment; caller guarantees exclusive writable storage.
        unsafe { output.write(result) };
        Ok(())
    })
}
