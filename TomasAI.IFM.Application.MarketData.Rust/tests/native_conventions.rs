//! Actual C# reference qualification and bit-exact UTC year fractions.
use base64::Engine;
use ifm_market_data_native::option_chain::convention::Convention;
#[test]
fn all_existing_csharp_convention_results_match() {
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextPreparationBridge/conventions.json"
    ))
    .unwrap();
    for row in rows.as_array().unwrap() {
        let bytes = base64::engine::general_purpose::STANDARD
            .decode(row["contract"].as_str().unwrap())
            .unwrap();
        let value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
        let convention = Convention::new(value).unwrap();
        let ticks = row["ticks"].as_array().expect("C# premium tick fixtures");
        assert_eq!(ticks.len(), 32);
        for tick in ticks {
            use ifm_market_data_native::{messaging::codec, option_chain::qualification::Decimal};
            let mut bits = [0; 4];
            for (target, value) in bits.iter_mut().zip(tick["price"].as_array().unwrap()) {
                *target = value.as_i64().unwrap() as u32;
            }
            let result = convention
                .premium_increment(Decimal { bits }, tick["combination"].as_bool().unwrap());
            if tick["result"].is_null() {
                assert!(result.is_err(), "{} premium {:?}", row["name"], bits);
            } else {
                let expected = base64::engine::general_purpose::STANDARD
                    .decode(tick["result"].as_str().unwrap())
                    .unwrap();
                assert_eq!(
                    codec::encode(&codec::decimal_bits(result.unwrap()).unwrap()).unwrap(),
                    expected,
                    "{} premium {:?}",
                    row["name"],
                    bits
                );
            }
        }
        let at = row["at"].as_i64().unwrap();
        let offset = row["offset"].as_i64().unwrap();
        let result = convention.qualify(at, offset);
        assert_eq!(
            result.as_ref().err().map(|e| e.code),
            row["code"].as_str(),
            "{}",
            row["name"]
        );
        assert_eq!(
            result.as_ref().err().map(|e| e.input),
            row["input"].as_str(),
            "{}",
            row["name"]
        );
        if let Some(expected) = row["yearFractionBits"].as_u64() {
            assert_eq!(
                convention.year_fraction(at).unwrap().to_bits(),
                expected,
                "{}",
                row["name"]
            );
        }
    }
    assert_eq!(rows.as_array().unwrap().len(), 84);
}
#[test]
fn malformed_convention_wire_cannot_admit_a_provider_scope() {
    use rmpv::Value;
    for value in [
        Value::Nil,
        Value::Array(vec![]),
        Value::Array(vec![Value::Nil; 30]),
    ] {
        assert!(Convention::new(value).is_err());
    }
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextPreparationBridge/conventions.json"
    ))
    .unwrap();
    let bytes = base64::engine::general_purpose::STANDARD
        .decode(rows[0]["contract"].as_str().unwrap())
        .unwrap();
    let value = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
    for key in [0, 3, 4, 12, 16, 17, 27] {
        let mut invalid = value.clone();
        let Value::Array(fields) = &mut invalid else {
            panic!("Expected oracle convention array");
        };
        fields[key] = Value::Binary(vec![0; 16]);
        assert!(Convention::new(invalid).is_err());
    }
}
