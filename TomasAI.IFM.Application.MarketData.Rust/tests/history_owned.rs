//! Owned HTTP/DNS/TLS cancellation using localhost servers and fabricated keys.
use databento::{
    dbn::{
        MetadataBuilder, SType, Schema, TradeMsg,
        encode::dbn::{AsyncMetadataEncoder, AsyncRecordEncoder},
    },
    historical::{metadata::GetCostParams, timeseries::GetRangeParams},
};
use ifm_market_data_native::{
    dns::Resolver,
    historical::{Failure, History},
    runtime::{Deadline, Interrupted},
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
fn params() -> GetRangeParams {
    let start = time::OffsetDateTime::from_unix_timestamp(0).unwrap();
    GetRangeParams::builder()
        .dataset("GLBX.MDP3")
        .schema(Schema::Trades)
        .symbols(vec!["ESZ6"])
        .date_time_range(start..start + time::Duration::seconds(1))
        .build()
}
fn cost_params() -> GetCostParams {
    let start = time::OffsetDateTime::from_unix_timestamp(0).unwrap();
    GetCostParams::builder()
        .dataset("GLBX.MDP3")
        .schema(Schema::Trades)
        .symbols(vec!["ESZ6"])
        .date_time_range(start..start + time::Duration::seconds(1))
        .build()
}
async fn dbn() -> Vec<u8> {
    dbn_records(1).await
}
async fn dbn_records(count: u32) -> Vec<u8> {
    let m = MetadataBuilder::new()
        .dataset("GLBX.MDP3")
        .schema(Some(Schema::Trades))
        .stype_in(None)
        .stype_out(SType::InstrumentId)
        .start(0)
        .build();
    let mut out = Vec::new();
    AsyncMetadataEncoder::new(&mut out)
        .encode(&m)
        .await
        .unwrap();
    let mut encoder = AsyncRecordEncoder::new(&mut out);
    for sequence in 0..count {
        encoder
            .encode(&TradeMsg {
                price: 42,
                size: 7,
                sequence,
                ..Default::default()
            })
            .await
            .unwrap();
    }
    zstd::stream::encode_all(out.as_slice(), 1).unwrap()
}
async fn server(
    status: u16,
    body: Vec<u8>,
    declared: Option<u64>,
    extra: &str,
) -> (String, tokio::task::JoinHandle<()>) {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let url = format!("http://{}/", listener.local_addr().unwrap());
    let extra = extra.to_owned();
    let task = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        let mut header = Vec::new();
        let mut byte = [0];
        while !header.ends_with(b"\r\n\r\n") {
            socket.read_exact(&mut byte).await.unwrap();
            header.push(byte[0]);
        }
        let header = String::from_utf8(header).unwrap();
        let length = header
            .lines()
            .find_map(|s| {
                s.to_ascii_lowercase()
                    .strip_prefix("content-length:")
                    .map(|v| v.trim().parse::<usize>().unwrap())
            })
            .unwrap_or(0);
        let mut request = vec![0; length];
        socket.read_exact(&mut request).await.unwrap();
        socket.write_all(format!("HTTP/1.1 {status} Result\r\nContent-Length: {}\r\n{extra}Connection: keep-alive\r\n\r\n",declared.unwrap_or(body.len() as u64)).as_bytes()).await.unwrap();
        if let Err(error) = socket.write_all(&body).await {
            assert!(matches!(
                error.kind(),
                std::io::ErrorKind::BrokenPipe
                    | std::io::ErrorKind::ConnectionReset
                    | std::io::ErrorKind::ConnectionAborted
            ));
            return;
        }
        let closed = tokio::time::timeout(Duration::from_secs(2), socket.read(&mut byte))
            .await
            .unwrap();
        assert!(
            matches!(closed, Ok(0) | Err(_)),
            "HTTP connection not retired"
        );
    });
    (url, task)
}
#[tokio::test]
async fn owned_range_completes_or_cancels_without_idle_connection_leaks() {
    for mode in 0..3 {
        let body = dbn().await;
        let declared = if mode == 1 {
            Some(body.len() as u64 + 1024)
        } else {
            None
        };
        let (url, server) = server(200, body, declared, "").await;
        let history = History::new(&url, "x".repeat(32), resolver()).unwrap();
        let mut seen = 0;
        let result = history
            .for_each(
                params(),
                &CancellationToken::new(),
                Deadline::after(Duration::from_millis(120)),
                |record| {
                    assert_eq!(record.get::<TradeMsg>().unwrap().price, 42);
                    seen += 1;
                    if mode == 2 {
                        Err("injected processing failure".into())
                    } else {
                        Ok(())
                    }
                },
            )
            .await;
        match mode {
            0 => {
                assert_eq!(result.unwrap().records, 1);
                assert_eq!(seen, 1);
            }
            1 => assert!(matches!(
                result,
                Err(Failure::Interrupted(Interrupted::DeadlineExpired))
            )),
            _ => assert!(matches!(result, Err(Failure::Processing(_)))),
        }
        server.await.unwrap();
    }
}
#[tokio::test]
async fn estimates_bound_success_and_error_bodies_redact_keys_and_never_follow_redirects() {
    for (status, body, length, extra) in [
        (200, b"1.25".to_vec(), None, ""),
        (
            400,
            format!("{{\"detail\":\"bad {}\"}}", "x".repeat(32)).into_bytes(),
            None,
            "",
        ),
        (200, Vec::new(), Some(9 * 1024 * 1024), ""),
        (400, Vec::new(), Some(u64::MAX), ""),
        (
            302,
            Vec::new(),
            None,
            "Location: https://must-not-resolve.invalid/\r\n",
        ),
    ] {
        let (url, server) = server(status, body, length, extra).await;
        let dns = resolver();
        let history = History::new(&url, "x".repeat(32), dns.clone()).unwrap();
        let result = history
            .cost(
                cost_params(),
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(1)),
            )
            .await;
        if status == 200 && length.is_none() {
            assert_eq!(result.unwrap(), 1.25);
        } else {
            match result {
                Err(Failure::Provider(message)) => assert!(!message.contains(&"x".repeat(32))),
                other => panic!("unexpected result {other:?}"),
            }
        }
        assert_eq!(dns.active_exchanges(), 0);
        server.await.unwrap();
    }
}
#[tokio::test]
async fn actual_tls_handshake_stall_is_cancellable_and_closes_the_socket() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let url = format!("https://{}/", listener.local_addr().unwrap());
    let server = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        let mut bytes = [0; 65536];
        let mut received = 0;
        loop {
            let n = tokio::time::timeout(Duration::from_secs(2), socket.read(&mut bytes))
                .await
                .unwrap()
                .unwrap();
            if n == 0 {
                break;
            }
            received += n;
        }
        assert!(received > 0, "no TLS ClientHello received");
    });
    let history = History::new(&url, "x".repeat(32), resolver()).unwrap();
    assert!(matches!(
        history
            .cost(
                cost_params(),
                &CancellationToken::new(),
                Deadline::after(Duration::from_millis(100))
            )
            .await,
        Err(Failure::Interrupted(Interrupted::DeadlineExpired))
    ));
    server.await.unwrap();
}
#[tokio::test]
async fn historical_dns_stall_ends_all_resolver_sockets_at_original_deadline() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let dns = Arc::new(Resolver::new(vec![address]).unwrap());
    let server = tokio::spawn(async move {
        let mut tasks = tokio::task::JoinSet::new();
        for _ in 0..2 {
            let (mut socket, _) = listener.accept().await.unwrap();
            tasks.spawn(async move {
                let mut buffer = [0; 4096];
                loop {
                    if socket.read(&mut buffer).await.unwrap() == 0 {
                        break;
                    }
                }
            });
        }
        while let Some(result) = tasks.join_next().await {
            result.unwrap();
        }
    });
    let history = History::new("https://history.invalid/", "x".repeat(32), dns.clone()).unwrap();
    assert!(matches!(
        history
            .cost(
                cost_params(),
                &CancellationToken::new(),
                Deadline::after(Duration::from_millis(100))
            )
            .await,
        Err(Failure::Interrupted(Interrupted::DeadlineExpired))
    ));
    assert_eq!(dns.active_exchanges(), 0);
    tokio::time::timeout(Duration::from_secs(1), server)
        .await
        .unwrap()
        .unwrap();
}

