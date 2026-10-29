//! Complete caller-controlled reference recipes survive native reconstruction.
use base64::Engine;
use ifm_market_data_native::reference::inputs::Inputs;
use serde_json::{Value, json};
#[test]
fn every_reviewed_calendar_matches_existing_clr_context_exactly() {
    let fixtures: Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    for input in fixtures.as_array().unwrap() {
        let recipe = Inputs::from_value(&input["raw"]).unwrap();
        let bytes = base64::engine::general_purpose::STANDARD
            .decode(input["context"].as_str().unwrap())
            .unwrap();
        let context = rmpv::decode::read_value(&mut bytes.as_slice()).unwrap();
        let fields = context.as_array().unwrap();
        assert_eq!(
            recipe.calendar.value(),
            fields[1],
            "calendar {}",
            input["name"]
        );
        assert_eq!(
            recipe.policy.publications.version(),
            fields[8].as_str().unwrap()
        );
        let mut other = input["raw"].clone();
        other["options"] = json!([]);
        other["statistics"] = json!([]);
        assert_eq!(
            recipe.signature,
            Inputs::from_value(&other).unwrap().signature,
            "Unrelated candidates cannot change reference recipe identity"
        );
    }
}
#[test]
fn malformed_non_utc_unordered_and_capacity_inputs_are_rejected() {
    let fixtures: Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let source = &fixtures[0]["raw"];
    for key in ["calendar", "publication", "conversion", "maximum_lead"] {
        let mut bad = source.clone();
        bad[key] = Value::Null;
        assert!(Inputs::from_value(&bad).is_err(), "{key}");
    }
    let mut bad = source.clone();
    bad["calendar"]["dates"] = json!([739900, 739900]);
    assert!(Inputs::from_value(&bad).is_err());
    let mut bad = source.clone();
    bad["calendar"]["rollover"] = json!(864_000_000_000u64);
    assert!(Inputs::from_value(&bad).is_err());
    let mut bad = source.clone();
    bad["calendar"]["dates"] = Value::Array(vec![json!(739900); 2001]);
    assert!(Inputs::from_value(&bad).is_err());
    for key in ["from_offset", "until_offset"] {
        let mut bad = source.clone();
        bad["publication"][key] = json!(60);
        assert!(Inputs::from_value(&bad).is_err());
    }
    let mut bad = source.clone();
    bad["publication"]["entries"][0]["offset"] = json!(60);
    assert!(Inputs::from_value(&bad).is_err());
    let mut bad = source.clone();
    bad["maximum_lead"] = json!(2001);
    assert!(Inputs::from_value(&bad).is_err());
    let mut bad = source.clone();
    bad["calendar"]["timezone"] = json!("Unknown/TimeZone");
    assert!(Inputs::from_value(&bad).is_err());
}
