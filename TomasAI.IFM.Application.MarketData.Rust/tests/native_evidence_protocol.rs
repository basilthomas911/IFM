//! Exact C# IO handoff parity and hostile-receipt allocation boundaries.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::messaging::{codec, evidence_outbox::Record, evidence_protocol};
use rmpv::Value;
use std::sync::Arc;
fn wire(fixture: &serde_json::Value, key: &str) -> Vec<u8> {
    STANDARD.decode(fixture[key].as_str().unwrap()).unwrap()
}
#[test]
fn exact_csharp_external_storage_protocol_and_every_truncation() {
    let fixture: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/EvidenceStorageBridge/protocol.json"
    ))
    .unwrap();
    let request = wire(&fixture, "request");
    let value = rmpv::decode::read_value(&mut request.as_slice()).unwrap();
    let fields = value.as_array().unwrap();
    let record = Record {
        contract: "ES-fixture".into(),
        value_date: 739915,
        generation: [1; 16],
        identity: fields[1].as_str().unwrap().into(),
        digest: fields[2].as_str().unwrap().into(),
        bytes: Arc::from(fields[3].as_slice().unwrap()),
        receipt: fields[0].as_u64().unwrap(),
        submitted: tokio::time::Instant::now(),
    };
    assert_eq!(evidence_protocol::request(&record).unwrap(), request);
    let receipt = wire(&fixture, "receipt");
    let actual = evidence_protocol::decode_receipt(&receipt).unwrap();
    assert_eq!(actual.receipt, record.receipt);
    assert_eq!(actual.identity, record.identity);
    assert_eq!(actual.digest, record.digest);
    assert!(actual.persisted);
    assert!(actual.error.is_none());
    let negative = evidence_protocol::decode_receipt(&wire(&fixture, "negative")).unwrap();
    assert!(!negative.persisted);
    assert_eq!(
        negative.error.as_deref(),
        Some("Durable option evidence persistence failed.")
    );
    for size in 0..receipt.len() {
        assert!(
            evidence_protocol::decode_receipt(&receipt[..size]).is_err(),
            "truncated at {size}"
        );
    }
    let mut trailing = receipt.clone();
    trailing.push(0xc0);
    assert!(evidence_protocol::decode_receipt(&trailing).is_err());
    let valid = rmpv::decode::read_value(&mut receipt.as_slice()).unwrap();
    let valid = valid.as_array().unwrap();
    for (key, bad) in [
        (0, Value::from(0)),
        (0, Value::from(-1)),
        (1, Value::from("wrong")),
        (2, Value::from("g".repeat(64))),
        (3, Value::Nil),
        (4, Value::from("unexpected success error")),
        (4, Value::from("x".repeat(4097))),
    ] {
        let mut mutated = valid.clone();
        mutated[key] = bad;
        let bytes = codec::encode(&Value::Array(mutated)).unwrap();
        assert!(evidence_protocol::decode_receipt(&bytes).is_err());
    }
    for bytes in [
        vec![0xdd, 0xff, 0xff, 0xff, 0xff],
        vec![0x95, 1, 0xdb, 0xff, 0xff, 0xff, 0xff],
        vec![0x95, 1, 0xa1, 0xff],
        vec![0; 8193],
    ] {
        assert!(evidence_protocol::decode_receipt(&bytes).is_err());
    }
}
