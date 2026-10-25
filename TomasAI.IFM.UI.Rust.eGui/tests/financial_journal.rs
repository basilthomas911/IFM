use ifm_ui_egui::{
    backend::schema::Registry,
    persistence::{financial_journal as financial, journal::Journal},
};
use serde_json::Value;
#[test]
fn typed_financial_journals_preserve_requests_and_managed_store_compatibility() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR"));
    let fixtures = root.join("tests/fixtures/backend");
    let registry = Registry::compiled().unwrap();
    let read = |name: &str| -> Value {
        serde_json::from_slice(&std::fs::read(fixtures.join(name)).unwrap()).unwrap()
    };
    let prepared = read("financial-posting-pending.json");
    let committed = read("financial-posting-committed.json");
    let configuration = read("financial-configuration-pending.json");
    for (name, command, value) in [
        ("posting", "PostFundTransactionCommand", &prepared),
        ("committed", "PostFundTransactionCommand", &committed),
        ("configuration", "ConfigureLedgerCommand", &configuration),
    ] {
        financial::validate(&registry, command, value)
            .unwrap_or_else(|error| panic!("{name}: {error}"));
    }
    for (command, pending, fixture) in [
        (
            "PostFundTransactionCommand",
            &prepared,
            "financial-posting-retained.msgpack",
        ),
        (
            "ConfigureLedgerCommand",
            &configuration,
            "financial-configuration-retained.msgpack",
        ),
    ] {
        let native =
            ifm_ui_egui::persistence::financial_request::rehydrate(&registry, command, pending)
                .unwrap();
        let managed =
            ifm_ui_egui::backend::codec::decode(&std::fs::read(fixtures.join(fixture)).unwrap())
                .unwrap();
        assert_eq!(
            native, managed,
            "Retained {command} must preserve every serialized field"
        );
    }
    let id = uuid::Uuid::parse_str(prepared["Request"]["OperationId"].as_str().unwrap()).unwrap();
    let directory = root.join("target/cross-language/pending-postings");
    let journal = Journal::new(&directory);
    let check = |value: &Value| financial::validate(&registry, "PostFundTransactionCommand", value);
    let saved = journal.save(id, &prepared, &check, &|| false).unwrap();
    assert_eq!(saved["Request"], prepared["Request"]);
    let saved = journal.save(id, &committed, &check, &|| false).unwrap();
    assert_eq!(saved["Phase"], 2);
    let retained = journal.save(id, &prepared, &check, &|| false).unwrap();
    assert_eq!(retained["Phase"], 2);
    assert_eq!(
        journal.load(id, &check).unwrap().unwrap()["Request"],
        prepared["Request"]
    );
    let mut tampered = prepared.clone();
    tampered["Request"]["Body"]["Amount"] = serde_json::json!(101.25);
    assert!(check(&tampered).is_err());
    tampered["Request"]["InputSha256"] = serde_json::json!(
        financial::request_hash(
            &registry,
            "PostFundTransactionCommand",
            &tampered["Request"]
        )
        .unwrap()
    );
    assert!(
        journal
            .save(id, &tampered, &check, &|| false)
            .unwrap_err()
            .contains("another financial request")
    );
    let mut invalid_completion = committed.clone();
    invalid_completion["Completion"]["InputHash"] = serde_json::json!("different");
    assert!(
        check(&invalid_completion)
            .unwrap_err()
            .contains("completion")
    );
    let config_id =
        uuid::Uuid::parse_str(configuration["Request"]["OperationId"].as_str().unwrap()).unwrap();
    let configs = Journal::new(root.join("target/cross-language/pending-configurations"));
    let check = |value: &Value| financial::validate(&registry, "ConfigureLedgerCommand", value);
    let saved = configs
        .save(config_id, &configuration, &check, &|| false)
        .unwrap();
    assert_eq!(saved["Request"], configuration["Request"]);
    assert_eq!(saved["Phase"], 1);
}
