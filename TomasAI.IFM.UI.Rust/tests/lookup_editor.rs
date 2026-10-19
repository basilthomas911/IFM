use egui_kittest::kittest::NodeT;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{Catalog, lookup_editor::*};
struct State {
    view: LookupEditor,
    snapshot: EditorPublication,
    intents: Vec<String>,
}
fn setup(count: usize, live: bool) -> Harness<'static, State> {
    setup_mode(count, live, false)
}
fn setup_mode(count: usize, live: bool, reference: bool) -> Harness<'static, State> {
    let c: Catalog = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let snapshot = EditorPublication {
        schema: 1,
        generation: 3,
        status: "Completed".into(),
        rows: (0..count)
            .map(|i| LookupRow {
                lookup_type_name: "Test".into(),
                short_code: format!("CODE{i}"),
                order_id: i as i32,
                description: format!("Row {i}"),
                ..Default::default()
            })
            .collect(),
        ..Default::default()
    };
    Harness::new_state(
        move |ctx, s: &mut State| {
            if let Some(intent) = if reference {
                s.view.draw_reference(ctx, &s.snapshot, live)
            } else {
                s.view.draw(ctx, &s.snapshot, &c, live)
            } {
                s.intents.push(intent)
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
fn selected_edit_emits_one_typed_intent_then_waits_for_completion() {
    let mut h = setup(3, true);
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    h.get_by_label("Test | 0 | CODE0 | Row 0").click();
    h.run();
    h.get_by_label("Save lookup").click();
    h.run();
    h.run();
    assert_eq!(h.state().intents.len(), 1);
    let request: serde_json::Value = serde_json::from_str(&h.state().intents[0]).unwrap();
    assert_eq!(request["intent"], "change");
    assert_eq!(request["expectedGeneration"], 3);
    assert_eq!(request["lookup"]["shortCode"], "CODE0");
    assert!(h.state().view.pending.is_some());
    h.state_mut().snapshot.outcomes.push(EditorOutcome {
        request_id: request["requestId"].as_str().unwrap().into(),
        state: "Completed".into(),
        message: "Saved".into(),
    });
    h.run();
    assert!(h.state().view.pending.is_none());
}
#[test]
fn removal_requires_confirmation_and_fixture_cannot_send() {
    let mut h = setup(2, true);
    h.run();
    h.get_by_label("Test | 0 | CODE0 | Row 0").click();
    h.run();
    h.get_by_label("Remove lookup").click();
    h.run();
    assert!(h.state().intents.is_empty());
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().intents.is_empty());
    h.get_by_label("Remove lookup").click();
    h.run();
    h.get_by_label("Confirm removal").click();
    h.run();
    assert_eq!(h.state().intents.len(), 1);
    let mut fixture = setup(2, false);
    fixture.run();
    assert!(
        fixture
            .query_by_label("Editing requires a live backend")
            .is_some()
    );
    assert!(fixture.state().intents.is_empty());
}
#[test]
fn lookup_history_is_virtualized() {
    let mut h = setup(100_000, true);
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    assert!(h.state().view.drawn_rows > 0);
    assert!(h.state().view.drawn_rows < 40);
}

#[test]
fn reference_lookup_preserves_lists_four_fields_and_two_step_add_without_extra_controls() {
    let mut h = setup_mode(3, true, true);
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    let request: serde_json::Value =
        serde_json::from_str(h.state().intents.last().unwrap()).unwrap();
    h.state_mut().snapshot.outcomes.push(EditorOutcome {
        request_id: request["requestId"].as_str().unwrap().into(),
        state: "Completed".into(),
        message: String::new(),
    });
    h.run();
    for label in [
        "Reference Data Manager",
        "Selection:",
        "Lookup Type Names",
        "Lookup Type Short Codes",
        "Lookup Type Name:",
        "Short Code:",
        "Order Id:",
        "Description:",
        "Add",
        "Change",
        "Remove",
        "Import",
        "Close",
    ] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    assert!(h.get_by_label("Import").accesskit_node().is_disabled());
    for extra in ["Load lookups", "New lookup", "Cancel editing"] {
        assert!(h.query_by_label(extra).is_none());
    }
    h.get_by_label("Add").click();
    h.run();
    assert_eq!(h.state().view.edit_mode, 1);
    h.state_mut().view.draft.lookup_type_name = "Test".into();
    h.state_mut().view.draft.short_code = "NEW".into();
    h.run();
    h.get_by_label("Save").click();
    h.run();
    let request: serde_json::Value =
        serde_json::from_str(h.state().intents.last().unwrap()).unwrap();
    assert_eq!(request["intent"], "add");
    assert_eq!(request["lookup"]["orderId"], 3);
}
