use egui_kittest::kittest::NodeT;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{contract_editor::ContractPublication, trade_workspace::TradeWorkspace};
struct State {
    view: TradeWorkspace,
    snapshot: ContractPublication,
    requests: Vec<serde_json::Value>,
    fail_load: bool,
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
            // Encode authored row documents exactly as the managed binary bridge does.
            let mut changed = false;
            for row in &mut state.snapshot.result.rows {
                if let Some(document)=row.fields.remove("data") {
                    changed = true;
                    let value:serde_json::Value=serde_json::from_str(&document).unwrap();
                    row.data=Some(ifm_ui_rust::wire::encode_value(&value).unwrap().into());
                }
            }
            if changed { state.snapshot.invalidate_render_cache(); }
            if let Some(request) = state.view.draw(ctx, &state.snapshot) {
                let v: serde_json::Value = serde_json::from_str(&request).unwrap();
                let failed = state.fail_load && v["intent"] == "load-trade";
                state.snapshot.outcomes=serde_json::from_value(serde_json::json!([{"requestId":v["requestId"],"state":if failed {"Failed"} else {"Completed"},"message":if failed {"Persisted trade limits were not found."} else {""},"generation":3}])).unwrap();
                if v["intent"]=="close-trade" {
                    let key=v["fields"]["tradeTabId"].as_str().unwrap();
                    state.snapshot.result.rows.retain(|r|r.fields.get("kind").is_none_or(|k|k!="established" && !(k=="workspace-tab" && r.id==format!("workspace-tab:{key}"))));
                }
                if v["intent"]=="activate-trade" || v["intent"]=="close-trade" {
                    let key=v["fields"]["tradeTabId"].as_str().unwrap();
                    for row in &mut state.snapshot.result.rows {
                        if row.fields.get("kind").is_some_and(|k|k=="workspace-tab") {
                            let mut value:serde_json::Value=ifm_ui_rust::wire::decode(row.data.as_ref().unwrap()).unwrap();
                            value["active"]=serde_json::json!(if v["intent"]=="close-trade" {true}else{value["key"]==key});
                            row.data=Some(ifm_ui_rust::wire::encode_value(&value).unwrap().into());
                        }
                    }
                }
                if v["intent"]=="activate-trade" || v["intent"]=="close-trade" {
                    let active=state.snapshot.result.rows.iter().filter(|r|r.fields.get("kind").is_some_and(|k|k=="workspace-tab")).map(|r|ifm_ui_rust::wire::decode::<serde_json::Value>(r.data.as_ref().unwrap()).unwrap()).find(|v|v["active"]==true);
                    if let Some(active)=active {
                        let id=active["key"].as_str().unwrap().split('.').filter_map(|v|v.parse::<i32>().ok()).collect::<Vec<_>>();
                        if id.len()==4 {
                            state.snapshot.result.rows.retain(|r|r.fields.get("kind").is_none_or(|k|k!="established"));
                            let value=serde_json::json!({"id":{"portfolioId":id[0],"fundId":id[1],"orderId":id[2],"tradeId":id[3]}});
                            let mut row:ifm_ui_rust::contract_editor::ContractRow=serde_json::from_value(serde_json::json!({"id":"established:active","fields":{"kind":"established"}})).unwrap();
                            row.data=Some(ifm_ui_rust::wire::encode_value(&value).unwrap().into());state.snapshot.result.rows.push(row);
                        }
                    }
                }
                state.requests.push(v);
            }
        },
        State {
            view,
            snapshot: snapshot(),
            requests: vec![],
            fail_load: false,
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
            serde_json::json!({"id":"established:1","fields":{"kind":"established","data":serde_json::json!({"id":{"orderId":27,"tradeId":1}}).to_string()}}),
        )
        .unwrap(),
    );
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    h.run();
    assert!(h.query_by_label("Trade Order Editor").is_none());
    assert!(h.query_by_label("ActionDateTime").is_some());
    assert!(h.query_by_label("Close trade 27:1").is_some());
    assert!(!h.state().view.open);
    let panel = h.state().view.loaded_bounds.expect("Trade occupies the main center panel");
    assert!(panel.left() < 10. && panel.top() < 10.);
    assert!(panel.width() > 2000. && panel.height() > 1200.);
    assert!(!h.state().requests.iter().any(|v| v["intent"] == "close"));
    h.get_by_label("Close trade 27:1").click();
    h.run();
    h.run();
    assert!(h.state().view.loaded_bounds.is_none());
    assert!(h.state().requests.iter().any(|v| v["intent"] == "close-trade"));
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

