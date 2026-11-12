//! Opt-in, read-only verification of an existing Development opening draft.
use ifm_ui_egui::{backend::{schema::Registry, transport::*}, state::trades::TradeController};
use serde_json::{json, Value};
use std::time::Duration;

/// Exercises the deployed Development queries through the same eGui controller used by Broker Trade.
#[tokio::test]
#[ignore = "Requires an existing Development opening draft and explicit IFM_VERIFY_* configuration"]
async fn new_draft_selects_a_listed_expiry_and_prices_four_live_legs() {
    let setting = |key: &str| std::env::var(key).unwrap_or_else(|_| panic!("Set {key}"));
    let portfolio: i64 = setting("IFM_VERIFY_PORTFOLIO").parse().unwrap();
    let fund: i64 = setting("IFM_VERIFY_FUND").parse().unwrap();
    let order = setting("IFM_VERIFY_ORDER");
    let trade: i64 = setting("IFM_VERIFY_TRADE").parse().unwrap();
    let expected_multiplier = std::env::var("IFM_VERIFY_CASH_MULTIPLIER")
        .ok().map(|value| value.parse::<f64>().unwrap());
    let expected_method = std::env::var("IFM_VERIFY_WINDOW_METHOD").ok();
    let expiry_catalog = std::env::var("IFM_VERIFY_EXPIRY_CATALOG").ok()
        .map(|path| std::fs::read_to_string(path).unwrap());
    let backend = Backend::connect(Settings {
        url: setting("IFM_VERIFY_NATS_URL"), credentials_file: None, token: None,
        request_timeout: Duration::from_secs(25),
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

    let opened = act(&mut state, &backend, &registry, &mut generation, "compose",
        json!({"tradeId":trade.to_string()}), Some(&order)).await;
    let context = &opened.result.context;
    assert_eq!(context["underlying"], setting("IFM_VERIFY_BASE_CONTRACT"));
    assert_eq!(context["tradeDate"], context["valueDate"]);
    assert_eq!(context["effectiveDate"], setting("IFM_VERIFY_EFFECTIVE_DATE"));
    assert!(context["expiry"].is_empty() && context["price"].is_empty());
    assert_eq!(context["submitted"], "False");
    let expiries = &opened.result.choices["expiry"];
    let bound = setting("IFM_VERIFY_LAST_EXPIRY");
    assert!(expiries.iter().all(|item| item.split(" / ").next().unwrap() <= bound.as_str()));
    assert!(expiries.iter().any(|item| item.split(" / ").next() == Some(bound.as_str())));
    println!("Opened {}:{} with {} expiry series through {}", order, trade, expiries.len(), bound);

    let mut passed = false;
    for expiry in setting("IFM_VERIFY_EXPIRIES").split(';') {
        assert!(expiries.iter().any(|listed| listed == expiry), "Unlisted expiry {expiry}");
        let fields = json!({
            "tradeId":trade.to_string(), "composerSessionId":context["composerSessionId"],
            "expiry":expiry, "live":"true", "callDelta":context["callDelta"],"putDelta":context["putDelta"],
            "callWidth":context["callWidth"],"putWidth":context["putWidth"],
        });
        let mut action = "chain";
        let mut selected_manually = false;
        let deadline = tokio::time::Instant::now() + Duration::from_secs(65);
        loop {
            let result = state.execute(&backend, &registry, &json!({
                "schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),
                "intent":action,"expectedGeneration":generation,"fields":fields,"originalId":order,
            })).await.unwrap();
            generation = result.generation;
            let view = &result.result.context;
            assert_eq!(view["submitted"], "False");
            let legs = result.result.rows.iter().filter(|row| row.fields.get("kind").is_some_and(|kind| kind == "leg"))
                .map(|row| ifm_ui_egui::wire::decode::<Value>(row.data.as_ref().unwrap()).unwrap()).collect::<Vec<_>>();
            let market = result.result.rows.iter().find(|row| row.fields.get("kind").is_some_and(|kind| kind == "market"))
                .and_then(|row| ifm_ui_egui::wire::decode::<Value>(row.data.as_ref()?).ok());
            let outcomes = result.outcomes.iter().map(|outcome| &outcome.message).collect::<Vec<_>>();
            if let Some(market) = &market {
                let contracts = market["contracts"].as_array().map(Vec::as_slice).unwrap_or_default();
                if let Some(expected) = &expected_method {
                    assert_eq!(market["windowMethod"].as_str(), Some(expected.as_str()));
                    let lower = number(&market["lowerStrikeBound"]).unwrap();
                    let upper = number(&market["upperStrikeBound"]).unwrap();
                    let in_range = contracts.iter().filter(|row| number(&row["strike"])
                        .is_some_and(|strike| strike >= lower && strike <= upper)).count();
                    println!("Expiry strike range {lower}–{upper}, {in_range} contracts inside");
                    if let Some(catalog) = &expiry_catalog {
                        let returned = contracts.iter().filter_map(|row| row["contractId"].as_str())
                            .collect::<std::collections::BTreeSet<_>>();
                        let expected = catalog.lines().filter(|id| {
                            id.rsplit_once(['C', 'P']).and_then(|(_, strike)| strike.parse::<f64>().ok())
                                .is_some_and(|strike| strike >= lower && strike <= upper)
                        }).collect::<std::collections::BTreeSet<_>>();
                        assert_eq!(in_range, expected.len());
                        assert!(expected.is_subset(&returned));
                    }
                }
                let quoted = contracts.iter().filter(|row| !row["bid"].is_null() && !row["ask"].is_null()).count();
                let greeks = contracts.iter().filter(|row| !row["delta"].is_null()).count();
                println!("expiry={expiry} market={} rows={} quoted={quoted} greeks={greeks}",
                    market["windowMethod"],contracts.len());
                if !selected_manually && legs.len() != 4 && quoted >= 4
                    && let Some(selection) = test_condor(market, contracts)
                {
                    act(&mut state, &backend, &registry, &mut generation, "clear-staged",
                        json!({"tradeId":trade.to_string(),"composerSessionId":context["composerSessionId"]}),
                        Some(&order)).await;
                    for (role, contract_id) in selection {
                        act(&mut state, &backend, &registry, &mut generation, "select-leg",
                            json!({"tradeId":trade.to_string(),"composerSessionId":context["composerSessionId"],
                                "contractId":contract_id,"role":role}),
                            Some(&order)).await;
                    }
                    selected_manually = true;
                    action = "refresh-chain";
                    continue;
                }
            }
            println!("expiry={expiry} live={} legs={} price={} outcomes={outcomes:?}",
                view["live"],legs.len(),view["price"]);
            passed = view["live"] == "True" && view["canPlaceOrder"] == "True" && !view["price"].is_empty()
                && legs.len() == 4 && legs.iter().all(|leg|
                    !leg["bid"].is_null() && !leg["ask"].is_null() && !leg["quoteAtUtc"].is_null()
                    && leg["stale"] != true && leg["frozen"] == false);
            if passed {
                println!("Qualified live OrderPrice {} with {}", view["price"], json!(legs));
                if let Some(multiplier) = expected_multiplier {
                    let preview = result.result.rows.iter()
                        .find(|row| row.fields.get("kind").is_some_and(|kind| kind == "broker-preview"))
                        .map(|row| ifm_ui_egui::wire::decode::<Value>(row.data.as_ref().unwrap()).unwrap())
                        .expect("Broker Trade cash preview");
                    assert_eq!(view["multiplier"].parse::<f64>().ok(), Some(multiplier));
                    verify_cash_preview(&legs, &preview["fields"], &view["price"], multiplier);
                }
                break;
            }
            if tokio::time::Instant::now() >= deadline { break; }
            action = if view["live"] == "True" && view["expiry"] == expiry { "refresh-chain" } else { "chain" };
            tokio::time::sleep(Duration::from_secs(4)).await;
        }
        if passed { break; }
    }
    state.release_preview(&backend, &registry, tokio::time::Instant::now() + Duration::from_secs(10)).await.unwrap();
    assert!(passed, "No selected expiry produced four qualified quotes and a valid OrderPrice");
}

/// Verifies the displayed one-lot short condor economics from its live strikes and point price.
fn verify_cash_preview(legs: &[Value], fields: &Value, order_price: &str, multiplier: f64) {
    let strike = |role: &str| number(&legs.iter().find(|leg| leg["role"] == role).unwrap()["strike"]).unwrap();
    let width = (strike("+LC") - strike("-SC")).max(strike("-SP") - strike("+LP"));
    let price: f64 = order_price.parse().unwrap();
    let credit = -price * multiplier;
    let cash = |caption: &str| fields[caption].as_str().unwrap().replace(',', "").parse::<f64>().unwrap();
    assert!(legs.iter().all(|leg| number(&leg["multiplier"]) == Some(multiplier)), "{legs:?}");
    let mid: f64 = legs.iter().map(|leg| {
        let side = number(&leg["sign"]).unwrap();
        side * (number(&leg["bid"]).unwrap() + number(&leg["ask"]).unwrap()) / 2.0
    }).sum();
    assert!((cash("Net mid") - mid).abs() < 0.005001);
    assert!((price - mid).abs() <= 0.125001, "OrderPrice {price} does not track Net Mid {mid}");
    for (caption, expected) in [("Est. credit", credit), ("Max profit", credit), ("Max loss", width * multiplier - credit)] {
        assert!((cash(caption) - expected).abs() < 0.005, "{caption}: displayed {}, expected {expected}", fields[caption]);
    }
    println!("Live net mid {mid} points, cash totals at {multiplier} per point: credit {}, maximum profit {}, maximum loss {}",
        fields["Est. credit"], fields["Max profit"], fields["Max loss"]);
}

fn number(value: &Value) -> Option<f64> {
    value.as_f64().or_else(|| value.as_str()?.parse().ok()).filter(|number| number.is_finite())
}

/// Selects two currently quoted strikes on either side for an unsubmitted preview.
fn test_condor(market: &Value, contracts: &[Value]) -> Option<[(&'static str, String); 4]> {
    let underlying = number(&market["underlyingPrice"])?;
    let side = |call| {
        let mut strikes = contracts.iter().filter_map(|row| {
            let strike = number(&row["strike"])?;
            let bid = number(&row["bid"])?;
            let ask = number(&row["ask"])?;
            let distance = if call { strike - underlying } else { underlying - strike };
            (row["isCall"].as_bool() == Some(call) && row["isStale"] != true
                && !row["quoteAtUtc"].is_null() && bid > 0.0 && ask >= bid && distance > 0.0)
                .then(|| (distance, row["contractId"].as_str().unwrap_or_default()))
                .filter(|(_, id)| !id.is_empty())
        }).collect::<Vec<_>>();
        strikes.sort_by(|a, b| a.0.total_cmp(&b.0));
        let (short, short_id) = *strikes.first()?;
        let (_, wing_id) = strikes.iter().filter(|(distance, _)| *distance > short)
            .min_by(|a, b| (a.0 - short - 50.).abs().total_cmp(&(b.0 - short - 50.).abs()))?;
        Some((short_id.to_string(), wing_id.to_string()))
    };
    let (short_call, long_call) = side(true)?;
    let (short_put, long_put) = side(false)?;
    Some([("-SC", short_call), ("+LC", long_call), ("-SP", short_put), ("+LP", long_put)])
}
