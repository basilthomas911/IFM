use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    contract_editor::ContractPublication, lookup_editor::EditorOutcome, portfolio::PortfolioManager,
};
use std::collections::BTreeMap;

struct State {
    manager: PortfolioManager,
    publication: ContractPublication,
    requests: Vec<serde_json::Value>,
}

#[test]
fn administration_does_not_grow_on_repaint_or_pointer_movement() {
    let mut context = BTreeMap::from([
        ("status".to_owned(), "Portfolio ready".to_owned()),
        (
            "metricsStatus".to_owned(),
            "Metrics status must not be displayed".repeat(30),
        ),
    ]);
    for i in 0..10 {
        context.insert(format!("metric{i}"), "123,456,789,012,345.67".into());
    }
    let bytes = rmp_serde::to_vec_named(&serde_json::json!({
        "grids": [{"key":"portfolios","columns":["Name","OperatingState","PortfolioVersion"],
            "rows":[{"id":"101","fields":{"Name":"Development Portfolio","OperatingState":"Active","PortfolioVersion":"4"}}]}],
        "enabled": {}, "context": context, "dialogs": []
    })).unwrap();
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([("portfolio".into(), bytes.into())]),
                ..Default::default()
            },
            ..Default::default()
        },
        requests: vec![],
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                state.manager.draw(ctx, &state.publication, true);
            },
            state,
        );
    h.run_steps(3);
    let initial = h.state().manager.controls["manager"];
    let controls = &h.state().manager.controls;
    let action_width = controls["portfolio-actions"].width();
    assert!(
        action_width < 172.,
        "Action rail should shrink to its caption width"
    );
    assert!((controls["portfolio-state"].width() - action_width).abs() < 1.);
    assert!((controls["risk-policy-actions"].width() - action_width).abs() < 1.);
    for i in 0..30 {
        h.input_mut().events.push(egui::Event::PointerMoved(
            initial.min + egui::vec2(20. + i as f32 * 10., 100.),
        ));
        h.run_steps(1);
        let current = h.state().manager.controls["manager"];
        assert!(
            (current.width() - initial.width()).abs() < 1.,
            "Width grew: {initial:?} -> {current:?}"
        );
        assert!(
            (current.height() - initial.height()).abs() < 1.,
            "Height grew: {initial:?} -> {current:?}"
        );
    }
    assert!(h.query_by_label("Portfolio ready").is_some());
    assert!(
        h.query_by_label("Metrics status must not be displayed")
            .is_none()
    );
}

#[test]
fn funds_select_exact_version_from_any_column_in_the_row() {
    let ids = [
        r#"["Same Fund","2","Active"]"#,
        r#"["Same Fund","1","Disabled"]"#,
    ];
    let bytes=rmp_serde::to_vec_named(&serde_json::json!({
        "context":{"fundFilter":"All"}, "enabled":{},"dialogs":[],
        "grids":[{"key":"funds","columns":["Name","OperatingState","FundMandateVersion"],
            "rows":[{"id":ids[0],"fields":{"Name":"Same Fund","OperatingState":"Active","FundMandateVersion":"2"}},
                    {"id":ids[1],"fields":{"Name":"Same Fund","OperatingState":"Disabled","FundMandateVersion":"1"}}]}]
    })).unwrap();
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([("portfolio".into(), bytes.into())]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    h.run();
    h.get_by_label("Funds").click();
    h.run();
    assert!(h.query_by_label("Show State").is_none());
    for (index, id) in ids.iter().enumerate() {
        let rect = h.state().manager.controls[&format!("row-funds-{id}")];
        let pos = egui::pos2(
            if index == 0 {
                rect.left() + 15.
            } else {
                rect.right() - 15.
            },
            rect.center().y,
        );
        h.input_mut().events.extend([
            egui::Event::PointerMoved(pos),
            egui::Event::PointerButton {
                pos,
                button: egui::PointerButton::Primary,
                pressed: true,
                modifiers: Default::default(),
            },
        ]);
        h.run_steps(1);
        h.input_mut().events.push(egui::Event::PointerButton {
            pos,
            button: egui::PointerButton::Primary,
            pressed: false,
            modifiers: Default::default(),
        });
        h.run();
        let request = h.state().requests.last().unwrap();
        assert_eq!(request["intent"], "portfolio-fund");
        assert_eq!(request["originalId"], *id);
    }
}

#[test]
#[ignore = "Requires the managed PortfolioMigrationTests wire fixtures and a graphics device"]
fn administration_embeds_policy_below_grid_with_actions_on_the_right() {
    let output =
        std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../.artifacts/portfolio-migration");
    let bytes = std::fs::read(output.join("administration.msgpack")).unwrap();
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([("portfolio".into(), bytes.into())]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();
    let controls = &h.state().manager.controls;
    let bounds = controls["manager"];
    let grid = controls["grid-portfolios"];
    let policy = controls["inline-risk-policy"];
    assert!((grid.height() - bounds.height() / 3.).abs() < 1.);
    assert!(controls["portfolio-state"].left() > grid.right());
    assert!(controls["portfolio-actions"].top() >= controls["portfolio-state"].bottom());
    assert!(
        policy.top() > grid.bottom() && policy.bottom() <= controls["metrics-and-status"].top()
    );
    let name_width = controls["header-portfolios-Name"].width();
    for (key, rect) in controls {
        if key.starts_with("header-portfolios-") && key != "header-portfolios-Name" {
            assert!(name_width > rect.width());
        }
    }
    assert!(controls["risk-policy-actions"].left() > controls["risk-policy-tabs"].right());
    for label in ["Save", "Cancel"] {
        let button = controls[&format!("risk-policy.button.{label}")];
        assert!(button.top() >= controls["risk-policy-actions"].bottom());
        assert!(button.left() > controls["risk-policy-tabs"].right());
        assert!((button.width() - controls["risk-policy-actions"].width()).abs() < 1.);
        assert!(bounds.contains_rect(button));
    }
    assert!(
        controls["risk-policy.button.Cancel"].top() >= controls["risk-policy.button.Save"].bottom()
    );
    assert!(h.query_by_label("New Policy").is_none());
    assert!(h.query_by_label("Versions").is_some());
    assert!(h.query_by_label("Risk Policy...").is_none());
    h.render()
        .unwrap()
        .save(output.join("rust-administration-inline.png"))
        .unwrap();
    h.get_by_label("Versions").click();
    h.run();
    assert!(h.state().manager.controls.contains_key("grid-policies"));
    assert!(
        !h.state()
            .manager
            .controls
            .contains_key("grid-family-limits")
    );
    h.render()
        .unwrap()
        .save(output.join("rust-policy-versions.png"))
        .unwrap();
    h.get_by_label("Risk Policy Actions").click();
    h.run();
    for label in [
        "New Policy...",
        "Change Policy...",
        "New Version",
        "Activate & Assign",
        "Retire",
        "Delete Draft",
    ] {
        assert!(h.query_by_label(label).is_some());
    }
    let controls = &h.state().manager.controls;
    assert!(
        controls["risk-policy-menu-New Policy"].top()
            < controls["risk-policy-menu-Change Policy..."].top()
    );
    assert!(
        controls["risk-policy-menu-Change Policy..."].top()
            < controls["risk-policy-menu-New Version"].top()
    );
    h.get_by_label("Change Policy...").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["fields"]["action"],
        "Change Policy..."
    );
    assert!(
        h.state()
            .manager
            .controls
            .contains_key("grid-family-limits")
    );
    assert!(!h.state().manager.controls.contains_key("grid-policies"));
    h.get_by_label("Portfolio Actions").click();
    h.run();
    let controls = &h.state().manager.controls;
    assert!(
        controls["portfolio-menu-new-portfolio"].top()
            < controls["portfolio-menu-version-portfolio"].top()
    );
    h.get_by_label("New Portfolio...").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["fields"]["view"],
        "new-portfolio"
    );
}

