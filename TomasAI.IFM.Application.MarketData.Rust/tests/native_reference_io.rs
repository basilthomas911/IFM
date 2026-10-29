//! The raw database handoff is outside provider ownership. A stalled copied
//! consumer cannot pin the native request socket or another dataset's resources.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    messaging::{codec, publisher::BrokerSettings, raw_io},
    runtime::{Deadline, owner::Owner},
};
use std::{sync::Arc, time::Duration};
use tokio::{
    io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
    net::TcpListener,
};
use tokio_util::sync::CancellationToken;
#[test]
fn actual_csharp_pages_match_native_decoder_and_all_truncations_are_bounded() {
    let frames: Vec<String> = serde_json::from_str(include_str!(
        "../qualification/NativeOwnershipBridge/reference-pages.json"
    ))
    .unwrap();
    let mut data = Vec::new();
    for frame in frames {
        let bytes = STANDARD.decode(frame).unwrap();
        let page = raw_io::decode(&bytes).unwrap();
        assert_eq!(page.id, 77);
        assert_eq!(page.total, 9000);
        assert_eq!(page.offset as usize, data.len());
        assert!(page.error.is_none());
        data.extend(page.bytes);
        for cut in 0..bytes.len() {
            assert!(raw_io::decode(&bytes[..cut]).is_err(), "truncation {cut}");
        }
        let mut trailing = bytes;
        trailing.push(0);
        assert!(raw_io::decode(&trailing).is_err());
    }
    assert_eq!(data, (0..9000).map(|i| (i % 251) as u8).collect::<Vec<_>>());
    for length in [4097u32, u32::MAX] {
        let mut invalid = vec![0x96, 1, 1, 0, 0xdb];
        invalid.extend_from_slice(&length.to_be_bytes());
        assert!(raw_io::decode(&invalid).is_err());
    }
    assert!(raw_io::request(0, 1, b"{}", 0).is_err());
    assert!(raw_io::request(1, 0, b"{}", 0).is_err());
    assert!(raw_io::request(1, 8, b"{}", 0).is_ok());
    assert!(raw_io::request(1, 9, b"{}", 0).is_ok());
    for kind in 1..=10 {
        assert!(raw_io::request(1, kind, b"{}", 0).is_ok());
    }
    for kind in [11, u8::MAX] {
        assert!(raw_io::request(1, kind, b"{}", 0).is_err());
    }
    assert!(raw_io::request(1, 1, &vec![0; 16385], 0).is_err());
    assert!(raw_io::request(1, 1, b"{}", 4095).is_err());
}
#[tokio::test]
async fn native_paging_checks_integrity_errors_and_cancel_join_without_ffi_or_database_callbacks() {
    for mode in 0..6 {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let endpoint = format!("nats://{}", listener.local_addr().unwrap());
        let (arrived, accepted) = tokio::sync::oneshot::channel();
        let peer = tokio::spawn(async move {
            let (socket, _) = listener.accept().await.unwrap();
            let (read, mut write) = socket.into_split();
            let mut reader = BufReader::new(read);
            let mut arrived = Some(arrived);
            write.write_all(b"INFO {\"server_id\":\"raw-reference-fixture\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
            let payload = (0..9000).map(|i| (i % 251) as u8).collect::<Vec<_>>();
            use sha2::{Digest, Sha256};
            let digest = format!("{:X}", Sha256::digest(&payload));
            let mut line = String::new();
            loop {
                line.clear();
                if reader.read_line(&mut line).await.unwrap_or(0) == 0 {
                    break;
                }
                if line.starts_with("PING") {
                    if write.write_all(b"PONG\r\n").await.is_err() {
                        break;
                    }
                } else if line.starts_with("PUB ") {
                    let fields = line.split_whitespace().collect::<Vec<_>>();
                    assert_eq!(fields.len(), 4);
                    assert!(fields[1].ends_with(".reference.read"));
                    let length = fields[3].parse::<usize>().unwrap();
                    assert!(length <= 16 * 1024 + 64);
                    let mut body = vec![0; length + 2];
                    reader.read_exact(&mut body).await.unwrap();
                    let request = rmpv::decode::read_value(&mut body[..length].as_ref()).unwrap();
                    let request = request.as_array().unwrap();
                    assert_eq!(request.len(), 4);
                    let id = request[0].as_u64().unwrap();
                    let offset = request[3].as_u64().unwrap() as usize;
                    if let Some(arrived) = arrived.take() {
                        let _ = arrived.send(());
                    }
                    if mode == 1 {
                        continue;
                    }
                    let error = if mode == 5 {
                        Some("Storage unavailable")
                    } else {
                        None
                    };
                    let total = if error.is_some() { 0 } else { payload.len() };
                    let count = if error.is_some() {
                        0
                    } else {
                        (payload.len() - offset).min(raw_io::PAGE)
                    };
                    let response = if mode == 4 {
                        vec![0x96, 1, 1, 0, 0xdb, 0xff, 0xff, 0xff, 0xff]
                    } else {
                        codec::encode(&rmpv::Value::Array(vec![
                            id.into(),
                            (total as u32).into(),
                            (if error.is_some() { 0 } else { offset as u32 }).into(),
                            if error.is_some() {
                                "".into()
                            } else if mode == 3 || mode == 2 && offset > 0 {
                                "0".repeat(64).into()
                            } else {
                                digest.clone().into()
                            },
                            rmpv::Value::Binary(payload[offset..offset + count].to_vec()),
                            error.map_or(rmpv::Value::Nil, Into::into),
                        ]))
                        .unwrap()
                    };
                    let header = format!("MSG {} 1 {}\r\n", fields[2], response.len());
                    if write.write_all(header.as_bytes()).await.is_err()
                        || write.write_all(&response).await.is_err()
                        || write.write_all(b"\r\n").await.is_err()
                    {
                        break;
                    }
                }
            }
        });
        let root = Arc::new(Owner::default());
        let parent = Arc::new(root.child().unwrap());
        let sibling = Arc::new(root.child().unwrap());
        let stop = CancellationToken::new();
        let stopping = stop.clone();
        let owned = parent.clone();
        let work = tokio::spawn(async move {
            raw_io::read(
                BrokerSettings::new(endpoint, vec!["127.0.0.1:1".parse().unwrap()]).unwrap(),
                "development".into(),
                owned,
                1,
                b"{}",
                &stopping,
                Deadline::after(Duration::from_secs(2)),
            )
            .await
        });
        tokio::time::timeout(Duration::from_secs(2), accepted)
            .await
            .unwrap()
            .unwrap();
        if mode == 1 {
            stop.cancel();
        }
        let result = tokio::time::timeout(Duration::from_secs(2), work)
            .await
            .unwrap()
            .unwrap();
        if mode == 0 {
            assert_eq!(result.unwrap().len(), 9000);
        } else {
            assert!(result.is_err(), "invalid mode {mode}");
        }
        assert!(!sibling.cancellation().is_cancelled());
        drop(parent);
        drop(sibling);
        root.stop().unwrap();
        root.wait_idle(
            &CancellationToken::new(),
            Deadline::after(Duration::from_secs(2)),
        )
        .await
        .unwrap();
        assert!(root.disposable().unwrap());
        tokio::time::timeout(Duration::from_secs(1), peer)
            .await
            .unwrap()
            .unwrap();
    }
}
