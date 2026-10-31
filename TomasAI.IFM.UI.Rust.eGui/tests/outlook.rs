use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::outlook::*,
};
use serde_json::Value;
use std::path::Path;
fn read(name: &str) -> Vec<u8> {
    std::fs::read(
        Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend")
            .join(name),
    )
    .unwrap()
}
#[test]
fn native_outlook_matches_unchanged_hybrid_cells_colors_warming_and_stale_status() {
    let cases: Value = serde_json::from_slice(&read("outlook-display.json")).unwrap();
    for case in cases.as_array().unwrap() {
        let name = case["name"].as_str().unwrap();
        let wire = codec::decode(&read(&format!("{name}.msgpack"))).unwrap();
        use ifm_ui_egui::models::{
            trade_contracts::MarketOutlookReadModel, wire_types::FromBackend,
        };
        let source = MarketOutlookReadModel::from_backend(&wire).unwrap();
        let now = chrono::DateTime::parse_from_rfc3339(case["now"].as_str().unwrap())
            .unwrap()
            .to_utc();
        let actual = project(&source, case["live"].as_bool().unwrap(), now).unwrap();
        let expected = &case["expected"];
        assert_eq!(actual.status, expected["status"], "{name}");
        assert_eq!(
            actual.cells.len(),
            expected["cells"].as_object().unwrap().len(),
            "{name}"
        );
        for (key, value) in &actual.cells {
            let expected = &expected["cells"][key];
            assert_eq!(value.value, expected["value"], "{name}: {key} value");
            assert_eq!(
                value.foreground, expected["foreground"],
                "{name}: {key} foreground"
            );
            assert_eq!(
                value.background, expected["background"],
                "{name}: {key} background"
            );
        }
    }
}
#[test]
fn exact_decimal_display_preserves_large_cents_and_dotnet_rounding() {
    let cases: Value = serde_json::from_slice(&read("decimal-display.json")).unwrap();
    for case in cases.as_array().unwrap() {
        let text = case["text"].as_str().unwrap();
        assert_eq!(
            decimal_fixed(text, 2, false).unwrap(),
            case["fixed2"],
            "{text}"
        );
        assert_eq!(
            decimal_fixed(text, 2, true).unwrap(),
            case["percent2"],
            "{text}"
        );
    }
    for text in ["NaN", "1e20", "79228162514264337593543950336"] {
        assert!(decimal_fixed(text, 2, false).is_err());
    }
}
#[test]
fn time_only_keeps_100ns_precision_without_inventing_a_date_or_time_zone() {
    let registry = Registry::compiled().unwrap();
    let wire = codec::decode(&read("time-only.wire.msgpack")).unwrap();
    assert_eq!(
        registry.project("System.TimeOnly", &wire).unwrap(),
        "12:30:00.1234567"
    );
    for value in [rmpv::Value::from(-1), rmpv::Value::from(864_000_000_000u64)] {
        assert!(registry.project("System.TimeOnly", &value).is_err());
    }
}