#[test]
#[ignore = "Requires the managed PortfolioMigrationTests wire fixtures and a graphics device"]
fn managed_portfolio_documents_render_all_controls_within_the_original_dialog_dimensions() {
    let output =
        std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../.artifacts/portfolio-migration");
    for view in [
        "new-portfolio",
        "version-portfolio",
        "new-fund",
        "version-fund",
        "risk-history",
        "account-version",
        "rule-version",
        "portfolio-state",
        "delete-portfolio",
        "fund-state",
        "allocation",
        "envelope",
        "assignment",
        "risk-policy",
        "financials",
        "ledger",
        "ledger-nested",
        "posting",
        "setup",
        "authority",
    ] {
        let bytes = std::fs::read(output.join(format!("{view}.msgpack"))).unwrap();
        let document: serde_json::Value = ifm_ui_egui::wire::decode(&bytes).unwrap();
        let dialog = document["dialogs"].as_array().unwrap().last().unwrap();
        let key = dialog["key"].as_str().unwrap();
        let mut manager = PortfolioManager::default();
        manager.open();
        let state = State {
            manager,
            publication: ContractPublication {
                generation: 7,
                result: ifm_ui_egui::contract_editor::ContractResult {
                    view: "portfolio".into(),
                    documents: BTreeMap::from([("portfolio".into(), bytes.into())]),
                    ..Default::default()
                },
                ..Default::default()
            },
            requests: vec![],
        };
        let mut h = Harness::builder()
            .with_size(egui::vec2(1800., 1200.))
            .with_max_steps(40)
            .build_state(
                |ctx, state: &mut State| {
                    if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                        let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                        state.publication.outcomes.push(EditorOutcome {
                            request_id: request["requestId"].as_str().unwrap().into(),
                            state: "Completed".into(),
                            message: String::new(),
                        });
                        state.requests.push(request);
                    }
                },
                state,
            );
        ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
        h.run();
        assert!(
            h.query_by_label("Portfolio Error").is_none(),
            "{view}: wire decode failed"
        );
        let bounds = h.state().manager.controls[&format!("dialog-{key}")];
        assert!(
            (bounds.width() - (dialog["width"].as_f64().unwrap() as f32 - 16.)).abs() < 12.,
            "{view}: dialog width grew beyond its WinForms baseline: {bounds:?}"
        );
        if key == "financials" {
            h.get_by_label("Financial Actions").click();
            h.run();
        }
        for button in dialog["buttons"].as_array().unwrap() {
            let name = button.as_str().unwrap();
            // These old managed documents retain toolbar metadata that the
            // approved current Rust layout intentionally does not render.
            // The later inline-fund and ledger tests assert the same absence.
            let removed = key == "risk-history"
                || (key == "ledger" && matches!(name, "Refresh" | "Close"))
                || (key == "financials" && name == "Refresh");
            if removed {
                assert!(!h.state().manager.controls.contains_key(&format!("{key}.button.{name}")),
                    "{view}: obsolete {name} unexpectedly returned");
                continue;
            }
            assert!(
                h.state()
                    .manager
                    .controls
                    .contains_key(&format!("{key}.button.{name}")),
                "{view}: missing {name}"
            );
        }
        for field in dialog["fields"].as_array().unwrap() {
            let name = field["key"].as_str().unwrap();
            if matches!(key, "financials" | "risk-history") {
                continue;
            }
            let rect = h.state().manager.controls[&format!("{key}.{name}")];
            assert!(
                rect.right() <= bounds.right() + 2.,
                "{view}.{name}: {rect:?} exceeds {bounds:?}"
            );
            assert!(
                rect.bottom() <= bounds.bottom() + 2.,
                "{view}.{name}: {rect:?} exceeds {bounds:?}"
            );
        }
        h.render()
            .unwrap()
            .save(output.join(format!("rust-{view}.png")))
            .unwrap();
        if key == "ledger" {
            assert!(
                !h.state()
                    .manager
                    .controls
                    .contains_key("ledger.button.Close")
            );
            assert!(
                !h.state()
                    .manager
                    .controls
                    .contains_key("ledger.button.Refresh")
            );
            let grid = h.state().manager.controls["ledger.grid-area"];
            for field in [
                "control", "period", "start", "end", "account", "rule", "reason",
            ] {
                assert!(
                    h.state().manager.controls[&format!("ledger.{field}")].top() >= grid.bottom()
                );
            }
        }
    }
}

