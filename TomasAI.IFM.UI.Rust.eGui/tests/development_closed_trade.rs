//! Opt-in, read-only regression check of a completed Development close.
use ifm_ui_egui::{backend::{schema::Registry, transport::*}, state::trades::TradeController};
use serde_json::{json, Value};
use std::time::Duration;

/// Loads the completed close through the real controller without issuing broker commands.
#[tokio::test]
#[ignore = "Requires an existing Development emulator closing setup and explicit IFM_VERIFY_* configuration"]
async fn closed_setup_loads_original_position_and_chart_without_order_submission() {
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
    let started = std::time::Instant::now();
    act(&mut state, &backend, &registry, &mut generation, "load-trade", json!({}), Some(&order)).await;
    let expected = format!("{}.{}.{}.{}", portfolio, fund,
        setting("IFM_VERIFY_OPENING_ORDER"), setting("IFM_VERIFY_OPENING_TRADE"));
    let deadline = tokio::time::Instant::now() + Duration::from_secs(60);
    loop {
        let publication = state.monitors.current();
        assert_eq!(publication.result.context["activeTradeTab"], expected);
        if publication.result.context["chartLoading"] == "False"
            && publication.result.context["observationsLoading"] == "False" {
            assert_eq!(publication.result.context["chartError"], "");
            assert_eq!(publication.result.context["observationsError"], "");
            let details = publication.result.rows.iter().find(|row| row.fields.get("kind").is_some_and(|v| v == "position-details")).unwrap();
            let position: Value = ifm_ui_egui::wire::decode(details.data.as_ref().unwrap()).unwrap();
            assert_eq!(position["isOpen"], false, "Expected completed close: {position}");
            if std::env::var("IFM_VERIFY_REALIZED_PNL").is_ok() {
                for (field, key) in [("realizedPnl","IFM_VERIFY_REALIZED_PNL"),
                    ("totalCommission","IFM_VERIFY_TOTAL_COMMISSION"),("netPnl","IFM_VERIFY_NET_PNL")] {
                    assert_eq!(position["historyValuation"][field].as_str(), Some(setting(key).as_str()));
                }
                let history = publication.result.rows.iter().filter(|row| row.fields.get("kind").is_some_and(|v| v == "position"))
                    .map(|row| ifm_ui_egui::wire::decode::<Value>(row.data.as_ref().unwrap()).unwrap())
                    .find(|row| row["tradeStatus"] == "Close").expect("Closed history row");
                for (field, key) in [("realizedPnl","IFM_VERIFY_REALIZED_PNL"),
                    ("totalCommission","IFM_VERIFY_TOTAL_COMMISSION"),("netPnl","IFM_VERIFY_NET_PNL")] {
                    assert_eq!(history[field].as_str(), Some(setting(key).as_str()));
                }
                let history_rows = publication.result.rows.iter().filter(|row| row.fields.get("kind").is_some_and(|v| v == "position"))
                    .map(|row| ifm_ui_egui::wire::decode::<Value>(row.data.as_ref().unwrap()).unwrap()).collect::<Vec<_>>();
                let cents = |text: &str| -> i64 {
                    let negative=text.starts_with('-'); let text=text.trim_start_matches('-');
                    let (whole,fraction)=text.split_once('.').unwrap_or((text,""));
                    assert!(fraction.chars().skip(2).all(|v|v=='0'));
                    let fraction=format!("{fraction:0<2}");
                    let value=whole.parse::<i64>().unwrap()*100+fraction[..2].parse::<i64>().unwrap();
                    if negative { -value } else { value }
                };
                let sum=history_rows.iter().map(|row|cents(row["tradePnl"].as_str().expect("History currency P&L"))).sum::<i64>();
                assert_eq!(sum,cents(&setting("IFM_VERIFY_NET_PNL")),"History rows must reconcile to final net");
                if std::env::var("IFM_VERIFY_RECOVERED_EOD").is_ok() {
                    for date in ["2026-10-26","2026-10-27"] {
                        let rows=history_rows.iter().filter(|row|row["valueDate"]==date).collect::<Vec<_>>();
                        assert_eq!(rows.len(),1);assert_eq!(rows[0]["tradeStatus"],"EndOfDay");
                    }
                }
                println!("Verified {} history rows sum to {sum} cents",history_rows.len());
                println!("Verified displayed close totals: {history}");
            }
            println!("Closed setup {trade} loaded original position {expected} including chart and details in {:?}", started.elapsed());
            break;
        }
        assert!(tokio::time::Instant::now() < deadline, "History did not finish: {:?}", publication.result.context);
        tokio::time::sleep(Duration::from_millis(50)).await;
    }
    state.monitors.close(&expected).unwrap();
}
