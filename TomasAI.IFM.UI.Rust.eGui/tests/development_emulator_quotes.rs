//! Opt-in, read-only verification of an existing Development closing setup.
use ifm_ui_egui::{backend::{schema::Registry, transport::*}, state::trades::TradeController};
use serde_json::{json, Value};
use std::time::Duration;

/// Exercises the real controller and backend preview without placing, changing, or cancelling an order.
#[tokio::test]
#[ignore = "Requires an existing Development emulator closing setup and explicit IFM_VERIFY_* configuration"]
async fn saved_close_uses_four_historical_quotes_and_publishes_an_order_price() {
    let setting = |key: &str| std::env::var(key).unwrap_or_else(|_| panic!("Set {key}"));
    let portfolio: i64 = setting("IFM_VERIFY_PORTFOLIO").parse().unwrap();
    let fund: i64 = setting("IFM_VERIFY_FUND").parse().unwrap();
    let order = setting("IFM_VERIFY_ORDER");
    let trade: i64 = setting("IFM_VERIFY_TRADE").parse().unwrap();
    let backend = Backend::connect(Settings {
        url: setting("IFM_VERIFY_NATS_URL"), credentials_file: None, token: None,
        request_timeout: Duration::from_secs(30),
    }).await.unwrap();
    let registry = Registry::compiled().unwrap();
    let mut state = TradeController::default();
    let mut generation = 0;
    async fn act(state: &mut TradeController, backend: &Backend, registry: &Registry,
        generation: &mut u64, action: &str, fields: Value, order: Option<&str>)
        -> ifm_ui_egui::contract_editor::ContractPublication {
        let result = state.execute(backend, registry, &json!({
            "schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),
            "intent":action,"expectedGeneration":*generation,"fields":fields,"originalId":order,
        })).await.unwrap();
        *generation = result.generation;
        assert!(result.outcomes.iter().all(|outcome| outcome.state == "Completed"),
            "{action}: {:?}", result.outcomes.iter().map(|outcome| &outcome.message).collect::<Vec<_>>());
        result
    }
    act(&mut state, &backend, &registry, &mut generation, "load", json!({}), None).await;
    let index = |rows: &[rmpv::Value], kind: &str, key: &str, id: i64| rows.iter().position(|row|
        registry.project(&ifm_ui_egui::services::trade_orders::kind(kind), row)
            .unwrap()[key].as_i64() == Some(id)).unwrap();
    let selected = index(&state.data.portfolios, "PortfolioReadModel", "PortfolioId", portfolio);
    act(&mut state, &backend, &registry, &mut generation, "portfolio", json!({"index":selected.to_string()}), None).await;
    let selected = index(&state.data.funds, "FundMandateReadModel", "FundId", fund);
    act(&mut state, &backend, &registry, &mut generation, "fund", json!({"index":selected.to_string()}), None).await;
    act(&mut state, &backend, &registry, &mut generation, "order", json!({}), Some(&order)).await;
    let selected = index(&state.data.trades, "FundOrderTradeProjectionReadModel", "TradeId", trade);
    act(&mut state, &backend, &registry, &mut generation, "trade", json!({"index":selected.to_string()}), Some(&order)).await;
    let result = act(&mut state, &backend, &registry, &mut generation, "compose",
        json!({"tradeId":trade.to_string()}), Some(&order)).await;
    assert_eq!(result.result.context["brokerMode"], "Emulator");
    assert_eq!(result.result.context["executionAction"], "Close");
    let legs = result.result.rows.iter().filter(|row| row.fields.get("kind").is_some_and(|kind| kind == "leg"))
        .map(|row| ifm_ui_egui::wire::decode::<Value>(row.data.as_ref().unwrap()).unwrap()).collect::<Vec<_>>();
    assert_eq!(legs.len(), 4);
    for leg in &legs {
        assert!(!leg["bid"].is_null() && !leg["ask"].is_null(), "Missing quote: {leg}");
    }
    assert!(!result.result.context["price"].is_empty());
    assert_eq!(result.result.context["canPlaceOrder"], "True");
    println!("Verified close preview with four historical quotes: {}", json!({
        "price":result.result.context["price"],"legs":legs,
    }));
}
