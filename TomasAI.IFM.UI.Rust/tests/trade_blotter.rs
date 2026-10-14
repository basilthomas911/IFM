use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{contract_editor::ContractPublication, trade_workspace::TradeWorkspace};
struct State {
    view: TradeWorkspace,
    snapshot: ContractPublication,
    requests: Vec<serde_json::Value>,
}
fn snapshot() -> ContractPublication {
    let mut rows = vec![
        serde_json::json!({"id":"order:27","fields":{"kind":"order","data":"{\"orderId\":27}"}}),
    ];
    rows.push(serde_json::json!({"id":"trade:1","fields":{"kind":"trade","data":"{\"index\":0,\"tradeId\":1,\"tradeState\":\"NewTrade\"}"}}));
    rows.push(serde_json::json!({"id":"market:0","fields":{"kind":"market","data":serde_json::json!({"windowMethod":"Frozen emulator preview","underlyingPrice":100,"contracts":[{"contractId":"SC","isCall":true,"strike":110,"bid":2,"ask":2.4,"delta":0.16}]}).to_string()}}));
    serde_json::from_value(serde_json::json!({"schema":1,"generation":3,"busy":false,"status":"Completed","outcomes":[],"result":{"view":"trade-orders","rows":rows,"choices":{"portfolio":["Development"],"fund":["Test Fund"],"expiry":["2026-11-20","2026-12-18"]},"hasMore":false,"context":{"orderId":"27","tradeId":"1","tradeState":"NewTrade","valueDate":"2026-10-14","expiry":"2026-11-20","editable":"True","compositionType":"ShortIronCondor","brokerMode":"Emulator","quantity":"1","price":"-3.20","previewKey":"SC","canPlaceOrder":"True"}}})).unwrap()
}
fn harness() -> Harness<'static, State> {
    let mut view = TradeWorkspace::default();
    view.open = true;
    let mut h = Harness::new_state(
        |ctx, state: &mut State| {
            if let Some(request) = state.view.draw(ctx, &state.snapshot) {
                let v: serde_json::Value = serde_json::from_str(&request).unwrap();
                state.snapshot.outcomes=serde_json::from_value(serde_json::json!([{"requestId":v["requestId"],"state":"Completed","message":"","generation":3}])).unwrap();
                state.requests.push(v);
            }
        },
        State {
            view,
            snapshot: snapshot(),
            requests: vec![],
        },
    );
    h.set_size(eframe::egui::vec2(2100., 1300.));
    h.run();
    h.get_by_label("Trade Blotter").click();
    h.run();
    h.run();
    h
}
#[test]
fn iron_condor_tabs_and_broker_fields_match_the_source_inventory() {
    let mut h = harness();
    for label in [
        "Market Selection",
        "Broker Trade",
        "Order Fills",
        "SHORT CALL DELTA:",
        "SHORT PUT DELTA:",
        "LIQUIDITY:",
        "Live Feed",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    assert!(h.query_by_label("Leg Staging").is_none());
    assert!(h.query_by_label("Load Chain").is_none());
    assert!(h.query_by_label("Refresh Trade").is_none());
    h.get_by_label("Broker Trade").click();
    h.run();
    for label in [
        "Contracts:",
        "Order type:",
        "Time in force:",
        "Action:",
        "OrderPrice:",
        "Net bid:",
        "Net mid:",
        "Net ask:",
        "IFM algorithm:",
        "Pace:",
        "Fund Economics and Limits:",
        "Available funds:",
        "Risk margin:",
        "Put spread:",
        "Call spread:",
        "Place Order",
        "Manage qualification...",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
}
#[test]
fn expiry_selection_refreshes_without_a_load_button() {
    let mut h = harness();
    h.get_by_label("expiry: 2026-11-20").click();
    h.run();
    h.get_by_label("2026-12-18").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "chain" && v["fields"]["expiry"] == "2026-12-18")
    );
}
#[test]
fn switching_to_fills_requests_durable_evidence_and_empty_actions_are_disabled() {
    let mut h = harness();
    h.get_by_label("Order Fills").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "refresh-execution")
    );
    assert!(h.query_by_label("ORDERS / FILLS").is_some());
    h.get_by_label("Cancel Unfilled").click();
    h.run();
    assert!(
        !h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "cancel-unfilled")
    );
}
#[test]
fn live_checkbox_uses_the_same_chain_intent() {
    let mut h = harness();
    h.get_by_label("Live Feed").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "chain" && v["fields"]["live"] == "true")
    );
}