#[test]
fn funds_embed_financial_and_risk_tabs_below_grid_with_equal_width_action_controls() {
    let fund_id = r#"["Fixture Fund","1","Active"]"#;
    let document = serde_json::json!({
        "context":{"fundFilter":"Active","status":"Fund ready"},
        "enabled":{"Create Fund...":true,"Change Fund...":true,"Change Fund State...":true},
        "dialogs":[],
        "grids":[{"key":"funds","columns":["FundId","Name","Portfolio","OperatingState","FundMandateVersion"],
            "selected":fund_id,"rows":[{"id":fund_id,"fields":{"FundId":"701","Name":"Fixture Fund",
                "Portfolio":"Fixture Portfolio","OperatingState":"Active","FundMandateVersion":"1"}}]}],
        "fundView":{"key":"financials","title":"Fixture Fund â€” Financials","width":1150,"height":700,
            "captionWidth":170,"fields":[
                {"key":"cash","caption":"Available Cash:","kind":"label","value":"12,345.00 USD"},
                {"key":"pendingWithdrawals","caption":"Pending Withdrawals:","kind":"label","value":"500.00 USD"},
                {"key":"financialState","kind":"label","value":"Active"}],
            "buttons":["Next transactions","Next reservations","Transaction","Ledger","Close"],
            "buttonEnabled":{"Next transactions":true,"Next reservations":true,"Transaction":true,"Ledger":true},"grids":[{"key":"balances","columns":["Balance"],
                "rows":[{"id":"balance","fields":{"Balance":"12,345.00"}}]}]}
    });
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([(
                    "portfolio".into(),
                    ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                )]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();
    h.get_by_label("Funds").click();
    h.run();
    let controls = &h.state().manager.controls;
    let bounds = controls["manager"];
    assert!(
        controls.contains_key("grid-funds"),
        "Controls: {:?}; requests: {:?}",
        controls.keys().collect::<Vec<_>>(),
        h.state().requests
    );
    let grid = controls["grid-funds"];
    let tabs = controls["fund-tabs"];
    let filter = controls["fund-state-filter"];
    let actions = controls["fund-actions"];
    assert!((grid.height() - bounds.height() / 3.).abs() < 1.);
    assert!(filter.left() > grid.right());
    assert!(actions.top() >= filter.bottom());
    assert!(
        (filter.width() - actions.width()).abs() < 1.,
        "State/actions width mismatch: {filter:?}, {actions:?}"
    );
    assert!(tabs.top() > grid.bottom());
    assert!(tabs.bottom() <= controls["metrics-and-status"].top());
    assert!(tabs.contains_rect(controls["dialog-financials"]));
    assert!(h.query_by_label("Show State").is_none());
    assert!(h.query_by_label("Financials...").is_none());
    assert!(h.query_by_label("Risk History...").is_none());
    assert!(!controls.contains_key("financials.button.Close"));
    assert!(h.query_by_label("Available Cash:").is_some());
    assert!(h.query_by_label("Pending Withdrawals:").is_some());
    assert!(h.query_by_label("12,345.00 USD").is_some());
    assert!(h.query_by_label("500.00 USD").is_some());
    assert!(controls["financial-actions"].left() > controls["financial-tabs"].right());
    assert!((controls["financial-actions"].top() - controls["grid-balances"].top()).abs() < 1.);
    assert!(h.query_by_label("Refresh").is_none());
    for (label, name) in [
        ("Next Transactions", "Next transactions"),
        ("Next Reservations", "Next reservations"),
        ("Transaction...", "Transaction"),
        ("Ledger...", "Ledger"),
    ] {
        h.get_by_label("Financial Actions").click();
        h.run();
        assert!(h.query_by_label("Refresh").is_none());
        h.get_by_label(label).click();
        h.run();
        let request = h.state().requests.last().unwrap();
        assert_eq!(request["intent"], "portfolio-financial");
        assert_eq!(request["fields"]["action"], name);
    }

    if let Ok(output) = std::env::var("IFM_CAPTURE_FUNDS_LAYOUT")
        && std::env::var("IFM_CAPTURE_FUNDS_TAB").as_deref() != Ok("risk")
    {
        std::fs::create_dir_all(&output).unwrap();
        h.render()
            .unwrap()
            .save(std::path::Path::new(&output).join("financials.png"))
            .unwrap();
    }
    h.get_by_label("Fund Actions").click();
    h.run();
    for label in ["Create Fund...", "Change Fund...", "Change Fund State..."] {
        assert!(h.query_by_label(label).is_some(), "Missing {label}");
    }
    h.get_by_label("Change Fund State...").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["fields"]["view"],
        "fund-state"
    );
    assert_eq!(
        h.state().requests.last().unwrap()["intent"],
        "portfolio-open"
    );

    h.get_by_label("Risk History").click();
    h.run();
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "portfolio-fund-view");
    assert_eq!(request["fields"]["view"], "risk-history");
    assert_eq!(request["originalId"], fund_id);
    // A failed/missing reply must not cause a request loop on every repaint.
    let count = h.state().requests.len();
    h.run_steps(10);
    assert_eq!(h.state().requests.len(), count);

    let mut risk_document = document.clone();
    risk_document["fundView"] = serde_json::json!({
        "key":"risk-history","title":"Risk history","width":1300,"height":850,"captionWidth":160,
        "fields":[{"key":"date","caption":"Evaluation date (UTC)","kind":"date","value":"2026-10-18","enabled":true},
                  {"key":"decision","kind":"multiline","value":"Current risk decision"},
                  {"key":"evidence","kind":"multiline","value":"Current risk evidence"}],
        "buttons":["Refresh","Next page"],
        "grids":[{"key":"risk-history","columns":["Outcome"],"rows":[{"id":"invocation","fields":{"Outcome":"Accepted"}}]}]
    });
    h.state_mut().publication.generation += 1;
    h.state_mut().publication.result.documents.insert(
        "portfolio".into(),
        ifm_ui_egui::wire::encode_value(&risk_document)
            .unwrap()
            .into(),
    );
    h.run();
    let controls = &h.state().manager.controls;
    assert!(controls["fund-tabs"].contains_rect(controls["dialog-risk-history"]));
    assert!(controls.contains_key("grid-risk-history"));
    assert!(!controls.contains_key("risk-history.button.Refresh"));
    assert!(!controls.contains_key("risk-history.button.Next page"));
    let caption = controls["risk-history.date.caption"];
    let date = controls["risk-history.date"];
    assert!((caption.left() - controls["dialog-risk-history"].left()).abs() < 1.);
    assert!(date.left() > caption.right() && date.left() - caption.right() < 10.);
    assert!((date.center().y - caption.center().y).abs() < 6.);
    assert!(h.query_by_label("Evaluation Date:").is_some());
    assert!(!controls.contains_key("grid-balances"));
    assert!(h.query_by_label("Evidence").is_some());
    h.run_steps(3);
    if let Ok(output) = std::env::var("IFM_CAPTURE_FUNDS_LAYOUT")
        && std::env::var("IFM_CAPTURE_FUNDS_TAB").as_deref() == Ok("risk")
    {
        std::fs::create_dir_all(&output).unwrap();
        h.render()
            .unwrap()
            .save(std::path::Path::new(&output).join("risk-history.png"))
            .unwrap();
    }
    assert!(h.query_by_label("Refresh").is_none());
    assert!(h.query_by_label("Next page").is_none());
}

