use eframe::egui;
use egui_kittest::Harness;
use ifm_ui_egui::presentation;
use std::collections::BTreeMap;

/// Draft text and rendered field geometry for mixed dialog layouts.
#[derive(Default)]
struct State {
    /// Editable value retained across frames.
    value: String,
    /// Caption and associated control rectangles, keyed by caption.
    fields: BTreeMap<String, (egui::Rect, egui::Rect)>,
}


/// Return a painted text run's right edge and font baseline, including galley alignment.
fn painted(shapes: &[egui::epaint::ClippedShape], caption: &str) -> (f32, f32) {
    /// Search recursively through grouped paint shapes.
    fn find(shape: &egui::Shape, caption: &str) -> Option<(f32, f32)> {
        match shape {
            egui::Shape::Vec(shapes) => shapes.iter().find_map(|shape| find(shape, caption)),
            egui::Shape::Text(text) if text.galley.text() == caption => {
                let row = &text.galley.rows[0];
                Some((
                    text.pos.x + text.galley.rect.right(),
                    text.pos.y + row.pos.y + row.glyphs[0].pos.y,
                ))
            }
            _ => None,
        }
    }
    shapes
        .iter()
        .find_map(|shape| find(&shape.shape, caption))
        .unwrap()
}


/// Short and long captions retain the same gap in two- and four-column forms at each DPI.
#[test]
fn mixed_dialog_fields_have_twenty_pixel_gaps_and_shared_baselines() {
    for columns in [2, 4] {
        for dpi in [1., 1.5, 2.] {
            let mut h = Harness::new_state(
                move |ctx, state: &mut State| {
                    presentation::apply(ctx);
                    egui::CentralPanel::default().show(ctx, |ui| {
                        presentation::form_grid(ui, "mixed-form")
                            .num_columns(columns)
                            .show(ui, |ui| {
                                for (i, caption) in
                                    ["Id:", "A much longer caption:", "Date:", "Selection:"]
                                        .iter()
                                        .enumerate()
                                {
                                    let label = presentation::form_caption(ui, *caption);
                                    let control = if i == 3 {
                                        egui::ComboBox::from_id_salt("choice")
                                            .width(160.)
                                            .selected_text("Selected value")
                                            .show_ui(ui, |_| {})
                                            .response
                                    } else {
                                        ui.add_sized(
                                            [160., 26.],
                                            egui::TextEdit::singleline(&mut state.value)
                                                .vertical_align(egui::Align::Center),
                                        )
                                    };
                                    state
                                        .fields
                                        .insert((*caption).into(), (label.rect, control.rect));
                                    if columns == 2 || i % 2 == 1 {
                                        ui.end_row();
                                    }
                                }
                            });
                    });
                },
                State {
                    value: "Draft value".into(),
                    ..Default::default()
                },
            );
            h.set_size(egui::vec2(1100., 700.));
            h.input_mut()
                .viewports
                .get_mut(&egui::ViewportId::ROOT)
                .unwrap()
                .native_pixels_per_point = Some(dpi);
            h.run();
            for (caption, (_, control)) in &h.state().fields {
                let (right, baseline) = painted(&h.output().shapes, caption);
                assert!(
                    ((control.left() - right) * dpi - 20.).abs() <= 1.,
                    "gap {caption} at {dpi}: {right} {control:?}"
                );
                let expected = if caption == "Selection:" {
                    "Selected value"
                } else {
                    "Draft value"
                };
                // Identify the value inside this control, since other rows reuse the draft text.
                let values: Vec<_> = h.output().shapes.iter().filter(|shape| matches!(&shape.shape, egui::Shape::Text(text) if text.galley.text() == expected && control.expand(1.).contains(text.pos))).cloned().collect();
                let (_, value_baseline) = painted(&values, expected);
                assert!(
                    ((baseline - value_baseline) * dpi).abs() <= 1.,
                    "baseline {caption} at {dpi}: {baseline} {value_baseline}"
                );
            }
        }
    }
}


/// Managed portfolio and fund dialogs keep the same gap across compact creation forms and larger property editors.
#[test]
fn published_portfolio_dialogs_align_every_caption_to_its_control() {
    use ifm_ui_egui::{portfolio::PortfolioManager, contract_editor::ContractPublication};
    for key in [
        "new-portfolio",
        "version-portfolio",
        "portfolio-state",
        "new-fund",
        "version-fund",
        "fund-state",
        "allocation",
        "envelope",
        "assignment",
        "posting",
        "setup",
        "authority",
        "account-version",
        "rule-version",
    ] {
        for dpi in [1., 1.5, 2.] {
            let document = serde_json::json!({"dialogs":[{"key":key,"title":format!("Layout {key}"),"width":800,"height":480,"captionWidth":170,"buttons":["Save","Cancel"],"fields":[
                {"key":"name","caption":"Name","kind":"text","value":"Draft name","enabled":true},
                {"key":"reference","caption":"A longer reference","kind":"text","value":"Draft reference","enabled":true},
                {"key":"count","caption":"Count","kind":"number","value":"42","enabled":true,"minimum":0,"maximum":100}
            ]}]});
            let mut publication = ContractPublication::default();
            publication.generation = 1;
            publication.result.view = "portfolio".into();
            publication.result.documents.insert(
                "portfolio".into(),
                ifm_ui_egui::wire::encode_value(&document).unwrap().into(),
            );
            let mut manager = PortfolioManager::default();
            manager.open();
            let mut h = Harness::new_state(
                move |ctx, manager: &mut PortfolioManager| {
                    presentation::apply(ctx);
                    manager.draw(ctx, &publication, true);
                },
                manager,
            );
            h.set_size(egui::vec2(1600., 1000.));
            h.input_mut()
                .viewports
                .get_mut(&egui::ViewportId::ROOT)
                .unwrap()
                .native_pixels_per_point = Some(dpi);
            h.run();
            for i in 0..10 {
                let label = h.state().controls[&format!("metric-caption:{i}")];
                let control = h.state().controls[&format!("metric-value:{i}")];
                let footer = h.state().controls["metrics-and-status"];
                assert!(
                    ((control.left() - label.right()) * dpi - 20.).abs() <= 1.,
                    "metric {i} at {dpi}"
                );
                assert!(
                    control.right() <= footer.right() + 1. && control.bottom() <= footer.bottom(),
                    "metric {i} outside footer in {key} at {dpi}: {control:?} footer {footer:?}"
                );
            }
            for field in ["name", "reference", "count"] {
                let prefix = format!("{key}.{field}");
                let label = h.state().controls[&format!("{prefix}.caption")];
                let control = h.state().controls[&prefix];
                assert!(
                    ((control.left() - label.right()) * dpi - 20.).abs() <= 1.,
                    "{prefix} at {dpi}: {label:?} {control:?}"
                );
            }
            if dpi == 1. && matches!(key, "new-fund" | "rule-version") {
                if let Ok(directory) = std::env::var("IFM_ALIGNMENT_SCREENSHOTS") {
                    h.render()
                        .unwrap()
                        .save(std::path::Path::new(&directory).join(format!("{key}.png")))
                        .unwrap();
                }
            }
        }
    }
}
