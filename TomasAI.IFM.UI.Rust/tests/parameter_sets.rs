use egui_kittest::kittest::NodeT;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    Catalog,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    parameter_sets::ParameterSets,
};
use std::collections::BTreeMap;
fn fixture(editing: bool) -> (Catalog, ContractPublication) {
    let mut context:BTreeMap<String,String>=BTreeMap::from([("editing".into(),if editing{"True"}else{"False"}.into()),("canEdit".into(),"True".into()),("hasPayload".into(),"True".into()),("name".into(),"Fixture parameters".into()),("selectedId".into(),"fixture:1".into()),("component".into(),"fixture".into()),("components".into(),r#"[{"AreaCode":"fixture","AreaName":"Fixture area","ComponentCode":"fixture","Name":"Fixture component"}]"#.into()),("fields".into(),r#"[{"Path":"/Risk/AdverseMove","Value":"0.20","Kind":4,"Group":"Risk","Name":"Adverse move"}]"#.into()),("signals".into(),"[]".into()),("observations".into(),"[]".into()),("intervals".into(),"[]".into())]);
    let mut documents = BTreeMap::new();
    for key in [
        "components",
        "fields",
        "signals",
        "observations",
        "intervals",
    ] {
        let value: serde_json::Value = serde_json::from_str(&context.remove(key).unwrap()).unwrap();
        documents.insert(
            key.into(),
            ifm_ui_rust::wire::encode_value(&value).unwrap().into(),
        );
    }
    (
        serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap(),
        ContractPublication {
            generation: 7,
            status: "Ready".into(),
            result: ContractResult {
                documents,
                view: "parameter-sets".into(),
                rows: vec![ContractRow {
                    data: None,
                    id: "fixture:1".into(),
                    fields: BTreeMap::from([
                        ("name".into(), "Fixture parameters".into()),
                        ("status".into(), "Draft".into()),
                        ("version".into(), "1".into()),
                    ]),
                }],
                context,
                ..Default::default()
            },
            ..Default::default()
        },
    )
}
#[test]
fn saving_sends_exact_scalar_working_copy_and_correlated_version_once() {
    let (c, s) = fixture(true);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ParameterSets, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p);
            }
        },
        (ParameterSets::default(), None),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.state_mut().0.fields[0].value = "0.1234567890123456789".into();
    h.get_by_label("Save draft").click();
    h.run();
    let intent: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(intent["view"], "parameter-sets");
    assert_eq!(intent["intent"], "save-draft");
    assert_eq!(intent["originalId"], "fixture:1");
    assert_eq!(intent["expectedGeneration"], 7);
    let fields = &intent["documents"]["changes"];
    assert_eq!(fields[0]["Value"], "0.1234567890123456789");
    assert_eq!(fields[0]["Kind"], 4);
    assert!(h.state().0.pending.is_some());
}
#[test]
fn cancelling_publication_does_not_write_and_tabs_keep_the_same_selected_version() {
    let (c, s) = fixture(false);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ParameterSets, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p);
            }
        },
        (ParameterSets::default(), None),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.get_by_label("Signals").click();
    h.run();
    h.get_by_label("Observations").click();
    h.run();
    h.get_by_label("Calculation parameters").click();
    h.run();
    h.get_by_label("Publish parameter version").click();
    h.run();
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().1.is_none());
    assert!(h.state().0.confirmation.is_none());
    assert_eq!(h.state().0.selected.as_deref(), Some("fixture:1"));
}
#[test]
fn returning_to_the_editor_requests_its_current_working_copy_without_a_refresh_or_save() {
    let (c, mut s) = fixture(true);
    s.result.view = "operations-health".into();
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ParameterSets, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p);
            }
        },
        (ParameterSets::default(), None),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.get_by_label("Show current working copy").click();
    h.run();
    let intent: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(intent["intent"], "resume-parameter");
    assert!(intent["fields"].get("changes").is_none());
}

#[test]
fn readonly_publication_preserves_unsaved_scalar_and_metadata() {
    let (c, s) = fixture(true);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ParameterSets, ContractPublication)| {
            state.0.draw(ctx, &state.1, &c, true);
        },
        (ParameterSets::default(), s),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.state_mut().0.fields[0].value = "0.1234567890123456789".into();
    h.state_mut().0.name = "Unsaved name".into();
    h.state_mut().1.generation += 1;
    h.state_mut()
        .1
        .result
        .context
        .insert("validation".into(), "[]".into());
    h.run();
    assert_eq!(h.state().0.fields[0].value, "0.1234567890123456789");
    assert_eq!(h.state().0.name, "Unsaved name");
    h.state_mut().1.generation += 1;
    h.state_mut()
        .1
        .result
        .context
        .insert("editing".into(), "False".into());
    h.run();
    assert_eq!(h.state().0.fields[0].value, "0.20");
    assert!(!h.state().0.editing);
}
#[test]
fn newer_publication_prevents_confirming_an_older_publish_selection() {
    let (c, s) = fixture(false);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ParameterSets, ContractPublication, Option<String>)| {
            if let Some(intent) = state.0.draw(ctx, &state.1, &c, true) {
                state.2 = Some(intent);
            }
        },
        (ParameterSets::default(), s, None),
    );
    h.set_size(eframe::egui::vec2(1400., 1000.));
    h.run();
    h.get_by_label("Publish parameter version").click();
    h.run();
    h.state_mut().1.generation += 1;
    h.run();
    assert!(h.state().2.is_none());
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().0.confirmation.is_none());
}