#[test]
fn order_type_change_updates_preview_without_submitting() {
    let mut h = harness();
    h.get_by_label("Broker Trade").click();
    h.run();
    h.get_by_label("orderType: Limit").click();
    h.run();
    h.get_by_label("Market").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "preview-draft" && v["fields"]["orderType"] == "Market")
    );
    assert!(
        !h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "submit-order")
    );
}
#[test]
fn read_only_trade_disables_live_feed_and_place_order() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("editable".into(), "False".into());
    h.run();
    h.get_by_label("Live Feed").click();
    h.run();
    h.get_by_label("Broker Trade").click();
    h.run();
    h.get_by_label("Place Order").click();
    h.run();
    assert!(
        !h.state()
            .requests
            .iter()
            .any(|v| matches!(v["intent"].as_str(), Some("chain" | "submit-order")))
    );
}
#[test]
fn vertical_strategy_has_the_five_source_tabs_and_staging_controls() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("compositionType".into(), "PutCreditSpread".into());
    h.run();
    h.get_by_label("Leg Staging").click();
    h.run();
    for label in [
        "Broker Side",
        "Validation / Review",
        "Clear Staged Legs",
        "Recalculate / Validate",
        "Submit Opening Order",
        "Submit Closing Order",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    h.get_by_label("Clear Staged Legs").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "clear-staged")
    );
    h.get_by_label("Orders and Fills").click();
    h.run();
    assert!(h.query_by_label("Broker orders: none submitted").is_some());
    assert!(h.query_by_label("End Of Day...").is_some());
}
#[test]
fn qualification_exposes_report_and_durable_actions() {
    let mut h = harness();
    h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":"account:0","fields":{"kind":"account","data":serde_json::json!({"qualificationStatus":"Accepted","manualHold":false}).to_string()}})).unwrap());
    h.get_by_label("Broker Trade").click();
    h.run();
    h.get_by_label("Manage qualification...").click();
    h.run();
    for label in [
        "Submit evidence",
        "Accept reviewed manifest",
        "Revoke",
        "Set hold",
        "Release hold",
        "Resynchronize",
    ] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    h.get_by_label("Set hold").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "qualification-hold")
    );
}

#[test]
fn working_order_mutations_target_the_selected_broker_identity() {
    let mut h = harness();
    for (kind, value) in [
        (
            "broker-order",
            serde_json::json!({"id":{"componentId":"component"},"status":"Working","currentSignedNetDebitLimit":-3.2,"order":{"components":[{"strategyKind":"IronCondor"}]}}),
        ),
        (
            "broker-mutation",
            serde_json::json!({"canMutate":true,"minimum":-10,"maximum":0,"tick":0.05}),
        ),
    ] {
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":format!("{kind}:owner"),"fields":{"kind":kind,"data":value.to_string()}})).unwrap());
    }
    h.get_by_label("Order Fills").click();
    h.run();
    h.get_by_label("Update Order Price").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "update-limit" && v["fields"]["brokerOrderId"] == "owner")
    );
    h.get_by_label("Cancel Unfilled").click();
    h.run();
    assert!(
        h.state()
            .requests
            .iter()
            .any(|v| v["intent"] == "cancel-unfilled" && v["fields"]["brokerOrderId"] == "owner")
    );
}