#[tokio::test]
async fn warnings_are_preserved_redacted_and_removed_only_when_drained() {
    let extra = format!(
        "X-Warning: [\"first\",\"{}\"]\r\nX-Warning: malformed warning JSON\r\nX-Request-Id: request-42\r\n",
        "x".repeat(32)
    );
    let (url, peer) = server(200, b"1.25".to_vec(), None, &extra).await;
    let owner = Arc::new(ifm_market_data_native::runtime::owner::Owner::default());
    let history = History::new(&url, "x".repeat(32), resolver())
        .unwrap()
        .with_notifications(owner.clone());
    assert_eq!(
        history
            .cost(
                cost_params(),
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(1))
            )
            .await
            .unwrap(),
        1.25
    );
    assert_eq!(owner.take_signals().unwrap(), 1 << 4);
    assert!(
        history
            .consume_warning::<()>(|_| Err(Failure::Input("small output")))
            .is_err()
    );
    let first = history
        .consume_warning(|warning| Ok(warning.clone()))
        .unwrap()
        .unwrap();
    let mut warnings = history.drain_warnings().unwrap();
    warnings.insert(0, first);
    assert_eq!(warnings.len(), 2);
    assert_eq!(warnings[0].request_id, "request-42");
    assert_eq!(warnings[0].status, 200);
    assert_eq!(warnings[0].endpoint, "metadata.get_cost");
    assert_eq!(warnings[0].text, "[\"first\",\"[REDACTED]\"]");
    assert_eq!(warnings[1].text, "malformed warning JSON");
    assert!(history.drain_warnings().unwrap().is_empty());
    peer.await.unwrap();
}

