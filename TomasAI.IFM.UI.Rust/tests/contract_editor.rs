use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{Catalog, contract_editor::*};
use std::collections::BTreeMap;
struct State {
    view: ContractEditor,
    snapshot: ContractPublication,
    intents: Vec<String>,
}
fn setup(count: usize) -> Harness<'static, State> {
    let catalog: Catalog =
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let snapshot = ContractPublication {
        schema: 1,
        generation: 4,
        status: "Completed".into(),
        result: ContractResult { documents: Default::default(),
            view: "futures-contract".into(),
            choices: [
                "symbol",
                "securityType",
                "currency",
                "exchange",
                "multiplier",
            ]
            .into_iter()
            .map(|key| (key.into(), vec!["ES".into()]))
            .collect(),
            rows: (0..count)
                .map(|i| ContractRow { data: None,
                    id: format!("identity-{i}"),
                    fields: BTreeMap::from([
                        ("contractId".into(), format!("ES-{i}")),
                        ("symbol".into(), "ES".into()),
                        ("lastTradeDate".into(), "2026-12-18".into()),
                        ("onTheRun".into(), "false".into()),
                        ("rollover".into(), "false".into()),
                    ]),
                })
                .collect(),
            ..Default::default()
        },
        ..Default::default()
    };
    Harness::new_state(
        move |ctx, s: &mut State| {
            if let Some(intent) = s.view.draw(ctx, &s.snapshot, &catalog, true) {
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
fn edit_uses_exact_original_identity_and_waits_for_ack() {
    let mut h = setup(2);
    h.set_size(eframe::egui::vec2(1100., 800.));
    h.run();
    h.get_by_label("ES-0").click();
    h.run();
    h.get_by_label("Save contract").click();
    h.run();
    h.run();
    assert_eq!(h.state().intents.len(), 1);
    let data: serde_json::Value = serde_json::from_str(&h.state().intents[0]).unwrap();
    assert_eq!(data["originalId"], "identity-0");
    assert_eq!(data["expectedGeneration"], 4);
    assert!(h.state().view.pending.is_some());
}
#[test]
fn remove_requires_confirmation_and_cancel_sends_nothing() {
    let mut h = setup(2);
    h.set_size(eframe::egui::vec2(1100., 800.));
    h.run();
    h.get_by_label("ES-0").click();
    h.run();
    h.get_by_label("Remove contract").click();
    h.run();
    assert!(h.state().intents.is_empty());
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().intents.is_empty());
    h.get_by_label("Remove contract").click();
    h.run();
    h.get_by_label("Confirm removal").click();
    h.run();
    assert_eq!(h.state().intents.len(), 1);
}
#[test]
fn hundred_thousand_contracts_draw_only_visible_rows() {
    let mut h = setup(100_000);
    h.set_size(eframe::egui::vec2(1100., 800.));
    h.run();
    assert!(h.state().view.drawn_rows > 0);
    assert!(h.state().view.drawn_rows < 40);
}

#[test]
fn provider_convention_review_requires_confirmation_and_cancel_submits_nothing() {
    let mut h = setup(1);
    h.state_mut().snapshot.result.rows[0]
        .fields
        .insert("provider".into(), "true".into());
    h.state_mut().snapshot.result.rows[0]
        .fields
        .insert("underlyingContractId".into(), "exact-underlying".into());
    h.set_size(eframe::egui::vec2(1100., 900.));
    h.run();
    h.get_by_label("ES-0").click();
    h.run();
    h.get_by_label("Review conventions").click();
    h.run();
    assert!(h.state().view.review_open);
    h.get_by_label("Save conventions").click();
    h.run();
    assert!(h.state().intents.is_empty());
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().intents.is_empty());
    assert!(!h.state().view.review_open);
    h.get_by_label("Review conventions").click();
    h.run();
    h.get_by_label("Save conventions").click();
    h.run();
    h.get_by_label("Confirm convention changes").click();
    h.run();
    let intent: serde_json::Value = serde_json::from_str(&h.state().intents[0]).unwrap();
    assert_eq!(intent["intent"], "review");
    assert_eq!(intent["originalId"], "identity-0");
    assert_eq!(intent["fields"]["underlyingContractId"], "exact-underlying");
    assert_eq!(intent["expectedGeneration"], 4);
}
