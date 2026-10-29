//! Real owned batch HTTP requests and fixed-buffer file streaming, offline only.
use databento::{
    dbn::Schema,
    historical::batch::{BatchFileDesc, ListJobsParams, SubmitJobParams},
};
use ifm_market_data_native::{
    dns::Resolver,
    historical::{Failure, History},
    runtime::{Deadline, Interrupted},
};
use sha2::{Digest, Sha256};
use std::{collections::HashMap, sync::Arc, time::Duration};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    net::TcpListener,
};
use tokio_util::sync::CancellationToken;
fn resolver() -> Arc<Resolver> {
    Arc::new(Resolver::new(vec!["127.0.0.1:1".parse().unwrap()]).unwrap())
}
async fn request(socket: &mut tokio::net::TcpStream) -> (String, String) {
    let mut bytes = Vec::new();
    let mut byte = [0];
    while !bytes.ends_with(b"\r\n\r\n") {
        socket.read_exact(&mut byte).await.unwrap();
        bytes.push(byte[0]);
        assert!(bytes.len() < 65536)
    }
    let header = String::from_utf8(bytes).unwrap();
    let length = header
        .lines()
        .find_map(|line| {
            line.to_ascii_lowercase()
                .strip_prefix("content-length:")
                .map(|v| v.trim().parse::<usize>().unwrap())
        })
        .unwrap_or(0);
    let mut body = vec![0; length];
    socket.read_exact(&mut body).await.unwrap();
    (header, String::from_utf8(body).unwrap())
}
fn job() -> serde_json::Value {
    serde_json::json!({"id":"job-123","dataset":"GLBX.MDP3","symbols":["ESZ6"],"stype_in":"raw_symbol","stype_out":"instrument_id","schema":"trades","start":"2026-10-26T00:00:00Z","end":"2026-10-27T00:00:00Z","encoding":"dbn","compression":"zstd","pretty_px":false,"pretty_ts":false,"map_symbols":false,"split_symbols":false,"split_duration":"day","delivery":"download","state":"queued","ts_received":"2026-10-26T00:00:01Z","ts_queued":null,"ts_process_start":null,"ts_process_done":null,"ts_expiration":null})
}
#[tokio::test]
async fn batch_control_uses_exact_sdk_forms_and_retains_job_and_file_fields() {
    let server = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let base = format!("http://{}/", server.local_addr().unwrap());
    let expected = job();
    let response = expected.clone();
    let task = tokio::spawn(async move {
        let mut captured = Vec::new();
        for i in 0..4 {
            let (mut socket, _) = server.accept().await.unwrap();
            captured.push(request(&mut socket).await);
            let body=match i{0|2=>response.to_string(),1=>serde_json::json!([response]).to_string(),_=>serde_json::json!([{"filename":"trades.dbn.zst","size":123,"hash":"sha256:abc","urls":{"https":"https://example.test/file"}}]).to_string()};
            socket
                .write_all(
                    format!(
                        "HTTP/1.1 200 OK\r\nContent-Length: {}\r\n\r\n{}",
                        body.len(),
                        body
                    )
                    .as_bytes(),
                )
                .await
                .unwrap();
            let mut byte = [0];
            assert_eq!(socket.read(&mut byte).await.unwrap(), 0);
        }
        captured
    });
    let history = History::new(&base, "x".repeat(32), resolver()).unwrap();
    let token = CancellationToken::new();
    let deadline = Deadline::after(Duration::from_secs(3));
    let start = time::OffsetDateTime::from_unix_timestamp(1792972800).unwrap();
    let params = SubmitJobParams::builder()
        .dataset("GLBX.MDP3")
        .schema(Schema::Trades)
        .symbols(vec!["ESZ6"])
        .date_time_range(start..start + time::Duration::days(1))
        .build();
    assert_eq!(
        history
            .submit_batch(&params, &token, deadline)
            .await
            .unwrap()
            .id,
        "job-123"
    );
    assert_eq!(
        history
            .list_batch_jobs(&ListJobsParams::default(), &token, deadline)
            .await
            .unwrap()
            .len(),
        1
    );
    assert_eq!(
        history
            .batch_details("job-123", &token, deadline)
            .await
            .unwrap()
            .id,
        "job-123"
    );
    let files = history
        .batch_files("job-123", &token, deadline)
        .await
        .unwrap();
    assert_eq!(
        (files[0].filename.as_str(), files[0].size),
        ("trades.dbn.zst", 123)
    );
    let captured = task.await.unwrap();
    assert!(captured[0].0.starts_with("POST /v0/batch.submit_job "));
    let form: HashMap<String, String> = serde_urlencoded::from_str(&captured[0].1).unwrap();
    for (key, value) in [
        ("dataset", "GLBX.MDP3"),
        ("schema", "trades"),
        ("symbols", "ESZ6"),
        ("encoding", "dbn"),
        ("compression", "zstd"),
        ("pretty_px", "false"),
        ("pretty_ts", "false"),
        ("map_symbols", "false"),
        ("split_symbols", "false"),
        ("delivery", "download"),
        ("stype_in", "raw_symbol"),
        ("stype_out", "instrument_id"),
        ("split_duration", "day"),
    ] {
        assert_eq!(form.get(key).unwrap(), value);
    }
    assert_eq!(form["start"], start.unix_timestamp_nanos().to_string());
    assert_eq!(
        form["end"],
        (start + time::Duration::days(1))
            .unix_timestamp_nanos()
            .to_string()
    );
    assert!(captured[1].0.starts_with("GET /v0/batch.list_jobs "));
    assert!(
        captured[2]
            .0
            .starts_with("GET /v0/batch.get_job_details?job_id=job-123 ")
    );
    assert!(
        captured[3]
            .0
            .starts_with("GET /v0/batch.list_files?job_id=job-123 ")
    );
    for (header, _) in captured {
        assert!(
            header
                .to_ascii_lowercase()
                .contains("authorization: basic ")
        );
    }
    let _: databento::historical::batch::BatchJob = serde_json::from_value(expected).unwrap();
}
#[tokio::test]
async fn large_file_streams_in_fixed_chunks_using_authenticated_api_origin_not_descriptor_origin() {
    const CHUNKS: usize = 1024;
    let chunk = vec![42u8; 65536];
    let mut hash = Sha256::new();
    for _ in 0..CHUNKS {
        hash.update(&chunk)
    }
    let expected = format!("{:x}", hash.finalize());
    let server = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let base = format!("http://{}/", server.local_addr().unwrap());
    let url = "http://127.0.0.1:1/file?signature=not-forwarded".to_owned();
    let task = tokio::spawn(async move {
        let (mut socket, _) = server.accept().await.unwrap();
        let (header, _) = request(&mut socket).await;
        assert!(header.starts_with("GET /file "));
        assert!(
            header
                .to_ascii_lowercase()
                .contains("authorization: basic ")
        );
        socket
            .write_all(
                format!(
                    "HTTP/1.1 200 OK\r\nContent-Length: {}\r\n\r\n",
                    CHUNKS * 65536
                )
                .as_bytes(),
            )
            .await
            .unwrap();
        for _ in 0..CHUNKS {
            socket.write_all(&chunk).await.unwrap()
        }
        let mut byte = [0];
        assert_eq!(socket.read(&mut byte).await.unwrap(), 0);
    });
    let history = History::new(&base, "x".repeat(32), resolver()).unwrap();
    let file = BatchFileDesc {
        filename: "huge.dbn.zst".into(),
        size: (CHUNKS * 65536) as u64,
        hash: format!("sha256:{expected}"),
        urls: HashMap::from([("https".into(), url)]),
    };
    let mut received = 0;
    let result = history
        .download_batch(
            &file,
            &CancellationToken::new(),
            Deadline::operation(Duration::from_secs(30)),
            |bytes| {
                assert!(bytes.len() <= 65536);
                assert!(bytes.iter().all(|b| *b == 42));
                received += bytes.len();
                Ok(())
            },
        )
        .await
        .unwrap();
    assert_eq!(received, CHUNKS * 65536);
    assert_eq!(result.sha256, expected);
    assert_eq!(result.size, received as u64);
    task.await.unwrap();
}
#[tokio::test]
async fn batch_download_invalid_size_checksum_consumer_failure_and_deadline_close_owned_socket() {
    for mode in 0..5 {
        let server = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let base = format!("http://{}/", server.local_addr().unwrap());
        let url = format!("http://{}/file", server.local_addr().unwrap());
        let task = tokio::spawn(async move {
            let (mut socket, _) = server.accept().await.unwrap();
            request(&mut socket).await;
            let length = if mode == 0 { 9 } else { 8 };
            socket
                .write_all(
                    format!("HTTP/1.1 200 OK\r\nContent-Length: {length}\r\n\r\n").as_bytes(),
                )
                .await
                .unwrap();
            if mode != 4 {
                let data = if mode == 3 {
                    &b"bad"[..]
                } else {
                    &b"12345678"[..]
                };
                let _ = socket.write_all(data).await;
            }
            if mode == 3 {
                socket.shutdown().await.unwrap();
                return;
            }
            let mut byte = [0];
            assert!(matches!(socket.read(&mut byte).await, Ok(0) | Err(_)));
        });
        let history = History::new(&base, "x".repeat(32), resolver()).unwrap();
        let digest = if mode == 1 {
            "0".repeat(64)
        } else {
            format!("{:x}", Sha256::digest(b"12345678"))
        };
        let file = BatchFileDesc {
            filename: "test".into(),
            size: 8,
            hash: format!("sha256:{digest}"),
            urls: HashMap::from([("https".into(), url)]),
        };
        let result = history
            .download_batch(
                &file,
                &CancellationToken::new(),
                Deadline::after(Duration::from_millis(150)),
                |_| {
                    if mode == 2 {
                        Err("test consumer failed".into())
                    } else {
                        Ok(())
                    }
                },
            )
            .await;
        match mode {
            4 => assert!(matches!(
                result,
                Err(Failure::Interrupted(Interrupted::DeadlineExpired))
            )),
            2 => assert!(matches!(result, Err(Failure::Processing(_)))),
            _ => assert!(matches!(result, Err(Failure::Provider(_)))),
        }
        task.await.unwrap();
    }
}

