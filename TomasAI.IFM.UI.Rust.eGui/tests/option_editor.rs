use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    state::option_contract::{display_contract, draft},
};
use std::path::Path;
fn read(name: &str) -> Vec<u8> {
    std::fs::read(
        Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend")
            .join(name),
    )
    .unwrap()
}
fn wire(name: &str) -> rmpv::Value {
    codec::decode(&read(&format!("{name}.wire.msgpack"))).unwrap()
}
#[test]
fn option_drafts_match_hybrid_exact_strikes_and_preserve_provider_identity() {
    let registry = Registry::compiled().unwrap();
    let input: serde_json::Value =
        serde_json::from_slice(&read("option-draft-inputs.json")).unwrap();
    assert_eq!(
        draft(&registry, &input["legacy"], None, false).unwrap(),
        wire("option-legacy-draft")
    );
    assert_eq!(
        draft(
            &registry,
            &input["provider"],
            Some(&wire("option-provider")),
            false
        )
        .unwrap(),
        wire("option-provider-edited")
    );
    let row = display_contract(&registry, &wire("option-provider")).unwrap();
    assert_eq!(row.fields["strikePrice"], "6500.2500");
    let expected: serde_json::Value =
        serde_json::from_slice(&read("option-review-display.json")).unwrap();
    for (key, value) in expected.as_object().unwrap() {
        assert_eq!(
            row.fields.get(key).unwrap(),
            value.as_str().unwrap(),
            "{key}"
        );
    }
}
#[test]
fn existing_legacy_option_identity_cannot_be_edited_into_another_instrument() {
    let registry = Registry::compiled().unwrap();
    let mut input: serde_json::Value =
        serde_json::from_slice(&read("option-draft-inputs.json")).unwrap();
    let old = wire("option-legacy-draft");
    input["legacy"]["strikePrice"] = "6500.5".into();
    assert!(
        draft(&registry, &input["legacy"], Some(&old), false)
            .unwrap_err()
            .contains("identity cannot be changed")
    );
    input["legacy"]["strikePrice"] = "6500.25000".into();
    assert!(draft(&registry, &input["legacy"], Some(&old), false).is_ok());
    input["legacy"]["contractMonth"] = "2026-10-27".into();
    assert!(draft(&registry, &input["legacy"], Some(&old), false).is_err());
}
