use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{Catalog, strategy_inspection::*};
use std::collections::BTreeMap;
struct State {
    view: StrategyInspection,
    publication: InspectionPublication,
    intents: Vec<String>,
}
fn harness(risk: bool, count: usize) -> Harness<'static, State> {
    let c: Catalog = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let mut view = StrategyInspection::default();
    view.risk = risk;
    let publication = InspectionPublication {
        schema: 1,
        generation: 3,
        status: "Loaded".into(),
        result: InspectionResult {
            view: if risk { "risk" } else { "observation" }.into(),
            rows: (0..count)
                .map(|i| InspectionRow {
                    id: format!("row{i}"),
                    label: format!("History row {i}"),
                    fields: BTreeMap::from([
                        ("workflow".into(), format!("workflow-{i}")),
                        ("invocation".into(), format!("invocation-{i}")),
                    ]),
                })
                .collect(),
            decision: "CURRENT AUTHORITY: Expired; Historical calculation: Approved".into(),
            evidence: "Frozen inputs and versions".into(),
            paging_state: Some("cursor".into()),
            ..Default::default()
        },
        ..Default::default()
    };
    Harness::new_state(
        move |ctx, s: &mut State| {
            if let Some(intent) = s.view.draw(ctx, &s.publication, &c, true) {
                s.intents.push(intent);
            }
        },
        State {
            view,
            publication,
            intents: vec![],
        },
    )
}
#[test]
fn selecting_history_opens_exact_identity_once() {
    let mut h = harness(false, 3);
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    h.get_by_label("History row 1").click();
    h.run();
    h.run();
    assert_eq!(h.state().intents.len(), 1);
    let request: serde_json::Value = serde_json::from_str(&h.state().intents[0]).unwrap();
    assert_eq!(request["intent"], "open");
    assert_eq!(request["fields"]["workflow"], "workflow-1");
}
#[test]
fn risk_evidence_and_paging_preserve_filter_values() {
    let mut h = harness(true, 3);
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    h.get_by_label("Evidence").click();
    h.run();
    assert!(h.state().view.evidence);
    h.get_by_label("Next page").click();
    h.run();
    let request: serde_json::Value = serde_json::from_str(&h.state().intents[0]).unwrap();
    assert_eq!(request["intent"], "next");
    assert_eq!(request["fields"]["cursor"], "cursor");
    assert_eq!(request["fields"]["portfolio"], "1");
}
#[test]
fn inspection_history_draws_only_the_visible_range() {
    let mut h = harness(true, 100_000);
    h.set_size(eframe::egui::vec2(1100.0, 800.0));
    h.run();
    assert!(h.state().view.drawn_rows > 0);
    assert!(h.state().view.drawn_rows < 20);
}