#[test]
fn create_fund_is_compact_fixed_and_saves_a_single_column_checked_list() {
    verify_compact_fund_layout("new-fund", "Create Fund");
}

#[test]
fn change_fund_uses_compact_layout_and_saves_checked_deployments() {
    verify_compact_fund_layout("version-fund", "Change Fund");
}

fn verify_compact_fund_layout(dialog_key: &str, title: &str) {
    let mut fields = vec![
        serde_json::json!({"key":"FundId","caption":"Fund ID","value":"902","enabled":false}),
        serde_json::json!({"key":"Name","caption":"Name","value":"New Fund","enabled":true}),
        serde_json::json!({"key":"TradingYear","caption":"Trading Year","value":"2026","kind":"number","minimum":2000,"maximum":2200,"decimals":0,"enabled":true}),
        serde_json::json!({"key":"OperatingState","caption":"Operating State","value":"Draft","kind":"choice","enabled":false,"choices":[{"value":"Draft","label":"Draft","enabled":true}]}),
        serde_json::json!({"key":"DecisionHorizon","caption":"Decision Horizon","value":"Daily","kind":"choice","enabled":true,"choices":[{"value":"Daily","label":"Daily","enabled":true}]}),
        serde_json::json!({"key":"Objective","caption":"Objective","value":"","enabled":true}),
        serde_json::json!({"key":"UnderlyingUniverse","caption":"Underlyings","value":"ES","kind":"multi","enabled":true,"choices":[{"value":"ES","label":"ES","enabled":true}]}),
        serde_json::json!({"key":"EligibleAssetTypes","caption":"Asset Types","value":"FuturesOption","kind":"multi","enabled":true,"choices":[{"value":"FuturesOption","label":"Futures Option","enabled":true}]}),
        serde_json::json!({"key":"PermittedDirections","caption":"Directions","value":"","kind":"multi","enabled":true,"choices":[]}),
        serde_json::json!({"key":"PermittedConditions","caption":"Market Conditions","value":"","kind":"multi","enabled":true,"choices":[]}),
        serde_json::json!({"key":"families","caption":"Permitted Strategy Deployments","value":"A@1","kind":"checklist","enabled":true,
            "choices":[{"value":"A@1","label":"Deployment A","enabled":true},
                       {"value":"B@1","label":"Deployment B","enabled":true},
                       {"value":"C@1","label":"Deployment C","enabled":true}]}),
    ];
    for index in 4..=12 {
        fields.last_mut().unwrap()["choices"].as_array_mut().unwrap().push(
            serde_json::json!({"value":format!("{index}@1"),"label":format!("Deployment {index}"),"enabled":true}));
    }
    let document = serde_json::json!({"context":{},"grids":[],"enabled":{},
        "dialogs":[{"key":dialog_key,"title":title,"width":780,"height":560,"captionWidth":225,
            "fields":fields,"buttons":["Save","Cancel"]}]});
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([(
                    "portfolio".into(),
                    ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                )]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();
    let controls = &h.state().manager.controls;
    let bounds = controls[&format!("dialog-{dialog_key}")];
    let window = controls[&format!("window-{dialog_key}")];
    assert!(window.height() < 580.);
    assert!(window.width() < 800.);
    let save = controls[&format!("{dialog_key}.button.Save")];
    let cancel = controls[&format!("{dialog_key}.button.Cancel")];
    assert!((save.center().y - cancel.center().y).abs() < 1.);
    assert!(((save.left() + cancel.right()) / 2. - bounds.center().x).abs() < 1.);
    assert!(bounds.bottom() - save.bottom() < 16.);
    for field in &fields {
        let key = field["key"].as_str().unwrap();
        let caption = controls[&format!("{dialog_key}.{key}.caption")];
        let input = controls[&format!("{dialog_key}.{key}")];
        assert!(
            (input.left() - caption.right() - 20.).abs() < 1.,
            "{key} caption gap: {}",
            input.left() - caption.right()
        );
        assert!(
            bounds.contains_rect(input),
            "{key} escapes dialog: {input:?} vs {bounds:?}"
        );
        assert!(input.bottom() < save.top(), "{key} overlaps buttons");
        assert!(
            input.bottom() <= save.top() - 20. + 1.,
            "{key} is clipped by the form footer"
        );
        assert!(
            h.query_by_label(&format!("{}:", field["caption"].as_str().unwrap()))
                .is_some()
        );
    }
    let deployment_list = controls[&format!("{dialog_key}.families")];
    assert!(deployment_list.height() >= 180.);
    assert!((save.top() - deployment_list.bottom() - 20.).abs() < 1.);
    assert!((cancel.top() - deployment_list.bottom() - 20.).abs() < 1.);
    let rows =
        ["A@1", "B@1", "C@1"].map(|key| controls[&format!("{dialog_key}.families.choice.{key}")]);
    assert!(rows[1].top() >= rows[0].bottom() && rows[2].top() >= rows[1].bottom());
    assert!(
        rows.iter()
            .all(|row| (row.left() - rows[0].left()).abs() < 1.)
    );
    h.get_by_label("Deployment A").click();
    h.run();
    h.get_by_label("Deployment B").click();
    h.run();
    if let Ok(output) = std::env::var("IFM_CAPTURE_CREATE_FUND") {
        std::fs::create_dir_all(&output).unwrap();
        h.render()
            .unwrap()
            .save(std::path::Path::new(&output).join(format!("{dialog_key}.png")))
            .unwrap();
    }
    let list = h.state().manager.controls[&format!("{dialog_key}.families")];
    assert!(
        h.state().manager.controls[&format!("{dialog_key}.families.choice.12@1")].bottom()
            > list.bottom()
    );
    h.input_mut().events.extend([
        egui::Event::PointerMoved(list.center()),
        egui::Event::MouseWheel {
            unit: egui::MouseWheelUnit::Point,
            delta: egui::vec2(0., -600.),
            modifiers: Default::default(),
        },
    ]);
    h.run();
    assert!(
        list.contains_rect(
            h.state().manager.controls[&format!("{dialog_key}.families.choice.12@1")]
        ),
        "Last deployment must be reachable inside the list border"
    );
    let corner = window.max - egui::vec2(2., 2.);
    h.input_mut().events.extend([
        egui::Event::PointerMoved(corner),
        egui::Event::PointerButton {
            pos: corner,
            button: egui::PointerButton::Primary,
            pressed: true,
            modifiers: Default::default(),
        },
    ]);
    h.run_steps(1);
    let moved = corner + egui::vec2(100., 100.);
    h.input_mut().events.push(egui::Event::PointerMoved(moved));
    h.run_steps(1);
    h.input_mut().events.push(egui::Event::PointerButton {
        pos: moved,
        button: egui::PointerButton::Primary,
        pressed: false,
        modifiers: Default::default(),
    });
    h.run();
    let resized = h.state().manager.controls[&format!("window-{dialog_key}")];
    assert!((resized.width() - window.width()).abs() < 1.);
    assert!((resized.height() - window.height()).abs() < 1.);
    h.get_by_label("Save").click();
    h.run();
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "portfolio-save");
    assert_eq!(request["fields"]["families"], "B@1");
    h.get_by_label("Cancel").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["intent"],
        "portfolio-cancel"
    );
}

