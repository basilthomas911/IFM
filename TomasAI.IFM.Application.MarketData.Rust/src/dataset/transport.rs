//! Real owned Databento sessions. Setup has the caller's original deadline;
//! normal live reads do not expire merely because two minutes have elapsed.
//! There are no background SDK reconnectors or blocking DNS jobs. A supervisor
//! must explicitly retire this complete session before installing its replacement.

use super::{
    databento::NamedGateway,
    records::{self, Observation},
};
use crate::{
    dns::ResolveError,
    runtime::{Deadline, Interrupted},
};
use databento::{
    LiveClient,
    dbn::{ErrorMsg, Record, SType, Schema, SymbolMappingMsg, SystemMsg},
    live::{SlowReaderBehavior, Subscription, TimeoutConf},
};
use futures_util::FutureExt;
use std::panic::AssertUnwindSafe;
use tokio_util::sync::CancellationToken;

/// Independently owned dataset/role lanes; options never share a session owner
/// with GLBX futures even though Databento gives both the same dataset string.
#[derive(Debug, Copy, Clone, PartialEq, Eq, PartialOrd, Ord)]
#[repr(u8)]
pub enum Lane {
    /// ES and other GLBX futures.
    FuturesGlbx = 1,
    /// VX futures on CFE.
    FuturesXcbf = 2,
    /// GLBX futures options.
    OptionsGlbx = 3,
}

impl Lane {
    /// Authoritative provider dataset for this lane.
    pub fn dataset(self) -> &'static str {
        match self {
            Self::FuturesGlbx | Self::OptionsGlbx => "GLBX.MDP3",
            Self::FuturesXcbf => "XCBF.PITCH",
        }
    }
    /// Existing public worker-role spelling, without implying a child process.
    pub fn role(self) -> &'static str {
        match self {
            Self::FuturesGlbx | Self::FuturesXcbf => "Futures",
            Self::OptionsGlbx => "Options",
        }
    }
}

/// One provider subscription group. Provider symbols stay below the application
/// facade; canonical domain contract IDs must be mapped before constructing it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SubscriptionSpec {
    /// Nonempty provider symbols, with bounded wire-safe ASCII spelling.
    pub symbols: Vec<String>,
    /// Raw symbol, continuous symbol or provider instrument identity input.
    pub symbology: SType,
    /// Required market schema.
    pub schema: Schema,
    /// Explicit API-selected replay start in Unix nanoseconds; None means live.
    pub replay_start_ns: Option<i64>,
}

/// Immutable desired provider subscriptions for one session attempt.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SessionPlan {
    /// Dataset/role isolation identity.
    pub lane: Lane,
    /// Bounded subscription groups; the supervisor retains desired business intent.
    pub subscriptions: Vec<SubscriptionSpec>,
}

impl SessionPlan {
    /// Validates before DNS/SDK invocation, including protocol delimiters and
    /// limits. An idle lane needs no session; an empty plan is rejected here.
    pub fn validate(&self) -> Result<(), Failure> {
        if self.subscriptions.is_empty() || self.subscriptions.len() > 64 {
            return Err(Failure::Input("Subscription group count must be 1..64"));
        }
        let mut count = 0usize;
        for sub in &self.subscriptions {
            count = count.saturating_add(sub.symbols.len());
            if sub.symbols.is_empty() || count > 65_536 {
                return Err(Failure::Input(
                    "Provider symbol capacity exceeded or empty subscription",
                ));
            }
            if !matches!(
                sub.schema,
                Schema::Mbp1 | Schema::Trades | Schema::Mbo | Schema::Statistics
            ) || !matches!(
                sub.symbology,
                SType::RawSymbol | SType::InstrumentId | SType::Continuous
            ) {
                return Err(Failure::Input(
                    "Unsupported live market subscription schema/symbology",
                ));
            }
            if sub.symbols.iter().any(|s| {
                s.is_empty()
                    || s.len() > 70
                    || !s.is_ascii()
                    || s.bytes()
                        .any(|b| b.is_ascii_control() || matches!(b, b',' | b'|' | b'='))
            }) {
                return Err(Failure::Input("Invalid provider symbol format"));
            }
            if sub.replay_start_ns.is_some_and(|start| start < 0) {
                return Err(Failure::Input(
                    "Replay start must be an explicit nonnegative Unix timestamp",
                ));
            }
        }
        Ok(())
    }
}

