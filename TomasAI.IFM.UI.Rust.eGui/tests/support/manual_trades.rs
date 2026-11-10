use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::trades::TradeController};
use serde_json::{Value as Json, json};

/// Creating an order selects its empty trade collection before adding the opening.
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_manual_create_then_add_opening_targets_the_new_order_for_each_family() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    for kind in ["ShortIronCondor", "CallDebitSpread", "FuturesOutright"] {
        mode(&backend, "ResetCommands").await;
        let mut state = TradeController::default();
        let mut result = act(&mut state, &backend, &registry, 0, "load", json!({}), None).await;
        assert_eq!(state.data.order, Some(123));
        result = act(&mut state, &backend, &registry, result.generation, "prepare-order", json!({}), None).await;
        result = act(&mut state, &backend, &registry, result.generation, "create-order", json!({"reference":kind}), None).await;
        assert_eq!(result.outcomes[0].state, "Completed", "{}", result.status);
        assert_eq!(state.data.order, Some(701));
        assert!(state.data.trades.is_empty());
        result = act(&mut state, &backend, &registry, result.generation, "prepare-trade", json!({}), Some("701")).await;
        assert_eq!(result.outcomes[0].state, "Completed", "{}", result.status);
        result = act(&mut state, &backend, &registry, result.generation, "create-trade",
            json!({"tradeType":kind,"baseContract":"ESZ6","effectiveDate":"2026-10-26","tradeReference":"Buy saved trade — edited reference"}), Some("701")).await;
        assert_eq!(result.outcomes[0].state, "Completed", "{}", result.status);
        assert_eq!(state.data.order, Some(701));
        assert_eq!(state.data.trades.len(), 1);
        let row = registry.project("TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel", &state.data.trades[0]).unwrap();
        assert_eq!(row["OrderId"], 701);
        assert_eq!(row["TradeType"], kind);
        assert_eq!(row["PrimaryTrade"], true);
        assert_eq!(row["TradeState"], "NewTrade");
        assert_eq!(count(&backend).await, 2);
    }
    mode(&backend, "Disabled").await;
}

/// A stale empty UI cache cannot turn an existing opening draft into another opening.
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_workspace_add_trade_refreshes_hidden_opening_before_allocating_or_writing() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    mode(&backend, "ResetCommands").await;
    mode(&backend, "Normal").await;
    let mut state = TradeController::default();
    let loaded = act(&mut state, &backend, &registry, 0, "load", json!({}), None).await;
    assert_eq!(loaded.result.context["canAddTrade"], "False");
    assert_eq!(state.data.trades.len(), 1);
    state.data.trades.clear(); // Reproduce the former default-selected-order cache.
    let result = act(&mut state, &backend, &registry, loaded.generation,
        "prepare-trade", json!({}), Some("123")).await;
    assert_eq!(result.outcomes[0].state, "Failed");
    assert!(result.outcomes[0].message.contains("already has an opening trade"));
    assert_eq!(state.data.trades.len(), 1);
    assert_eq!(count(&backend).await, 0);
    mode(&backend, "Disabled").await;
}

/// A dialog prepared for an empty order cannot silently become a closing trade.
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_workspace_add_trade_rejects_changed_opening_after_preparation() {
    let backend = backend().await;
    let registry = Registry::compiled().unwrap();
    mode(&backend, "ResetEmptyCommands").await;
    let mut state = TradeController::default();
    let loaded = act(&mut state, &backend, &registry, 0, "load", json!({}), None).await;
    let prepared = act(&mut state, &backend, &registry, loaded.generation,
        "prepare-trade", json!({}), Some("123")).await;
    assert_eq!(prepared.outcomes[0].state, "Completed");
    mode(&backend, "Normal").await; // Another client has supplied an opening.
    let result = act(&mut state, &backend, &registry, prepared.generation, "create-trade",
        json!({"tradeType":"ShortIronCondor","baseContract":"ESZ6","effectiveDate":"2026-10-26","tradeReference":"Opening"}), Some("123")).await;
    assert_eq!(result.outcomes[0].state, "Failed");
    assert!(result.outcomes[0].message.contains("opening trade changed"));
    assert_eq!(count(&backend).await, 0);
    mode(&backend, "Disabled").await;
}


/// Provides backend for the manual trades implementation.
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


/// Provides mode for the manual trades implementation.
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


/// Provides count for the manual trades implementation.
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


/// Provides request for the manual trades implementation.
fn request(generation: u64, action: &str, fields: Json, original: Option<&str>) -> Json {
    json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"trade-orders","intent":action,"expectedGeneration":generation,"originalId":original,"fields":fields})
}


/// Provides act for the manual trades implementation.
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


/// Verifies that trade manual commands use backend ids versions and projection receipts.
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
        Some(701),
        "Select the newly created empty order for its first opening trade"
    );
    assert!(state.data.trades.is_empty());
    // This scenario specifically tests a close against the earlier opening.
    p = act(&mut state, &backend, &registry, p.generation, "order", json!({}), Some("123")).await;
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
        json!({"tradeType":"FuturesOutright","baseContract":"forged","effectiveDate":"2026-10-26","tradeReference":"  Buy saved trade — edited reference  "}),
        Some("123"),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.trade, Some(201));
    let added = registry.project(
        "TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel",
        state.data.trades.iter().find(|wire| registry.project(
            "TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel", wire
        ).is_ok_and(|row| row["TradeId"] == 201)).unwrap()
    ).unwrap();
    assert_eq!(added["MaturityDate"], "2026-10-28");
    assert_eq!(added["TradeType"], "LongIronCondor");
    assert_eq!(added["TradeReference"], "Buy saved trade — edited reference");
    assert_eq!(added["InstructionReference"], "ESZ6 @ 20261026 - 20261028");
    assert_eq!(count(&backend).await, 2);
    p = act(
        &mut state,
        &backend,
        &registry,
        p.generation,
        "remove-trade",
        json!({"removeTradeId":"201"}),
        Some("123"),
    )
    .await;
    assert_eq!(p.outcomes[0].state, "Completed", "{}", p.status);
    assert_eq!(state.data.trades.len(), 1);
    let remaining = registry.project(
        "TomasAI.IFM.Domain.Portfolio.Shared.ViewModels.FundOrderTradeProjectionReadModel",
        &state.data.trades[0],
    ).unwrap();
    assert_eq!(remaining["TradeId"], 456);
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


/// Verifies that trade manual lost acknowledgement reconciles without another command.
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


/// Verifies that trade manual deletion requires not found and not an unavailable read.
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