#[test]
fn fund_details_actions_align_with_headers_and_property_captions_precede_values() {
    let document = serde_json::json!({
        "context":{"fundName":"Lifecycle Fund","portfolioName":"Trading Portfolio"},
        "enabled":{"Allocation...":true,"Risk Envelope...":true,"Trade Assignment...":true},
        "dialogs":[],
        "grids":[
            {"key":"fund","columns":["Property","Value"],"rows":[{"id":"0","fields":{"Property":"Name","Value":"Lifecycle Fund"}},{"id":"1","fields":{"Property":"Trading year","Value":"2026"}}]},
            {"key":"allocation","columns":["Property","Value"],"rows":[{"id":"0","fields":{"Property":"Allocation","Value":"1000"}}]},
            {"key":"envelope","columns":["Property","Value"],"rows":[{"id":"0","fields":{"Property":"Risk budget","Value":"100"}}]},
            {"key":"assignments","columns":["TradeId"],"rows":[]}
        ]
    });
    let mut manager = PortfolioManager::default();
    manager.open();
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            State {
                manager,
                requests: vec![],
                publication: ContractPublication {
                    generation: 7,
                    result: ifm_ui_egui::contract_editor::ContractResult {
                        view: "portfolio".into(),
                        documents: BTreeMap::from([(
                            "portfolio".into(),
                            ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                        )]),
                        ..Default::default()
                    },
                    ..Default::default()
                },
            },
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();
    h.get_by_label("Fund Details").click();
    h.run();
    assert!(h.query_by_label("Fund:").is_some());
    assert!(h.query_by_label("Trading Portfolio").is_some());
    assert!(h.query_by_label("Allocation...").is_none());
    for (tab, grid) in [
        ("Fund", "fund"),
        ("Allocation", "allocation"),
        ("Risk", "envelope"),
        ("Assignments", "assignments"),
    ] {
        h.get_by_label(tab).click();
        h.run();
        let controls = &h.state().manager.controls;
        let button = controls["fund-detail-actions"];
        let header = controls[&format!("grid-{grid}")];
        assert!((button.top() - header.top()).abs() < 1.);
        assert!(button.left() > header.right());
        if grid != "assignments" {
            let caption = controls[&format!("property-{grid}-0")];
            let value = controls[&format!("value-{grid}-0")];
            assert!(
                (value.top() - caption.top()).abs() < 1.,
                "Property and value must share a baseline"
            );
            assert!(
                (value.left() - caption.right() - 20.).abs() < 1.,
                "Property gap: {}",
                value.left() - caption.right()
            );
        }
    }
    h.get_by_label("Fund").click();
    h.run();
    if let Ok(output) = std::env::var("IFM_CAPTURE_FUND_DETAILS") {
        std::fs::create_dir_all(&output).unwrap();
        h.render()
            .unwrap()
            .save(std::path::Path::new(&output).join("fund-details.png"))
            .unwrap();
    }
    for (label, view) in [
        ("Allocation...", "allocation"),
        ("Risk Envelope...", "envelope"),
        ("Trade Assignment...", "assignment"),
    ] {
        h.get_by_label("Fund Detail Actions").click();
        h.run();
        h.get_by_label(label).click();
        h.run();
        let request = h.state().requests.last().unwrap();
        assert_eq!(request["intent"], "portfolio-open");
        assert_eq!(request["fields"]["view"], view);
    }
}

#[test]
fn create_portfolio_uses_compact_creation_layout_and_preserves_save_cancel() {
    verify_portfolio_creation_layout("new-portfolio", "Create Portfolio");
}

#[test]
fn create_portfolio_version_uses_compact_creation_layout_and_preserves_save_cancel() {
    verify_portfolio_creation_layout("version-portfolio", "Create Portfolio Version");
}

#[test]
fn change_portfolio_state_uses_compact_layout_and_preserves_apply_cancel() {
    verify_portfolio_creation_layout("portfolio-state", "Change Portfolio State");
}

#[test]
fn change_fund_state_uses_compact_layout_and_preserves_apply_cancel() {
    verify_portfolio_creation_layout("fund-state", "Change Fund State");
}