#[test]
fn accepted_submission_selects_fills_before_evidence_refresh_completes() {
    let mut h = harness();
    h.get_by_label("Broker Trade").click();
    h.run();
    let state = h.state_mut();
    state.snapshot.busy = true;
    state.snapshot.result.context.insert("submitted".into(), "True".into());
    state.snapshot.result.context.insert("submissionStatus".into(), "Broker accepted".into());
    h.run();
    assert!(h.query_by_label("ORDERS / FILLS").is_some());
    assert!(h.query_by_label("Broker accepted").is_some());
    assert!(h.query_by_label("Place Order").is_none());
    h.state_mut().snapshot.status = "Execution evidence unavailable".into();
    h.run();
    assert!(h.query_by_label("ORDERS / FILLS").is_some());
    assert!(h.query_by_label("Broker accepted").is_some());
}

#[test]
fn confirmation_waits_for_broker_result_and_rejection_closes_before_error() {
    let mut h=harness();
    let confirmation=serde_json::json!({"id":"test-confirmation","submitting":true,
        "order":{"orderQuantity":1},"legs":[{"side":"Sell","role":"Short Put",
        "contractId":"ES-VERY-LONG-CONTRACT-IDENTIFIER-20261120-P-6000","legCommission":0.65}]}).to_string();
    h.state_mut().snapshot.result.documents.insert("confirmation".into(),ifm_ui_rust::wire::encode_value(&serde_json::from_str::<serde_json::Value>(&confirmation).unwrap()).unwrap().into());
    h.run();
    assert!(h.query_by_label("Submitting ? awaiting broker acceptance?").is_some());
    assert!(h.query_by_label("Contract ID").is_some());
    let header_x = ["Contract ID", "Role", "Side", "Leg Commission"].map(|caption|
        h.get_by_label(caption).accesskit_node().bounding_box().unwrap().x0);
    assert!(header_x.windows(2).all(|pair| pair[0] < pair[1]));
    let quantity = h.get_by_label("Order Quantity:").accesskit_node().bounding_box().unwrap();
    let broker = h.get_by_label("Broker Type:").accesskit_node().bounding_box().unwrap();
    assert!((quantity.x0 - broker.x0).abs() < 1., "Summary captions must share one column");
    assert!(broker.y0 > quantity.y0);
    assert!(h.query_by_label("Total Commission:").is_some());
    h.state_mut().snapshot.result.context.insert("submissionError".into(),"BROKER.123: Order rejected".into());
    h.run();
    assert!(h.query_by_label("BROKER.123: Order rejected").is_none());
    h.state_mut().snapshot.result.documents.remove("confirmation");
    h.run();
    assert!(h.query_by_label("Submitting ? awaiting broker acceptance?").is_none());
    assert!(h.query_by_label("BROKER.123: Order rejected").is_some());
    h.get_by_label("OK").click(); h.run();
    assert!(h.query_by_label("BROKER.123: Order rejected").is_none());
}

#[test]
fn confirmation_leg_rows_follow_broker_trade_display_order() {
    let mut h = harness();
    for (index, contract) in ["HIGH-CALL", "LOW-PUT"].iter().enumerate() {
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({
            "id":format!("leg:{index}"),"fields":{"kind":"leg","data":serde_json::json!({
                "contractId":contract,"role":"Selected","strike":if index==0 {110} else {90},"isCall":index==0
            }).to_string()}
        })).unwrap());
    }
    let confirmation = serde_json::json!({"id":"ordered-legs","order":{"orderQuantity":1},"legs":[
        {"contractId":"LOW-PUT","role":"Short Put","side":"Sell","legCommission":0.65},
        {"contractId":"HIGH-CALL","role":"Short Call","side":"Sell","legCommission":0.65}
    ]});
    h.state_mut().snapshot.result.documents.insert("confirmation".into(), ifm_ui_rust::wire::encode_value(&confirmation).unwrap().into());
    h.run();
    let high = h.get_by_label("HIGH-CALL").accesskit_node().bounding_box().unwrap();
    let low = h.get_by_label("LOW-PUT").accesskit_node().bounding_box().unwrap();
    assert!(high.y0 < low.y0, "Confirmation must use the selected-leg display order");
}

