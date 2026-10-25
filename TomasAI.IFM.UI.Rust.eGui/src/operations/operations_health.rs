//! Operational health and dataset projections. Visible rows are rendered from the published query result; severity colors communicate backend state without recalculating it.

use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
/// Operational dataset/health view selection and pending-query state.
#[derive(Default)]
pub struct OperationsHealth {
    /// Monotonic UI refresh schedule. No task survives a hidden view.
    next_refresh: Option<std::time::Instant>,
    /// Whether the operational-health view displays the dataset section.
    pub datasets: bool,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Count of rows actually drawn in the most recent frame, used by virtualization checks.
    pub drawn_rows: usize,
}
/// Map published status/severity text to its view-specific indicator color.
pub fn status_color(status: &str) -> egui::Color32 {
    match status.to_uppercase().as_str() {
        "GREEN" | "UP" | "HEALTHY" => egui::Color32::LIGHT_GREEN,
        "YELLOW" | "SUSPECT" => egui::Color32::from_rgb(240, 230, 140),
        "ORANGE" | "RECOVERING" | "RESETTING" | "QUALIFYING" => {
            egui::Color32::from_rgb(255, 165, 0)
        }
        "RED" | "DOWN" | "FAILED" => egui::Color32::from_rgb(250, 128, 114),
        _ => egui::Color32::GRAY,
    }
}
impl OperationsHealth {
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns operational dataset/health view selection and pending-query state.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        if self.pending.as_ref().is_some_and(|id| {
            s.outcomes
                .iter()
                .any(|o| &o.request_id == id && o.state != "Pending")
        }) {
            self.pending = None;
        }
        let matching = s.result.view == "operations-health";
        let now = std::time::Instant::now();
        let mut intent = None;
        if live
            && !s.busy
            && self.pending.is_none()
            && self.next_refresh.is_none_or(|next| now >= next)
        {
            intent = Some(self.request(s, now));
        }
        if live {
            ctx.request_repaint_after(
                self.next_refresh
                    .map(|next| next.saturating_duration_since(now))
                    .unwrap_or(std::time::Duration::from_secs(5))
                    .max(std::time::Duration::from_millis(100)),
            );
        }
        egui::CentralPanel::default().show(ctx, |ui| {
            if ui
                .add_enabled(
                    live && !s.busy && self.pending.is_none(),
                    egui::Button::new(c.text(strings::HEALTH_REFRESH)),
                )
                .clicked()
            {
                intent = Some(self.request(s, now));
            }
            if matching {
                ui.trading_colored_label(
                    status_color(
                        s.result
                            .context
                            .get("status")
                            .map(String::as_str)
                            .unwrap_or("Unknown"),
                    ),
                    s.result
                        .context
                        .get("summary")
                        .map(String::as_str)
                        .unwrap_or(""),
                );
                ui.trading_label(
                    s.result
                        .context
                        .get("observation")
                        .map(String::as_str)
                        .unwrap_or(""),
                );
            } else {
                ui.trading_label(&s.status);
            }
            ui.trading_caption(c.text(strings::HEALTH_EXPLANATION));
            ui.horizontal(|ui| {
                if crate::presentation::tab_label(
                    ui,
                    !self.datasets,
                    c.text(strings::HEALTH_STAGES),
                )
                .clicked()
                {
                    self.datasets = false;
                    self.selected = None;
                }
                if crate::presentation::tab_label(
                    ui,
                    self.datasets,
                    c.text(strings::HEALTH_DATASETS),
                )
                .clicked()
                {
                    self.datasets = true;
                    self.selected = None;
                }
            });
            let kind = if self.datasets { "dataset" } else { "stage" };
            let rows = s
                .result
                .rows
                .iter()
                .filter(|r| matching && r.fields.get("kind").is_some_and(|k| k == kind))
                .collect::<Vec<_>>();
            egui::ScrollArea::vertical()
                .max_height(250.)
                .id_salt("health-rows")
                .show_rows(ui, 24., rows.len(), |ui, range| {
                    self.drawn_rows = range.len();
                    for i in range {
                        let row = rows[i];
                        let name = row
                            .fields
                            .get(if self.datasets { "Dataset" } else { "Stage" })
                            .unwrap_or(&row.id);
                        let status = row
                            .fields
                            .get("Status")
                            .map(String::as_str)
                            .unwrap_or("Unknown");
                        if ui
                            .add(
                                egui::Button::new(
                                    egui::RichText::new(format!("{} | {}", name, status))
                                        .color(status_color(status)),
                                )
                                .selected(self.selected.as_ref() == Some(&row.id)),
                            )
                            .clicked()
                        {
                            self.selected = Some(row.id.clone());
                        }
                    }
                });
            if let Some(row) = rows.iter().find(|r| Some(&r.id) == self.selected.as_ref()) {
                egui::ScrollArea::both()
                    .id_salt("health-properties")
                    .show(ui, |ui| {
                        egui::Grid::new("health-grid")
                            .num_columns(2)
                            .striped(true)
                            .show(ui, |ui| {
                                for (key, value) in &row.fields {
                                    if key != "kind" {
                                        ui.trading_caption(c.text(&format!("health_{}", key)));
                                        ui.trading_label(value);
                                        ui.end_row();
                                    }
                                }
                            });
                    });
            }
        });
        intent
    }
    fn request(&mut self, publication: &ContractPublication, now: std::time::Instant) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.next_refresh = Some(now + std::time::Duration::from_secs(5));
        serde_json::json!({"schema":1,"requestId":id,"view":"operations-health","intent":"load","expectedGeneration":publication.generation,"originalId":null,"fields":{}}).to_string()
    }
}

#[cfg(test)]
mod native_refresh_tests {
    use super::*;
    #[test]
    fn health_refresh_is_visible_only_and_never_accumulates_pending_queries() {
        let context = egui::Context::default();
        let catalog: Catalog =
            serde_json::from_str(include_str!("../../catalog/strings.en.json")).unwrap();
        let mut screen = OperationsHealth::default();
        let mut publication = ContractPublication::default();
        let mut response = None;
        let _ = context.run(Default::default(), |ctx| {
            response = screen.draw(ctx, &publication, &catalog, false)
        });
        assert!(response.is_none());
        let _ = context.run(Default::default(), |ctx| {
            response = screen.draw(ctx, &publication, &catalog, true)
        });
        let first: serde_json::Value = serde_json::from_str(response.as_ref().unwrap()).unwrap();
        assert_eq!(first["view"], "operations-health");
        // Even when the refresh time has elapsed, the in-flight query is single-flight.
        screen.next_refresh = Some(std::time::Instant::now() - std::time::Duration::from_secs(10));
        let _ = context.run(Default::default(), |ctx| {
            response = screen.draw(ctx, &publication, &catalog, true)
        });
        assert!(response.is_none());
        publication
            .outcomes
            .push(crate::lookup_editor::EditorOutcome {
                request_id: first["requestId"].as_str().unwrap().into(),
                state: "Failed".into(),
                message: "Offline".into(),
            });
        let _ = context.run(Default::default(), |ctx| {
            response = screen.draw(ctx, &publication, &catalog, true)
        });
        let second: serde_json::Value = serde_json::from_str(response.as_ref().unwrap()).unwrap();
        assert_ne!(first["requestId"], second["requestId"]);
        assert_eq!(second["intent"], "load");
    }
}
