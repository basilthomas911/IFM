//! Differential checks for performance changes; expected wire bytes use the previous independent Value encoder.
use ifm_market_data_native::{
    dataset::transport::Lane,
    messaging::{codec, wire::Frame},
    pricing::{Calculator, Request, Settings},
    runtime::Deadline,
};
use rmpv::Value;
use tokio_util::sync::CancellationToken;

#[test]
fn direct_envelopes_preserve_every_wire_byte_and_reject_invalid_identity() {
    for lane in [Lane::FuturesGlbx, Lane::FuturesXcbf, Lane::OptionsGlbx] {
        for length in [1, 63, 64, 127, 128, 255, 256, 4096, 65536] {
            let frame = Frame {
                lane,
                value_date: 3_652_058,
                worker: [1; 16],
                generation: [2; 16],
                manifest_revision: i64::MAX,
                sequence: i64::MAX,
                kind: 7,
                payload: (0..length).map(|i| (i * 37) as u8).collect(),
            };
            let expected = codec::encode(&frame.value().unwrap()).unwrap();
            assert_eq!(frame.content().unwrap(), expected);
            assert_eq!(frame.encode().unwrap(), old_compress(expected));
        }
    }
    let invalid = Frame {
        lane: Lane::OptionsGlbx,
        value_date: 0,
        worker: [0; 16],
        generation: [2; 16],
        manifest_revision: 1,
        sequence: 1,
        kind: 1,
        payload: vec![1],
    };
    assert!(invalid.content().is_err());
}

fn old_compress(content: Vec<u8>) -> Vec<u8> {
    if content.len() < 64 {
        return content;
    }
    let size = codec::encode(&Value::from(content.len() as i32)).unwrap();
    codec::encode(&Value::Array(vec![
        Value::Ext(98, size),
        Value::Binary(lz4_flex::block::compress(&content)),
    ]))
    .unwrap()
}

#[test]
fn compression_keeps_original_framing_at_thresholds_and_incompressible_sizes() {
    for length in [0, 1, 63, 64, 65, 127, 128, 255, 256, 65535, 65536] {
        for repeating in [false, true] {
            let mut random = 123456789_u32;
            let bytes: Vec<_> = (0..length)
                .map(|_| {
                    random ^= random << 13;
                    random ^= random >> 17;
                    random ^= random << 5;
                    if repeating { 42 } else { random as u8 }
                })
                .collect();
            assert_eq!(codec::compress(bytes.clone()).unwrap(), old_compress(bytes));
        }
    }
}

#[tokio::test]
async fn cancelled_and_concurrent_pricing_workspace_leases_preserve_results() {
    let calculator = Calculator::new(Settings::default()).unwrap();
    let model = Request {
        forward: 5000.,
        strike: 5000.,
        time: 30. / 365.,
        rate: 0.04,
        call: true,
        american: true,
        futures_style: false,
    };
    let cancelled = CancellationToken::new();
    cancelled.cancel();
    assert!(
        calculator
            .price(model, 0.2, &cancelled, Deadline::recovery())
            .await
            .is_err()
    );
    let stop = CancellationToken::new();
    let (left, right) = tokio::join!(
        calculator.price(model, 0.2, &stop, Deadline::recovery()),
        calculator.price(model, 0.3, &stop, Deadline::recovery())
    );
    for (actual, sigma) in [(left.unwrap(), 0.2), (right.unwrap(), 0.3)] {
        let expected = Calculator::new(Settings::default())
            .unwrap()
            .price(model, sigma, &stop, Deadline::recovery())
            .await
            .unwrap();
        assert_eq!(actual, expected);
    }
    assert!(
        calculator
            .price(
                model,
                0.2,
                &stop,
                Deadline::operation(std::time::Duration::ZERO)
            )
            .await
            .is_err()
    );
    let reused = calculator
        .price(model, 0.2, &stop, Deadline::recovery())
        .await
        .unwrap();
    assert_eq!(
        reused,
        Calculator::new(Settings::default())
            .unwrap()
            .price(model, 0.2, &stop, Deadline::recovery())
            .await
            .unwrap()
    );
}

