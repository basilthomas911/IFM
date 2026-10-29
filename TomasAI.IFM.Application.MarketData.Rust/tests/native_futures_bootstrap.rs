//! Native metadata/bootstrap cancellation and full plan construction.
use databento::dbn::{SType, Schema};
use ifm_market_data_native::{
    dataset::{
        futures_bootstrap::{Discovery, Root, session_start},
        lanes::Gateway,
        live::{Evidence, Start},
        transport::{Lane, SessionPlan, SubscriptionSpec},
    },
    dns::Resolver,
    historical::History,
    runtime::Deadline,
};
use std::{sync::Arc, time::Duration};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpListener,
};
use tokio_util::sync::CancellationToken;
fn resolver() -> Arc<Resolver> {
    Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap())
}
fn source() -> Start {
    Start {
        plan: SessionPlan {
            lane: Lane::FuturesGlbx,
            subscriptions: [Schema::Mbp1, Schema::Trades, Schema::Statistics]
                .into_iter()
                .map(|schema| SubscriptionSpec {
                    symbols: vec!["ES.FUT".into()],
                    schema,
                    symbology: SType::RawSymbol,
                    replay_start_ns: None,
                })
                .collect(),
        },
        registrations: vec![],
        key: "x".repeat(32),
        gateway: Gateway {
            host: "127.0.0.1".into(),
            port: 9,
            resolver: resolver(),
        },
        evidence: Evidence::Observations,
    }
}
fn date() -> time::Date {
    time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap()
}
#[tokio::test]
async fn complete_raw_provider_lookup_builds_native_contract_and_replay_plan() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let endpoint = format!("http://{}/", listener.local_addr().unwrap());
    let baseline: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    let line = baseline[0]["json"].as_str().unwrap().to_owned();
    let peer = tokio::spawn(async move {
        for index in 0..2 {
            let (mut socket, _) = listener.accept().await.unwrap();
            let mut headers = Vec::new();
            let mut byte = [0];
            while !headers.ends_with(b"\r\n\r\n") {
                socket.read_exact(&mut byte).await.unwrap();
                headers.push(byte[0]);
                assert!(headers.len() < 8192);
            }
            let headers = String::from_utf8(headers).unwrap();
            let length = headers
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
            let response = if index == 0 {
                assert!(headers.starts_with("GET /v0/metadata.get_dataset_range"));
                r#"{"start":"2025-01-01T00:00:00Z","end":"2026-10-27T00:00:00Z","schema":{"definition":{"start":"2025-01-01T00:00:00Z","end":"2026-10-27T00:00:00Z"}}}"#.to_owned()
            } else {
                let form: std::collections::BTreeMap<String, String> =
                    serde_urlencoded::from_bytes(&form).unwrap();
                assert_eq!(form["symbols"], "ES.FUT");
                assert_eq!(form["stype_in"], "parent");
                assert_eq!(form["start"], "2026-10-26");
                format!("{line}\n")
            };
            socket
                .write_all(
                    format!(
                        "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",
                        response.len()
                    )
                    .as_bytes(),
                )
                .await
                .unwrap();
            for chunk in response.as_bytes().chunks(31) {
                socket.write_all(chunk).await.unwrap();
            }
        }
    });
    let discovery = Discovery {
        history: Arc::new(History::new(&endpoint, "x".repeat(32), resolver()).unwrap()),
        roots: vec![Root {
            symbol: "ES".into(),
            count: 1,
            fallback: Some("USD".into()),
            vwap: true,
        }],
        fixed: vec![ifm_market_data_native::option_chain::committed::Route {
            contract: "ES20261225".into(),
            symbol: "ESZ6".into(),
            root: "ES".into(),
        }],
        session_replay: true,
    };
    let result = discovery
        .resolve_with_models(
            &source(),
            date(),
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(3)),
        )
        .await
        .unwrap();
    assert_eq!(result.2.len(), 1);
    let public = result.2[0].as_array().unwrap();
    assert_eq!(public.len(), 32);
    assert_eq!(public[0].as_str(), Some("ES20261225"));
    assert!(
        public[13].is_nil() && public[14].is_nil() && public[15].is_nil() && public[16].is_nil(),
        "configured catalog does not invent reviewed reference metadata"
    );
    assert_eq!(result.1.len(), 1);
    let fields = result.1[0].as_array().unwrap();
    assert_eq!(fields.len(), 32);
    assert_eq!(fields[0].as_str(), Some("ES20261225"));
    assert_eq!(fields[3].as_str(), Some("ESZ6"));
    let result = result.0;
    assert_eq!(result.registrations.len(), 1);
    let registration = &result.registrations[0];
    assert_eq!(registration.contract, "ES20261225");
    assert_eq!(registration.source, "ESZ6");
    assert_eq!(registration.instrument, 42);
    assert_eq!(registration.publisher, 1);
    assert_eq!(registration.tick, Some(250_000_000));
    assert!(registration.vwap && registration.required);
    for subscription in &result.plan.subscriptions {
        assert_eq!(subscription.symbols, ["ESZ6"]);
        assert_eq!(subscription.symbology, SType::RawSymbol);
        assert_eq!(
            subscription.replay_start_ns,
            if matches!(subscription.schema, Schema::Trades | Schema::Statistics) {
                Some(session_start(date()).unwrap())
            } else {
                None
            }
        );
    }
    assert!(
        source().registrations.is_empty(),
        "no caller-owned template is mutated"
    );
    peer.await.unwrap();
}
#[tokio::test]
async fn stalled_bootstrap_is_cancelled_under_the_original_native_budget() {
    for cancellation in [false, true] {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("http://{}/", listener.local_addr().unwrap());
        let (arrived, accepted) = tokio::sync::oneshot::channel();
        let peer = tokio::spawn(async move {
            let (mut socket, _) = listener.accept().await.unwrap();
            let _ = arrived.send(());
            let mut byte = [0];
            while socket.read(&mut byte).await.unwrap_or(0) != 0 {}
        });
        let discovery = Discovery {
            history: Arc::new(History::new(&endpoint, "x".repeat(32), resolver()).unwrap()),
            roots: vec![Root {
                symbol: "ES".into(),
                count: 1,
                fallback: None,
                vwap: true,
            }],
            fixed: vec![],
            session_replay: false,
        };
        let stop = CancellationToken::new();
        let child = stop.clone();
        let work = tokio::spawn(async move {
            discovery
                .resolve(
                    &source(),
                    date(),
                    &child,
                    Deadline::after(Duration::from_millis(150)),
                )
                .await
        });
        accepted.await.unwrap();
        if cancellation {
            stop.cancel();
        }
        let result = tokio::time::timeout(Duration::from_secs(2), work)
            .await
            .unwrap()
            .unwrap();
        assert!(result.is_err());
        tokio::time::timeout(Duration::from_secs(1), peer)
            .await
            .unwrap()
            .unwrap();
    }
}
#[test]
fn explicit_value_date_uses_pinned_dst_session_start() {
    for (month, day, hour) in [(3, 9, 22), (11, 2, 23), (10, 27, 22)] {
        let date = time::Date::from_calendar_date(2026, time::Month::try_from(month).unwrap(), day)
            .unwrap();
        let instant = time::OffsetDateTime::from_unix_timestamp_nanos(i128::from(
            session_start(date).unwrap(),
        ))
        .unwrap();
        assert_eq!(instant.hour(), hour);
        assert_eq!(instant.date(), date.previous_day().unwrap());
    }
    assert!(
        session_start(time::Date::from_calendar_date(1, time::Month::January, 1).unwrap()).is_err()
    );
}

