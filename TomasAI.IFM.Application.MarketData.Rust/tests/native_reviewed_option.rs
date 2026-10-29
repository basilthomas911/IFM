//! Raw source and derived convention are compared with unchanged CLR rules.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{messaging::codec, reference::option_contract::OptionContract};
#[test]
fn every_raw_field_and_review_failure_matches_existing_contract() {
    let cases: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    assert!(cases.as_array().unwrap().len() > 80);
    for case in cases.as_array().unwrap() {
        let source = OptionContract::from_command(&case["command"]).unwrap();
        assert_eq!(
            codec::encode(&source.value()).unwrap(),
            STANDARD.decode(case["content"].as_str().unwrap()).unwrap(),
            "raw wire {}",
            case["name"]
        );
        let errors = source.errors();
        let expected = case["errors"]
            .as_array()
            .unwrap()
            .iter()
            .map(|v| v.as_str().unwrap())
            .collect::<Vec<_>>();
        assert_eq!(errors, expected, "raw qualification {}", case["name"]);
        if let Some(expected) = case["convention"].as_str() {
            assert_eq!(
                codec::encode(&source.convention().unwrap().value()).unwrap(),
                STANDARD.decode(expected).unwrap(),
                "derived convention {}",
                case["name"]
            );
        } else {
            assert!(
                source.convention().is_err(),
                "unqualified convention {}",
                case["name"]
            );
        }
    }
}

#[test]
fn raw_command_shapes_and_numeric_boundaries_fail_before_admission() {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/reviewed-options.json"
    ))
    .unwrap();
    let original = &fixtures[0]["command"];
    for length in 0..45 {
        let value = serde_json::Value::Array(original.as_array().unwrap()[..length].to_vec());
        assert!(OptionContract::from_command(&value).is_err());
    }
    for index in 0..45 {
        let mut value = original.clone();
        value[index] = serde_json::json!({});
        assert!(
            OptionContract::from_command(&value).is_err(),
            "field {index}"
        );
    }
    for index in [8, 12, 13, 24, 34, 35, 36, 37, 40, 42] {
        let mut value = original.clone();
        value[index] = serde_json::Value::Null;
        assert!(
            OptionContract::from_command(&value).is_err(),
            "required scalar {index}"
        );
    }
    for (index, bad) in [
        (8, serde_json::json!(-1)),
        (8, serde_json::json!(3652059)),
        (15, serde_json::json!(65536)),
        (44, serde_json::json!(65536)),
        (16, serde_json::json!(4294967296u64)),
        (43, serde_json::json!(-1)),
        (11, serde_json::json!("1e999999999")),
        (9, serde_json::json!("z".repeat(16))),
        (18, serde_json::json!([0, 841])),
        (18, serde_json::json!([2534023007999999999i64, 60])),
        (18, serde_json::json!([-621355968000000000i64, -60])),
    ] {
        let mut value = original.clone();
        value[index] = bad;
        assert!(
            OptionContract::from_command(&value).is_err(),
            "boundary field {index}"
        );
    }
    let mut value = original.clone();
    value[1] = serde_json::json!("x".repeat(16385));
    assert!(OptionContract::from_command(&value).is_err());
    let mut value = original.clone();
    for index in [0, 1, 2, 3] {
        value[index] = serde_json::json!("x".repeat(16000));
    }
    assert!(OptionContract::from_command(&value).is_err());
}