#[test]
fn notification_bits_survive_drain_and_resignal_without_lost_wakeups() {
    use ifm_market_data_native::runtime::owner::Owner;
    use std::sync::{Arc, mpsc};
    let owner = Arc::new(Owner::default());
    let (ready, received) = mpsc::channel();
    let waiting = owner.clone();
    let thread = std::thread::spawn(move || {
        for _ in 0..1000 {
            ready.send(()).unwrap();
            assert!(waiting.wait().unwrap());
            let signals = waiting.take_signals().unwrap();
            assert_eq!(signals & 3, 3);
        }
    });
    for _ in 0..1000 {
        received
            .recv_timeout(std::time::Duration::from_secs(5))
            .unwrap();
        // One locked signal sets all required bits before the waiting consumer can drain them.
        owner.signal(3).unwrap();
    }
    thread.join().unwrap();
    assert_eq!(owner.take_signals().unwrap(), 0);
}

#[test]
fn guid_format_preserves_clr_byte_order_without_formatter_growth() {
    use ifm_market_data_native::messaging::wire::guid;
    for seed in 0..1024_u32 {
        let bytes: [u8; 16] = std::array::from_fn(|index| {
            seed.wrapping_mul(37).wrapping_add(index as u32 * 23) as u8
        });
        let expected = format!(
            "{:08x}-{:04x}-{:04x}-{:02x}{:02x}-{:02x}{:02x}{:02x}{:02x}{:02x}{:02x}",
            u32::from_le_bytes(bytes[..4].try_into().unwrap()),
            u16::from_le_bytes(bytes[4..6].try_into().unwrap()),
            u16::from_le_bytes(bytes[6..8].try_into().unwrap()),
            bytes[8],
            bytes[9],
            bytes[10],
            bytes[11],
            bytes[12],
            bytes[13],
            bytes[14],
            bytes[15]
        );
        assert_eq!(guid(bytes), expected);
    }
}

#[test]
fn streamed_snapshot_and_moved_cache_preserve_old_bytes_and_string_storage() {
    use ifm_market_data_native::{
        messaging::option_cache::{CHUNK, Publication},
        messaging::wire::guid,
        option_chain::composition::model::{Instrument, Snapshot, Valued},
    };
    use sha2::{Digest, Sha256};
    for count in [0, 1, 15, 16, 128] {
        let mut snapshot = Snapshot {
            ranking: None,
            schema: 2,
            id: [1; 16],
            scope: "fixture".into(),
            token: "policy/v1".into(),
            horizon: "Daily".into(),
            generation: [2; 16],
            at: 0,
            until: 10_000_000,
            instruments: (0..count)
                .map(|index| Valued {
                    instrument: Instrument {
                        contract: format!("Contract{index:04}"),
                        quote: None,
                        context: None,
                        strike: None,
                        call: None,
                        underlying: None,
                        future: None,
                        selection: None,
                        volume: None,
                        interest: None,
                        statistics_at: None,
                        unavailable: Some(if count == 1 {
                            "x".repeat(150_000)
                        } else {
                            "Unavailable fixture".into()
                        }),
                        last_event: None,
                        last_received: None,
                        volume_date: None,
                        interest_date: None,
                    },
                    valuation: None,
                })
                .collect(),
            digest: String::new(),
        };
        snapshot.seal().unwrap();
        assert_eq!(
            snapshot.encode().unwrap(),
            codec::encode(&snapshot.value().unwrap()).unwrap()
        );
        let expected = codec::encode(&Value::Array(vec![
            snapshot.value().unwrap(),
            739914.into(),
            "policy/v1".into(),
            1.into(),
            "fixture".into(),
        ]))
        .unwrap();
        let address = snapshot
            .instruments
            .first()
            .map(|row| row.instrument.contract.as_ptr());
        let publication = Publication::owned(snapshot, 739914, "policy/v1", 1, "fixture").unwrap();
        assert_eq!(publication.content(), expected);
        assert_eq!(
            publication
                .snapshot
                .instruments
                .first()
                .map(|row| row.instrument.contract.as_ptr()),
            address
        );
        for index in 0..publication.chunks() {
            let offset = index * CHUNK;
            let frame = codec::encode(&Value::Array(vec![
                guid([2; 16]).into(),
                "fixture".into(),
                1.into(),
                publication.len().into(),
                offset.into(),
                Value::Binary(expected[offset..(offset + CHUNK).min(expected.len())].to_vec()),
                format!("{:X}", Sha256::digest(&expected)).into(),
            ]))
            .unwrap();
            assert_eq!(publication.frame(index).unwrap(), old_compress(frame));
        }
    }
}