#[tokio::test]
async fn descriptor_double_slash_path_cannot_replace_authenticated_api_authority() {
    let server = TcpListener::bind("127.0.0.1:0").await.unwrap();
    let address = server.local_addr().unwrap();
    let task = tokio::spawn(async move {
        let (mut socket, _) = server.accept().await.unwrap();
        let (header, _) = request(&mut socket).await;
        assert!(header.starts_with("GET //external.invalid/file "));
        assert!(
            header.contains(&format!("Host: {address}"))
                || header.contains(&format!("host: {address}"))
        );
        assert!(
            header
                .to_ascii_lowercase()
                .contains("authorization: basic ")
        );
        assert!(!header.contains("not-forwarded"));
        socket
            .write_all(b"HTTP/1.1 200 OK\r\nContent-Length: 8\r\nConnection: close\r\n\r\n12345678")
            .await
            .unwrap();
    });
    let history = History::new(&format!("http://{address}/"), "x".repeat(32), resolver()).unwrap();
    let file = BatchFileDesc {
        filename: "test.dbn".into(),
        size: 8,
        hash: format!("sha256:{:x}", Sha256::digest(b"12345678")),
        urls: HashMap::from([(
            "https".into(),
            "https://untrusted.invalid//external.invalid/file?signature=not-forwarded".into(),
        )]),
    };
    history
        .download_batch(
            &file,
            &CancellationToken::new(),
            Deadline::operation(Duration::from_secs(3)),
            |_| Ok(()),
        )
        .await
        .unwrap();
    task.await.unwrap();
}
