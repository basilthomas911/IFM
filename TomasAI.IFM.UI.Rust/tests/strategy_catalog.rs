use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    Catalog, contract_editor::ContractPublication, strategy_catalog::StrategyCatalog,
};
fn fixture(editing: bool) -> (Catalog, ContractPublication) {
    let c = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let s=serde_json::from_value(serde_json::json!({"schema":1,"busy":false,"generation":7,"status":"Ready","result":{"hasMore":false,"view":"strategy-catalog","rows":[{"id":"fixture","fields":{"name":"Fixture family","version":"1","status":"Draft"}}],"choices":{"kind":["Family","Structure"]},"context":{"kind":"Family","selectedId":"fixture","editing":if editing{"True"}else{"False"},"definition":r#"{"Key":{"Kind":1,"Id":"00000000-0000-0000-0000-000000000001","Version":1},"Code":"Fixture","Name":"Fixture family","Description":"","Settings":{},"Families":[],"Structures":[],"Variants":[],"Products":[],"Capabilities":[],"ExpiryGroups":[],"Legs":[],"VariantLegs":[],"Parameters":[],"PipelineParameters":[],"LegacyFamilies":[]}"#,"templates":"[]","references":"[]","products":"[]"}},"outcomes":[]})).unwrap();
    (c, s)
}
#[test]
fn saving_keeps_exact_immutable_key_and_decimal_parameter_text() {
    let (c, s) = fixture(true);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (StrategyCatalog, Option<String>)| {
            if let Some(i) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(i);
            }
        },
        (StrategyCatalog::default(), None),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.state_mut().0.definition["Settings"] =
        serde_json::from_str(r#"{"TargetNetDelta":0.1234567890123456789012345678}"#).unwrap();
    h.get_by_label("Save catalog draft").click();
    h.run();
    let intent: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(intent["originalId"], "fixture");
    assert_eq!(intent["expectedGeneration"], 7);
    let d = intent["fields"]["definition"].as_str().unwrap();
    assert!(d.contains("0.1234567890123456789012345678"));
    assert!(h.state().0.pending.is_some());
}
#[test]
fn publication_cancel_does_not_write() {
    let (c, s) = fixture(false);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (StrategyCatalog, Option<String>)| {
            if let Some(i) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(i);
            }
        },
        (StrategyCatalog::default(), None),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.get_by_label("Publish exact catalog version").click();
    h.run();
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().1.is_none());
    assert!(h.state().0.confirmation.is_none());
}
#[test]
fn report_publication_preserves_unsaved_name_and_discarded_copy_reload_replaces_it() {
    let (c, s) = fixture(true);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (StrategyCatalog, ContractPublication)| {
            state.0.draw(ctx, &state.1, &c, true);
        },
        (StrategyCatalog::default(), s),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.state_mut().0.definition["Name"] = serde_json::json!("Unsaved name");
    h.state_mut().1.generation += 1;
    h.run();
    assert_eq!(h.state().0.definition["Name"], "Unsaved name");
    h.state_mut().1.generation += 1;
    h.state_mut()
        .1
        .result
        .context
        .insert("editing".into(), "False".into());
    h.run();
    assert_eq!(h.state().0.definition["Name"], "Fixture family");
}
