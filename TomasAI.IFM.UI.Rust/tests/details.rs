use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::{Catalog, DetailsView, Snapshot};
fn data() -> (Catalog, Snapshot) {
    let c = serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let s=serde_json::from_str(r#"{"schema":1,"epoch":"test","generation":1,"mode":"Fixture","workflowId":"test","revision":1,"roots":[{"id":"signal","label":"ITI Signal","properties":[{"name":"DailyPnl","value":"-125.50"}],"children":[{"id":"signal/child","label":"Signal child","properties":[{"name":"Value","value":"16"}],"children":[]}]}]}"#).unwrap();
    (c, s)
}
#[test]
fn cached_tree_expands_collapses_and_selects_inspector() {
    let (catalog, snapshot) = data();
    let mut harness = Harness::new_state(
        move |ctx, view: &mut DetailsView| view.draw(ctx, &snapshot, &catalog),
        DetailsView::default(),
    );
    ifm_ui_rust::apply_winforms_typography(&harness.ctx).unwrap();
    harness.run();
    harness.get_by_label("ITI Signal").click();
    harness.run();
    assert!(harness.query_by_label("Signal child").is_some());
    assert_eq!(harness.state().selected.as_deref(), Some("signal"));
    harness.get_by_label("Signal child").click();
    harness.run();
    assert_eq!(harness.state().selected.as_deref(), Some("signal/child"));
    ifm_ui_rust::apply_winforms_typography(&harness.ctx).unwrap();
    harness.run();
    harness.get_by_label("ITI Signal").click();
    harness.run();
    assert!(harness.query_by_label("Signal child").is_none());
}
#[test]
fn invalid_snapshot_is_rejected() {
    assert!(serde_json::from_str::<Snapshot>("{}").is_err());
}

#[cfg(feature = "visual-tests")]
#[test]
fn details_default_layout_image() {
    let (catalog, snapshot) = data();
    let mut harness = Harness::builder()
        .with_size(eframe::egui::vec2(1100.0, 800.0))
        .build_state(
            move |ctx, view: &mut DetailsView| view.draw(ctx, &snapshot, &catalog),
            DetailsView::default(),
        );
    ifm_ui_rust::apply_winforms_typography(&harness.ctx).unwrap();
    harness.run();
    harness.snapshot("strategy-details-default");
}
