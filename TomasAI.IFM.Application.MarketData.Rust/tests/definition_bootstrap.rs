//! Exact symbol bootstrap and pinned-SDK coverage-date compatibility.
use ifm_market_data_native::{dns::Resolver, historical::History, runtime::Deadline};
use std::{sync::Arc, time::Duration};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpListener,
};
use tokio_util::sync::CancellationToken;

#[tokio::test]
async fn exact_bootstrap_uses_sdk_coverage_selection_and_rejects_invalid_coverage_before_query() {
    let start = "1990-01-01T00:00:00Z";
    for (coverage, expected) in [
        (
            serde_json::json!({"start":start,"end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":start,"end":"2026-11-02T23:59:59.123456789Z"}}}),
            Some(("2026-11-01", "2026-11-02T23:59:59.123456789Z")),
        ),
        (
            serde_json::json!({"start":start,"end":"2026-10-25T00:00:00Z","schema":{}}),
            Some(("2026-10-24", "2026-10-25T00:00:00Z")),
        ),
        (
            serde_json::json!({"start":start,"end":"2026-10-25Tbroken","schema":{}}),
            None,
        ),
        (
            serde_json::json!({"start":start,"end":"2026-02-30T00:00:00Z","schema":{}}),
            None,
        ),
        (
            serde_json::json!({"start":start,"end":"2026-10-25T00:00:00Z","schema":{"definition":{"start":start,"end":42}}}),
            None,
        ),
        (
            serde_json::json!({"start":start,"end":"2026-10-25T00:00:00Z","schema":{"definition":null}}),
            None,
        ),
        (
            serde_json::json!({"end":"2026-10-25T00:00:00Z","schema":{}}),
            None,
        ),
    ] {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let base = format!("http://{}/", listener.local_addr().unwrap());
        let peer = tokio::spawn(async move {
            for phase in 0..if expected.is_some() { 2 } else { 1 } {
                let (mut socket, _) = listener.accept().await.unwrap();
                let mut header = Vec::new();
                let mut one = [0];
                while !header.ends_with(b"\r\n\r\n") {
                    socket.read_exact(&mut one).await.unwrap();
                    header.push(one[0]);
                    assert!(header.len() < 16_384);
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
                let body = if phase == 0 {
                    coverage.to_string()
                } else {
                    let form: std::collections::BTreeMap<String, String> =
                        serde_urlencoded::from_bytes(&form).unwrap();
                    let (start, end) = expected.unwrap();
                    assert_eq!(form["start"], start);
                    assert_eq!(form["end"], end);
                    assert_eq!(form["symbols"], "ESZ6,ESZ6 C6000");
                    assert_eq!(form["stype_in"], "raw_symbol");
                    "{\"instrument\":42}\n".into()
                };
                socket.write_all(format!("HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{body}", body.len()).as_bytes()).await.unwrap();
            }
        });
        let history = History::new(
            &base,
            "x".repeat(32),
            Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
        )
        .unwrap();
        let mut observed = Vec::new();
        let result = history
            .exact_definitions(
                "GLBX.MDP3",
                &["ESZ6".into(), "ESZ6 C6000".into()],
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(2)),
                |line| {
                    observed.push(line.to_owned());
                    Ok(())
                },
            )
            .await;
        assert_eq!(result.is_ok(), expected.is_some());
        assert_eq!(observed.len(), usize::from(expected.is_some()));
        peer.await.unwrap();
    }
}

#[tokio::test]
async fn invalid_exact_symbols_fail_without_dns_or_provider_io() {
    let resolver = Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap());
    let history = History::new("http://127.0.0.1:1/", "x".repeat(32), resolver.clone()).unwrap();
    for symbols in [
        vec![],
        vec!["".into()],
        vec![" ".into()],
        vec!["ES\nZ6".into()],
        vec!["ES,Z6".into()],
        vec!["☃".into()],
        vec!["x".repeat(71)],
        vec!["ESZ6".into(); 2049],
    ] {
        let result = history
            .exact_definitions(
                "GLBX.MDP3",
                &symbols,
                &CancellationToken::new(),
                Deadline::after(Duration::from_secs(1)),
                |_| panic!("Invalid request reached the decoder"),
            )
            .await;
        assert!(matches!(
            result,
            Err(ifm_market_data_native::historical::Failure::Input(_))
        ));
    }
    assert_eq!(resolver.active_exchanges(), 0);
    assert_eq!(history.active_requests(), 0);
}
