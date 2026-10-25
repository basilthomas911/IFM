use ifm_ui_egui::lookup_editor::EditorOutcome;
use ifm_ui_egui::{
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    state::publications::Publications,
};
fn publication(view: &str, generation: u64, id: &str) -> ContractPublication {
    ContractPublication {
        schema: 1,
        generation,
        result: ContractResult {
            view: view.into(),
            rows: vec![ContractRow {
                id: id.into(),
                ..Default::default()
            }],
            ..Default::default()
        },
        ..Default::default()
    }
}
#[test]
fn delayed_hidden_view_reply_cannot_replace_active_screen_or_its_generation() {
    let mut state = Publications::default();
    state
        .apply(publication("strategy-catalog", 7, "strategy-version"))
        .unwrap();
    state
        .apply(publication("yield-curve", 1, "2026-10-01"))
        .unwrap();
    // A catalog request finishes after the user navigates to Yield Curve.
    state
        .apply(publication("strategy-catalog", 8, "other-strategy-version"))
        .unwrap();
    assert_eq!(state.get("yield-curve").generation, 1);
    assert_eq!(state.get("yield-curve").result.rows[0].id, "2026-10-01");
    // Navigating back uses that controller's own latest state.
    assert_eq!(state.get("strategy-catalog").generation, 8);
    assert_eq!(
        state.get("strategy-catalog").result.rows[0].id,
        "other-strategy-version"
    );
    assert!(
        !state
            .apply(publication("strategy-catalog", 7, "obsolete"))
            .unwrap()
    );
    assert!(
        !state
            .apply(publication("yield-curve", 1, "duplicate"))
            .unwrap()
    );
    assert_eq!(state.get("strategy-catalog").generation, 8);
    // An unloaded screen cannot accidentally borrow another screen's rows.
    assert!(state.get("economic-calendar").result.rows.is_empty());
    assert_eq!(state.get("economic-calendar").generation, 0);
}
#[test]
fn malformed_or_unbounded_view_names_do_not_enter_the_cache() {
    let mut state = Publications::default();
    assert!(state.apply(publication("unknown", 1, "bad")).is_err());
    let mut wrong = publication("yield-curve", 1, "bad");
    wrong.schema = 2;
    assert!(state.apply(wrong).is_err());
    assert!(state.get("yield-curve").result.rows.is_empty());
}
#[test]
fn command_outcomes_survive_observer_refresh_until_their_view_renders() {
    let mut cache = Publications::default();
    let mut saved = publication("futures-contract", 3, "saved");
    saved.outcomes.push(EditorOutcome {
        request_id: "save-request".into(),
        state: "Completed".into(),
        message: "Saved".into(),
    });
    cache.apply(saved).unwrap();
    cache
        .apply(publication("futures-contract", 4, "observer"))
        .unwrap();
    assert_eq!(cache.get("futures-contract").result.rows[0].id, "observer");
    assert_eq!(
        cache.get("futures-contract").outcomes[0].request_id,
        "save-request"
    );
    // A real copied editor must release its pending state using the retained
    // receipt, even when both publications arrived before the same UI frame.
    let mut editor = ifm_ui_egui::contract_editor::ContractEditor::default();
    editor.pending = Some("save-request".into());
    let catalog: ifm_ui_egui::Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let context = eframe::egui::Context::default();
    let _ = context.run(Default::default(), |context| {
        editor.draw(context, cache.get("futures-contract"), &catalog, true);
    });
    assert_ne!(editor.pending.as_deref(), Some("save-request"));
    cache.acknowledge_rendered("yield-curve");
    assert_eq!(cache.get("futures-contract").outcomes.len(), 1);
    cache.acknowledge_rendered("futures-contract");
    assert!(cache.get("futures-contract").outcomes.is_empty());
}
#[test]
fn stale_data_can_deliver_a_late_receipt_without_replacing_current_rows() {
    let mut cache = Publications::default();
    cache
        .apply(publication("futures-contract", 5, "latest"))
        .unwrap();
    let mut reply = publication("futures-contract", 4, "old");
    reply.outcomes.push(EditorOutcome {
        request_id: "late-request".into(),
        state: "Unknown".into(),
        message: "Awaiting verification".into(),
    });
    assert!(!cache.apply(reply).unwrap());
    assert_eq!(cache.get("futures-contract").result.rows[0].id, "latest");
    let mut confirmed = publication("futures-contract", 3, "older");
    confirmed.outcomes.push(EditorOutcome {
        request_id: "late-request".into(),
        state: "Completed".into(),
        message: "Confirmed".into(),
    });
    assert!(!cache.apply(confirmed).unwrap());
    assert_eq!(cache.get("futures-contract").outcomes[0].state, "Completed");
    let mut delayed = publication("futures-contract", 6, "new");
    delayed.outcomes.push(EditorOutcome {
        request_id: "late-request".into(),
        state: "Unknown".into(),
        message: "Obsolete uncertainty".into(),
    });
    cache.apply(delayed).unwrap();
    assert_eq!(cache.get("futures-contract").outcomes[0].state, "Completed");
}
#[test]
fn unconsumed_receipts_are_bounded_and_can_be_released_without_resetting_the_controller() {
    let mut cache = Publications::default();
    for generation in 1..=256 {
        let mut value = publication("futures-contract", generation, "row");
        value.outcomes.push(EditorOutcome {
            request_id: generation.to_string(),
            state: "Completed".into(),
            message: String::new(),
        });
        cache.apply(value).unwrap();
    }
    let mut excess = publication("futures-contract", 257, "row");
    excess.outcomes.push(EditorOutcome {
        request_id: "excess".into(),
        state: "Completed".into(),
        message: String::new(),
    });
    assert!(cache.apply(excess).is_err());
    assert_eq!(cache.get("futures-contract").outcomes.len(), 256);
    cache.acknowledge_rendered("futures-contract");
    assert!(
        cache
            .apply(publication("futures-contract", 258, "recovered"))
            .unwrap()
    );
}
#[test]
fn duplicate_receipts_in_the_first_publication_are_coalesced_and_terminal_state_never_conflicts() {
    let mut cache = Publications::default();
    let mut value = publication("futures-contract", 1, "row");
    value.outcomes = vec![
        EditorOutcome {
            request_id: "same".into(),
            state: "Unknown".into(),
            message: String::new(),
        },
        EditorOutcome {
            request_id: "same".into(),
            state: "Completed".into(),
            message: String::new(),
        },
    ];
    cache.apply(value).unwrap();
    assert_eq!(cache.get("futures-contract").outcomes.len(), 1);
    assert_eq!(cache.get("futures-contract").outcomes[0].state, "Completed");
    let mut conflict = publication("futures-contract", 2, "bad");
    conflict.outcomes.push(EditorOutcome {
        request_id: "same".into(),
        state: "Failed".into(),
        message: String::new(),
    });
    assert!(cache.apply(conflict).is_err());
    assert_eq!(cache.get("futures-contract").result.rows[0].id, "row");
}
