use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    state::scheduled_tasks::{run_document, schedule_input},
};
use serde_json::json;
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
fn run_receipts_match_unchanged_hybrid_without_recomputing_outcomes() {
    let registry = Registry::compiled().unwrap();
    let wire = codec::decode(&read("scheduled-run.wire.msgpack")).unwrap();
    let golden: serde_json::Value =
        serde_json::from_slice(&read("scheduled-run-display.json")).unwrap();
    assert_eq!(run_document(&registry, &wire).unwrap(), golden);
}
#[test]
fn schedule_draft_matches_typed_backend_contract_and_rejects_bad_input() {
    let registry = Registry::compiled().unwrap();
    let mut intent = json!({"fields":{"taskKey":"test-task","name":"Fixture task","environment":"Development","hostId":"development","oneTime":"False","expression":"0 1 17 ? * MON-FRI","timeZoneId":"America/New_York","startsAtUtc":"2026-10-13T08:30:00-04:00","endsAtUtc":"","maximumRuntimeSeconds":"1800","dispatchToleranceSeconds":"60","description":"Offline native scheduler"}});
    assert_eq!(
        schedule_input(&registry, &intent).unwrap(),
        codec::decode(&read("scheduled-schedule.wire.msgpack")).unwrap()
    );
    intent["fields"]["oneTime"] = json!("yes");
    assert!(schedule_input(&registry, &intent).is_err());
    intent["fields"]["oneTime"] = json!("true");
    intent["fields"]["maximumRuntimeSeconds"] = json!("2147483648");
    assert!(schedule_input(&registry, &intent).is_err());
    intent["fields"]["maximumRuntimeSeconds"] = json!("120");
    intent["fields"]["startsAtUtc"] = json!("2026-10-13T08:30:00");
    assert!(schedule_input(&registry, &intent).is_err());
}
