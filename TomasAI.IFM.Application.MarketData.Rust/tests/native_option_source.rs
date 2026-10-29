//! Byte and identity parity against independently executed existing C# DTOs.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    messaging::{codec, option_source::Source},
    option_chain::qualification::Decimal,
    reference::semantic,
};

#[test]
fn option_source_matches_existing_wire_and_idempotency_contract() {
    let cases: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/sources.json"
    ))
    .unwrap();
    for case in cases.as_array().unwrap() {
        let mut bits = [0; 4];
        for (out, number) in bits.iter_mut().zip(case["price"].as_array().unwrap()) {
            *out = number.as_i64().unwrap() as u32;
        }
        let revision = case["revision"].as_i64().unwrap();
        let source = Source {
            dataset: "GLBX.MDP3".into(),
            publisher: u16::MAX,
            instrument: u32::MAX,
            contract: "ES<\"&\\漢字".into(),
            date: time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap(),
            price: Decimal { bits },
            size: u32::MAX,
            sequence: i64::MAX,
            event: 1_793_102_400_123_456_789,
            received: 1_793_102_400_123_456_999 + revision,
            generation: if revision == 0 {
                [
                    0xcc, 0xdd, 0xee, 0xff, 0xaa, 0xbb, 0x88, 0x99, 0x77, 0x66, 0x55, 0x44, 0x33,
                    0x22, 0x11, 0,
                ]
            } else {
                [0; 16]
            },
            symbol: "ES option \u{1}".into(),
        };
        assert_eq!(
            codec::encode(&source.value().unwrap()).unwrap(),
            STANDARD.decode(case["wire"].as_str().unwrap()).unwrap()
        );
        assert_eq!(
            source.identity().unwrap(),
            case["identity"].as_str().unwrap()
        );
        assert_eq!(
            source.source_digest().unwrap(),
            case["sourceDigest"].as_str().unwrap()
        );
        assert_eq!(
            semantic::compute(&source.semantic().unwrap()).unwrap(),
            case["semantic"].as_str().unwrap()
        );
        let mut replay = source.clone();
        replay.received += 1;
        replay.generation = [5; 16];
        assert_eq!(source.identity(), replay.identity());
        assert_eq!(source.source_digest(), replay.source_digest());
        assert_ne!(
            semantic::compute(&source.semantic().unwrap()),
            semantic::compute(&replay.semantic().unwrap())
        );
        replay.size -= 1;
        assert_eq!(source.identity(), replay.identity());
        assert_ne!(source.source_digest(), replay.source_digest());
        replay.symbol = "x".repeat(4097);
        assert!(replay.value().is_err());
        assert!(replay.identity().is_err());
        replay.symbol.clear();
        replay.price.bits[3] = 29 << 16;
        assert!(replay.value().is_err());
        assert!(replay.source_digest().is_err());
    }
}
