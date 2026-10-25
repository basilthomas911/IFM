use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::parameter_fields::{self, ParameterField},
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
fn json(name: &str) -> Value {
    serde_json::from_slice(&read(name)).unwrap()
}
#[test]
fn all_eight_typed_parameter_variants_preserve_hybrid_fields_and_leaf_edits() {
    let registry = Registry::compiled().unwrap();
    let cases = json("parameter-fields-cases.json");
    assert_eq!(cases.as_array().unwrap().len(), 8);
    for case in cases.as_array().unwrap() {
        let name = case["name"].as_str().unwrap();
        let wire = codec::decode(&read(&format!("{name}.msgpack"))).unwrap();
        assert_eq!(
            serde_json::to_value(parameter_fields::read(&registry, &wire, false).unwrap()).unwrap(),
            json(&format!("{name}-fields.json")),
            "{name}"
        );
        assert_eq!(
            serde_json::to_value(parameter_fields::read(&registry, &wire, true).unwrap()).unwrap(),
            json(&format!("{name}-all-fields.json")),
            "{name} include identity"
        );
        assert_eq!(
            Value::String(parameter_fields::display(&registry, &wire).unwrap()),
            json(&format!("{name}-display.json")),
            "{name} display"
        );
        assert_eq!(
            parameter_fields::apply(&registry, &wire, &[]).unwrap(),
            wire
        );
        if case["hasChange"] == true {
            let change: ParameterField =
                serde_json::from_slice(&read(&format!("{name}-change.json"))).unwrap();
            let changed =
                parameter_fields::apply(&registry, &wire, std::slice::from_ref(&change)).unwrap();
            assert_eq!(
                changed,
                codec::decode(&read(&format!("{name}-changed.msgpack"))).unwrap()
            );
            assert!(
                parameter_fields::apply(&registry, &wire, &[change.clone(), change.clone()])
                    .is_err()
            );
            let mut bad = change;
            bad.path = "/ParameterSetId".into();
            assert!(parameter_fields::apply(&registry, &wire, &[bad]).is_err());
        }
    }
}