#[test]
fn broker_leg_delta_precision_and_grid_bounds_survive_value_updates() {
    let mut h = harness();
    h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({
        "id":"leg:precision", "fields":{"kind":"leg"},
        "data":null
    })).unwrap());
    let publish = |h: &mut Harness<'_, State>, delta: f64, contract: &str| {
        let leg = h.state_mut().snapshot.result.rows.last_mut().unwrap();
        leg.data = Some(ifm_ui_rust::wire::encode_value(&serde_json::json!({
            "contractId":contract,"role":"Short Call","delta":delta,"bid":2.0,"ask":2.4
        })).unwrap().into());
    };
    publish(&mut h, 0.16, "ES-CALL");
    h.get_by_label("Broker Trade").click();
    h.run();
    assert!(h.query_by_label("0.160000").is_some());
    let before = h.state().view.controls["brokerPreviewLegGrid"];
    publish(&mut h, -0.123456789, "ES-CHANGED-LONG-CONTRACT-IDENTIFIER");
    h.run();
    assert!(h.query_by_label("-0.123457").is_some());
    assert_eq!(before, h.state().view.controls["brokerPreviewLegGrid"]);
}

#[test]
fn failed_load_closes_editor_and_shows_a_separate_error() {
    let mut h = harness();
    h.state_mut().fail_load = true;
    h.state_mut().snapshot.result.context.insert("canLoadTrade".into(), "True".into());
    h.state_mut().snapshot.result.rows.iter_mut().find(|r| r.id == "trade:1").unwrap().fields.insert(
        "data".into(), serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string());
    h.get_by_label("Trade Orders").click();
    h.run();
    h.get_by_label("Load Trade").click();
    h.run();
    assert!(!h.state().view.open);
    h.run();
    assert!(h.query_by_label("Trade Order Editor").is_none());
    assert!(h.query_by_label("Load Trade Error").is_some());
    assert!(h.query_by_label("Persisted trade limits were not found.").is_some());
    assert!(h.state().view.loaded_bounds.is_none());
    h.get_by_label("OK").click();
    h.run();
    assert!(h.state().requests.iter().any(|v| v["intent"] == "close"));
}

#[test]
fn established_iron_condor_dashboard_displays_saved_limits_and_leg_marks() {
    let mut h=harness();
    h.state_mut().snapshot.result.rows.iter_mut().find(|r|r.id=="trade:1").unwrap().fields.insert("data".into(),serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string());
    for (kind,value) in [
        ("established",serde_json::json!({"id":{"orderId":27,"tradeId":1},"status":"Open","openingValue":-3.2,"establishedAtUtc":"2026-10-14T14:30:00Z","legs":[
            {"tradeLegId":"sp","contractId":"SAVED-PUT","putCall":2,"strike":90,"signedQuantity":-1,"cashMultiplier":50,"expiry":"2026-11-20"},
            {"tradeLegId":"lp","contractId":"LONG-PUT","putCall":2,"strike":85,"signedQuantity":1,"cashMultiplier":50,"expiry":"2026-11-20"}]})),
        ("position",serde_json::json!({"orderId":27,"tradeId":1,"tradeType":"ShortIronCondor","valueDate":"2026-10-14","tradeStatus":"Open","netSpread":-3.2,"tradePnl":12.5})),
        ("limits",serde_json::json!({"orderId":27,"tradeLimit":{"riskMargin":38.55,"maxProfit":11.45,"maxLoss":-38.55}})),
        ("position-details",serde_json::json!({"valueDate":"2026-10-14","legs":[{"tradeLegId":"sp","signedQuantity":-1,"openingPrice":2.5,"currentPrice":2.3},{"tradeLegId":"lp","signedQuantity":1,"openingPrice":0.5,"currentPrice":0.4}]})),
        ("plan",serde_json::json!({"valueDate":"2026-10-14","calculatedAtUtc":"2026-10-14T15:00:00Z","state":"Hold","action":"Monitor","explanation":"Saved plan evidence","totalPnl":12.5})),
    ] {
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":format!("{kind}:0"),"fields":{"kind":kind,"data":value.to_string()}})).unwrap());
    }
    h.state_mut().snapshot.result.context.insert("canSelectPosition".into(),"True".into());
    h.get_by_label("Trade Orders").click();h.run();
    h.get_by_label("Load Trade").click();h.run();h.run();
    for label in ["Option Contract Ids","Iron Condor Net / Forward Spread (price points)","ActionDateTime","SAVED-PUT","38.55","Saved plan evidence","Spread Type","PutCreditSpread"] {
        assert!(h.query_all_by_label(label).next().is_some(),"Missing {label}");
    }
    assert!(h.state().view.loaded_bounds.unwrap().width()>2000.);
    let controls=&h.state().view.controls;
    assert!(controls["condor-chart"].left()>controls["condor-history"].right()-5.);
    assert!(controls["condor-trade-info"].top()>controls["condor-history"].bottom());
    assert!(controls["condor-limits"].top()>=controls["condor-contracts"].bottom()-6.);
    assert!(controls["condor-plans"].top()>controls["condor-limits"].bottom());
    assert!((controls["condor-plans"].bottom()-h.state().view.loaded_bounds.unwrap().bottom()).abs()<1.);
    assert!(h.query_by_label("TradePlans").is_none());
    if let Ok(path)=std::env::var("IFM_CONDOR_SCREENSHOT") {h.render().unwrap().save(path).unwrap();}
    h.set_size(eframe::egui::vec2(1200.,800.));h.run();
    assert!(h.query_by_label("Close trade 27:1").is_some());
}