/// Session failure; transport/session diagnostics never contain its supplied key.
#[derive(Debug)]
pub enum Failure {
    /// Input validation failed before contacting the provider.
    Input(&'static str),
    /// Original setup deadline or explicit caller cancellation.
    Interrupted(Interrupted),
    /// Cancellable DNS failed; there is no OS DNS fallback.
    Resolution(ResolveError),
    /// SDK/network/decoder failure, sanitized at this ownership boundary.
    Provider(String),
    /// Provider metadata could not resolve requested symbols completely.
    UnresolvedSymbols {
        /// Symbols not found.
        missing: Vec<String>,
        /// Symbols only partially resolved.
        partial: Vec<String>,
    },
    /// Read attempted after this session was retired.
    Retired,
}

/// Owned data/control event. Diagnostics and mappings must be consumed before
/// deciding readiness; receiving session metadata alone does not establish it.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Event {
    /// Market input copied out of the borrowed decode buffer.
    Market(Observation),
    /// Symbol-to-instrument mapping, scoped to the current lane generation.
    Mapping {
        /// Requested provider symbol.
        requested: String,
        /// Current instrument identity.
        instrument: u32,
        /// Source publisher.
        publisher: u16,
    },
    /// Provider system message, including heartbeats, acknowledgements and warnings.
    System {
        /// Unmodified provider code, including unknown codes.
        code: u8,
        /// Sanitized complete provider text.
        message: String,
    },
    /// Every provider error is surfaced; unknown codes are never silently transient.
    Error {
        /// Unmodified provider error code.
        code: u8,
        /// End-of-error-batch indicator.
        last: bool,
        /// Sanitized complete provider text.
        message: String,
    },
    /// Non-market/control record requiring a later catalog or schema handler.
    Other {
        /// Original record type byte.
        record_type: u8,
    },
}

/// One non-clonable client/socket lifetime. The owner must consume control events
/// and decide health; this transport deliberately does not claim workload readiness.
pub struct Session {
    client: Option<LiveClient>,
    key: String,
    lane: Lane,
}

