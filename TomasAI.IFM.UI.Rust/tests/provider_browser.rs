use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{
    Catalog,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    provider_browser::ProviderBrowser,
};
use std::collections::BTreeMap;
fn fixture(count: usize) -> (Catalog, ContractPublication) {
    let c = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let s = ContractPublication {
        schema: 1,
        generation: 9,
        status: "Ready".into(),
        busy: false,
        result: ContractResult { documents: Default::default(),
            view: "provider-definitions".into(),
            rows: (0..count)
                .map(|i| ContractRow { data: None,
                    id: format!("snapshot:GLBX.MDP3:1:{i}"),
                    fields: BTreeMap::from([
                        ("rawSymbol".into(), format!("ESZ6 {i}")),
                        ("strike".into(), "6500.25".into()),
                        ("definitionDigest".into(), "verified-digest".into()),
                    ]),
                })
                .collect(),
            has_more: true,
            choices: BTreeMap::new(),
            context: BTreeMap::from([("snapshotId".into(), "pinned-snapshot".into())]),
        },
        outcomes: vec![],
        ..Default::default()
    };
    (c, s)
}
#[test]
fn search_emits_exact_filters_once_and_pending_disables_further_search() {
    let (c, s) = fixture(3);
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ProviderBrowser, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p)
            }
        },
        (ProviderBrowser::default(), None),
    );
    h.set_size(eframe::egui::vec2(1100., 900.));
    h.run();
    h.get_by_label("Search definitions").click();
    h.run();
    let p: serde_json::Value = serde_json::from_str(h.state().1.as_ref().unwrap()).unwrap();
    assert_eq!(p["view"], "provider-definitions");
    assert_eq!(p["fields"]["dataset"], "GLBX.MDP3");
    assert_eq!(p["fields"]["root"], "ES");
    assert_eq!(p["expectedGeneration"], 9);
    assert!(h.state().0.pending.is_some());
    assert!(h.query_by_label("Confirm removal").is_none());
}
#[test]
fn selected_provider_identity_and_exact_decimal_are_retained_with_virtual_rows() {
    let (c, s) = fixture(100000);
    let mut h = Harness::new_state(
        move |ctx, state: &mut ProviderBrowser| {
            state.draw(ctx, &s, &c, true);
        },
        ProviderBrowser::default(),
    );
    h.set_size(eframe::egui::vec2(1100., 900.));
    h.run();
    assert!(h.state().drawn_rows > 0 && h.state().drawn_rows < 40);
    h.get_by_label("ESZ6 0").click();
    h.run();
    assert_eq!(
        h.state().selected.as_deref(),
        Some("snapshot:GLBX.MDP3:1:0")
    );
    assert!(h.query_by_label("6500.25").is_some());
    assert!(h.query_by_label("verified-digest").is_some());
}

#[test]
fn mapping_preview_and_confirmation_keep_exact_selection_and_cancel_does_not_dispatch() {
    let (c, mut s) = fixture(3);
    let mut browser = ProviderBrowser::default();
    browser.selected = Some("snapshot:GLBX.MDP3:1:0".into());
    browser.mapping_open = true;
    browser
        .mapping
        .insert("existingContractId".into(), "ES20261218".into());
    let request: serde_json::Value = serde_json::from_str(&browser.request("preview", &s)).unwrap();
    assert_eq!(request["originalId"], "snapshot:GLBX.MDP3:1:0");
    assert_eq!(request["fields"]["existingContractId"], "ES20261218");
    s.result
        .context
        .insert("previewToken".into(), "exact-token".into());
    s.result
        .context
        .insert("previewSelectedId".into(), "snapshot:GLBX.MDP3:1:0".into());
    s.result.context.insert(
        "previewContract".into(),
        r#"{"contractId":"ES20261218","schemaVersion":1}"#.into(),
    );
    s.outcomes.push(ifm_ui_rust::lookup_editor::EditorOutcome {
        request_id: request["requestId"].as_str().unwrap().into(),
        state: "Completed".into(),
        message: "Backend completion received".into(),
    });
    let mut h = Harness::new_state(
        move |ctx, state: &mut (ProviderBrowser, Option<String>)| {
            if let Some(p) = state.0.draw(ctx, &s, &c, true) {
                state.1 = Some(p)
            }
        },
        (browser, None),
    );
    h.set_size(eframe::egui::vec2(1100., 900.));
    h.run();
    assert!(h.query_by_label("Confirm draft import").is_some());
    h.get_by_label("Cancel").click();
    h.run();
    assert!(!h.state().0.mapping_open);
    assert!(h.state().1.is_none());
}
