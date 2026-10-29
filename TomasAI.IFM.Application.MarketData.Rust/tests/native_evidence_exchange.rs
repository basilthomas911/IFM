//! Real TCP evidence replies are distinct from protocol PONG and cannot pin the
//! native connection owner while an external database consumer is stalled.
use ifm_market_data_native::{
    messaging::{
        evidence_outbox::Outbox,
        evidence_protocol,
        option_evidence::{Evidence, Failure, Outcome},
        option_source::Source,
        publisher::{BrokerSettings, Publisher},
    },
    reference::decimal,
    runtime::{Deadline, owner::Owner},
};
use std::{sync::Arc, time::Duration};
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::TcpListener,
    sync::oneshot,
};
fn budget() -> Deadline {
    Deadline::after(Duration::from_secs(5))
}
fn evidence() -> Evidence {
    Evidence {
        source: Source {
            dataset: "GLBX.MDP3".into(),
            publisher: 3,
            instrument: 42,
            contract: "ES-option".into(),
            date: time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap(),
            price: decimal::parse("10.25").unwrap(),
            size: 1,
            sequence: 7,
            event: 1_793_102_400_000_000_000,
            received: 1_793_102_400_000_000_100,
            generation: [1; 16],
            symbol: "fixture".into(),
        },
        context: None,
        underlying: None,
        at: 17_931_024_000_000_001,
        outcome: Outcome::Failure(Failure {
            code: "PricingContextUnavailable".into(),
            input: "OptionPricing".into(),
            contract: "ES-option".into(),
            detail: "Qualified calculation is unavailable.".into(),
            retryable: false,
        }),
    }
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn durable_receipt_is_separate_and_silent_writer_cannot_block_join() {
    for mode in 0..5 {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("nats://{}", listener.local_addr().unwrap());
        let (submitted, observed) = oneshot::channel();
        let server = tokio::spawn(async move {
            let (socket, _) = listener.accept().await.unwrap();
            let (read, mut write) = socket.into_split();
            let mut reader = BufReader::new(read);
            write.write_all(b"INFO {\"server_id\":\"evidence\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
            let mut line = String::new();
            let mut subscription = None;
            let mut submitted = Some(submitted);
            loop {
                line.clear();
                match reader.read_line(&mut line).await {
                    Ok(0) | Err(_) => break,
                    Ok(_) => {}
                }
                let fields: Vec<_> = line.split_whitespace().collect();
                if fields.first() == Some(&"PING") {
                    if write.write_all(b"PONG\r\n").await.is_err() {
                        break;
                    }
                } else if fields.first() == Some(&"SUB") {
                    assert_eq!(fields.len(), 3);
                    subscription = Some((fields[1].to_owned(), fields[2].to_owned()));
                } else if fields.first() == Some(&"PUB") {
                    assert_eq!(fields.len(), 4);
                    assert_eq!(fields[1], "ifm.test.marketdata.v1.evidence.write");
                    let size: usize = fields[3].parse().unwrap();
                    assert!(size < 132096);
                    let mut bytes = vec![0; size + 2];
                    reader.read_exact(&mut bytes).await.unwrap();
                    assert_eq!(&bytes[size..], b"\r\n");
                    bytes.truncate(size);
                    let value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
                    let value = value.as_array().unwrap();
                    assert_eq!(value.len(), 4);
                    let (inbox, sid) = subscription.as_ref().unwrap();
                    assert_eq!(fields[2], inbox);
                    if mode == 4 {
                        // Rejected from its declared size without waiting for or
                        // allocating the body, before the subscription can queue it.
                        write
                            .write_all(format!("MSG {inbox} {sid} 8193\r\n").as_bytes())
                            .await
                            .unwrap();
                    } else if mode == 3 {
                        let header = b"NATS/1.0 503 No Responders\r\n\r\n";
                        write
                            .write_all(
                                format!("HMSG {inbox} {sid} {} {}\r\n", header.len(), header.len())
                                    .as_bytes(),
                            )
                            .await
                            .unwrap();
                        write.write_all(header).await.unwrap();
                        write.write_all(b"\r\n").await.unwrap();
                    } else if mode != 1 {
                        let receipt = if mode == 0 {
                            rmpv::Value::Array(vec![
                                value[0].clone(),
                                value[1].clone(),
                                value[2].clone(),
                                true.into(),
                                rmpv::Value::Nil,
                            ])
                        } else {
                            rmpv::Value::Array(vec![
                                value[0].clone(),
                                value[1].clone(),
                                value[2].clone(),
                                false.into(),
                                "Durable option evidence persistence failed.".into(),
                            ])
                        };
                        let receipt =
                            ifm_market_data_native::messaging::codec::encode(&receipt).unwrap();
                        write
                            .write_all(
                                format!("MSG {inbox} {sid} {}\r\n", receipt.len()).as_bytes(),
                            )
                            .await
                            .unwrap();
                        write.write_all(&receipt).await.unwrap();
                        write.write_all(b"\r\n").await.unwrap();
                    }
                    if let Some(sent) = submitted.take() {
                        let _ = sent.send(());
                    }
                }
            }
        });
        let owner = Arc::new(Owner::default());
        let parent = Arc::new(owner.child().unwrap());
        let settings = BrokerSettings::new(endpoint, vec!["127.0.0.1:1".parse().unwrap()]).unwrap();
        let (mut publisher, mut replies) = Publisher::start_evidence_exchange(
            settings,
            "test".into(),
            2,
            1024 * 1024,
            budget(),
            &parent,
        )
        .unwrap();
        let mut progress = publisher.changes();
        while !progress.borrow().connected {
            tokio::time::timeout(Duration::from_secs(5), progress.changed())
                .await
                .unwrap()
                .unwrap();
        }
        let outbox = Outbox::new(2, 1024 * 1024).unwrap();
        let retained = outbox.retain(&evidence()).unwrap();
        publisher.try_evidence(&retained).unwrap();
        tokio::time::timeout(Duration::from_secs(5), observed)
            .await
            .unwrap()
            .unwrap();
        while progress.borrow().flushed != 1 && progress.borrow().failure.is_none() {
            tokio::time::timeout(Duration::from_secs(5), progress.changed())
                .await
                .unwrap()
                .unwrap();
        }
        assert!(
            outbox.capture(true, 1).unwrap().is_empty(),
            "PONG cannot acknowledge the durable writer"
        );
        if mode == 3 || mode == 4 {
            while progress.borrow().failure.is_none() {
                tokio::time::timeout(Duration::from_secs(5), progress.changed())
                    .await
                    .unwrap()
                    .unwrap();
            }
            let detail = progress.borrow().detail.clone().unwrap();
            if mode == 3 {
                assert!(detail.contains("503"));
                assert!(detail.contains("No Responders"));
            } else {
                assert!(detail.contains("native limit"), "{detail}");
                assert_eq!(progress.borrow().flushed, 0);
            }
            assert!(outbox.capture(true, 1).unwrap().is_empty());
        } else if mode != 1 {
            let response = tokio::time::timeout(Duration::from_secs(5), replies.recv())
                .await
                .unwrap()
                .unwrap();
            let result = evidence_protocol::acknowledge(&response, &outbox);
            assert_eq!(result.is_ok(), mode == 0);
            assert_eq!(
                outbox.capture(true, 1).unwrap().len(),
                usize::from(mode == 0)
            );
        } else {
            assert!(replies.try_recv().is_err());
        }
        // Even the silent writer case cancels the subscription, connection and
        // event worker without waiting for a receipt or broker drain.
        publisher.shutdown(budget()).await.unwrap();
        parent
            .wait_nested(&tokio_util::sync::CancellationToken::new(), budget())
            .await
            .unwrap();
        tokio::time::timeout(Duration::from_secs(2), server)
            .await
            .unwrap()
            .unwrap();
        assert_eq!(publisher.retained_bytes(), 0);
        drop(parent);
        drop(owner);
        assert_eq!(retained.bytes.len(), outbox.retained_bytes().unwrap());
    }
}

async fn spool_integration(production: bool) {
    use ifm_market_data_native::messaging::evidence_delivery::{Configuration, Delivery};
    for mode in if production { 2..3 } else { 0..4 } {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("nats://{}", listener.local_addr().unwrap());
        let (published, observed) = oneshot::channel();
        let server = tokio::spawn(async move {
            let (socket, _) = listener.accept().await.unwrap();
            let (read, mut write) = socket.into_split();
            let mut reader = BufReader::new(read);
            write.write_all(b"INFO {\"server_id\":\"evidence\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
            let mut line = String::new();
            let mut subscription = None;
            let mut published = Some(published);
            let mut acknowledged = false;
            loop {
                line.clear();
                match reader.read_line(&mut line).await {
                    Ok(0) | Err(_) => break,
                    Ok(_) => {}
                }
                let fields: Vec<_> = line.split_whitespace().collect();
                if fields.first() == Some(&"PING") {
                    if write.write_all(b"PONG\r\n").await.is_err() {
                        break;
                    }
                } else if fields.first() == Some(&"SUB") {
                    subscription = Some((fields[1].to_owned(), fields[2].to_owned()));
                } else if fields.first() == Some(&"PUB") {
                    let size: usize = fields.last().unwrap().parse().unwrap();
                    assert!(size < 132096);
                    let mut bytes = vec![0; size + 2];
                    reader.read_exact(&mut bytes).await.unwrap();
                    bytes.truncate(size);
                    if fields.len() == 4 {
                        if mode == 1
                            && let Some(published) = published.take()
                        {
                            let _ = published.send(bytes.clone());
                        }

                        assert_eq!(fields[1], "ifm.test.marketdata.v1.evidence.write");
                        let request = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
                        let request = request.as_array().unwrap();
                        if mode == 3 && request[0].as_u64() == Some(2) {
                            tokio::time::sleep(Duration::from_millis(250)).await;
                        }
                        if mode != 1 {
                            let response = rmpv::Value::Array(vec![
                                request[0].clone(),
                                request[1].clone(),
                                request[2].clone(),
                                (mode == 0 || mode == 3).into(),
                                if mode == 0 || mode == 3 {
                                    rmpv::Value::Nil
                                } else {
                                    "Durable option evidence persistence failed.".into()
                                },
                            ]);
                            let response =
                                ifm_market_data_native::messaging::codec::encode(&response)
                                    .unwrap();
                            let (inbox, sid) = subscription.as_ref().unwrap();
                            write
                                .write_all(
                                    format!("MSG {inbox} {sid} {}\r\n", response.len()).as_bytes(),
                                )
                                .await
                                .unwrap();
                            write.write_all(&response).await.unwrap();
                            write.write_all(b"\r\n").await.unwrap();
                            acknowledged = mode == 0 || mode == 3;
                        }
                    } else {
                        assert!(
                            acknowledged,
                            "Actor publication preceded durable persistence"
                        );
                        assert_eq!(fields.len(), 3);
                        assert!(
                            fields[1].starts_with("ifm.test.marketdata.v1.publications.Options.")
                        );
                        if let Some(published) = published.take() {
                            let _ = published.send(bytes);
                        }
                    }
                }
            }
        });
        let owner = Arc::new(Owner::default());
        let parent = Arc::new(owner.child().unwrap());
        let outbox = Arc::new(Outbox::new(2, 1024 * 1024).unwrap());
        let retained = outbox.retain(&evidence()).unwrap();
        if mode == 3 {
            let mut next = evidence();
            next.source.sequence = 8;
            outbox.retain(&next).unwrap();
        }

        let mut changes = outbox.changes();
        let config = Configuration {
            broker: BrokerSettings::new(endpoint, vec!["127.0.0.1:1".parse().unwrap()]).unwrap(),
            scope: "test".into(),
            worker: [3; 16],
            revision: 1,
            actor: None,
        };
        let started = tokio::time::Instant::now();
        let deadline = if mode == 2 {
            if production {
                Deadline::recovery()
            } else {
                Deadline::after(Duration::from_millis(200))
            }
        } else if mode == 3 {
            Deadline::after(Duration::from_millis(100))
        } else {
            budget()
        };
        let mut delivery =
            Delivery::start(outbox.clone(), config, parent.clone(), deadline).unwrap();
        let mut status = delivery.changes();
        if mode == 0 || mode == 3 {
            let bytes = tokio::time::timeout(Duration::from_secs(5), observed)
                .await
                .unwrap()
                .unwrap();
            let value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
            let value = value.as_array().unwrap();
            let rmpv::Value::Ext(98, ref size) = value[0] else {
                panic!("expected existing compressed publication envelope")
            };
            let size = rmpv::decode::read_value(&mut size.as_slice())
                .unwrap()
                .as_u64()
                .unwrap() as usize;
            let content = lz4_flex::block::decompress(value[1].as_slice().unwrap(), size).unwrap();
            let frame = rmpv::decode::read_value(&mut content.as_slice()).unwrap();
            let frame = frame.as_array().unwrap();
            assert_eq!(frame[6].as_i64(), Some(6));
            assert_eq!(frame[7].as_slice().unwrap(), retained.bytes.as_ref());
            while outbox.retained_bytes().unwrap() != 0 {
                tokio::time::timeout(Duration::from_secs(5), changes.changed())
                    .await
                    .unwrap()
                    .unwrap();
            }
            assert_eq!(retained.bytes.as_ref(), evidence().encode().unwrap());
        } else if mode == 2 {
            while !status.borrow().terminal {
                tokio::time::timeout(
                    if production {
                        Duration::from_secs(122)
                    } else {
                        Duration::from_secs(2)
                    },
                    status.changed(),
                )
                .await
                .unwrap()
                .unwrap();
            }
            assert!(
                started.elapsed()
                    < if production {
                        Duration::from_secs(122)
                    } else {
                        Duration::from_secs(1)
                    }
            );
            assert!(status.borrow().deadline.unwrap().remaining().is_zero());
            assert!(!status.borrow().running);
            assert!(outbox.retained_bytes().unwrap() > 0);
        } else {
            let request = tokio::time::timeout(Duration::from_secs(5), observed)
                .await
                .unwrap()
                .unwrap();
            assert!(
                !request.is_empty(),
                "A real write request must be stalled before cancellation is tested"
            );
            while !status.borrow().running {
                // There is deliberately no durable receipt.
                // Startup can be connected while a write is pending; cancel from
                // outside regardless of the source/data plane's progress.
                if status.borrow().terminal {
                    break;
                }
                tokio::select! {result=status.changed()=>{result.unwrap();},()=tokio::time::sleep(Duration::from_millis(50))=>break}
            }
            assert!(outbox.retained_bytes().unwrap() > 0);
        }
        delivery.shutdown(budget()).await.unwrap();
        parent
            .wait_nested(&tokio_util::sync::CancellationToken::new(), budget())
            .await
            .unwrap();
        tokio::time::timeout(Duration::from_secs(2), server)
            .await
            .unwrap()
            .unwrap();
        drop(parent);
        drop(owner);
    }
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
async fn native_spool_publishes_only_after_durable_ack_and_bounds_failure_and_join() {
    spool_integration(false).await;
}

#[tokio::test(flavor = "multi_thread", worker_threads = 2)]
#[ignore = "Production-duration original 120-second persistence recovery qualification"]
async fn native_spool_storage_failure_expires_original_120_seconds_without_losing_copied_evidence()
{
    spool_integration(true).await;
}
