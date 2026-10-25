use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    Catalog,
    calendar_editor::CalendarEditor,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
};
use std::collections::BTreeMap;
fn fixture() -> (Catalog, ContractPublication) {
    let c = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let s = ContractPublication {
        schema: 1,
        generation: 7,
        status: "Ready".into(),
        busy: false,
        result: ContractResult {
            documents: Default::default(),
            view: "economic-calendar".into(),
            rows: vec![ContractRow {
                data: None,
                id: "exact-original-id".into(),
                fields: BTreeMap::from([
                    ("eventDate".into(), "2026-10-13T12:30:00.0000000Z".into()),
                    ("countryCode".into(), "US".into()),
                    ("eventName".into(), "Inflation".into()),
                    ("actual".into(), "2.4%".into()),
                ]),
            }],
            choices: BTreeMap::from([("countryCode".into(), vec!["US".into()])]),
            context: BTreeMap::from([
                ("filterDate".into(), "2026-10-13".into()),
                ("filterCountry".into(), "US".into()),
            ]),
            has_more: false,
        },
        outcomes: vec![],
        ..Default::default()
    };
    (c, s)
}
#[test]
fn changes_keep_exact_identity_and_utc_instant() {
    let (c, s) = fixture();
    let mut h = Harness::new_state(
        move |ctx, state: &mut (CalendarEditor, Option<String>)| {
            if let Some(payload) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(payload)
            }
        },
        (CalendarEditor::default(), None),
    );
    h.set_size(eframe::egui::vec2(1100., 900.));
    h.run();
    h.get_by_label("2026-10-13T12:30:00.0000000Z").click();
    h.run();
    h.get_by_label("Save event").click();
    h.run();
    let p: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(p["originalId"], "exact-original-id");
    assert_eq!(p["expectedGeneration"], 7);
    assert_eq!(p["fields"]["eventDate"], "2026-10-13T12:30:00.0000000Z");
    assert!(h.state().0.pending.is_some());
}
#[test]
fn cancel_removal_submits_no_action() {
    let (c, s) = fixture();
    let mut h = Harness::new_state(
        move |ctx, state: &mut (CalendarEditor, Option<String>)| {
            if let Some(payload) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(payload)
            }
        },
        (CalendarEditor::default(), None),
    );
    h.set_size(eframe::egui::vec2(1100., 900.));
    h.run();
    h.get_by_label("2026-10-13T12:30:00.0000000Z").click();
    h.run();
    h.get_by_label("Remove event").click();
    h.run();
    assert!(h.query_by_label("Confirm removal").is_some());
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().1.is_none());
}
#[test]
fn calendar_history_draws_only_viewport_rows() {
    let (c, mut s) = fixture();
    s.result.rows = (0..100000)
        .map(|i| ContractRow {
            data: None,
            id: i.to_string(),
            fields: BTreeMap::from([
                ("eventDate".into(), i.to_string()),
                ("countryCode".into(), "US".into()),
                ("eventName".into(), "Fixture".into()),
            ]),
        })
        .collect();
    let mut h = Harness::new_state(
        move |ctx, state: &mut CalendarEditor| {
            state.draw(ctx, &s, &c, true);
        },
        CalendarEditor::default(),
    );
    h.set_size(eframe::egui::vec2(1100., 900.));
    h.run();
    assert!(h.state().drawn_rows > 0 && h.state().drawn_rows < 40);
}
