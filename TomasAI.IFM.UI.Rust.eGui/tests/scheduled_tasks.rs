use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    state::scheduled_tasks::{run_document, schedule_input},
};
use serde_json::json;
use std::path::Path;

/// Catalog evidence uses appended command keys without moving existing fields.
#[test]
fn catalog_evidence_uses_the_compiled_command_wire_slots() {
    use rmpv::Value;
    use std::collections::BTreeMap;
    let registry = Registry::compiled().unwrap();
    let dashboard = codec::decode(&read("scheduled-dashboard.wire.msgpack")).unwrap();
    let evidence = registry.wire_field(
        "TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts.ScheduledTasksDashboard",
        "Catalog",
        &dashboard,
    ).unwrap().clone();
    let fields = BTreeMap::from([
        ("ExpectedRevision".into(), Value::from(42)),
        ("CatalogEvidence".into(), evidence.clone()),
    ]);
    for (name, slot) in [("EnableScheduledTaskCommand", 7), ("AdmitScheduledTaskRunCommand", 13)] {
        let command = registry.encode(
            &format!("TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Commands.{name}"),
            &fields,
        ).unwrap();
        let roundtrip = codec::decode(&codec::encode(&command).unwrap()).unwrap();
        let array = roundtrip.as_array().unwrap();
        assert_eq!(array.len(), slot + 1);
        assert_eq!(array[3], Value::from(42));
        assert_eq!(array[slot], evidence);
    }
}


/// Reads scheduled tasks for scheduled tasks.
fn read(name: &str) -> Vec<u8> {
    std::fs::read(
        Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend")
            .join(name),
    )
    .unwrap()
}


/// Verifies that run receipts match unchanged hybrid without recomputing outcomes.
#[test]
fn run_receipts_match_unchanged_hybrid_without_recomputing_outcomes() {
    let registry = Registry::compiled().unwrap();
    let wire = codec::decode(&read("scheduled-run.wire.msgpack")).unwrap();
    let golden: serde_json::Value =
        serde_json::from_slice(&read("scheduled-run-display.json")).unwrap();
    assert_eq!(
        serde_json::to_value(run_document(&registry, &wire).unwrap()).unwrap(),
        golden
    );
}


/// Verifies that schedule draft matches typed backend contract and rejects bad input.
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
