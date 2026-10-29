//! Real provider HTTP -> retained warning -> independent signal -> C ABI copy.
//! Copy failures preserve the warning; no real credentials/services are used.
use ifm_market_data_native::ffi::{catalog::*, history::*, live::*, runtime::*};
use std::{
    io::{Read, Write},
    net::TcpListener,
    thread,
};

#[test]
fn complete_http_warnings_survive_small_outputs_and_arrive_without_price_polling() {
    let listener = TcpListener::bind("127.0.0.1:0").unwrap();
    let address = listener.local_addr().unwrap();
    let corpus: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    let source = corpus[0]["json"].as_str().unwrap().to_owned();
    let request_id = "r".repeat(1024);
    let large = format!("a{}b", "\t".repeat(8190));
    let expected = large.clone();
    let peer = thread::spawn(move || {
        for body in [
            r#"{"start":"1990-01-01T00:00:00Z","end":"2026-12-31T00:00:00Z","schema":{"definition":{"start":"1990-01-01T00:00:00Z","end":"2026-10-25T00:00:00Z"}}}"#.to_owned(),
            source + "\n",
        ] {
            let (mut socket, _) = listener.accept().unwrap();
            socket
                .set_read_timeout(Some(std::time::Duration::from_secs(5)))
                .unwrap();
            let mut header = Vec::new();
            let mut one = [0];
            while !header.ends_with(b"\r\n\r\n") {
                socket.read_exact(&mut one).unwrap();
                header.push(one[0]);
                assert!(header.len() < 16_384);
            }
            let count: usize = String::from_utf8(header)
                .unwrap()
                .lines()
                .find_map(|line| {
                    line.to_ascii_lowercase()
                        .strip_prefix("content-length:")
                        .map(|value| value.trim().parse().unwrap())
                })
                .unwrap_or(0);
            socket.read_exact(&mut vec![0; count]).unwrap();
            write!(socket, "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nX-Warning: {}\r\nX-Warning: {} unknown warning\r\nX-Request-Id: {}\r\nConnection: close\r\n\r\n{}", body.len(), large, "x".repeat(32), request_id, body).unwrap();
        }
    });
    let config = serde_json::to_vec(&serde_json::json!({"endpoint":format!("http://{address}/"),"key":"x".repeat(32),"dns":["127.0.0.1:1"]})).unwrap();
    let query = br#"[{"dataset":"GLBX.MDP3","parents":["ES.FUT"]}]"#;
    let mut handle = 0;
    let mut received = 0;
    let mut signaled = false;
    // SAFETY: All accepted pointer ranges are bounded, aligned and exclusively
    // writable. Deliberately invalid arguments are rejected before dereference.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
        assert_eq!(ifm_market_data_catalog_configure_v1(handle, 4, 131_072), 0);
        assert_eq!(
            ifm_market_data_history_configure_v1(handle, config.as_ptr(), config.len() as u32),
            0
        );
        assert_eq!(
            ifm_market_data_catalog_refresh_v1(handle, 1, query.as_ptr(), query.len() as u32, 5000),
            0
        );
        let mut completion = NativeCompletion::default();
        while received == 0 {
            let mut available = 0;
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
            assert_eq!(available, 1);
            let mut bits = 0;
            assert_eq!(ifm_market_data_live_signals_v1(handle, &mut bits), 0);
            signaled |= bits & 16 != 0;
            assert_eq!(
                ifm_market_data_runtime_drain_v1(handle, &mut completion, 1, &mut received),
                0
            );
        }
        assert_eq!(completion.outcome, 0);
        assert!(signaled);
        let mut result = NativeControlResult::default();
        assert_eq!(
            ifm_market_data_live_take_v1(handle, 1, 4112, &mut result),
            0
        );
        assert_eq!(result.failure, 0);
        let mut buffer = vec![0xa5; 65_536];
        let mut length = 999;
        for capacity in [0, 1, 65_537] {
            assert_ne!(
                ifm_market_data_history_warning_v1(
                    handle,
                    buffer.as_mut_ptr(),
                    capacity,
                    &mut length
                ),
                0
            );
            assert_eq!(length, 999);
            assert!(buffer.iter().all(|value| *value == 0xa5));
        }
        assert_ne!(
            ifm_market_data_history_warning_v1(handle, std::ptr::null_mut(), 65_536, &mut length),
            0
        );
        assert_eq!(length, 999);
        for index in 0..4 {
            assert_eq!(
                ifm_market_data_history_warning_v1(
                    handle,
                    buffer.as_mut_ptr(),
                    65_536,
                    &mut length
                ),
                0
            );
            let warning: serde_json::Value =
                serde_json::from_slice(&buffer[..length as usize]).unwrap();
            assert_eq!(warning["status"], 200);
            assert_eq!(warning["request_id"], "r".repeat(1024));
            assert_eq!(
                warning["text"],
                if index % 2 == 0 {
                    expected.as_str()
                } else {
                    "[REDACTED] unknown warning"
                }
            );
            assert!(!String::from_utf8_lossy(&buffer[..length as usize]).contains(&"x".repeat(32)));
        }
        assert_eq!(
            ifm_market_data_history_warning_v1(handle, buffer.as_mut_ptr(), 65_536, &mut length),
            0
        );
        assert_eq!(length, 0);
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        let mut available = 1;
        assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
        assert_eq!(available, 0);
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
    }
    peer.join().unwrap();
}
