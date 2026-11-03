//! Operational diagnostics and scheduled-task administration. Actions are sent through existing managed services and require the published eligibility/confirmation state.

/// Actor hierarchy, resource health, and operator actions. A confirmation captures generation and target identity so a changed selection cannot silently redirect an operation.
pub mod actor_health;
/// Operational health and dataset projections. Visible rows are rendered from the published query result; severity colors communicate backend state without recalculating it.
pub mod operations_health;
/// Scheduled-task setup, daily-time editing, preview/confirmation, and run history. Draft schedule fields are sent to the managed scheduler; Rust does not create its own timer or execute jobs.
pub mod scheduled_tasks;


/// Allocate two equal-height bounded panes below a health view's controls.
pub(super) fn split_health_body(ui: &mut eframe::egui::Ui) -> [eframe::egui::Rect; 2] {
    use eframe::egui;
    let gap = 8. / ui.ctx().pixels_per_point();
    let available = ui.available_rect_before_wrap();
    let height = (available.height() - gap).max(0.) * 0.5;
    let upper = egui::Rect::from_min_size(available.min, egui::vec2(available.width(), height));
    let lower = egui::Rect::from_min_size(
        egui::pos2(available.left(), upper.bottom() + gap),
        upper.size(),
    );
    ui.allocate_rect(available, egui::Sense::hover());
    [upper, lower]
}


/// Keep the complete pane border and optional vertical scrollbar edge, without a duplicate bottom rule.
pub(super) fn health_scroll<R>(
    ui: &mut eframe::egui::Ui,
    bounds: eframe::egui::Rect,
    id: impl std::hash::Hash,
    scrollbar_border: bool,
    contents: impl FnOnce(&mut eframe::egui::Ui) -> R,
) -> eframe::egui::scroll_area::ScrollAreaOutput<R> {
    use eframe::egui;
    let pixel = 1. / ui.ctx().pixels_per_point();
    let inner = bounds.shrink(4. * pixel);
    let mut child = ui.new_child(egui::UiBuilder::new().max_rect(inner));
    child.set_clip_rect(inner.intersect(ui.clip_rect()));
    child.style_mut().wrap_mode = Some(egui::TextWrapMode::Extend);
    let scroll = egui::ScrollArea::both()
        .id_salt(id)
        .auto_shrink([false, false])
        .max_width(inner.width())
        .max_height(inner.height());
    let output = scroll.show(&mut child, contents);
    if scrollbar_border {
        let viewport = output.inner_rect.intersect(child.clip_rect());
        child.painter().vline(
            viewport.right() - pixel / 2.,
            viewport.top()..=viewport.bottom(),
            egui::Stroke::new(pixel, crate::presentation::BORDER),
        );
    }
    ui.painter().rect_stroke(
        bounds,
        0.,
        egui::Stroke::new(pixel, crate::presentation::BORDER),
        egui::StrokeKind::Inside,
    );
    output
}


/// Render published health properties as an expandable tree without recomputing any health values.
pub(super) fn detail_tree(
    ui: &mut eframe::egui::Ui,
    id: &str,
    title: &str,
    fields: &std::collections::BTreeMap<String, String>,
    caption: impl Fn(&str) -> String,
) {
    use eframe::egui;
    use crate::presentation::TradingLabelUi as _;


    /// Group known published properties for navigation; unknown fields remain visible in Details.
    fn group(key: &str) -> &'static str {
        match key {
            "name" | "parent" | "Stage" | "Dataset" | "ProcessId" | "Generation"
            | "WorkerInstance" => "Identity",
            "Status" | "status" | "Session" | "Requirement" | "Running" | "Healthy" | "Reason"
            | "ReasonCode" => "State",
            "Pending"
            | "Capacity"
            | "HighWater"
            | "Received"
            | "Completed"
            | "Failed"
            | "Coalesced"
            | "Saturated"
            | "NativeConsumedProduced"
            | "RingUsedCapacity"
            | "ChannelUsedCapacity"
            | "AggregationCompletedStarted" => "Traffic and capacity",
            "CooperativeAttempts"
            | "ProcessReplacements"
            | "ReplacementLatched"
            | "GracefulStopSucceeded"
            | "ForcedTermination" => "Recovery",
            "SourceAge" | "MarketDataAsOfUtc" | "LastSucceededUtc" | "OldestPending"
            | "P50Latency" | "P95Latency" | "P99Latency" | "IncidentAge" | "LastHealthyUtc"
            | "NextProbeUtc" | "StartedOnUtc" => "Timing",
            _ => "Details",
        }
    }
    ui.push_id(id, |ui| {
        egui::CollapsingHeader::new(title)
            .default_open(true)
            .show(ui, |ui| {
                for category in [
                    "State",
                    "Identity",
                    "Traffic and capacity",
                    "Recovery",
                    "Timing",
                    "Details",
                ] {
                    let values: Vec<_> = fields
                        .iter()
                        .filter(|(key, _)| key.as_str() != "kind" && group(key) == category)
                        .collect();
                    if values.is_empty() {
                        continue;
                    }
                    egui::CollapsingHeader::new(category)
                        .default_open(true)
                        .show(ui, |ui| {
                            crate::presentation::form_grid(ui, category)
                                .num_columns(2)
                                .show(ui, |ui| {
                                    for (key, value) in values {
                                        crate::presentation::form_caption(ui, caption(key));
                                        if key.eq_ignore_ascii_case("status") {
                                            ui.trading_colored_label(
                                                operations_health::status_color(value),
                                                operations_health::status_description(value),
                                            );
                                        } else {
                                            ui.trading_label(value);
                                        }
                                        ui.end_row();
                                    }
                                });
                        });
                }
            });
    });
}