fn loaded_tabs_harness() -> Harness<'static,State> {
    let mut h=harness();
    h.state_mut().snapshot.result.rows.iter_mut().find(|r|r.id=="trade:1").unwrap().fields.insert("data".into(),serde_json::json!({"index":0,"tradeId":1,"tradeState":"Open"}).to_string());
    for (kind,key,value) in [
        ("established","first",serde_json::json!({"id":{"portfolioId":101,"fundId":701,"orderId":27,"tradeId":1}})),
        ("workspace-tab","101.701.27.1",serde_json::json!({"key":"101.701.27.1","title":"27:1","kind":"trade","active":true})),
        ("workspace-tab","101.701.28.2",serde_json::json!({"key":"101.701.28.2","title":"28:2","kind":"trade","active":false})),
    ] {
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({"id":format!("{kind}:{key}"),"fields":{"kind":kind,"data":value.to_string()}})).unwrap());
    }
    h.get_by_label("Trade Orders").click();h.run();h.get_by_label("Load Trade").click();h.run();h.run();h
}

fn scrolling_trade_plans_harness() -> Harness<'static,State> {
    trade_plans_harness(200)
}
fn trade_plans_harness(count: usize) -> Harness<'static,State> {
    let mut h = loaded_tabs_harness();
    ifm_ui_rust::presentation::apply(&h.ctx);
    for i in 0..count {
        let value = serde_json::json!({"calculatedAtUtc":format!("plan-row-{i:03}"),"explanation":"scroll fixture"});
        h.state_mut().snapshot.result.rows.push(serde_json::from_value(serde_json::json!({
            "id":format!("plan:{i}"),"fields":{"kind":"plan","data":value.to_string()}
        })).unwrap());
    }
    h.set_size(eframe::egui::vec2(1200., 800.));
    h.run();
    h
}

#[test]
#[ignore = "Explicit CPU frame benchmark; run alone in debug and release"]
fn benchmark_trade_view_mouse_frames() {
    use eframe::egui::{Event, pos2};
    let mut results = Vec::new();
    for count in [100, 1_000, 10_000] {
        let mut h = trade_plans_harness(count);
        h.ctx.style_mut(|style| style.interaction.tooltip_delay = f32::INFINITY);
        let mut times = Vec::new();
        for frame in 0..65 {
            h.input_mut().events.push(Event::PointerMoved(pos2(40. + (frame % 20) as f32 * 20., 590.)));
            let start = std::time::Instant::now();
            h.step();
            if frame >= 5 { times.push(start.elapsed().as_secs_f64() * 1_000.); }
        }
        times.sort_by(f64::total_cmp);
        let mut preparation = Vec::new(); let mut first_frames = Vec::new();
        for _ in 0..5 {
            h.state_mut().snapshot.invalidate_render_cache();
            let start = std::time::Instant::now();
            TradeWorkspace::prepare_publication(&h.state().snapshot);
            preparation.push(start.elapsed().as_secs_f64() * 1_000.);
            let start = std::time::Instant::now(); h.step();
            first_frames.push(start.elapsed().as_secs_f64() * 1_000.);
        }
        preparation.sort_by(f64::total_cmp); first_frames.sort_by(f64::total_cmp);
        let result = serde_json::json!({"plans":count,"frames":times.len(),"p50Ms":times[30],"p95Ms":times[57],"maxMs":times[59],
            "preparationP50Ms":preparation[2],"preparedFirstFrameP50Ms":first_frames[2]});
        println!("{result}"); results.push(result);
    }
    if let Ok(path) = std::env::var("IFM_TRADE_FRAME_BENCHMARK") {
        std::fs::write(path, serde_json::to_vec_pretty(&results).unwrap()).unwrap();
    }
}