#[tokio::test]
async fn dataset_resource_group_cancels_and_joins_a_stalled_bootstrap_without_a_managed_waiter() {
    use ifm_market_data_native::{
        dataset::{
            futures_owner::{FuturesFactory, Intent},
            supervisor::Factory,
        },
        messaging::publisher::BrokerSettings,
        runtime::{clock::Clock, owner::Owner},
    };
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let endpoint = format!("http://{}/", listener.local_addr().unwrap());
    let (arrived, accepted) = tokio::sync::oneshot::channel();
    let peer = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        let _ = arrived.send(());
        let mut byte = [0];
        while socket.read(&mut byte).await.unwrap_or(0) != 0 {}
    });
    let root = Arc::new(Owner::default());
    let sibling = Arc::new(root.child().unwrap());
    let child = Arc::new(root.child().unwrap());
    let clock = Arc::new(Clock::new(17931024000000000, false).unwrap());
    let factory = FuturesFactory::new(clock);
    let intent = Arc::new(Intent {
        bootstrap: Some(Arc::new(Discovery {
            history: Arc::new(History::new(&endpoint, "x".repeat(32), resolver()).unwrap()),
            roots: vec![Root {
                symbol: "ES".into(),
                count: 1,
                fallback: None,
                vwap: true,
            }],
            fixed: vec![],
            session_replay: false,
        })),
        date: date(),
        definition: date(),
        source: source(),
        broker: BrokerSettings::new(
            "nats://127.0.0.1:9".into(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        scope: "test".into(),
        revision: 1,
    });
    let mut resource = factory.start(intent, child, Deadline::recovery()).unwrap();
    accepted.await.unwrap();
    assert!(factory.registrations().is_none());
    assert!(factory.current().is_none());
    assert!(resource.changes().borrow().proofs.is_empty());
    let before = tokio::time::Instant::now();
    resource.cancel(Deadline::recovery());
    tokio::time::timeout(Duration::from_secs(1), resource.join(Deadline::recovery()))
        .await
        .unwrap()
        .unwrap();
    assert!(before.elapsed() < Duration::from_secs(1));
    drop(resource);
    assert!(!sibling.cancellation().is_cancelled());
    drop(sibling);
    tokio::time::timeout(Duration::from_secs(1), peer)
        .await
        .unwrap()
        .unwrap();
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
}

/// This production-duration gate is explicit so ordinary offline runs stay fast.
#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Production-duration 120-second native bootstrap deadline qualification"]
async fn original_120_second_bootstrap_deadline_reaches_observable_terminal_state() {
    use ifm_market_data_native::{
        dataset::{
            futures_owner::{FuturesFactory, Intent},
            supervisor::Supervisor,
        },
        messaging::publisher::BrokerSettings,
        runtime::{
            clock::Clock,
            owner::Owner,
            recovery::{Phase, Proof, Requirements},
        },
    };
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let endpoint = format!("http://{}/", listener.local_addr().unwrap());
    let peer = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        let mut byte = [0];
        while socket.read(&mut byte).await.unwrap_or(0) != 0 {}
    });
    let root = Arc::new(Owner::default());
    let factory = Arc::new(FuturesFactory::new(Arc::new(
        Clock::new(17931024000000000, false).unwrap(),
    )));
    let intent = Arc::new(Intent {
        bootstrap: Some(Arc::new(Discovery {
            history: Arc::new(History::new(&endpoint, "x".repeat(32), resolver()).unwrap()),
            roots: vec![Root {
                symbol: "ES".into(),
                count: 1,
                fallback: None,
                vwap: true,
            }],
            fixed: vec![],
            session_replay: false,
        })),
        date: date(),
        definition: date(),
        source: source(),
        broker: BrokerSettings::new(
            "nats://127.0.0.1:9".into(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        scope: "test".into(),
        revision: 1,
    });
    let before = tokio::time::Instant::now();
    let control = Supervisor::start(
        root.clone(),
        factory,
        Some(intent),
        Requirements::new(&[Proof::Transport, Proof::Publication]),
        Deadline::recovery(),
    )
    .unwrap();
    let health = control.recovery();
    let mut updates = health.subscribe();
    tokio::time::timeout(Duration::from_secs(124), async {
        while health.status().phase != Phase::Failed {
            updates.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    assert!(
        before.elapsed() >= Duration::from_secs(120) && before.elapsed() < Duration::from_secs(124)
    );
    let notices = health.failures(0, 16);
    assert_eq!(notices.len(), 1);
    assert_eq!(notices[0].status.phase, Phase::Failed);
    root.stop().unwrap();
    control.join(Deadline::recovery()).await.unwrap();
    root.wait_idle(&CancellationToken::new(), Deadline::recovery())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    tokio::time::timeout(Duration::from_secs(1), peer)
        .await
        .unwrap()
        .unwrap();
}

#[test]
fn fixed_committed_routes_use_exact_symbol_without_front_substitution() {
    use ifm_market_data_native::{
        catalog::Definition, dataset::futures_bootstrap::materialize_fixed,
        option_chain::committed::Route,
    };
    let baseline: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    let row =
        Arc::new(Definition::parse("GLBX.MDP3", baseline[0]["json"].as_str().unwrap()).unwrap());
    let route = Route {
        contract: "fixed-position-ESZ6".into(),
        symbol: row.symbol.clone(),
        root: row.asset.clone(),
    };
    let stop = CancellationToken::new();
    let deadline = Deadline::recovery();
    let result = materialize_fixed(
        std::slice::from_ref(&row),
        std::slice::from_ref(&route),
        true,
        &stop,
        deadline,
    )
    .unwrap();
    assert_eq!(result.len(), 1);
    assert_eq!(result[0].contract, route.contract);
    assert_eq!(result[0].source, row.symbol);
    assert_eq!(result[0].instrument, row.instrument);
    assert!(result[0].required && result[0].vwap);
    let mut absent = route.clone();
    absent.symbol = "not-the-requested-contract".into();
    assert!(
        materialize_fixed(
            std::slice::from_ref(&row),
            &[absent],
            false,
            &stop,
            deadline
        )
        .is_err()
    );
    let mut wrong = route.clone();
    wrong.root = "VX".into();
    assert!(
        materialize_fixed(std::slice::from_ref(&row), &[wrong], false, &stop, deadline).is_err()
    );
    let mut deleted = row.as_ref().clone();
    deleted.deleted = true;
    assert!(
        materialize_fixed(
            &[Arc::new(deleted)],
            std::slice::from_ref(&route),
            false,
            &stop,
            deadline
        )
        .is_err()
    );
    assert!(
        materialize_fixed(
            &[row.clone(), row.clone()],
            std::slice::from_ref(&route),
            false,
            &stop,
            deadline
        )
        .is_err()
    );
    assert!(
        materialize_fixed(
            std::slice::from_ref(&row),
            &[route.clone(), route.clone()],
            false,
            &stop,
            deadline
        )
        .is_err()
    );
    stop.cancel();
    assert!(
        materialize_fixed(
            std::slice::from_ref(&row),
            std::slice::from_ref(&route),
            false,
            &stop,
            deadline
        )
        .is_err()
    );
    assert!(
        materialize_fixed(
            &[row],
            &[route],
            false,
            &CancellationToken::new(),
            Deadline::after(Duration::ZERO)
        )
        .is_err()
    );
}
