//! Bounded read-only live trade replay for current-session indicator seeding.
//! It owns a temporary Tokio socket, not a worker thread or primary feed lane.
use super::*;
use crate::{
    dataset::{
        databento::NamedGateway,
        records::Observation,
        transport::{Event, Lane, Session, SessionPlan, SubscriptionSpec},
    },
    dns::Resolver,
};
use databento::dbn::{SType, Schema};
use serde::Deserialize;
#[derive(Deserialize)]
struct Query {
    dataset: String,
    symbol: String,
    key: String,
    host: String,
    port: u16,
    dns: Vec<std::net::SocketAddr>,
    start: i64,
}
/// Opens one current-session replay, ending only at the provider's trade replay
/// completion marker. At most four readers share the normal historical reader
/// slots. EOF before that marker fails; queued rows remain provisional. The
/// original capped recovery budget covers DNS, setup, reads and backpressure.
/// # Safety
/// Input is readable for length bytes (1..8192), borrowed only during this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_replay_open_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
    capacity: u32,
    budget_ms: u64,
) -> i32 {
    let deadline =
        Deadline::operation(Duration::from_millis(budget_ms)).earliest(Deadline::recovery());
    boundary(|| {
        if input.is_null() || length == 0 || length > 8192 || !(1..=4096).contains(&capacity) {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller supplies the bounded readable input; all fields are owned.
        let query: Query =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let lane = match query.dataset.as_str() {
            "GLBX.MDP3" => Lane::FuturesGlbx,
            "XCBF.PITCH" => Lane::FuturesXcbf,
            _ => return Err(Status::InvalidInput),
        };
        if query.host.is_empty()
            || query.host.len() > 253
            || query.port == 0
            || query.key.len() != 32
            || !query.key.is_ascii()
        {
            return Err(Status::InvalidInput);
        }
        let plan = SessionPlan {
            lane,
            subscriptions: vec![SubscriptionSpec {
                symbols: vec![query.symbol.clone()],
                symbology: SType::RawSymbol,
                schema: Schema::Trades,
                replay_start_ns: Some(query.start),
            }],
        };
        plan.validate().map_err(|_| Status::InvalidInput)?;
        let resolver = Resolver::new(query.dns).map_err(|_| Status::InvalidInput)?;
        let instance = instance(handle)?;
        let history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let mut readers = instance
            .history_readers
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if readers.len() >= 4 || readers.contains_key(&operation) {
            return Err(Status::Capacity);
        }
        let mut reservation = instance.reserve_control(operation)?;
        let (sender, receiver) = mpsc::channel(capacity as usize);
        let queue = Arc::new(Queue {
            receiver: Some(Mutex::new(receiver)),
            bytes: None,
            owner: instance.owner.clone(),
            input: Mutex::new(None),
            is_file: false,
        });
        readers.insert(operation, queue.clone());
        drop(readers);
        let signal = Signal(queue.clone());
        let work = async move {
            let token = CancellationToken::new();
            let result = async {
                let mut session = Session::open(
                    &plan,
                    query.key,
                    NamedGateway {
                        host: &query.host,
                        port: query.port,
                        resolver: &resolver,
                    },
                    &token,
                    deadline,
                )
                .await
                .map_err(|e| format!("Replay setup failed: {e:?}"))?;
                let mut instrument = None;
                loop {
                    match session
                        .next(&token, false, true)
                        .await
                        .map_err(|e| format!("Replay read failed: {e:?}"))?
                    {
                        Some(Event::Mapping {
                            requested,
                            instrument: id,
                            publisher: _,
                        }) if requested == query.symbol => instrument = Some(id),
                        Some(Event::Market(Observation::Trade(trade))) => {
                            if instrument != Some(trade.header.instrument) {
                                return Err("Replay trade has no matching provider symbol mapping"
                                    .to_owned());
                            }
                            let mut row = Record {
                                size: 120,
                                abi: 1,
                                kind: 3,
                                schema: 3,
                                instrument: trade.header.instrument,
                                publisher: trade.header.publisher,
                                event: trade.header.event_ns,
                                sequence: i64::from(trade.header.sequence),
                                price: trade.price,
                                quantity: u64::from(trade.size),
                                action: trade.action,
                                side: trade.side,
                                ..Default::default()
                            };
                            let symbol = query.symbol.as_bytes();
                            let count = symbol.len().min(31);
                            row.symbol[..count].copy_from_slice(&symbol[..count]);
                            sender
                                .send(row)
                                .await
                                .map_err(|_| "Replay consumer closed".to_owned())?;
                            queue
                                .owner
                                .signal(1 << 5)
                                .map_err(|_| "Replay delivery interrupted".to_owned())?;
                        }
                        Some(Event::System { code, message }) => {
                            history
                                .record_notice("live.replay.system", u16::from(code), &message)
                                .map_err(|e| format!("Replay notice failed: {e:?}"))?;
                            if code == 3 && message.contains("trades") {
                                return Ok(());
                            }
                        }
                        Some(Event::Error {
                            code,
                            last,
                            message,
                        }) => {
                            return Err(format!(
                                "Replay ErrorMsg code={code} is_last={last}: {message}"
                            ));
                        }
                        Some(_) => {}
                        None => {
                            return Err("Provider ended before trade replay completion".to_owned());
                        }
                    }
                }
            }
            .await;
            reservation.result = super::super::live::NativeControlResult::finished(result);
            drop(reservation);
            drop(signal);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok(0.0)
            }
        };
        if let Err(error) = instance.submit(operation, deadline, work) {
            instance
                .history_readers
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(error);
        }
        Ok(())
    })
}
