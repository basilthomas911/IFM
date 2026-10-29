//! Real TCP publication/cancellation checks; no managed receiver publishes data.
use ifm_market_data_native::{
    dataset::transport::Lane,
    messaging::{
        publisher::{BrokerSettings, Publisher},
        wire::Frame,
    },
    runtime::Deadline,
};
use std::time::Duration;
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::TcpListener,
    sync::{mpsc, oneshot},
};

fn frame(sequence: i64) -> Frame {
    Frame {
        lane: Lane::OptionsGlbx,
        value_date: 739915,
        worker: [1; 16],
        generation: [2; 16],
        manifest_revision: 1,
        sequence,
        kind: 7,
        payload: vec![0x92, 0xc0, 0xc3],
    }
}
fn budget() -> Deadline {
    Deadline::after(Duration::from_secs(5))
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn terminal_error_publishes_after_dataset_failure_without_owning_its_slot_or_consuming_the_journal()
 {
    use ifm_market_data_native::{
        messaging::{recovery_delivery::Delivery, recovery_notice::Notice},
        runtime::{
            clock::Clock,
            owner::Owner,
            recovery::{Recovery, Requirements},
        },
    };
    use std::sync::Arc;
    use tokio_util::sync::CancellationToken;
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    for stalled in [false, true] {
        let root = Arc::new(Owner::default());
        let siblings = [
            root.child().unwrap(),
            root.child().unwrap(),
            root.child().unwrap(),
        ];
        let health = [
            Recovery::new(Requirements::new(&[]), budget()),
            Recovery::new(Requirements::new(&[]), budget()),
            Recovery::new(Requirements::new(&[]), budget()),
        ];
        let mut broker = Broker::start(stalled, false).await;
        let settings = BrokerSettings::new(
            broker.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap();
        let mut delivery = Delivery::start(
            &root,
            Arc::new(Clock::new(0, false).unwrap()),
            health.clone(),
            settings,
            "development".into(),
        )
        .unwrap();
        delivery
            .date(Lane::OptionsGlbx, date, "development")
            .unwrap();
        health[2].plan_final_reset(true);
        assert!(
            delivery
                .date(Lane::OptionsGlbx, date.next_day().unwrap(), "development")
                .is_err()
        );
        health[2].fault(
            health[2].status().ticket,
            "injected terminal source failure",
            Deadline::after(Duration::ZERO),
        );
        let terminal = health[2].failures(0, 16).pop().unwrap();
        let notice = Notice::new([1; 16], Lane::OptionsGlbx, date, &terminal, 0).unwrap();
        let decoded = rmpv::decode::read_value(&mut notice.bytes.as_slice()).unwrap();
        let fields = decoded.as_array().unwrap();
        assert_eq!(fields.len(), 20, "existing UI event contract is unchanged");
        let message = fields[7].as_str().unwrap();
        assert!(message.contains("Options/GLBX.MDP3"));
        assert!(message.contains("Resetting this dataset now."));
        assert!(message.contains("Cause: injected terminal source failure"));
        assert!(message.contains("ValueDate: 2026-10-27"));
        assert!(message.contains(fields[2].as_str().unwrap()));
        // Later final failure must not inherit the earlier resetting action,
        // including when the broker delivers both records late.
        health[2].plan_final_reset(false);
        health[2].fail("Final reset could not establish current quotes");
        let failed = health[2].failures(terminal.ordinal, 16).pop().unwrap();
        let final_notice = Notice::new([1; 16], Lane::OptionsGlbx, date, &failed, 0).unwrap();
        let final_decoded = rmpv::decode::read_value(&mut final_notice.bytes.as_slice()).unwrap();
        let final_message = final_decoded.as_array().unwrap()[7].as_str().unwrap();
        assert!(!final_message.contains("Resetting this dataset now."));
        assert!(final_message.contains("Final reset could not establish current quotes"));
        assert_ne!(notice.identity, final_notice.identity);
        assert_eq!(
            notice.identity,
            Notice::new([1; 16], Lane::OptionsGlbx, date, &terminal, 1)
                .unwrap()
                .identity,
            "retry time must not change deduplication identity"
        );
        let bytes = tokio::time::timeout(Duration::from_secs(2), broker.packets.recv())
            .await
            .unwrap()
            .unwrap();
        assert!(!bytes.is_empty());
        assert_eq!(
            health[2].failures(0, 16).len(),
            2,
            "PONG is not UI display confirmation"
        );
        let began = std::time::Instant::now();
        delivery.shutdown(budget()).await.unwrap();
        drop(delivery);
        assert!(began.elapsed() < Duration::from_secs(1));
        assert!(siblings.iter().all(|s| !s.cancellation().is_cancelled()));
        health[2].stop();
        assert_eq!(health[2].failures(0, 16).len(), 2);
        assert!(health[2].acknowledge_failures(terminal.ordinal));
        broker.joined().await;
        if !stalled {
            use base64::{Engine, engine::general_purpose::STANDARD};
            let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("../.artifacts/rust-marketdata-offline/native-recovery-notice.json");
            std::fs::write(path,serde_json::to_vec(&serde_json::json!({"content":STANDARD.encode(&notice.bytes),"subject":notice.subject})).unwrap()).unwrap();
        }
        drop(siblings);
        root.stop().unwrap();
        root.wait_idle(&CancellationToken::new(), budget())
            .await
            .unwrap();
        assert!(root.disposable().unwrap());
    }
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn direct_native_diagnostics_preserve_unknown_codes_and_old_worker_identity_without_pinning_stalled_logs()
 {
    use ifm_market_data_native::{
        dataset::{
            live::{Diagnostic, Diagnostics},
            transport::Event,
        },
        messaging::{diagnostic_delivery::Delivery, wire::guid},
        runtime::owner::Owner,
    };
    use std::sync::Arc;
    use tokio_util::sync::CancellationToken;
    let sink = Arc::new(Diagnostics::default());
    let record = |index| Diagnostic {
        worker: if index == 0 { [9; 16] } else { [7; 16] },
        lane: Lane::OptionsGlbx,
        attempt: 2,
        observed: index,
        replaced: 0,
        event: if index == 0 {
            Event::System {
                code: 250,
                message: "unknown provider warning".into(),
            }
        } else {
            Event::Error {
                code: 199,
                last: true,
                message: format!("unknown provider error {index}"),
            }
        },
    };
    // This record predates the delivery worker, representing a previous physical
    // generation retained for catch-up rather than re-labelled on replacement.
    sink.retain(record(0)).unwrap();
    sink.retain(record(1)).unwrap();
    let mut broker = Broker::start_diagnostics(false).await;
    let root = Arc::new(Owner::default());
    let parent = Arc::new(root.child().unwrap());
    let settings = BrokerSettings::new(
        broker.endpoint.clone(),
        vec!["127.0.0.1:1".parse().unwrap()],
    )
    .unwrap();
    let mut delivery =
        Delivery::start(sink.clone(), settings, "development".into(), parent.clone()).unwrap();
    let mut exported = Vec::new();
    for index in 0..2 {
        let bytes = tokio::time::timeout(Duration::from_secs(2), broker.packets.recv())
            .await
            .unwrap()
            .unwrap();
        let value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
        let fields = value.as_array().unwrap();
        assert_eq!(fields.len(), 10);
        assert_eq!(
            fields[2].as_str(),
            Some(guid(record(index).worker).as_str())
        );
        assert_eq!(fields[6].as_u64(), Some(if index == 0 { 250 } else { 199 }));
        assert_eq!(fields[7].as_bool(), Some(index != 0));
        exported.push(bytes);
    }
    let mut logging = delivery.changes();
    tokio::time::timeout(Duration::from_secs(2), async {
        while logging.borrow().accepted < 2 {
            logging.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    delivery.shutdown(budget()).await.unwrap();
    parent
        .wait_nested(&CancellationToken::new(), budget())
        .await
        .unwrap();
    broker.joined().await;
    // Export actual native wire for the managed DTO/serializer qualification.
    use base64::{Engine, engine::general_purpose::STANDARD};
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("../.artifacts/rust-marketdata-offline/native-diagnostic-frames.json");
    std::fs::create_dir_all(path.parent().unwrap()).unwrap();
    std::fs::write(
        path,
        serde_json::to_vec(
            &exported
                .into_iter()
                .map(|bytes| STANDARD.encode(bytes))
                .collect::<Vec<_>>(),
        )
        .unwrap(),
    )
    .unwrap();
    let mut stalled = Broker::start_diagnostics(true).await;
    let settings = BrokerSettings::new(
        stalled.endpoint.clone(),
        vec!["127.0.0.1:1".parse().unwrap()],
    )
    .unwrap();
    let mut delivery =
        Delivery::start(sink.clone(), settings, "development".into(), parent.clone()).unwrap();
    sink.retain(record(2)).unwrap();
    tokio::time::timeout(Duration::from_secs(2), stalled.packets.recv())
        .await
        .unwrap()
        .unwrap();
    for index in 3..1003 {
        sink.retain(record(index)).unwrap();
    }
    let began = std::time::Instant::now();
    delivery.shutdown(budget()).await.unwrap();
    parent
        .wait_nested(&CancellationToken::new(), budget())
        .await
        .unwrap();
    assert!(began.elapsed() < Duration::from_secs(1));
    let retained = sink.drain(usize::MAX).unwrap();
    assert_eq!(retained.len(), 256);
    assert_eq!(retained.iter().map(|r| r.replaced).sum::<u64>(), 745);
    assert!(
        retained.iter().any(|r| r.observed == 2),
        "unconfirmed in-flight diagnostic survives cancellation"
    );
    stalled.joined().await;
    drop(parent);
    root.stop().unwrap();
    root.wait_idle(&CancellationToken::new(), budget())
        .await
        .unwrap();
    assert!(
        root.disposable().unwrap(),
        "retained diagnostic bytes do not own native resources"
    );
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn cache_latest_replacement_never_waits_for_stalled_flush_and_retained_ports_cannot_pin_shutdown()
 {
    use ifm_market_data_native::{
        messaging::{cache_delivery::Delivery, option_cache::Publication},
        option_chain::composition::model::Snapshot,
        runtime::owner::Owner,
    };
    use std::{collections::BTreeMap, sync::Arc};
    use tokio_util::sync::CancellationToken;
    for stalled in [false, true] {
        let mut broker = Broker::start(stalled, false).await;
        let mut packets = std::mem::replace(&mut broker.packets, mpsc::channel(1).1);
        let (received, mut version) = tokio::sync::watch::channel(0u64);
        let drain = tokio::spawn(async move {
            while let Some(bytes) = packets.recv().await {
                let outer = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
                let parts = outer.as_array().unwrap();
                let rmpv::Value::Ext(98, size) = &parts[0] else {
                    panic!("compressed cache frame")
                };
                let length = rmpv::decode::read_value(&mut size.as_slice())
                    .unwrap()
                    .as_u64()
                    .unwrap() as usize;
                let plain =
                    lz4_flex::block::decompress(parts[1].as_slice().unwrap(), length).unwrap();
                let value = rmpv::decode::read_value(&mut plain.as_slice()).unwrap();
                received.send_replace(value.as_array().unwrap()[2].as_u64().unwrap());
            }
        });
        let root = Arc::new(Owner::default());
        let parent = root.child().unwrap();
        let sibling = root.child().unwrap();
        let settings = BrokerSettings::new(
            broker.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap();
        let mut delivery =
            Delivery::start(settings, "development".into(), &parent, budget()).unwrap();
        let port = delivery.port();
        let mut status = delivery.changes();
        tokio::time::timeout(Duration::from_secs(2), async {
            while !status.borrow().connected {
                status.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
        let mut snapshot = Snapshot {
            schema: 2,
            id: [1; 16],
            scope: "fixture".into(),
            token: "scope-token".into(),
            horizon: "Daily".into(),
            generation: [2; 16],
            at: 0,
            until: 10_000_000,
            instruments: vec![],
            digest: String::new(),
            ranking: None,
        };
        snapshot.seal().unwrap();
        let batch = |version| {
            BTreeMap::from([(
                "fixture".into(),
                Arc::new(
                    Publication::new(&snapshot, 739915, "configuration", version, "fixture")
                        .unwrap(),
                ),
            )])
        };
        port.submit(batch(1)).unwrap();
        tokio::time::timeout(Duration::from_secs(2), async {
            while *version.borrow() < 1 {
                version.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
        for i in 2..=1001 {
            port.submit(batch(i)).unwrap();
        }
        if !stalled {
            tokio::time::timeout(Duration::from_secs(2), async {
                while *version.borrow() != 1001 {
                    version.changed().await.unwrap();
                }
            })
            .await
            .unwrap();
            tokio::time::timeout(Duration::from_secs(2), async {
                while !status.borrow().published {
                    status.changed().await.unwrap();
                }
            })
            .await
            .unwrap();
            assert!(status.borrow().error.is_none());
        } else {
            assert!(
                !status.borrow().published,
                "withheld PONG cannot establish cache readiness"
            );
        }
        let started = std::time::Instant::now();
        delivery.shutdown(budget()).await.unwrap();
        parent
            .wait_nested(&CancellationToken::new(), budget())
            .await
            .unwrap();
        assert!(started.elapsed() < Duration::from_secs(1));
        assert!(port.submit(batch(1002)).is_err());
        assert!(!sibling.cancellation().is_cancelled());
        broker.joined().await;
        drain.await.unwrap();
        drop(parent);
        drop(sibling);
        root.stop().unwrap();
        root.wait_idle(&CancellationToken::new(), budget())
            .await
            .unwrap();
        assert!(
            root.disposable().unwrap(),
            "retained copied ports cannot retain dataset ownership"
        );
    }
}

struct Broker {
    endpoint: String,
    packets: mpsc::Receiver<Vec<u8>>,
    exit: oneshot::Receiver<()>,
    authentication: oneshot::Receiver<serde_json::Value>,
    task: tokio::task::JoinHandle<()>,
}
impl Broker {
    async fn start(stall_flush: bool, stall_info: bool) -> Self {
        Self::start_mode(stall_flush, stall_info, None).await
    }
    async fn start_mode(stall_flush: bool, stall_info: bool, injected: Option<Vec<u8>>) -> Self {
        Self::start_full(stall_flush, stall_info, injected, false).await
    }
    async fn start_diagnostics(stall: bool) -> Self {
        Self::start_full(stall, false, None, true).await
    }
    async fn start_full(
        stall_flush: bool,
        stall_info: bool,
        injected: Option<Vec<u8>>,
        diagnostic_receipt: bool,
    ) -> Self {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("nats://{}", listener.local_addr().unwrap());
        let (packets, received) = mpsc::channel(16);
        let (closed, exit) = oneshot::channel();
        let (auth, authentication) = oneshot::channel();
        let task = tokio::spawn(async move {
            let (socket, _) = listener.accept().await.unwrap();
            let (read, mut write) = socket.into_split();
            let mut read = BufReader::new(read);
            if !stall_info {
                write.write_all(b"INFO {\"server_id\":\"fixture\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
            }
            let mut line = String::new();
            let mut published = false;
            let mut auth = Some(auth);
            let mut subscription = None;
            loop {
                line.clear();
                match read.read_line(&mut line).await {
                    Ok(0) | Err(_) => break,
                    Ok(_) => {}
                }
                if let Some(connect) = line.strip_prefix("CONNECT ") {
                    if let Some(auth) = auth.take() {
                        let _ = auth.send(serde_json::from_str(connect.trim()).unwrap());
                    }
                } else if line.starts_with("SUB ") {
                    let fields = line.split_whitespace().collect::<Vec<_>>();
                    subscription = Some(fields[2].to_owned());
                } else if line.starts_with("PING") {
                    if (!stall_flush || !published) && write.write_all(b"PONG\r\n").await.is_err() {
                        break;
                    }
                } else if line.starts_with("PUB ") {
                    let count = line
                        .split_whitespace()
                        .last()
                        .unwrap()
                        .parse::<usize>()
                        .unwrap();
                    let mut payload = vec![0; count + 2];
                    read.read_exact(&mut payload).await.unwrap();
                    payload.truncate(count);
                    published = true;
                    if diagnostic_receipt && !stall_flush {
                        use sha2::{Digest, Sha256};
                        let fields = line.split_whitespace().collect::<Vec<_>>();
                        assert_eq!(fields.len(), 4);
                        assert!(fields[1].contains(".diagnostics."));
                        let digest = format!("{:X}", Sha256::digest(&payload));
                        let reply = ifm_market_data_native::messaging::codec::encode(
                            &rmpv::Value::Array(vec![
                                1.into(),
                                digest.clone().into(),
                                digest.into(),
                                true.into(),
                                rmpv::Value::Nil,
                            ]),
                        )
                        .unwrap();
                        let header = format!(
                            "MSG {} {} {}\r\n",
                            fields[2],
                            subscription.as_ref().unwrap(),
                            reply.len()
                        );
                        if write.write_all(header.as_bytes()).await.is_err()
                            || write.write_all(&reply).await.is_err()
                            || write.write_all(b"\r\n").await.is_err()
                        {
                            break;
                        }
                    }
                    if let Some(bytes) = &injected
                        && write.write_all(bytes).await.is_err()
                    {
                        break;
                    }
                    if packets.send(payload).await.is_err() {
                        break;
                    }
                }
            }
            let _ = closed.send(());
        });
        Self {
            endpoint,
            packets: received,
            exit,
            authentication,
            task,
        }
    }
    async fn joined(self) {
        tokio::time::timeout(Duration::from_secs(2), self.exit)
            .await
            .unwrap()
            .unwrap();
        self.task.await.unwrap();
    }
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_broker_authentication_matches_existing_url_token_and_password_policy() {
    for (userinfo, user, password, token) in [
        (
            "fixture%20user:fixture%2Bpass",
            Some("fixture user"),
            Some("fixture+pass"),
            None,
        ),
        ("fixture%2Ftoken", None, None, Some("fixture/token")),
    ] {
        let mut broker = Broker::start(false, false).await;
        let endpoint = broker
            .endpoint
            .replacen("nats://", &format!("nats://{userinfo}@"), 1);
        let settings = BrokerSettings::new(endpoint, vec!["127.0.0.1:1".parse().unwrap()]).unwrap();
        let mut publisher =
            Publisher::start(settings, "development".into(), 2, 4096, budget()).unwrap();
        connected(&publisher).await;
        let auth = tokio::time::timeout(Duration::from_secs(2), &mut broker.authentication)
            .await
            .unwrap()
            .unwrap();
        assert_eq!(auth["user"].as_str(), user);
        assert_eq!(auth["pass"].as_str(), password);
        assert_eq!(auth["auth_token"].as_str(), token);
        publisher.shutdown(budget()).await.unwrap();
        broker.joined().await;
    }
}

async fn connected(publisher: &Publisher) {
    let mut changed = publisher.changes();
    tokio::time::timeout(Duration::from_secs(3), async {
        while !changed.borrow().connected {
            changed.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn root_cancellation_joins_stalled_broker_workers_after_control_handle_is_dropped() {
    use ifm_market_data_native::runtime::owner::Owner;
    use std::sync::Arc;
    let mut broker = Broker::start(true, false).await;
    let root = Arc::new(Owner::default());
    let parent = root.child().unwrap();
    let settings = BrokerSettings::new(
        broker.endpoint.clone(),
        vec!["127.0.0.1:1".parse().unwrap()],
    )
    .unwrap();
    let publisher =
        Publisher::start_owned(settings, "development".into(), 2, 4096, budget(), &parent).unwrap();
    connected(&publisher).await;
    publisher.try_publish(&frame(1)).unwrap();
    tokio::time::timeout(Duration::from_secs(2), broker.packets.recv())
        .await
        .unwrap()
        .unwrap();
    // Parent stop, not a managed callback or broker PONG, owns cleanup proof.
    root.stop().unwrap();
    drop(publisher);
    drop(parent);
    root.wait_idle(&tokio_util::sync::CancellationToken::new(), budget())
        .await
        .unwrap();
    assert!(root.disposable().unwrap());
    broker.joined().await;
}

#[tokio::test]
async fn rust_constructs_and_posts_existing_envelope_then_joins_actual_socket() {
    let mut broker = Broker::start(false, false).await;
    let mut publisher = Publisher::start(
        BrokerSettings::new(
            broker.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "development".into(),
        2,
        4096,
        budget(),
    )
    .unwrap();
    connected(&publisher).await;
    let event = frame(1);
    publisher.try_publish(&event).unwrap();
    let received = tokio::time::timeout(Duration::from_secs(2), broker.packets.recv())
        .await
        .unwrap()
        .unwrap();
    assert_eq!(received, event.encode().unwrap());
    let mut changes = publisher.changes();
    tokio::time::timeout(Duration::from_secs(2), async {
        while changes.borrow().flushed != 1 {
            changes.changed().await.unwrap();
        }
    })
    .await
    .unwrap();
    publisher.shutdown(budget()).await.unwrap();
    assert_eq!(publisher.retained_bytes(), 0);
    assert!(publisher.status().stopped);
    broker.joined().await;
}

#[tokio::test]
async fn stalled_broker_and_full_lane_do_not_hold_another_lane_or_cancel_join() {
    let mut blocked = Broker::start(true, false).await;
    let mut working = Broker::start(false, false).await;
    let mut a = Publisher::start(
        BrokerSettings::new(
            blocked.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "development".into(),
        1,
        4096,
        budget(),
    )
    .unwrap();
    let mut b = Publisher::start(
        BrokerSettings::new(
            working.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "development".into(),
        1,
        4096,
        budget(),
    )
    .unwrap();
    connected(&a).await;
    connected(&b).await;
    a.try_publish(&frame(1)).unwrap();
    blocked.packets.recv().await.unwrap();
    assert_eq!(
        a.status().flushed,
        0,
        "a local socket write cannot count as a broker PONG"
    );
    assert!(a.status().pending_since.is_some());
    a.try_publish(&frame(2)).unwrap();
    assert!(a.try_publish(&frame(3)).is_err());
    assert!(a.retained_bytes() <= 4096);
    b.try_publish(&frame(9)).unwrap();
    assert!(working.packets.recv().await.is_some());
    tokio::time::timeout(Duration::from_secs(1), a.shutdown(budget()))
        .await
        .unwrap()
        .unwrap();
    assert_eq!(a.retained_bytes(), 0);
    blocked.joined().await;
    b.shutdown(budget()).await.unwrap();
    working.joined().await;
}

#[tokio::test]
async fn cancelled_partial_handshake_releases_socket_without_a_server_response() {
    let broker = Broker::start(false, true).await;
    let mut publisher = Publisher::start(
        BrokerSettings::new(
            broker.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "development".into(),
        1,
        4096,
        budget(),
    )
    .unwrap();
    // No protocol progress is necessary for cancellation to release a partial
    // connection. The listener accepted socket must reach EOF after joined stop.
    tokio::time::sleep(Duration::from_millis(40)).await;
    publisher.shutdown(budget()).await.unwrap();
    broker.joined().await;
    assert!(publisher.status().stopped);
}

#[tokio::test]
async fn invalid_frames_and_byte_capacity_are_explicit_without_broker_dependencies() {
    let broker = Broker::start(false, true).await;
    let mut publisher = Publisher::start(
        BrokerSettings::new(
            broker.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "development".into(),
        1,
        frame(1).encode().unwrap().len() - 1,
        budget(),
    )
    .unwrap();
    assert!(publisher.try_publish(&frame(1)).is_err());
    let mut invalid = frame(0);
    invalid.value_date = -1;
    assert!(publisher.try_publish(&invalid).is_err());
    assert_eq!(publisher.retained_bytes(), 0);
    tokio::time::sleep(Duration::from_millis(40)).await;
    publisher.shutdown(budget()).await.unwrap();
    broker.joined().await;
}

#[tokio::test]
async fn rejected_or_malformed_broker_messages_fail_without_false_receipt_or_unbounded_allocation()
{
    let mut oversized = b"INFO ".to_vec();
    oversized.extend(vec![b'x'; 65537]);
    let cases = vec![
        b"-ERR 'Permissions Violation for Publish denied.subject private-token'\r\nPONG\r\n"
            .to_vec(),
        b"MSG x 1 18446744073709551615\r\n".to_vec(),
        b"HMSG x 1 4294967295 5\r\n".to_vec(),
        b"-ERR\r\n".to_vec(),
        b"MSG x 1 1\r\nZxx".to_vec(),
        oversized,
    ];
    for (index, reply) in cases.into_iter().enumerate() {
        let broker = Broker::start_mode(true, false, Some(reply)).await;
        let endpoint = broker
            .endpoint
            .replacen("nats://", "nats://private-token@", 1);
        let mut publisher = Publisher::start(
            BrokerSettings::new(endpoint, vec!["127.0.0.1:1".parse().unwrap()]).unwrap(),
            "development".into(),
            1,
            4096,
            budget(),
        )
        .unwrap();
        connected(&publisher).await;
        publisher.try_publish(&frame(1)).unwrap();
        let mut health = publisher.changes();
        tokio::time::timeout(Duration::from_secs(1), async {
            while health.borrow().failure.is_none() {
                health.changed().await.unwrap();
            }
        })
        .await
        .unwrap();
        assert_eq!(
            publisher.status().flushed,
            0,
            "case {index}: rejected/malformed data cannot qualify native recovery"
        );
        if index == 0 {
            let detail = publisher.status().detail.unwrap();
            assert!(detail.contains("Permissions Violation"), "{detail}");
            assert!(
                !detail.contains("private-token"),
                "credentials escaped status"
            );
        }
        publisher.shutdown(budget()).await.unwrap();
        assert_eq!(publisher.retained_bytes(), 0);
        broker.joined().await;
    }
}

#[tokio::test]
async fn one_native_writer_orders_concurrent_quotes_and_trade_evidence_and_ports_cannot_pin_shutdown()
 {
    use ifm_market_data_native::messaging::ordered::{Header, Ordered};
    use tokio_util::sync::CancellationToken;
    let mut broker = Broker::start(false, false).await;
    let mut publisher = Publisher::start(
        BrokerSettings::new(
            broker.endpoint.clone(),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap(),
        "development".into(),
        128,
        1024 * 1024,
        budget(),
    )
    .unwrap();
    let ordered = Ordered::new(
        publisher.port(),
        Header {
            lane: Lane::OptionsGlbx,
            value_date: 739915,
            worker: [1; 16],
            generation: [2; 16],
            revision: 1,
        },
    )
    .unwrap();
    let mut jobs = tokio::task::JoinSet::new();
    for n in 0..80u8 {
        let target = ordered.clone();
        jobs.spawn(async move {
            let kind = if n % 2 == 0 { 6 } else { 7 };
            let payload = vec![0x91, n];
            let sequence = target.submit(kind, payload.clone(), [2; 16], None).unwrap();
            (sequence, kind, payload)
        });
    }
    let mut expected = std::collections::BTreeMap::new();
    while let Some(job) = jobs.join_next().await {
        let (seq, kind, payload) = job.unwrap();
        expected.insert(seq, (kind, payload));
    }
    assert_eq!(expected.len(), 80);
    for seq in 1..=80 {
        let (kind, payload) = expected.remove(&seq).unwrap();
        let mut value = frame(seq as i64);
        value.kind = kind;
        value.payload = payload;
        let actual = tokio::time::timeout(Duration::from_secs(2), broker.packets.recv())
            .await
            .unwrap()
            .unwrap();
        assert_eq!(
            actual,
            value.encode().unwrap(),
            "one TCP stream must preserve global sequence {seq}"
        );
    }
    ordered
        .confirmed(80, &CancellationToken::new(), budget())
        .await
        .unwrap();
    assert!(
        ordered
            .confirmed(
                80,
                &CancellationToken::new(),
                Deadline::after(Duration::ZERO)
            )
            .await
            .is_err()
    );
    assert!(ordered.submit(7, vec![0xc0], [9; 16], None).is_err());
    let copied = ordered.clone();
    publisher.shutdown(budget()).await.unwrap();
    tokio::time::timeout(Duration::from_secs(1), broker.exit)
        .await
        .unwrap()
        .unwrap();
    assert!(copied.submit(7, vec![0xc0], [2; 16], None).is_err());
    broker.task.await.unwrap();
}