/// Legacy standalone framing remains available alongside the embedded sidebar health views.
fn health_surface(
    ctx: &eframe::egui::Context,
    title: &str,
    open: Option<&mut bool>,
    contents: impl FnOnce(&mut eframe::egui::Ui),
) {
    use eframe::egui;
    if let Some(open) = open {
        let mut close = false;
        egui::Window::new(crate::presentation::dialog_title(title))
            .open(open)
            .default_pos(ctx.screen_rect().center() - egui::vec2(500., 340.))
            .default_size(egui::vec2(1000., 680.))
            .min_size(egui::vec2(720., 420.))
            .resizable(true)
            .scroll([false, true])
            .show(ctx, |ui| {
                contents(ui);
                ui.separator();
                close = ui.button("Close").clicked();
            });
        if close {
            *open = false;
        }
    } else {
        egui::CentralPanel::default().show(ctx, contents);
    }
}

#[cfg(test)]
mod health_scroll_tests {
    use super::*;
    use eframe::egui;


    /// Long and deep health content remains reachable while scrollbars stay inside the complete pane border.
    #[test]
    fn health_scroll_preserves_border_and_reaches_both_content_edges() {
        for scale in [1., 1.5, 2.] {
            let ctx = egui::Context::default();
            ctx.set_pixels_per_point(scale);
            crate::presentation::apply(&ctx);
            let bounds = egui::Rect::from_min_size(egui::pos2(12., 12.), egui::vec2(260., 160.));
            let mut latest = None;
            let mut last_label = egui::Rect::NOTHING;
            for frame in 0..4 {
                let result = ctx.run(
                    egui::RawInput {
                        screen_rect: Some(egui::Rect::from_min_size(
                            egui::Pos2::ZERO,
                            egui::vec2(300., 200.),
                        )),
                        ..Default::default()
                    },
                    |ctx| {
                        egui::CentralPanel::default().show(ctx, |ui| {
                            latest = Some(health_scroll(ui, bounds, "overflow-health", false, |ui| {
                                for row in 0..80 {
                                    last_label = ui
                                        .label(format!(
                                            "Row {row}: {} END",
                                            "long published detail ".repeat(12)
                                        ))
                                        .rect;
                                }
                            }));
                        });
                    },
                );
                let output = latest.as_ref().unwrap();
                assert!(output.content_size.x > output.inner_rect.width());
                assert!(output.content_size.y > output.inner_rect.height());
                assert!(bounds.contains_rect(output.inner_rect));
                let border = result.shapes.iter().rfind(
                    |shape| matches!(&shape.shape, egui::Shape::Rect(rect) if rect.rect == bounds),
                );
                let border = border.expect("Complete border must be painted after the scroll area");
                assert!(border.clip_rect.contains_rect(bounds));
                if frame == 3 {
                    assert!(output.state.offset.x > 0. && output.state.offset.y > 0.);
                    assert!(
                        output
                            .inner_rect
                            .expand(1.)
                            .contains(last_label.max - egui::vec2(1., 1.)),
                        "Last text edge is unreachable: {last_label:?}, viewport={:?}",
                        output.inner_rect
                    );
                }
                let mut state = output.state;
                state.offset =
                    (output.content_size - output.inner_rect.size()).max(egui::Vec2::ZERO);
                state.store(&ctx, output.id);
            }
        }
    }
}
