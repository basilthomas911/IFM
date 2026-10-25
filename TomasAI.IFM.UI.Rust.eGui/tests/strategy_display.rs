use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::strategy_display,
};
use serde_json::Value;
fn read(name: &str) -> Vec<u8> {
    std::fs::read(
        std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend")
            .join(name),
    )
    .unwrap()
}
fn difference(path: &str, a: &Value, b: &Value) -> Option<String> {
    if a == b {
        return None;
    }
    match (a, b) {
        (Value::Object(a), Value::Object(b)) => {
            for key in a.keys().chain(b.keys()) {
                if let Some(d) = difference(
                    &format!("{path}/{key}"),
                    a.get(key).unwrap_or(&Value::Null),
                    b.get(key).unwrap_or(&Value::Null),
                ) {
                    return Some(d);
                }
            }
        }
        (Value::Array(a), Value::Array(b)) => {
            if a.len() != b.len() {
                return Some(format!("{path}: lengths {} != {}", a.len(), b.len()));
            }
            for (i, (a, b)) in a.iter().zip(b).enumerate() {
                if let Some(d) = difference(&format!("{path}/{i}"), a, b) {
                    return Some(d);
                }
            }
        }
        _ => {
            return Some(format!(
                "{path}: {} != {}",
                a.to_string().chars().take(200).collect::<String>(),
                b.to_string().chars().take(200).collect::<String>()
            ));
        }
    }
    None
}
#[test]
fn strategy_rows_and_complete_details_tree_match_unchanged_hybrid() {
    let registry = Registry::compiled().unwrap();
    let wire = codec::decode(&read("strategy-view.msgpack")).unwrap();
    let expected: Value = serde_json::from_slice(&read("strategy-row-display.json")).unwrap();
    let row = strategy_display::row(&registry, &wire, 1000).unwrap();
    assert!(
        difference("row", &row, &expected).is_none(),
        "{}",
        difference("row", &row, &expected).unwrap_or_default()
    );
    let expected: Value = serde_json::from_slice(&read("strategy-details-display.json")).unwrap();
    let result = strategy_display::details(&registry, &wire).unwrap();
    fn missing(value: &Value, items: &mut Vec<String>) {
        match value {
            Value::String(v) if v.contains("No native display getter") => items.push(v.clone()),
            Value::Array(v) => {
                for v in v {
                    missing(v, items)
                }
            }
            Value::Object(v) => {
                for v in v.values() {
                    missing(v, items)
                }
            }
            _ => {}
        }
    }
    let mut missing_getters = vec![];
    missing(&result, &mut missing_getters);
    assert!(missing_getters.is_empty(), "{}", missing_getters.join("\n"));
    for key in ["workflowId", "revision", "roots", "header"] {
        assert!(
            difference(key, &result[key], &expected[key]).is_none(),
            "{}",
            difference(key, &result[key], &expected[key]).unwrap_or_default()
        );
    }
    assert!(!result.to_string().contains("No native display getter"));
}
