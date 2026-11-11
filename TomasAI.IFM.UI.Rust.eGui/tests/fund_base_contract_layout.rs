use eframe::egui;
use egui_kittest::{
    Harness,
    kittest::{NodeT, Queryable},
};
use ifm_ui_egui::{
    contract_editor::{ContractPublication, ContractResult},
    portfolio::PortfolioManager,
};
use serde_json::json;
use std::collections::BTreeMap;

struct State {
    manager: PortfolioManager,
    publication: ContractPublication,
    last_request: serde_json::Value,
}

/// Equities is a visibly empty placeholder; saved Futures defaults survive switching back.
#[test]
fn compact_symbol_grids_preserve_selections_and_equities_cannot_save() {
    let mut document = json!({"grids":[],"enabled":{},"context":{},"dialogs":[{
        "key":"base-contracts","title":"Fund Base Contracts","width":780,"height":560,
        "fields":[{"key":"saved","value":"Futures ES: current + 2 forward cycles"},
            {"key":"BaseAsset","value":"Futures"},
            {"key":"selected.ES","caption":"ES","value":"true","choices":[{"label":"E-mini S&P 500"}]},
            {"key":"cycles.ES","value":"2"},
            {"key":"selected.VX","caption":"VX","value":"false","choices":[{"label":"Volatility futures"}]},
            {"key":"cycles.VX","value":"1"},
            {"key":"selected.VXA","caption":"VXA","value":"false","choices":[{"label":"Another provider future"}]},
            {"key":"cycles.VXA","value":"0"},
            {"key":"selected.CL","caption":"CL","value":"false","choices":[{"label":"Crude oil"}]},
            {"key":"cycles.CL","value":"0"}],"buttons":["Save","Cancel"]}]});
    // Production catalog has 2,644 symbols; submitting every row exceeded the controller limit.
    let fields = document["dialogs"][0]["fields"].as_array_mut().unwrap();
    for index in 0..2700 {
        fields.push(json!({"key":format!("selected.TEST{index}"),"caption":format!("TEST{index}"),"value":"false"}));
        fields.push(json!({"key":format!("cycles.TEST{index}"),"value":"2"}));
    }
    let mut manager = PortfolioManager::default();
    manager.open();
    let publication = ContractPublication {
        generation: 1,
        result: ContractResult {
            view: "portfolio".into(),
            documents: BTreeMap::from([(
                "portfolio".into(),
                ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
            )]),
            ..Default::default()
        },
        ..Default::default()
    };
    let mut h = Harness::builder()
        .with_max_steps(20)
        .with_size(egui::vec2(1800., 1200.))
        .build_state(
            |ctx, state: &mut State| {
                if let Some(payload) = state.manager.draw(ctx, &state.publication, true) {
                    let intent: serde_json::Value = serde_json::from_str(&payload).unwrap();
                    state.last_request = intent.clone();
                    state
                        .publication
                        .outcomes
                        .push(ifm_ui_egui::lookup_editor::EditorOutcome {
                            request_id: intent["requestId"].as_str().unwrap().into(),
                            state: "Completed".into(),
                            message: String::new(),
                        });
                }
            },
            State {
                manager,
                publication,
                last_request: serde_json::Value::Null,
            },
        );
    h.run();
    assert!(
        h.query_by_label("Saved defaults").is_none(),
        "Controls: {:?}",
        h.state().manager.controls.keys().collect::<Vec<_>>()
    );
    assert!(h.query_by_label("ES").is_some());
    assert!(!h.get_by_label("Save").accesskit_node().is_disabled());
    let controls = &h.state().manager.controls;
    let save = controls["base-contracts.button.Save"];
    let cancel = controls["base-contracts.button.Cancel"];
    let window = controls["window-base-contracts"];
    assert!(((save.left() + cancel.right()) / 2. - window.center().x).abs() < 2.);
    let caption = controls["base-contracts.BaseAsset.label"];
    let combo = controls["base-contracts.BaseAsset"];
    assert!((combo.left() - caption.right() - 20.).abs() < 2.);
    assert!(
        window.height() < 480.,
        "Compact editor grew to {}",
        window.height()
    );
    assert!(
        window.width() < 700.,
        "Dialog should fit its four columns compactly"
    );
    let selected_grid = controls["base-contracts.grid.selected"];
    let available_grid = controls["base-contracts.grid.available"];
    assert!((selected_grid.left() - available_grid.left()).abs() < 1.);
    assert!((selected_grid.width() - available_grid.width()).abs() < 1.);
    for column in 0..4 {
        let selected = controls[&format!("base-contracts.grid.selected.column.{column}")];
        let available = controls[&format!("base-contracts.grid.available.column.{column}")];
        assert!(
            (selected.left() - available.left()).abs() < 1.,
            "Column {column} left edges differ"
        );
        assert!(
            (selected.right() - available.right()).abs() < 1.,
            "Column {column} right edges differ"
        );
    }
    assert!(available_grid.bottom() < save.top());
    let cycles = controls["base-contracts.cycles.ES"];
    let up = controls["base-contracts.cycles.ES.up"];
    let down = controls["base-contracts.cycles.ES.down"];
    assert!(up.left() >= cycles.right());
    assert!(down.top() >= up.bottom());
    h.get_by_label("\u{2191}").click();
    h.run();
    h.get_by_label("Save").click();
    h.run();
    assert_eq!(
        h.state().last_request["fields"],
        json!({"BaseAsset":"Futures","selected.ES":"true","cycles.ES":"3"})
    );
    h.get_by_label("\u{2193}").click();
    h.run();
    h.get_by_label("Save").click();
    h.run();
    assert_eq!(h.state().last_request["fields"]["cycles.ES"], "2");
    let filter = h.state().manager.controls["base-contracts.SymbolFilter"].center();
    for pressed in [true, false] {
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: filter,
            button: egui::PointerButton::Primary,
            pressed,
            modifiers: egui::Modifiers::NONE,
        });
        h.run_steps(1);
    }
    for letter in ["v", "x"] {
        h.input_mut().events.push(egui::Event::Text(letter.into()));
        h.run();
        assert!(h.query_by_label("VX").is_some());
        assert!(h.query_by_label("VXA").is_some());
        assert!(h.query_by_label("CL").is_none());
        assert!(
            h.query_by_label("ES").is_some(),
            "Checked ES must remain pinned above filtered choices"
        );
    }
    h.input_mut().events.push(egui::Event::Text("a".into()));
    h.run();
    assert!(h.query_by_label("VX").is_none());
    assert!(h.query_by_label("VXA").is_some());
    assert!(h.query_by_label("ES").is_some());
    let combo = h.state().manager.controls["base-contracts.BaseAsset"].center();
    for pressed in [true, false] {
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: combo,
            button: egui::PointerButton::Primary,
            pressed,
            modifiers: egui::Modifiers::NONE,
        });
        h.run_steps(1);
    }
    h.get_by_label("Equities").click();
    h.run();
    assert!(h.get_by_label("Save").accesskit_node().is_disabled());
    assert!(h.query_by_label("ES").is_none());
    let combo = h.state().manager.controls["base-contracts.BaseAsset"].center();
    for pressed in [true, false] {
        h.input_mut().events.push(egui::Event::PointerButton {
            pos: combo,
            button: egui::PointerButton::Primary,
            pressed,
            modifiers: egui::Modifiers::NONE,
        });
        h.run_steps(1);
    }
    h.get_by_label("Futures").click();
    h.run();
    assert!(!h.get_by_label("Save").accesskit_node().is_disabled());
    assert!(h.query_by_label("ES").is_some());
    h.get_by_label("Cancel").click();
    h.run();
    assert_eq!(h.state().last_request["intent"], "portfolio-cancel");
    assert!(h.query_by_label("Symbol filter:").is_none(), "Cancel should close without waiting for a network response");
}
