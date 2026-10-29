//! Real bounded DLL transport, decoder ownership and cancellation under pressure.
use databento::dbn::{
    MetadataBuilder, SType, Schema, TradeMsg,
    encode::dbn::{AsyncMetadataEncoder, AsyncRecordEncoder},
};
use ifm_market_data_native::ffi::{history::*, history_stream::*, live::*, runtime::*};
use ifm_market_data_native::historical::record::Record;
use std::io::{Read, Write};
fn wire() -> Vec<u8> {
    tokio::runtime::Builder::new_current_thread()
        .build()
        .unwrap()
        .block_on(async {
            let metadata = MetadataBuilder::new()
                .dataset("GLBX.MDP3")
                .schema(Some(Schema::Trades))
                .stype_in(None)
                .stype_out(SType::InstrumentId)
                .start(0)
                .build();
            let mut bytes = Vec::new();
            AsyncMetadataEncoder::new(&mut bytes)
                .encode(&metadata)
                .await
                .unwrap();
            let mut encoder = AsyncRecordEncoder::new(&mut bytes);
            for sequence in 0..5000 {
                encoder
                    .encode(&TradeMsg {
                        price: 12345,
                        size: 7,
                        sequence,
                        ..Default::default()
                    })
                    .await
                    .unwrap();
            }
            zstd::stream::encode_all(bytes.as_slice(), 1).unwrap()
        })
}
unsafe fn completion(handle: u64, expected: i32) {
    let mut count = 0;
    let mut item = NativeCompletion::default();
    while count == 0 {
        let mut available = 0;
        let mut bits = 0;
        // SAFETY: Each scalar/DTO destination is aligned, writable and disjoint.
        unsafe {
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
            assert_eq!(ifm_market_data_live_signals_v1(handle, &mut bits), 0);
            assert_eq!(
                ifm_market_data_runtime_drain_v1(handle, &mut item, 1, &mut count),
                0
            );
        }
    }
    assert_eq!(item.operation, 1);
    assert_eq!(item.outcome, expected);
    let mut result = NativeControlResult::default();
    // SAFETY: Complete aligned retained control DTO, exclusive for the call.
    unsafe {
        assert_eq!(
            ifm_market_data_live_take_v1(handle, 1, 4112, &mut result),
            0
        );
    }
    if expected == 0 {
        assert_eq!(
            result.failure,
            0,
            "{}",
            String::from_utf8_lossy(&result.message[..result.length as usize])
        );
    }
}
#[test]
fn native_history_reader_backpressures_without_loss_and_can_cancel_with_full_queue() {
    assert_eq!(std::mem::size_of::<Record>(), 120);
    for cancel in [false, true] {
        let listener = std::net::TcpListener::bind("127.0.0.1:0").unwrap();
        let endpoint = format!("http://{}/", listener.local_addr().unwrap());
        let body = wire();
        let server = std::thread::spawn(move || {
            let (mut socket, _) = listener.accept().unwrap();
            socket
                .set_read_timeout(Some(std::time::Duration::from_secs(5)))
                .unwrap();
            let mut head = Vec::new();
            let mut byte = [0];
            while !head.ends_with(b"\r\n\r\n") {
                socket.read_exact(&mut byte).unwrap();
                head.push(byte[0]);
            }
            let text = String::from_utf8(head).unwrap();
            let length = text
                .lines()
                .find_map(|line| {
                    line.to_ascii_lowercase()
                        .strip_prefix("content-length:")
                        .map(|s| s.trim().parse::<usize>().unwrap())
                })
                .unwrap();
            let mut form = vec![0; length];
            socket.read_exact(&mut form).unwrap();
            write!(
                socket,
                "HTTP/1.1 200 OK\r\nContent-Length: {}\r\nConnection: keep-alive\r\n\r\n",
                body.len()
            )
            .unwrap();
            socket.write_all(&body).unwrap();
            assert!(
                match socket.read(&mut byte) {
                    Ok(0) => true,
                    Err(error) => matches!(
                        error.kind(),
                        std::io::ErrorKind::ConnectionReset
                            | std::io::ErrorKind::ConnectionAborted
                            | std::io::ErrorKind::BrokenPipe
                    ),
                    _ => false,
                },
                "HTTP source was not closed"
            );
        });
        let config = serde_json::to_vec(
            &serde_json::json!({"endpoint":endpoint,"key":"x".repeat(32),"dns":["127.0.0.1:9"]}),
        )
        .unwrap();
        let query=br#"{"dataset":"GLBX.MDP3","symbols":["ESZ6"],"schema":3,"symbology":1,"start":0,"end":1000000000,"limit":0}"#;
        let mut handle = 0;
        let mut output = [Record::default(); 128];
        let (mut count, mut ended) = (0, 0);
        // SAFETY: Inputs are owned readable bounded buffers; all output storage
        // is aligned, exclusive, and alive through each call. No pointer escapes.
        unsafe {
            assert_eq!(ifm_market_data_runtime_create_v1(1, 32, 2, &mut handle), 0);
            assert_eq!(
                ifm_market_data_history_configure_v1(handle, config.as_ptr(), config.len() as u32),
                0
            );
            assert_eq!(
                ifm_market_data_history_range_open_v1(
                    handle,
                    1,
                    query.as_ptr(),
                    query.len() as u32,
                    1,
                    5000
                ),
                0
            );
            let mut received = 0u32;
            while ended == 0 {
                assert_eq!(
                    ifm_market_data_history_records_v1(
                        handle,
                        1,
                        output.as_mut_ptr(),
                        128,
                        &mut count,
                        &mut ended
                    ),
                    0
                );
                for row in output.iter().take(count as usize) {
                    assert_eq!(row.size, 120);
                    assert_eq!(row.kind, 3);
                    assert_eq!(row.price, 12345);
                    assert_eq!(row.quantity, 7);
                    assert_eq!(row.sequence, i64::from(received));
                    assert_eq!(&row.symbol[..4], b"ESZ6");
                    received += 1;
                }
                if cancel && received > 0 {
                    break;
                }
                if ended == 0 && count == 0 {
                    let (mut available, mut bits) = (0, 0);
                    assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
                    assert_eq!(ifm_market_data_live_signals_v1(handle, &mut bits), 0);
                }
            }
            if cancel {
                assert_eq!(ifm_market_data_history_reader_close_v1(handle, 1), 0);
                completion(handle, 1);
            } else {
                assert_eq!(received, 5000);
                completion(handle, 0);
            }
            assert_eq!(ifm_market_data_runtime_stop_v1(handle), 0);
            let mut available = 99;
            assert_eq!(ifm_market_data_runtime_wait_v1(handle, &mut available), 0);
            assert_eq!(available, 0);
            if !cancel {
                assert_eq!(
                    ifm_market_data_runtime_release_v1(handle),
                    7,
                    "retained reader prevents premature owner release"
                );
            }
            assert_eq!(ifm_market_data_history_reader_close_v1(handle, 1), 0);
            assert_eq!(ifm_market_data_history_reader_close_v1(handle, 1), 0);
            assert_eq!(ifm_market_data_runtime_release_v1(handle), 0);
        }
        server.join().unwrap();
    }
}
