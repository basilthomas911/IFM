//! Real pinned-SDK stream/recovery fixtures. No provider credentials or live feeds.

use databento::dbn::{
    self, ErrorCode, ErrorMsg, MetadataBuilder, SType, Schema, SymbolMappingMsg, SystemCode,
    SystemMsg, TradeMsg,
    encode::dbn::{AsyncMetadataEncoder, AsyncRecordEncoder},
};
use ifm_market_data_native::{
    dataset::{
        databento::NamedGateway,
        lanes::{Gateway, Lanes, Phase, Status},
        records::Observation,
        transport::{Event, Lane, Session, SessionPlan, SubscriptionSpec},
    },
    dns::Resolver,
    runtime::{Deadline, Interrupted},
};
use std::{
    net::SocketAddr,
    sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    },
    time::{Duration, Instant},
};
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::{TcpListener, TcpStream},
    sync::watch,
};
use tokio_util::sync::CancellationToken;

#[derive(Clone, Copy)]
enum Mode {
    Healthy,
    HealthyHeartbeat,
    FuturesQuotes,
    AckOnly,
    QuotesAckOnly,
    Pricing,
    PricingAt(u64, u64),
    PricingTradeAt(u64),
    PricingTradeReplayAt(u64),
    QuoteFlood,
    DuplicateAck,
    TradeOrdering,
    OptionReplay,
    MetadataBadFirst,
    MetadataAlwaysBad,
    MetadataStall,
    FirstError,
    AlwaysError,
    NoAck,
    HealthyThenNoAck,
    AuthStall,
    ErrorBatch,
    Replay,
    ReplayFlood,
    Flood,
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Real heartbeat-only quiet feed beyond the 40-second silence threshold"]
async fn heartbeat_only_quiet_source_stays_open_without_quotes_and_cancels_cleanly() {
    use ifm_market_data_native::{
        dataset::{
            bootstrap_quotes,
            live::{Evidence, Registration, Start},
        },
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::QuotesAckOnly).await;
    let root = Arc::new(Owner::default());
    let parent = Arc::new(root.child().unwrap());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let source = Start {
        plan: SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions: vec![
                SubscriptionSpec {
                    symbols: vec!["42".into()],
                    schema: Schema::Trades,
                    symbology: SType::InstrumentId,
                    replay_start_ns: None,
                },
                SubscriptionSpec {
                    symbols: vec!["42".into()],
                    schema: Schema::Mbp1,
                    symbology: SType::InstrumentId,
                    replay_start_ns: None,
                },
            ],
        },
        registrations: vec![Registration {
            instrument: 42,
            publisher: 3,
            contract: "ES20261218".into(),
            source: "42".into(),
            asset: Asset::Futures,
            tick: None,
            vwap: false,
            required: true,
        }],
        key: "x".repeat(32),
        gateway: fixture.gateway(),
        evidence: Evidence::Observations,
    };
    let stop = CancellationToken::new();
    let input_stop = stop.clone();
    let input_parent = parent.clone();
    let mut work = tokio::spawn(async move {
        bootstrap_quotes::acquire(
            source,
            date,
            Arc::new(Clock::new(17_931_024_000_000_000, false).unwrap()),
            input_parent,
            &input_stop,
            Deadline::recovery(),
        )
        .await
    });
    assert!(
        tokio::time::timeout(Duration::from_secs(46), &mut work)
            .await
            .is_err(),
        "regular heartbeats must not fail or manufacture a missing quote"
    );
    assert_eq!(
        fixture.accepted.load(Ordering::SeqCst),
        1,
        "quiet periods cannot reconnect"
    );
    assert!(matches!(&*parent.provider_state().borrow(), Some(Ok(()))));
    let started = Instant::now();
    stop.cancel();
    assert!(
        tokio::time::timeout(Duration::from_secs(2), work)
            .await
            .unwrap()
            .unwrap()
            .is_err()
    );
    assert!(started.elapsed() < Duration::from_secs(2));
    fixture.await_closed().await;
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
    drop(parent);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Actual SDK 30-second heartbeat interval plus ten-second silence grace"]
async fn silent_sdk_connection_fails_after_40_seconds_not_35() {
    let fixture = Fixture::new(Mode::Healthy).await;
    let gateway = fixture.gateway();
    let stop = CancellationToken::new();
    let mut session = Session::open(
        &plan(Lane::OptionsGlbx),
        "x".repeat(32),
        NamedGateway {
            host: &gateway.host,
            port: gateway.port,
            resolver: &gateway.resolver,
        },
        &stop,
        Deadline::recovery(),
    )
    .await
    .unwrap();
    let started = Instant::now();
    let error = loop {
        match session.next(&stop, false, false).await {
            Ok(Some(_)) => {}
            Err(error) => break format!("{error:?}"),
            Ok(None) => panic!("Fixture unexpectedly closed before heartbeat silence"),
        }
    };
    assert!(error.contains("heartbeat timeout"), "{error}");
    assert!(started.elapsed() >= Duration::from_secs(40));
    assert!(started.elapsed() < Duration::from_secs(44));
    drop(session);
    fixture.await_closed().await;
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_reviewed_scope_builds_references_manifest_and_risk_without_managed_preparation() {
    native_reviewed_scope_integration(false).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "uses the actual production 120-second native reference deadline"]
async fn native_reference_http_stall_expires_original_120_seconds_and_closes_resources() {
    native_reviewed_scope_integration(true).await;
}
async fn native_reviewed_scope_integration(permanent: bool) {
    use base64::Engine;
    use ifm_market_data_native::{
        dataset::live::{Evidence, Registration, Start},
        latest::Asset,
        messaging::codec,
        option_chain::{
            convention::Convention,
            manifest::Budget,
            owner::Options,
            preparation::{Blueprint, Definition, References, ReviewedScope},
        },
        reference::{
            calendar::Calendar,
            decimal,
            treasury_curve::Treasury,
            treasury_policy::{Conversion, Publication, Publications},
        },
        runtime::{clock::Clock, owner::Owner},
    };
    let oracle: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let at = oracle[0]["at"].as_i64().unwrap() - 12 * 864_000_000_000;
    let bytes = base64::engine::general_purpose::STANDARD
        .decode(oracle[0]["contract"].as_str().unwrap())
        .unwrap();
    let mut value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
    let rmpv::Value::Array(ref mut fields) = value else {
        panic!("contract array")
    };
    fields[4] = 43.into();
    fields[12] = codec::utc_offset(at + 30 * 864_000_000_000).unwrap();
    fields[13] = fields[12].clone();
    let date = time::Date::from_calendar_date(2026, time::Month::October, 15).unwrap();
    let first = date.previous_day().unwrap();
    let until = first + time::Duration::days(59);
    let clock = Arc::new(Clock::new(at, false).unwrap());
    let fixture = Fixture::new(Mode::PricingAt((at as u64) * 100, 864_000_000_000 * 100)).await;
    let http = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let endpoint = format!("http://{}/curve.xml", http.local_addr().unwrap());
    let source_server = tokio::spawn(async move {
        for index in 0..2 {
            let (peer, _) = http.accept().await.unwrap();
            let mut peer = BufReader::new(peer);
            let mut line = String::new();
            loop {
                line.clear();
                peer.read_line(&mut line).await.unwrap();
                if line == "\r\n" {
                    break;
                }
            }
            if permanent && index == 1 {
                let mut byte = [0u8; 1];
                assert_eq!(
                    peer.read(&mut byte).await.unwrap(),
                    0,
                    "native deadline must close the stalled HTTP socket"
                );
                return;
            }
            let body = "<feed xmlns='http://www.w3.org/2005/Atom' xmlns:d='http://schemas.microsoft.com/ado/2007/08/dataservices' xmlns:m='http://schemas.microsoft.com/ado/2007/08/dataservices/metadata'><entry><content><m:properties><d:NEW_DATE>2026-10-14T00:00:00</d:NEW_DATE><d:BC_1MONTH>4.0000</d:BC_1MONTH><d:BC_2MONTH>4.10</d:BC_2MONTH><d:BC_3MONTH>4.2</d:BC_3MONTH></m:properties></content></entry></feed>";
            let body = if index == 0 {
                body.to_owned()
            } else {
                body.replace("2026-10-14", "2026-10-15")
            };
            peer.get_mut()
                .write_all(
                    format!(
                        "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",
                        body.len()
                    )
                    .as_bytes(),
                )
                .await
                .unwrap();
        }
    });
    let resolver = Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap());
    let references = Arc::new(References::new(
        Arc::new(Treasury::new(&endpoint, resolver, clock.clone()).unwrap()),
        clock.clone(),
    ));
    let manifests = Budget::new(16 * 1024 * 1024).unwrap();
    let reviewed = Arc::new(ReviewedScope {
        manifests: manifests.clone(),
        references,
        definitions: vec![Definition {
            instrument: 43,
            underlying: 42,
            strike: decimal::parse("6000").unwrap(),
            call: true,
            required: true,
            blueprint: Blueprint {
                contract: Convention::new(value).unwrap(),
                calendar: Calendar::new(
                    "fixture/v1".into(),
                    "Eastern Standard Time",
                    first,
                    until,
                    648_000_000_000,
                    (0..60).map(|i| first + time::Duration::days(i)).collect(),
                )
                .unwrap(),
                publications: Publications::new(
                    "publication/v1".into(),
                    at - 864_000_000_000,
                    at + 2 * 864_000_000_000,
                    vec![Publication {
                        date: first,
                        available: at - 864_000_000_000,
                    }],
                    "Eastern Standard Time",
                )
                .unwrap(),
                conversion: Conversion::standard(),
                maximum_lead: 0,
            },
        }],
        production: false,
        production_approved: false,
        review_evidence: None,
        policy_version: "OptionRefresh/TestDefaults/v1".into(),
        selection_ms: 10,
        iv_ms: 5000,
        risk_ms: 20,
        maximum_contracts: 2048,
        batch: 16,
        statistics: Vec::new(),
    });
    let root = Arc::new(Owner::default());
    let options = Options::new(date, 1, clock.clone(), root.clone()).unwrap();
    let request = || Start {
        plan: SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions: vec![
                SubscriptionSpec {
                    symbols: vec!["42".into(), "43".into()],
                    schema: Schema::Trades,
                    symbology: SType::InstrumentId,
                    replay_start_ns: None,
                },
                SubscriptionSpec {
                    symbols: vec!["42".into(), "43".into()],
                    schema: Schema::Mbp1,
                    symbology: SType::InstrumentId,
                    replay_start_ns: None,
                },
            ],
        },
        registrations: vec![
            Registration {
                publisher: 3,
                instrument: 42,
                contract: "ES20261218".into(),
                source: "42".into(),
                asset: Asset::Futures,
                tick: None,
                vwap: false,
                required: true,
            },
            Registration {
                publisher: 3,
                instrument: 43,
                contract: "ES20261110C6000".into(),
                source: "43".into(),
                asset: Asset::Option,
                tick: None,
                vwap: false,
                required: true,
            },
        ],
        key: "x".repeat(32),
        gateway: fixture.gateway(),
        evidence: Evidence::Observations,
    };
    let stop = CancellationToken::new();
    let digest = "a".repeat(64);
    let startup = options.install_reviewed(
        "reviewed",
        &digest,
        request(),
        reviewed.clone(),
        &stop,
        Deadline::after(Duration::from_secs(2)),
    );
    assert!(
        std::mem::size_of_val(&startup) <= 32768,
        "cold native startup future must remain bounded on smaller managed caller stacks"
    );
    startup.await.unwrap();
    let engine = options.pricing("reviewed").unwrap().unwrap();
    let mut updates = engine.updates();
    tokio::time::timeout(Duration::from_secs(2), async {
        while !engine.ready(at).unwrap() {
            updates.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    let risk = engine.read(43, true, at).unwrap().unwrap();
    assert!(risk.risk.is_some());
    assert!(risk.failure.is_none());
    assert!(options.reviewed("reviewed").unwrap().is_some());
    let physical = options
        .scope("reviewed")
        .unwrap()
        .unwrap()
        .generation(Lane::OptionsGlbx)
        .unwrap()
        .unwrap();
    let captured = engine.trade_context(43, physical).unwrap().unwrap();
    let manifest = captured.manifest.unwrap();
    assert!(!manifest.bytes().is_empty());
    let encoded = rmpv::decode::read_value(&mut manifest.bytes()).unwrap();
    assert!(encoded.is_array());
    assert!(manifests.used() > 0);
    let old_identity = captured.context.identity;
    let next_at = at + 864_000_000_000;
    let recovery_started = Instant::now();
    clock.update(next_at, false).unwrap();
    if permanent {
        tokio::time::timeout(Duration::from_secs(124),async {
            let data=options.scope("reviewed").unwrap().unwrap();let mut signals=data.updates();
            loop {
                if options.status("reviewed").unwrap().is_some_and(|status|status.phase==Phase::Failed){break;}
                tokio::select!{_ = signals.changed()=>{},_ = updates.changed()=>{},_ = tokio::time::sleep(Duration::from_millis(20))=>{}}
            }
        }).await.unwrap();
        assert!(recovery_started.elapsed() >= Duration::from_secs(119));
        assert!(recovery_started.elapsed() < Duration::from_secs(124));
        assert!(
            engine
                .reference_deadline()
                .unwrap()
                .unwrap()
                .remaining()
                .is_zero()
        );
        let status = options.status("reviewed").unwrap().unwrap();
        assert!(
            status
                .error
                .as_deref()
                .is_some_and(|s| s.contains("deadline expired")),
            "{status:?}"
        );
    } else {
        tokio::time::timeout(Duration::from_secs(3), async {
            loop {
                if engine.context_identities().unwrap().get(&43) != Some(&old_identity)
                    && engine.ready(next_at).unwrap()
                    && engine.reference_deadline().unwrap().is_none()
                    && options
                        .status("reviewed")
                        .unwrap()
                        .is_some_and(|s| s.phase == Phase::Running)
                {
                    break;
                }
                updates.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
    }
    source_server.await.unwrap();
    assert!(
        fixture.accepted.load(Ordering::Acquire) >= 2,
        "native reference rollover must restore fresh physical source observations"
    );
    assert!(
        !manifest.bytes().is_empty(),
        "old source evidence retains its original context during native refresh"
    );
    // Detached copied context/evidence can outlive socket cleanup without owning
    // a provider task, lease, FFI callback or dataset admission gate.
    let detached = codec::encode(&reviewed.definitions[0].blueprint.contract.value()).unwrap();
    options
        .remove("reviewed", &stop, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    fixture.await_closed().await;
    assert!(!detached.is_empty());
    options
        .shutdown(&stop, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    drop(options);
    drop(engine);
    drop(captured.context);
    drop(manifest);
    drop(reviewed);
    assert_eq!(manifests.used(), 0);
    root.stop().unwrap();
    root.wait_idle(&stop, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
}

struct Fixture {
    address: SocketAddr,
    shutdown: CancellationToken,
    task: tokio::task::JoinHandle<()>,
    accepted: Arc<AtomicUsize>,
    accepted_notice: Arc<tokio::sync::Notify>,
    closed: Arc<AtomicUsize>,
    closed_notice: Arc<tokio::sync::Notify>,
}
impl Fixture {
    async fn new(mode: Mode) -> Self {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let address = listener.local_addr().unwrap();
        let shutdown = CancellationToken::new();
        let stop = shutdown.clone();
        let accepted = Arc::new(AtomicUsize::new(0));
        let accepted_notice = Arc::new(tokio::sync::Notify::new());
        let accepted_signal = accepted_notice.clone();
        let closed = Arc::new(AtomicUsize::new(0));
        let closed_notice = Arc::new(tokio::sync::Notify::new());
        let notice = closed_notice.clone();
        let (seen, ended) = (accepted.clone(), closed.clone());
        let task = tokio::spawn(async move {
            let mut clients = tokio::task::JoinSet::new();
            loop {
                tokio::select! {
                    () = stop.cancelled() => break,
                    accepted = listener.accept() => {
                        let (socket, _) = accepted.unwrap();
                        let attempt = seen.fetch_add(1, Ordering::SeqCst);
                        accepted_signal.notify_one();
                        let ended = ended.clone();
                        let notice = notice.clone();
                        clients.spawn(async move { serve(socket, mode, attempt).await; ended.fetch_add(1, Ordering::SeqCst); notice.notify_one(); });
                    }
                    Some(result) = clients.join_next(), if !clients.is_empty() => { result.unwrap(); }
                }
            }
            clients.abort_all();
            while let Some(result) = clients.join_next().await {
                assert!(result.is_ok() || result.unwrap_err().is_cancelled());
            }
        });
        Self {
            address,
            shutdown,
            task,
            accepted,
            accepted_notice,
            closed,
            closed_notice,
        }
    }
    fn gateway(&self) -> Gateway {
        Gateway {
            host: self.address.ip().to_string(),
            port: self.address.port(),
            resolver: Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
        }
    }
    async fn stop(self) {
        self.shutdown.cancel();
        self.task.await.unwrap();
    }
    async fn await_closed(&self) {
        tokio::time::timeout(Duration::from_secs(2), async {
            loop {
                let changed = self.closed_notice.notified();
                if self.closed.load(Ordering::SeqCst) == self.accepted.load(Ordering::SeqCst) {
                    break;
                }
                changed.await;
            }
        })
        .await
        .unwrap();
    }
}

async fn serve(socket: TcpStream, mode: Mode, attempt: usize) {
    let timestamp = if let Mode::PricingTradeAt(at) | Mode::PricingTradeReplayAt(at) = mode {
        at
    } else if let Mode::PricingAt(at, step) = mode {
        at + if attempt > 0 { step } else { 0 }
    } else {
        0
    };
    let mut peer = BufReader::new(socket);
    if matches!(mode, Mode::AuthStall) {
        let mut byte = [0];
        let _ = peer.read(&mut byte).await;
        return;
    }
    peer.get_mut()
        .write_all(b"lsg-test\ncram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n")
        .await
        .unwrap();
    let mut line = String::new();
    if peer.read_line(&mut line).await.unwrap_or(0) == 0 || !line.ends_with('\n') {
        return;
    }
    let dataset = if line.contains("dataset=XCBF.PITCH") {
        "XCBF.PITCH"
    } else {
        assert!(line.contains("dataset=GLBX.MDP3"));
        "GLBX.MDP3"
    };
    peer.get_mut()
        .write_all(b"success=1|session_id=5\n")
        .await
        .unwrap();
    line.clear();
    if peer.read_line(&mut line).await.unwrap_or(0) == 0 || !line.ends_with('\n') {
        return;
    }
    assert!(line.contains("schema=trades"));
    let mut quote_replay=false;
    if matches!(
        mode,
        Mode::FuturesQuotes
            | Mode::QuotesAckOnly
            | Mode::Pricing
            | Mode::PricingAt(_, _)
            | Mode::PricingTradeAt(_)
            | Mode::PricingTradeReplayAt(_)
            | Mode::OptionReplay
            | Mode::QuoteFlood
            | Mode::DuplicateAck
            | Mode::TradeOrdering
    ) {
        line.clear();
        if peer.read_line(&mut line).await.unwrap_or(0) == 0 {
            return;
        }
        assert!(line.contains("schema=mbp-1"));
        quote_replay=line.contains("start=");
        if matches!(mode, Mode::PricingTradeReplayAt(_)) && attempt == 0 {
            // There will be no later live quote changes: the initial book is
            // supplied only when the client explicitly requests session replay.
            assert!(line.contains("start=1793052000000000000"), "quiet underlying bootstrap must use the API ValueDate 2026-10-27 session start: {line}");
        }
        if matches!(mode, Mode::OptionReplay) {
            line.clear();
            if peer.read_line(&mut line).await.unwrap_or(0) == 0 {
                return;
            }
            assert!(line.contains("schema=statistics"));
        }
    }
    line.clear();
    if peer.read_line(&mut line).await.unwrap_or(0) == 0 || !line.ends_with('\n') {
        return;
    }
    let additional_statistics = line.contains("schema=statistics");
    if additional_statistics {
        line.clear();
        if peer.read_line(&mut line).await.unwrap_or(0) == 0 {
            return;
        }
    }
    assert_eq!(line, "start_session\n");
    if matches!(mode, Mode::MetadataStall | Mode::MetadataAlwaysBad)
        || (matches!(mode, Mode::MetadataBadFirst) && attempt == 0)
    {
        let mut header = b"DBN\x03".to_vec();
        header.extend_from_slice(&u32::MAX.to_le_bytes());
        if !matches!(mode, Mode::MetadataStall) {
            // Invalid schema. Reject before trusting wire length or allocating
            // mappings. Keep socket open to prove client owns retirement.
            header.extend_from_slice(&[0; 16]);
            header.extend_from_slice(&65534_u16.to_le_bytes());
        }
        peer.get_mut().write_all(&header).await.unwrap();
        let mut byte = [0];
        match peer.read(&mut byte).await {
            Ok(0) => {}
            Err(error)
                if matches!(
                    error.kind(),
                    std::io::ErrorKind::ConnectionReset | std::io::ErrorKind::ConnectionAborted
                ) => {}
            other => panic!("Expected retired metadata socket, got {other:?}"),
        }
        return;
    }
    AsyncMetadataEncoder::new(peer.get_mut())
        .encode(
            &MetadataBuilder::new()
                .dataset(dataset)
                .start(0)
                .schema(None)
                .stype_in(None)
                .stype_out(SType::InstrumentId)
                .build(),
        )
        .await
        .unwrap();
    let mut records = AsyncRecordEncoder::new(peer.get_mut());
    records
        .encode(
            &SymbolMappingMsg::new(
                42,
                10,
                SType::RawSymbol,
                "ESZ6",
                SType::RawSymbol,
                "ESZ6",
                0,
                u64::MAX,
            )
            .unwrap(),
        )
        .await
        .unwrap();
    if !matches!(mode, Mode::NoAck)
        && !(matches!(mode, Mode::HealthyThenNoAck | Mode::Flood) && attempt > 0)
    {
        records
            .encode(
                &SystemMsg::new(11, Some(SystemCode::SubscriptionAck), "subscription_id=1")
                    .unwrap(),
            )
            .await
            .unwrap();
        if matches!(
            mode,
            Mode::FuturesQuotes
                | Mode::QuotesAckOnly
                | Mode::Pricing
                | Mode::PricingAt(_, _)
                | Mode::PricingTradeAt(_)
            | Mode::PricingTradeReplayAt(_)
                | Mode::OptionReplay
                | Mode::QuoteFlood
                | Mode::DuplicateAck
                | Mode::TradeOrdering
        ) {
            records
                .encode(
                    &SystemMsg::new(
                        11,
                        Some(SystemCode::SubscriptionAck),
                        if matches!(mode, Mode::DuplicateAck) {
                            "subscription_id=1"
                        } else {
                            "subscription_id=2"
                        },
                    )
                    .unwrap(),
                )
                .await
                .unwrap();
            if matches!(mode, Mode::OptionReplay) || additional_statistics {
                records
                    .encode(
                        &SystemMsg::new(11, Some(SystemCode::SubscriptionAck), "subscription_id=3")
                            .unwrap(),
                    )
                    .await
                    .unwrap();
            }
        }
    }
    if matches!(mode, Mode::AlwaysError | Mode::ErrorBatch)
        || (matches!(mode, Mode::FirstError) && attempt == 0)
    {
        let mut error = ErrorMsg::new(
            12,
            Some(ErrorCode::InternalError),
            &format!("test error {}", "x".repeat(32)),
            !matches!(mode, Mode::ErrorBatch),
        );
        if matches!(mode, Mode::AlwaysError | Mode::ErrorBatch) {
            error.code = 199;
        }
        records.encode(&error).await.unwrap();
        if matches!(mode, Mode::ErrorBatch) {
            records
                .encode(&ErrorMsg::new(
                    13,
                    Some(ErrorCode::InvalidSubscription),
                    "second error",
                    true,
                ))
                .await
                .unwrap();
        }
    } else if !matches!(mode, Mode::AckOnly | Mode::QuotesAckOnly) {
        records
            .encode(
                &SystemMsg::new(
                    12,
                    Some(SystemCode::SlowReaderWarning),
                    "slow-reader advisory",
                )
                .unwrap(),
            )
            .await
            .unwrap();
        let mut trade = TradeMsg {
            price: 6_000_250_000_000,
            size: 4,
            sequence: 7,
            ts_recv: 15,
            action: b'T' as _,
            side: b'B' as _,
            ..TradeMsg::default()
        };
        trade.hd.instrument_id = 42;
        trade.hd.publisher_id = 3;
        trade.hd.ts_event = timestamp + 14;
        trade.ts_recv = timestamp + 15;
        records.encode(&trade).await.unwrap();
        if matches!(
            mode,
            Mode::Pricing
                | Mode::PricingAt(_, _)
                | Mode::PricingTradeAt(_)
            | Mode::PricingTradeReplayAt(_)
                | Mode::OptionReplay
                | Mode::QuoteFlood
                | Mode::DuplicateAck
                | Mode::TradeOrdering
        ) {
            for (instrument, bid, ask) in [
                (42, 6_000_000_000_000, 6_000_500_000_000),
                (43, 99_750_000_000, 100_250_000_000),
            ] {
                if matches!(mode, Mode::PricingTradeAt(_) | Mode::PricingTradeReplayAt(_)) && attempt > 0 && instrument == 43 {
                    // Deterministically expose the replacement's quote gap:
                    // subscription acknowledgements/underlying data are live,
                    // but optional cache capture must wait for the option quote.
                    tokio::time::sleep(Duration::from_millis(40)).await;
                }
                let mut quote = databento::dbn::Mbp1Msg {
                    sequence: 8,
                    ts_recv: 15,
                    ..Default::default()
                };
                quote.hd.instrument_id = instrument;
                quote.hd.publisher_id = 3;
                quote.hd.ts_event = timestamp + 14;
                quote.ts_recv = timestamp + 15;
                quote.levels[0].bid_px = bid;
                quote.levels[0].ask_px = ask;
                quote.levels[0].bid_sz = 10;
                quote.levels[0].ask_sz = 10;
                records.encode(&quote).await.unwrap();
            }
            if matches!(mode, Mode::PricingTradeAt(_) | Mode::PricingTradeReplayAt(_)) {
                trade.hd.instrument_id = 43;
                trade.price = 100_000_000_000;
                trade.hd.ts_event = timestamp + 16;
                trade.ts_recv = timestamp + 17;
                trade.sequence = 9;
                trade.size = 1;
                records.encode(&trade).await.unwrap();
                trade.hd.instrument_id = 42;
            }
            if additional_statistics {
                for message in ["Replay completed trades", "Replay completed statistics"] {
                    records
                        .encode(
                            &SystemMsg::new(
                                timestamp + 18,
                                Some(SystemCode::ReplayCompleted),
                                message,
                            )
                            .unwrap(),
                        )
                        .await
                        .unwrap();
                }
            }
            if additional_statistics && matches!(mode, Mode::PricingTradeAt(_) | Mode::PricingTradeReplayAt(_)) {
                trade.hd.instrument_id = 43;
                trade.hd.ts_event = timestamp + 19;
                trade.ts_recv = timestamp + 20;
                trade.sequence = 10;
                trade.size = 1;
                records.encode(&trade).await.unwrap();
                trade.hd.instrument_id = 42;
            }
            if matches!(mode, Mode::OptionReplay) {
                trade.hd.instrument_id = 43;
                trade.price = 100_000_000_000;
                for (event, sequence, size) in [(20, 12, 3), (30, 13, 4)] {
                    trade.hd.ts_event = event;
                    trade.sequence = sequence;
                    trade.size = size;
                    records.encode(&trade).await.unwrap();
                }
                records
                    .encode(
                        &SystemMsg::new(
                            35,
                            Some(SystemCode::ReplayCompleted),
                            "Replay completed trades",
                        )
                        .unwrap(),
                    )
                    .await
                    .unwrap();
                trade.hd.ts_event = 40;
                trade.sequence = 14;
                trade.size = 5;
                trade.price = 101_000_000_000;
                records.encode(&trade).await.unwrap();
                let reference = time::Date::from_calendar_date(2026, time::Month::October, 26)
                    .unwrap()
                    .with_hms(0, 0, 0)
                    .unwrap()
                    .assume_utc()
                    .unix_timestamp_nanos() as u64;
                for (kind, quantity, date) in
                    [(6, 9000, reference), (9, 666, reference - 86400000000000)]
                {
                    let mut stat = databento::dbn::StatMsg {
                        stat_type: kind,
                        quantity,
                        ts_ref: date,
                        update_action: 1,
                        ..Default::default()
                    };
                    stat.hd.instrument_id = 43;
                    stat.hd.publisher_id = 3;
                    stat.hd.ts_event = 50;
                    records.encode(&stat).await.unwrap();
                }
            }
        }
        if matches!(mode, Mode::TradeOrdering) {
            trade.hd.instrument_id = 43;
            trade.hd.ts_event = 18;
            trade.ts_recv = 19;
            trade.sequence = 9;
            trade.price = 100_000_000_000;
            records.encode(&trade).await.unwrap();
            let mut quote = databento::dbn::Mbp1Msg {
                sequence: 10,
                ts_recv: 21,
                ..Default::default()
            };
            quote.hd.instrument_id = 42;
            quote.hd.ts_event = 20;
            quote.levels[0].bid_px = 6_100_000_000_000;
            quote.levels[0].ask_px = 6_100_500_000_000;
            quote.levels[0].bid_sz = 10;
            quote.levels[0].ask_sz = 10;
            records.encode(&quote).await.unwrap();
        }
        if matches!(mode, Mode::FuturesQuotes) {
            let mut quote = databento::dbn::Mbp1Msg {
                ts_recv: 21,
                sequence: 10,
                ..Default::default()
            };
            quote.hd.instrument_id = 42;
            quote.hd.publisher_id = 3;
            quote.hd.ts_event = 20;
            quote.levels[0].bid_px = 6_100_000_000_000;
            quote.levels[0].ask_px = 6_100_500_000_000;
            quote.levels[0].bid_sz = 10;
            quote.levels[0].ask_sz = 10;
            records.encode(&quote).await.unwrap();
        }
        if matches!(mode, Mode::QuoteFlood) {
            let mut quote = databento::dbn::Mbp1Msg::default();
            quote.hd.instrument_id = 43;
            quote.levels[0].bid_px = 99_750_000_000;
            quote.levels[0].ask_px = 100_250_000_000;
            quote.levels[0].bid_sz = 10;
            quote.levels[0].ask_sz = 10;
            for sequence in 9..=20_000 {
                quote.sequence = sequence;
                quote.hd.ts_event = u64::from(sequence);
                if records.encode(&quote).await.is_err() {
                    return;
                }
            }
        }
        if matches!(mode, Mode::Replay | Mode::ReplayFlood) {
            if matches!(mode, Mode::ReplayFlood) {
                for sequence in 8..20_008 {
                    trade.sequence = sequence;
                    trade.hd.ts_event = u64::from(sequence) + 100;
                    if records.encode(&trade).await.is_err() {
                        return;
                    }
                }
            }
            records
                .encode(
                    &SystemMsg::new(
                        16,
                        Some(SystemCode::ReplayCompleted),
                        "Replay completed trades",
                    )
                    .unwrap(),
                )
                .await
                .unwrap();
            trade.sequence = if matches!(mode, Mode::ReplayFlood) {
                20_008
            } else {
                8
            };
            trade.size = 6;
            trade.hd.ts_event = if matches!(mode, Mode::ReplayFlood) {
                20_114
            } else {
                114
            };
            records.encode(&trade).await.unwrap();
        }
        if matches!(mode, Mode::Flood) && attempt == 0 {
            for sequence in 8..9010 {
                trade.sequence = sequence;
                if records.encode(&trade).await.is_err() {
                    return;
                }
            }
        }
    }
    if quote_replay {
        records.encode(&SystemMsg::new(timestamp+30,Some(SystemCode::ReplayCompleted),"Finished mbp-1 replay").unwrap()).await.unwrap();
    }
    if matches!(
        mode,
        Mode::HealthyHeartbeat
            | Mode::FuturesQuotes
            | Mode::AckOnly
            | Mode::QuotesAckOnly
            | Mode::Pricing
            | Mode::PricingAt(_, _)
            | Mode::PricingTradeAt(_)
            | Mode::PricingTradeReplayAt(_)
            | Mode::OptionReplay
            | Mode::QuoteFlood
            | Mode::DuplicateAck
            | Mode::TradeOrdering
    ) {
        // A healthy live provider sends heartbeat records during quiet markets.
        // Without these the SDK correctly starts independent heartbeat recovery,
        // which would confound a pricing-only reconnect-count assertion.
        let (mut reader, mut writer) = peer.into_inner().into_split();
        let mut timer = tokio::time::interval(Duration::from_secs(5));
        let mut byte = [0];
        loop {
            tokio::select! {
                read=reader.read(&mut byte)=>{assert!(matches!(read,Ok(0)|Err(_)));return;}
                _=timer.tick()=>{
                    if AsyncRecordEncoder::new(&mut writer).encode(&SystemMsg::new(100,Some(SystemCode::Heartbeat),"Heartbeat").unwrap()).await.is_err(){return;}
                }
            }
        }
    }
    let mut byte = [0];
    let closed = match peer.read(&mut byte).await {
        Ok(0) => true,
        Err(error) => matches!(
            error.kind(),
            std::io::ErrorKind::ConnectionReset | std::io::ErrorKind::ConnectionAborted
        ),
        _ => false,
    };
    assert!(closed, "owner did not close its fixture socket");
}

fn plan(lane: Lane) -> SessionPlan {
    SessionPlan {
        lane,
        subscriptions: vec![SubscriptionSpec {
            symbols: vec!["ESZ6".into()],
            symbology: SType::RawSymbol,
            schema: Schema::Trades,
            replay_start_ns: None,
        }],
    }
}

/// Actual SDK records reach native coalesced ingress before any managed relay.
/// One tracked pricing task shares the option owner and retires with its sockets.
#[tokio::test]
async fn sdk_option_quotes_drive_native_pricing_and_reset_fences_previous_calculations() {
    native_pricing_integration(false).await;
}
#[tokio::test]
#[ignore = "real two-minute native pricing supervisor deadline qualification"]
async fn real_120_second_pricing_fault_expires_without_renewal_and_closes_provider() {
    native_pricing_integration(true).await;
}
async fn native_pricing_integration(permanent: bool) {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        option_chain::{
            coalesced::{Coalesced, Context, underlying_quote},
            pipeline::PricingTask,
            qualification::{Decimal, Request},
        },
        pricing::Request as Model,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Pricing).await;
    let root = Arc::new(Owner::default());
    let parent = root.child().unwrap();
    let clock = Arc::new(Clock::new(0, false).unwrap());
    let pricing = Arc::new(Coalesced::new(8, 16, 5000).unwrap());
    let strike = Decimal {
        bits: [6000, 0, 0, 0],
    };
    pricing
        .register(
            43,
            1,
            Context {
                identity: 11,
                underlying: 42,
                qualification: Request {
                    size: 304,
                    flags: 3 | 4 | 8 | 16,
                    generation_low: 1,
                    maximum_age: 5000,
                    maximum_skew: 2000,
                    strike,
                    definition_strike: strike,
                    ..Default::default()
                },
                model: Model {
                    forward: 0.0,
                    strike: 6000.0,
                    time: 0.0,
                    rate: 0.04,
                    call: true,
                    american: false,
                    futures_style: false,
                },
                expiration: 30 * 864000000000,
                days_in_year: 365,
                valid_until: 864000000000,
            },
            true,
        )
        .unwrap();
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = Arc::new(
        LiveData::new(date, 1, 8, clock.clone(), root.clone())
            .unwrap()
            .with_option_pricing(pricing.clone())
            .unwrap(),
    );
    let subscriptions = vec![
        SubscriptionSpec {
            symbols: vec!["42".into(), "43".into()],
            schema: Schema::Trades,
            symbology: SType::InstrumentId,
            replay_start_ns: None,
        },
        SubscriptionSpec {
            symbols: vec!["42".into(), "43".into()],
            schema: Schema::Mbp1,
            symbology: SType::InstrumentId,
            replay_start_ns: None,
        },
    ];
    let registrations = vec![
        Registration {
            publisher: 0,
            instrument: 42,
            contract: "ES20261218".into(),
            source: "42".into(),
            asset: Asset::Futures,
            tick: None,
            vwap: false,
            required: true,
        },
        Registration {
            publisher: 0,
            instrument: 43,
            contract: "ES20261218C6000".into(),
            source: "43".into(),
            asset: Asset::Option,
            tick: None,
            vwap: false,
            required: true,
        },
    ];
    data.start_nested(
        Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions,
            },
            registrations,
            key: "x".repeat(32),
            gateway: fixture.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::recovery(),
        &parent,
    )
    .await
    .unwrap();
    let old = data.generation(Lane::OptionsGlbx).unwrap().unwrap();
    let owned = data.clone();
    let fault = Arc::new(std::sync::atomic::AtomicBool::new(false));
    let fail = fault.clone();
    let worker = PricingTask::start_with_reader(
        data.clone(),
        pricing.clone(),
        clock,
        10,
        20,
        &parent,
        move |id| {
            if if permanent {
                fail.load(Ordering::SeqCst)
            } else {
                fail.swap(false, Ordering::SeqCst)
            } {
                return Err("Injected pricing input exception");
            }
            owned
                .raw_quote(Lane::OptionsGlbx, id)
                .map(|tick| tick.and_then(|quote| underlying_quote(quote, 1)))
                .map_err(|_| "Underlying book failed")
        },
    )
    .unwrap();
    async fn risk(pricing: &Coalesced, generation: u128) {
        tokio::time::timeout(Duration::from_secs(2), async {
            loop {
                if let Some(value) = pricing.read(43, true, 0).unwrap()
                    && value.source_generation == generation
                    && value.risk.is_some()
                {
                    break;
                }
                tokio::task::yield_now().await;
            }
        })
        .await
        .unwrap();
    }
    risk(&pricing, old).await;
    assert_eq!(
        pricing.read(43, true, 0).unwrap().unwrap().option.sequence,
        8
    );
    data.reset(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    let fresh = data.generation(Lane::OptionsGlbx).unwrap().unwrap();
    assert_ne!(old, fresh);
    risk(&pricing, fresh).await;
    assert_eq!(pricing.counters().unwrap().0, 2);
    let incident = Instant::now();
    fault.store(true, Ordering::SeqCst);
    if permanent {
        tokio::time::timeout(Duration::from_secs(123), async {
            while worker.status().unwrap().running {
                tokio::time::sleep(Duration::from_millis(10)).await;
            }
        })
        .await
        .unwrap();
        let elapsed = incident.elapsed();
        let status = worker.status().unwrap();
        assert!(elapsed >= Duration::from_millis(119900));
        assert!(elapsed < Duration::from_secs(123));
        let detail = status.error.unwrap();
        assert!(detail.contains("two-minute deadline expired"));
        assert!(detail.contains("Injected pricing input exception"));
        assert_eq!(
            fixture.accepted.load(Ordering::SeqCst),
            3,
            "one provider reset per pricing incident; repeated calculation faults must not hammer the gateway"
        );
        worker
            .stop(&CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
        root.stop().unwrap();
        drop(parent);
        root.wait_idle(&CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
        tokio::time::timeout(Duration::from_secs(1), async {
            while fixture.closed.load(Ordering::SeqCst) != fixture.accepted.load(Ordering::SeqCst) {
                tokio::task::yield_now().await;
            }
        })
        .await
        .unwrap();
        eprintln!(
            "pricing recovery terminal at {elapsed:?}; closed={} accepted={}",
            fixture.closed.load(Ordering::SeqCst),
            fixture.accepted.load(Ordering::SeqCst)
        );
        assert!(root.disposable().unwrap());
        fixture.stop().await;
        return;
    }
    tokio::time::timeout(Duration::from_secs(3), async {
        loop {
            let generation = data.generation(Lane::OptionsGlbx).unwrap();
            let status = worker.status().unwrap();
            if generation.is_some_and(|generation| generation != fresh)
                && status.running
                && !status.recovering
                && status.error.is_none()
                && pricing.ready(0).unwrap()
            {
                break;
            }
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 3);
    data.stop(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert!(pricing.read(43, true, 0).unwrap().is_none());
    root.stop().unwrap();
    worker
        .stop(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    drop(parent);
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
}

#[tokio::test]
async fn asynchronous_pricing_fault_wakes_idle_session_and_uses_original_recovery_deadline() {
    for mode in [Mode::Healthy, Mode::HealthyThenNoAck] {
        let fixture = Fixture::new(mode).await;
        let mut lanes = Lanes::default();
        let mut status = lanes
            .start_with_budget(
                plan(Lane::OptionsGlbx),
                "x".repeat(32),
                fixture.gateway(),
                |_, _| Ok(true),
                Duration::from_secs(2),
            )
            .unwrap();
        let before = status_where(&mut status, |s| s.phase == Phase::Running).await;
        let started = Instant::now();
        let deadline = Deadline::after(Duration::from_millis(350));
        lanes
            .report_fault(
                Lane::OptionsGlbx,
                "pricing loop unexpectedly exited",
                deadline,
            )
            .unwrap();
        if matches!(mode, Mode::Healthy) {
            let after = status_where(&mut status, |s| {
                s.phase == Phase::Running && s.generation > before.generation
            })
            .await;
            assert!(after.error.is_none());
            assert!(started.elapsed() < Duration::from_millis(350));
        } else {
            status_where(&mut status, |s| s.phase == Phase::Recovering).await;
            tokio::time::sleep(Duration::from_millis(100)).await;
            // A later external fault cannot give this incident another budget.
            lanes
                .report_fault(
                    Lane::OptionsGlbx,
                    "pricing retry still unavailable",
                    Deadline::after(Duration::from_secs(2)),
                )
                .unwrap();
            let after = status_where(&mut status, |s| s.phase == Phase::Failed).await;
            assert!(started.elapsed() < Duration::from_millis(800));
            assert!(after.error.unwrap().contains("pricing"));
        }
        lanes.shutdown(Deadline::recovery()).await.unwrap();
        fixture.stop().await;
    }
}

#[tokio::test]
async fn qualified_option_owner_installs_pricing_before_ingress_and_joins_both_workers_on_release()
{
    use ifm_market_data_native::{
        dataset::live::{Evidence, Registration, Start},
        latest::Asset,
        option_chain::{
            coalesced::Context,
            owner::{Options, PricingConfiguration, PricingRegistration},
            qualification::{Decimal, Request},
        },
        pricing::Request as Model,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Pricing).await;
    let root = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let options = Options::new(
        date,
        1,
        Arc::new(Clock::new(0, false).unwrap()),
        root.clone(),
    )
    .unwrap();
    let logical = options.generation();
    let request = || Start {
        plan: SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions: vec![
                SubscriptionSpec {
                    symbols: vec!["42".into(), "43".into()],
                    schema: Schema::Trades,
                    symbology: SType::InstrumentId,
                    replay_start_ns: None,
                },
                SubscriptionSpec {
                    symbols: vec!["42".into(), "43".into()],
                    schema: Schema::Mbp1,
                    symbology: SType::InstrumentId,
                    replay_start_ns: None,
                },
            ],
        },
        registrations: vec![
            Registration {
                publisher: 0,
                instrument: 42,
                contract: "ES20261218".into(),
                source: "42".into(),
                asset: Asset::Futures,
                tick: None,
                vwap: false,
                required: true,
            },
            Registration {
                publisher: 0,
                instrument: 43,
                contract: "ES20261218C6000".into(),
                source: "43".into(),
                asset: Asset::Option,
                tick: None,
                vwap: false,
                required: true,
            },
        ],
        key: "x".repeat(32),
        gateway: fixture.gateway(),
        evidence: Evidence::Observations,
    };
    let configuration = |generation: u128| {
        let strike = Decimal {
            bits: [6000, 0, 0, 0],
        };
        PricingConfiguration {
            digest: "b".repeat(64),
            policy_version: "OptionRefresh/TestDefaults/v1".into(),
            statistics: Vec::new(),
            selection_ms: 10,
            risk_ms: 20,
            iv_ms: 5000,
            batch: 16,
            options: vec![PricingRegistration {
                manifest: None,
                instrument: 43,
                required: true,
                context: Context {
                    identity: 11,
                    underlying: 42,
                    qualification: Request {
                        size: 304,
                        flags: 3 | 4 | 8 | 16,
                        generation_low: generation as u64,
                        generation_high: (generation >> 64) as u64,
                        maximum_age: 5000,
                        maximum_skew: 2000,
                        strike,
                        definition_strike: strike,
                        ..Default::default()
                    },
                    model: Model {
                        forward: 0.0,
                        strike: 6000.0,
                        time: 0.0,
                        rate: 0.04,
                        call: true,
                        american: false,
                        futures_style: false,
                    },
                    expiration: 30 * 864000000000,
                    days_in_year: 365,
                    valid_until: 864000000000,
                },
            }],
        }
    };
    let token = CancellationToken::new();
    let digest = "a".repeat(64);
    assert!(
        options
            .install_priced(
                "priced",
                &digest,
                request(),
                configuration(0),
                &token,
                Deadline::recovery()
            )
            .await
            .is_err()
    );
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 0);
    options
        .install_priced(
            "priced",
            &digest,
            request(),
            configuration(logical),
            &token,
            Deadline::recovery(),
        )
        .await
        .unwrap();
    let engine = options.pricing("priced").unwrap().unwrap();
    async fn ready(engine: &ifm_market_data_native::option_chain::coalesced::Coalesced) {
        let mut updates = engine.updates();
        tokio::time::timeout(Duration::from_secs(2), async {
            while !engine.ready(0).unwrap() {
                updates.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
    }
    ready(&engine).await;
    let before = engine.read(43, true, 0).unwrap().unwrap();
    assert!(before.risk.is_some());
    assert_eq!(before.option.generation_low, logical as u64);
    assert_eq!(before.option.generation_high, (logical >> 64) as u64);
    options
        .install_priced(
            "priced",
            &digest,
            request(),
            configuration(logical),
            &token,
            Deadline::recovery(),
        )
        .await
        .unwrap();
    assert!(Arc::ptr_eq(
        &engine,
        &options.pricing("priced").unwrap().unwrap()
    ));
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
    assert!(
        options
            .install("priced", &digest, request(), &token, Deadline::recovery())
            .await
            .is_err()
    );
    let mut conflict = configuration(logical);
    conflict.digest = "c".repeat(64);
    assert!(
        options
            .install_priced(
                "priced",
                &digest,
                request(),
                conflict,
                &token,
                Deadline::recovery()
            )
            .await
            .is_err()
    );
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
    options
        .reset("priced", &token, Deadline::recovery())
        .await
        .unwrap();
    ready(&engine).await;
    assert_ne!(
        engine.read(43, true, 0).unwrap().unwrap().source_generation,
        before.source_generation
    );
    assert_eq!(
        options.status("priced").unwrap().unwrap().phase,
        Phase::Running
    );
    options
        .remove("priced", &token, Deadline::recovery())
        .await
        .unwrap();
    assert!(options.pricing("priced").unwrap().is_none());
    assert!(engine.read(43, true, 0).unwrap().is_none());
    options
        .shutdown(&token, Deadline::recovery())
        .await
        .unwrap();
    root.stop().unwrap();
    root.wait_idle(&token, Deadline::recovery()).await.unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
}

#[tokio::test]
async fn real_sdk_option_replay_statistics_and_reset_do_not_recount_logical_volume() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, OutputSource, Registration, Start},
        latest::Asset,
        option_chain::statistics::Seed,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::OptionReplay).await;
    let root = Arc::new(Owner::default());
    let day = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        day,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        root.clone(),
    )
    .unwrap();
    data.seed_option_statistics(
        43,
        Seed {
            volume: Some(1000),
            volume_date: Some(day),
            open_interest: Some(300),
            open_interest_date: Some(day - time::Duration::days(3)),
        },
    )
    .unwrap();
    data.start(
        Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: vec![
                    SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        schema: Schema::Trades,
                        symbology: SType::InstrumentId,
                        replay_start_ns: Some(0),
                    },
                    SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        schema: Schema::Mbp1,
                        symbology: SType::InstrumentId,
                        replay_start_ns: None,
                    },
                    SubscriptionSpec {
                        symbols: vec!["43".into()],
                        schema: Schema::Statistics,
                        symbology: SType::InstrumentId,
                        replay_start_ns: None,
                    },
                ],
            },
            registrations: vec![
                Registration {
                    publisher: 0,
                    instrument: 42,
                    contract: "ES20261218".into(),
                    source: "42".into(),
                    asset: Asset::Futures,
                    tick: None,
                    vwap: false,
                    required: true,
                },
                Registration {
                    publisher: 0,
                    instrument: 43,
                    contract: "ES20261218C6000".into(),
                    source: "43".into(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                },
            ],
            key: "x".repeat(32),
            gateway: fixture.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    async fn complete(
        data: &LiveData,
    ) -> ifm_market_data_native::option_chain::statistics::Statistics {
        let mut changes = data.updates();
        tokio::time::timeout(Duration::from_secs(2), async {
            loop {
                if let Some((_, state)) = data.publication_state(Lane::OptionsGlbx, 43).unwrap()
                    && let Some(stats) = state.option_statistics
                    && stats.open_interest == Some(666)
                {
                    return stats;
                }
                changes.changed().await.unwrap();
            }
        })
        .await
        .unwrap()
    }
    let before = complete(&data).await;
    assert_eq!(before.volume, Some(9000));
    assert!(before.official);
    assert!(before.replay_complete);
    assert_eq!(before.observed, 12);
    assert_eq!(before.volume_date, Some(day));
    assert_eq!(
        before.open_interest_date,
        Some(day - time::Duration::days(1))
    );
    let reader = data
        .reader(Lane::OptionsGlbx, "ES20261218C6000")
        .unwrap()
        .unwrap();
    assert_eq!(
        reader.trade().unwrap().unwrap().value.price,
        101_000_000_000
    );
    let output = ifm_market_data_native::dataset::live::Output {
        observed: 0,
        lane: Lane::OptionsGlbx,
        generation: 0,
        source: OutputSource::ReplayComplete(0),
        state: Default::default(),
        trade: None,
    };
    let mut outputs: [_; 16] = std::array::from_fn(|_| output.clone());
    let count = data.drain_output(&mut outputs).unwrap();
    let outputs = &outputs[..count];
    assert_eq!(outputs.iter().filter(|o|matches!(o.source,OutputSource::Observation(Observation::Trade(t))if t.header.instrument==43&&t.header.flags&2!=0)).count(),0);
    assert!(outputs.iter().any(|o|matches!(o.source,OutputSource::Observation(Observation::Trade(t))if t.header.instrument==43&&t.header.flags&2==0)));
    assert!(
        !outputs
            .iter()
            .any(|o| matches!(o.source, OutputSource::ReplayComplete(43)))
    );
    data.reset(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert_eq!(complete(&data).await, before);
    assert!(reader.trade().unwrap().is_none());
    data.stop(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn resetting_primary_futures_cannot_prune_option_statistics_or_replace_its_book() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        option_chain::statistics::Seed,
        runtime::{clock::Clock, owner::Owner},
    };
    let future = Fixture::new(Mode::Healthy).await;
    let option = Fixture::new(Mode::Pricing).await;
    let root = Arc::new(Owner::default());
    let day = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        day,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        root.clone(),
    )
    .unwrap();
    let registration = Registration {
        publisher: 0,
        instrument: 42,
        contract: "ES20261218".into(),
        source: "ESZ6".into(),
        asset: Asset::Futures,
        tick: None,
        vwap: false,
        required: true,
    };
    data.start(
        Start {
            plan: plan(Lane::FuturesGlbx),
            registrations: vec![registration.clone()],
            key: "x".repeat(32),
            gateway: future.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    data.seed_option_statistics(
        43,
        Seed {
            volume: Some(321),
            volume_date: Some(day),
            ..Default::default()
        },
    )
    .unwrap();
    let mut dependency = registration;
    dependency.source = "42".into();
    data.start(
        Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: vec![
                    SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        schema: Schema::Trades,
                        symbology: SType::InstrumentId,
                        replay_start_ns: None,
                    },
                    SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        schema: Schema::Mbp1,
                        symbology: SType::InstrumentId,
                        replay_start_ns: None,
                    },
                ],
            },
            registrations: vec![
                dependency,
                Registration {
                    publisher: 0,
                    instrument: 43,
                    contract: "ES20261218C6000".into(),
                    source: "43".into(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                },
            ],
            key: "x".repeat(32),
            gateway: option.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    let (generation, state) = data
        .publication_state(Lane::OptionsGlbx, 43)
        .unwrap()
        .unwrap();
    assert_eq!(state.option_statistics.unwrap().volume, Some(321));
    data.reset(
        Lane::FuturesGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    let (after, stats) = data
        .publication_state(Lane::OptionsGlbx, 43)
        .unwrap()
        .unwrap();
    assert_eq!(generation, after);
    assert_eq!(stats.option_statistics, state.option_statistics);
    data.reset(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert_eq!(
        data.publication_state(Lane::OptionsGlbx, 43)
            .unwrap()
            .unwrap()
            .1
            .option_statistics,
        state.option_statistics
    );
    for lane in [Lane::FuturesGlbx, Lane::OptionsGlbx] {
        data.stop(lane, &CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
    }
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    future.stop().await;
    option.stop().await;
}

async fn status_where(
    status: &mut watch::Receiver<Status>,
    predicate: impl Fn(&Status) -> bool,
) -> Status {
    tokio::time::timeout(Duration::from_secs(3), async {
        loop {
            let snapshot = status.borrow_and_update().clone();
            if predicate(&snapshot) {
                return snapshot;
            }
            status.changed().await.unwrap();
        }
    })
    .await
    .unwrap()
}

#[tokio::test]
async fn actual_sdk_trade_reaches_native_book_and_reset_retires_old_reader() {
    use ifm_market_data_native::{dataset::book::Book, latest::Asset};
    let fixture = Fixture::new(Mode::Healthy).await;
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let shared = Arc::new(std::sync::Mutex::new(None::<(u64, Book<()>)>));
    let processing = shared.clone();
    let mut lanes = Lanes::default();
    let mut status = lanes
        .start_with_budget(
            plan(Lane::FuturesGlbx),
            "x".repeat(32),
            fixture.gateway(),
            move |generation, event| {
                let mut slot = processing
                    .lock()
                    .map_err(|_| "Native book writer interrupted".to_owned())?;
                if slot.as_ref().is_none_or(|(prior, _)| *prior != generation) {
                    if let Some((_, prior)) = slot.as_mut() {
                        prior.retire().map_err(|e| format!("{e:?}"))?;
                    }
                    let mut book =
                        Book::new(date, u128::from(generation), 4).map_err(|e| format!("{e:?}"))?;
                    book.register(42, "ES20261218", Asset::Futures, 4)
                        .map_err(|e| format!("{e:?}"))?;
                    *slot = Some((generation, book));
                }
                let book = &mut slot.as_mut().unwrap().1;
                if let Event::Market(record) = event {
                    book.apply(u128::from(generation), *record, 15)
                        .map_err(|e| format!("{e:?}"))?;
                }
                Ok(book
                    .reader("ES20261218")
                    .map_err(|e| format!("{e:?}"))?
                    .unwrap()
                    .trade()
                    .map_err(|e| format!("{e:?}"))?
                    .is_some())
            },
            Duration::from_secs(1),
        )
        .unwrap();
    status_where(&mut status, |s| s.phase == Phase::Running).await;
    let old = shared
        .lock()
        .unwrap()
        .as_ref()
        .unwrap()
        .1
        .reader("ES20261218")
        .unwrap()
        .unwrap();
    assert_eq!(old.trade().unwrap().unwrap().value.price, 6_000_250_000_000);
    lanes.reset(Lane::FuturesGlbx).unwrap();
    status_where(&mut status, |s| {
        s.phase == Phase::Running && s.generation == 2
    })
    .await;
    assert!(old.trade().unwrap().is_none());
    {
        let slot = shared.lock().unwrap();
        let book = &slot.as_ref().unwrap().1;
        assert_eq!(book.contract_id(42), Some("ES20261218"));
        assert_eq!(
            book.reader("ES20261218")
                .unwrap()
                .unwrap()
                .trade()
                .unwrap()
                .unwrap()
                .value
                .size,
            4
        );
    }
    // The typed book test covers stale source records; this integrated fixture
    // verifies socket -> SDK -> copied observation -> coherent native reader.
    lanes
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    shared.lock().unwrap().take();
    assert!(old.trade().unwrap().is_none());
    fixture.stop().await;
}

#[tokio::test]
async fn runtime_shutdown_waits_for_all_tracked_sdk_lanes_and_individual_stop_is_isolated() {
    use ifm_market_data_native::runtime::owner::{Owner, OwnerError};
    let fixture = Fixture::new(Mode::Healthy).await;
    let owner = Arc::new(Owner::default());
    let mut lanes = Lanes::default();
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
        let mut status = lanes
            .start_tracked(
                plan(lane),
                "x".repeat(32),
                fixture.gateway(),
                |_, event| Ok(matches!(event, Event::Market(_))),
                Deadline::after(Duration::from_secs(2)),
                &owner,
            )
            .unwrap();
        status_where(&mut status, |s| s.phase == Phase::Running).await;
    }
    assert!(matches!(owner.child(), Err(OwnerError::Capacity)));
    lanes
        .stop(Lane::OptionsGlbx, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert_eq!(
        lanes.status(Lane::FuturesGlbx).unwrap().phase,
        Phase::Running
    );
    assert_eq!(
        lanes.status(Lane::FuturesXcbf).unwrap().phase,
        Phase::Running
    );
    let waiter = owner.clone();
    let (tx, rx) = std::sync::mpsc::channel();
    // Qualification's separate thread exercises the actual event-driven managed
    // wait primitive, without blocking a Tokio provider worker or timer polling.
    let thread = std::thread::spawn(move || {
        loop {
            let ready = waiter.wait();
            if ready == Ok(true) {
                waiter.take_signals().unwrap();
            } else {
                tx.send(ready).unwrap();
                break;
            }
        }
    });
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert_eq!(rx.recv_timeout(Duration::from_secs(2)).unwrap(), Ok(false));
    thread.join().unwrap();
    assert!(owner.disposable().unwrap());
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        assert_eq!(lanes.status(lane).unwrap().phase, Phase::Stopped);
    }
    lanes
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    tokio::time::timeout(Duration::from_secs(2), async {
        while fixture.closed.load(Ordering::SeqCst) != 3 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn independent_option_sessions_share_one_root_slot_and_reset_or_retire_separately() {
    use ifm_market_data_native::runtime::owner::{Owner, OwnerError};
    let fixture = Fixture::new(Mode::Healthy).await;
    let owner = Arc::new(Owner::default());
    let mut futures = Lanes::default();
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        let mut state = futures
            .start_tracked(
                plan(lane),
                "x".repeat(32),
                fixture.gateway(),
                |_, event| Ok(matches!(event, Event::Market(_))),
                Deadline::after(Duration::from_secs(2)),
                &owner,
            )
            .unwrap();
        status_where(&mut state, |s| s.phase == Phase::Running).await;
    }
    let options = owner.child().unwrap();
    let mut chain = Lanes::default();
    let mut individual = Lanes::default();
    let mut a = chain
        .start_nested(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, event| Ok(matches!(event, Event::Market(_))),
            Deadline::after(Duration::from_secs(2)),
            &options,
        )
        .unwrap();
    let mut b = individual
        .start_nested(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, event| Ok(matches!(event, Event::Market(_))),
            Deadline::after(Duration::from_secs(2)),
            &options,
        )
        .unwrap();
    status_where(&mut a, |s| s.phase == Phase::Running).await;
    status_where(&mut b, |s| s.phase == Phase::Running).await;
    assert!(matches!(owner.child(), Err(OwnerError::Capacity)));
    let unchanged = b.borrow().generation;
    chain
        .reset_with_deadline(Lane::OptionsGlbx, Deadline::after(Duration::from_secs(2)))
        .unwrap();
    status_where(&mut a, |s| s.phase == Phase::Running && s.generation == 2).await;
    assert_eq!(b.borrow().generation, unchanged);
    assert_eq!(b.borrow().phase, Phase::Running);
    chain
        .stop(Lane::OptionsGlbx, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert_eq!(b.borrow().phase, Phase::Running);
    drop(options);
    status_where(&mut b, |s| s.phase == Phase::Stopped).await;
    individual
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        let state = futures.status(lane).unwrap();
        assert_eq!(state.phase, Phase::Running);
        assert_eq!(state.generation, 1);
    }
    owner.stop().unwrap();
    futures
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert!(owner.disposable().unwrap());
    tokio::time::timeout(Duration::from_secs(2), async {
        while fixture.closed.load(Ordering::SeqCst) != 5 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn live_data_owner_integrates_three_books_and_options_reset_without_futures_changes() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Healthy).await;
    let owner = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(100, false).unwrap()),
        owner.clone(),
    )
    .unwrap();
    assert_eq!(data.instance(), 1);
    let mut invalidation = data.updates();
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
        let option = lane == Lane::OptionsGlbx;
        let registration = Registration {
            publisher: 0,
            instrument: 42,
            contract: if option {
                "ES20261218C6500"
            } else {
                "ES20261218"
            }
            .into(),
            source: "ESZ6".into(),
            asset: if option {
                Asset::Option
            } else {
                Asset::Futures
            },
            tick: Some(250000000),
            vwap: !option,
            required: true,
        };
        let running = data
            .start(
                Start {
                    plan: plan(lane),
                    registrations: vec![registration],
                    key: "x".repeat(32),
                    gateway: fixture.gateway(),
                    evidence: Evidence::Observations,
                },
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2)),
            )
            .await
            .unwrap();
        assert_eq!(running.phase, Phase::Running);
    }
    tokio::time::timeout(Duration::from_secs(1), invalidation.changed())
        .await
        .unwrap()
        .unwrap();
    let old = data
        .reader(Lane::OptionsGlbx, "ES20261218C6500")
        .unwrap()
        .unwrap();
    assert_eq!(old.trade().unwrap().unwrap().value.size, 4);
    let futures = data.status(Lane::FuturesGlbx).unwrap().unwrap().generation;
    let restored = data
        .reset(
            Lane::OptionsGlbx,
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert_eq!(restored.generation, 2);
    assert!(old.trade().unwrap().is_none());
    assert!(
        data.reader(Lane::OptionsGlbx, "ES20261218C6500")
            .unwrap()
            .unwrap()
            .trade()
            .unwrap()
            .is_some()
    );
    assert_eq!(
        data.status(Lane::FuturesGlbx).unwrap().unwrap().generation,
        futures
    );
    data.stop(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    assert!(
        data.reader(Lane::OptionsGlbx, "ES20261218C6500")
            .unwrap()
            .is_none()
    );
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        assert!(
            data.reader(lane, "ES20261218")
                .unwrap()
                .unwrap()
                .trade()
                .unwrap()
                .is_some()
        );
        data.stop(
            lane,
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    }
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert!(owner.disposable().unwrap());
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn option_scope_supervisor_reuses_live_evidence_resets_one_scope_and_keeps_futures_running() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        option_chain::owner::Options,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Healthy).await;
    let root = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let clock = Arc::new(Clock::new(0, false).unwrap());
    let primary = LiveData::new(date, 1, 8, clock.clone(), root.clone()).unwrap();
    let make = |lane: Lane, id: &str, option| Start {
        plan: plan(lane),
        registrations: vec![Registration {
            publisher: 0,
            instrument: 42,
            contract: id.into(),
            source: "ESZ6".into(),
            asset: if option {
                Asset::Option
            } else {
                Asset::Futures
            },
            tick: None,
            vwap: false,
            required: true,
        }],
        key: "x".repeat(32),
        gateway: fixture.gateway(),
        evidence: Evidence::Observations,
    };
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        primary
            .start(
                make(lane, "ES20261218", false),
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2)),
            )
            .await
            .unwrap();
    }
    let options = Options::new(date, 1, clock, root.clone()).unwrap();
    assert_ne!(options.generation(), 0);
    let digest = "a".repeat(64);
    let token = CancellationToken::new();
    options
        .install(
            "chain",
            &digest,
            make(Lane::OptionsGlbx, "ES20261218C6000", true),
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    options
        .install(
            "individual",
            &digest,
            make(Lane::OptionsGlbx, "ES20261218P6000", true),
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    let before = fixture.accepted.load(Ordering::SeqCst);
    options
        .install(
            "chain",
            &digest,
            make(Lane::OptionsGlbx, "ES20261218C6000", true),
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), before);
    assert!(
        options
            .install(
                "chain",
                &"b".repeat(64),
                make(Lane::OptionsGlbx, "ES20261218C6000", true),
                &token,
                Deadline::after(Duration::from_secs(2))
            )
            .await
            .is_err()
    );
    let old = options
        .scope("chain")
        .unwrap()
        .unwrap()
        .reader(Lane::OptionsGlbx, "ES20261218C6000")
        .unwrap()
        .unwrap();
    options
        .reset("chain", &token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert!(old.trade().unwrap().is_none());
    assert_eq!(options.status("chain").unwrap().unwrap().generation, 2);
    assert_eq!(options.status("individual").unwrap().unwrap().generation, 1);
    options
        .remove("chain", &token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert!(options.scope("chain").unwrap().is_none());
    assert_eq!(
        options.status("individual").unwrap().unwrap().phase,
        Phase::Running
    );
    options
        .shutdown(&token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        assert!(
            primary
                .reader(lane, "ES20261218")
                .unwrap()
                .unwrap()
                .trade()
                .unwrap()
                .is_some()
        );
    }
    root.stop().unwrap();
    root.wait_idle(&token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_scope_claims_expire_join_sdk_and_renew_without_provider_control_admission() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, Registration, Start},
        latest::Asset,
        option_chain::owner::Options,
        runtime::{clock::Clock, owner::Owner},
        subscriptions::leases::Lifetime,
    };
    let fixture = Fixture::new(Mode::Healthy).await;
    let stalled = Fixture::new(Mode::AuthStall).await;
    let root = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let options = Arc::new(
        Options::new(
            date,
            1,
            Arc::new(Clock::new(0, false).unwrap()),
            root.clone(),
        )
        .unwrap(),
    );
    let make = |gateway| Start {
        plan: plan(Lane::OptionsGlbx),
        registrations: vec![Registration {
            publisher: 0,
            instrument: 42,
            contract: "ES20261218C6000".into(),
            source: "ESZ6".into(),
            asset: Asset::Option,
            tick: None,
            vwap: false,
            required: true,
        }],
        key: "x".repeat(32),
        gateway,
        evidence: Evidence::Observations,
    };
    let token = CancellationToken::new();
    options
        .install(
            "chain",
            &"a".repeat(64),
            make(fixture.gateway()),
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    let retained = options
        .scope("chain")
        .unwrap()
        .unwrap()
        .reader(Lane::OptionsGlbx, "ES20261218C6000")
        .unwrap()
        .unwrap();
    let temporary = options
        .retain_scopes(
            11,
            &["chain".into()],
            Lifetime::Ephemeral(Duration::from_millis(30)),
        )
        .unwrap();
    let durable = options
        .retain_scopes(12, &["chain".into()], Lifetime::Durable)
        .unwrap();
    tokio::time::sleep(Duration::from_millis(60)).await;
    assert!(
        options
            .renew_claim(temporary, Duration::from_secs(1))
            .is_err()
    );
    assert_eq!(
        options.status("chain").unwrap().unwrap().phase,
        Phase::Running
    );
    options.release_claim(durable).unwrap();
    fixture.await_closed().await;
    tokio::time::timeout(Duration::from_secs(2), async {
        while options.status("chain").unwrap().is_some() {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    assert!(retained.trade().unwrap().is_none());
    options
        .install(
            "chain",
            &"a".repeat(64),
            make(fixture.gateway()),
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    let claim = options
        .retain_scopes(
            13,
            &["chain".into()],
            Lifetime::Ephemeral(Duration::from_secs(2)),
        )
        .unwrap();
    let request = make(stalled.gateway());
    let cancel = CancellationToken::new();
    let stop = cancel.clone();
    let control = options.clone();
    let blocked = tokio::spawn(async move {
        control
            .install(
                "stalled",
                &"b".repeat(64),
                request,
                &stop,
                Deadline::after(Duration::from_secs(2)),
            )
            .await
    });
    tokio::time::timeout(Duration::from_secs(1), async {
        while stalled.accepted.load(Ordering::SeqCst) == 0 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    let before = Instant::now();
    options.renew_claim(claim, Duration::from_secs(2)).unwrap();
    options.release_claim(claim).unwrap();
    assert!(before.elapsed() < Duration::from_millis(100));
    fixture.await_closed().await;
    assert!(
        !blocked.is_finished(),
        "Released scope waited for unrelated provider control"
    );
    cancel.cancel();
    assert!(blocked.await.unwrap().is_err());
    fixture.await_closed().await;
    stalled.await_closed().await;
    options
        .shutdown(&token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    root.stop().unwrap();
    root.wait_idle(&token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
    stalled.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_futures_reach_nats_without_ffi_and_dataset_rebuild_ignores_stalled_broker() {
    native_futures_delivery_integration(0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_source_failure_cannot_hide_behind_old_broker_flush_or_extend_its_deadline() {
    native_futures_delivery_integration(1).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "uses the actual production 120-second native publication deadline"]
async fn native_silent_broker_stall_expires_original_120_seconds_without_a_second_window() {
    native_futures_delivery_integration(2).await;
}
async fn native_futures_delivery_integration(failure_mode: u8) {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        messaging::{
            futures::Futures,
            futures_delivery::{Configuration, Delivery},
            publisher::BrokerSettings,
        },
        runtime::{clock::Clock, owner::Owner},
    };
    use std::sync::atomic::AtomicBool;
    let fixture = Fixture::new(if failure_mode == 1 {
        Mode::HealthyThenNoAck
    } else {
        Mode::HealthyHeartbeat
    })
    .await;
    let other_fixture = Fixture::new(Mode::HealthyHeartbeat).await;
    let root = Arc::new(Owner::default());
    let parent = Arc::new(root.child().unwrap());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let clock = Arc::new(Clock::new(0, false).unwrap());
    let data = Arc::new(LiveData::new(date, 1, 8, clock.clone(), root.clone()).unwrap());
    let other = LiveData::new(date, 2, 8, clock, root.clone()).unwrap();
    let registration = Registration {
        publisher: 3,
        instrument: 42,
        contract: "ES20261218".into(),
        source: "ESZ6".into(),
        asset: Asset::Futures,
        tick: None,
        vwap: false,
        required: true,
    };
    let make = |lane| Start {
        plan: plan(lane),
        registrations: vec![registration.clone()],
        key: "x".repeat(32),
        gateway: if lane == Lane::FuturesXcbf {
            other_fixture.gateway()
        } else {
            fixture.gateway()
        },
        evidence: Evidence::Observations,
    };
    let token = CancellationToken::new();
    data.start_in_parent(
        make(Lane::FuturesGlbx),
        &token,
        Deadline::after(Duration::from_secs(2)),
        &parent,
    )
    .await
    .unwrap();
    other
        .start(
            make(Lane::FuturesXcbf),
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    let other_generation = other.generation(Lane::FuturesXcbf).unwrap();
    let old = data
        .reader(Lane::FuturesGlbx, "ES20261218")
        .unwrap()
        .unwrap();
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let stall = Arc::new(AtomicBool::new(false));
    let blocked = stall.clone();
    let (sent, mut packets) = tokio::sync::mpsc::channel(8);
    let server = tokio::spawn(async move {
        let (socket, _) = listener.accept().await.unwrap();
        let (read, mut write) = socket.into_split();
        let mut read = BufReader::new(read);
        write.write_all(b"INFO {\"server_id\":\"native-delivery\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
        let mut line = String::new();
        loop {
            line.clear();
            if read.read_line(&mut line).await.unwrap_or(0) == 0 {
                break;
            }
            if line.starts_with("PING") && !blocked.load(Ordering::SeqCst) {
                if write.write_all(b"PONG\r\n").await.is_err() {
                    break;
                }
            } else if line.starts_with("PUB ") {
                let count = line
                    .split_whitespace()
                    .last()
                    .unwrap()
                    .parse::<usize>()
                    .unwrap();
                let mut bytes = vec![0; count + 2];
                read.read_exact(&mut bytes).await.unwrap();
                bytes.truncate(count);
                if sent.send(bytes).await.is_err() {
                    break;
                }
            }
        }
    });
    let encoder = Futures::new(Lane::FuturesGlbx, date, date, vec![registration], 2).unwrap();
    let mut delivery = Delivery::start(
        data.clone(),
        encoder,
        Configuration {
            broker: BrokerSettings::new(
                format!("nats://{address}"),
                vec!["127.0.0.1:1".parse().unwrap()],
            )
            .unwrap(),
            scope: "development".into(),
            lane: Lane::FuturesGlbx,
            value_date: date.to_julian_day() - 1721426,
            worker: [1; 16],
            generation: [2; 16],
            revision: 1,
            control_only: false,
            queue: 2,
            bytes: 8192,
        },
        parent.clone(),
        Deadline::after(Duration::from_secs(2)),
    )
    .unwrap();
    // The coherent session statistics now precede the execution that consumes
    // them. Decode the actual NATS packets, not a second volume algorithm.
    let mut last_volume = None;
    loop {
        let packet = tokio::time::timeout(Duration::from_secs(2), packets.recv())
            .await.unwrap().unwrap();
        let compressed = rmpv::decode::read_value(&mut packet.as_slice()).unwrap();
        let array = compressed.as_array().unwrap();
        let rmpv::Value::Ext(98, length) = &array[0] else { panic!("Expected production envelope compression"); };
        let length = rmpv::decode::read_value(&mut length.as_slice()).unwrap().as_u64().unwrap() as usize;
        let content = lz4_flex::block::decompress(array[1].as_slice().unwrap(), length).unwrap();
        let envelope = rmpv::decode::read_value(&mut content.as_slice()).unwrap();
        let envelope = envelope.as_array().unwrap();
        assert_eq!(envelope[0].as_str(), Some("GLBX.MDP3"));
        let event = rmpv::decode::read_value(&mut envelope[7].as_slice().unwrap()).unwrap();
        let event = event.as_array().unwrap();
        if envelope[6].as_u64() == Some(4) {
            let statistics = event[8].as_array().unwrap();
            assert_eq!(statistics[1].as_i64(), Some(i64::from(date.to_julian_day() - 1721426)));
            last_volume = statistics[7].as_i64();
            continue;
        }
        assert_eq!(envelope[6].as_u64(), Some(3));
        assert!(last_volume.is_some_and(|v| v > 0), "volume must reach the broker before its execution");
        assert_eq!(event[0].as_array().unwrap()[1].as_str(), Some("FuturesMarketPrice"));
        assert_eq!(event[12].as_array().unwrap()[15].as_array().unwrap()[5].as_str(), Some("6000.25"));
        break;
    }
    let mut health = delivery.changes();
    tokio::time::timeout(Duration::from_secs(2), async {
        while !health.borrow().running {
            health.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    let detached_catchup = delivery.current().capture(0, 128).unwrap();
    assert_eq!(detached_catchup.len(), 2);
    if failure_mode == 1 {
        let incident = Deadline::after(Duration::from_millis(100));
        let before = Instant::now();
        assert!(
            data.reset(Lane::FuturesGlbx, &token, incident)
                .await
                .is_err()
        );
        let mut status = delivery.changes();
        tokio::time::timeout(Duration::from_secs(1), async {
            while !status.borrow().terminal {
                status.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
        assert!(
            before.elapsed() < Duration::from_secs(1),
            "the exhausted provider incident must not become another two minutes"
        );
        assert!(incident.remaining().is_zero());
        assert!(!delivery.status().running);
        assert!(!detached_catchup[0].payload.is_empty());
    } else {
        stall.store(true, Ordering::SeqCst);
        let incident = if failure_mode == 2 {
            Deadline::recovery()
        } else {
            Deadline::after(Duration::from_secs(2))
        };
        let incident_started = Instant::now();
        data.reset(Lane::FuturesGlbx, &token, incident)
            .await
            .unwrap();
        // A real native PUB is now awaiting a broker PONG; retain the detached
        // payload as a stalled downstream consumer while replacing source resources.
        let unacknowledged = tokio::time::timeout(Duration::from_secs(2), packets.recv())
            .await
            .unwrap()
            .unwrap();
        if failure_mode == 2 {
            let mut status = delivery.changes();
            tokio::time::timeout(Duration::from_secs(124), async {
                while !status.borrow().terminal {
                    status.changed().await.unwrap();
                }
            })
            .await
            .unwrap();
            assert!(incident_started.elapsed() >= Duration::from_secs(119));
            assert!(incident_started.elapsed() < Duration::from_secs(124));
            assert!(incident.remaining().is_zero());
            assert!(
                delivery
                    .status()
                    .error
                    .as_deref()
                    .is_some_and(|s| s.contains("deadline expired"))
            );
        } else {
            let before = data.generation(Lane::FuturesGlbx).unwrap();
            let deadline = Deadline::after(Duration::from_secs(2));
            data.reset(Lane::FuturesGlbx, &token, deadline)
                .await
                .unwrap();
            assert_ne!(data.generation(Lane::FuturesGlbx).unwrap(), before);
            assert!(old.trade().unwrap().is_none());
            assert!(!unacknowledged.is_empty());
            assert!(!detached_catchup[0].payload.is_empty());
        }
    }
    assert_eq!(
        other.generation(Lane::FuturesXcbf).unwrap(),
        other_generation
    );
    assert_eq!(
        other.status(Lane::FuturesXcbf).unwrap().unwrap().phase,
        Phase::Running
    );
    delivery
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    tokio::time::timeout(Duration::from_secs(2), server)
        .await
        .unwrap()
        .unwrap();
    data.stop(
        Lane::FuturesGlbx,
        &token,
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    other
        .stop(
            Lane::FuturesXcbf,
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    drop(parent);
    root.stop().unwrap();
    root.wait_idle(&token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.await_closed().await;
    fixture.stop().await;
    other_fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn option_scope_failure_and_cancellation_keep_cleanup_ownership_and_allow_fresh_acquisition()
{
    use ifm_market_data_native::{
        dataset::live::{Evidence, Registration, Start},
        latest::Asset,
        option_chain::owner::Options,
        runtime::{clock::Clock, owner::Owner},
    };
    let failing = Fixture::new(Mode::MetadataAlwaysBad).await;
    let healthy = Fixture::new(Mode::Healthy).await;
    let root = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let options = Options::new(
        date,
        1,
        Arc::new(Clock::new(0, false).unwrap()),
        root.clone(),
    )
    .unwrap();
    let make = |fixture: &Fixture| Start {
        plan: plan(Lane::OptionsGlbx),
        registrations: vec![Registration {
            publisher: 0,
            instrument: 42,
            contract: "ES20261218C6000".into(),
            source: "ESZ6".into(),
            asset: Asset::Option,
            tick: None,
            vwap: false,
            required: true,
        }],
        key: "x".repeat(32),
        gateway: fixture.gateway(),
        evidence: Evidence::Observations,
    };
    let digest = "a".repeat(64);
    let token = CancellationToken::new();
    assert!(
        options
            .install(
                "scope",
                &digest,
                make(&failing),
                &token,
                Deadline::after(Duration::from_millis(350))
            )
            .await
            .is_err()
    );
    assert!(options.scope("scope").unwrap().is_none());
    options
        .install(
            "scope",
            &digest,
            make(&healthy),
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert_eq!(
        options.status("scope").unwrap().unwrap().phase,
        Phase::Running
    );
    options
        .remove("scope", &token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    let stalled = Fixture::new(Mode::AuthStall).await;
    let cancelled = CancellationToken::new();
    let future = options.install(
        "scope",
        &digest,
        make(&stalled),
        &cancelled,
        Deadline::after(Duration::from_secs(2)),
    );
    tokio::pin!(future);
    tokio::select! {result=&mut future=>panic!("Stalled install finished early: {result:?}"), ()=async{while stalled.accepted.load(Ordering::SeqCst)==0{tokio::task::yield_now().await;}}=>{}}
    cancelled.cancel();
    assert!(future.await.is_err());
    options
        .shutdown(&token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    root.stop().unwrap();
    root.wait_idle(&token, Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    failing.stop().await;
    healthy.stop().await;
    stalled.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn option_scope_emergency_stop_bypasses_saturated_controls_and_wakes_every_caller() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, Registration, Start},
        latest::Asset,
        option_chain::owner::Options,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::AuthStall).await;
    let root = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let options = Arc::new(
        Options::new(
            date,
            1,
            Arc::new(Clock::new(0, false).unwrap()),
            root.clone(),
        )
        .unwrap(),
    );
    let (results, mut replies) = tokio::sync::mpsc::channel(18);
    let mut calls = Vec::new();
    for index in 0..18 {
        let options = options.clone();
        let results = results.clone();
        let gateway = fixture.gateway();
        calls.push(tokio::spawn(async move {
            let request = Start {
                plan: plan(Lane::OptionsGlbx),
                registrations: vec![Registration {
                    publisher: 0,
                    instrument: 42,
                    contract: "ES20261218C6000".into(),
                    source: "ESZ6".into(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                }],
                key: "x".repeat(32),
                gateway,
                evidence: Evidence::Observations,
            };
            let result = options
                .install(
                    &format!("scope-{index}"),
                    &"a".repeat(64),
                    request,
                    &CancellationToken::new(),
                    Deadline::after(Duration::from_secs(5)),
                )
                .await;
            results.send(result).await.unwrap();
        }));
        if index == 0 {
            tokio::time::timeout(Duration::from_secs(1), async {
                while fixture.accepted.load(Ordering::SeqCst) == 0 {
                    tokio::task::yield_now().await;
                }
            })
            .await
            .unwrap();
        }
    }
    let saturated = tokio::time::timeout(Duration::from_secs(1), replies.recv())
        .await
        .unwrap()
        .unwrap()
        .unwrap_err();
    assert!(
        saturated.contains("capacity"),
        "unexpected result before emergency shutdown: {saturated}"
    );
    root.stop().unwrap();
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    tokio::time::timeout(Duration::from_secs(1), async {
        for _ in 0..17 {
            assert!(replies.recv().await.unwrap().is_err());
        }
    })
    .await
    .unwrap();
    for call in calls {
        call.await.unwrap();
    }
    options
        .shutdown(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(1)),
        )
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn nested_option_books_reset_independently_and_parent_stop_retires_retained_readers() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Healthy).await;
    let root = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let clock = Arc::new(Clock::new(0, false).unwrap());
    let primary = LiveData::new(date, 1, 8, clock.clone(), root.clone()).unwrap();
    let make = |lane: Lane, id: &str, option| Start {
        plan: plan(lane),
        registrations: vec![Registration {
            publisher: 0,
            instrument: 42,
            contract: id.into(),
            source: "ESZ6".into(),
            asset: if option {
                Asset::Option
            } else {
                Asset::Futures
            },
            tick: None,
            vwap: false,
            required: true,
        }],
        key: "x".repeat(32),
        gateway: fixture.gateway(),
        evidence: Evidence::Observations,
    };
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        primary
            .start(
                make(lane, "ES20261218", false),
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2)),
            )
            .await
            .unwrap();
    }
    let parent = root.child().unwrap();
    let a = LiveData::new(date, 1, 8, clock.clone(), root.clone()).unwrap();
    let b = LiveData::new(date, 1, 8, clock, root.clone()).unwrap();
    a.start_nested(
        make(Lane::OptionsGlbx, "ES20261218C6000", true),
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
        &parent,
    )
    .await
    .unwrap();
    b.start_nested(
        make(Lane::OptionsGlbx, "ES20261218P6000", true),
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
        &parent,
    )
    .await
    .unwrap();
    let old_a = a
        .reader(Lane::OptionsGlbx, "ES20261218C6000")
        .unwrap()
        .unwrap();
    let old_b = b
        .reader(Lane::OptionsGlbx, "ES20261218P6000")
        .unwrap()
        .unwrap();
    assert!(old_a.trade().unwrap().is_some());
    assert!(old_b.trade().unwrap().is_some());
    a.reset(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    assert!(old_a.trade().unwrap().is_none());
    assert!(old_b.trade().unwrap().is_some());
    let new_a = a
        .reader(Lane::OptionsGlbx, "ES20261218C6000")
        .unwrap()
        .unwrap();
    assert_ne!(new_a.generation(), old_a.generation());
    parent.cancellation().cancel();
    parent
        .wait_nested(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert!(new_a.trade().unwrap().is_none());
    assert!(old_b.trade().unwrap().is_none());
    // Neither reading retained books nor parent retirement alters primary futures.
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        assert!(
            primary
                .reader(lane, "ES20261218")
                .unwrap()
                .unwrap()
                .trade()
                .unwrap()
                .is_some()
        );
    }
    drop(parent);
    root.stop().unwrap();
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf] {
        assert!(primary.reader(lane, "ES20261218").unwrap().is_none());
    }
    assert!(root.disposable().unwrap());
    fixture.stop().await;
}

#[tokio::test]
async fn options_own_exact_underlying_dependency_without_replacing_primary_futures() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Healthy).await;
    let owner = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(100, false).unwrap()),
        owner.clone(),
    )
    .unwrap();
    for lane in [Lane::FuturesGlbx, Lane::OptionsGlbx] {
        let mut selected = plan(lane);
        let mut registrations = vec![Registration {
            publisher: 0,
            instrument: 42,
            contract: "ES20261218".into(),
            source: "ESZ6".into(),
            asset: Asset::Futures,
            tick: None,
            vwap: false,
            required: true,
        }];
        if lane == Lane::OptionsGlbx {
            selected.subscriptions[0].symbology = SType::InstrumentId;
            selected.subscriptions[0].symbols = vec!["42".into(), "43".into()];
            registrations[0].source = "42".into();
            registrations.push(Registration {
                publisher: 0,
                instrument: 43,
                contract: "ES20261218C6500".into(),
                source: "43".into(),
                asset: Asset::Option,
                tick: None,
                vwap: false,
                required: false,
            });
        }
        data.start(
            Start {
                plan: selected,
                registrations,
                key: "x".repeat(32),
                gateway: fixture.gateway(),
                evidence: Evidence::Observations,
            },
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    }
    let primary = data
        .reader(Lane::FuturesGlbx, "ES20261218")
        .unwrap()
        .unwrap();
    let old_dependency = data
        .reader(Lane::OptionsGlbx, "ES20261218")
        .unwrap()
        .unwrap();
    assert!(primary.trade().unwrap().is_some());
    assert!(old_dependency.trade().unwrap().is_some());
    assert_ne!(primary.generation(), old_dependency.generation());
    data.reset(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    assert!(old_dependency.trade().unwrap().is_none());
    assert!(primary.trade().unwrap().is_some());
    let new_dependency = data
        .reader(Lane::OptionsGlbx, "ES20261218")
        .unwrap()
        .unwrap();
    assert_ne!(old_dependency.generation(), new_dependency.generation());
    assert!(new_dependency.trade().unwrap().is_some());
    data.stop(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    assert!(new_dependency.trade().unwrap().is_none());
    assert!(primary.trade().unwrap().is_some());
    data.stop(
        Lane::FuturesGlbx,
        &CancellationToken::new(),
        Deadline::recovery(),
    )
    .await
    .unwrap();
    owner.stop().unwrap();
    owner
        .wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn real_sdk_copies_market_data_and_reports_warnings_and_all_consumed_errors() {
    for mode in [Mode::Healthy, Mode::ErrorBatch] {
        let fixture = Fixture::new(mode).await;
        let gateway = fixture.gateway();
        let token = CancellationToken::new();
        let mut session = Session::open(
            &plan(Lane::OptionsGlbx),
            "x".repeat(32),
            NamedGateway {
                host: &gateway.host,
                port: gateway.port,
                resolver: &gateway.resolver,
            },
            &token,
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
        assert_eq!(session.lane(), Lane::OptionsGlbx);
        assert!(matches!(
            session.next(&token, false, false).await.unwrap(),
            Some(Event::Mapping { instrument: 42, .. })
        ));
        assert!(matches!(
            session.next(&token, false, false).await.unwrap(),
            Some(Event::System { code: 1, .. })
        ));
        if matches!(mode, Mode::ErrorBatch) {
            match session.next(&token, false, false).await.unwrap().unwrap() {
                Event::Error {
                    code,
                    last,
                    message,
                } => {
                    assert_eq!(code, 199);
                    assert!(!last);
                    assert!(message.contains("[REDACTED]"));
                    assert!(!message.contains(&"x".repeat(32)));
                }
                other => panic!("{other:?}"),
            }
            assert!(matches!(
                session.next(&token, false, false).await.unwrap(),
                Some(Event::Error {
                    code: 5,
                    last: true,
                    ..
                })
            ));
        } else {
            assert!(matches!(
                session.next(&token, false, false).await.unwrap(),
                Some(Event::System { code: 2, .. })
            ));
            match session.next(&token, false, true).await.unwrap().unwrap() {
                Event::Market(Observation::Trade(trade)) => {
                    assert_eq!(trade.price, 6_000_250_000_000);
                    assert_eq!(trade.size, 4);
                    assert_eq!(trade.header.sequence, 7);
                    assert_eq!(trade.header.flags, 2);
                    assert_eq!(trade.header.publisher, 3);
                }
                other => panic!("{other:?}"),
            }
        }
        token.cancel();
        assert!(matches!(
            session.next(&token, false, false).await,
            Err(
                ifm_market_data_native::dataset::transport::Failure::Interrupted(
                    Interrupted::Cancelled
                )
            )
        ));
        fixture.stop().await;
    }
}

#[tokio::test]
async fn three_lanes_run_independently_and_options_reset_does_not_replace_futures() {
    let fixture = Fixture::new(Mode::Healthy).await;
    let mut lanes = Lanes::default();
    let mut observations = Vec::new();
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
        let status = lanes
            .start_with_budget(
                plan(lane),
                "x".repeat(32),
                fixture.gateway(),
                |_, event| Ok(matches!(event, Event::Market(_))),
                Duration::from_millis(500),
            )
            .unwrap();
        observations.push(status);
    }
    for status in &mut observations {
        status_where(status, |s| s.phase == Phase::Running).await;
    }
    let futures_generation = lanes.status(Lane::FuturesGlbx).unwrap().generation;
    lanes.reset(Lane::OptionsGlbx).unwrap();
    let option = status_where(&mut observations[2], |s| {
        s.phase == Phase::Running && s.generation >= 2
    })
    .await;
    assert_eq!(option.episode, 2);
    assert_eq!(
        lanes.status(Lane::FuturesGlbx).unwrap().generation,
        futures_generation
    );
    assert_eq!(
        lanes.status(Lane::FuturesXcbf).unwrap().phase,
        Phase::Running
    );
    // A healthy session outlives its initial setup/recovery budget.
    tokio::time::sleep(Duration::from_millis(550)).await;
    assert_eq!(
        lanes.status(Lane::OptionsGlbx).unwrap().phase,
        Phase::Running
    );
    lanes
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 4);
    tokio::time::timeout(Duration::from_secs(2), async {
        while fixture.closed.load(Ordering::SeqCst) != 4 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn recoverable_provider_error_reuses_original_episode_and_completes_replacement() {
    let fixture = Fixture::new(Mode::FirstError).await;
    let mut lanes = Lanes::default();
    let mut status = lanes
        .start_with_budget(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, event| Ok(matches!(event, Event::Market(_))),
            Duration::from_secs(1),
        )
        .unwrap();
    let running = status_where(&mut status, |s| s.phase == Phase::Running).await;
    assert_eq!(
        (running.generation, running.episode, running.attempts),
        (2, 1, 2)
    );
    lanes
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn unknown_provider_errors_never_claim_recovery_or_renew_the_deadline() {
    let fixture = Fixture::new(Mode::AlwaysError).await;
    let mut lanes = Lanes::default();
    let started = Instant::now();
    let mut status = lanes
        .start_with_budget(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, event| Ok(matches!(event, Event::Market(_))),
            Duration::from_millis(470),
        )
        .unwrap();
    let failed = status_where(&mut status, |s| s.phase == Phase::Failed).await;
    assert!(started.elapsed() >= Duration::from_millis(470));
    assert!(started.elapsed() < Duration::from_millis(900));
    assert_eq!(failed.episode, 1);
    assert!(failed.attempts >= 2);
    assert!(failed.error.unwrap().contains("code=199"));
    assert!(lanes.reset(Lane::OptionsGlbx).is_err());
    lanes
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn missing_acknowledgement_or_workload_proof_fails_at_deadline_and_is_observable() {
    for (mode, proof) in [(Mode::NoAck, true), (Mode::Healthy, false)] {
        let fixture = Fixture::new(mode).await;
        let mut lanes = Lanes::default();
        let mut status = lanes
            .start_with_budget(
                plan(Lane::OptionsGlbx),
                "x".repeat(32),
                fixture.gateway(),
                move |_, _| Ok(proof),
                Duration::from_millis(100),
            )
            .unwrap();
        status_where(&mut status, |s| s.phase == Phase::Failed).await;
        assert_eq!(status.borrow().episode, 1);
        lanes
            .shutdown(Deadline::after(Duration::from_secs(2)))
            .await
            .unwrap();
        fixture.stop().await;
    }
}

#[tokio::test]
async fn processor_panic_is_repaired_within_the_episode_and_not_silently_swallowed() {
    let fixture = Fixture::new(Mode::Healthy).await;
    let mut lanes = Lanes::default();
    let panics = Arc::new(AtomicUsize::new(0));
    let count = panics.clone();
    let mut status = lanes
        .start_with_budget(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            move |_, event| {
                if matches!(event, Event::Market(_)) && count.fetch_add(1, Ordering::SeqCst) == 0 {
                    panic!("injected native processing failure");
                }
                Ok(matches!(event, Event::Market(_)))
            },
            Duration::from_secs(1),
        )
        .unwrap();
    let running = status_where(&mut status, |s| s.phase == Phase::Running).await;
    assert_eq!(running.generation, 2);
    assert_eq!(running.episode, 1);
    assert_eq!(panics.load(Ordering::SeqCst), 2);
    lanes
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn reset_during_stalled_auth_does_not_renew_setup_and_stop_does_not_drain_quotes() {
    let fixture = Fixture::new(Mode::AuthStall).await;
    let mut lanes = Lanes::default();
    let started = Instant::now();
    let mut status = lanes
        .start_with_budget(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, _| Ok(false),
            Duration::from_millis(200),
        )
        .unwrap();
    tokio::time::sleep(Duration::from_millis(50)).await;
    lanes.reset(Lane::OptionsGlbx).unwrap();
    status_where(&mut status, |s| s.phase == Phase::Failed).await;
    assert!(started.elapsed() < Duration::from_millis(350));
    lanes
        .shutdown(Deadline::after(Duration::from_secs(1)))
        .await
        .unwrap();
    fixture.stop().await;
}

#[test]
fn validation_rejects_empty_and_protocol_delimited_subscriptions() {
    let mut value = plan(Lane::OptionsGlbx);
    value.subscriptions[0].symbols[0] = "ESZ6|dataset=other".into();
    assert!(value.validate().is_err());
    value.subscriptions.clear();
    assert!(value.validate().is_err());
    assert_eq!(Lane::OptionsGlbx.dataset(), Lane::FuturesGlbx.dataset());
    assert_ne!(Lane::OptionsGlbx, Lane::FuturesGlbx);
    assert_eq!(Lane::FuturesXcbf.dataset(), "XCBF.PITCH");
    assert_eq!(dbn::DBN_VERSION, 3);
}

#[tokio::test]
async fn malformed_metadata_retries_to_readiness_and_stalls_fail_at_original_deadline() {
    for mode in [
        Mode::MetadataBadFirst,
        Mode::MetadataAlwaysBad,
        Mode::MetadataStall,
    ] {
        let fixture = Fixture::new(mode).await;
        let mut lanes = Lanes::default();
        let start = Instant::now();
        let mut status = lanes
            .start_with_budget(
                plan(Lane::OptionsGlbx),
                "x".repeat(32),
                fixture.gateway(),
                |_, event| Ok(matches!(event, Event::Market(_))),
                Duration::from_millis(650),
            )
            .unwrap();
        let outcome = status_where(&mut status, |s| {
            matches!(s.phase, Phase::Running | Phase::Failed)
        })
        .await;
        assert_eq!(outcome.episode, 1);
        if matches!(mode, Mode::MetadataBadFirst) {
            assert_eq!(outcome.phase, Phase::Running);
            assert_eq!(outcome.attempts, 2);
        } else {
            assert_eq!(outcome.phase, Phase::Failed);
            assert!(start.elapsed() >= Duration::from_millis(650));
            assert!(start.elapsed() < Duration::from_secs(2));
        }
        lanes
            .shutdown(Deadline::after(Duration::from_secs(1)))
            .await
            .unwrap();
        tokio::time::timeout(Duration::from_secs(1), async {
            while fixture.closed.load(Ordering::SeqCst) != fixture.accepted.load(Ordering::SeqCst) {
                tokio::task::yield_now().await;
            }
        })
        .await
        .unwrap();
        fixture.stop().await;
    }
}

#[tokio::test]
#[ignore = "real two-minute metadata stall deadline qualification"]
async fn real_120_second_metadata_stall_closes_sdk_socket_at_original_deadline() {
    let fixture = Fixture::new(Mode::MetadataStall).await;
    let gateway = fixture.gateway();
    let token = CancellationToken::new();
    let start = Instant::now();
    let result = Session::open(
        &plan(Lane::OptionsGlbx),
        "x".repeat(32),
        NamedGateway {
            host: &gateway.host,
            port: gateway.port,
            resolver: &gateway.resolver,
        },
        &token,
        Deadline::recovery(),
    )
    .await;
    assert!(matches!(
        result,
        Err(
            ifm_market_data_native::dataset::transport::Failure::Interrupted(
                Interrupted::DeadlineExpired
            )
        )
    ));
    assert!(start.elapsed() >= Duration::from_secs(120));
    assert!(start.elapsed() < Duration::from_secs(123));
    tokio::time::timeout(Duration::from_secs(1), async {
        while fixture.closed.load(Ordering::SeqCst) != 1 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    eprintln!(
        "metadata_stall_deadline_seconds={} closed_sockets=1",
        start.elapsed().as_secs_f64()
    );
    fixture.stop().await;
}

#[tokio::test]
async fn standard_sdk_start_also_uses_bounded_metadata_without_custom_hook() {
    for mode in [Mode::Healthy, Mode::MetadataAlwaysBad] {
        let fixture = Fixture::new(mode).await;
        let mut client = databento::LiveClient::builder()
            .addr([fixture.address].as_slice())
            .await
            .unwrap()
            .key("x".repeat(32))
            .unwrap()
            .dataset("GLBX.MDP3")
            .build()
            .await
            .unwrap();
        client
            .subscribe(
                databento::live::Subscription::builder()
                    .symbols(vec!["ESZ6"])
                    .schema(Schema::Trades)
                    .stype_in(SType::RawSymbol)
                    .build(),
            )
            .await
            .unwrap();
        let result = client.start().await;
        if matches!(mode, Mode::Healthy) {
            assert!(result.is_ok());
            assert!(client.next_record().await.unwrap().is_some());
        } else {
            assert!(result.is_err());
        }
        drop(client);
        tokio::time::timeout(Duration::from_secs(1), async {
            while fixture.closed.load(Ordering::SeqCst) != 1 {
                tokio::task::yield_now().await;
            }
        })
        .await
        .unwrap();
        fixture.stop().await;
    }
}

#[tokio::test]
async fn caller_deadlines_cover_admission_and_pending_resets_cannot_extend_them() {
    let fixture = Fixture::new(Mode::NoAck).await;
    let mut lanes = Lanes::default();
    let start = Instant::now();
    let mut status = lanes
        .start_with_deadline(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, _| Ok(true),
            Deadline::recovery(),
        )
        .unwrap();
    lanes
        .reset_with_deadline(
            Lane::OptionsGlbx,
            Deadline::after(Duration::from_millis(90)),
        )
        .unwrap();
    lanes
        .reset_with_deadline(Lane::OptionsGlbx, Deadline::recovery())
        .unwrap();
    let failed = status_where(&mut status, |s| s.phase == Phase::Failed).await;
    assert_eq!(failed.episode, 1);
    assert!(start.elapsed() >= Duration::from_millis(90));
    assert!(start.elapsed() < Duration::from_millis(400));
    lanes.shutdown(Deadline::recovery()).await.unwrap();
    fixture.stop().await;
    let fixture = Fixture::new(Mode::Healthy).await;
    let mut lanes = Lanes::default();
    let mut status = lanes
        .start_with_deadline(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, _| Ok(true),
            Deadline::after(Duration::ZERO),
        )
        .unwrap();
    status_where(&mut status, |s| s.phase == Phase::Failed).await;
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 0);
    lanes.shutdown(Deadline::recovery()).await.unwrap();
    fixture.stop().await;
}
#[tokio::test]
async fn running_lane_reset_uses_remaining_caller_budget_not_a_new_two_minutes() {
    let fixture = Fixture::new(Mode::HealthyThenNoAck).await;
    let mut lanes = Lanes::default();
    let mut status = lanes
        .start(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, _| Ok(true),
        )
        .unwrap();
    status_where(&mut status, |s| s.phase == Phase::Running).await;
    let start = Instant::now();
    lanes
        .reset_with_deadline(
            Lane::OptionsGlbx,
            Deadline::after(Duration::from_millis(150)),
        )
        .unwrap();
    status_where(&mut status, |s| s.phase == Phase::Recovering).await;
    lanes
        .reset_with_deadline(Lane::OptionsGlbx, Deadline::recovery())
        .unwrap();
    let failed = status_where(&mut status, |s| s.phase == Phase::Failed).await;
    assert_eq!(failed.episode, 2);
    assert!(start.elapsed() >= Duration::from_millis(150));
    assert!(start.elapsed() < Duration::from_millis(500));
    lanes.shutdown(Deadline::recovery()).await.unwrap();
    fixture.stop().await;
}
#[tokio::test]
async fn completed_reset_deadline_does_not_poison_a_later_independent_episode() {
    let fixture = Fixture::new(Mode::Healthy).await;
    let mut lanes = Lanes::default();
    let mut status = lanes
        .start(
            plan(Lane::OptionsGlbx),
            "x".repeat(32),
            fixture.gateway(),
            |_, _| Ok(true),
        )
        .unwrap();
    status_where(&mut status, |s| s.phase == Phase::Running).await;
    lanes
        .reset_with_deadline(
            Lane::OptionsGlbx,
            Deadline::after(Duration::from_millis(100)),
        )
        .unwrap();
    status_where(&mut status, |s| {
        s.phase == Phase::Running && s.generation == 2
    })
    .await;
    tokio::time::sleep(Duration::from_millis(120)).await;
    lanes
        .reset_with_deadline(Lane::OptionsGlbx, Deadline::after(Duration::from_secs(1)))
        .unwrap();
    let ready = status_where(&mut status, |s| {
        s.phase == Phase::Running && s.generation == 3
    })
    .await;
    assert_eq!(ready.episode, 3);
    lanes.shutdown(Deadline::recovery()).await.unwrap();
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn live_abi_runs_real_sdk_and_fences_hot_reads_across_reset_and_disposal() {
    use ifm_market_data_native::ffi::{live::*, runtime::*};
    let fixture = Fixture::new(Mode::Healthy).await;
    let mut handle = 0;
    let config = NativeLiveConfiguration {
        size: 40,
        date: 20261026,
        utc_ticks: 123456789,
        identity_low: 99,
        identity_high: 1,
        capacity: 16,
        advancing: 0,
    };
    // SAFETY: Every pointer is to exclusive aligned local storage or a bounded readable byte slice.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
        assert_eq!(ifm_market_data_live_configure_v1(handle, &config), 0);
        assert_eq!(ifm_market_data_live_configure_v1(handle, &config), 7);
    }
    let body=serde_json::to_vec(&serde_json::json!({"key":"x".repeat(32),"host":"127.0.0.1","port":fixture.address.port(),"dns":["127.0.0.1:1"],"evidence":"observations","subscriptions":[{"schema":"trades","symbology":"raw_symbol","symbols":["ESZ6"]}],"registrations":[{"instrument":42,"contract":"ES20261218C6000","source":"ESZ6","asset":"option","tick":null,"vwap":false,"required":true}]})).unwrap();
    async fn completion(handle: u64, id: u64) -> NativeCompletion {
        tokio::time::timeout(Duration::from_secs(3), async {
            loop {
                let mut records = [NativeCompletion::default(); 1];
                let mut count = 0;
                // SAFETY: Output capacity is one, count and records do not overlap.
                unsafe {
                    assert_eq!(
                        ifm_market_data_runtime_drain_v1(
                            handle,
                            records.as_mut_ptr(),
                            1,
                            &mut count
                        ),
                        0
                    );
                }
                if count == 1 {
                    assert_eq!(records[0].operation, id);
                    return records[0];
                }
                tokio::task::yield_now().await;
            }
        })
        .await
        .unwrap()
    }
    // SAFETY: Start body stays readable for the call; native side copies it before returning.
    unsafe {
        assert_eq!(
            ifm_market_data_live_submit_v1(handle, 1, 0, 3, body.as_ptr(), body.len() as u32, 2000),
            0
        );
    }
    assert_eq!(completion(handle, 1).await.outcome, 0);
    // A drained completion must not let a scalar operation steal an unread control ID.
    assert_eq!(ifm_market_data_submit_treasury_v1(handle, 1, 0.01, 100), 1);
    let mut result = NativeControlResult::default();
    let mut status = NativeLaneStatus::default();
    let mut price = NativeLast::default();
    let id = b"ES20261218C6000";
    // SAFETY: All outputs have the advertised sizes, and contract bytes are readable.
    unsafe {
        assert_eq!(
            ifm_market_data_live_take_v1(handle, 1, 4111, &mut result),
            3
        );
        assert_eq!(
            ifm_market_data_live_take_v1(handle, 1, 4112, &mut result),
            0
        );
        assert_eq!(result.failure, 0);
        assert_eq!(
            ifm_market_data_live_take_v1(handle, 1, 4112, &mut result),
            1
        );
        assert_eq!(
            ifm_market_data_live_status_v1(handle, 3, 64, &mut status),
            0
        );
        assert_eq!(status.phase, 3);
        assert_eq!(
            ifm_market_data_live_last_v1(
                handle,
                3,
                1,
                id.as_ptr(),
                id.len() as u32,
                104,
                &mut price
            ),
            0
        );
        assert_eq!(
            (price.flags, price.price, price.quantity, price.sequence),
            (1, 6_000_250_000_000, 4, 7)
        );
        assert_eq!(
            (price.generation_low, price.generation_high),
            (status.generation_low, status.generation_high)
        );
    }
    let mut events = [NativeMarketEvent::default(); 64];
    let mut count = 0;
    // SAFETY: Output array is aligned, count is separate, capacities are exact.
    unsafe {
        assert_eq!(
            ifm_market_data_live_events_v1(handle, events.as_mut_ptr(), 64, &mut count),
            0
        );
    }
    assert_eq!(count, 1);
    assert_eq!(events[0].kind, 2);
    assert_eq!(events[0].fields[5], 6_000_250_000_000);
    assert_eq!(events[0].fields[6], 4);
    let mut diagnostics = vec![std::mem::MaybeUninit::<NativeDiagnostic>::uninit(); 64];
    let old = (price.generation_low, price.generation_high);
    // Exercise repeated physical socket retirement and new identity, not just a logical reset flag.
    for operation in 2..=1001 {
        // SAFETY: Reset consumes no body and the null input is allowed at zero length.
        unsafe {
            assert_eq!(
                ifm_market_data_live_submit_v1(handle, operation, 1, 3, std::ptr::null(), 0, 2000),
                0
            );
        }
        assert_eq!(completion(handle, operation).await.outcome, 0);
        // SAFETY: The output locals are aligned exclusive records of the stated sizes.
        unsafe {
            assert_eq!(
                ifm_market_data_live_take_v1(handle, operation, 4112, &mut result),
                0
            );
            assert_eq!(result.failure, 0);
            assert_eq!(
                ifm_market_data_live_last_v1(
                    handle,
                    3,
                    1,
                    id.as_ptr(),
                    id.len() as u32,
                    104,
                    &mut price
                ),
                0
            );
        }
        // SAFETY: MaybeUninit storage has exact size/alignment for 64 diagnostics.
        unsafe {
            assert_eq!(
                ifm_market_data_live_diagnostics_v1(
                    handle,
                    diagnostics.as_mut_ptr().cast(),
                    64,
                    &mut count
                ),
                0
            );
        }
        assert_ne!((price.generation_low, price.generation_high), old);
    }
    assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
    std::thread::spawn(move || {
        let mut available = 1;
        // SAFETY: Exclusive aligned local storage. Wait is on a harness blocking thread, never a Tokio owner.
        unsafe {
            loop {
                assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
                if available == 0 {
                    break;
                }
                let mut bits = 0;
                assert_eq!(ifm_market_data_live_signals_v1(handle, &mut bits), 0);
            }
        }
        assert_eq!(available, 0);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    })
    .join()
    .unwrap();
    tokio::time::timeout(Duration::from_secs(2), async {
        while fixture.closed.load(Ordering::SeqCst) != fixture.accepted.load(Ordering::SeqCst) {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1001);
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn live_abi_cancelled_start_exits_socket_and_retains_terminal_result() {
    use ifm_market_data_native::ffi::{live::*, runtime::*};
    let fixture = Fixture::new(Mode::AuthStall).await;
    let mut handle = 0;
    let config = NativeLiveConfiguration {
        size: 40,
        date: 20261026,
        utc_ticks: 0,
        identity_low: 123,
        capacity: 8,
        ..Default::default()
    };
    // SAFETY: Aligned exclusive local outputs and readable configuration.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 1, &mut handle), 0);
        assert_eq!(ifm_market_data_live_configure_v1(handle, &config), 0);
    }
    let body=serde_json::to_vec(&serde_json::json!({"key":"x".repeat(32),"host":"127.0.0.1","port":fixture.address.port(),"dns":["127.0.0.1:1"],"evidence":"observations","subscriptions":[{"schema":"trades","symbology":"raw_symbol","symbols":["ESZ6"]}],"registrations":[{"instrument":42,"contract":"ES20261218C6000","source":"ESZ6","asset":"option","tick":null,"vwap":false,"required":true}]})).unwrap();
    // SAFETY: The native parser copies the bounded readable body before returning.
    unsafe {
        assert_eq!(
            ifm_market_data_live_submit_v1(handle, 1, 0, 3, body.as_ptr(), body.len() as u32, 2000),
            0
        );
    }
    tokio::time::timeout(Duration::from_secs(2), async {
        while fixture.accepted.load(Ordering::SeqCst) == 0 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    assert_eq!(ifm_market_data_runtime_cancel_v1(handle, 1), 0);
    let mut terminal = None;
    tokio::time::timeout(Duration::from_secs(2), async {
        while terminal.is_none() {
            let mut records = [NativeCompletion::default(); 1];
            let mut count = 0;
            // SAFETY: Single-element aligned output and separate count local.
            unsafe {
                assert_eq!(
                    ifm_market_data_runtime_drain_v1(handle, records.as_mut_ptr(), 1, &mut count),
                    0
                );
            }
            if count == 1 {
                terminal = Some(records[0]);
            } else {
                tokio::task::yield_now().await;
            }
        }
    })
    .await
    .unwrap();
    assert_eq!(terminal.unwrap().outcome, 1);
    assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
    std::thread::spawn(move || {
        let mut available = 1;
        // SAFETY: Exclusive local output; wait runs outside every Tokio runtime.
        unsafe {
            loop {
                assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
                if available == 0 {
                    break;
                }
                let mut bits = 0;
                assert_eq!(ifm_market_data_live_signals_v1(handle, &mut bits), 0);
            }
        }
        assert_eq!(available, 0);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 7);
        let mut result = NativeControlResult::default();
        // SAFETY: Aligned exclusive result of the advertised size; retained after completion drain.
        unsafe {
            assert_eq!(
                ifm_market_data_live_take_v1(handle, 1, 4112, &mut result),
                0
            );
        }
        assert_eq!(result.failure, 2);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    })
    .join()
    .unwrap();
    tokio::time::timeout(Duration::from_secs(2), async {
        while fixture.closed.load(Ordering::SeqCst) != 1 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn live_replay_finishes_native_volume_without_exposing_historical_last_prices() {
    verify_live_replay(Mode::Replay, 4, 8).await;
    // More than twice the 8192-entry authoritative output capacity. There is no
    // managed drain until startup completes, exactly as a real epoch bootstrap.
    verify_live_replay(Mode::ReplayFlood, 80_004, 20_008).await;
}
async fn verify_live_replay(mode: Mode, replay_volume: i64, live_sequence: i64) {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(mode).await;
    let owner = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        owner.clone(),
    )
    .unwrap();
    let mut replay = plan(Lane::FuturesGlbx);
    replay.subscriptions[0].replay_start_ns = Some(0);
    data.start(
        Start {
            plan: replay,
            registrations: vec![Registration {
                publisher: 0,
                instrument: 42,
                contract: "ES20261218".into(),
                source: "ESZ6".into(),
                asset: Asset::Futures,
                tick: Some(250000000),
                vwap: true,
                required: true,
            }],
            key: "x".repeat(32),
            gateway: fixture.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(10)),
    )
    .await
    .unwrap();
    let trade = data
        .reader(Lane::FuturesGlbx, "ES20261218")
        .unwrap()
        .unwrap()
        .trade()
        .unwrap()
        .unwrap();
    assert_eq!(trade.sequence, live_sequence);
    assert_eq!(trade.value.size, 6);
    let session = data
        .statistics(Lane::FuturesGlbx, 42, date)
        .unwrap()
        .unwrap();
    assert_eq!(session.volume, replay_volume + 6);
    assert!(session.has_volume());
    let vwap = data.vwap(Lane::FuturesGlbx, 42).unwrap().unwrap();
    assert!(vwap.replay_complete);
    assert_eq!(vwap.volume, replay_volume + 6);
    // Replay completion must cross the same ordered delivery boundary even if
    // no further provider price arrives. Reading a later mutable book would
    // incorrectly attach the live trade's volume to this completion marker.
    use ifm_market_data_native::dataset::live::{Output, OutputSource};
    let empty = Output {
        observed: 0,
        lane: Lane::FuturesGlbx,
        generation: 0,
        source: OutputSource::ReplayComplete(0),
        state: Default::default(),
        trade: None,
    };
    let mut publications: [_; 4] = std::array::from_fn(|_| empty.clone());
    assert_eq!(data.drain_output(&mut publications).unwrap(), 2);
    assert!(matches!(
        publications[0].source,
        OutputSource::ReplayComplete(42)
    ));
    let completed = publications[0].state;
    assert!(
        completed.market.is_none(),
        "Replay must not manufacture a live price"
    );
    assert_eq!(completed.session.unwrap().volume, replay_volume);
    assert!(completed.vwap.unwrap().replay_complete);
    assert_eq!(completed.vwap.unwrap().volume, replay_volume);
    assert_eq!(
        publications[1].state.vwap.unwrap().volume,
        replay_volume + 6
    );
    let wire = ifm_market_data_native::ffi::live::NativeMarketEvent::from(&publications[0]);
    assert_eq!(wire.kind, 5);
    assert_eq!(wire.instrument, 42);
    assert_eq!(
        wire.fields, [0; 12],
        "Control must not invent provider sequence/time"
    );
    let wire =
        ifm_market_data_native::ffi::publication::NativePublication::from_state(wire, completed);
    assert_eq!(wire.update, 5);
    assert_eq!(wire.flags & 1, 0);
    assert_ne!(wire.flags & 8, 0);
    assert_ne!(wire.flags & 16, 0);
    data.stop(
        Lane::FuturesGlbx,
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    fixture.stop().await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn saturated_application_output_causes_visible_lane_recovery_without_touching_other_lanes() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Flood).await;
    let owner = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        owner.clone(),
    )
    .unwrap();
    let healthy = Fixture::new(Mode::Healthy).await;
    for (lane, id) in [
        (Lane::FuturesGlbx, "ES20261218"),
        (Lane::FuturesXcbf, "VX20261218"),
    ] {
        data.start(
            Start {
                plan: plan(lane),
                registrations: vec![Registration {
                    publisher: 0,
                    instrument: 42,
                    contract: id.into(),
                    source: "ESZ6".into(),
                    asset: Asset::Futures,
                    tick: None,
                    vwap: false,
                    required: true,
                }],
                key: "x".repeat(32),
                gateway: healthy.gateway(),
                evidence: Evidence::Observations,
            },
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    }
    data.start(
        Start {
            plan: plan(Lane::OptionsGlbx),
            registrations: vec![Registration {
                publisher: 0,
                instrument: 42,
                contract: "ES20261218C6000".into(),
                source: "ESZ6".into(),
                asset: Asset::Option,
                tick: None,
                vwap: false,
                required: true,
            }],
            key: "x".repeat(32),
            gateway: fixture.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    tokio::time::timeout(Duration::from_secs(3), async {
        loop {
            let status = data.status(Lane::OptionsGlbx).unwrap().unwrap();
            if status.phase == Phase::Recovering {
                assert!(status.error.unwrap().contains("output capacity exhausted"));
                break;
            }
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    assert!(
        data.reader(Lane::OptionsGlbx, "ES20261218C6000")
            .unwrap()
            .is_none()
    );
    let restarted = data
        .reset(
            Lane::FuturesGlbx,
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert_eq!(restarted.generation, 2);
    assert_eq!(restarted.episode, 2);
    assert_eq!(
        data.status(Lane::FuturesXcbf).unwrap().unwrap().generation,
        1
    );
    assert_eq!(
        data.reader(Lane::FuturesGlbx, "ES20261218")
            .unwrap()
            .unwrap()
            .trade()
            .unwrap()
            .unwrap()
            .value
            .price,
        6_000_250_000_000
    );
    let begin = Instant::now();
    assert!(
        data.reset(
            Lane::OptionsGlbx,
            &CancellationToken::new(),
            Deadline::after(Duration::from_millis(120))
        )
        .await
        .is_err()
    );
    assert!(begin.elapsed() < Duration::from_secs(1));
    data.stop(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    fixture.stop().await;
    healthy.stop().await;
}

#[tokio::test]
async fn every_live_auth_text_line_is_bounded_and_drops_oversized_or_invalid_provider_input() {
    for stage in 0..3 {
        for mode in 0..3 {
            let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
            let address = listener.local_addr().unwrap();
            let task = tokio::spawn(async move {
                let (mut socket, _) = listener.accept().await.unwrap();
                if stage > 0 {
                    socket.write_all(b"greeting\n").await.unwrap();
                }
                if stage > 1 {
                    socket
                        .write_all(b"cram=t7kNhwj4xqR0QYjzFKtBEG2ec2pXJ4FK\n")
                        .await
                        .unwrap();
                    let mut reader = BufReader::new(&mut socket);
                    let mut line = String::new();
                    reader.read_line(&mut line).await.unwrap();
                }
                let payload = match mode {
                    0 => vec![b'x'; 65537],
                    1 => vec![0xff, b'\n'],
                    _ => b"truncated".to_vec(),
                };
                let _ = socket.write_all(&payload).await;
                if mode == 2 {
                    socket.shutdown().await.unwrap();
                } else {
                    let mut byte = [0];
                    assert!(matches!(socket.read(&mut byte).await, Ok(0) | Err(_)));
                }
            });
            let gateway = Gateway {
                host: address.ip().to_string(),
                port: address.port(),
                resolver: Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
            };
            let token = CancellationToken::new();
            let result = Session::open(
                &plan(Lane::OptionsGlbx),
                "x".repeat(32),
                NamedGateway {
                    host: &gateway.host,
                    port: gateway.port,
                    resolver: &gateway.resolver,
                },
                &token,
                Deadline::after(Duration::from_secs(1)),
            )
            .await;
            let error = match result {
                Ok(_) => panic!("Invalid auth input succeeded"),
                Err(error) => format!("{error:?}"),
            };
            assert!(!error.contains(&"x".repeat(32)));
            assert!(error.len() < 1024);
            task.await.unwrap();
        }
    }
}

#[tokio::test]
async fn explicit_activation_replaces_terminal_lane_without_dead_reset_receiver_or_lost_identity() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let failing = Fixture::new(Mode::NoAck).await;
    let healthy = Fixture::new(Mode::Healthy).await;
    let owner = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        owner.clone(),
    )
    .unwrap();
    let request = |gateway| Start {
        plan: plan(Lane::OptionsGlbx),
        registrations: vec![Registration {
            publisher: 0,
            instrument: 42,
            contract: "ES20261218C6000".into(),
            source: "ESZ6".into(),
            asset: Asset::Option,
            tick: None,
            vwap: false,
            required: true,
        }],
        key: "x".repeat(32),
        gateway,
        evidence: Evidence::Observations,
    };
    assert!(
        data.start(
            request(failing.gateway()),
            &CancellationToken::new(),
            Deadline::after(Duration::from_millis(80))
        )
        .await
        .is_err()
    );
    let prior = data.status(Lane::OptionsGlbx).unwrap().unwrap();
    let restarted = data
        .start(
            request(healthy.gateway()),
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    assert_eq!(restarted.phase, Phase::Running);
    assert!(restarted.generation > prior.generation);
    assert!(restarted.episode > prior.episode);
    assert_eq!(
        data.reader(Lane::OptionsGlbx, "ES20261218C6000")
            .unwrap()
            .unwrap()
            .trade()
            .unwrap()
            .unwrap()
            .value
            .size,
        4
    );
    assert!(
        data.start(
            request(healthy.gateway()),
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2))
        )
        .await
        .is_err()
    );
    assert_eq!(
        data.status(Lane::OptionsGlbx).unwrap().unwrap().generation,
        restarted.generation
    );
    data.stop(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    failing.stop().await;
    healthy.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn option_quote_flood_retains_latest_without_queue_recovery_or_trade_loss() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Output, OutputSource, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::QuoteFlood).await;
    let owner = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        owner.clone(),
    )
    .unwrap();
    data.start(
        Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: [Schema::Trades, Schema::Mbp1]
                    .into_iter()
                    .map(|schema| SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        schema,
                        symbology: SType::InstrumentId,
                        replay_start_ns: None,
                    })
                    .collect(),
            },
            registrations: vec![
                Registration {
                    publisher: 0,
                    instrument: 42,
                    contract: "ES20261218".into(),
                    source: "42".into(),
                    asset: Asset::Futures,
                    tick: None,
                    vwap: false,
                    required: true,
                },
                Registration {
                    publisher: 0,
                    instrument: 43,
                    contract: "ES20261218C6000".into(),
                    source: "43".into(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                },
            ],
            key: "x".repeat(32),
            gateway: fixture.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(3)),
    )
    .await
    .unwrap();
    tokio::time::timeout(Duration::from_secs(5), async {
        loop {
            let reader = data.raw_quote(Lane::OptionsGlbx, 43).unwrap().unwrap();
            if reader.sequence == 20_000 {
                break;
            }
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
    assert_eq!(
        data.status(Lane::OptionsGlbx).unwrap().unwrap().phase,
        Phase::Running
    );
    let empty = Output {
        observed: 0,
        lane: Lane::OptionsGlbx,
        generation: 0,
        source: OutputSource::ReplayComplete(0),
        state: Default::default(),
        trade: None,
    };
    let mut batch: [_; 16] = std::array::from_fn(|_| empty.clone());
    let count = data.drain_output(&mut batch).unwrap();
    assert_eq!(count, 3);
    assert_eq!(
        batch[..count]
            .iter()
            .filter(|v| matches!(v.source, OutputSource::Observation(Observation::Trade(_))))
            .count(),
        1
    );
    let quotes: Vec<_> = batch[..count]
        .iter()
        .filter_map(|v| match v.source {
            OutputSource::Observation(Observation::Quote(q)) if q.header.instrument == 43 => {
                Some(q.header.sequence)
            }
            _ => None,
        })
        .collect();
    assert_eq!(quotes, vec![20_000]);
    owner.stop().unwrap();
    owner
        .wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
    fixture.stop().await;
}

#[tokio::test]
async fn duplicate_acknowledgements_cannot_claim_a_missing_logical_subscription() {
    let fixture = Fixture::new(Mode::DuplicateAck).await;
    let mut lanes = Lanes::default();
    let mut request = plan(Lane::OptionsGlbx);
    request.subscriptions.push(SubscriptionSpec {
        symbols: vec!["ESZ6".into()],
        symbology: SType::RawSymbol,
        schema: Schema::Mbp1,
        replay_start_ns: None,
    });
    let mut status = lanes
        .start_with_budget(
            request,
            "x".repeat(32),
            fixture.gateway(),
            |_, _| Ok(true),
            Duration::from_millis(250),
        )
        .unwrap();
    loop {
        let next = status.borrow().clone();
        assert_ne!(
            next.phase,
            Phase::Running,
            "Duplicate acknowledgment falsely completed the two-subscription proof"
        );
        if next.phase == Phase::Failed {
            break;
        }
        status.changed().await.unwrap();
    }
    lanes
        .shutdown(Deadline::after(Duration::from_secs(2)))
        .await
        .unwrap();
    fixture.await_closed().await;
    assert_eq!(
        fixture.accepted.load(Ordering::SeqCst),
        fixture.closed.load(Ordering::SeqCst)
    );
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn actual_option_trade_retains_source_underlying_and_reference_before_newer_quotes() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, OutputSource, Registration, Start},
        latest::Asset,
        option_chain::{
            coalesced::{Coalesced, Context},
            manifest::Budget,
            qualification::{Decimal, Request},
        },
        pricing::Request as Model,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::TradeOrdering).await;
    let root = Arc::new(Owner::default());
    let pricing = Arc::new(Coalesced::new(1, 1, 5000).unwrap());
    let strike = Decimal {
        bits: [6000, 0, 0, 0],
    };
    pricing
        .register(
            43,
            1,
            Context {
                identity: 11,
                underlying: 42,
                qualification: Request {
                    size: 304,
                    flags: 3 | 4 | 8 | 16,
                    generation_low: 1,
                    maximum_age: 5000,
                    maximum_skew: 2000,
                    strike,
                    definition_strike: strike,
                    ..Default::default()
                },
                model: Model {
                    forward: 0.0,
                    strike: 6000.0,
                    time: 0.0,
                    rate: 0.04,
                    call: true,
                    american: false,
                    futures_style: false,
                },
                expiration: 30 * 864000000000,
                days_in_year: 365,
                valid_until: 864000000000,
            },
            true,
        )
        .unwrap();
    let manifests = Budget::new(64).unwrap();
    pricing
        .attach_manifest(43, 11, manifests.retain(11, b"source-context").unwrap())
        .unwrap();
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        root.clone(),
    )
    .unwrap()
    .with_option_pricing(pricing.clone())
    .unwrap();
    data.start(
        Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: [Schema::Trades, Schema::Mbp1]
                    .into_iter()
                    .map(|schema| SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        schema,
                        symbology: SType::InstrumentId,
                        replay_start_ns: None,
                    })
                    .collect(),
            },
            registrations: vec![
                Registration {
                    publisher: 0,
                    instrument: 42,
                    contract: "ES20261218".into(),
                    source: "42".into(),
                    asset: Asset::Futures,
                    tick: None,
                    vwap: false,
                    required: true,
                },
                Registration {
                    publisher: 0,
                    instrument: 43,
                    contract: "ES20261218C6000".into(),
                    source: "43".into(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                },
            ],
            key: "x".repeat(32),
            gateway: fixture.gateway(),
            evidence: Evidence::Observations,
        },
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    let mut changes = data.updates();
    tokio::time::timeout(Duration::from_secs(2), async {
        loop {
            if data
                .raw_quote(Lane::OptionsGlbx, 42)
                .unwrap()
                .is_some_and(|q| q.sequence == 10)
            {
                break;
            }
            changes.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    let mut rows: [_; 16] =
        std::array::from_fn(|_| ifm_market_data_native::dataset::live::Output {
            observed: 0,
            lane: Lane::OptionsGlbx,
            generation: 0,
            source: OutputSource::ReplayComplete(0),
            state: Default::default(),
            trade: None,
        });
    let count = data.drain_output(&mut rows).unwrap();
    let frozen=rows[..count].iter().find(|row|matches!(row.source,OutputSource::Observation(Observation::Trade(t))if t.header.instrument==43)).unwrap().trade.as_ref().unwrap();
    assert_eq!(frozen.prepared.context.identity, 11);
    assert_eq!(frozen.prepared.context.model.rate, 0.04);
    assert_eq!(
        frozen.prepared.manifest.as_ref().unwrap().bytes(),
        b"source-context"
    );
    assert_eq!(frozen.underlying.unwrap().sequence, 8);
    assert_eq!(frozen.underlying.unwrap().bid.bits, [6000, 0, 0, 0]);
    assert_eq!(
        data.raw_quote(Lane::OptionsGlbx, 42)
            .unwrap()
            .unwrap()
            .value
            .bid,
        Some(6_100_000_000_000)
    );
    assert_eq!(frozen.at, 0);
    assert!(rows[..count].iter().filter(|row|matches!(row.source,OutputSource::Observation(Observation::Trade(t))if t.header.instrument==42)).all(|row|row.trade.is_none()));
    data.stop(
        Lane::OptionsGlbx,
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    fixture.await_closed().await;
    drop(data);
    drop(pricing);
    assert_eq!(manifests.used(), 14);
    drop(rows);
    assert_eq!(manifests.used(), 0);
    root.stop().unwrap();
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    fixture.stop().await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn wrong_publisher_enters_bounded_recovery_before_book_or_pricing_mutation() {
    use ifm_market_data_native::{
        dataset::live::{Evidence, LiveData, Registration, Start},
        latest::Asset,
        runtime::{clock::Clock, owner::Owner},
    };
    let fixture = Fixture::new(Mode::Pricing).await;
    let root = Arc::new(Owner::default());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let data = LiveData::new(
        date,
        1,
        8,
        Arc::new(Clock::new(0, false).unwrap()),
        root.clone(),
    )
    .unwrap();
    let start = Instant::now();
    let result = data
        .start(
            Start {
                plan: SessionPlan {
                    lane: Lane::OptionsGlbx,
                    subscriptions: [Schema::Trades, Schema::Mbp1]
                        .into_iter()
                        .map(|schema| SubscriptionSpec {
                            symbols: vec!["42".into(), "43".into()],
                            schema,
                            symbology: SType::InstrumentId,
                            replay_start_ns: None,
                        })
                        .collect(),
                },
                registrations: vec![
                    Registration {
                        instrument: 42,
                        publisher: 99,
                        contract: "ES20261218".into(),
                        source: "42".into(),
                        asset: Asset::Futures,
                        tick: None,
                        vwap: false,
                        required: true,
                    },
                    Registration {
                        instrument: 43,
                        publisher: 3,
                        contract: "ES20261218C6000".into(),
                        source: "43".into(),
                        asset: Asset::Option,
                        tick: None,
                        vwap: false,
                        required: true,
                    },
                ],
                key: "x".repeat(32),
                gateway: fixture.gateway(),
                evidence: Evidence::Observations,
            },
            &CancellationToken::new(),
            Deadline::after(Duration::from_millis(600)),
        )
        .await;
    assert!(result.is_err());
    assert!(start.elapsed() < Duration::from_secs(3));
    // Deadline expiry signals cancellation before the source task gets another
    // scheduling turn. Observe its terminal transition rather than racing Drop.
    let mut lifecycle = data.lifecycle(Lane::OptionsGlbx).unwrap().unwrap();
    let status = tokio::time::timeout(Duration::from_secs(2), async {
        loop {
            let status = lifecycle.borrow_and_update().clone();
            if matches!(status.phase, Phase::Failed | Phase::Stopped) {
                break status;
            }
            lifecycle.changed().await.unwrap();
        }
    })
    .await
    .expect("The canceled source must reach a terminal state");
    assert!(
        status
            .root_error
            .as_deref()
            .unwrap_or_default()
            .contains("publisher mismatch")
    );
    assert!(data.raw_quote(Lane::OptionsGlbx, 42).unwrap().is_none());
    assert!(
        fixture.accepted.load(Ordering::Acquire) >= 2,
        "Recovery stopped after its first processing exception"
    );
    root.stop().unwrap();
    root.wait_idle(
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(2)),
    )
    .await
    .unwrap();
    fixture.await_closed().await;
    fixture.stop().await;
}

/// Concrete owned resources, actual pinned SDK DBN framing and actual NATS
/// protocol acknowledgments. There is no FFI stream or managed controller here.
#[tokio::test]
async fn native_dataset_supervisor_rebuilds_actual_futures_and_accepts_explicit_quiet_control() {
    native_futures_owner_case(false, false).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_public_owner_recovers_actual_sdk_and_snapshots_without_managed_orchestration() {
    native_futures_owner_case(true, false).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_public_owner_recovers_quiet_heartbeats_without_claiming_financial_readiness() {
    native_futures_configured_case(true, false, false, true).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Production-duration public native owner recovery and terminal notification at 120 seconds"]
async fn native_public_owner_deadline_returns_terminal_result_and_publishes_notice_without_competing_timer()
 {
    native_futures_owner_case(true, true).await;
}
#[tokio::test]
async fn committed_fixed_futures_are_realized_and_retired_by_native_dataset_control() {
    native_futures_configured_case(true, false, true, false).await;
}
async fn native_futures_owner_case(public_owner: bool, unrecoverable: bool) {
    native_futures_configured_case(public_owner, unrecoverable, false, false).await;
}
async fn native_futures_configured_case(
    public_owner: bool,
    unrecoverable: bool,
    committed: bool,
    quiet_no_quotes: bool,
) {
    use ifm_market_data_native::{
        dataset::{
            futures_owner::{FuturesFactory, Intent},
            live::{Evidence, Registration, Start},
            supervisor::Supervisor,
        },
        latest::Asset,
        messaging::publisher::BrokerSettings,
        runtime::{
            clock::Clock,
            owner::Owner,
            recovery::{Phase as RecoveryPhase, Proof, Requirements},
        },
    };
    let fixture = Fixture::new(if unrecoverable {
        Mode::MetadataStall
    } else if quiet_no_quotes {
        Mode::AckOnly
    } else if committed {
        Mode::FuturesQuotes
    } else {
        Mode::HealthyHeartbeat
    })
    .await;
    let quiet = Fixture::new(Mode::AckOnly).await;
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let stopping = CancellationToken::new();
    let stop = stopping.clone();
    let notice = Arc::new(tokio::sync::Notify::new());
    let notification = notice.clone();
    let terminal_notices = Arc::new(AtomicUsize::new(0));
    let notice_count = terminal_notices.clone();
    let broker = tokio::spawn(async move {
        let mut jobs = tokio::task::JoinSet::new();
        loop {
            tokio::select! {
                ()=stop.cancelled()=>break,
                accepted=listener.accept()=>{
                    let (socket,_)=accepted.unwrap();
                    let notification = notification.clone(); let notice_count=notice_count.clone();
                    jobs.spawn(async move{
                        let (read,mut write)=socket.into_split();let mut read=BufReader::new(read);
                        if write.write_all(b"INFO {\"server_id\":\"native-factory\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.is_err(){return;}
                        let mut line=String::new();
                        loop{
                            line.clear();if read.read_line(&mut line).await.unwrap_or(0)==0{break;}
                            if line.starts_with("PING"){
                                if write.write_all(b"PONG\r\n").await.is_err(){break;}
                            }else if line.starts_with("PUB "){
                                let count=line.split_whitespace().last().unwrap().parse::<usize>().unwrap();assert!(count<=1024*1024);
                                let mut bytes=vec![0;count+2];if read.read_exact(&mut bytes).await.is_err(){break;}
                                if line.split_whitespace().nth(1).is_some_and(|subject| subject.starts_with("Event.MarketDataFeedEvent.DatasetRecoveryFailed.")) {
                                    let value=rmpv::decode::read_value(&mut std::io::Cursor::new(&bytes[..count])).unwrap();
                                    let outer=value.as_array().unwrap();
                                    assert_eq!(outer.len(),2);
                                    let rmpv::Value::Ext(98,size)=&outer[0] else {panic!("Expected existing LZ4BlockArray envelope");};
                                    let length=rmpv::decode::read_value(&mut std::io::Cursor::new(size)).unwrap().as_u64().unwrap() as usize;
                                    assert!(length<=16384);
                                    let decoded=lz4_flex::block::decompress(outer[1].as_slice().unwrap(),length).unwrap();
                                    let inner=rmpv::decode::read_value(&mut std::io::Cursor::new(decoded)).unwrap();
                                    let event=inner.as_array().unwrap();assert_eq!(event.len(),20);
                                    assert_eq!(event[16].as_str(),Some("GLBX.MDP3"));
                                    assert_eq!(event[17].as_str(),Some("Futures"));
                                    notice_count.fetch_add(1,Ordering::SeqCst);notification.notify_one();
                                }
                            }
                        }
                    });
                },
                result=jobs.join_next(),if !jobs.is_empty()=>{result.unwrap().unwrap();}
            }
        }
        jobs.abort_all();
        while let Some(result) = jobs.join_next().await {
            assert!(result.is_ok() || result.unwrap_err().is_cancelled());
        }
    });
    // Discovery runs inside the same replaceable native group as the SDK.
    // Every dataset rebuild must fetch a complete raw definition before opening
    // its replacement source, while the independent quiet lane remains running.
    let metadata = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let metadata_endpoint = format!("http://{}/", metadata.local_addr().unwrap());
    let metadata_stop = CancellationToken::new();
    let metadata_stopping = metadata_stop.clone();
    let metadata_peer = tokio::spawn(async move {
        let baseline: serde_json::Value =
            serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
        let mut definition: serde_json::Value =
            serde_json::from_str(baseline[0]["json"].as_str().unwrap()).unwrap();
        definition["hd"]["publisher_id"] = 3.into();
        loop {
            let accepted = tokio::select! {biased;()=metadata_stopping.cancelled()=>break,accepted=metadata.accept()=>accepted.unwrap()};
            let (mut socket, _) = accepted;
            let mut header = Vec::new();
            let mut byte = [0];
            while !header.ends_with(b"\r\n\r\n") {
                if socket.read_exact(&mut byte).await.is_err() {
                    break;
                }
                header.push(byte[0]);
                assert!(header.len() < 8192);
            }
            let header = String::from_utf8(header).unwrap();
            let length = header
                .lines()
                .find_map(|line| {
                    line.to_lowercase()
                        .strip_prefix("content-length:")
                        .map(|v| v.trim().parse::<usize>().unwrap())
                })
                .unwrap_or(0);
            assert!(length < 8192);
            let mut form = vec![0; length];
            socket.read_exact(&mut form).await.unwrap();
            let response = if header.starts_with("GET ") {
                r#"{"start":"2025-01-01T00:00:00Z","end":"2026-10-27T00:00:00Z","schema":{"definition":{"start":"2025-01-01T00:00:00Z","end":"2026-10-27T00:00:00Z"}}}"#.to_owned()
            } else {
                format!("{}\n", serde_json::to_string(&definition).unwrap())
            };
            socket
                .write_all(
                    format!(
                        "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                        response.len(),
                        response
                    )
                    .as_bytes(),
                )
                .await
                .unwrap();
        }
    });
    let root = Arc::new(Owner::default());
    let clock = Arc::new(Clock::new(0, false).unwrap());
    let factory = Arc::new(FuturesFactory::new(clock.clone()));
    let quiet_factory = Arc::new(FuturesFactory::new(clock));
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let registration = Registration {
        publisher: 3,
        instrument: 42,
        contract: "ES20261218".into(),
        source: "ESZ6".into(),
        asset: Asset::Futures,
        tick: None,
        vwap: false,
        required: true,
    };
    let make = |gateway, evidence, lane| {
        Arc::new(Intent {
            bootstrap: None,
            date,
            definition: date,
            source: Start {
                plan: if committed && lane == Lane::FuturesGlbx {
                    let mut source = plan(lane);
                    source.subscriptions.push(SubscriptionSpec {
                        symbols: vec!["ESZ6".into()],
                        symbology: SType::RawSymbol,
                        schema: Schema::Mbp1,
                        replay_start_ns: None,
                    });
                    source
                } else {
                    plan(lane)
                },
                registrations: vec![registration.clone()],
                key: "x".repeat(32),
                gateway,
                evidence,
            },
            broker: BrokerSettings::new(
                format!("nats://{address}"),
                vec!["127.0.0.1:1".parse().unwrap()],
            )
            .unwrap(),
            scope: "development".into(),
            revision: 1,
        })
    };
    let mut intent = make(fixture.gateway(), Evidence::Observations, Lane::FuturesGlbx);
    Arc::get_mut(&mut intent).unwrap().bootstrap = Some(Arc::new(
        ifm_market_data_native::dataset::futures_bootstrap::Discovery {
            history: Arc::new(
                ifm_market_data_native::historical::History::new(
                    &metadata_endpoint,
                    "x".repeat(32),
                    Arc::new(
                        ifm_market_data_native::dns::Resolver::new(vec![
                            "127.0.0.1:1".parse().unwrap(),
                        ])
                        .unwrap(),
                    ),
                )
                .unwrap(),
            ),
            roots: vec![ifm_market_data_native::dataset::futures_bootstrap::Root {
                symbol: "ES".into(),
                count: 1,
                fallback: Some("USD".into()),
                vwap: false,
            }],
            fixed: vec![],
            session_replay: false,
        },
    ));
    if public_owner {
        use ifm_market_data_native::api::owner::{MarketData, RecoveryRequest};
        let market = Arc::new(
            MarketData::new(root.clone(), Arc::new(Clock::new(0, false).unwrap()), None).unwrap(),
        );
        let started = Instant::now();
        market.start_futures_once(intent.clone(), [21; 32]).unwrap();
        market
            .start_futures(make(
                quiet.gateway(),
                Evidence::ControlOnly,
                Lane::FuturesXcbf,
            ))
            .unwrap();
        if unrecoverable || quiet_no_quotes {
            market.wait_ready(Lane::FuturesXcbf, false).await.unwrap();
            let sibling = market.health(Lane::FuturesXcbf).status().ticket;
            let mut changed = market.health(Lane::FuturesGlbx).subscribe();
            let identity = tokio::time::timeout(Duration::from_secs(5), async {
                loop {
                    if let Some(identity) = market.publication_identity(Lane::FuturesGlbx) {
                        break identity;
                    }
                    changed.changed().await.unwrap();
                }
            })
            .await
            .unwrap();
            let request = RecoveryRequest {
                lane: Lane::FuturesGlbx,
                operation: [31; 16],
                worker: identity.worker,
                generation: identity.generation,
                date,
            };
            let rejected = market.begin_recovery(&request).unwrap();
            assert!(rejected.is_none());
            let (send, receive) = tokio::sync::oneshot::channel();
            let observing = market.clone();
            root.submit_observer(&tokio::runtime::Handle::current(), 901, async move {
                let result = observing.recovery_result(&request, rejected).await.unwrap();
                let _ = send.send(result);
                Ok(1.0)
            })
            .unwrap();
            let terminal = if quiet_no_quotes {
                // Heartbeats prove transport health even when no quote arrives.
                // Financial readiness is deliberately a separate result field.
                tokio::time::timeout(Duration::from_secs(6), receive)
                    .await
                    .unwrap_or_else(|_| panic!("Quiet final source did not become connected: health={:?}; accepted={}; contracts={:?}", market.health(Lane::FuturesGlbx).status(),fixture.accepted.load(Ordering::SeqCst),market.futures_contracts(Lane::FuturesGlbx)))
                    .unwrap()
            } else {
                // Observe the original incident deadline independently of the
                // public waiter. The supervisor owns its one final reset phase.
                tokio::time::timeout(
                    Duration::from_secs(124).saturating_sub(started.elapsed()),
                    notice.notified(),
                )
                .await
                .unwrap();
                let elapsed = started.elapsed();
                assert!(
                    elapsed >= Duration::from_secs(120) && elapsed < Duration::from_secs(124),
                    "{elapsed:?}"
                );
                assert!(terminal_notices.load(Ordering::SeqCst) > 0);
                tokio::time::timeout(
                    Duration::from_secs(244).saturating_sub(started.elapsed()),
                    receive,
                )
                .await
                .unwrap()
                .unwrap()
            };
            let result = terminal.as_array().unwrap();
            assert_eq!(result[3].as_bool(), Some(quiet_no_quotes));
            assert_eq!(result[4].as_bool(), Some(quiet_no_quotes));
            assert_eq!(result[5].as_bool(), Some(false));
            assert_eq!(result[6].is_nil(), quiet_no_quotes);
            assert_eq!(market.health(Lane::FuturesXcbf).status().ticket, sibling);
            if quiet_no_quotes {
                assert!(market.health(Lane::FuturesGlbx).connection_alive());
                assert!(market.health(Lane::FuturesGlbx).status().deadline.is_none(), "Quiet final subscriptions must finish setup without inventing a financial publication");
                assert!(market.failures().is_empty());
                assert_eq!(terminal_notices.load(Ordering::SeqCst), 0);
                let replacement = market.publication_identity(Lane::FuturesGlbx).unwrap();
                assert_ne!(replacement.generation, identity.generation);
            } else {
                assert!(!market.failures().is_empty());
            }
            market.shutdown(Deadline::recovery()).await.unwrap();
            root.stop().unwrap();
            root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            use ifm_market_data_native::runtime::owner::{Completion, Outcome};
            let mut completions = [Completion {
                operation: 0,
                outcome: Outcome::InternalFailure,
                value: 0.0,
            }; 1];
            assert_eq!(root.drain(&mut completions).unwrap(), 1);
            assert_eq!(completions[0].operation, 901);
            assert_eq!(
                completions[0].outcome,
                Outcome::Success,
                "The observer must deliver the terminal DTO, not lose it to a competing deadline"
            );
            assert!(root.disposable().unwrap());
            fixture.await_closed().await;
            quiet.await_closed().await;
            stopping.cancel();
            broker.await.unwrap();
            fixture.stop().await;
            quiet.stop().await;
            metadata_stop.cancel();
            metadata_peer.await.unwrap();
            return;
        }
        for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
            tokio::time::timeout(Duration::from_secs(4), market.wait_ready(lane, false))
                .await
                .unwrap()
                .unwrap();
        }
        if committed {
            use ifm_market_data_native::option_chain::{
                committed::Book, composition::capture::Request,
            };
            let before = market.futures_contracts(Lane::FuturesGlbx).unwrap();
            let canonical = before.as_array().unwrap()[0].as_array().unwrap()[0]
                .as_str()
                .unwrap()
                .to_owned();
            let plan = "c".repeat(64);
            let source = serde_json::json!({"schema":1,"authority":"IFM","dataset":"GLBX.MDP3","revision":1,"digest":"a".repeat(64),
                "authorities":[{"id":"position:fixed","version":1,"event_id":"00112233445566778899aabbccddeeff","digest":"b".repeat(64),"status":0,"reason":"CommittedActive","workflow_type":"Position","workflow_id":"fixed","leg_id":"future","leases":[["112233445566778899aabbccddeeff00",1,4,"Databento","GLBX.MDP3",canonical,"mbp-1",0,null,plan]]}],
                "plans":[{"id":plan,"schema":1,"dataset":"GLBX.MDP3","maturity":0,"candidates":[],"futures":[[canonical,"ES","GLBX.MDP3","XCME","USD",18000000000000000i64,0,"50","0.25","d".repeat(64)]],"routes":[[canonical,"ESZ6",1,"ES","GLBX.MDP3",false,false]],"separate":false,"independent":false}]});
            let book = Book::parse(&serde_json::to_vec(&source).unwrap(), "IFM").unwrap();
            let sibling = market.health(Lane::FuturesXcbf).status().ticket;
            let first = market.publication_identity(Lane::FuturesGlbx).unwrap();
            let deadline = Deadline::recovery();
            tokio::time::timeout(
                Duration::from_secs(5),
                market.realize_committed_futures(&book, &CancellationToken::new(), deadline),
            )
            .await
            .unwrap()
            .unwrap();
            let fresh = market.publication_identity(Lane::FuturesGlbx).unwrap();
            assert_ne!(fresh.generation, first.generation);
            assert_eq!(market.health(Lane::FuturesXcbf).status().ticket, sibling);
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2);
            let request = Request {
                id: [98; 16],
                scope: plan.clone(),
                horizon: "Daily".into(),
                generation: fresh.generation,
                at: -621355968000000000,
                until: 1_200_000_000,
                maximum: 1,
                age: 5000,
                skew: 1000,
                options: false,
                selection: false,
                allow_missing: false,
                risk: Default::default(),
            };
            let snapshot = tokio::time::timeout(
                Duration::from_secs(6),
                market.capture_futures_ready(
                    request,
                    book.plans[&plan].futures.clone(),
                    false,
                    &CancellationToken::new(),
                    deadline,
                ),
            )
            .await
            .expect("The futures fixture must supply current bid/ask quotes")
            .unwrap();
            assert_eq!(snapshot.instruments.len(), 1);
            assert_eq!(snapshot.instruments[0].instrument.contract, canonical);
            market
                .realize_committed_futures(&book, &CancellationToken::new(), deadline)
                .await
                .unwrap();
            assert_eq!(
                fixture.accepted.load(Ordering::SeqCst),
                2,
                "An identical committed recipe must not reset a healthy source"
            );
            let mut empty = source;
            empty["revision"] = 2.into();
            empty["plans"] = serde_json::json!([]);
            empty["authorities"][0]["version"] = 2.into();
            empty["authorities"][0]["status"] = 1.into();
            empty["authorities"][0]["leases"] = serde_json::json!([]);
            let empty = Book::parse(&serde_json::to_vec(&empty).unwrap(), "IFM").unwrap();
            tokio::time::timeout(
                Duration::from_secs(5),
                market.realize_committed_futures(&empty, &CancellationToken::new(), deadline),
            )
            .await
            .unwrap()
            .unwrap();
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 3);
            assert!(market.has_desired(Lane::FuturesGlbx));
            assert_eq!(market.health(Lane::FuturesXcbf).status().ticket, sibling);
            market.shutdown(Deadline::recovery()).await.unwrap();
            root.stop().unwrap();
            root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            assert!(root.disposable().unwrap());
            fixture.await_closed().await;
            quiet.await_closed().await;
            stopping.cancel();
            broker.await.unwrap();
            fixture.stop().await;
            quiet.stop().await;
            metadata_stop.cancel();
            metadata_peer.await.unwrap();
            return;
        }
        let stable_start = market.health(Lane::FuturesGlbx).status().ticket;
        market.start_futures_once(intent.clone(), [21; 32]).unwrap();
        assert_eq!(
            market.health(Lane::FuturesGlbx).status().ticket,
            stable_start
        );
        assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
        let mut wrong_date = make(fixture.gateway(), Evidence::Observations, Lane::FuturesGlbx);
        Arc::get_mut(&mut wrong_date).unwrap().date = date.next_day().unwrap();
        assert!(market.start_futures_once(wrong_date, [22; 32]).is_err());
        assert_eq!(
            market.health(Lane::FuturesGlbx).status().ticket,
            stable_start
        );
        assert!(market.root_price("ES").unwrap().is_some());
        assert_eq!(
            market
                .contract_price("unknown", false, 9_000_000_000)
                .unwrap(),
            (false, None)
        );
        let before = market.read_snapshot(true).unwrap();
        let fields = before.as_array().unwrap();
        assert_eq!(fields.len(), 8);
        assert_eq!(fields[1].as_array().unwrap().len(), 1);
        assert_eq!(fields[2].as_array().unwrap().len(), 1);
        assert!(!fields[3].as_array().unwrap().is_empty());
        let canonical = fields[2].as_array().unwrap()[0].as_array().unwrap()[0]
            .as_str()
            .unwrap();
        assert!(market.stream_active(canonical).unwrap());
        assert!(
            market
                .change_stream_route(17, canonical, false, true)
                .unwrap()
        );
        assert!(
            !market
                .change_stream_route(17, canonical, false, true)
                .unwrap()
        );
        assert!(
            market
                .change_stream_route(18, canonical, false, true)
                .unwrap()
        );
        assert!(
            market
                .change_stream_route(17, canonical, false, false)
                .unwrap()
        );
        assert!(
            !market
                .change_stream_route(17, canonical, false, false)
                .unwrap()
        );
        assert!(
            market
                .change_stream_route(19, "unknown", false, true)
                .is_err()
        );
        let current = market.current_snapshot(canonical, false).unwrap();
        let current = current.as_array().unwrap();
        assert_eq!(current.len(), 7);
        assert_eq!(current[0].as_str(), Some(canonical));
        let statistics = market.current_snapshot(canonical, true).unwrap();
        assert!(
            statistics.is_nil()
                || statistics
                    .as_array()
                    .is_some_and(|fields| fields.len() == 9)
        );
        assert!(market.current_snapshot("unknown", false).unwrap().is_nil());
        if let Ok(path) = std::env::var("IFM_NATIVE_CURRENT_OUTPUT") {
            std::fs::write(
                path,
                ifm_market_data_native::messaging::codec::encode(&rmpv::Value::Array(vec![
                    rmpv::Value::Array(current.clone()),
                    statistics,
                ]))
                .unwrap(),
            )
            .unwrap();
        }

        let old = market.futures_current(Lane::FuturesGlbx).unwrap();
        let old_frames = old.capture_all(1024 * 1024).unwrap();
        assert!(!old_frames.is_empty());
        let identity = market.publication_identity(Lane::FuturesGlbx).unwrap();
        let quiet_ticket = market.health(Lane::FuturesXcbf).status().ticket;
        let request = RecoveryRequest {
            lane: Lane::FuturesGlbx,
            operation: [9; 16],
            worker: identity.worker,
            generation: identity.generation,
            date,
        };
        let rejected = market.begin_recovery(&request).unwrap();
        assert!(rejected.is_none());
        let recovered = tokio::time::timeout(
            Duration::from_secs(4),
            async {
                let result = market.recovery_result(&request, rejected);
                tokio::pin!(result);
                loop {
                    tokio::select! {
                        value = &mut result => break value,
                        () = tokio::task::yield_now() => {
                            // Reconstruction may temporarily remove all prices,
                            // but cannot turn a known canonical ID into NotFound.
                            assert!(market.contract_price(canonical, false, 9_000_000_000).unwrap().0);
                            assert_eq!(market.contract_price("unknown", false, 9_000_000_000).unwrap(), (false, None));
                        }
                    }
                }
            },
        )
        .await
        .unwrap()
        .unwrap();
        let recovered = recovered.as_array().unwrap();
        assert_eq!(recovered.len(), 9);
        assert_eq!(recovered[3].as_bool(), Some(true));
        assert_eq!(recovered[4].as_bool(), Some(true));
        // Transport recovery may finish before the next financial publication.
        // Verify financial readiness separately instead of treating quiet quotes
        // as a provider failure or requiring the public transport waiter to hang.
        tokio::time::timeout(
            Duration::from_secs(4),
            market.wait_ready(Lane::FuturesGlbx, false),
        )
        .await
        .unwrap()
        .unwrap();
        let financially_ready = market.recovery_result(&request, None).await.unwrap();
        assert_eq!(
            financially_ready.as_array().unwrap()[5].as_bool(),
            Some(true)
        );
        let replacement = market.publication_identity(Lane::FuturesGlbx).unwrap();
        assert_ne!(replacement.worker, identity.worker);
        assert_ne!(replacement.generation, identity.generation);
        assert!(
            !market
                .change_stream_route(18, canonical, false, true)
                .unwrap(),
            "Physical reconstruction must retain transient stream-owner intent"
        );

        assert_eq!(
            market.health(Lane::FuturesXcbf).status().ticket,
            quiet_ticket
        );
        assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2);
        assert_eq!(quiet.accepted.load(Ordering::SeqCst), 1);
        let stable_ticket = market.health(Lane::FuturesGlbx).status().ticket;
        assert!(market.begin_recovery(&request).unwrap().is_none());
        let duplicate = market.recovery_result(&request, None).await.unwrap();
        assert_eq!(duplicate.as_array().unwrap()[3].as_bool(), Some(true));
        assert_eq!(
            market.health(Lane::FuturesGlbx).status().ticket,
            stable_ticket
        );
        assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2);
        let invalid = RecoveryRequest {
            operation: [10; 16],
            worker: replacement.worker,
            generation: replacement.generation,
            date: date.next_day().unwrap(),
            ..request.clone()
        };
        let rejected = market.begin_recovery(&invalid).unwrap();
        assert!(rejected.is_some());
        assert_eq!(
            market
                .recovery_result(&invalid, rejected)
                .await
                .unwrap()
                .as_array()
                .unwrap()[3]
                .as_bool(),
            Some(false)
        );
        assert_eq!(
            market.health(Lane::FuturesGlbx).status().ticket,
            stable_ticket
        );
        let current = market.read_snapshot(true).unwrap();
        let frames = current.as_array().unwrap()[3].as_array().unwrap();
        assert!(!frames.is_empty());
        assert_eq!(old_frames[0].generation, identity.generation);
        assert!(
            market.wait_feeds_stopped().await.is_err(),
            "Active work is not stopped"
        );
        market.stop_feeds().unwrap();
        tokio::time::timeout(Duration::from_secs(4), market.wait_feeds_stopped())
            .await
            .unwrap()
            .unwrap();
        fixture.await_closed().await;
        quiet.await_closed().await;
        assert!(!market.has_desired(Lane::FuturesGlbx));
        assert!(market.futures_current(Lane::FuturesGlbx).is_none());
        market.start_futures_once(intent.clone(), [21; 32]).unwrap();
        tokio::time::timeout(
            Duration::from_secs(4),
            market.wait_ready(Lane::FuturesGlbx, true),
        )
        .await
        .unwrap()
        .unwrap();
        assert_eq!(
            fixture.accepted.load(Ordering::SeqCst),
            3,
            "A previously accepted command must create a fresh source after explicit stop"
        );
        market.stop_feeds().unwrap();
        tokio::time::timeout(Duration::from_secs(4), market.wait_feeds_stopped())
            .await
            .unwrap()
            .unwrap();
        market.shutdown(Deadline::recovery()).await.unwrap();
        root.stop().unwrap();
        root.wait_idle(&CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
        assert!(root.disposable().unwrap());
        fixture.await_closed().await;
        quiet.await_closed().await;
        stopping.cancel();
        broker.await.unwrap();
        fixture.stop().await;
        quiet.stop().await;
        metadata_stop.cancel();
        metadata_peer.await.unwrap();
        return;
    }
    let required = Requirements::new(&[Proof::Transport, Proof::Publication]);
    let controller = Supervisor::start(
        root.clone(),
        factory.clone(),
        Some(intent.clone()),
        required,
        Deadline::after(Duration::from_secs(3)),
    )
    .unwrap();
    let quiet_controller = Supervisor::start(
        root.clone(),
        quiet_factory.clone(),
        Some(make(
            quiet.gateway(),
            Evidence::ControlOnly,
            Lane::FuturesXcbf,
        )),
        required,
        Deadline::after(Duration::from_secs(3)),
    )
    .unwrap();
    async fn running<T: Send + Sync + 'static>(controller: &Supervisor<T>) {
        let health = controller.recovery();
        let mut changed = health.subscribe();
        tokio::time::timeout(Duration::from_secs(4), async {
            loop {
                if health.status().phase == RecoveryPhase::Running {
                    break;
                }
                changed.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
    }
    running(&controller).await;
    running(&quiet_controller).await;
    let contracts = factory.contracts().unwrap();
    assert_eq!(contracts.len(), 1);
    assert_eq!(
        contracts[0].as_array().unwrap()[0].as_str(),
        Some("ES20261225")
    );
    assert!(quiet_factory.contracts().is_none());
    let old = factory.current().unwrap();
    let before = old.capture(0, 128).unwrap();
    assert!(!before.is_empty());
    let previous = before[0].generation;
    let identity = factory.identity().unwrap();
    assert_eq!(identity.worker, before[0].worker);
    assert_eq!(identity.generation, previous);
    assert_eq!(identity.revision, before[0].manifest_revision);
    assert!(
        quiet_factory
            .current()
            .unwrap()
            .capture(0, 128)
            .unwrap()
            .is_empty()
    );
    let quiet_owner = quiet_controller.recovery().status().ticket;
    controller.apply(Some(intent), required).unwrap();
    running(&controller).await;
    let current = factory.current().unwrap().capture(0, 128).unwrap();
    assert!(!current.is_empty());
    assert_ne!(previous, current[0].generation);
    assert_eq!(old.capture(0, 128).unwrap()[0].generation, previous);
    assert_eq!(quiet_controller.recovery().status().ticket, quiet_owner);
    assert_eq!(quiet.accepted.load(Ordering::SeqCst), 1);
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2);
    root.stop().unwrap();
    controller.join(Deadline::recovery()).await.unwrap();
    quiet_controller.join(Deadline::recovery()).await.unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.await_closed().await;
    quiet.await_closed().await;
    stopping.cancel();
    broker.await.unwrap();
    fixture.stop().await;
    quiet.stop().await;
    metadata_stop.cancel();
    metadata_peer.await.unwrap();
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_options_group_owns_preparation_pricing_storage_publication_and_whole_dataset_rebuild()
 {
    native_options_group_case(false, false, false, false, false, false, false, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Production-duration native options raw-reference 120-second deadline"]
async fn native_options_reference_stall_fails_once_at_original_120_seconds_and_joins() {
    native_options_group_case(true, false, false, false, false, false, false, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_options_stage_missing_reviewed_metadata_inside_owned_recovery() {
    native_options_group_case(false, true, false, false, false, false, false, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_public_option_owner_captures_risk_and_rebuilds_while_old_query_data_is_retained() {
    native_options_group_case(false, true, true, false, false, false, false, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_background_empty_policy_is_idle_then_trade_recovery_reuses_native_facts_with_database_stalled()
 {
    native_options_group_case(false, true, true, true, false, false, false, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_background_published_policy_owns_its_quotes_plan_and_cached_recovery() {
    native_options_group_case(false, true, true, true, true, false, false, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_background_refresh_does_not_reset_unchanged_sources_and_retires_removed_policy() {
    native_options_group_case(false, true, true, true, true, true, false, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_canonical_monitor_resolves_under_owner_and_rebuilds_without_storage() {
    native_options_group_case(false, true, true, true, false, false, true, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_canonical_first_lookup_stall_cannot_pin_stop_or_resurrect_released_feed() {
    native_options_group_case(true, true, true, true, false, false, true, 0).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_discovery_owns_exact_qualification_contexts_and_recovery_without_managed_leases() {
    native_options_group_case(false, true, true, true, false, false, false, 1).await;
}
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_discovery_rejects_incomplete_duplicate_and_wrong_version_without_stopping_siblings()
{
    for mode in [2, 3, 4] {
        native_options_group_case(false, true, true, true, false, false, false, mode).await;
    }
}
#[cfg(target_os = "windows")]
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Explicit actual-DLL/.NET qualification; build the native DLL and NativeOwnershipBridge first"]
async fn native_discovery_crosses_actual_csharp_ffi_sdk_and_broker_boundaries() {
    native_options_group_case(false, true, true, true, false, false, false, 5).await;
}
// Named test cases above share one complete SDK/broker/storage fixture.
#[tokio::test(flavor = "multi_thread", worker_threads = 4)]
#[cfg(windows)]
#[ignore = "Explicit actual-DLL/.NET preparation acceptance; build OwnerPreparationBridge first"]
async fn native_preparation_rebuilds_while_the_csharp_business_commit_is_stalled() {
    native_options_group_case(false, true, true, true, false, false, false, 6).await;
}

#[tokio::test]
async fn native_committed_option_ownership_rebuilds_without_managed_journal_responder() {
    native_options_group_case(false, true, true, true, false, false, false, 7).await;
}

#[tokio::test]
async fn native_browser_views_capture_release_and_rebuild_without_managed_leases() {
    native_options_group_case(false, true, true, true, false, false, false, 8).await;
}

#[tokio::test]
async fn native_browser_metadata_maintenance_preserves_unchanged_provider_connection() {
    native_options_group_case(false, true, true, true, false, false, false, 10).await;
}

#[cfg(windows)]
#[tokio::test(flavor = "multi_thread", worker_threads = 4)]
#[ignore = "Actual DLL browser acceptance: build current DLL and OwnerBrowserBridge first"]
async fn native_browser_crosses_actual_public_csharp_api_ffi_and_sdk_boundaries() {
    native_options_group_case(false, true, true, true, false, false, false, 9).await;
}

#[allow(clippy::too_many_arguments)]
async fn native_options_group_case(
    stall_reference: bool,
    stage_reference: bool,
    public_owner: bool,
    native_background: bool,
    populated_background: bool,
    refresh_background: bool,
    canonical_monitor: bool,
    discovery_mode: u8,
) {
    // The DLL's nonblocking panic hook intentionally avoids stderr. Preserve
    // fixture assertion evidence when another concurrently running ABI test has
    // installed that hook in this test executable.
    struct FailureEvidence;
    impl Drop for FailureEvidence {
        fn drop(&mut self) {
            if std::thread::panicking() {
                for record in ifm_market_data_native::runtime::panic_capture::capture().2 {
                    eprintln!(
                        "Native captured panic {}:{}: {}",
                        record.file(),
                        record.line(),
                        record.message()
                    );
                }
            }
        }
    }
    let _failure_evidence = FailureEvidence;
    use base64::Engine;
    use ifm_market_data_native::{
        dataset::{
            live::{Evidence, Registration, Start},
            options_owner::{Intent, OptionsFactory},
            supervisor::Supervisor,
        },
        latest::Asset,
        messaging::{codec, publisher::BrokerSettings},
        option_chain::{
            convention::Convention,
            manifest::Budget,
            preparation::{Blueprint, Definition, References, ReviewedScope},
        },
        reference::{
            calendar::Calendar,
            decimal,
            treasury_curve::Treasury,
            treasury_policy::{Conversion, Publication, Publications},
        },
        runtime::{
            clock::Clock,
            owner::Owner,
            recovery::{Phase as RecoveryPhase, Requirements},
        },
    };
    let oracle: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let fixture_index = if stage_reference { 4 } else { 0 };
    let at = oracle[fixture_index]["at"].as_i64().unwrap();
    let bytes = base64::engine::general_purpose::STANDARD
        .decode(oracle[fixture_index]["contract"].as_str().unwrap())
        .unwrap();
    let mut contract = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
    let rmpv::Value::Array(ref mut fields) = contract else {
        panic!()
    };
    fields[4] = 43.into();
    let fixture = Fixture::new(if populated_background {
        Mode::PricingTradeReplayAt(at as u64 * 100)
    } else {Mode::PricingTradeAt(at as u64 * 100)}).await;
    let stop = CancellationToken::new();
    let http = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let endpoint = format!("http://{}/curve.xml", http.local_addr().unwrap());
    let http_stop = stop.clone();
    let http_server = tokio::spawn(async move {
        loop {
            let accepted = tokio::select! {biased;()=http_stop.cancelled()=>break,result=http.accept()=>result};
            let (socket, _) = accepted.unwrap();
            let mut peer = BufReader::new(socket);
            let mut line = String::new();
            loop {
                line.clear();
                if peer.read_line(&mut line).await.unwrap_or(0) == 0 || line == "\r\n" {
                    break;
                }
            }
            let body = "<feed xmlns='http://www.w3.org/2005/Atom' xmlns:d='http://schemas.microsoft.com/ado/2007/08/dataservices' xmlns:m='http://schemas.microsoft.com/ado/2007/08/dataservices/metadata'><entry><content><m:properties><d:NEW_DATE>2026-10-26T00:00:00</d:NEW_DATE><d:BC_1MONTH>4.0000</d:BC_1MONTH><d:BC_2MONTH>4.10</d:BC_2MONTH><d:BC_3MONTH>4.2</d:BC_3MONTH></m:properties></content></entry></feed>";
            let response = format!(
                "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}",
                body.len()
            );
            let _ = peer.get_mut().write_all(response.as_bytes()).await;
        }
    });
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let broker_stop = stop.clone();
    let (published, mut events) = tokio::sync::mpsc::channel(128);
    let durable = Arc::new(std::sync::atomic::AtomicBool::new(false));
    let persisted = durable.clone();
    let mut raw_reference = oracle[fixture_index]["raw"]["options"][0]["contract"].clone();
    raw_reference[4] = 43.into();
    let reviewed_models: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let mut raw_model = reviewed_models[0]["command"].clone();
    for (convention, model) in [
        (1, 0),
        (2, 14),
        (3, 15),
        (4, 16),
        (5, 17),
        (6, 2),
        (7, 6),
        (8, 5),
        (9, 33),
        (10, 36),
        (11, 24),
        (12, 21),
        (13, 22),
        (14, 42),
        (15, 28),
        (16, 25),
        (17, 27),
        (18, 41),
        (19, 19),
        (20, 29),
        (21, 30),
        (22, 31),
        (23, 32),
        (24, 40),
        (25, 37),
        (26, 34),
        (27, 11),
        (28, 35),
    ] {
        raw_model[model] = raw_reference[convention].clone();
    }
    raw_model[23] = "Eastern Standard Time".into();
    let staging_model =
        ifm_market_data_native::reference::option_contract::OptionContract::from_command(
            &raw_model,
        )
        .unwrap();
    if stage_reference {
        assert!(staging_model.errors().is_empty());
    }

    let policy_fixture: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/strategy-policy.json"
    ))
    .unwrap();
    let remove_policies = Arc::new(std::sync::atomic::AtomicBool::new(false));
    let removed = remove_policies.clone();
    let update_policy = Arc::new(std::sync::atomic::AtomicBool::new(false));
    let updated = update_policy.clone();
    let policy_body = if populated_background {
        serde_json::to_vec(&serde_json::json!([
            policy_fixture[0]["command"]["parameters"]
        ]))
        .unwrap()
    } else {
        b"[]".to_vec()
    };
    let canonical_body = serde_json::to_vec(&raw_model).unwrap();
    let candidate = serde_json::json!([
        raw_model[0],
        raw_model[29],
        raw_model[19],
        raw_model[14],
        raw_model[15],
        raw_model[16],
        raw_model[17],
        raw_model[33],
        raw_model[35],
        raw_model[11],
        raw_model[21][0].as_u64().unwrap() * 100
    ]);
    let journal_body = serde_json::to_vec(&serde_json::json!({"schema":1,"authority":"IFM","dataset":"GLBX.MDP3","revision":1,"digest":"a".repeat(64),
        "authorities":[{"id":"IntrinsicTimeWorkflow:test","version":1,"event_id":"00112233445566778899aabbccddeeff","digest":"b".repeat(64),"status":0,"reason":"CommittedActive","workflow_type":"IntrinsicTimeWorkflow","workflow_id":"test","leg_id":"Position","leases":[["112233445566778899aabbccddeeff00",1,4,"Databento","GLBX.MDP3",raw_model[0],"mbp-1",1,raw_model[33],"c".repeat(64)]]}],
        "plans":[{"id":"c".repeat(64),"schema":1,"dataset":"GLBX.MDP3","maturity":739930,"candidates":[candidate],"references":oracle[fixture_index]["raw"],"futures":[],"routes":[[raw_model[33],"ESZ6",1,"ES","GLBX.MDP3",false,false]],"separate":false,"independent":true}]})).unwrap();

    let journal_source = Arc::new(std::sync::Mutex::new(journal_body.clone()));
    let current_journal = journal_source.clone();
    let definitions_body = serde_json::to_vec(&serde_json::json!([raw_model.clone()])).unwrap();
    let definition_calls = Arc::new(AtomicUsize::new(0));
    let definition_count = definition_calls.clone();
    let reference_calls = Arc::new(std::sync::atomic::AtomicUsize::new(0));
    let policy_calls = Arc::new(AtomicUsize::new(0));
    let policy_count = policy_calls.clone();
    let counted = reference_calls.clone();
    let block_references = Arc::new(std::sync::atomic::AtomicBool::new(stall_reference));
    let blocked = block_references.clone();
    let diagnostics = Arc::new(std::sync::Mutex::new(Vec::<String>::new()));
    let diagnostic_log = diagnostics.clone();
    let server = tokio::spawn(async move {
        let mut clients = tokio::task::JoinSet::new();
        loop {
            tokio::select! {biased;()=broker_stop.cancelled()=>break,accepted=listener.accept()=>{
                let (socket,_)=accepted.unwrap();let published=published.clone();let durable=durable.clone();let raw_reference=raw_reference.clone();let counted=counted.clone();let blocked=blocked.clone();let policy_count=policy_count.clone();let definition_count=definition_count.clone();let policy_body=policy_body.clone();let definitions_body=definitions_body.clone();let canonical_body=canonical_body.clone();let removed=removed.clone();let updated=updated.clone();let journal_body=current_journal.lock().unwrap().clone();let diagnostic_log=diagnostic_log.clone();
                clients.spawn(async move{let (read,mut write)=socket.into_split();let mut peer=BufReader::new(read);if write.write_all(b"INFO {\"server_id\":\"native-options\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.is_err(){return;}
                    let mut line=String::new();let mut subscription=None;
                    loop{line.clear();if peer.read_line(&mut line).await.unwrap_or(0)==0{break;}let fields:Vec<_>=line.split_whitespace().collect();
                        if fields.first()==Some(&"PING"){if write.write_all(b"PONG\r\n").await.is_err(){break;}}
                        else if fields.first()==Some(&"SUB"){subscription=Some((fields[1].to_owned(),fields[2].to_owned()));}
                        else if fields.first()==Some(&"PUB"){
                            let count=fields.last().unwrap().parse::<usize>().unwrap();assert!(count<132096);let mut bytes=vec![0;count+2];if peer.read_exact(&mut bytes).await.is_err(){break;}bytes.truncate(count);
                            if fields.len()==4 {
                                // This feed fixture deliberately has no audit service. Keep its
                                // connection alive so audit unavailability cannot break the
                                // independently tested reference/publication paths. Real durable
                                // acknowledgements are qualified by the managed integration suite.
                                if fields[1].starts_with("Function.MarketDataFeedRecoveryRecordFunction.Record."){continue;}
                                let request=rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();let input=request.as_array().unwrap();
                                let receipt=if fields[1].ends_with(".evidence.write") {assert_eq!(input.len(),4);durable.store(true,Ordering::SeqCst);
                                    codec::encode(&rmpv::Value::Array(vec![input[0].clone(),input[1].clone(),input[2].clone(),true.into(),rmpv::Value::Nil])).unwrap()
                                }else if fields[1].ends_with(".reference.read"){
                                    let policy_request=input[1].as_u64()==Some(4);
                                    if policy_request{policy_count.fetch_add(1,Ordering::SeqCst);}else if input[1].as_u64()==Some(5) || input[1].as_u64()==Some(7){
                                        let args:serde_json::Value=serde_json::from_slice(input[2].as_slice().unwrap()).unwrap();
                                        if !args["root"].is_null(){definition_count.fetch_add(1,Ordering::SeqCst);}
                                    }else if input[1].as_u64()!=Some(10){counted.fetch_add(1,Ordering::SeqCst);}
                                    assert_eq!(input.len(),4);if blocked.load(Ordering::SeqCst){continue;}
                                    let args=serde_json::from_slice::<serde_json::Value>(input[2].as_slice().unwrap()).unwrap();
                                    let body=if policy_request{assert_eq!(args["environment"],"Development");if removed.load(Ordering::SeqCst){b"[]".to_vec()}else if updated.load(Ordering::SeqCst){let mut policies:serde_json::Value=serde_json::from_slice(&policy_body).unwrap();policies[0][1]=2.into();serde_json::to_vec(&policies).unwrap()}else{policy_body.clone()}}
                                    else if input[1].as_u64()==Some(5){assert_eq!(args["root"],"ES");definitions_body.clone()}
                                    else if input[1].as_u64()==Some(7){
                                        if !args["root"].is_null(){use sha2::{Digest,Sha256};serde_json::to_vec(&serde_json::json!({"id":"11111111111111111111111111111111","count":1,"digest":format!("{:X}",Sha256::digest(&definitions_body))})).unwrap()}
                                        else {assert_eq!(args["snapshot"],"11111111111111111111111111111111");assert_eq!(args["offset"],0);definitions_body.clone()}
                                    }
                                    else if input[1].as_u64()==Some(10){b"[]".to_vec()}
                                    else if input[1].as_u64()==Some(8){
                                        let date=args["date"].as_i64().unwrap();serde_json::to_vec(&serde_json::json!([[date,"6000",format!("{:016X}",100f64.to_bits())],null,null,null])).unwrap()
                                    }
                                    else if input[1].as_u64()==Some(9){
                                        use sha2::{Digest,Sha256};
                                        if args["action"].as_str()==Some("pending"){serde_json::to_vec(&serde_json::json!({"accepted":true,"revision":args["revision"],"digest":args["digest"],"rows":[]})).unwrap()}
                                        else if args["snapshot"].is_null(){assert_eq!(args["authority"],"IFM");serde_json::to_vec(&serde_json::json!({"id":"22222222222222222222222222222222","total":journal_body.len(),"digest":format!("{:X}",Sha256::digest(&journal_body)),"page_bytes":32768})).unwrap()}
                                        else {assert_eq!(args["snapshot"],"22222222222222222222222222222222");let at=args["offset"].as_u64().unwrap() as usize;journal_body[at..(at+32768).min(journal_body.len())].to_vec()}
                                    }
                                    else if input[1].as_u64()==Some(6){assert_eq!(args["contract"],"ES20261110C6000");canonical_body.clone()}
                                    else if input[1].as_u64()==Some(2){assert_eq!(args["contracts"][0]["contract"],"ES20261110C6000");serde_json::to_vec(&vec![if stage_reference{serde_json::Value::Null}else{raw_reference.clone()}]).unwrap()}else if input[1].as_u64()==Some(3){
                                        assert!(stage_reference);assert_eq!(args["contract"],"ES20261110C6000");assert_eq!(args["version"],"fixture/v1");
                                        let model=base64::engine::general_purpose::STANDARD.decode(args["model"].as_str().unwrap()).unwrap();let model=rmpv::decode::read_value(&mut model.as_slice()).unwrap();let model=model.as_array().unwrap();assert_eq!(model.len(),45);assert_eq!(model[12].as_i64(),Some(1));assert_eq!(model[13].as_i64(),Some(2));assert_eq!(model[16].as_i64(),Some(43));serde_json::to_vec(&raw_reference).unwrap()
                                    }else{
                                        assert_eq!(input[1].as_u64(),Some(1));let from=args["from"].as_i64().unwrap();let until=args["until"].as_i64().unwrap();serde_json::to_vec(&serde_json::json!({"dates":(from..=until).collect::<Vec<_>>()})).unwrap()
                                    };let offset=input[3].as_u64().unwrap() as usize;let end=(offset+4096).min(body.len());use sha2::{Digest,Sha256};let digest=format!("{:X}",Sha256::digest(&body));
                                    codec::encode(&rmpv::Value::Array(vec![input[0].clone(),(body.len() as u32).into(),(offset as u32).into(),digest.into(),rmpv::Value::Binary(body[offset..end].to_vec()),rmpv::Value::Nil])).unwrap()
                                }else{assert!(fields[1].contains(".diagnostics."));assert_eq!(input.len(),10);{let mut records=diagnostic_log.lock().unwrap();if records.len()==256{records.remove(0);}records.push(input[8].as_str().unwrap().to_owned());}use sha2::{Digest,Sha256};let digest=format!("{:X}",Sha256::digest(&bytes));
                                    codec::encode(&rmpv::Value::Array(vec![1.into(),digest.clone().into(),digest.into(),true.into(),rmpv::Value::Nil])).unwrap()};
                                let (_,sid)=subscription.as_ref().unwrap();let header=format!("MSG {} {} {}\r\n",fields[2],sid,receipt.len());if write.write_all(header.as_bytes()).await.is_err(){break;}
                        if write.write_all(&receipt).await.is_err(){break;}
                        if write.write_all(b"\r\n").await.is_err(){break;}
                            }else if fields[1].contains(".publications.")&&published.send(bytes).await.is_err(){break;}
                        }
                    }
                });
            },result=clients.join_next(),if !clients.is_empty()=>{result.unwrap().unwrap();}}
        }
        clients.abort_all();
        while let Some(result) = clients.join_next().await {
            assert!(result.is_ok() || result.unwrap_err().is_cancelled());
        }
    });
    if matches!(discovery_mode, 5 | 6 | 9) {
        // This starts a qualification tool, never a production dataset worker.
        // Only this directly owned child is terminated if the test tool hangs.
        let project = std::path::Path::new(env!("CARGO_MANIFEST_DIR"));
        let repository = project.parent().unwrap();
        let artifacts = repository.join(".artifacts/rust-marketdata-offline");
        std::fs::create_dir_all(&artifacts).unwrap();
        let name = format!("managed-native-discovery-{:032x}", rand::random::<u128>());
        let input = artifacts.join(format!("{name}.json"));
        let log = artifacts.join(format!("{name}.log"));
        let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
        let config = serde_json::json!({"at":at+5_000_000,"date":date.to_julian_day()-1721426,"port":fixture.address.port(),"broker":format!("nats://{address}"),"treasury":endpoint,"model":raw_model});
        std::fs::write(&input, serde_json::to_vec(&config).unwrap()).unwrap();
        let output = std::fs::File::create(&log).unwrap();
        let mut command = std::process::Command::new("dotnet");
        if discovery_mode == 5 {
            command.arg(project.join("qualification/NativeOwnershipBridge/bin/Debug/net10.0/NativeOwnershipBridge.dll")).arg("--owner-live-discovery");
        } else if discovery_mode == 9 {
            command.arg(
                project.join(
                    "qualification/OwnerBrowserBridge/bin/Debug/net10.0/OwnerBrowserBridge.dll",
                ),
            );
        } else {
            command.arg(project.join(
                "qualification/OwnerPreparationBridge/bin/Debug/net10.0/OwnerPreparationBridge.dll",
            ));
        }
        // Qualification can exercise the exact packaged release DLL without
        // changing any production backend selector or copying over debug assets.
        let library = std::env::var_os("IFM_NATIVE_QUALIFICATION_LIBRARY")
            .map(std::path::PathBuf::from)
            .unwrap_or_else(|| project.join("target/debug/ifm_market_data_native.dll"));
        assert!(
            library.is_absolute() && library.is_file(),
            "Qualification DLL must exist at an absolute path"
        );
        let mut child = command
            .arg(library)
            .arg(&input)
            .current_dir(repository)
            .stdout(output.try_clone().unwrap())
            .stderr(output)
            .spawn()
            .unwrap();
        let result = tokio::time::timeout(Duration::from_secs(30), async {
            loop {
                if let Some(status) = child.try_wait().unwrap() {
                    break status;
                }
                tokio::time::sleep(Duration::from_millis(10)).await;
            }
        })
        .await;
        if result.is_err() {
            let _ = child.kill();
            let _ = child.wait();
        }
        let bytes = std::fs::read(&log).unwrap();
        let output = String::from_utf8_lossy(&bytes);
        assert!(
            result.is_ok_and(|status| status.success()),
            "Actual managed discovery failed: {}",
            output.chars().take(16000).collect::<String>()
        );
        assert!(
            output.contains("PASS actual C# -> DLL -> native-owned SDK"),
            "Qualification did not execute its positive path: {output}"
        );
        assert_eq!(
            fixture.accepted.load(Ordering::SeqCst),
            2,
            "Actual CLR must recover the source exactly once"
        );
        if discovery_mode != 9 {
            assert_eq!(
                reference_calls.load(Ordering::SeqCst),
                3,
                "Pinned native references must reuse immutable facts through the real FFI boundary"
            );
        }

        fixture.await_closed().await;
        stop.cancel();
        server.await.unwrap();
        http_server.await.unwrap();
        fixture.stop().await;
        std::fs::remove_file(input).unwrap();
        return;
    }
    let root = Arc::new(Owner::default());
    let sibling = root.child().unwrap();
    let clock =
        Arc::new(Clock::new(if public_owner { at + 5_000_000 } else { at }, false).unwrap());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let first = time::Date::from_calendar_date(2026, time::Month::October, 1).unwrap();
    let references = Arc::new(References::new(
        Arc::new(
            Treasury::new(
                &endpoint,
                Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
                clock.clone(),
            )
            .unwrap(),
        ),
        clock.clone(),
    ));
    let reviewed = Arc::new(ReviewedScope {
        manifests: Budget::new(16 * 1024 * 1024).unwrap(),
        references,
        definitions: vec![Definition {
            instrument: 43,
            underlying: 42,
            strike: decimal::parse("6000").unwrap(),
            call: true,
            required: true,
            blueprint: Blueprint {
                contract: Convention::new(contract).unwrap(),
                calendar: Calendar::new(
                    "fixture/v1".into(),
                    "Eastern Standard Time",
                    first,
                    first + time::Duration::days(59),
                    648_000_000_000,
                    (0..60).map(|i| first + time::Duration::days(i)).collect(),
                )
                .unwrap(),
                publications: Publications::new(
                    "publication/v1".into(),
                    at - 864_000_000_000,
                    at + 20 * 864_000_000_000,
                    vec![Publication {
                        date: date.previous_day().unwrap(),
                        available: at - 864_000_000_000,
                    }],
                    "Eastern Standard Time",
                )
                .unwrap(),
                conversion: Conversion::standard(),
                maximum_lead: 0,
            },
        }],
        production: false,
        production_approved: false,
        review_evidence: None,
        policy_version: "OptionRefresh/TestDefaults/v1".into(),
        selection_ms: 10,
        iv_ms: 5000,
        risk_ms: 20,
        maximum_contracts: 2048,
        batch: 16,
        statistics: vec![],
    });
    let registrations = vec![
        Registration {
            publisher: 3,
            instrument: 42,
            contract: "ES20261218".into(),
            source: "42".into(),
            asset: Asset::Futures,
            tick: None,
            vwap: false,
            required: true,
        },
        Registration {
            publisher: 3,
            instrument: 43,
            contract: "ES20261110C6000".into(),
            source: "43".into(),
            asset: Asset::Option,
            tick: None,
            vwap: false,
            required: true,
        },
    ];
    let bootstrap = Arc::new(
        ifm_market_data_native::option_chain::reference_bootstrap::Bootstrap::new(
            BrokerSettings::new(
                format!("nats://{address}"),
                vec!["127.0.0.1:1".parse().unwrap()],
            )
            .unwrap(),
            "development".into(),
        )
        .unwrap(),
    );
    if stage_reference && !canonical_monitor && discovery_mode == 0 {
        bootstrap.register_option_model(staging_model).unwrap();
    }
    let mut intent = Arc::new(Intent {
        background: None,
        reference_bootstrap: Some(bootstrap),
        date,
        definition: date,
        source: Start {
            plan: SessionPlan {
                lane: Lane::OptionsGlbx,
                subscriptions: vec![
                    SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        symbology: SType::InstrumentId,
                        schema: Schema::Trades,
                        replay_start_ns: None,
                    },
                    SubscriptionSpec {
                        symbols: vec!["42".into(), "43".into()],
                        symbology: SType::InstrumentId,
                        schema: Schema::Mbp1,
                        replay_start_ns: None,
                    },
                ],
            },
            registrations,
            key: "x".repeat(32),
            gateway: fixture.gateway(),
            evidence: Evidence::Observations,
        },
        reviewed,
        monitored: [43].into_iter().collect(),
        caches: vec![],
        broker: BrokerSettings::new(
            format!("nats://{address}"),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        scope: "development".into(),
        revision: 1,
    });
    if public_owner {
        use ifm_market_data_native::{
            api::owner::{MarketData, RecoveryRequest},
            dataset::option_claims::Scope,
            option_chain::composition::capture::Request,
            subscriptions::leases::Lifetime,
        };
        let market = Arc::new(MarketData::new(root.clone(), clock.clone(), None).unwrap());
        if native_background {
            let bp = &intent.reviewed.definitions[0].blueprint;
            let configuration = ifm_market_data_native::dataset::background::Configuration::new(
                Arc::new(ifm_market_data_native::option_chain::discovery::Discovery {
                    broker: intent.broker.clone(),
                    scope: intent.scope.clone(),
                    environment: "Development".into(),
                }),
                ifm_market_data_native::option_chain::preparation::RawPolicy {
                    publications: bp.publications.clone(),
                    conversion: bp.conversion.clone(),
                    maximum_lead: bp.maximum_lead,
                },
                Arc::downgrade(&market),
            );
            let configuration = if canonical_monitor {
                configuration
                    .monitored_only()
                    .with_monitor(ifm_market_data_native::dataset::background::Monitor {
                        owner: 111,
                        scope: "canonical-first-load".into(),
                        contract: "ES20261110C6000".into(),
                    })
                    .unwrap()
            } else {
                configuration
            };
            let configuration = if matches!(discovery_mode, 8 | 10) {
                configuration.monitored_only()
            } else if discovery_mode == 7 {
                configuration.monitored_only().with_committed().unwrap()
            } else if discovery_mode != 0 {
                let mut candidate = serde_json::json!([
                    raw_model[0],
                    raw_model[29],
                    raw_model[19],
                    raw_model[14],
                    raw_model[15],
                    raw_model[16],
                    raw_model[17],
                    raw_model[33],
                    raw_model[35],
                    raw_model[11],
                    raw_model[21][0].as_u64().unwrap() * 100
                ]);
                if discovery_mode == 4 {
                    candidate[1] = "wrong-version".into();
                }
                let candidates = if discovery_mode == 3 {
                    vec![candidate.clone(), candidate]
                } else {
                    vec![candidate]
                };
                let input = serde_json::json!({"owner":[111,0],"maturity":date.to_julian_day()-1721426+14,"complete":discovery_mode!=2,"separate":false,"independent":true,"candidates":candidates});
                let demand =
                    ifm_market_data_native::dataset::background::ScopeDemand::from_command(&input)
                        .unwrap();
                configuration.monitored_only().with_scope(demand).unwrap()
            } else {
                configuration
            };
            Arc::get_mut(&mut intent).unwrap().background = Some(Arc::new(configuration));
            if canonical_monitor || discovery_mode != 0 {
                let input = Arc::get_mut(&mut intent).unwrap();
                Arc::make_mut(&mut input.reviewed).definitions.clear();
                input.source.registrations.clear();
                input.source.plan.subscriptions.clear();
                input.monitored.clear();
            }
        }
        market
            .merge_raw_catalog_once(
                intent.clone(),
                [41; 32],
                vec![("ES20261110C6000".into(), [42; 32])],
            )
            .unwrap();
        if matches!(discovery_mode, 8 | 10) {
            use ifm_market_data_native::option_chain::browser::Command;
            let mut command=Command::parse(&serde_json::json!({"contract":raw_model[33],"root":"ES","expiry":date.to_julian_day()-1721426+14,"providers":[],"owner":"view-one","required":[raw_model[0]],"multiplier":2.5,"wing":"25","release":false})).unwrap();
            market.begin_browser(command.clone()).unwrap();
            let result = tokio::time::timeout(
                Duration::from_secs(8),
                market.browser_result(&command, &CancellationToken::new(), Deadline::recovery()),
            )
            .await
            .unwrap_or_else(|_| {
                panic!(
                    "Browser setup: {:?}",
                    market.health(Lane::OptionsGlbx).status()
                )
            })
            .unwrap();
            let fields = result.as_array().unwrap();
            assert_eq!(fields[1].as_i64(), Some(0), "{result:?}");
            let model = fields[0].as_array().unwrap();
            let rows = model[7].as_array().unwrap();
            assert_eq!(rows.len(), 1);
            assert_eq!(
                rows[0].as_array().unwrap()[0].as_str(),
                Some("ES20261110C6000")
            );
            let statistics = market
                .options_current()
                .unwrap()
                .queries
                .publication_state(43)
                .unwrap()
                .unwrap()
                .1
                .option_statistics
                .unwrap();
            assert!(statistics.observed > 0);
            let old = market.publication_identity(Lane::OptionsGlbx).unwrap();
            command.owner = "view-two".into();
            market.begin_browser(command.clone()).unwrap();
            assert_eq!(
                market
                    .publication_identity(Lane::OptionsGlbx)
                    .unwrap()
                    .generation,
                old.generation
            );
            if discovery_mode == 10 {
                let previous_reads = definition_calls.load(Ordering::SeqCst);
                tokio::time::timeout(Duration::from_secs(35), async {
                    while definition_calls.load(Ordering::SeqCst) == previous_reads {
                        tokio::time::sleep(Duration::from_millis(10)).await;
                    }
                    // Finish both the raw copy and the native refresh decision.
                    tokio::time::sleep(Duration::from_millis(100)).await;
                })
                .await
                .expect("Native browser did not refresh copied metadata on its existing cadence");
                assert_eq!(
                    fixture.accepted.load(Ordering::SeqCst),
                    1,
                    "Unchanged metadata reset a healthy provider source"
                );
                assert_eq!(
                    market
                        .publication_identity(Lane::OptionsGlbx)
                        .unwrap()
                        .generation,
                    old.generation
                );
                assert!(
                    market
                        .browser_result(&command, &CancellationToken::new(), Deadline::recovery())
                        .await
                        .is_ok()
                );
            }
            let request = RecoveryRequest {
                lane: Lane::OptionsGlbx,
                operation: [98; 16],
                worker: old.worker,
                generation: old.generation,
                date,
            };
            block_references.store(true, Ordering::SeqCst);
            let rejected = market.begin_recovery(&request).unwrap();
            let recovered = tokio::time::timeout(
                Duration::from_secs(8),
                market.recovery_result(&request, rejected),
            )
            .await
            .unwrap()
            .unwrap();
            assert_eq!(recovered.as_array().unwrap()[3].as_bool(), Some(true));
            tokio::time::timeout(
                Duration::from_secs(8),
                market.wait_ready(Lane::OptionsGlbx, false),
            )
            .await
            .unwrap()
            .unwrap();
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2);
            let restored = market
                .options_current()
                .unwrap()
                .queries
                .publication_state(43)
                .unwrap()
                .unwrap()
                .1
                .option_statistics
                .unwrap();
            assert_eq!(
                restored.observed, statistics.observed,
                "Physical replay double-counted volume"
            );
            assert_eq!(restored.last_event_ns, statistics.last_event_ns);
            command.owner = "view-one".into();
            command.release = true;
            market.begin_browser(command.clone()).unwrap();
            assert_eq!(
                market
                    .browser_result(&command, &CancellationToken::new(), Deadline::recovery())
                    .await
                    .unwrap()
                    .as_array()
                    .unwrap()[0]
                    .as_array()
                    .unwrap()[5]
                    .as_str(),
                Some("Released")
            );
            assert!(market.has_desired(Lane::OptionsGlbx));
            command.owner = "view-two".into();
            market.begin_browser(command.clone()).unwrap();
            market
                .browser_result(&command, &CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            market.wait_ready(Lane::OptionsGlbx, false).await.unwrap();
            assert!(!market.has_desired(Lane::OptionsGlbx));
            market.stop_feeds().unwrap();
            market.wait_feeds_stopped().await.unwrap();
            market.shutdown(Deadline::recovery()).await.unwrap();
            drop(sibling);
            root.stop().unwrap();
            root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            fixture.await_closed().await;
            stop.cancel();
            server.await.unwrap();
            http_server.await.unwrap();
            fixture.stop().await;
            return;
        }
        if discovery_mode == 7 {
            tokio::time::timeout(
                Duration::from_secs(6),
                market.wait_ready(Lane::OptionsGlbx, true),
            )
            .await
            .unwrap_or_else(|_| {
                panic!(
                    "Committed source setup: {:?}",
                    market.health(Lane::OptionsGlbx).status()
                )
            })
            .unwrap();
            let state = market.committed_state().unwrap();
            assert_eq!(state.as_array().unwrap()[0].as_i64(), Some(1));
            assert_eq!(state.as_array().unwrap()[2].as_bool(), Some(true));
            assert!(market.options_current().unwrap().monitored.contains(&43));
            let scope = "c".repeat(64);
            let request = Request {
                id: [96; 16],
                scope: scope.clone(),
                horizon: "Daily".into(),
                generation: market
                    .publication_identity(Lane::OptionsGlbx)
                    .unwrap()
                    .generation,
                at: -621355968000000000,
                until: at + 1_200_000_000,
                maximum: 1,
                age: 5000,
                skew: 1000,
                options: true,
                selection: false,
                allow_missing: false,
                risk: Default::default(),
            };
            assert_eq!(
                market
                    .capture_options_ready(request, &CancellationToken::new(), Deadline::recovery())
                    .await
                    .unwrap()
                    .instruments
                    .len(),
                1
            );
            let risk = market
                .monitored_risk_command("ES20261110C6000", date)
                .unwrap();
            assert!(risk.request.scope.starts_with("native-risk-"));
            assert_eq!(
                market
                    .capture_options_ready(
                        risk.request,
                        &CancellationToken::new(),
                        Deadline::recovery()
                    )
                    .await
                    .unwrap()
                    .instruments
                    .len(),
                1
            );
            assert_eq!(reference_calls.load(Ordering::SeqCst), 6);
            let stable = market.publication_identity(Lane::OptionsGlbx).unwrap();
            {
                let mut source = journal_source.lock().unwrap();
                let mut next: serde_json::Value = serde_json::from_slice(&source).unwrap();
                next["revision"] = 2.into();
                next["authorities"][0]["version"] = 2.into();
                next["authorities"][0]["event_id"] = "00112233445566778899aabbccddee00".into();
                next["authorities"][0]["digest"] = "d".repeat(64).into();
                *source = serde_json::to_vec(&next).unwrap();
            }
            market.reconcile_business().unwrap();
            let reconciled =
                tokio::time::timeout(Duration::from_secs(6), market.wait_committed_state())
                    .await
                    .unwrap_or_else(|_| {
                        panic!(
                            "In-place committed refresh: {:?}",
                            market.health(Lane::OptionsGlbx).status()
                        )
                    })
                    .unwrap();
            assert_eq!(reconciled.as_array().unwrap()[0].as_i64(), Some(2));
            assert_eq!(reconciled.as_array().unwrap()[2].as_bool(), Some(true));
            assert_eq!(
                market
                    .publication_identity(Lane::OptionsGlbx)
                    .unwrap()
                    .generation,
                stable.generation
            );
            assert_eq!(
                fixture.accepted.load(Ordering::SeqCst),
                1,
                "Business watermarks reset healthy SDK resources"
            );
            market.reconcile_business().unwrap();
            tokio::time::timeout(Duration::from_secs(6), market.wait_committed_state())
                .await
                .unwrap()
                .unwrap();
            assert_eq!(
                fixture.accepted.load(Ordering::SeqCst),
                1,
                "Unchanged reconciliation reset the SDK"
            );
            {
                let mut source = journal_source.lock().unwrap();
                let mut next: serde_json::Value = serde_json::from_slice(&source).unwrap();
                next["revision"] = 3.into();
                next["digest"] = "f".repeat(64).into();
                next["authorities"][0]["version"] = 3.into();
                next["authorities"][0]["event_id"] = "00112233445566778899aabbccddee11".into();
                next["plans"][0]["id"] = "e".repeat(64).into();
                next["plans"][0]["independent"] = false.into();
                next["authorities"][0]["leases"][0][9] = "e".repeat(64).into();
                *source = serde_json::to_vec(&next).unwrap();
            }
            market.reconcile_business().unwrap();
            let changed =
                tokio::time::timeout(Duration::from_secs(6), market.wait_committed_state())
                    .await
                    .unwrap()
                    .unwrap();
            assert_eq!(changed.as_array().unwrap()[0].as_i64(), Some(3));
            assert_eq!(changed.as_array().unwrap()[2].as_bool(), Some(true));
            assert_ne!(
                market
                    .publication_identity(Lane::OptionsGlbx)
                    .unwrap()
                    .generation,
                stable.generation
            );
            assert_eq!(
                fixture.accepted.load(Ordering::SeqCst),
                2,
                "A changed physical recipe was acknowledged without source replacement"
            );
            let before = reference_calls.load(Ordering::SeqCst);
            block_references.store(true, Ordering::SeqCst);
            let old = market.publication_identity(Lane::OptionsGlbx).unwrap();
            let request = RecoveryRequest {
                lane: Lane::OptionsGlbx,
                operation: [97; 16],
                worker: old.worker,
                generation: old.generation,
                date,
            };
            let rejected = market.begin_recovery(&request).unwrap();
            let outcome = tokio::time::timeout(
                Duration::from_secs(6),
                market.recovery_result(&request, rejected),
            )
            .await
            .unwrap()
            .unwrap();
            assert_eq!(outcome.as_array().unwrap()[3].as_bool(), Some(true));
            // Financial assertions below require a publication, beyond transport recovery.
            tokio::time::timeout(
                Duration::from_secs(6),
                market.wait_ready(Lane::OptionsGlbx, false),
            )
            .await
            .unwrap()
            .unwrap();
            assert_ne!(
                market
                    .publication_identity(Lane::OptionsGlbx)
                    .unwrap()
                    .generation,
                old.generation
            );
            assert_eq!(reference_calls.load(Ordering::SeqCst), before);
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 3);
            assert!(market.options_current().unwrap().monitored.contains(&43));
            assert!(
                market.committed_state().unwrap().as_array().unwrap()[2]
                    .as_bool()
                    .unwrap()
            );
            assert!(!sibling.cancellation().is_cancelled());
            market.stop_feeds().unwrap();
            market.wait_feeds_stopped().await.unwrap();
            market.shutdown(Deadline::recovery()).await.unwrap();
            drop(sibling);
            root.stop().unwrap();
            root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            assert!(root.disposable().unwrap());
            stop.cancel();
            server.await.unwrap();
            http_server.await.unwrap();
            fixture.stop().await;
            return;
        }
        if discovery_mode != 0 {
            tokio::time::timeout(
                Duration::from_secs(6),
                market.wait_ready(Lane::OptionsGlbx, false),
            )
            .await
            .unwrap_or_else(|_| {
                panic!(
                    "Native discovery health: {:?}",
                    market.health(Lane::OptionsGlbx).status()
                )
            })
            .unwrap();
            let result = market
                .discovery_scope_result(111u128.to_le_bytes())
                .unwrap();
            let fields = result.as_array().unwrap();
            if discovery_mode == 1 {
                assert!(fields[3].is_nil());
                assert!(!fields[0].is_nil());
                assert_eq!(
                    fields[0].as_array().unwrap()[6].as_array().unwrap().len(),
                    1
                );
                assert_eq!(fields[0].as_array().unwrap()[9].as_bool(), Some(true));
                assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
                let before = reference_calls.load(Ordering::SeqCst);
                assert_eq!(before, 4);
                let lease = fields[0].as_array().unwrap();
                let original_scope = lease[0].as_str().unwrap().to_owned();
                let alias = "a".repeat(64);
                let identity = market.publication_identity(Lane::OptionsGlbx).unwrap();
                let unchanged = market.options_current().unwrap();
                assert!(
                    market
                        .alias_option_scope(111, [99; 16], &original_scope, &alias)
                        .is_err()
                );
                assert!(
                    market
                        .alias_option_scope(
                            111,
                            identity.generation,
                            &original_scope,
                            "invalid-plan"
                        )
                        .is_err()
                );
                assert!(
                    market
                        .alias_option_scope(999, identity.generation, &original_scope, &alias)
                        .is_err()
                );
                market
                    .alias_option_scope(111, identity.generation, &original_scope, &alias)
                    .unwrap();
                market
                    .alias_option_scope(111, identity.generation, &original_scope, &alias)
                    .unwrap();
                assert!(Arc::ptr_eq(&unchanged, &market.options_current().unwrap()));
                assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
                let named = market
                    .discovery_scope_result(111u128.to_le_bytes())
                    .unwrap();
                assert_eq!(
                    named.as_array().unwrap()[0].as_array().unwrap()[0].as_str(),
                    Some(alias.as_str())
                );
                let request = Request {
                    id: [94; 16],
                    scope: alias.clone(),
                    horizon: "Daily".into(),
                    generation: market
                        .publication_identity(Lane::OptionsGlbx)
                        .unwrap()
                        .generation,
                    at: -621355968000000000,
                    until: at + 1_200_000_000,
                    maximum: 1,
                    age: 5000,
                    skew: 1000,
                    options: true,
                    selection: false,
                    allow_missing: false,
                    risk: Default::default(),
                };
                let snapshot = market
                    .capture_options_ready(request, &CancellationToken::new(), Deadline::recovery())
                    .await
                    .unwrap();
                assert_eq!(snapshot.instruments.len(), 1);
                block_references.store(true, Ordering::SeqCst);
                let old = market.publication_identity(Lane::OptionsGlbx).unwrap();
                let recovery = RecoveryRequest {
                    lane: Lane::OptionsGlbx,
                    operation: [95; 16],
                    worker: old.worker,
                    generation: old.generation,
                    date,
                };
                let rejected = market.begin_recovery(&recovery).unwrap();
                let outcome = tokio::time::timeout(
                    Duration::from_secs(6),
                    market.recovery_result(&recovery, rejected),
                )
                .await
                .unwrap()
                .unwrap();
                assert_eq!(outcome.as_array().unwrap()[3].as_bool(), Some(true));
                // Financial assertions below require a publication, beyond transport recovery.
                tokio::time::timeout(
                    Duration::from_secs(6),
                    market.wait_ready(Lane::OptionsGlbx, false),
                )
                .await
                .unwrap()
                .unwrap();
                assert_ne!(
                    market
                        .publication_identity(Lane::OptionsGlbx)
                        .unwrap()
                        .generation,
                    old.generation
                );
                assert_eq!(reference_calls.load(Ordering::SeqCst), before);
                let named = market
                    .discovery_scope_result(111u128.to_le_bytes())
                    .unwrap();
                assert_eq!(
                    named.as_array().unwrap()[0].as_array().unwrap()[0].as_str(),
                    Some(alias.as_str())
                );
                assert!(
                    !market
                        .discovery_scope_result(111u128.to_le_bytes())
                        .unwrap()
                        .as_array()
                        .unwrap()[0]
                        .is_nil()
                );
            } else {
                assert!(fields[0].is_nil());
                assert_eq!(
                    fields[3].as_array().unwrap()[0].as_str(),
                    Some(match discovery_mode {
                        2 => "IncompleteChain",
                        3 => "ConflictingDefinition",
                        _ => "ContractMetadataUnavailable",
                    })
                );
                assert_eq!(fixture.accepted.load(Ordering::SeqCst), 0);
                assert!(!sibling.cancellation().is_cancelled());
            }
            market.option_claims().unwrap().disable(111).unwrap();
            market.stop_feeds().unwrap();
            market.wait_feeds_stopped().await.unwrap();
            market.shutdown(Deadline::recovery()).await.unwrap();
            drop(sibling);
            root.stop().unwrap();
            root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            assert!(root.disposable().unwrap());
            stop.cancel();
            server.await.unwrap();
            http_server.await.unwrap();
            fixture.stop().await;
            return;
        }
        if canonical_monitor {
            assert!(
                market
                    .change_stream_route(111, "ES20261110C6000", true, true)
                    .unwrap()
            );
            if stall_reference {
                tokio::time::timeout(Duration::from_secs(3), async {
                    while reference_calls.load(Ordering::SeqCst) == 0 {
                        tokio::task::yield_now().await;
                    }
                })
                .await
                .unwrap();
                assert_eq!(fixture.accepted.load(Ordering::SeqCst), 0);
                market.option_claims().unwrap().disable(111).unwrap();
                market.stop_feeds().unwrap();
                tokio::time::timeout(Duration::from_secs(2), market.wait_feeds_stopped())
                    .await
                    .unwrap()
                    .unwrap();
                block_references.store(false, Ordering::SeqCst);
                tokio::time::sleep(Duration::from_millis(75)).await;
                assert!(!market.has_desired(Lane::OptionsGlbx));
                assert_eq!(
                    fixture.accepted.load(Ordering::SeqCst),
                    0,
                    "Late copied facts must not resurrect disabled intent"
                );
                assert!(!sibling.cancellation().is_cancelled());
                market.shutdown(Deadline::recovery()).await.unwrap();
                drop(sibling);
                root.stop().unwrap();
                root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                    .await
                    .unwrap();
                assert!(root.disposable().unwrap());
                stop.cancel();
                server.await.unwrap();
                http_server.await.unwrap();
                fixture.stop().await;
                return;
            }
            tokio::time::timeout(
                Duration::from_secs(6),
                market.wait_ready(Lane::OptionsGlbx, true),
            )
            .await
            .unwrap_or_else(|_| {
                panic!(
                    "Native canonical setup: {:?}",
                    market.health(Lane::OptionsGlbx).status()
                )
            })
            .unwrap();
            assert!(market.stream_active("ES20261110C6000").unwrap());
            let request = market
                .monitored_risk_command("ES20261110C6000", date)
                .unwrap();
            let captured = market
                .capture_options(
                    request.request,
                    &CancellationToken::new(),
                    Deadline::recovery(),
                )
                .await
                .unwrap();
            assert_eq!(captured.instruments.len(), 1);

            assert!(market.options_current().unwrap().monitored.contains(&43));
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
            assert_eq!(policy_calls.load(Ordering::SeqCst), 0);
            assert_eq!(definition_calls.load(Ordering::SeqCst), 0);
            let calls = reference_calls.load(Ordering::SeqCst);
            assert_eq!(
                calls, 4,
                "Canonical lookup plus convention lookup/staging/calendar only"
            );
            let workload = market.read_snapshot(true).unwrap();
            let workload = workload.as_array().unwrap()[7].as_array().unwrap();
            assert_eq!(workload[3], workload[4]);
            assert!(
                workload[3]
                    .as_array()
                    .unwrap()
                    .iter()
                    .any(|id| id.as_str() == Some("canonical-first-load"))
            );
            block_references.store(true, Ordering::SeqCst);
            let old = market.publication_identity(Lane::OptionsGlbx).unwrap();
            let request = RecoveryRequest {
                lane: Lane::OptionsGlbx,
                operation: [93; 16],
                worker: old.worker,
                generation: old.generation,
                date,
            };
            let rejected = market.begin_recovery(&request).unwrap();
            let result = tokio::time::timeout(
                Duration::from_secs(6),
                market.recovery_result(&request, rejected),
            )
            .await
            .unwrap()
            .unwrap();
            assert_eq!(result.as_array().unwrap()[3].as_bool(), Some(true));
            // Financial assertions below require a publication, beyond transport recovery.
            tokio::time::timeout(
                Duration::from_secs(6),
                market.wait_ready(Lane::OptionsGlbx, false),
            )
            .await
            .unwrap()
            .unwrap();
            assert_ne!(
                market
                    .publication_identity(Lane::OptionsGlbx)
                    .unwrap()
                    .generation,
                old.generation
            );
            assert_eq!(
                reference_calls.load(Ordering::SeqCst),
                calls,
                "Copied reconstruction must not wait on blocked DB"
            );
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2);
            market.option_claims().unwrap().disable(111).unwrap();
            tokio::time::timeout(Duration::from_secs(3), async {
                while market.has_desired(Lane::OptionsGlbx) {
                    tokio::task::yield_now().await;
                }
            })
            .await
            .unwrap();
            assert!(!sibling.cancellation().is_cancelled());
            market.stop_feeds().unwrap();
            market.wait_feeds_stopped().await.unwrap();
            market.shutdown(Deadline::recovery()).await.unwrap();
            drop(sibling);
            root.stop().unwrap();
            root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            assert!(root.disposable().unwrap());
            fixture.await_closed().await;
            stop.cancel();
            server.await.unwrap();
            http_server.await.unwrap();
            fixture.stop().await;
            return;
        }
        if populated_background {
            tokio::time::timeout(
                Duration::from_secs(6),
                market.wait_ready(Lane::OptionsGlbx, true),
            )
            .await
            .unwrap_or_else(|_| {
                panic!(
                    "Native background setup: {:?}; diagnostics={:?}; accepted={}; closed={}",
                    market.health(Lane::OptionsGlbx).status(),diagnostics.lock().unwrap(),fixture.accepted.load(Ordering::SeqCst),fixture.closed.load(Ordering::SeqCst)
                )
            })
            .unwrap();
            assert!(market.options_current().is_some());
            // This fixture has only one call, so there is no executable condor.
            // Fully priced no-candidate output must be explicit and must not
            // cause the availability watchdog to rebuild a healthy dataset.
            tokio::time::timeout(Duration::from_secs(6), async {
                loop {
                    let ready = {
                        let view = market.options_current().unwrap();
                        let caches = view.caches.lock().unwrap();
                        !caches.is_empty()
                    };
                    if ready {
                        break;
                    }
                    tokio::task::yield_now().await;
                }
            })
            .await
            .expect("No-candidate scope was never published explicitly");
            {
                let view = market.options_current().unwrap();
                let caches = view.caches.lock().unwrap();
                assert!(!caches.is_empty());
                assert!(caches.values().all(|publication| {
                    publication.snapshot.instruments.is_empty()
                        && publication.snapshot.ranking.is_some()
                        && publication.until > clock.utc_ticks().unwrap()
                }));
            }
            assert_eq!(
                fixture.accepted.load(Ordering::SeqCst),
                2,
                "Temporary quote session must retire before the complete option session"
            );
            assert_eq!(policy_calls.load(Ordering::SeqCst), 1);
            assert_eq!(definition_calls.load(Ordering::SeqCst), 1);
            let captured = market.read_snapshot(true).unwrap();
            let workload = captured.as_array().unwrap()[7].as_array().unwrap();
            assert_eq!(workload.len(), 8);
            assert_eq!(workload[6].as_u64(), Some(1));
            assert_eq!(workload[7].as_bool(), Some(true));
            assert!(
                !workload[3].as_array().unwrap().is_empty(),
                "Background ownership must not masquerade as clean idle"
            );
            assert_eq!(
                workload[3], workload[4],
                "Resource-ready scopes must not depend on optional executable candidates"
            );
            if refresh_background {
                let before = market.publication_identity(Lane::OptionsGlbx).unwrap();
                tokio::time::timeout(Duration::from_secs(36), async {
                    while policy_calls.load(Ordering::SeqCst) < 2
                        || definition_calls.load(Ordering::SeqCst) < 2
                    {
                        tokio::task::yield_now().await;
                    }
                })
                .await
                .expect("Native source maintenance never refreshed published facts");
                tokio::time::sleep(Duration::from_millis(100)).await;
                assert_eq!(
                    market
                        .publication_identity(Lane::OptionsGlbx)
                        .unwrap()
                        .generation,
                    before.generation,
                    "Unchanged raw policies/definitions must not reset the SDK; health={:?}",
                    market.health(Lane::OptionsGlbx).status()
                );
                assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2);
                // A policy revision is fully resolved while the old source is
                // live. The replacement must open only its final source, not a
                // second temporary underlying-quote bootstrap.
                update_policy.store(true, Ordering::SeqCst);
                tokio::time::timeout(Duration::from_secs(36), async {
                    loop {
                        if market.publication_identity(Lane::OptionsGlbx).unwrap().generation != before.generation
                            && market.health(Lane::OptionsGlbx).status().phase
                                == ifm_market_data_native::runtime::recovery::Phase::Running
                        { break; }
                        tokio::time::sleep(Duration::from_millis(10)).await;
                    }
                }).await.expect("Resolved changed-policy replacement did not recover");
                assert_eq!(fixture.accepted.load(Ordering::SeqCst), 3,
                    "Changed-policy replacement discarded its resolved recipe and reopened quote bootstrap");
                remove_policies.store(true, Ordering::SeqCst);
                tokio::time::timeout(Duration::from_secs(36), async {
                    loop {
                        if policy_calls.load(Ordering::SeqCst) >= 4
                            && market.options_current().is_none()
                            && market.health(Lane::OptionsGlbx).status().phase
                                == ifm_market_data_native::runtime::recovery::Phase::Running
                        {
                            break;
                        }
                        tokio::time::sleep(Duration::from_millis(10)).await;
                    }
                })
                .await
                .expect("Native removed-policy dataset failed to retire to proven idle");
                assert_eq!(
                    fixture.accepted.load(Ordering::SeqCst),
                    3,
                    "Removing the final policy must not open an empty provider session"
                );
                fixture.await_closed().await;
                assert!(!sibling.cancellation().is_cancelled());
                market.stop_feeds().unwrap();
                market.wait_feeds_stopped().await.unwrap();
                market.shutdown(Deadline::recovery()).await.unwrap();
                drop(sibling);
                root.stop().unwrap();
                root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                    .await
                    .unwrap();
                assert!(root.disposable().unwrap());
                stop.cancel();
                server.await.unwrap();
                http_server.await.unwrap();
                fixture.stop().await;
                return;
            }
            block_references.store(true, Ordering::SeqCst);
            let old = market.publication_identity(Lane::OptionsGlbx).unwrap();
            let request = RecoveryRequest {
                lane: Lane::OptionsGlbx,
                operation: [91; 16],
                worker: old.worker,
                generation: old.generation,
                date,
            };
            let rejected = market.begin_recovery(&request).unwrap();
            let restored = tokio::time::timeout(
                Duration::from_secs(6),
                market.recovery_result(&request, rejected),
            )
            .await
            .unwrap_or_else(|_| {
                panic!(
                    "Native background recovery: {:?}",
                    market.health(Lane::OptionsGlbx).status()
                )
            })
            .unwrap();
            assert_eq!(restored.as_array().unwrap()[3].as_bool(), Some(true));
            // Financial assertions below require a publication, beyond transport recovery.
            tokio::time::timeout(
                Duration::from_secs(6),
                market.wait_ready(Lane::OptionsGlbx, false),
            )
            .await
            .unwrap()
            .unwrap();
            assert_ne!(
                market
                    .publication_identity(Lane::OptionsGlbx)
                    .unwrap()
                    .generation,
                old.generation
            );
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 3,
                "Recovery must reopen the qualified option source directly, without another temporary underlying-quote session");
            assert_eq!(policy_calls.load(Ordering::SeqCst), 1);
            assert_eq!(definition_calls.load(Ordering::SeqCst), 1);
            assert_eq!(
                reference_calls.load(Ordering::SeqCst),
                4,
                "Pinned native reconstruction must bypass stalled storage"
            );
            assert!(!sibling.cancellation().is_cancelled());
            market.stop_feeds().unwrap();
            tokio::time::timeout(Duration::from_secs(3), market.wait_feeds_stopped())
                .await
                .unwrap()
                .unwrap();
            assert!(!market.has_desired(Lane::OptionsGlbx));
            market.shutdown(Deadline::recovery()).await.unwrap();
            drop(sibling);
            root.stop().unwrap();
            root.wait_idle(&CancellationToken::new(), Deadline::recovery())
                .await
                .unwrap();
            assert!(root.disposable().unwrap());
            fixture.await_closed().await;
            stop.cancel();
            server.await.unwrap();
            http_server.await.unwrap();
            fixture.stop().await;
            return;
        }
        if native_background {
            tokio::time::timeout(
                Duration::from_secs(4),
                market.wait_ready(Lane::OptionsGlbx, true),
            )
            .await
            .unwrap()
            .unwrap();
            assert!(market.options_current().is_none());
            assert_eq!(
                fixture.accepted.load(Ordering::SeqCst),
                0,
                "Empty policy must not open a provider session"
            );
            assert_eq!(policy_calls.load(Ordering::SeqCst), 1);
            let mut background_only = (*intent).clone();
            let mut reviewed = (*background_only.reviewed).clone();
            reviewed.definitions.clear();
            background_only.reviewed = Arc::new(reviewed);
            background_only.source.registrations.clear();
            background_only.source.plan.subscriptions.clear();
            background_only.reference_bootstrap = None;
            market
                .merge_raw_catalog_once(Arc::new(background_only), [62; 32], vec![])
                .unwrap();
            tokio::time::timeout(
                Duration::from_secs(4),
                market.wait_ready(Lane::OptionsGlbx, true),
            )
            .await
            .unwrap()
            .unwrap();
            assert!(market.options_current().is_none());
            assert_eq!(fixture.accepted.load(Ordering::SeqCst), 0);
            assert_eq!(
                policy_calls.load(Ordering::SeqCst),
                1,
                "Background-only reconfiguration reuses copied facts without indexing a missing first option"
            );
        }
        if native_background {
            // Restore the original complete command fingerprint before the
            // separate idempotent-monitor assertions below.
            market
                .merge_raw_catalog_once(
                    intent.clone(),
                    [41; 32],
                    vec![("ES20261110C6000".into(), [42; 32])],
                )
                .unwrap();
            tokio::time::timeout(
                Duration::from_secs(4),
                market.wait_ready(Lane::OptionsGlbx, true),
            )
            .await
            .unwrap()
            .unwrap();
        }
        let claims = market.option_claims().unwrap();
        let policies: serde_json::Value = serde_json::from_str(include_str!(
            "../qualification/ContextWireBridge/strategy-policy.json"
        ))
        .unwrap();
        let parameters = Arc::new(
            ifm_market_data_native::option_chain::strategy::from_command(
                &policies[0]["command"]["parameters"],
            )
            .unwrap(),
        );
        let direction = parameters
            .biases
            .iter()
            .find(|b| b.enabled)
            .unwrap()
            .direction;
        let membership = ["ES20261110C6000".to_owned()]
            .into_iter()
            .collect::<std::collections::BTreeSet<_>>();
        let trade_scope = Scope {
            options: [43].into_iter().collect(),
            monitored: [43].into_iter().collect(),
            caches: vec![
                ifm_market_data_native::dataset::options_owner::CacheScope {
                    scope: "complete-cache".into(),
                    horizon: "Weekly".into(),
                    contracts: membership.clone(),
                    ranking: None,
                },
                ifm_market_data_native::dataset::options_owner::CacheScope {
                    scope: "no-candidates".into(),
                    horizon: "Weekly".into(),
                    contracts: membership,
                    ranking: Some(ifm_market_data_native::option_chain::strategy::Ranking {
                        parameters,
                        direction,
                        date,
                    }),
                },
            ],
        };
        assert!(
            claims
                .configure_and_acquire(0, "rejected", trade_scope.clone(), Lifetime::Durable)
                .is_err()
        );
        assert!(claims.active().unwrap().is_empty());
        assert!(
            claims
                .acquire(112, &["rejected".into()], Lifetime::Durable)
                .is_err(),
            "Rejected atomic acquisition must not leave a configured scope"
        );
        claims
            .configure_and_acquire(111, "trade", trade_scope.clone(), Lifetime::Durable)
            .unwrap();
        tokio::time::timeout(
            Duration::from_secs(6),
            market.wait_ready(Lane::OptionsGlbx, true),
        )
        .await
        .unwrap()
        .unwrap();
        let receipt = market
            .scope_receipt(
                "trade",
                111u128.to_le_bytes(),
                date + time::Duration::days(14),
            )
            .unwrap();
        let fields = receipt.as_array().unwrap();
        assert_eq!(fields.len(), 12);
        assert_eq!(fields[0].as_str(), Some("trade"));
        let options = fields[6].as_array().unwrap();
        assert_eq!(options.len(), 1);
        assert_eq!(
            options[0].as_array().unwrap()[0].as_array().unwrap().len(),
            10
        );
        assert!(
            market
                .scope_receipt("trade", 112u128.to_le_bytes(), date)
                .unwrap()
                .is_nil(),
            "Another business owner must not borrow this receipt"
        );
        if let Ok(path) = std::env::var("IFM_NATIVE_SCOPE_RECEIPT_OUTPUT") {
            std::fs::write(path, codec::encode(&receipt).unwrap()).unwrap();
        }
        let workload = market.read_snapshot(true).unwrap();
        assert!(
            workload.as_array().unwrap()[7].as_array().unwrap()[4]
                .as_array()
                .unwrap()
                .iter()
                .any(|id| id.as_str() == Some("trade")),
            "Optional no-candidate cache must not hide readiness of a fully monitored trade"
        );
        // Trade monitoring can be verified before optional background cache
        // composition finishes. Qualify those publications separately.
        tokio::time::timeout(Duration::from_secs(6), async {
            loop {
                let complete = market.options_current().is_some_and(|view| {
                    let caches=view.caches.lock().unwrap();
                    caches.contains_key("complete-cache") && caches.contains_key("no-candidates")
                });
                if complete {break;}
                tokio::time::sleep(Duration::from_millis(10)).await;
            }
        }).await.expect("Ready trade never produced its optional cache results");
        let published = market.options_current().unwrap();
        assert!(
            published
                .caches
                .lock()
                .unwrap()
                .contains_key("complete-cache")
        );
        {
            let caches = published.caches.lock().unwrap();
            let empty = caches
                .get("no-candidates")
                .expect("Qualified no-candidate result must be explicit");
            assert!(
                empty.snapshot.instruments.is_empty() && empty.snapshot.ranking.is_some(),
                "A single call must produce no eligible candidates, never a fabricated four-leg spread"
            );
        }
        let ticker = market.option_ticker("ES20261110C6000").unwrap();
        assert_eq!(ticker.as_array().unwrap().len(), 2);
        assert!(
            !ticker.as_array().unwrap()[1].is_nil(),
            "Monitored native ticker must preserve matching qualified Greeks"
        );
        if let Ok(path) = std::env::var("IFM_NATIVE_TICKER_OUTPUT") {
            std::fs::write(path, codec::encode(&ticker).unwrap()).unwrap();
        }
        let stable_ticket = market.health(Lane::OptionsGlbx).status().ticket;
        market
            .merge_raw_catalog_once(
                intent.clone(),
                [41; 32],
                vec![("ES20261110C6000".into(), [42; 32])],
            )
            .unwrap();
        assert_eq!(
            market.health(Lane::OptionsGlbx).status().ticket,
            stable_ticket
        );
        assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
        claims
            .configure_and_acquire(111, "trade", trade_scope, Lifetime::Durable)
            .unwrap();
        assert_eq!(
            market.health(Lane::OptionsGlbx).status().ticket,
            stable_ticket
        );
        let invalid_scope = Scope {
            options: [999].into_iter().collect(),
            monitored: Default::default(),
            caches: vec![],
        };
        assert!(
            claims
                .configure_and_acquire(112, "unknown", invalid_scope, Lifetime::Durable)
                .is_err()
        );
        assert_eq!(
            claims.active().unwrap(),
            ["trade".to_owned()].into_iter().collect()
        );
        assert_eq!(
            market.health(Lane::OptionsGlbx).status().ticket,
            stable_ticket
        );
        let raw_quote = market
            .raw_prices(
                Lane::OptionsGlbx,
                "ES20261110C6000",
                date.to_julian_day() - 1721426,
            )
            .unwrap()
            .unwrap();
        let raw_quote = raw_quote.as_array().unwrap()[1].as_array().unwrap();
        assert_ne!(
            raw_quote[10], raw_quote[11],
            "Fixture must distinguish provider receipt and local ingestion"
        );
        assert!(
            !market
                .canonical_risk("ES20261110C6000", date.to_julian_day() - 1721426, false)
                .unwrap()
                .is_nil(),
            "Native quote risk must join using the original local ingestion time, not provider receipt time"
        );
        assert!(
            market
                .canonical_risk("ES20261110C6000", date.to_julian_day() - 1721426 + 1, false)
                .unwrap()
                .is_nil()
        );
        if let Ok(path) = std::env::var("IFM_NATIVE_RISK_OUTPUT") {
            let value = market
                .canonical_risk("ES20261110C6000", date.to_julian_day() - 1721426, false)
                .unwrap();
            std::fs::write(path, codec::encode(&value).unwrap()).unwrap();
        }
        let retained = market.options_current().unwrap();
        let request = || Request {
            id: [7; 16],
            scope: "trade".into(),
            generation: market
                .publication_identity(Lane::OptionsGlbx)
                .unwrap()
                .generation,
            horizon: "Daily".into(),
            at: clock.utc_ticks().unwrap(),
            until: clock.utc_ticks().unwrap() + 10_000_000,
            maximum: 2048,
            age: 1000,
            skew: 250,
            options: true,
            selection: false,
            allow_missing: false,
            risk: Default::default(),
        };
        let captured = market
            .capture_options(
                request(),
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(3)),
            )
            .await
            .unwrap();
        assert_eq!(captured.instruments.len(), 1);
        assert!(captured.instruments[0].valuation.is_some());
        let mut invalid = request();
        invalid.id = [0; 16];
        assert_eq!(
            market
                .capture_options(invalid, &CancellationToken::new(), Deadline::recovery())
                .await
                .err()
                .unwrap()
                .code,
            "InvalidSnapshotRequest"
        );
        let mut ended = request();
        ended.scope = "missing".into();
        assert_eq!(
            market
                .capture_options(ended, &CancellationToken::new(), Deadline::recovery())
                .await
                .err()
                .unwrap()
                .code,
            "LeaseEnded"
        );
        let mut stale = request();
        stale.generation = [9; 16];
        assert_eq!(
            market
                .capture_options(stale, &CancellationToken::new(), Deadline::recovery())
                .await
                .err()
                .unwrap()
                .code,
            "Recovering"
        );
        block_references.store(true, Ordering::SeqCst);
        let identity = market.publication_identity(Lane::OptionsGlbx).unwrap();
        let recover = RecoveryRequest {
            lane: Lane::OptionsGlbx,
            operation: [8; 16],
            worker: identity.worker,
            generation: identity.generation,
            date: intent.date,
        };
        let rejected = market.begin_recovery(&recover).unwrap();
        assert!(rejected.is_none());
        let result = tokio::time::timeout(
            Duration::from_secs(6),
            market.recovery_result(&recover, rejected),
        )
        .await
        .unwrap()
        .unwrap();
        assert_eq!(result.as_array().unwrap()[3].as_bool(), Some(true));
        // Financial assertions below require a publication, beyond transport recovery.
        tokio::time::timeout(
            Duration::from_secs(6),
            market.wait_ready(Lane::OptionsGlbx, false),
        )
        .await
        .unwrap()
        .unwrap();
        tokio::time::timeout(Duration::from_secs(1), retained.cancelled())
            .await
            .unwrap();
        assert_ne!(
            market
                .publication_identity(Lane::OptionsGlbx)
                .unwrap()
                .generation,
            identity.generation
        );
        assert_eq!(
            reference_calls.load(Ordering::SeqCst),
            3,
            "native immutable facts bypass the stalled external database during rebuild"
        );
        assert_eq!(fixture.accepted.load(Ordering::SeqCst), 2, "Unexpected source replacement; health={:?}; diagnostics={:?}", market.health(Lane::OptionsGlbx).status(), diagnostics.lock().unwrap());
        if native_background {
            assert_eq!(
                policy_calls.load(Ordering::SeqCst),
                1,
                "Emergency recovery must not wait on a stalled database for pinned facts"
            );
        }
        assert!(!sibling.cancellation().is_cancelled());
        // The next assertions inspect priced publications, not transport alone.
        tokio::time::timeout(
            Duration::from_secs(6),
            market.wait_ready(Lane::OptionsGlbx, false),
        )
        .await
        .unwrap()
        .unwrap();
        let next = market
            .capture_options(request(), &CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
        assert_ne!(captured.generation, next.generation);
        assert_eq!(retained.header.generation, identity.generation);
        let catchup = market.read_snapshot(true).unwrap();
        assert!(
            catchup.as_array().unwrap()[6]
                .as_array()
                .unwrap()
                .iter()
                .all(|frame| frame.as_array().unwrap()[6].as_u64() == Some(7)),
            "Quote catch-up must not contain durable trade evidence with another DTO"
        );
        assert!(
            !catchup.as_array().unwrap()[6]
                .as_array()
                .unwrap()
                .is_empty()
        );
        market.shutdown(Deadline::recovery()).await.unwrap();
        drop(sibling);
        root.stop().unwrap();
        root.wait_idle(&CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
        assert!(root.disposable().unwrap());
        fixture.await_closed().await;
        stop.cancel();
        server.await.unwrap();
        http_server.await.unwrap();
        fixture.stop().await;
        return;
    }
    let factory = Arc::new(OptionsFactory::new(clock));
    let controller = Arc::new(
        Supervisor::start(
            root.clone(),
            factory.clone(),
            None,
            Requirements::new(&[]),
            Deadline::after(Duration::from_secs(5)),
        )
        .unwrap(),
    );
    use ifm_market_data_native::{
        dataset::option_claims::{Coordinator, Scope},
        subscriptions::leases::Lifetime,
    };
    let claims = Coordinator::start(&root, controller.clone(), intent.clone()).unwrap();
    claims
        .configure(
            "trade",
            Scope {
                options: [43].into_iter().collect(),
                monitored: [43].into_iter().collect(),
                caches: vec![],
            },
        )
        .unwrap();
    let accepted_at = tokio::time::Instant::now();
    let trade_claim = claims
        .acquire(111, &["trade".into()], Lifetime::Durable)
        .unwrap();
    let health = controller.recovery();
    let mut changes = health.subscribe();
    async fn healthy(
        health: &ifm_market_data_native::runtime::recovery::Recovery,
        changes: &mut tokio::sync::watch::Receiver<
            ifm_market_data_native::runtime::recovery::Status,
        >,
    ) {
        tokio::time::timeout(Duration::from_secs(6), async {
            loop {
                let status = health.status();
                assert_ne!(status.phase, RecoveryPhase::Failed, "{:?}", status);
                if status.phase == RecoveryPhase::Running {
                    break;
                }
                changes.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
    }
    if stall_reference {
        tokio::time::timeout(Duration::from_secs(124), async {
            while health.failures(0, 16).is_empty() {
                changes.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
        assert!(
            accepted_at.elapsed() >= Duration::from_secs(120)
                && accepted_at.elapsed() < Duration::from_secs(124)
        );
        assert_eq!(health.failures(0, 16).len(), 1);
        assert_eq!(
            fixture.accepted.load(Ordering::SeqCst),
            0,
            "no source can start before complete native references"
        );
        assert!(
            (1..=2).contains(&reference_calls.load(Ordering::SeqCst)),
            "one original acquisition and at most one final native reset"
        );
        assert!(!sibling.cancellation().is_cancelled());
        root.stop().unwrap();
        claims.cancel();
        claims.join(Deadline::recovery()).await.unwrap();
        controller.join(Deadline::recovery()).await.unwrap();
        drop(sibling);
        root.wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
        assert!(root.disposable().unwrap());
        stop.cancel();
        server.await.unwrap();
        http_server.await.unwrap();
        fixture.stop().await;
        return;
    }
    healthy(&health, &mut changes).await;
    assert!(persisted.load(Ordering::SeqCst));
    assert_eq!(
        reference_calls.load(Ordering::SeqCst),
        if stage_reference { 3 } else { 2 },
        "native setup acquired exact mapping and calendar facts"
    );
    block_references.store(true, Ordering::SeqCst);
    if stage_reference {
        let mut extended = (*intent).clone();
        extended.reference_bootstrap = Some(Arc::new(
            ifm_market_data_native::option_chain::reference_bootstrap::Bootstrap::new(
                extended.broker.clone(),
                extended.scope.clone(),
            )
            .unwrap(),
        ));
        let reviewed = Arc::make_mut(&mut extended.reviewed);
        let mut extra = reviewed.definitions[0].clone();
        extra.instrument = 44;
        extra.strike = decimal::parse("6005").unwrap();
        let mut contract = extra.blueprint.contract.value();
        let rmpv::Value::Array(ref mut fields) = contract else {
            panic!("option metadata array")
        };
        fields[1] = "ES20261110C6005".into();
        fields[4] = 44.into();
        fields[27] = "6005".into();
        extra.blueprint.contract = Convention::new(contract).unwrap();
        reviewed.definitions.push(extra);
        let mut registration = extended
            .source
            .registrations
            .iter()
            .find(|r| r.instrument == 43)
            .unwrap()
            .clone();
        registration.instrument = 44;
        registration.source = "44".into();
        registration.contract = "ES20261110C6005".into();
        extended.source.registrations.push(registration);
        for subscription in &mut extended.source.plan.subscriptions {
            subscription.symbols.push("44".into());
        }
        claims.merge_catalog(Arc::new(extended)).unwrap();
        healthy(&health, &mut changes).await;
        assert_eq!(
            claims.active().unwrap(),
            ["trade".to_string()].into_iter().collect()
        );
        assert_eq!(
            reference_calls.load(Ordering::SeqCst),
            3,
            "adding unused metadata reused the native facts despite a stalled database responder"
        );
        assert!(
            factory
                .view()
                .unwrap()
                .registrations
                .iter()
                .all(|r| r.instrument != 44),
            "unclaimed catalog rows must not subscribe"
        );
    }
    let accepted_revision = controller.revision();
    claims.validate_date(intent.date).unwrap();
    assert!(
        claims
            .validate_date(intent.date.next_day().unwrap())
            .is_err(),
        "active date cannot redirect failure messages"
    );
    claims.monitor(222, "trade", 43).unwrap();
    claims.monitor(222, "trade", 43).unwrap();
    assert_eq!(
        controller.revision(),
        accepted_revision,
        "shared/idempotent native monitoring must not restart a healthy provider"
    );
    assert!(claims.monitor(222, "trade", 999).is_err());
    assert!(claims.monitor(0, "trade", 43).is_err());
    assert_eq!(
        controller.revision(),
        accepted_revision,
        "invalid monitored commands must preserve healthy desired intent"
    );
    claims.disable(222).unwrap();
    claims.disable(222).unwrap();
    assert_eq!(
        controller.revision(),
        accepted_revision,
        "another workflow's release cannot stop the live trade"
    );
    assert_eq!(
        claims.active().unwrap(),
        ["trade".to_string()].into_iter().collect()
    );
    assert_eq!(
        claims
            .acquire(111, &["trade".into()], Lifetime::Durable)
            .unwrap(),
        trade_claim
    );
    assert_eq!(
        controller.revision(),
        accepted_revision,
        "idempotent durable claim must not rebuild its provider"
    );
    assert!(
        claims
            .acquire(333, &["unconfigured".into()], Lifetime::Durable)
            .is_err()
    );
    assert!(
        claims
            .configure(
                "unknown",
                Scope {
                    options: [999].into_iter().collect(),
                    monitored: Default::default(),
                    caches: vec![]
                }
            )
            .is_err()
    );
    assert!(
        claims
            .configure(
                "trade",
                Scope {
                    options: [43].into_iter().collect(),
                    monitored: Default::default(),
                    caches: vec![]
                }
            )
            .is_err(),
        "active scope cannot change beneath another owner"
    );
    let mut stale = trade_claim;
    stale.serial += 1;
    claims.release(stale).unwrap();
    assert_eq!(
        controller.revision(),
        accepted_revision,
        "invalid or stale control cannot alter accepted native intent"
    );

    let old = factory.view().unwrap();
    let identity = factory.identity().unwrap();
    assert_eq!(identity.worker, old.header.worker);
    assert_eq!(identity.generation, old.header.generation);
    assert_eq!(identity.revision, old.header.revision);
    assert!(old.pricing.ready(at).unwrap());
    assert!(
        old.queries
            .publication_state(43)
            .unwrap()
            .unwrap()
            .1
            .market
            .is_some()
    );

    assert_eq!(old.evidence.retained_bytes().unwrap(), 0);
    let acknowledged = old
        .current
        .capture(0, 256)
        .unwrap()
        .into_iter()
        .find(|f| f.kind == 6)
        .expect("durably acknowledged native trade retained for query");
    let frozen = rmpv::decode::read_value(&mut acknowledged.payload.as_slice()).unwrap();
    let contract = frozen.as_array().unwrap()[0].as_array().unwrap()[3]
        .as_str()
        .unwrap();
    assert_eq!(
        old.current.latest(contract, 6).unwrap().unwrap().payload,
        acknowledged.payload
    );

    let mut observed = std::collections::BTreeSet::new();
    for _ in 0..2 {
        let bytes = tokio::time::timeout(Duration::from_secs(2), events.recv())
            .await
            .unwrap()
            .unwrap();
        let outer = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
        let array = outer.as_array().unwrap();
        let rmpv::Value::Ext(98, size) = &array[0] else {
            panic!("compressed native frame")
        };
        let length = rmpv::decode::read_value(&mut size.as_slice())
            .unwrap()
            .as_u64()
            .unwrap() as usize;
        let content = lz4_flex::block::decompress(array[1].as_slice().unwrap(), length).unwrap();
        let value = rmpv::decode::read_value(&mut content.as_slice()).unwrap();
        let fields = value.as_array().unwrap();
        observed.insert(fields[6].as_u64().unwrap());
    }
    assert_eq!(observed, [6, 7].into_iter().collect());
    controller
        .report_fault(
            "injected native options processing failure",
            Deadline::after(Duration::from_secs(5)),
        )
        .unwrap();
    healthy(&health, &mut changes).await;
    assert_ne!(
        old.header.generation,
        factory.view().unwrap().header.generation
    );
    assert!(
        old.queries.publication_state(43).unwrap().is_none(),
        "retained numeric queries retire without owning any provider resource"
    );
    assert!(!sibling.cancellation().is_cancelled());
    assert_eq!(
        fixture.accepted.load(Ordering::SeqCst),
        if stage_reference { 3 } else { 2 }
    );
    assert_eq!(
        claims.active().unwrap(),
        ["trade".to_owned()].into_iter().collect(),
        "physical recovery must retain durable native business intent"
    );
    let background_scope = ifm_market_data_native::dataset::options_owner::CacheScope {
        ranking: None,
        scope: "native-background".into(),
        horizon: "Daily".into(),
        contracts: intent
            .reviewed
            .definitions
            .iter()
            .map(|d| d.blueprint.contract.contract().into())
            .collect(),
    };
    claims
        .configure(
            "background",
            Scope {
                options: [43].into_iter().collect(),
                monitored: Default::default(),
                caches: vec![background_scope],
            },
        )
        .unwrap();
    let background_claim = claims
        .acquire(
            222,
            &["background".into()],
            Lifetime::Ephemeral(Duration::from_secs(2)),
        )
        .unwrap();
    healthy(&health, &mut changes).await;
    assert_eq!(
        fixture.accepted.load(Ordering::SeqCst),
        if stage_reference { 4 } else { 3 }
    );
    claims.release(trade_claim).unwrap();
    healthy(&health, &mut changes).await;
    let current = factory.view().unwrap();
    let copied = current
        .caches
        .lock()
        .unwrap()
        .get("native-background")
        .unwrap()
        .clone();
    assert!(!copied.is_empty());
    assert_eq!(copied.scope, "native-background");
    assert_eq!(copied.generation, current.header.generation);
    assert_eq!(
        fixture.accepted.load(Ordering::SeqCst),
        if stage_reference { 5 } else { 4 }
    );
    assert!(!sibling.cancellation().is_cancelled());
    tokio::time::timeout(Duration::from_secs(4), async {
        loop {
            if claims.active().unwrap().is_empty()
                && health.status().phase == RecoveryPhase::Running
            {
                break;
            }
            changes.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    assert!(
        claims
            .renew(background_claim, Duration::from_secs(1))
            .is_err(),
        "native expiry cannot be revived by a delayed C# command"
    );
    claims.release(trade_claim).unwrap(); // stale release cannot change another owner.
    assert_eq!(
        reference_calls.load(Ordering::SeqCst),
        if stage_reference { 3 } else { 2 },
        "joined native resets and membership updates reuse immutable facts while external storage is stalled"
    );
    claims.cancel();
    claims.join(Deadline::recovery()).await.unwrap();
    controller.cancel();
    controller.join(Deadline::recovery()).await.unwrap();
    fixture.await_closed().await;
    drop(sibling);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    stop.cancel();
    server.await.unwrap();
    http_server.await.unwrap();
    fixture.stop().await;
}

/// A setup-only quote session is physically retired before its copied prices
/// return. A stalled quote source is cancelled beneath the same dataset parent.
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_background_underlying_quotes_join_before_return_and_cancel_stalled_setup() {
    // The actual-DLL tests install the nonblocking production panic hook for
    // this whole test process. Copy its evidence on a test failure so parallel
    // qualification never reduces a useful assertion to a silent FAILED line.
    struct FailureEvidence;
    impl Drop for FailureEvidence {
        fn drop(&mut self) {
            if std::thread::panicking() {
                for record in ifm_market_data_native::runtime::panic_capture::capture().2 {
                    eprintln!(
                        "Native captured panic {}:{}: {}",
                        record.file(),
                        record.line(),
                        record.message()
                    );
                }
            }
        }
    }
    let _failure_evidence = FailureEvidence;
    use ifm_market_data_native::{
        dataset::{
            bootstrap_quotes,
            live::{Evidence, Registration, Start},
        },
        latest::Asset,
        reference::decimal,
        runtime::{clock::Clock, owner::Owner},
    };
    let at = 17_931_024_000_000_000_i64;
    let root = Arc::new(Owner::default());
    let clock = Arc::new(Clock::new(at, false).unwrap());
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let make = |gateway| Start {
        plan: SessionPlan {
            lane: Lane::OptionsGlbx,
            subscriptions: vec![
                SubscriptionSpec {
                    symbols: vec!["42".into()],
                    symbology: SType::InstrumentId,
                    schema: Schema::Trades,
                    replay_start_ns: None,
                },
                SubscriptionSpec {
                    symbols: vec!["42".into()],
                    symbology: SType::InstrumentId,
                    schema: Schema::Mbp1,
                    replay_start_ns: None,
                },
            ],
        },
        registrations: vec![Registration {
            instrument: 42,
            publisher: 3,
            contract: "ES20261218".into(),
            source: "42".into(),
            asset: Asset::Futures,
            tick: None,
            vwap: false,
            required: true,
        }],
        key: "x".repeat(32),
        gateway,
        evidence: Evidence::Observations,
    };
    let fixture = Fixture::new(Mode::PricingTradeAt(u64::try_from(at).unwrap() * 100)).await;
    let parent = Arc::new(root.child().unwrap());
    let prices = bootstrap_quotes::acquire(
        make(fixture.gateway()),
        date,
        clock.clone(),
        parent.clone(),
        &CancellationToken::new(),
        Deadline::after(Duration::from_secs(3)),
    )
    .await
    .unwrap();
    assert_eq!(prices.len(), 1);
    assert!(
        prices["ES20261218"]
            .compare(decimal::parse("0").unwrap())
            .unwrap()
            .is_gt()
    );
    fixture.await_closed().await;
    assert_eq!(fixture.accepted.load(Ordering::SeqCst), 1);
    let stalled = Fixture::new(Mode::QuotesAckOnly).await;
    let stopped = CancellationToken::new();
    let cancellation = stopped.clone();
    let (ready, started) = tokio::sync::oneshot::channel();
    let gateway = stalled.gateway();
    let active_parent = parent.clone();
    let task = tokio::spawn(async move {
        let _ = ready.send(());
        bootstrap_quotes::acquire(
            make(gateway),
            date,
            clock,
            active_parent,
            &cancellation,
            Deadline::recovery(),
        )
        .await
    });
    started.await.unwrap();
    // Qualification-only fixture notification, never a production polling loop.
    tokio::time::timeout(Duration::from_secs(2), async {
        loop {
            let notified = stalled.accepted_notice.notified();
            if stalled.accepted.load(Ordering::SeqCst) > 0 {
                break;
            }
            notified.await;
        }
    })
    .await
    .unwrap();
    stopped.cancel();
    assert!(
        tokio::time::timeout(Duration::from_secs(2), task)
            .await
            .unwrap()
            .unwrap()
            .is_err()
    );
    stalled.await_closed().await;
    drop(parent);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    fixture.stop().await;
    stalled.stop().await;
}