fn verify_portfolio_creation_layout(dialog_key: &str, title: &str) {
    let changing_state = matches!(dialog_key, "portfolio-state" | "fund-state");
    let save_label = if changing_state { "Apply" } else { "Save" };
    let fields = if changing_state {
        vec![
            serde_json::json!({"key":"state","caption":"Target State","value":"Paused","kind":"choice","enabled":true,"choices":[{"value":"Active","label":"Active","enabled":true},{"value":"Paused","label":"Paused","enabled":true}]}),
            serde_json::json!({"key":"reason","caption":"Reason","value":"Pause for maintenance","enabled":true}),
        ]
    } else {
        vec![
            serde_json::json!({"key":"PortfolioId","caption":"Portfolio ID","value":"102","enabled":false}),
            serde_json::json!({"key":"Name","caption":"Name","value":"New Portfolio","enabled":true}),
            serde_json::json!({"key":"BaseCurrency","caption":"Base Currency","value":"USD","kind":"choice","enabled":true,"choices":[{"value":"USD","label":"USD","enabled":true},{"value":"CAD","label":"CAD","enabled":true}]}),
            serde_json::json!({"key":"OperatingState","caption":"Operating State","value":"Draft","kind":"choice","enabled":true,"choices":[{"value":"Draft","label":"Draft","enabled":true}]}),
            serde_json::json!({"key":"BrokerAccountRefs","caption":"Broker Accounts (CSV)","value":"","enabled":true}),
            serde_json::json!({"key":"EffectiveFromUtc","caption":"Effective From","value":"2026-10-18T12:00:00Z","kind":"datetime","enabled":true}),
        ]
    };
    let document = serde_json::json!({"context":{},"grids":[],"enabled":{},
        "dialogs":[{"key":dialog_key,"title":title,"width":if changing_state {520}else{640},"height":if changing_state {150}else{260},"captionWidth":if changing_state {140}else{180},
            "fields":fields,"buttons":[save_label,"Cancel"]}]});
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([(
                    "portfolio".into(),
                    ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                )]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();
    // Raising an ancestor must not bury its still-open modal child.
    h.ctx.move_to_top(egui::LayerId::new(
        egui::Order::Middle,
        egui::Id::new("portfolio-administration"),
    ));
    h.run();
    let child_layer = egui::LayerId::new(
        egui::Order::Middle,
        egui::Id::new(("portfolio-dialog", dialog_key)),
    );
    let child_center = h.state().manager.controls[&format!("window-{dialog_key}")].center();
    assert_eq!(
        h.ctx.layer_id_at(child_center),
        Some(child_layer),
        "Modal child was hidden behind its disabled parent"
    );
    let controls = &h.state().manager.controls;
    let bounds = controls[&format!("dialog-{dialog_key}")];
    let window = controls[&format!("window-{dialog_key}")];
    assert!(window.height() < if changing_state { 170. } else { 280. });
    assert!(window.width() < 660.);
    let save = controls[&format!("{dialog_key}.button.{save_label}")];
    let cancel = controls[&format!("{dialog_key}.button.Cancel")];
    assert!((save.center().y - cancel.center().y).abs() < 1.);
    assert!(((save.left() + cancel.right()) / 2. - bounds.center().x).abs() < 1.);
    assert!(bounds.bottom() - save.bottom() < 16.);
    let last_key = fields.last().unwrap()["key"].as_str().unwrap();
    let last_control = controls[&format!("{dialog_key}.{last_key}")];
    assert!((save.top() - last_control.bottom() - 20.).abs() < 1.);
    assert!((cancel.top() - last_control.bottom() - 20.).abs() < 1.);
    for field in &fields {
        let key = field["key"].as_str().unwrap();
        let caption = controls[&format!("{dialog_key}.{key}.caption")];
        let input = controls[&format!("{dialog_key}.{key}")];
        assert!(
            (input.left() - caption.right() - 20.).abs() < 1.,
            "{key} caption gap: {}",
            input.left() - caption.right()
        );
        assert!(
            bounds.contains_rect(input),
            "{key} escapes dialog: {input:?} vs {bounds:?}"
        );
        assert!(input.bottom() < save.top(), "{key} overlaps buttons");
        assert!(
            input.bottom() <= save.top() - 20. + 1.,
            "{key} is clipped by the form footer"
        );
        assert!(
            h.query_by_label(&format!("{}:", field["caption"].as_str().unwrap()))
                .is_some()
        );
    }
    if let Ok(output) = std::env::var("IFM_CAPTURE_CREATE_PORTFOLIO") {
        std::fs::create_dir_all(&output).unwrap();
        h.render()
            .unwrap()
            .save(std::path::Path::new(&output).join(format!("{dialog_key}.png")))
            .unwrap();
    }
    if !changing_state {
        let currency = h.state().manager.controls[&format!("{dialog_key}.BaseCurrency")].center();
        h.input_mut().events.extend([
            egui::Event::PointerMoved(currency),
            egui::Event::PointerButton {
                pos: currency,
                button: egui::PointerButton::Primary,
                pressed: true,
                modifiers: Default::default(),
            },
        ]);
        h.run_steps(1);
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: currency,
            button: egui::PointerButton::Primary,
            pressed: false,
            modifiers: Default::default(),
        });
        h.run();
        h.get_by_label("CAD").click();
        h.run();
    }
    let corner = window.max - egui::vec2(2., 2.);
    h.input_mut().events.extend([
        egui::Event::PointerMoved(corner),
        egui::Event::PointerButton {
            pos: corner,
            button: egui::PointerButton::Primary,
            pressed: true,
            modifiers: Default::default(),
        },
    ]);
    h.run_steps(1);
    let moved = corner + egui::vec2(100., 100.);
    h.input_mut().events.push(egui::Event::PointerMoved(moved));
    h.run_steps(1);
    h.input_mut().events.push(egui::Event::PointerButton {
        pos: moved,
        button: egui::PointerButton::Primary,
        pressed: false,
        modifiers: Default::default(),
    });
    h.run();
    let resized = h.state().manager.controls[&format!("window-{dialog_key}")];
    assert!((resized.width() - window.width()).abs() < 1.);
    assert!((resized.height() - window.height()).abs() < 1.);
    h.get_by_label(save_label).click();
    h.run();
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "portfolio-save");
    if changing_state {
        assert_eq!(request["fields"]["state"], "Paused");
        assert_eq!(request["fields"]["reason"], "Pause for maintenance");
    } else {
        assert_eq!(request["fields"]["Name"], "New Portfolio");
        assert_eq!(request["fields"]["BaseCurrency"], "CAD");
    }
    h.get_by_label("Cancel").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["intent"],
        "portfolio-cancel"
    );
    // Apply the backend's close acknowledgement and prove the parent accepts input again.
    let mut closed_document = document.clone();
    closed_document["dialogs"] = serde_json::json!([]);
    h.state_mut().publication.generation += 1;
    h.state_mut().publication.result.documents.insert(
        "portfolio".into(),
        ifm_ui_egui::wire::encode_value(&closed_document)
            .unwrap()
            .into(),
    );
    h.run();
    assert!(
        !h.state()
            .manager
            .controls
            .contains_key(&format!("window-{dialog_key}"))
    );
    h.get_by_label("Funds").click();
    h.run();
    assert!(
        h.state().manager.controls.contains_key("fund-actions"),
        "Parent remained disabled after the child closed"
    );
}

