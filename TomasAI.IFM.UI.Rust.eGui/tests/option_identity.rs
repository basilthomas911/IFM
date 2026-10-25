use ifm_ui_egui::backend::identity::{decimal_to_legacy_double, option_contract_id, option_strike};
#[test]
fn option_identity_matches_csharp_without_rounding_strikes_or_changing_scale() {
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests/fixtures/backend/option-identity.json");
    let cases: serde_json::Value = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    let expiry = chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap();
    for case in cases.as_array().unwrap() {
        let text = case["text"].as_str().unwrap();
        let (_, payload) = option_strike(text).unwrap();
        assert_eq!(payload.as_str(), case["payload"]);
        assert_eq!(
            option_contract_id("ES", expiry, "Put", text).unwrap(),
            case["id"]
        );
        assert_eq!(
            decimal_to_legacy_double(payload.as_str()).unwrap(),
            case["legacy"].as_f64().unwrap()
        );
    }
    for text in [
        "0",
        "-1",
        "+1",
        ".25",
        "1.",
        "1e3",
        "79228162514264337593543950336",
        "0.00000000000000000000000000001",
        "1.00000000000000000000000000001",
    ] {
        assert!(option_strike(text).is_err(), "{text}");
    }
    assert!(option_contract_id("ES.*", expiry, "Put", "6500").is_err());
}
#[test]
fn legacy_conversion_matches_compiled_dotnet_bit_for_bit_across_all_decimal_scales() {
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests/fixtures/backend/decimal-legacy-double.json");
    let cases: serde_json::Value = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    for case in cases.as_array().unwrap() {
        let text = case["text"].as_str().unwrap();
        assert_eq!(
            decimal_to_legacy_double(text).unwrap().to_bits(),
            case["bits"].as_u64().unwrap(),
            "{text}"
        );
    }
}