#[tokio::test]
async fn definition_coverage_request_and_raw_lines_preserve_provider_digest_inputs() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let url = format!("http://{}/", listener.local_addr().unwrap());
    let source = "  {\"hd\":{\"instrument_id\":42},\"unknown\":\"☃\"}  ";
    let expected = source.to_owned();
    let peer = tokio::spawn(async move {
        for index in 0..2 {
            let (mut socket, _) = listener.accept().await.unwrap();
            let mut header = Vec::new();
            let mut byte = [0];
            while !header.ends_with(b"\r\n\r\n") {
                socket.read_exact(&mut byte).await.unwrap();
                header.push(byte[0]);
            }
            let header = String::from_utf8(header).unwrap();
            let length = header
                .lines()
                .find_map(|v| {
                    v.to_ascii_lowercase()
                        .strip_prefix("content-length:")
                        .map(|s| s.trim().parse::<usize>().unwrap())
                })
                .unwrap_or(0);
            let mut form = vec![0; length];
            socket.read_exact(&mut form).await.unwrap();
            let response = if index == 0 {
                assert!(
                    header.starts_with("GET /v0/metadata.get_dataset_range?dataset=GLBX.MDP3 ")
                );
                r#"{"start":"1990-01-01T00:00:00Z","end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-11-02T23:59:59.000000000Z"}}}"#.to_owned()
            } else {
                assert!(header.starts_with("POST /v0/timeseries.get_range "));
                let form: std::collections::BTreeMap<String, String> =
                    serde_urlencoded::from_bytes(&form).unwrap();
                assert_eq!(form["start"], "2026-11-01");
                assert_eq!(form["end"], "2026-11-02T23:59:59.000000000Z");
                for (name, value) in [
                    ("symbols", "ES.FUT,ES.OPT"),
                    ("stype_in", "parent"),
                    ("encoding", "json"),
                    ("compression", "none"),
                    ("pretty_px", "false"),
                    ("pretty_ts", "false"),
                    ("map_symbols", "false"),
                ] {
                    assert_eq!(form[name], value);
                }
                format!("\r\n{expected}\r\n\n{expected}")
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
            // Fragment inside UTF-8 to prove decoding occurs after full line assembly.
            for chunk in response.as_bytes().chunks(7) {
                socket.write_all(chunk).await.unwrap();
            }
        }
    });
    let history = History::new(&url, "x".repeat(32), resolver()).unwrap();
    let mut lines = Vec::new();
    let count = history
        .latest_definitions(
            "GLBX.MDP3",
            &["ES.FUT".into(), "ES.OPT".into()],
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
            |s| {
                lines.push(s.to_owned());
                Ok(())
            },
        )
        .await
        .unwrap();
    assert_eq!(count, 2);
    assert_eq!(lines, vec![source, source]);
    peer.await.unwrap();
}