#[test]
fn trade_plan_grid_scrolls_both_axes_inside_its_bounds() {
    use eframe::egui::{Event, MouseWheelUnit, Modifiers, vec2};
    let mut h = scrolling_trade_plans_harness();
    let bounds = h.state().view.controls["condor-plans"];
    assert!(h.query_by_label("plan-row-000").is_some());
    let header_x = h.get_by_label("ActionDateTime").accesskit_node().bounding_box().unwrap().x0;
    h.input_mut().events.extend([
        Event::PointerMoved(bounds.center()),
        Event::MouseWheel { unit: MouseWheelUnit::Point, delta: vec2(0., -300.), modifiers: Modifiers::NONE },
    ]);
    h.run_steps(8);
    assert!(h.query_by_label("plan-row-000").is_none(), "Vertical wheel must advance the plan rows");
    h.input_mut().events.push(Event::MouseWheel {
        unit: MouseWheelUnit::Point, delta: vec2(-600., 0.), modifiers: Modifiers::NONE,
    });
    h.run_steps(8);
    let shifted = h.query_by_label("ActionDateTime").map(|node| node.accesskit_node().bounding_box().unwrap().x0);
    assert!(shifted.is_none_or(|x| x < header_x - 100.), "Horizontal wheel must move the columns: {shifted:?}");
}

