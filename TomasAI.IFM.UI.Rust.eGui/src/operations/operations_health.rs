//! Operational health and dataset projections. Visible rows are rendered from the published query result; severity colors communicate backend state without recalculating it.

use crate::presentation::TradingLabelUi as _;
use crate::{Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings};
use eframe::egui;

/// Operational dataset/health view selection and pending-query state.
#[derive(Default)]
pub struct OperationsHealth {
    /// Most recent upper-view and lower-details bounds for layout verification.
    pub panes: Option<[egui::Rect; 2]>,

    /// Monotonic UI refresh schedule. No task survives a hidden view.
    next_refresh: Option<std::time::Instant>,
    /// Detects reopening the view so Dataset Workers gets one fresh snapshot.
    last_draw: Option<std::time::Instant>,
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
            egui::Color32::from_rgb(240, 230, 140)
        }
        "RED" | "DOWN" | "FAILED" => egui::Color32::from_rgb(250, 128, 114),
        _ => egui::Color32::GRAY,
    }
}


/// Severity ordering is stable across both the stage and dataset lists.
pub fn severity_rank(status: &str) -> u8 {
    match status.to_uppercase().as_str() {
        "RED" | "DOWN" | "FAILED" => 0,
        "YELLOW" | "ORANGE" | "RECOVERING" | "RESETTING" | "QUALIFYING" => 1,
        "GREEN" | "UP" | "HEALTHY" => 2,
        _ => 3,
    }
}


/// Returns the explanatory text for the current published status.
pub(super) fn status_description(status: &str) -> &str {
    match status.to_uppercase().as_str() {
        "RED" => "Down",
        "YELLOW" => "Awaiting verification",
        "ORANGE" => "Recovering",
        "GREEN" => "Verified",
        _ => status,
    }
}


/// Provides health surface for the operations health implementation.
fn health_surface(
    ctx: &egui::Context,
    open: Option<&mut bool>,
    contents: impl FnOnce(&mut egui::Ui),
) {
    if let Some(open) = open {
        egui::Window::new(crate::presentation::dialog_title("Operations Health"))
            .open(open)
            .default_size(egui::vec2(1000., 500.))
            .min_size(egui::vec2(720., 360.))
            .resizable(true)
            .show(ctx, contents);
    } else {
        egui::CentralPanel::default().show(ctx, contents);
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
        self.draw_surface(ctx, s, c, live, None, None)
    }


    /// Display the health explorer as an independent desktop dialog.
    pub fn draw_dialog(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
        open: &mut bool,
    ) -> Option<String> {
        self.draw_surface(ctx, s, c, live, Some(open), None)
    }


    /// Render the health view inside the active sidebar tab without opening another window.
    pub fn draw_inside(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let ctx = ui.ctx().clone();
        self.draw_surface(&ctx, s, c, live, None, Some(ui))
    }


    /// Renders the surface view and collects its UI interactions.
    fn draw_surface(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
        open: Option<&mut bool>,
        inside: Option<&mut egui::Ui>,
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
        if self.last_draw.is_some_and(|last| now.duration_since(last) > std::time::Duration::from_secs(1)) {
            self.next_refresh = None;
        }
        self.last_draw = Some(now);
        let mut intent = None;
        if live
            && !s.busy
            && self.pending.is_none()
            && (self.next_refresh.is_none() || !self.datasets && self.next_refresh.is_some_and(|next| now >= next))
        {
            intent = Some(self.request(s, now));
        }
        if live && !self.datasets {
            ctx.request_repaint_after(
                self.next_refresh
                    .map(|next| next.saturating_duration_since(now))
                    .unwrap_or(std::time::Duration::from_secs(5))
                    .max(std::time::Duration::from_millis(100)),
            );
        }
        let mut contents = |ui: &mut egui::Ui| {
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
                if let Some(status) = s.result.context.get("restartStatus") {
                    ui.trading_label(status);
                }
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
                    self.next_refresh = None;
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
                    self.next_refresh = None;
                }
                if ui.add_enabled(live && self.pending.is_none(), egui::Button::new("Refresh")).clicked() {
                    intent = Some(self.request(s, now));
                }
                if self.datasets {
                    let fields = self.restart_fields(s);
                    if ui.add_enabled(live && !s.busy && self.pending.is_none() && fields.is_some(),
                        egui::Button::new("Restart")).clicked()
                        && let Some(fields) = fields
                    {
                        let id = new_request_id();
                        self.pending = Some(id.clone());
                        intent = Some(serde_json::json!({"schema":1,"requestId":id,"view":"operations-health",
                            "intent":"restart-dataset","expectedGeneration":s.generation,"fields":fields}).to_string());
                    }
                }
            });
            let kind = if self.datasets { "dataset" } else { "stage" };
            let mut rows = s
                .result
                .rows
                .iter()
                .filter(|r| matching && r.fields.get("kind").is_some_and(|k| k == kind))
                .collect::<Vec<_>>();
            rows.sort_by(|a, b| {
                let rank = |r: &&crate::contract_editor::ContractRow| {
                    severity_rank(
                        r.fields
                            .get("Status")
                            .map(String::as_str)
                            .unwrap_or("Unknown"),
                    )
                };
                rank(a).cmp(&rank(b)).then_with(|| a.id.cmp(&b.id))
            });
            let [list_rect, details_rect] = super::split_health_body(ui);
            self.panes = Some([list_rect, details_rect]);
            super::health_scroll(ui, list_rect, ("health-rows", self.datasets), false, |ui| {
                self.drawn_rows = 0;
                for row in &rows {
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
                        .selectable_label(
                            self.selected.as_ref() == Some(&row.id),
                            egui::RichText::new(name).color(status_color(status)),
                        )
                        .clicked()
                    {
                        self.selected = Some(row.id.clone());
                        if self.datasets && live && !s.busy && self.pending.is_none() {
                            intent = Some(self.request(s, now));
                        }
                    }
                    self.drawn_rows += 1;
                }
            });
            super::health_scroll(
                ui,
                details_rect,
                ("health-properties", self.datasets),
                false,
                |ui| {
                    if let Some(row) = rows
                        .iter()
                        .find(|r| Some(&r.id) == self.selected.as_ref())
                        .or_else(|| rows.first())
                    {
                        super::detail_tree(ui, &row.id, "Health details", &row.fields, |key| {
                            if let Some(name) = key.strip_prefix("Counter/") { return name.into(); }
                            let label = c.text(&format!("health_{key}"));
                            if label == "[Missing string]" {
                                key.into()
                            } else {
                                label.into()
                            }
                        });
                    }
                },
            );
        };
        if let Some(ui) = inside {
            contents(ui);
        } else {
            health_surface(ctx, open, contents);
        }
        intent
    }


    /// Provides request for the operations health implementation.
    fn request(&mut self, publication: &ContractPublication, now: std::time::Instant) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.next_refresh = Some(now + std::time::Duration::from_secs(5));
        serde_json::json!({"schema":1,"requestId":id,"view":"operations-health","intent":"load","expectedGeneration":publication.generation,"originalId":null,"fields":{}}).to_string()
    }

    fn restart_fields(&self, publication: &ContractPublication) -> Option<serde_json::Value> {
        if publication.result.view != "operations-health" {
            return None;
        }
        let row = publication.result.rows.iter().find(|row| Some(&row.id) == self.selected.as_ref())?;
        let field = |key| row.fields.get(key).map(String::as_str);
        if field("kind") != Some("dataset") || field("Running") != Some("False")
            || !matches!(field("Status"), Some("Red" | "Down" | "Failed"))
        {
            return None;
        }
        Some(serde_json::json!({"dataset":field("Dataset")?,"valueDate":field("ValueDate")?,
            "worker":field("WorkerInstance")?,"generation":field("Generation")?}))
    }
}

