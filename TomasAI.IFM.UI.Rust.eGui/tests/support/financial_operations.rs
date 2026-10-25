use super::*;
use ifm_ui_egui::{
    backend::schema::Registry, persistence::financial_journal,
    services::financial_operations::OperationStore,
};
use serde_json::{Value as Json, json};
async fn mode(backend: &Backend, name: &str) {
    service_result(
        &backend
            .request(
                &ActorSubject::query("NativeCompatibility", "FinancialOperationMode", "none"),
                &name.into(),
                None,
                None,
            )
            .await
            .unwrap(),
    )
    .unwrap();
}
async fn count(backend: &Backend) -> i64 {
    service_result(
        &backend
            .request(
                &ActorSubject::query("NativeCompatibility", "FinancialOperationCount", "none"),
                &Value::Nil,
                None,
                None,
            )
            .await
            .unwrap(),
    )
    .unwrap()
    .as_array()
    .unwrap()[0]
        .as_i64()
        .unwrap()
}
fn prepared(registry: &Registry, configuration: bool) -> Json {
    let file = if configuration {
        "financial-configuration-pending.json"
    } else {
        "financial-posting-pending.json"
    };
    let mut pending: Json = serde_json::from_slice(
        &std::fs::read(
            std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
                .join("tests/fixtures/backend")
                .join(file),
        )
        .unwrap(),
    )
    .unwrap();
    let request = &mut pending["Request"];
    let id = uuid::Uuid::new_v4().to_string();
    for key in ["CommandId", "OperationId", "CorrelationId", "CausationId"] {
        request[key] = id.clone().into();
    }
    request["Subject"]["EntityId"] = "1".into();
    request["Access"]["Roles"] = json!(["LedgerRead", "LedgerPost", "LedgerConfigure"]);
    if configuration {
        request["Body"]["BookId"] = 11.into();
    }
    request["InputSha256"] = financial_journal::request_hash(
        registry,
        if configuration {
            "ConfigureLedgerCommand"
        } else {
            "PostFundTransactionCommand"
        },
        request,
    )
    .unwrap()
    .into();
    pending["Phase"] = 0.into();
    pending["Completion"] = Json::Null;
    pending
}
fn store(configuration: bool) -> OperationStore {
    OperationStore {
        directory: std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("target/cross-language/financial-operations")
            .join(format!(
                "{}-{}",
                if configuration {
                    "configuration"
                } else {
                    "posting"
                },
                uuid::Uuid::new_v4()
            )),
        configuration,
    }
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn financial_operations_reconcile_persisted_identity_before_any_dispatch() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let deadline = || tokio::time::Instant::now() + Duration::from_secs(2);
    mode(&backend, "Normal").await;
    for configuration in [false, true] {
        let store = store(configuration);
        let input = prepared(&registry, configuration);
        let original = input["Request"].clone();
        let id = uuid::Uuid::parse_str(original["OperationId"].as_str().unwrap()).unwrap();
        let before = count(&backend).await;
        let committed = store
            .submit(&backend, &registry, input, deadline())
            .await
            .unwrap();
        assert_eq!(committed["Phase"], 2, "{committed}");
        assert_eq!(committed["Request"], original);
        assert_eq!(count(&backend).await, before + 1);
        mode(&backend, "Unavailable").await;
        assert_eq!(
            store
                .recover(&backend, &registry, id, deadline())
                .await
                .unwrap()["Phase"],
            2
        );
        assert_eq!(count(&backend).await, before + 1);
        mode(&backend, "Normal").await;
    }
    let store = store(false);
    let input = prepared(&registry, false);
    let original = input["Request"].clone();
    let id = uuid::Uuid::parse_str(original["OperationId"].as_str().unwrap()).unwrap();
    mode(&backend, "LostAck").await;
    let before = count(&backend).await;
    let unknown = store
        .submit(&backend, &registry, input, deadline())
        .await
        .unwrap();
    assert_eq!(unknown["Phase"], 1);
    assert_eq!(unknown["Request"], original);
    assert_eq!(count(&backend).await, before + 1);
    mode(&backend, "Mismatch").await;
    assert!(
        store
            .recover(&backend, &registry, id, deadline())
            .await
            .unwrap_err()
            .starts_with("Integrity:")
    );
    assert_eq!(store.load(id).await.unwrap()["Phase"], 1);
    mode(&backend, "Normal").await;
    assert_eq!(
        store
            .recover(&backend, &registry, id, deadline())
            .await
            .unwrap()["Phase"],
        2
    );
    assert_eq!(count(&backend).await, before + 1);
    let expired = prepared(&registry, false);
    let expiry_store = self::store(false);
    let before = count(&backend).await;
    mode(&backend, "Expired").await;
    let expired = expiry_store
        .submit(&backend, &registry, expired, deadline())
        .await
        .unwrap();
    assert_eq!(expired["Phase"], 3);
    assert_eq!(count(&backend).await, before);
    let waiting = self::store(false);
    let input = prepared(&registry, false);
    let id = uuid::Uuid::parse_str(input["Request"]["OperationId"].as_str().unwrap()).unwrap();
    let original = input["Request"].clone();
    mode(&backend, "AckOnly").await;
    let before = count(&backend).await;
    let unknown = waiting
        .submit(&backend, &registry, input, deadline())
        .await
        .unwrap();
    assert_eq!(unknown["Phase"], 1);
    assert_eq!(count(&backend).await, before + 1);
    mode(&backend, "Unavailable").await;
    assert_eq!(
        waiting
            .recover(&backend, &registry, id, deadline())
            .await
            .unwrap()["Phase"],
        1
    );
    assert_eq!(count(&backend).await, before + 1);
    mode(&backend, "Normal").await;
    let committed = waiting
        .recover(&backend, &registry, id, deadline())
        .await
        .unwrap();
    assert_eq!(committed["Phase"], 2);
    assert_eq!(committed["Request"], original);
    assert_eq!(count(&backend).await, before + 2);
}
