use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::trades::TradeController};
use serde_json::{Value as Json, json};
async fn backend() -> Backend {
    Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap()
}
async fn mode(backend: &Backend, name: &str) {
    service_result(
        &backend
            .request(
                &ActorSubject::query("NativeCompatibility", "TradeWorkspaceMode", "none"),
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
                &ActorSubject::query("NativeCompatibility", "TradeWorkspaceCount", "none"),
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
fn request(generation: u64, action: &str, fields: Json, original: Option<&str>) -> Json {
    json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"trade-orders","intent":action,"expectedGeneration":generation,"originalId":original,"fields":fields})
}
async fn act(
    state: &mut TradeController,
    backend: &Backend,
    registry: &Registry,
    generation: u64,
    action: &str,
    fields: Json,
    original: Option<&str>,
) -> ifm_ui_egui::contract_editor::ContractPublication {
    state
        .execute(
            backend,
            registry,
            &request(generation, action, fields, original),
        )
        .await
        .unwrap()
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_manual_commands_use_backend_ids_versions_and_projection_receipts() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    let mut state = TradeController::default();
    mode(&backend, "ResetCommands").await;
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({}), None).await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "prepare-order",
        json!({}),
        None,
    )
    .await;
    let draft = p
        .result
        .rows
        .iter()
        .find(|row| row.fields.get("kind").is_some_and(|v| v == "order-draft"))
        .unwrap();
    let draft: Json = ifm_ui_egui::wire::decode(draft.data.as_ref().unwrap()).unwrap();
    assert_eq!(draft["orderDate"], "2026-10-26T08:30:00.0000000");
    assert_eq!(draft["orderId"], 0);
    let create = request(
        p.generation,
        "create-order",
        json!({"reference":"  Native manual order\n🙂  "}),
        None,
    );
    p = state.execute(&backend, &registry, &create).await.unwrap();
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(count(&backend).await, 1);
    assert_eq!(
        state.data.order,
        Some(123),
        "Preserve the existing selected order after creation, matching the current hybrid"
    );
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "prepare-trade",
        json!({}),
        Some("123"),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(p.result.context["preparedTradeId"], "201");
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "create-trade",
        json!({"tradeType":"FuturesOutright","baseContract":"forged","effectiveDate":"2026-10-26"}),
        Some("123"),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.trade, Some(201));
    assert_eq!(count(&backend).await, 2);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "remove-trade",
        json!({}),
        Some("123"),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert!(state.data.trades.is_empty());
    assert_eq!(count(&backend).await, 3);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "remove-order",
        json!({"reason":"Native deletion"}),
        Some("123"),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(count(&backend).await, 4);
    assert_eq!(state.data.order, Some(701));
    p = state.execute(&backend, &registry, &create).await.unwrap();
    assert_eq!(p.outcomes[0].state, "Completed");
    assert_eq!(
        count(&backend).await,
        4,
        "Replaying a completed request must not create another order"
    );
    mode(&backend, "Disabled").await;
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_manual_lost_acknowledgement_reconciles_without_another_command() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    let mut state = TradeController::default();
    mode(&backend, "ResetCommands").await;
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({}), None).await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "prepare-order",
        json!({}),
        None,
    )
    .await;
    mode(&backend, "LostAck").await;
    let create = request(
        p.generation,
        "create-order",
        json!({"reference":"Lost acknowledgement"}),
        None,
    );
    p = state.execute(&backend, &registry, &create).await.unwrap();
    assert_eq!(p.outcomes[0].state, "Unknown", "{}", p.status);
    assert_eq!(count(&backend).await, 1);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "prepare-order",
        json!({}),
        None,
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed");
    assert_eq!(count(&backend).await, 1);
    // Repeated UI retries must not fill the receipt cache with invented
    // uncertain writes and prevent the real operation from being reconciled.
    for _ in 0..260 {
        p = act(
            &mut state,
            &backend,
            &registry,
            p.generation,
            "prepare-order",
            json!({}),
            None,
        )
        .await;
        assert_eq!(p.outcomes[0].state, "Failed");
    }
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "load",
        json!({}),
        None,
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert!(p.outcomes.iter().any(|outcome| outcome.request_id
        == create["requestId"].as_str().unwrap()
        && outcome.state == "Completed"));
    p = state.execute(&backend, &registry, &create).await.unwrap();
    assert_eq!(p.outcomes[0].state, "Completed");
    assert_eq!(count(&backend).await, 1);
    mode(&backend, "Disabled").await;
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_manual_deletion_requires_not_found_and_not_an_unavailable_read() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    let mut state = TradeController::default();
    mode(&backend, "ResetCommands").await;
    let mut p = act(&mut state, &backend, &registry, 0, "load", json!({}), None).await;
    mode(&backend, "NoProjection").await;
    let remove = request(
        p.generation,
        "remove-order",
        json!({"reason":"Deferred deletion"}),
        Some("123"),
    );
    p = state.execute(&backend, &registry, &remove).await.unwrap();
    assert_eq!(p.outcomes[0].state, "Unknown", "{}", p.status);
    assert_eq!(count(&backend).await, 1);
    mode(&backend, "ReadUnavailable").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "load",
        json!({}),
        None,
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Failed", "{}", p.status);
    assert_eq!(state.data.order, Some(123));
    assert_eq!(count(&backend).await, 1);
    mode(&backend, "CommitPending").await;
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "load",
        json!({}),
        None,
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert!(state.data.orders.is_empty());
    assert_eq!(count(&backend).await, 1);
    p = state.execute(&backend, &registry, &remove).await.unwrap();
    assert_eq!(p.outcomes[0].state, "Completed");
    assert_eq!(count(&backend).await, 1);
    mode(&backend, "Disabled").await;
}
