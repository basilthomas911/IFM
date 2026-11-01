use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    Catalog,
    actor_health::ActorHealth,
    operations_health::OperationsHealth,
    contract_editor::{ContractPublication, ContractResult, ContractRow},
};
use std::collections::BTreeMap;


/// Exercise actual stage, worker and actor views with overflowing names, rows and detail properties.
#[test]
fn health_panes_keep_long_published_text_and_bounded_scrollbars() {
    for mode in ["stage", "dataset", "actor"] {
        let catalog: Catalog =
            serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
        let long_name = format!(
            "Published {mode} name {} END",
            "with long identity ".repeat(8)
        );
        let long_detail = format!(
            "Published detail {} END",
            "with full diagnostic context ".repeat(10)
        );
        let publication = ContractPublication {
            result: ContractResult {
                view: if mode == "actor" {
                    "actor-health"
                } else {
                    "operations-health"
                }
                .into(),
                rows: (0..80)
                    .map(|index| {
                        let mut fields = BTreeMap::from([
                            ("kind".into(), mode.into()),
                            (
                                (if mode == "actor" {
                                    "name"
                                } else if mode == "stage" {
                                    "Stage"
                                } else {
                                    "Dataset"
                                })
                                .into(),
                                format!("{index}: {long_name}"),
                            ),
                            (
                                (if mode == "actor" { "status" } else { "Status" }).into(),
                                "Green".into(),
                            ),
                        ]);
                        fields.extend((0..40).map(|field| {
                            (
                                format!("Diagnostic{field:02}"),
                                format!("{field}: {long_detail}"),
                            )
                        }));
                        ContractRow {
                            id: index.to_string(),
                            fields,
                            ..Default::default()
                        }
                    })
                    .collect(),
                ..Default::default()
            },
            ..Default::default()
        };
        let mut operations = OperationsHealth::default();
        operations.datasets = mode == "dataset";
        let mut h = Harness::builder()
            .with_size(egui::vec2(420., 900.))
            .build_state(
                move |ctx, state: &mut (OperationsHealth, ActorHealth)| {
                    ifm_ui_egui::presentation::apply(ctx);
                    if mode == "actor" {
                        state.1.draw(ctx, &publication, &catalog, false);
                    } else {
                        state.0.draw(ctx, &publication, &catalog, false);
                    }
                },
                (
                    operations,
                    ActorHealth {
                        selected: Some("0".into()),
                        ..Default::default()
                    },
                ),
            );
        h.run();
        let panes = if mode == "actor" {
            h.state().1.panes
        } else {
            h.state().0.panes
        }
        .unwrap();
        assert!(
            panes
                .iter()
                .all(|pane| pane.right() <= 420. && pane.bottom() <= 900.)
        );
        assert!(
            h.query_all_by_label(&format!("0: {long_name}"))
                .next()
                .is_some()
        );
        assert!(
            h.query_all_by_label(&format!("0: {long_detail}"))
                .next()
                .is_some()
        );
        if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
            h.render()
                .unwrap()
                .save(std::path::Path::new(&directory).join(format!("{mode}-overflow.png")))
                .unwrap();
        }
    }
}