impl Session {
    /// Resolves, authenticates, subscribes and starts under one original deadline.
    /// Cancelling any partially completed setup drops the entire SDK client.
    /// Connect/auth SDK timers are disabled; heartbeat detection remains separate.
    pub async fn open(
        plan: &SessionPlan,
        key: String,
        gateway: NamedGateway<'_>,
        cancellation: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Self, Failure> {
        plan.validate()?;
        if key.len() != 32 || !key.is_ascii() {
            return Err(Failure::Input("Invalid provider credential format"));
        }
        let setup = async {
            let addresses = gateway
                .resolver
                .resolve(gateway.host, gateway.port, cancellation, deadline)
                .await
                .map_err(|e| match e {
                    ResolveError::Interrupted(reason) => Failure::Interrupted(reason),
                    error => Failure::Resolution(error),
                })?;
            let mut client = LiveClient::builder()
                .addr(addresses.as_slice())
                .await
                .map_err(|e| Failure::Provider(redact(&e.to_string(), &key)))?
                .key(&key)
                .map_err(|e| Failure::Provider(redact(&e.to_string(), &key)))?
                .dataset(plan.lane.dataset())
                // Explicit gateway cadence; SDK silence detection allows an
                // additional ten seconds and counts every valid DBN record.
                .heartbeat_interval(time::Duration::seconds(30))
                .slow_reader_behavior(SlowReaderBehavior::Warn)
                .timeout_conf(TimeoutConf {
                    connect: None,
                    auth: None,
                })
                .build()
                .await
                .map_err(|e| Failure::Provider(redact(&e.to_string(), &key)))?;
            for sub in &plan.subscriptions {
                let mut request = Subscription::builder()
                    .symbols(sub.symbols.clone())
                    .schema(sub.schema)
                    .stype_in(sub.symbology)
                    .build();
                request.start = sub
                    .replay_start_ns
                    .map(|start| time::OffsetDateTime::from_unix_timestamp_nanos(i128::from(start)))
                    .transpose()
                    .map_err(|_| Failure::Input("Invalid replay timestamp"))?;
                client
                    .subscribe(request)
                    .await
                    .map_err(|e| Failure::Provider(redact(&e.to_string(), &key)))?;
            }
            let metadata = client
                .start_with_metadata_decoder(super::metadata::live_decoder)
                .await
                .map_err(|e| Failure::Provider(redact(&e.to_string(), &key)))?;
            if !metadata.not_found.is_empty() || !metadata.partial.is_empty() {
                return Err(Failure::UnresolvedSymbols {
                    missing: metadata
                        .not_found
                        .into_iter()
                        .map(|symbol| redact(&symbol, &key))
                        .collect(),
                    partial: metadata
                        .partial
                        .into_iter()
                        .map(|symbol| redact(&symbol, &key))
                        .collect(),
                });
            }
            if metadata.dataset != plan.lane.dataset() {
                return Err(Failure::Provider(
                    "Gateway metadata dataset mismatch".into(),
                ));
            }
            Ok(Self {
                client: Some(client),
                key: key.clone(),
                lane: plan.lane,
            })
        };
        match deadline
            .run(cancellation, AssertUnwindSafe(setup).catch_unwind())
            .await
        {
            Err(reason) => Err(Failure::Interrupted(reason)),
            Ok(Ok(result)) => result,
            Ok(Err(_)) => Err(Failure::Provider(
                "SDK setup panicked; owned client unwound".into(),
            )),
        }
    }

    /// Lane owned by this exact client lifetime.
    pub fn lane(&self) -> Lane {
        self.lane
    }

    /// Event-driven live read. No new recovery timer starts on every quote.
    /// Interrupted/error reads retire the entire client before returning. Error
    /// records themselves are surfaced to the supervisor for recovery policy.
    pub async fn next(
        &mut self,
        cancellation: &CancellationToken,
        statistics_replay: bool,
        trade_replay: bool,
    ) -> Result<Option<Event>, Failure> {
        let client = self.client.as_mut().ok_or(Failure::Retired)?;
        let read = async {
            let Some(record) = client
                .next_record()
                .await
                .map_err(|e| Failure::Provider(redact(&e.to_string(), &self.key)))?
            else {
                return Ok(None);
            };
            if let Some(error) = record.get::<ErrorMsg>() {
                return Ok(Some(Event::Error {
                    code: error.code,
                    last: error.is_last != 0,
                    message: redact(
                        error.err().map_err(|e| Failure::Provider(e.to_string()))?,
                        &self.key,
                    ),
                }));
            }
            if let Some(system) = record.get::<SystemMsg>() {
                return Ok(Some(Event::System {
                    code: system.code,
                    message: redact(
                        system.msg().map_err(|e| Failure::Provider(e.to_string()))?,
                        &self.key,
                    ),
                }));
            }
            if let Some(mapping) = record.get::<SymbolMappingMsg>() {
                return Ok(Some(Event::Mapping {
                    requested: mapping
                        .stype_in_symbol()
                        .map_err(|e| Failure::Provider(e.to_string()))?
                        .to_owned(),
                    instrument: mapping.hd.instrument_id,
                    publisher: mapping.hd.publisher_id,
                }));
            }
            Ok(Some(
                match records::normalize(record, statistics_replay, trade_replay) {
                    Some(market) => Event::Market(market),
                    None => Event::Other {
                        record_type: record.header().rtype,
                    },
                },
            ))
        };
        let result = tokio::select! { biased;
            () = cancellation.cancelled() => Err(Failure::Interrupted(Interrupted::Cancelled)),
            result = AssertUnwindSafe(read).catch_unwind() => result.unwrap_or_else(|_| Err(Failure::Provider("SDK record decoder panicked; session retired".into()))),
        };
        if !matches!(&result, Ok(Some(_))) {
            self.client.take();
        }
        result
    }
}

fn redact(message: &str, key: &str) -> String {
    message.replace(key, "[REDACTED]")
}
