use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    Catalog,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    yield_curve_editor::*,
};
use std::collections::BTreeMap;
struct State {
    view: YieldCurveEditor,
    snapshot: ContractPublication,
    intents: Vec<String>,
}
fn setup() -> Harness<'static, State> {
    let c: Catalog = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let mut fields = BTreeMap::from([("valueDate".into(), "2026-10-13".into())]);
    for (key, _) in TENORS {
        fields.insert(key.into(), "4.25".into());
    }
    let snapshot = ContractPublication {
        schema: 1,
        generation: 4,
        status: "Completed".into(),
        result: ContractResult { documents: Default::default(),
            view: "yield-curve".into(),
            rows: vec![ContractRow { data: None,
                id: "2026-10-13".into(),
                fields,
            }],
            context: BTreeMap::from([
                ("valueDate".into(), "2026-10-13".into()),
                ("timePeriod".into(), "2026".into()),
            ]),
            choices: BTreeMap::from([("timePeriod".into(), vec!["2026".into()])]),
            ..Default::default()
        },
        ..Default::default()
    };
    Harness::new_state(
        move |ctx, s: &mut State| {
            if let Some(intent) = s.view.draw(ctx, &s.snapshot, &c, true) {
                s.intents.push(intent);
            }
        },
        State {
            view: Default::default(),
            snapshot,
            intents: vec![],
        },
    )
}
#[test]
fn editing_a_curve_sends_all_twelve_exact_rates_once() {
    let mut h = setup();
    h.set_size(eframe::egui::vec2(1200., 800.));
    h.run();
    h.get_by_label("2026-10-13").click();
    h.run();
    h.get_by_label("Save rate").click();
    h.run();
    h.run();
    assert_eq!(h.state().intents.len(), 1);
    let input: serde_json::Value = serde_json::from_str(&h.state().intents[0]).unwrap();
    assert_eq!(input["view"], "yield-curve");
    assert_eq!(input["originalId"], "2026-10-13");
    for (key, _) in TENORS {
        assert_eq!(input["fields"][key], "4.25");
    }
}
#[test]
fn remove_is_confirmed_and_new_date_uses_operational_snapshot() {
    let mut h = setup();
    h.set_size(eframe::egui::vec2(1200., 800.));
    h.run();
    h.get_by_label("2026-10-13").click();
    h.run();
    h.get_by_label("Remove rate").click();
    h.run();
    assert!(h.state().intents.is_empty());
    h.get_by_label("Cancel").click();
    h.run();
    h.get_by_label("New rate").click();
    h.run();
    assert_eq!(h.state().view.draft["valueDate"], "2026-10-13");
    assert!(h.state().view.selected.is_none());
}
