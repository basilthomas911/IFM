//! Compare native preparation/presentation to unchanged hybrid projections.
use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    services::database_backup as api,
    state::database_backup::{configured_sets, operation_document, policy_document},
};
use serde_json::{Value, json};
fn read(name: &str) -> Vec<u8> {
    std::fs::read(
        std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("tests/fixtures/backend")
            .join(name),
    )
    .unwrap()
}
fn golden(name: &str) -> Value {
    serde_json::from_slice(&read(name)).unwrap()
}
#[test]
fn backup_projections_and_configured_sets_match_hybrid() {
    let registry = Registry::compiled().unwrap();
    assert_eq!(
        serde_json::to_value(
            operation_document(
                &registry,
                &codec::decode(&read("backup-operation.wire.msgpack")).unwrap()
            )
            .unwrap()
        )
        .unwrap(),
        golden("backup-operation-display.json")
    );
    let (policy, editor) = policy_document(
        &registry,
        &codec::decode(&read("backup-policy.wire.msgpack")).unwrap(),
    )
    .unwrap();
    assert_eq!(
        serde_json::to_value(policy).unwrap(),
        golden("backup-policy-display.json")
    );
    assert_eq!(
        serde_json::to_value(editor).unwrap(),
        json!({"rpoMinutes":"15","rtoMinutes":"90","maximumVerificationHours":"24"})
    );
    let setup = registry
        .project(
            &api::kind("ReadModels", "DatabaseBackupSetupReadModel"),
            &codec::decode(&read("backup-setup.wire.msgpack")).unwrap(),
        )
        .unwrap();
    let sets = configured_sets(
        "LocalWorkstation",
        vec![
            serde_json::from_value(json!({"id":"postgres-main","source":"LocalWorkstation","engines":["PostgreSql"],"enabled":true,"policyRevision":7})).unwrap(),
        ],
        &setup["BackupHostSettings"],
    );
    assert_eq!(json!(sets), golden("backup-configured-display.json"));
}
#[test]
fn typed_backup_projection_preserves_unknown_enums_absent_lineage_and_exact_offsets() {
    let registry = Registry::compiled().unwrap();
    let mut wire = codec::decode(&read("backup-operation.wire.msgpack")).unwrap();
    let rmpv::Value::Array(fields) = &mut wire else {
        panic!("Expected record")
    };
    fields[6] = rmpv::Value::from(654i64);
    fields[10] = rmpv::Value::Nil;
    fields[12] = rmpv::Value::Nil;
    fields[13] = rmpv::Value::from(987i64);
    let actual = serde_json::to_value(operation_document(&registry, &wire).unwrap()).unwrap();
    let mut expected = golden("backup-operation-display.json");
    expected["outcome"] = json!(654);
    expected["completedUtc"] = Value::Null;
    expected["engine"] = json!(987);
    expected["requestedMode"] = json!("Full");
    expected["resolvedMode"] = json!("None");
    assert_eq!(actual, expected);
    let rmpv::Value::Array(fields) = &mut wire else {
        panic!("Expected record")
    };
    fields[7] = "invalid progress".into();
    assert!(operation_document(&registry, &wire).is_err());
}
#[test]
fn backup_routes_and_commands_are_closed_and_preserve_clr_defaults() {
    use ifm_ui_egui::services::business_context::BusinessContext;
    let registry = Registry::compiled().unwrap();
    let context = BusinessContext {
        value_date: chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap(),
        active_value_date: None,
        as_of_utc: "2026-10-26T12:30:00Z".parse().unwrap(),
        revision: 1,
        market_state: 0,
        allow_development_emulator: false,
    };
    let id = uuid::Uuid::new_v4();
    assert!(api::query_verb("DropDatabaseQuery").is_err());
    assert!(api::command_route("ExecuteBackupRetentionPlanCommand").is_err());
    assert!(api::source(&registry, "None").is_err());
    let (subject, body) = api::command_body(
        &registry,
        &context,
        "RequestDatabaseBackupCommand",
        "LocalWorkstation",
        id,
        id,
        Default::default(),
    )
    .unwrap();
    assert_eq!(
        subject.route().unwrap(),
        format!(
            "Command.DatabaseBackupCommand.RequestBackup.{}",
            id.simple()
        )
    );
    let row = registry
        .project(
            &api::kind("Commands", "RequestDatabaseBackupCommand"),
            &body,
        )
        .unwrap();
    assert_eq!(row["PostEvents"], true);
    assert_eq!(row["ErrorCode"], 9101);
    assert_eq!(row["RouteTo"], "SystemAdminBoundedContext");
    assert_eq!(row["RequiredDestinations"], json!([]));
    assert_eq!(row["SafeReason"], "");
    assert_eq!(row["Request"]["CreatedUtc"], "2026-10-26T12:30:00Z");
    assert!(
        api::command_body(
            &registry,
            &context,
            "RequestDatabaseBackupCommand",
            "LocalWorkstation",
            uuid::Uuid::nil(),
            id,
            Default::default()
        )
        .is_err()
    );
}
