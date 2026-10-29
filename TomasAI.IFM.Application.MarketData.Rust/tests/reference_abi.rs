//! Reference query output bounds, retained-slot ownership and failure atomicity.
use ifm_market_data_native::ffi::{live::*, reference::*, runtime::*};
use std::io::{Read, Write};

unsafe fn finish(handle: u64, id: u64) {
    let mut count = 0;
    let mut completion = NativeCompletion::default();
    while count == 0 {
        let mut available = 0;
        // SAFETY: Independent aligned exclusive destinations and a live runtime.
        unsafe {
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
            let mut signals = 0;
            assert_eq!(ifm_market_data_live_signals_v1(handle, &mut signals), 0);
            assert_eq!(
                ifm_market_data_runtime_drain_v1(handle, &mut completion, 1, &mut count),
                0
            );
        }
    }
    assert_eq!(completion.operation, id);
    assert_eq!(completion.outcome, 0);
    let mut result = NativeControlResult::default();
    // SAFETY: Correct size, matching retained control operation, exclusive DTO.
    unsafe {
        assert_eq!(
            ifm_market_data_live_take_v1(handle, id, 4112, &mut result),
            0
        );
    }
    assert_eq!(
        result.failure,
        0,
        "{}",
        String::from_utf8_lossy(&result.message[..result.length as usize])
    );
}

#[test]
fn reference_results_remain_bounded_preserve_small_outputs_and_block_premature_release() {
    let listener = std::net::TcpListener::bind("127.0.0.1:0").unwrap();
    let endpoint = format!("http://{}/treasury.xml", listener.local_addr().unwrap());
    let server = std::thread::spawn(move || {
        let (mut stream, _) = listener.accept().unwrap();
        let mut head = Vec::new();
        let mut byte = [0];
        while !head.ends_with(b"\r\n\r\n") {
            assert_eq!(stream.read(&mut byte).unwrap(), 1);
            head.push(byte[0]);
            assert!(head.len() < 4096);
        }
        let body=b"<feed xmlns='http://www.w3.org/2005/Atom' xmlns:d='http://schemas.microsoft.com/ado/2007/08/dataservices' xmlns:m='http://schemas.microsoft.com/ado/2007/08/dataservices/metadata'><entry><content><m:properties><d:NEW_DATE>2026-09-08T00:00:00</d:NEW_DATE><d:BC_1MONTH>5.00</d:BC_1MONTH></m:properties></content></entry></feed>";
        write!(
            stream,
            "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: close\r\n\r\n",
            body.len()
        )
        .unwrap();
        stream.write_all(body).unwrap();
        assert_eq!(stream.read(&mut byte).unwrap(), 0);
    });
    let config =
        serde_json::to_vec(&serde_json::json!({"endpoint":endpoint,"dns":["127.0.0.1:1"]}))
            .unwrap();
    let mut handle = 0;
    let mut length = 77;
    let mut buffer = vec![0xaau8; 524_288];
    // SAFETY: All configured/read/write ranges are bounded and disjoint. Invalid
    // capacities/DTO sizes are rejected before access; no pointer escapes a call.
    unsafe {
        assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
        assert_eq!(
            ifm_market_data_reference_configure_v1(handle, config.as_ptr(), config.len() as u32),
            1,
            "API clock is required first"
        );
        let clock = NativeLiveConfiguration {
            size: 40,
            date: 20260908,
            identity_low: 1,
            capacity: 8,
            ..Default::default()
        };
        assert_eq!(ifm_market_data_live_configure_v1(handle, &clock), 0);
        assert_eq!(
            ifm_market_data_reference_configure_v1(handle, config.as_ptr(), config.len() as u32),
            0
        );
        assert_eq!(
            ifm_market_data_reference_configure_v1(handle, config.as_ptr(), config.len() as u32),
            7
        );
        assert_eq!(
            ifm_market_data_reference_submit_v1(handle, 1, 2, 20260901, 20260930, 1000),
            1
        );
        assert_eq!(
            ifm_market_data_reference_submit_v1(handle, 1, 0, 20260931, 20260930, 1000),
            1
        );
        for id in 1..=32 {
            assert_eq!(
                ifm_market_data_reference_submit_v1(handle, id, 0, 20260901, 20260930, 1000),
                0
            );
            finish(handle, id);
        }
        assert_eq!(
            ifm_market_data_reference_submit_v1(handle, 33, 0, 20260901, 20260930, 1000),
            4
        );
        assert_eq!(
            ifm_market_data_reference_take_v1(handle, 2, buffer.as_mut_ptr(), 1, &mut length),
            4
        );
        assert_eq!(length, 77);
        assert!(buffer.iter().all(|b| *b == 0xaa));
        assert_eq!(
            ifm_market_data_reference_take_v1(handle, 2, buffer.as_mut_ptr(), 524_288, &mut length),
            0
        );
        let value: serde_json::Value = serde_json::from_slice(&buffer[..length as usize]).unwrap();
        assert_eq!(
            value[0]["rates"][0]["bits"],
            serde_json::json!([500, 0, 0, 131072])
        );
        let prior = buffer.clone();
        let prior_length = length;
        assert_eq!(
            ifm_market_data_reference_take_v1(handle, 2, buffer.as_mut_ptr(), 524_288, &mut length),
            1
        );
        assert_eq!(buffer, prior);
        assert_eq!(length, prior_length);
        assert_eq!(ifm_market_data_reference_discard_v1(handle, 1), 0);
        assert_eq!(
            ifm_market_data_reference_submit_v1(handle, 33, 0, 20260901, 20260930, 1000),
            0,
            "Failed admission leaked its shared control slot"
        );
        finish(handle, 33);
        assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
        let mut available = 99;
        assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
        assert_eq!(available, 0);
        assert_eq!(
            ifm_market_data_runtime_release_v1(handle),
            7,
            "Retained reference results must be explicitly released"
        );
        for id in 1..=33 {
            assert_eq!(ifm_market_data_reference_discard_v1(handle, id), 0);
        }
        assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
        assert_eq!(
            ifm_market_data_reference_take_v1(handle, 3, buffer.as_mut_ptr(), 524_288, &mut length),
            6
        );
    }
    server.join().unwrap();
}
