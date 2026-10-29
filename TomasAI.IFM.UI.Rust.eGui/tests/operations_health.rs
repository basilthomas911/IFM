use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    Catalog,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    operations_health::OperationsHealth,
};
use std::collections::BTreeMap;
fn fixture() -> (Catalog, ContractPublication) {
    (
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap(),
        ContractPublication {
            schema: 1,
            generation: 3,
            status: "Ready".into(),
            busy: false,
            result: ContractResult {
                documents: Default::default(),
                view: "operations-health".into(),
                rows: vec![
                    ContractRow {
                        data: None,
                        id: "stage:TickAggregation".into(),
                        fields: BTreeMap::from([
                            ("kind".into(), "stage".into()),
                            ("Stage".into(), "TickAggregation".into()),
                            ("Status".into(), "Green".into()),
                            ("Pending".into(), "4".into()),
                        ]),
                    },
                    ContractRow {
                        data: None,
                        id: "dataset:GLBX.MDP3".into(),
                        fields: BTreeMap::from([
                            ("kind".into(), "dataset".into()),
                            ("Dataset".into(), "GLBX.MDP3".into()),
                            ("Status".into(), "Green".into()),
                            ("ProcessId".into(), "123".into()),
                        ]),
                    },
                ],
                ..Default::default()
            },
            outcomes: vec![],
            ..Default::default()
        },
    )
}
#[test]
fn stage_and_worker_selection_inspects_exact_shared_fields() {
    let (c, s) = fixture();
    let mut h = Harness::new_state(
        move |ctx, state: &mut OperationsHealth| {
            state.draw(ctx, &s, &c, true);
        },
        OperationsHealth::default(),
    );
    h.set_size(eframe::egui::vec2(1100., 800.));
    h.run();
    h.query_all_by_label("TickAggregation").next().unwrap().click();
    h.run();
    h.get_by_label("Pending");
    h.get_by_label("4");
    h.get_by_label("Dataset workers").click();
    h.run();
    h.query_all_by_label("GLBX.MDP3").next().unwrap().click();
    h.run();
    h.get_by_label("Process Id");
    h.get_by_label("123");
    assert_eq!(h.state().selected.as_deref(), Some("dataset:GLBX.MDP3"));
}
#[test]
fn refresh_dispatches_only_a_read_and_pending_is_guarded() {
    let (c, s) = fixture();
    let mut h = Harness::new_state(
        move |ctx, state: &mut (OperationsHealth, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p)
            }
        },
        (OperationsHealth::default(), None),
    );
    h.run();
    assert!(h.query_by_label("Refresh status").is_none());
    h.run();
    let p: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(p["view"], "operations-health");
    assert_eq!(p["intent"], "load");
    assert!(h.state().0.pending.is_some());
}

#[test]
fn compact_dialog_has_plain_colored_items_and_title_bar_close_only() {
    let (c,s)=fixture();
    let mut h=Harness::new_state(move |ctx,state:&mut (OperationsHealth,bool)| {state.0.draw_dialog(ctx,&s,&c,true,&mut state.1);},(OperationsHealth::default(),true));
    h.set_size(eframe::egui::vec2(1200.,800.)); h.run();
    assert!(h.query_all_by_label("TickAggregation").next().is_some());
    assert!(h.query_by_label("TickAggregation | Green").is_none());
    assert!(h.query_by_label("Refresh status").is_none());
    h.get_by_label("Health details");
}
