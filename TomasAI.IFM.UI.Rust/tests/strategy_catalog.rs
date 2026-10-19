use egui_kittest::kittest::NodeT;
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

#[test]
fn reference_catalog_preserves_selector_rail_and_original_toolbar() {
    let (c, s) = fixture(false);
    let mut h = Harness::new_state(
        move |ctx, state: &mut StrategyCatalog| {
            state.draw_reference(ctx, &s, &c, true);
        },
        StrategyCatalog::default(),
    );
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    for label in [
        "Reference Data Manager",
        "Selection:",
        "Add",
        "Change",
        "Remove",
        "Import",
        "Close",
        "Manage",
        "Start from",
        "Publish",
        "Refresh",
        "Show all catalog data",
    ] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    assert!(h.get_by_label("Import").accesskit_node().is_disabled());
    assert_eq!(h.query_all_by_label("Refresh").count(), 1);
    assert!(h.query_by_label("Resume catalog operation").is_none());
}

#[test]
fn reference_variant_uses_the_original_variant_tab_and_fields() {
    let (c, mut s) = fixture(false);
    s.result.context.insert("kind".into(), "Variant".into());
    let mut definition: serde_json::Value =
        serde_json::from_str(&s.result.context["definition"]).unwrap();
    definition["Key"]["Kind"] = serde_json::json!(4);
    definition["Settings"] = serde_json::json!({"TargetNetDelta":0.1,"BalanceTolerance":0.2,"SymmetricWings":true,"MinimumWingWidth":5,"MaximumWingWidth":50});
    s.result
        .context
        .insert("definition".into(), definition.to_string());
    let mut h = Harness::new_state(
        move |ctx, state: &mut StrategyCatalog| {
            state.draw_reference(ctx, &s, &c, true);
        },
        StrategyCatalog::default(),
    );
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    assert!(h.query_by_label("Variant legs").is_some());
    assert!(h.query_by_label("Additional parameters").is_some());
    h.get_by_label("Variant").click();
    h.run();
    for label in [
        "Structure side",
        "Directional bias",
        "Premium mode",
        "Target net delta",
        "Balance tolerance",
        "Wing symmetry",
        "Equal wing widths",
        "Minimum wing width",
        "Maximum wing width",
    ] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    assert!(h.query_by_label("Add property").is_none());
}

#[test]
fn reference_catalog_selects_the_first_loaded_definition_without_an_extra_click() {
    let (c, mut s) = fixture(false);
    s.result.context.insert("selectedId".into(), "".into());
    s.result.context.insert("definition".into(), "null".into());
    let mut h = Harness::new_state(
        move |ctx, state: &mut (StrategyCatalog, Option<String>)| {
            if let Some(intent) = state.0.draw_reference(ctx, &s, &c, true) {
                state.1 = Some(intent);
            }
        },
        (StrategyCatalog::default(), None),
    );
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    let intent: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(intent["intent"], "catalog-select");
    assert_eq!(intent["originalId"], "fixture");
}
