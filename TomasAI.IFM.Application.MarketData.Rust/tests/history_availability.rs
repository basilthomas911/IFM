//! Actual native HTTP availability boundaries; no paid or live provider calls.
use databento::dbn::Schema;
use ifm_market_data_native::{dns::Resolver, historical::History, runtime::Deadline};
use std::{sync::Arc, time::Duration};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpListener,
};
use tokio_util::sync::CancellationToken;

async fn history(body: &'static str) -> (History, tokio::task::JoinHandle<()>) {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let task = tokio::spawn(async move {
        let (mut socket, _) = listener.accept().await.unwrap();
        let mut bytes = Vec::new();
        while !bytes.ends_with(b"\r\n\r\n") {
            let mut byte = [0];
            socket.read_exact(&mut byte).await.unwrap();
            bytes.push(byte[0]);
            assert!(bytes.len() < 65536);
        }
        let request = String::from_utf8(bytes).unwrap();
        assert!(
            request.starts_with("GET /v0/metadata.get_dataset_range?dataset=GLBX.MDP3 HTTP/1.1")
        );
        socket
            .write_all(
                format!(
                    "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
                    body.len(),
                    body
                )
                .as_bytes(),
            )
            .await
            .unwrap();
    });
    (
        History::new(
            &format!("http://{address}/"),
            "x".repeat(32),
            Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
        )
        .unwrap(),
        task,
    )
}