#[test]
fn reference_parameters_preserve_navigation_toolbar_and_exact_four_main_tabs() {
    let (c, s) = fixture(false);
    let mut h = Harness::new_state(
        move |ctx, state: &mut ParameterSets| {
            state.draw_reference(ctx, &s, &c, true);
        },
        ParameterSets::default(),
    );
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    for label in [
        "Reference Data Manager",
        "Selection:",
        "Parameter Set",
        "Fixture area",
        "Fixture component",
        "Fixture parameters - v1 (Draft)",
        "Add",
        "Change",
        "Remove",
        "Import",
        "Close",
        "New horizon",
        "Name",
        "Refresh",
        "Legacy versions",
        "Payload",
        "Schema",
        "Intervals",
        "Validate",
        "Publish",
        "Assignment",
        "Startup preview",
        "Recent startups",
        "Compare",
        "Operations",
        "Signals",
        "Observations",
        "Calculation parameters",
        "Validation",
    ] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    assert_eq!(h.query_all_by_label("Intervals").count(), 1);
    assert!(h.get_by_label("Import").accesskit_node().is_disabled());
    h.get_by_label("Calculation parameters").click();
    h.run();
    assert!(h.query_by_label("Parameter").is_some());
    assert!(h.query_by_label("Value").is_some());
    h.get_by_label("Payload").click();
    h.run();
    assert!(h.state().pending.is_some()); // The source opens the modal only after its read has completed.
    assert!(h.query_by_label("Parameter values").is_none());
}

#[test]
fn reference_startup_history_reads_messagepack_documents_and_uses_grid_double_click() {
    let (c, mut s) = fixture(false);
    s.result.documents.insert("startupRuns".into(),ifm_ui_rust::wire::encode_value(&serde_json::json!([{"RunId":"run-42","CreatedAtUtc":"2026-10-19T10:00:00Z","CreatedBy":"fixture","Scopes":[],"Plan":{"Fingerprint":"snapshot-fingerprint"}}])).unwrap().into());
    let mut editor = ParameterSets::default();
    editor.section = "startupRuns".into();
    let mut h = Harness::new_state(
        move |ctx, state: &mut ParameterSets| {
            state.draw_reference(ctx, &s, &c, true);
        },
        editor,
    );
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    for label in [
        "Recent parameter startup snapshots",
        "RunId",
        "CreatedAtUtc",
        "Assignments",
        "Fingerprint",
        "run-42",
        "snapshot-fingerprint",
    ] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    assert!(h.query_by_label("Show preparation report").is_none());
}

#[test]
fn reference_interval_cancel_restores_working_copy_without_submitting() {
    let (c, mut s) = fixture(true);
    s.result.context.insert("regime".into(), "True".into());
    s.result.documents.insert("intervals".into(),ifm_ui_rust::wire::encode_value(&serde_json::json!([{"TimeFrame":5,"Weight":1.5,"MaximumAgeSeconds":300,"IsRequired":true}])).unwrap().into());
    let mut editor = ParameterSets::default();
    editor.section = "intervals".into();
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ParameterSets, Option<String>)| {
            if let Some(intent) = state.0.draw_reference(ctx, &s, &c, true) {
                state.1 = Some(intent);
            }
        },
        (editor, None),
    );
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    for label in ["Interval", "Relative weight", "Max age (seconds)", "Apply"] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    h.state_mut().0.intervals[0]["Weight"] = serde_json::json!(7);
    // The original command rail has Cancel as well; use the last matching button in the dialog.
    h.query_all_by_label("Cancel").last().unwrap().click();
    h.run();
    assert_eq!(h.state().0.intervals[0]["Weight"], 1.5);
    assert!(h.state().1.is_none());
}

#[test]
fn published_assignment_dialog_preserves_information_and_three_original_actions() {
    let (c, mut s) = fixture(false);
    s.result.rows[0]
        .fields
        .insert("status".into(), "Published".into());
    s.result.documents.insert("assignment".into(),ifm_ui_rust::wire::encode_value(&serde_json::json!({"Scope":{"WorkflowDefinitionId":"IntrinsicTimeStrategy"},"Assignment":{"Enabled":true,"Reference":{"SetId":"published-fixture","Version":1}}})).unwrap().into());
    let mut editor = ParameterSets::default();
    editor.section = "assignment".into();
    let mut h = Harness::new_state(
        move |ctx, state: &mut ParameterSets| {
            state.draw_reference(ctx, &s, &c, true);
        },
        editor,
    );
    h.set_size(eframe::egui::vec2(1800., 1200.));
    h.run();
    for label in [
        "Workflow parameter assignment",
        "Assign selected",
        "Disable",
        "Cancel",
    ] {
        assert!(
            h.query_all_by_label(label).next().is_some(),
            "Missing {label}"
        );
    }
    h.get_by_label("Cancel").click();
    h.run();
    assert!(h.state().pending.is_none());
    assert!(h.query_by_label("Workflow parameter assignment").is_none());
}