#[test]
fn trade_plan_scrollbars_are_clickable_inside_the_grid() {
    use eframe::egui::{Event, Modifiers, PointerButton, pos2};
    let mut h = scrolling_trade_plans_harness();
    let bounds = h.state().view.controls["condor-plans"];
    if let Ok(path) = std::env::var("IFM_CONDOR_SCROLL_SCREENSHOT") { h.render().unwrap().save(path).unwrap(); }
    for (axis, point) in [pos2(bounds.right() - 6., bounds.center().y), pos2(bounds.center().x, bounds.bottom() - 6.)].into_iter().enumerate() {
        h.input_mut().events.push(Event::PointerMoved(point));
        h.run_steps(2);
        h.input_mut().events.push(Event::PointerButton { pos:point, button:PointerButton::Primary, pressed:true, modifiers:Modifiers::NONE });
        h.run_steps(2);
        let dragged = point + if axis == 0 { eframe::egui::vec2(0., 40.) } else { eframe::egui::vec2(80., 0.) };
        h.input_mut().events.push(Event::PointerMoved(dragged));
        h.run_steps(2);
        h.input_mut().events.push(Event::PointerButton { pos:dragged, button:PointerButton::Primary, pressed:false, modifiers:Modifiers::NONE });
        h.run_steps(8);
        if axis == 0 { assert!(h.query_by_label("plan-row-000").is_none(), "Vertical scrollbar must advance the rows before horizontal scrolling"); }
    }
    let header = h.query_by_label("ActionDateTime").map(|node|node.accesskit_node().bounding_box().unwrap().x0);
    assert!(header.is_none_or(|x| x < bounds.left() as f64 - 100.), "Horizontal scrollbar must move the columns: {header:?}");
}
#[test]
fn center_tabs_switch_and_close_only_the_requested_trade() {
    let mut h=loaded_tabs_harness();
    for label in ["27:1","28:2","Close trade 27:1","Close trade 28:2"] {assert!(h.query_by_label(label).is_some(),"Missing {label}");}
    let controls=&h.state().view.controls;
    let tab=controls["trade-tab:101.701.27.1"];let close=controls["close-trade:101.701.27.1"];
    assert!(close.left()>tab.center().x && close.right()<=tab.right());
    assert!(h.query_by_label("Close Trade").is_none());
    h.get_by_label("28:2").click();h.run();h.run();
    assert!(h.state().requests.iter().any(|r|r["intent"]=="activate-trade" && r["fields"]["tradeTabId"]=="101.701.28.2"));
    h.get_by_label("Close trade 27:1").click();h.run();h.run();
    assert!(h.query_by_label("27:1").is_none());assert!(h.query_by_label("28:2").is_some());
    assert!(h.state().view.loaded_bounds.is_some());
    h.get_by_label("Close trade 28:2").click();h.run();h.run();
    assert!(h.state().view.loaded_bounds.is_none());
    assert!(h.state().requests.iter().filter(|r|r["intent"]=="close-trade").count()==2);
}
#[test]
fn a_failed_additional_load_keeps_existing_center_tabs_open() {
    let mut h=loaded_tabs_harness();h.state_mut().fail_load=true;h.state_mut().view.open=true;h.run();
    h.get_by_label("Load Trade").click();h.run();h.run();
    assert!(h.query_by_label("Load Trade Error").is_some());
    assert!(h.query_by_label("28:2").is_some());
    assert!(h.state().view.loaded_bounds.is_some());
}
#[test]
fn live_plan_publications_update_the_grid_without_a_user_refresh() {
    let mut h=loaded_tabs_harness();
    h.state_mut().snapshot.result.context.insert("live".into(),"True".into());
    let requests_before=h.state().requests.len();
    for revision in [1,2] {
        let value=serde_json::json!({"planRevision":revision,"valueDate":"2026-10-16","ironCondorTradePlanSnapshot":{"actionDateTime":"2026-10-16T15:00:00Z","sequenceId":revision,"dailyPnl":revision as f64*1.25,"isComplete":true,"actionType":"Monitor","actionReason":format!("Incoming live plan {revision}")}});
        let row=serde_json::from_value(serde_json::json!({"id":format!("plan:{revision}"),"fields":{"kind":"plan","data":value.to_string(),"actionDateTimeEastern":format!("2026-10-16 11:00:0{revision}.123")}})).unwrap();
        h.state_mut().snapshot.result.rows.insert(0,row);
        h.state_mut().snapshot.generation+=1;
        h.run();
        assert!(h.query_all_by_label(&format!("Incoming live plan {revision}")).next().is_some());
        assert!(h.query_all_by_label(&format!("2026-10-16 11:00:0{revision}.123")).next().is_some());
    }
    assert!(h.query_all_by_label("Incoming live plan 1").next().is_some());
    assert!(h.query_all_by_label("Incoming live plan 2").next().is_some());
    assert_eq!(h.state().requests.len(),requests_before);
}

#[test]
fn load_trade_transfer_failure_remains_visible_after_editor_closes() {
    let mut view = TradeWorkspace::default();
    view.request("load-trade", &snapshot());
    view.transport_failed("Trade history transfer failed");
    assert!(view.is_visible());
    let mut h = Harness::new_state(|ctx, (view, snapshot): &mut (TradeWorkspace, ContractPublication)| {
        view.draw(ctx, snapshot);
    }, (view, snapshot()));
    h.run();
    assert!(h.query_all_by_label("Trade history transfer failed").next().is_some());
}