#[tokio::test]
async fn definition_lines_enforce_exact_65k_boundaries_cr_lf_eof_and_invalid_utf8() {
    let mut cases = Vec::new();
    for length in [65535usize, 65536, 65537] {
        for separator in ["\n", "\r", "\r\n", ""] {
            let mut bytes = format!("{{\"x\":\"{}\"}}", "a".repeat(length - 8)).into_bytes();
            assert_eq!(bytes.len(), length);
            bytes.extend_from_slice(separator.as_bytes());
            cases.push((bytes, length <= 65536, 1));
        }
    }
    cases.push((vec![0xff, b'\n'], false, 0));
    cases.push((vec![b' '; 65536], true, 0));
    cases.push((vec![b' '; 65537], false, 0));
    for size in [8usize, 65536] {
        let mut bytes = vec![0xef, 0xbb, 0xbf];
        bytes.extend_from_slice(format!("{{\"x\":\"{}\"}}", "a".repeat(size - 8)).as_bytes());
        cases.push((bytes, true, 1));
    }
    for (body, valid, expected_count) in cases {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let url = format!("http://{}/", listener.local_addr().unwrap());
        let peer = tokio::spawn(async move {
            for phase in 0..2 {
                let (mut socket, _) = listener.accept().await.unwrap();
                let mut header = Vec::new();
                let mut byte = [0];
                while !header.ends_with(b"\r\n\r\n") {
                    socket.read_exact(&mut byte).await.unwrap();
                    header.push(byte[0]);
                }
                let text = String::from_utf8(header).unwrap();
                let count = text
                    .lines()
                    .find_map(|line| {
                        line.to_ascii_lowercase()
                            .strip_prefix("content-length:")
                            .map(|s| s.trim().parse::<usize>().unwrap())
                    })
                    .unwrap_or(0);
                let mut request = vec![0; count];
                socket.read_exact(&mut request).await.unwrap();
                let coverage = br#"{"start":"1990-01-01T00:00:00Z","end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-10-25T00:00:00Z"}}}"#;
                let payload = if phase == 0 { &coverage[..] } else { &body[..] };
                socket
                    .write_all(
                        format!(
                            "HTTP/1.1 200 OK\r\nContent-Length: {}\r\n\r\n",
                            payload.len()
                        )
                        .as_bytes(),
                    )
                    .await
                    .unwrap();
                let _ = socket.write_all(payload).await;
                let mut byte = [0];
                assert!(matches!(socket.read(&mut byte).await, Ok(0) | Err(_)));
            }
        });
        let history = History::new(&url, "x".repeat(32), resolver()).unwrap();
        let mut observed = 0;
        let result = history
            .latest_definitions(
                "GLBX.MDP3",
                &["ES.FUT".into()],
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2)),
                |line| {
                    serde_json::from_str::<serde_json::Value>(line).unwrap();
                    observed += 1;
                    Ok(())
                },
            )
            .await;
        if valid {
            assert_eq!(result.unwrap(), expected_count);
            assert_eq!(observed, expected_count);
        } else {
            assert!(result.is_err());
        }
        peer.await.unwrap();
    }
}

#[tokio::test]
async fn concurrent_history_calls_bound_socket_and_retained_body_owners_and_cancel_queued_work() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let base = format!("http://{}/", listener.local_addr().unwrap());
    let token = CancellationToken::new();
    let history = Arc::new(History::new(&base, "x".repeat(32), resolver()).unwrap());
    let (ready_tx, ready_rx) = tokio::sync::oneshot::channel();
    let server = tokio::spawn(async move {
        let mut peers = Vec::new();
        for _ in 0..4 {
            let (mut socket, _) = listener.accept().await.unwrap();
            let mut header = Vec::new();
            let mut byte = [0];
            while !header.ends_with(b"\r\n\r\n") {
                socket.read_exact(&mut byte).await.unwrap();
                header.push(byte[0]);
            }
            let text = String::from_utf8(header).unwrap();
            let length = text
                .lines()
                .find_map(|line| {
                    line.to_ascii_lowercase()
                        .strip_prefix("content-length:")
                        .map(|s| s.trim().parse::<usize>().unwrap())
                })
                .unwrap_or(0);
            let mut form = vec![0; length];
            socket.read_exact(&mut form).await.unwrap();
            peers.push(socket);
        }
        ready_tx.send(()).unwrap();
        for mut peer in peers {
            let mut byte = [0];
            assert!(matches!(peer.read(&mut byte).await, Ok(0) | Err(_)));
        }
    });
    let mut calls = tokio::task::JoinSet::new();
    for _ in 0..8 {
        let client = history.clone();
        let token = token.clone();
        calls.spawn(async move {
            client
                .cost(
                    cost_params(),
                    &token,
                    Deadline::after(Duration::from_secs(3)),
                )
                .await
        });
    }
    tokio::time::timeout(Duration::from_secs(2), ready_rx)
        .await
        .unwrap()
        .unwrap();
    assert_eq!(history.active_requests(), 4);
    token.cancel();
    while let Some(result) = calls.join_next().await {
        assert!(matches!(
            result.unwrap(),
            Err(Failure::Interrupted(Interrupted::Cancelled))
        ))
    }
    server.await.unwrap();
    assert_eq!(history.active_requests(), 0);
}