#[tokio::test]
async fn schema_end_is_used_instead_of_newer_dataset_or_other_schema_end() {
    let (history, task) = history(r#"{"start":"2010-06-06T00:00:00Z","end":"2026-10-29T00:00:00Z","schema":{"ohlcv-1d":{"start":"2010-06-06T00:00:00Z","end":"2026-10-28T00:00:00Z"},"ohlcv-1m":{"start":"2010-06-06T00:00:00Z","end":"2026-10-28T23:00:00Z"}}}"#).await;
    let end = history
        .available_end(
            "GLBX.MDP3",
            Schema::Ohlcv1D,
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
    assert_eq!(
        end,
        time::OffsetDateTime::parse(
            "2026-10-28T00:00:00Z",
            &time::format_description::well_known::Rfc3339
        )
        .unwrap()
    );
    task.await.unwrap();
}

#[tokio::test]
async fn missing_invalid_empty_and_malformed_availability_never_fall_back_to_dataset_end() {
    for body in [
        r#"{"start":"2010-06-06T00:00:00Z","end":"2026-10-29T00:00:00Z","schema":{}}"#,
        r#"{"start":"2010-06-06T00:00:00Z","end":"2026-10-29T00:00:00Z","schema":{"ohlcv-1d":{"start":"2026-10-29T00:00:00Z","end":"2026-10-28T00:00:00Z"}}}"#,
        r#"{"start":"2010-06-06T00:00:00Z","end":"2026-10-29T00:00:00Z","schema":{"ohlcv-1d":{"start":"2026-10-28T00:00:00Z","end":"2026-10-28T00:00:00Z"}}}"#,
        r#"{"start":"bad","end":"2026-10-29T00:00:00Z","schema":{}}"#,
        "not-json",
    ] {
        let (history, task) = history(body).await;
        assert!(
            history
                .available_end(
                    "GLBX.MDP3",
                    Schema::Ohlcv1D,
                    &CancellationToken::new(),
                    Deadline::recovery()
                )
                .await
                .is_err()
        );
        task.await.unwrap();
    }
}

#[tokio::test]
async fn stalled_availability_is_cancellable_and_closes_its_socket() {
    let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = listener.local_addr().unwrap();
    let history = History::new(
        &format!("http://{address}/"),
        "x".repeat(32),
        Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap()),
    )
    .unwrap();
    let stop = CancellationToken::new();
    let canceled = stop.clone();
    let work = tokio::spawn(async move {
        history
            .available_end(
                "GLBX.MDP3",
                Schema::Ohlcv1D,
                &canceled,
                Deadline::recovery(),
            )
            .await
    });
    let (mut socket, _) = listener.accept().await.unwrap();
    stop.cancel();
    assert!(
        tokio::time::timeout(Duration::from_secs(2), work)
            .await
            .unwrap()
            .unwrap()
            .is_err()
    );
    let mut bytes = Vec::new();
    tokio::time::timeout(Duration::from_secs(2), socket.read_to_end(&mut bytes))
        .await
        .unwrap()
        .unwrap();
}

#[test]
fn availability_crosses_command_completion_and_retained_result_ffi_without_a_stream() {
    use ifm_market_data_native::ffi::{
        catalog::*, history::*, history_control::*, live::*, reference::*, runtime::*,
    };
    use std::io::{Read, Write};
    let listener = std::net::TcpListener::bind("127.0.0.1:0").unwrap();
    let address = listener.local_addr().unwrap();
    let peer = std::thread::spawn(move || {
        let (mut socket, _) = listener.accept().unwrap();
        socket
            .set_read_timeout(Some(Duration::from_secs(5)))
            .unwrap();
        let mut header = Vec::new();
        while !header.ends_with(b"\r\n\r\n") {
            let mut byte = [0];
            socket.read_exact(&mut byte).unwrap();
            header.push(byte[0]);
        }
        assert!(
            String::from_utf8(header)
                .unwrap()
                .starts_with("GET /v0/metadata.get_dataset_range?dataset=GLBX.MDP3")
        );
        let body = r#"{"start":"2010-06-06T00:00:00Z","end":"2026-10-29T00:00:00Z","schema":{"ohlcv-1d":{"start":"2010-06-06T00:00:00Z","end":"2026-10-28T00:00:00Z"}}}"#;
        write!(
            socket,
            "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n{}",
            body.len(),
            body
        )
        .unwrap();
    });
    let config=serde_json::to_vec(&serde_json::json!({"endpoint":format!("http://{address}/"),"key":"x".repeat(32),"dns":["127.0.0.1:1"]})).unwrap();
    let query=br#"{"dataset":"GLBX.MDP3","schema":5,"symbols":["ES.c.0"],"symbology":2,"start":1793052000000000000,"end":1793221200000000000,"limit":0}"#;
    let mut handle = 0;
    // SAFETY: Inputs are bounded borrowed byte arrays; all scalar/DTO outputs
    // are aligned, exclusive and valid for each call. No pointer is retained.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
        assert_eq!(ifm_market_data_catalog_configure_v1(handle, 4, 131072), 0);
        assert_eq!(ifm_market_data_clock_v1(handle, 0, 0), 0);
        assert_eq!(
            ifm_market_data_history_configure_v1(handle, config.as_ptr(), config.len() as u32),
            0
        );
        assert_eq!(
            ifm_market_data_history_control_v1(
                handle,
                1,
                4,
                query.as_ptr(),
                query.len() as u32,
                5000
            ),
            0
        );
        let mut count = 0;
        let mut completion = NativeCompletion::default();
        while count == 0 {
            let mut ready = 0;
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut ready), 0);
            assert_eq!(ready, 1);
            assert_eq!(
                ifm_market_data_runtime_drain_v1(handle, &mut completion, 1, &mut count),
                0
            );
        }
        assert_eq!(completion.outcome, 0);
        let mut result = NativeControlResult::default();
        assert_eq!(
            ifm_market_data_live_take_v1(handle, 1, 4112, &mut result),
            0
        );
        assert_eq!(
            result.failure,
            0,
            "{}",
            String::from_utf8_lossy(&result.message[..result.length as usize])
        );
        let mut bytes = [0u8; 65536];
        let mut length = 0;
        assert_eq!(
            ifm_market_data_reference_take_v1(
                handle,
                1,
                bytes.as_mut_ptr(),
                bytes.len() as u32,
                &mut length
            ),
            0
        );
        let result: serde_json::Value = serde_json::from_slice(&bytes[..length as usize]).unwrap();
        let end = time::OffsetDateTime::parse(
            "2026-10-28T00:00:00Z",
            &time::format_description::well_known::Rfc3339,
        )
        .unwrap();
        assert_eq!(
            result["end"].as_i64(),
            Some(end.unix_timestamp_nanos() as i64)
        );
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        let mut ready = 1;
        assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut ready), 0);
        assert_eq!(ready, 0);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    }
    peer.join().unwrap();
}
