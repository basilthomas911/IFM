//! Exercise the same typed decode used by copied screens, not a JSON-to-JSON
//! roundtrip that can conceal serde_json's private Number representation.
use ifm_ui_egui::{backend::codec, wire};
use serde_json::json;
#[test]
fn native_documents_use_standard_numeric_markers_and_decode_as_screen_models() {
    let bytes=wire::encode_value(&json!([{"Path":"/DefaultTradingDays/Daily","Value":"2","Kind":4,"Group":"Default Trading Days","Name":"Daily"}])).unwrap();
    let fields: Vec<ifm_ui_egui::parameter_sets::Field> = wire::decode(&bytes).unwrap();
    assert_eq!(fields[0].kind, 4);
    assert_eq!(fields[0].value, "2");
    let raw = codec::decode(&bytes).unwrap();
    let map = raw.as_array().unwrap()[0].as_map().unwrap();
    assert_eq!(
        map.iter()
            .find(|(k, _)| k.as_str() == Some("Kind"))
            .unwrap()
            .1
            .as_i64(),
        Some(4)
    );
    let bytes = wire::encode_value(
        &json!({"revision":7,"count":u64::MAX,"value":"79228162514264337593543950335","ratio":0.5}),
    )
    .unwrap();
    #[derive(serde::Deserialize)]
    struct Model {
        revision: i64,
        count: u64,
        value: String,
        ratio: f64,
    }
    let decoded: Model = wire::decode(&bytes).unwrap();
    assert_eq!(decoded.revision, 7);
    assert_eq!(decoded.count, u64::MAX);
    assert_eq!(decoded.value, "79228162514264337593543950335");
    assert_eq!(decoded.ratio, 0.5);
}
#[test]
fn dotnet_timestamp_display_has_seven_digits_without_unsupported_chrono_format() {
    use ifm_ui_egui::backend::contracts::utc_roundtrip;
    for text in [
        "0001-01-01T00:00:00.0000000Z",
        "2026-10-26T12:30:00.1234567Z",
        "9999-12-31T23:59:59.9999999Z",
    ] {
        let at = chrono::DateTime::parse_from_rfc3339(text).unwrap().to_utc();
        assert_eq!(utc_roundtrip(at), text);
    }
}