#[test]
fn fund_transaction_uses_compact_layout_without_inline_status_and_routes_financial_actions() {
    let fields = vec![
        serde_json::json!({"key":"rule","caption":"Transaction","value":"deposit","kind":"choice","enabled":true,"choices":[{"value":"deposit","label":"Confirmed deposit (version 1)","enabled":true}]}),
        serde_json::json!({"key":"amount","caption":"Amount (USD)","value":"100","kind":"number","minimum":0.01,"maximum":1000000000,"enabled":true}),
        serde_json::json!({"key":"accountingDate","caption":"Accounting date","value":"2026-10-18","kind":"date","enabled":true}),
        serde_json::json!({"key":"valueDate","caption":"Value date","value":"2026-10-18","kind":"date","enabled":true}),
        serde_json::json!({"key":"description","caption":"Description","value":"Test transfer","kind":"multiline","enabled":true}),
        serde_json::json!({"key":"reference","caption":"Movement reference","value":"LOCAL-TEST","enabled":true}),
        serde_json::json!({"key":"confirmed","caption":"I confirm this cash movement","value":"true","kind":"check","enabled":true}),
        serde_json::json!({"key":"original","caption":"Original transaction","value":"None selected","enabled":false}),
    ];
    let mut document = serde_json::json!({"context":{},"grids":[],"enabled":{},"dialogs":[{
        "key":"posting","title":"Fund Transaction","width":660,"height":360,"captionWidth":280,
        "fields":fields,"buttons":["Post","Close"],"buttonEnabled":{"Post":false,"Close":true},"status":"Hidden transaction status"
    }]});
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([(
                    "portfolio".into(),
                    ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                )]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();

    assert!(h.query_by_label("Hidden transaction status").is_none());
    let controls = &h.state().manager.controls;
    let bounds = controls["dialog-posting"];
    let window = controls["window-posting"];
    assert!(window.height() < 380. && window.width() < 680.);
    let post = controls["posting.button.Post"];
    let close = controls["posting.button.Close"];
    assert!((post.top() - controls["posting.original"].bottom() - 20.).abs() < 1.);
    assert!((post.center().y - close.center().y).abs() < 1.);
    assert!(((post.left() + close.right()) / 2. - bounds.center().x).abs() < 1.);
    for field in &fields {
        let key = field["key"].as_str().unwrap();
        let caption = controls[&format!("posting.{key}.caption")];
        let input = controls[&format!("posting.{key}")];
        assert!(
            (input.left() - caption.right() - 20.).abs() < 1.,
            "Caption gap for {key}: {}",
            input.left() - caption.right()
        );
        assert!(
            bounds.contains_rect(input),
            "{key} escapes dialog: {input:?} vs {bounds:?}"
        );
        assert!(input.bottom() <= post.top() - 19.);
    }
    if let Ok(output) = std::env::var("IFM_CAPTURE_FUND_TRANSACTION") {
        std::fs::create_dir_all(&output).unwrap();
        h.render()
            .unwrap()
            .save(std::path::Path::new(&output).join("fund-transaction.png"))
            .unwrap();
    }
    let requests_before = h.state().requests.len();
    h.input_mut().events.extend([
        egui::Event::PointerMoved(post.center()),
        egui::Event::PointerButton {
            pos: post.center(),
            button: egui::PointerButton::Primary,
            pressed: true,
            modifiers: Default::default(),
        },
    ]);
    h.run_steps(1);
    h.input_mut().events.push(egui::Event::PointerButton {
        pos: post.center(),
        button: egui::PointerButton::Primary,
        pressed: false,
        modifiers: Default::default(),
    });
    h.run();
    assert_eq!(
        h.state().requests.len(),
        requests_before,
        "Disabled Post must not send a request"
    );
    document["dialogs"][0]["buttonEnabled"]["Post"] = serde_json::json!(true);
    h.state_mut().publication.generation += 1;
    h.state_mut().publication.result.documents.insert(
        "portfolio".into(),
        ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
    );
    h.run();
    h.get_by_label("Post").click();
    h.run();
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "portfolio-financial");
    assert_eq!(request["fields"]["action"], "Post");
    assert_eq!(request["fields"]["reference"], "LOCAL-TEST");
    document["dialogs"][0]["buttons"] = serde_json::json!(["Check outcome", "Close"]);
    document["dialogs"][0]["buttonEnabled"] =
        serde_json::json!({"Check outcome":true,"Close":true});
    h.state_mut().publication.generation += 1;
    h.state_mut().publication.result.documents.insert(
        "portfolio".into(),
        ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
    );
    h.run();
    h.get_by_label("Check outcome").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["fields"]["action"],
        "Check outcome"
    );
    h.get_by_label("Close").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["intent"],
        "portfolio-cancel"
    );
}

#[test]
fn ledger_places_fields_below_full_width_grid_and_preserves_apply() {
    let keys = [
        "control", "period", "start", "end", "account", "rule", "reason",
    ];
    let fields: Vec<_> = keys.iter().map(|key| serde_json::json!({
        "key":key,"caption":key,"value":"test","kind":if *key == "reason" {"multiline"} else {"text"},"enabled":true
    })).collect();
    let document = serde_json::json!({"context":{},"grids":[],"enabled":{},"dialogs":[{
        "key":"ledger","title":"Portfolio ledger administration","width":1120,"height":710,"captionWidth":95,
        "fields":fields,"buttons":["Apply","Refresh","Close"],"status":"Book ready",
        "grids":[{"key":"periods","columns":["Period"],"rows":[]}]
    }]});
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([(
                    "portfolio".into(),
                    ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                )]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();
    let controls = &h.state().manager.controls;
    let bounds = controls["dialog-ledger"];
    let grid = controls["ledger.grid-area"];
    assert!((grid.width() - bounds.width()).abs() < 1.);
    assert!((grid.height() / (bounds.bottom() - grid.top()) - 2. / 3.).abs() < 0.01);
    for (index, key) in keys.iter().enumerate() {
        let field = controls[&format!("ledger.{key}")];
        assert!(
            bounds.contains_rect(field),
            "{key} outside form: {field:?} vs {bounds:?}"
        );
        assert!(field.top() > grid.bottom());
        if index < 4 {
            assert!(field.right() < bounds.center().x);
        }
    }
    let apply = controls["ledger.button.Apply"];
    assert!(controls["window-ledger"].height() < 500.);
    assert!((apply.right() - bounds.right()).abs() < 1.);
    assert!(apply.width() >= 140.);
    assert!(apply.top() > grid.bottom());
    assert!((apply.top() - controls["ledger.account"].top()).abs() < 6.);
    assert!((controls["ledger.control"].top() - controls["ledger.account"].top()).abs() < 6.);
    let group_gap = controls["ledger.account.caption"].left() - controls["ledger.control"].right();
    assert!(group_gap >= 0. && group_gap < 70.);
    assert!(controls["ledger.reason"].right() < apply.left());
    assert!(bounds.contains_rect(apply));
    assert!(!controls.contains_key("ledger.button.Refresh"));
    assert!(!controls.contains_key("ledger.button.Close"));
    let pos = apply.center();
    h.input_mut().events.extend([
        egui::Event::PointerMoved(pos),
        egui::Event::PointerButton {
            pos,
            button: egui::PointerButton::Primary,
            pressed: true,
            modifiers: Default::default(),
        },
    ]);
    h.run_steps(1);
    h.input_mut().events.push(egui::Event::PointerButton {
        pos,
        button: egui::PointerButton::Primary,
        pressed: false,
        modifiers: Default::default(),
    });
    h.run();
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "portfolio-financial");
    assert_eq!(request["fields"]["action"], "Apply");
    for key in keys {
        assert_eq!(request["fields"][key], "test");
    }
}