#[test]
fn load_trade_closes_the_editor_and_keeps_monitoring_outside_the_blotter() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .rows
        .iter_mut()
        .find(|r| r.id == "trade:1")
        .unwrap()
        .fields
        .insert(
            "data".into(),
            serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string(),
        );
    h.state_mut().snapshot.result.rows.push(
        serde_json::from_value(
            serde_json::json!({"id":"established:1","fields":{"kind":"established","data":"{}"}}),
        )
        .unwrap(),
    );
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    h.run();
    assert!(h.query_by_label("Trade Order Editor").is_none());
    assert!(h.query_by_label("Position History").is_some());
    assert!(h.query_by_label("Iron Condor Trade").is_some());
    assert!(!h.state().requests.iter().any(|v| v["intent"] == "close"));
}

#[test]
fn late_composer_snapshot_replaces_the_initial_empty_draft() {
    let mut h = harness();
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("composerSessionId".into(), "ready-session".into());
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("expiry".into(), "2026-12-18".into());
    h.state_mut()
        .snapshot
        .result
        .context
        .insert("putDelta".into(), "20".into());
    h.run();
    assert!(h.query_by_label("expiry: 2026-12-18").is_some());
    h.get_by_label("Live Feed").click();
    h.run();
    assert!(h.state().requests.iter().any(|v| v["intent"] == "chain"
        && v["fields"]["expiry"] == "2026-12-18"
        && v["fields"]["putDelta"] == "20"));
}

#[test]
fn background_quote_refresh_keeps_controls_bright_and_queues_edits_until_acknowledged() {
    let mut h = harness();
    let snapshot = snapshot();
    let refresh: serde_json::Value = serde_json::from_str(&h.state_mut().view.request("refresh-chain", &snapshot)).unwrap();
    h.state_mut().snapshot.busy = true;
    h.state_mut().snapshot.status = "Pending".into();
    h.state_mut().snapshot.outcomes.clear();
    let before = h.state().requests.len();
    h.run();
    assert!(h.query_by_label("Pending").is_none());
    h.get_by_label("Live Feed").click(); // Remains enabled during a read refresh.
    h.run();
    assert_eq!(h.state().requests.len(), before); // Never submit into the busy gateway.
    h.state_mut().snapshot.busy = false;
    h.state_mut().snapshot.status = "Completed".into();
    h.state_mut().snapshot.outcomes = serde_json::from_value(serde_json::json!([
        {"requestId":refresh["requestId"],"state":"Completed","message":"","generation":4}
    ])).unwrap();
    h.state_mut().snapshot.generation = 4;
    h.run();
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "chain");
    assert_eq!(request["fields"]["live"], "true");
    assert_eq!(request["expectedGeneration"], 4);
}

#[test]
fn rejected_submission_releases_controls_without_an_outcome_or_replaying_a_mutation() {
    let mut h = harness();
    let request: serde_json::Value = serde_json::from_str(&h.state_mut().view.request("chain", &snapshot())).unwrap();
    h.state_mut().snapshot.outcomes.clear();
    h.run();
    h.state_mut().view.submission_rejected(request["requestId"].as_str().unwrap());
    h.run();
    h.get_by_label("Live Feed").click();
    h.run();
    assert!(h.state().requests.iter().any(|r| r["intent"] == "chain"));
}

#[test]
fn physical_pointer_click_switches_the_blotter_tab() {
    use eframe::egui::{Event, PointerButton, Modifiers};
    let mut h = harness();
    let pos = h.state().view.controls["tab:Order Fills"].center();
    h.input_mut().events.push(Event::PointerMoved(pos));
    h.step();
    h.input_mut().events.push(Event::PointerButton {pos, button:PointerButton::Primary,pressed:true,modifiers:Modifiers::NONE});
    h.step();
    h.input_mut().events.push(Event::PointerButton {pos, button:PointerButton::Primary,pressed:false,modifiers:Modifiers::NONE});
    h.run();
    assert!(h.query_by_label("Cancel Unfilled").is_some());
}
