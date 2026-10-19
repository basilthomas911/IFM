use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    lookup_editor::EditorOutcome,
    market::market_data_manager::MarketDataManager,
};
use std::collections::BTreeMap;

struct State {
    manager: MarketDataManager,
    publication: ContractPublication,
    requests: Vec<serde_json::Value>,
}

fn harness(selection: usize, size: egui::Vec2) -> Harness<'static, State> {
    let mut manager = MarketDataManager::default();
    manager.selection = selection;
    manager.open();
    let view = ["futures-contract", "option-contract", "yield-curve"][selection];
    let state = State {
        manager,
        publication: ContractPublication {
            generation: 7,
            result: ContractResult {
                view: view.into(),
                rows: vec![ContractRow {
                    id: "exact-saved-identity".into(),
                    fields: BTreeMap::from([
                        ("contractId".into(), "ES20261218".into()),
                        ("symbol".into(), "ES".into()),
                        ("lastTradeDate".into(), "2026-12-18".into()),
                        ("contractMonth".into(), "2026-12-18".into()),
                        ("description".into(), "Original description".into()),
                        ("valueDate".into(), "2026-10-16".into()),
                        ("oneMonth".into(), "4.1".into()),
                        ("onTheRun".into(), "True".into()),
                    ]),
                    data: None,
                }],
                choices: BTreeMap::from([
                    ("symbol".into(), vec!["ES".into(), "NQ".into()]),
                    (
                        "timePeriod".into(),
                        vec!["Today".into(), "Last Week".into()],
                    ),
                ]),
                context: BTreeMap::from([
                    ("valueDate".into(), "2026-10-16".into()),
                    ("timePeriod".into(), "Today".into()),
                ]),
                ..Default::default()
            },
            ..Default::default()
        },
        requests: vec![],
    };
    let mut h = Harness::builder()
        .with_size(size)
        .with_max_steps(30)
        .with_step_dt(1.0 / 60.0)
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
    h.set_size(size);
    h.run();
    h
}

fn click(h: &mut Harness<'_, State>, key: &str) {
    let pos = h.state().manager.controls[key].center();
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
}

#[test]
fn all_three_winforms_views_load_their_existing_query_and_keep_command_rail() {
    for selection in 0..3 {
        let h = harness(selection, egui::vec2(1400., 900.));
        assert!(h.query_by_label("Refresh").is_none());
        assert!(h.query_by_label("Next Page").is_none());
        let c = &h.state().manager.controls;
        assert!((c["commands"].width() - 115.).abs() < 1.);
        assert!((c["selector"].height() - 37.).abs() < 1.);
        assert!(c["content"].right() <= c["commands"].left() + 0.1);
        assert_eq!(h.state().requests.len(), 1);
        assert_eq!(
            h.state().requests[0]["view"],
            ["futures-contract", "option-contract", "yield-curve"][selection]
        );
        assert_eq!(h.state().requests[0]["intent"], "load");
        if selection < 2 {
            assert!((c["contract-list"].width() - 232. * 8. / 7.).abs() < 1.);
            assert!(c["contract-fields"].left() > c["contract-list"].right());
            assert!(h.query_by_label("ES20261218").is_some());
        } else {
            assert!(h.query_by_label("30 Yr").is_some());
        }
    }
}

#[test]
fn contract_change_preserves_exact_original_identity_and_cancel_does_not_send() {
    let mut h = harness(0, egui::vec2(1400., 900.));
    h.get_by_label("ES20261218").click();
    h.run();
    click(&mut h, "change");
    assert!(h.query_by_label("Save").is_some());
    click(&mut h, "change");
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "change");
    assert_eq!(request["originalId"], "exact-saved-identity");
    assert_eq!(request["fields"]["onTheRun"], "true");
    let count = h.state().requests.len();
    click(&mut h, "change");
    click(&mut h, "close");
    assert_eq!(h.state().requests.len(), count);
    assert!(h.state().manager.open);
}

#[test]
fn compact_dialog_keeps_commands_inside_window_and_rates_editable() {
    let mut h = harness(2, egui::vec2(900., 650.));
    let c = &h.state().manager.controls;
    assert!(c["commands"].right() <= 900.);
    assert!(c["rates-grid"].right() <= c["commands"].left());
    click(&mut h, "add");
    assert!(h.query_by_label("Save").is_some());
    click(&mut h, "add");
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "add");
    assert_eq!(request["fields"]["valueDate"], "2026-10-16");
    for (key, _) in ifm_ui_rust::yield_curve_editor::TENORS {
        assert_eq!(request["fields"][key], "0");
    }
}

#[test]
fn yield_double_click_starts_inline_change_and_preserves_identity() {
    let mut h = harness(2, egui::vec2(1400., 900.));
    h.get_by_label("2026-10-16").click();
    h.run_steps(4);
    h.get_by_label("2026-10-16").click();
    h.run_steps(4);
    assert!(h.query_by_label("Save").is_some());
    assert!(h.query_by_label("Change Yield Curve Rate").is_none());
    click(&mut h, "change");
    let request = h.state().requests.last().unwrap();
    assert_eq!(request["intent"], "change");
    assert_eq!(request["originalId"], "exact-saved-identity");
    assert_eq!(request["fields"]["oneMonth"], "4.1");
}

#[test]
fn escape_cancels_inline_add_and_change_without_submitting() {
    for command in ["add", "change"] {
        let mut h = harness(2, egui::vec2(1400., 900.));
        h.get_by_label("2026-10-16").click();
        h.run();
        let count = h.state().requests.len();
        click(&mut h, command);
        assert!(h.query_by_label("Save").is_some());
        h.input_mut().events.push(egui::Event::Key {
            key: egui::Key::Escape,
            physical_key: None,
            pressed: true,
            repeat: false,
            modifiers: Default::default(),
        });
        h.run();
        assert!(h.query_by_label("Save").is_none());
        assert!(h.state().manager.open);
        assert_eq!(h.state().requests.len(), count);
        assert!(h.query_by_label("4.1").is_some());
    }
}

#[test]
fn yield_change_is_inline_and_cancel_does_not_submit() {
    let mut h = harness(2, egui::vec2(1400., 900.));
    h.get_by_label("2026-10-16").click();
    h.run();
    let count = h.state().requests.len();
    click(&mut h, "change");
    assert!(h.query_by_label("Save").is_some());
    assert!(h.query_by_label("Change Yield Curve Rate").is_none());
    click(&mut h, "close");
    assert_eq!(h.state().requests.len(), count);
    assert!(h.state().manager.open);
    assert!(h.query_by_label("Save").is_none());
}

#[test]
#[ignore = "Exports rendered layout evidence; requires a graphics device"]
fn render_market_data_manager_layouts() {
    let output =
        std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("../.artifacts/market-data-manager");
    std::fs::create_dir_all(&output).unwrap();
    for (selection, name) in ["futures", "options", "yield"].into_iter().enumerate() {
        let mut h = harness(selection, egui::vec2(1400., 900.));
        ifm_ui_rust::apply_winforms_typography(&h.ctx).unwrap();
        h.run();
        h.render()
            .unwrap()
            .save(output.join(format!("{name}.png")))
            .unwrap();
    }
}