#[test]
fn fund_allocation_uses_compact_aligned_fields_and_centered_actions() {
    let keys = [
        "TargetWeight",
        "MinimumWeight",
        "MaximumWeight",
        "AllocatedCapital",
        "Currency",
        "SourcePolicyVersion",
    ];
    let fields: Vec<_> = keys
        .iter()
        .map(|key| serde_json::json!({"key":key,"caption":key,"value":"1","enabled":true}))
        .collect();
    let document = serde_json::json!({"context":{},"grids":[],"enabled":{},"dialogs":[{
        "key":"allocation","title":"Fund Allocation","width":640,"height":280,"captionWidth":225,"fields":fields,"buttons":["Save","Cancel"]
    }]});
    let mut manager = PortfolioManager::default();
    manager.open();
    let state = State {
        manager,
        requests: vec![],
        publication: ContractPublication {
            generation: 7,
            result: ifm_ui_egui::contract_editor::ContractResult {
                view: "portfolio".into(),
                documents: BTreeMap::from([(
                    "portfolio".into(),
                    ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                )]),
                ..Default::default()
            },
            ..Default::default()
        },
    };
    let mut h = Harness::builder()
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.publication.outcomes.push(EditorOutcome {
                        request_id: request["requestId"].as_str().unwrap().into(),
                        state: "Completed".into(),
                        message: String::new(),
                    });
                    state.requests.push(request);
                }
            },
            state,
        );
    ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
    h.run();
    let controls = &h.state().manager.controls;
    let bounds = controls["dialog-allocation"];
    assert!(controls["window-allocation"].height() < 300.);
    let save = controls["allocation.button.Save"];
    let cancel = controls["allocation.button.Cancel"];
    assert!(((save.left() + cancel.right()) / 2. - bounds.center().x).abs() < 1.);
    for key in keys {
        let input = controls[&format!("allocation.{key}")];
        let caption = controls[&format!("allocation.{key}.caption")];
        assert!(bounds.contains_rect(input));
        assert!((input.left() - caption.right() - 20.).abs() < 1.);
        assert!(input.bottom() < save.top());
        assert!(h.query_by_label(&format!("{key}:")).is_some());
    }
    assert!((save.top() - controls["allocation.SourcePolicyVersion"].bottom() - 20.).abs() < 1.);
    h.get_by_label("Save").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["intent"],
        "portfolio-save"
    );
    h.get_by_label("Cancel").click();
    h.run();
    assert_eq!(
        h.state().requests.last().unwrap()["intent"],
        "portfolio-cancel"
    );
}

#[test]
fn risk_envelope_and_assignment_follow_compact_fund_editor_layout() {
    for (key, title, keys) in [
        (
            "envelope",
            "Fund Risk Envelope",
            vec![
                "CapacityState",
                "Currency",
                "AllocatedCapital",
                "AvailableCapital",
                "MaximumRiskPerTrade",
                "MaximumAggregateRisk",
                "MaximumMargin",
                "MaximumGrossNotional",
                "MaximumContracts",
                "MaximumOpenPositions",
                "RemainingLossBudget",
                "days",
            ],
        ),
        (
            "assignment",
            "Trade Template Assignment",
            vec![
                "Enabled",
                "TradeTemplateId",
                "TradeTemplateVersion",
                "DecisionHorizon",
                "UnderlyingUniverse",
                "AssetType",
                "deployment",
                "Priority",
                "TradeSelectionHintProfileId",
                "TradeSelectionHintProfileVersion",
                "OrderCompositionProfileId",
                "OrderCompositionProfileVersion",
            ],
        ),
    ] {
        let fields:Vec<_>=keys.iter().map(|name|serde_json::json!({"key":name,"caption":name,"value":"1","kind":if *name=="Enabled" {"check"} else {"text"},"enabled":true})).collect();
        let document = serde_json::json!({"context":{},"grids":[],"enabled":{},"dialogs":[{
            "key":key,"title":title,"width":780,"height":450,"captionWidth":225,"fields":fields,"buttons":["Save","Cancel"],"status":"Ready"
        }]});
        let mut manager = PortfolioManager::default();
        manager.open();
        let state = State {
            manager,
            requests: vec![],
            publication: ContractPublication {
                generation: 7,
                result: ifm_ui_egui::contract_editor::ContractResult {
                    view: "portfolio".into(),
                    documents: BTreeMap::from([(
                        "portfolio".into(),
                        ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
                    )]),
                    ..Default::default()
                },
                ..Default::default()
            },
        };
        let mut h = Harness::builder()
            .with_size(egui::vec2(1800., 1200.))
            .build_state(
                |ctx, state: &mut State| {
                    if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                        let request: serde_json::Value = serde_json::from_str(&payload).unwrap();
                        state.publication.outcomes.push(EditorOutcome {
                            request_id: request["requestId"].as_str().unwrap().into(),
                            state: "Completed".into(),
                            message: String::new(),
                        });
                        state.requests.push(request);
                    }
                },
                state,
            );
        ifm_ui_egui::apply_winforms_typography(&h.ctx).unwrap();
        h.run();
        let controls = &h.state().manager.controls;
        let bounds = controls[&format!("dialog-{key}")];
        let save = controls[&format!("{key}.button.Save")];
        let cancel = controls[&format!("{key}.button.Cancel")];
        assert!(controls[&format!("window-{key}")].height() < 500.);
        assert!(bounds.contains_rect(save));
        assert!(bounds.contains_rect(cancel));
        assert!(((save.left() + cancel.right()) / 2. - bounds.center().x).abs() < 1.);
        for name in &keys {
            let input = controls[&format!("{key}.{name}")];
            let caption = controls[&format!("{key}.{name}.caption")];
            assert!(bounds.contains_rect(input), "{key}.{name} clipped");
            assert!((input.left() - caption.right() - 20.).abs() < 1.);
            assert!(input.bottom() < save.top());
            assert!(h.query_by_label(&format!("{name}:")).is_some());
        }
        assert!(
            (save.top() - controls[&format!("{key}.{}", keys.last().unwrap())].bottom() - 20.)
                .abs()
                < 1.
        );
        h.get_by_label("Save").click();
        h.run();
        assert_eq!(
            h.state().requests.last().unwrap()["intent"],
            "portfolio-save"
        );
        h.get_by_label("Cancel").click();
        h.run();
        assert_eq!(
            h.state().requests.last().unwrap()["intent"],
            "portfolio-cancel"
        );
    }
}
