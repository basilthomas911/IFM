//! Explicit Development acceptance for editing one existing manual draft.
use ifm_ui_egui::{
    backend::{schema::Registry, transport::{Backend, Settings}},
    services::trade_orders,
    state::trades::TradeController,
};
use serde_json::{Value, json};
use std::time::Duration;

/// Drives the same editor actions as the desktop and saves only the requested draft edit.
#[tokio::test]
#[ignore = "Mutates one explicitly identified draft in a local Development environment"]
async fn saved_draft_changes_contract_without_changing_identity() {
    let setting = |name: &str| std::env::var(name).unwrap_or_else(|_| panic!("Set {name}"));
    assert_eq!(setting("DOTNET_ENVIRONMENT"), "Development");
    let url = setting("IFM_VERIFY_NATS_URL");
    assert_eq!(url, "nats://127.0.0.1:4222");
    let portfolio = setting("IFM_VERIFY_PORTFOLIO").parse::<i64>().unwrap();
    let fund = setting("IFM_VERIFY_FUND").parse::<i64>().unwrap();
    let order = setting("IFM_VERIFY_ORDER");
    let trade = setting("IFM_VERIFY_TRADE").parse::<i64>().unwrap();
    let original = setting("IFM_VERIFY_ORIGINAL_CONTRACT");
    let replacement = setting("IFM_VERIFY_REPLACEMENT_CONTRACT");
    let backend = Backend::connect(Settings {
        url, credentials_file: None, token: None, request_timeout: Duration::from_secs(30),
    }).await.unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = TradeController::default();
    let mut generation = 0;
    async fn act(
        state: &mut TradeController, backend: &Backend, registry: &Registry, generation: &mut u64,
        action: &str, fields: Value, order: Option<&str>,
    ) -> ifm_ui_egui::contract_editor::ContractPublication {
        let result = state.execute(backend, registry, &json!({
            "schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),
            "intent":action,"expectedGeneration":*generation,"fields":fields,"originalId":order,
        })).await.unwrap();
        *generation = result.generation;
        assert_eq!(result.outcomes[0].state, "Completed", "{action}: {}", result.outcomes[0].message);
        result
    }
    let index = |rows: &[rmpv::Value], kind, key: &str, id| rows.iter().position(|row|
        registry.project(&trade_orders::kind(kind), row).unwrap()[key].as_i64() == Some(id)
    ).unwrap().to_string();
    act(&mut state, &backend, &registry, &mut generation, "load", json!({}), None).await;
    let selected = index(&state.data.portfolios, "PortfolioReadModel", "PortfolioId", portfolio);
    act(&mut state, &backend, &registry, &mut generation, "portfolio", json!({"index":selected}), None).await;
    let selected = index(&state.data.funds, "FundMandateReadModel", "FundId", fund);
    act(&mut state, &backend, &registry, &mut generation, "fund", json!({"index":selected}), None).await;
    act(&mut state, &backend, &registry, &mut generation, "order", json!({}), Some(&order)).await;
    let selected = index(&state.data.trades, "FundOrderTradeProjectionReadModel", "TradeId", trade);
    let loaded = act(&mut state, &backend, &registry, &mut generation, "trade", json!({"index":selected}), Some(&order)).await;
    assert_eq!(loaded.result.context["canChangeTrade"], "True");
    let opened = act(&mut state, &backend, &registry, &mut generation, "prepare-change-trade",
        json!({"tradeId":trade.to_string()}), Some(&order)).await;
    let values = opened.result.context;
    assert_eq!(values["changeBaseContract"], original);
    assert!(opened.result.choices["baseContract"].contains(&replacement));
    let result = act(&mut state, &backend, &registry, &mut generation, "change-trade", json!({
        "tradeId":trade.to_string(), "baseContract":replacement,
        "tradeType":values["changeTradeType"], "tradeReference":values["changeTradeReference"],
        "effectiveDate":values["changeEffectiveDate"],
    }), Some(&order)).await;
    assert_eq!(result.result.context["tradeId"], trade.to_string());
    assert_eq!(result.result.context["changeBaseContract"], replacement);
    assert_eq!(result.result.context["tradeState"], "NewTrade");
    assert_eq!(result.result.context["changeTradeReference"], values["changeTradeReference"]);
    let reopened = act(&mut state, &backend, &registry, &mut generation, "prepare-change-trade",
        json!({"tradeId":trade.to_string()}), Some(&order)).await;
    assert_eq!(reopened.result.context["changeBaseContract"], replacement);
    println!("Trade {trade} kept its ID and NewTrade state after {original} changed to {replacement}.");
}
