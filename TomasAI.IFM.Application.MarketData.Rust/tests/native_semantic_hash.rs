//! Independent production C# canonical-byte/hash qualification across scalar edges.
use ifm_market_data_native::{
    option_chain::qualification::Decimal,
    reference::semantic::{self, Value},
};
fn json(value: &serde_json::Value) -> Value {
    match value {
        serde_json::Value::Null => Value::Null,
        serde_json::Value::Bool(v) => Value::Bool(*v),
        serde_json::Value::String(v) => Value::String(v.clone()),
        serde_json::Value::Number(v) => {
            if let Some(v) = v.as_i64() {
                Value::Integer(v)
            } else if let Some(v) = v.as_u64() {
                Value::Unsigned(v)
            } else {
                Value::Double(v.as_f64().unwrap())
            }
        }
        serde_json::Value::Array(v) => Value::Array(v.iter().map(json).collect()),
        serde_json::Value::Object(v) => {
            Value::Object(v.iter().map(|(k, v)| (k.clone(), json(v))).collect())
        }
    }
}
#[test]
fn every_csharp_scalar_byte_and_digest_fixture_matches() {
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/SemanticHashBridge/hashes.json"
    ))
    .unwrap();
    for (index, row) in rows.as_array().unwrap().iter().enumerate() {
        let value = match row["kind"].as_str().unwrap() {
            "double" => Value::Double(f64::from_bits(row["bits"].as_u64().unwrap())),
            "decimal" => Value::Decimal(Decimal {
                bits: std::array::from_fn(|i| row["bits"][i].as_u64().unwrap() as u32),
            }),
            "string" => Value::String(row["text"].as_str().unwrap().into()),
            "object" => json(&row["input"]),
            _ => panic!("Unknown scalar oracle"),
        };
        let actual = semantic::canonical(&value).unwrap();
        assert_eq!(
            String::from_utf8(actual).unwrap(),
            row["json"].as_str().unwrap(),
            "fixture {index} {}",
            row["kind"]
        );
        assert_eq!(
            semantic::compute(&value).unwrap(),
            row["digest"].as_str().unwrap(),
            "hash fixture {index}"
        );
    }
    assert_eq!(rows.as_array().unwrap().len(), 5075);
}
#[test]
fn invalid_and_unbounded_inputs_never_return_a_valid_digest() {
    for value in [f64::NAN, f64::INFINITY, f64::NEG_INFINITY] {
        assert!(semantic::compute(&Value::Double(value)).is_err());
    }
    assert!(
        semantic::compute(&Value::Decimal(Decimal {
            bits: [1, 0, 0, 29 << 16]
        }))
        .is_err()
    );
    assert!(
        semantic::compute(&Value::Object(vec![
            ("same".into(), Value::Null),
            ("same".into(), Value::Null)
        ]))
        .is_err()
    );
    assert!(semantic::compute(&Value::Array(vec![Value::Null; 65537])).is_err());
    let mut deep = Value::Null;
    for _ in 0..66 {
        deep = Value::Array(vec![deep]);
    }
    assert!(semantic::compute(&deep).is_err());
}

#[test]
fn treasury_decimal_division_and_continuous_rate_preserve_csharp_bits() {
    use ifm_market_data_native::reference::{decimal, treasury};
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/SemanticHashBridge/hashes.json"
    ))
    .unwrap();
    for (index, row) in rows
        .as_array()
        .unwrap()
        .iter()
        .enumerate()
        .filter(|(_, r)| r["kind"] == "decimal")
    {
        let value = Decimal {
            bits: std::array::from_fn(|i| row["bits"][i].as_u64().unwrap() as u32),
        };
        let half = decimal::to_double(decimal::divide_200(value).unwrap()).unwrap();
        assert_eq!(
            half.to_bits(),
            row["halfBits"].as_u64().unwrap(),
            "half fixture {index}"
        );
        let rate = treasury::continuous_rate(half);
        if let Some(expected) = row["rateBits"].as_u64() {
            assert_eq!(rate.unwrap().to_bits(), expected, "rate fixture {index}");
        } else {
            assert!(rate.is_err());
        }
    }
}