#[cfg(test)]
mod native_refresh_tests {
    use super::*;

    #[test]
    fn restart_requires_the_explicit_selection_of_a_down_worker() {
        let mut publication = ContractPublication::default();
        publication.result.view = "operations-health".into();
        publication.result.rows.push(crate::contract_editor::ContractRow {
            id: "dataset:Options/GLBX.MDP3".into(),
            fields: std::collections::BTreeMap::from([
                ("kind".into(), "dataset".into()), ("Dataset".into(), "Options/GLBX.MDP3".into()),
                ("Running".into(), "False".into()), ("Status".into(), "Red".into()),
                ("ValueDate".into(), "2026-11-12".into()),
                ("WorkerInstance".into(), uuid::Uuid::from_u128(1).to_string()),
                ("Generation".into(), uuid::Uuid::from_u128(2).to_string()),
            ]),
            ..Default::default()
        });
        let mut view = OperationsHealth { datasets: true, ..Default::default() };
        assert!(view.restart_fields(&publication).is_none());
        view.selected = Some(publication.result.rows[0].id.clone());
        assert_eq!(view.restart_fields(&publication).unwrap()["dataset"], "Options/GLBX.MDP3");
        for status in ["Green", "Orange", "Inactive"] {
            publication.result.rows[0].fields.insert("Status".into(), status.into());
            assert!(view.restart_fields(&publication).is_none());
        }
        let fields = &mut publication.result.rows[0].fields;
        fields.insert("Status".into(), "Red".into());
        fields.insert("Running".into(), "True".into());
        assert!(view.restart_fields(&publication).is_none());
    }

    /// Dataset diagnostics remain frozen until the next view opening or Refresh.
    #[test]
    fn datasets_read_once_instead_of_polling() {
        let context = egui::Context::default();
        let catalog: Catalog = serde_json::from_str(include_str!("../../catalog/strings.en.json")).unwrap();
        let mut publication = ContractPublication::default();
        let mut screen = OperationsHealth { datasets: true, ..Default::default() };
        let mut response = None;
        let _ = context.run(Default::default(), |ctx| response = screen.draw(ctx, &publication, &catalog, true));
        let first: serde_json::Value = serde_json::from_str(response.as_ref().unwrap()).unwrap();
        publication.outcomes.push(crate::lookup_editor::EditorOutcome {
            request_id: first["requestId"].as_str().unwrap().into(), state: "Completed".into(), message: "Snapshot".into(),
        });
        screen.next_refresh = Some(std::time::Instant::now() - std::time::Duration::from_secs(10));
        let _ = context.run(Default::default(), |ctx| response = screen.draw(ctx, &publication, &catalog, true));
        assert!(response.is_none());
        screen.last_draw = Some(std::time::Instant::now() - std::time::Duration::from_secs(2));
        let _ = context.run(Default::default(), |ctx| response = screen.draw(ctx, &publication, &catalog, true));
        assert!(response.is_some());
    }


    /// Verifies that health refresh is visible only and never accumulates pending queries.
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
