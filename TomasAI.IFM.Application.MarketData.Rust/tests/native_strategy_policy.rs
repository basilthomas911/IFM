//! Complete native policy and prepared-cache provenance match current CLR DTOs.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    messaging::codec,
    option_chain::{
        composition::model::Snapshot,
        strategy::{Parameters, Ranking},
    },
    reference::semantic,
};
use std::sync::Arc;
fn wire(row: &serde_json::Value) -> Vec<u8> {
    STANDARD.decode(row["content"].as_str().unwrap()).unwrap()
}
fn array_mut(value: &mut rmpv::Value) -> &mut Vec<rmpv::Value> {
    if let rmpv::Value::Array(v) = value {
        v
    } else {
        panic!("array fixture")
    }
}
fn fixture() -> serde_json::Value {
    serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/strategy-policy.json"
    ))
    .unwrap()
}
#[test]
fn full_policy_wire_hash_semantics_and_all_snapshot_metadata_match() {
    let cases = fixture();
    assert_eq!(cases.as_array().unwrap().len(), 8);
    for case in cases.as_array().unwrap() {
        let bytes = wire(case);
        let value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
        let policy = Arc::new(Parameters::new(value).unwrap());
        let command = ifm_market_data_native::option_chain::strategy::from_command(
            &case["command"]["parameters"],
        )
        .unwrap();
        assert_eq!(command.value(), policy.value());
        assert_eq!(command.hash().unwrap(), policy.hash().unwrap());

        assert_eq!(codec::encode(&policy.value()).unwrap(), bytes);
        assert_eq!(policy.hash().unwrap(), case["hash"].as_str().unwrap());
        assert_eq!(
            semantic::compute(&policy.semantic().unwrap()).unwrap(),
            case["semantic"].as_str().unwrap()
        );
        for expected in case["snapshots"].as_array().unwrap() {
            let raw = wire(expected);
            let value = rmpv::decode::read_value(&mut raw.as_slice()).unwrap();
            let fields = value.as_array().unwrap();
            let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
            let mut snapshot = Snapshot {
                schema: 1,
                id: [
                    0x33, 0x22, 0x11, 0x00, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb, 0xcc,
                    0xdd, 0xee, 0xff,
                ],
                scope: "strategy-test".into(),
                token: "strategy-token".into(),
                horizon: "Weekly".into(),
                generation: [
                    0xcc, 0xdd, 0xee, 0xff, 0xaa, 0xbb, 0x88, 0x99, 0x77, 0x66, 0x55, 0x44, 0x33,
                    0x22, 0x11, 0x00,
                ],
                at: 17931024000000000,
                until: 17931024010000000,
                instruments: vec![],
                digest: String::new(),
                ranking: Some(Ranking {
                    parameters: policy.clone(),
                    direction: expected["direction"].as_u64().unwrap() as u8,
                    date,
                }),
            };
            snapshot.seal().unwrap();
            assert_eq!(snapshot.digest, expected["digest"].as_str().unwrap());
            assert_eq!(
                snapshot.value().unwrap(),
                value,
                "ranking metadata {}",
                fields[11]
            );
            assert_eq!(codec::encode(&snapshot.value().unwrap()).unwrap(), raw);
        }
    }
}
#[test]
fn invalid_complete_policy_is_rejected_before_ownership() {
    let cases = fixture();
    let original = rmpv::decode::read_value(&mut wire(&cases[0]).as_slice()).unwrap();
    for key in 0..13 {
        let mut value = original.clone();
        array_mut(&mut value)[key] = rmpv::Value::Nil;
        assert!(Parameters::new(value).is_err(), "missing policy key {key}");
    }
    for (key, number) in [
        (1, 0),
        (4, 0),
        (10, 3),
        (10, 2049),
        (11, 0),
        (11, 17),
        (12, 2),
    ] {
        let mut value = original.clone();
        array_mut(&mut value)[key] = number.into();
        assert!(Parameters::new(value).is_err());
    }
    for bias_key in 0..18 {
        let mut value = original.clone();
        array_mut(&mut array_mut(&mut array_mut(&mut value)[8])[0])[bias_key] = rmpv::Value::Nil;
        if bias_key == 14 {
            assert!(Parameters::new(value).is_ok());
        } else {
            assert!(
                Parameters::new(value).is_err(),
                "missing bias key {bias_key}"
            );
        }
    }
    let mut duplicate = original;
    let v = array_mut(&mut array_mut(&mut duplicate)[8]);
    v[1] = v[0].clone();
    assert!(Parameters::new(duplicate).is_err());
}
