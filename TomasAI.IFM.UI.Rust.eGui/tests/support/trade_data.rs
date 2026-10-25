use super::*;
use ifm_ui_egui::{backend::schema::Registry, state::trade_data::TradeData};
use serde_json::json;
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
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_workspace_reads_keep_atomic_scope_and_current_fund_mandates() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(2),
    })
    .await
    .unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = TradeData::default();
    mode(&backend, "Normal").await;
    state
        .load(
            &backend,
            &registry,
            tokio::time::Instant::now() + Duration::from_secs(10),
        )
        .await
        .unwrap();
    assert_eq!(
        (state.portfolio, state.fund, state.order, state.trade),
        (Some(1), Some(2), Some(123), Some(456))
    );
    assert_eq!(
        state.context.as_ref().unwrap().value_date.to_string(),
        "2026-10-26"
    );
    assert_eq!(state.from.unwrap().to_string(), "2026-10-01");
    assert_eq!(state.through.unwrap().to_string(), "2026-10-31");
    assert_eq!(state.orders.len(), 1);
    assert_eq!(state.trades.len(), 1);
    let original = state.clone();
    for failure in [
        "RejectOrders",
        "WrongScope",
        "WrongTradeScope",
        "Duplicate",
        "RepeatCursor",
    ] {
        mode(&backend, failure).await;
        assert!(
            state
                .load(
                    &backend,
                    &registry,
                    tokio::time::Instant::now() + Duration::from_secs(10)
                )
                .await
                .is_err(),
            "{failure}"
        );
        assert_eq!(state.orders, original.orders, "{failure}");
        assert_eq!(state.trades, original.trades, "{failure}");
        assert_eq!(
            (state.portfolio, state.fund, state.order, state.trade),
            (Some(1), Some(2), Some(123), Some(456)),
            "{failure}"
        );
    }
    mode(&backend, "LatestInactive").await;
    state
        .load(
            &backend,
            &registry,
            tokio::time::Instant::now() + Duration::from_secs(10),
        )
        .await
        .unwrap();
    assert!(
        state.funds.is_empty(),
        "An older Active mandate must not revive a newer Paused mandate"
    );
    assert_eq!((state.fund, state.order, state.trade), (None, None, None));
    mode(&backend, "Normal").await;
    state
        .load(
            &backend,
            &registry,
            tokio::time::Instant::now() + Duration::from_secs(10),
        )
        .await
        .unwrap();
    assert!(
        state
            .select_portfolio(
                &backend,
                &registry,
                999,
                tokio::time::Instant::now() + Duration::from_secs(2)
            )
            .await
            .is_err()
    );
    assert!(
        state
            .select_fund(
                &backend,
                &registry,
                999,
                tokio::time::Instant::now() + Duration::from_secs(2)
            )
            .await
            .is_err()
    );
    assert!(
        state
            .select_order(
                &backend,
                &registry,
                999,
                tokio::time::Instant::now() + Duration::from_secs(2)
            )
            .await
            .is_err()
    );
    mode(&backend, "Disabled").await;
}
#[tokio::test]
#[ignore = "Requires isolated C# compatibility responder"]
async fn trade_workspace_publication_matches_hybrid_and_fences_stale_choices() {
    let backend = Backend::connect(Settings {
        url: std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),
        credentials_file: None,
        token: None,
        request_timeout: Duration::from_secs(3),
    })
    .await
    .unwrap();
    mode(&backend, "Normal").await;
    let registry = Registry::compiled().unwrap();
    let mut state = ifm_ui_egui::state::trades::TradeController::default();
    let request = |generation: u64, action: &str, fields: serde_json::Value| json!({"schema":1,"requestId":uuid::Uuid::new_v4().to_string(),"view":"trade-orders","intent":action,"expectedGeneration":generation,"originalId":null,"fields":fields});
    let load = request(0, "load", json!({}));
    let first = state.execute(&backend, &registry, &load).await.unwrap();
    assert_eq!(first.outcomes[0].state, "Completed", "{}", first.status);
    let fixture = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests/fixtures/backend/trade-workspace-session.msgpack");
    let managed: ifm_ui_egui::contract_editor::ContractResult =
        rmp_serde::from_slice(&std::fs::read(fixture).unwrap()).unwrap();
    assert_eq!(first.result.choices, managed.choices);
    let mut context = first.result.context.clone();
    assert_eq!(
        context.remove("asOfUtc"),
        Some("2026-10-26T12:30:00.0000000Z".into())
    );
    assert_eq!(context, managed.context);
    assert_eq!(first.result.rows.len(), managed.rows.len());
    for (native, source) in first.result.rows.iter().zip(&managed.rows) {
        assert_eq!(native.id, source.id);
        assert_eq!(native.fields, source.fields);
        assert_eq!(
            ifm_ui_egui::wire::decode::<serde_json::Value>(native.data.as_ref().unwrap()).unwrap(),
            ifm_ui_egui::wire::decode::<serde_json::Value>(source.data.as_ref().unwrap()).unwrap()
        );
    }
    let stale = state
        .execute(
            &backend,
            &registry,
            &request(0, "fund", json!({"index":"0"})),
        )
        .await
        .unwrap();
    assert_eq!(stale.outcomes[0].state, "Failed");
    assert_eq!(state.data.order, Some(123));
    let repeated = state.execute(&backend, &registry, &load).await.unwrap();
    assert_eq!(
        repeated.outcomes[0].request_id,
        first.outcomes[0].request_id
    );
    assert_eq!(repeated.outcomes[0].state, first.outcomes[0].state);
    assert_eq!(repeated.outcomes[0].message, first.outcomes[0].message);
    let mut collision = load.clone();
    collision["fields"] = json!({"index":"1"});
    assert!(
        state
            .execute(&backend, &registry, &collision)
            .await
            .is_err()
    );
    mode(&backend, "Disabled").await;
}