#[tokio::test]
async fn async_history_backpressure_retains_one_record_without_drops_or_record_vec() {
    let (url, peer) = server(200, dbn_records(10_000).await, None, "").await;
    let history = Arc::new(History::new(&url, "x".repeat(32), resolver()).unwrap());
    let (sender, mut receiver) = tokio::sync::mpsc::channel(1);
    let produced = Arc::new(std::sync::atomic::AtomicUsize::new(0));
    let observed = produced.clone();
    let owner = history.clone();
    let producer = tokio::spawn(async move {
        owner
            .for_each_async(
                params(),
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(5)),
                move |record| {
                    let trade = record.get::<TradeMsg>().unwrap();
                    let copy = (trade.sequence, trade.price);
                    let sender = sender.clone();
                    let produced = produced.clone();
                    async move {
                        sender
                            .send(copy)
                            .await
                            .map_err(|_| "historical consumer closed".to_owned())?;
                        produced.fetch_add(1, std::sync::atomic::Ordering::SeqCst);
                        Ok(())
                    }
                },
            )
            .await
    });
    tokio::time::timeout(Duration::from_secs(1), async {
        while observed.load(std::sync::atomic::Ordering::SeqCst) == 0 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    tokio::time::sleep(Duration::from_millis(20)).await;
    assert_eq!(
        observed.load(std::sync::atomic::Ordering::SeqCst),
        1,
        "a stalled consumer must backpressure rather than grow/drop"
    );
    for sequence in 0..10_000 {
        assert_eq!(receiver.recv().await, Some((sequence, 42)));
    }
    assert_eq!(producer.await.unwrap().unwrap().records, 10_000);
    assert_eq!(receiver.recv().await, None);
    assert_eq!(history.active_requests(), 0);
    peer.await.unwrap();
}
#[tokio::test]
async fn full_history_consumer_does_not_prevent_cancel_or_original_deadline_cleanup() {
    for cancel in [false, true] {
        let (url, peer) = server(200, dbn_records(1000).await, None, "").await;
        let history = History::new(&url, "x".repeat(32), resolver()).unwrap();
        let token = CancellationToken::new();
        let signal = token.clone();
        let timer = tokio::spawn(async move {
            if cancel {
                tokio::time::sleep(Duration::from_millis(20)).await;
                signal.cancel();
            }
        });
        let (sender, mut receiver) = tokio::sync::mpsc::channel(1);
        let result = history
            .for_each_async(
                params(),
                &token,
                Deadline::after(Duration::from_millis(80)),
                move |record| {
                    let price = record.get::<TradeMsg>().unwrap().price;
                    let sender = sender.clone();
                    async move {
                        sender
                            .send(price)
                            .await
                            .map_err(|_| "closed historical consumer".to_owned())
                    }
                },
            )
            .await;
        assert!(
            matches!(result,Err(Failure::Interrupted(why))if why==if cancel{Interrupted::Cancelled}else{Interrupted::DeadlineExpired})
        );
        assert_eq!(receiver.recv().await, Some(42));
        assert_eq!(
            receiver.recv().await,
            None,
            "canceled producer retained its blocked send"
        );
        assert_eq!(history.active_requests(), 0);
        timer.await.unwrap();
        peer.await.unwrap();
    }
}
