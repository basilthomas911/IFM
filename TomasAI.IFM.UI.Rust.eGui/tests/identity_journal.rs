use ifm_ui_egui::{backend::identity::*, persistence::journal::Journal};
use serde_json::{Value, json};

#[test]
fn financial_canonical_hash_matches_compiled_dotnet_including_escaping_and_g29() {
    let cases: Value = serde_json::from_str(include_str!("fixtures/backend/hashes.json")).unwrap();
    for case in cases.as_array().unwrap() {
        assert_eq!(
            financial_hash(&case["json"]).unwrap(),
            case["hash"].as_str().unwrap(),
            "{}",
            canonical_json(&case["json"]).unwrap()
        );
    }
}

fn validate(value: &Value) -> Result<(), String> {
    if value["Request"]["CommandId"] != value["Request"]["OperationId"] {
        return Err("Identity mismatch".into());
    }
    if value["Request"]["InputSha256"] != financial_hash(&value["Request"]["Body"])? {
        return Err("Hash mismatch".into());
    }
    Ok(())
}

#[test]
fn uncertain_journal_survives_restart_and_terminal_phase_cannot_regress() {
    let root =
        std::env::temp_dir().join(format!("ifm-standalone-journal-{}", uuid::Uuid::new_v4()));
    let id = uuid::Uuid::new_v4();
    let body: Value =
        serde_json::from_str(r#"{"Amount":12345678901234567890.123456789,"Currency":"USD"}"#)
            .unwrap();
    let mut value = json!({ "Request": {"OperationId":id.to_string(),"CommandId":id.to_string(),"Body":body,"InputSha256":financial_hash(&body).unwrap(),"RequestedAtUtc":"2026-10-26T12:30:00Z"},"Phase":1,"Message":"Outcome unknown","Completion":null });
    let store = Journal::new(&root);
    assert_eq!(store.save(id, &value, &validate, &|| false).unwrap(), value);
    drop(store);
    let store = Journal::new(&root);
    assert_eq!(store.load(id, &validate).unwrap(), Some(value.clone()));
    assert_eq!(store.list(&validate).unwrap().len(), 1);
    value["Phase"] = 2.into();
    store.save(id, &value, &validate, &|| false).unwrap();
    let mut regressed = value.clone();
    regressed["Phase"] = 1.into();
    assert_eq!(
        store.save(id, &regressed, &validate, &|| false).unwrap()["Phase"],
        2
    );
    regressed["Request"]["Body"]["Currency"] = "CAD".into();
    regressed["Request"]["InputSha256"] = financial_hash(&regressed["Request"]["Body"])
        .unwrap()
        .into();
    assert!(
        store
            .save(id, &regressed, &validate, &|| false)
            .unwrap_err()
            .contains("another financial request")
    );
    assert!(store.save(id, &value, &validate, &|| true).is_err());
    // Remove only the files created by this test; never touch the user's journal.
    for file in std::fs::read_dir(&root).unwrap() {
        std::fs::remove_file(file.unwrap().path()).unwrap();
    }
    std::fs::remove_dir(root).unwrap();
}