#[test]
fn virtual_plan_grid_requests_only_visible_rows_after_trade_is_loaded() {
    let mut publication = snapshot();
    publication.result.context.insert("activeTradeTab".into(), "101.701.1901.1501".into());
    publication.result.context.insert("planOffset".into(), "0".into());
    publication.result.context.insert("planTotal".into(), "10000".into());
    publication.result.context.insert("planDate".into(), "2026-10-16".into());
    publication.result.rows.push(serde_json::from_value(serde_json::json!({"id":"established:1", "fields":{"kind":"established"}, "data":null})).unwrap());
    publication.result.rows.last_mut().unwrap().data = Some(ifm_ui_rust::wire::encode_value(&serde_json::json!({"id":{"portfolioId":101,"fundId":701,"orderId":1901,"tradeId":1501},"legs":[]})).unwrap().into());
    let mut view = TradeWorkspace::default();
    let id = view.request("load-trade", &publication);
    let request:serde_json::Value = serde_json::from_str(&id).unwrap();
    publication.outcomes.push(serde_json::from_value(serde_json::json!({"requestId":request["requestId"],"state":"Completed","message":"Loaded"})).unwrap());
    let mut h = Harness::new_state(|ctx, state: &mut (TradeWorkspace, ContractPublication, Vec<serde_json::Value>)| {
        if let Some(request)=state.0.draw(ctx,&state.1) {state.2.push(serde_json::from_str(&request).unwrap());}
    }, (view, publication, Vec::new()));
    h.run();
    let page = h.state().2.iter().find(|request|request["intent"]=="plans-page").expect("visible page requested");
    let size:usize = page["fields"]["pageSize"].as_str().unwrap().parse().unwrap();
    assert!(size>0 && size<=100);
    assert_eq!(page["fields"]["offset"], "0");
    let page_id = page["requestId"].clone();
    for index in 0..size {
        let mut row:ifm_ui_rust::contract_editor::ContractRow = serde_json::from_value(serde_json::json!({"id":format!("plan:{index}"),"fields":{"kind":"plan"}})).unwrap();
        row.data=Some(ifm_ui_rust::wire::encode_value(&serde_json::json!({"ironCondorTradePlanSnapshot":{"isComplete":true,"sequenceId":10000-index}})).unwrap().into());
        h.state_mut().1.result.rows.push(row);
    }
    h.state_mut().1.generation += 1;
    h.state_mut().1.outcomes.push(serde_json::from_value(serde_json::json!({"requestId":page_id,"state":"Completed","message":"Page loaded"})).unwrap());
    h.run();
    let bounds=h.state().0.controls["condor-plans"];
    h.input_mut().events.extend([
        eframe::egui::Event::PointerMoved(bounds.center()),
        eframe::egui::Event::MouseWheel{unit:eframe::egui::MouseWheelUnit::Point,delta:eframe::egui::vec2(0.,-3000.),modifiers:eframe::egui::Modifiers::NONE},
    ]);
    h.run_steps(8);
    assert!(h.state().2.iter().any(|request| request["intent"]=="plans-page" && request["fields"]["offset"].as_str().and_then(|v|v.parse::<usize>().ok()).is_some_and(|offset|offset>0)), "scrolling must query a new offset");
}

#[test]
fn spread_chart_shows_price_limits_and_forward_spread_without_forward_limit() {
    let mut h = loaded_tabs_harness();
    for name in ["LossLimit", "WinLimit", "ForwardSpread", "NetSpread"] {
        assert!(h.query_all_by_label(name).next().is_some(), "Missing chart legend {name}");
    }
    assert!(h.query_all_by_label("ForwardSpreadLimit").next().is_none());
}

#[test]
fn live_plan_grid_never_requests_database_pages_and_stopped_grid_does() {
    let mut publication=snapshot();
    for (key,value) in [("activeTradeTab","101.701.1901.1501"),("planOffset","0"),
        ("planTotal","10000"),("planDate","2026-10-19"),("latestPlanRevision","101"),
        ("planMaximumRevision","100"),("live","True"),("monitorLive","True")] {
        publication.result.context.insert(key.into(),value.into());
    }
    let mut row:ifm_ui_rust::contract_editor::ContractRow=serde_json::from_value(serde_json::json!({
        "id":"established:1","fields":{"kind":"established"}})).unwrap();
    row.data=Some(ifm_ui_rust::wire::encode_value(&serde_json::json!({
        "id":{"portfolioId":101,"fundId":701,"orderId":1901,"tradeId":1501},"legs":[]})).unwrap().into());
    publication.result.rows.push(row);
    let mut view=TradeWorkspace::default();
    let request:serde_json::Value=serde_json::from_str(&view.request("load-trade",&publication)).unwrap();
    publication.outcomes.push(serde_json::from_value(serde_json::json!({
        "requestId":request["requestId"],"state":"Completed","message":"Loaded"})).unwrap());
    let mut h=Harness::new_state(|ctx,state:&mut(TradeWorkspace,ContractPublication,Vec<serde_json::Value>)|{
        if let Some(request)=state.0.draw(ctx,&state.1){state.2.push(serde_json::from_str(&request).unwrap());}
    },(view,publication,Vec::new()));
    h.run_steps(3);
    assert!(!h.state().2.iter().any(|request|request["intent"]=="plans-page"));
    h.state_mut().1.result.context.insert("live".into(),"False".into());
    h.state_mut().1.result.context.insert("monitorLive".into(),"False".into());
    h.state_mut().1.generation+=1;
    h.run();
    assert!(h.state().2.iter().any(|request|request["intent"]=="plans-page"));
}
