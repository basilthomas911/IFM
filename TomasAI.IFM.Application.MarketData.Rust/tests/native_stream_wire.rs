//! Byte-exact existing C# envelope and subject parity. Regenerate fixtures only
//! with qualification/StreamingWireBridge using the actual managed serializer.
use base64::Engine;
use ifm_market_data_native::{dataset::transport::Lane, messaging::wire::Frame};
use serde::Deserialize;

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct Oracle {
    lane: u8,
    value_date: i32,
    worker: String,
    generation: String,
    revision: i64,
    sequence: i64,
    kind: u8,
    payload: String,
    subject: String,
    envelope: String,
    content: String,
}
fn bytes(value: &str) -> Vec<u8> {
    base64::engine::general_purpose::STANDARD
        .decode(value)
        .unwrap()
}

#[test]
fn all_lanes_and_kinds_match_actual_csharp_serializer_bytes_and_subjects() {
    let rows: Vec<Oracle> = serde_json::from_str(include_str!(
        "../qualification/StreamingWireBridge/envelopes.json"
    ))
    .unwrap();
    assert_eq!(rows.len(), 21);
    for row in rows {
        let lane = match row.lane {
            1 => Lane::FuturesGlbx,
            2 => Lane::FuturesXcbf,
            3 => Lane::OptionsGlbx,
            _ => panic!("invalid oracle lane"),
        };
        let frame = Frame {
            lane,
            value_date: row.value_date,
            worker: bytes(&row.worker).try_into().unwrap(),
            generation: bytes(&row.generation).try_into().unwrap(),
            manifest_revision: row.revision,
            sequence: row.sequence,
            kind: row.kind,
            payload: bytes(&row.payload),
        };
        assert_eq!(frame.subject("development").unwrap(), row.subject);
        // Both LZ4 implementations must decode to the same byte-exact C# content;
        // compression token choice and block boundaries are not contract fields.
        for encoded in [frame.encode().unwrap(), bytes(&row.envelope)] {
            let value = rmpv::decode::read_value(&mut std::io::Cursor::new(encoded)).unwrap();
            let parts = value.as_array().unwrap();
            let rmpv::Value::Ext(98, length) = &parts[0] else {
                panic!("missing LZ4 frame")
            };
            let length = rmpv::decode::read_value(&mut std::io::Cursor::new(length))
                .unwrap()
                .as_u64()
                .unwrap() as usize;
            assert_eq!(parts.len(), 2);
            assert_eq!(
                lz4_flex::block::decompress(parts[1].as_slice().unwrap(), length).unwrap(),
                bytes(&row.content)
            );
        }

        assert_eq!(
            frame.content().unwrap(),
            bytes(&row.content),
            "lane {} kind {}",
            row.lane,
            row.kind
        );
    }
}

#[test]
fn invalid_identity_date_payload_kind_and_subject_never_publish() {
    let mut frame = Frame {
        lane: Lane::OptionsGlbx,
        value_date: 0,
        worker: [1; 16],
        generation: [2; 16],
        manifest_revision: 1,
        sequence: 1,
        kind: 1,
        payload: vec![0xc0],
    };
    for scope in ["", "*", "development.bad", "with space", ">", "test_unsafe"] {
        assert!(frame.subject(scope).is_err());
    }
    frame.worker = [0; 16];
    assert!(frame.encode().is_err());
    frame.worker = [1; 16];
    frame.generation = [0; 16];
    assert!(frame.encode().is_err());
    frame.generation = [2; 16];
    frame.value_date = -1;
    assert!(frame.encode().is_err());
    frame.value_date = 3_652_059;
    assert!(frame.encode().is_err());
    frame.value_date = 0;
    frame.sequence = 0;
    assert!(frame.encode().is_err());
    frame.sequence = 1;
    frame.manifest_revision = 0;
    assert!(frame.encode().is_err());
    frame.manifest_revision = 1;
    frame.kind = 0;
    assert!(frame.encode().is_err());
    frame.kind = 8;
    assert!(frame.encode().is_err());
    frame.kind = 1;
    frame.payload.clear();
    assert!(frame.encode().is_err());
    frame.payload = vec![0; 1024 * 1024 + 1];
    assert!(frame.encode().is_err());
    frame.payload.truncate(1024 * 1024);
    assert!(frame.encode().is_ok());
}
