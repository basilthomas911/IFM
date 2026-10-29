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
            egui::Color32::from_rgb(240, 230, 140)
        }
        "RED" | "DOWN" | "FAILED" => egui::Color32::from_rgb(250, 128, 114),
        _ => egui::Color32::GRAY,
    }
}
/// Severity ordering is stable across both the stage and dataset lists.
pub fn severity_rank(status:&str)->u8 {match status.to_uppercase().as_str(){"RED"|"DOWN"|"FAILED"=>0,"YELLOW"|"ORANGE"|"RECOVERING"|"RESETTING"|"QUALIFYING"=>1,"GREEN"|"UP"|"HEALTHY"=>2,_=>3}}
fn status_description(status:&str)->&str {match status.to_uppercase().as_str(){"RED"=>"Down","YELLOW"=>"Awaiting verification","ORANGE"=>"Recovering","GREEN"=>"Verified",_=>status}}
fn health_surface(ctx:&egui::Context,open:Option<&mut bool>,contents:impl FnOnce(&mut egui::Ui)) {
    if let Some(open)=open {
        egui::Window::new(crate::presentation::dialog_title("Operations Health"))
            .open(open).default_size(egui::vec2(1000.,500.)).min_size(egui::vec2(720.,360.))
            .resizable(true).show(ctx,contents);
    }else{egui::CentralPanel::default().show(ctx,contents);}
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
        self.draw_surface(ctx,s,c,live,None)
    }
    /// Display the health explorer as an independent desktop dialog.
    pub fn draw_dialog(&mut self,ctx:&egui::Context,s:&ContractPublication,c:&Catalog,live:bool,open:&mut bool)->Option<String>{
        self.draw_surface(ctx,s,c,live,Some(open))
    }
    fn draw_surface(&mut self,ctx:&egui::Context,s:&ContractPublication,c:&Catalog,live:bool,open:Option<&mut bool>)->Option<String>{
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
        health_surface(ctx,open, |ui| {
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
            let mut rows = s
                .result
                .rows
                .iter()
                .filter(|r| matching && r.fields.get("kind").is_some_and(|k| k == kind))
                .collect::<Vec<_>>();
            rows.sort_by(|a,b| {
                let rank=|r: &&crate::contract_editor::ContractRow| severity_rank(r.fields.get("Status").map(String::as_str).unwrap_or("Unknown"));
                rank(a).cmp(&rank(b)).then_with(|| a.id.cmp(&b.id))
            });
            egui::Frame::NONE
                .stroke(egui::Stroke::new(1.0_f32, ui.visuals().widgets.noninteractive.fg_stroke.color))
                .inner_margin(6.)
                .show(ui, |ui| {
                // Allocate the entire tab body first, so neither pane shrinks to its text.
                let height=360.;
                let (body, _)=ui.allocate_exact_size(egui::vec2(ui.available_width(),height),egui::Sense::hover());
                let gap=ui.spacing().item_spacing.x;
                let list_width=(body.width()-gap)/3.;
                let list_rect=egui::Rect::from_min_size(body.min,egui::vec2(list_width,height));
                let details_rect=egui::Rect::from_min_max(egui::pos2(list_rect.right()+gap,body.top()),body.max);
                let mut list=ui.new_child(egui::UiBuilder::new().max_rect(list_rect));
                list.set_width(list_rect.width());
                egui::ScrollArea::vertical().auto_shrink([false,false]).max_height(height).id_salt("health-rows").show(&mut list, |ui| {
                    self.drawn_rows=0;
                    for row in &rows {
                        let name=row.fields.get(if self.datasets {"Dataset"} else {"Stage"}).unwrap_or(&row.id);
                        let status=row.fields.get("Status").map(String::as_str).unwrap_or("Unknown");
                        if ui.selectable_label(self.selected.as_ref()==Some(&row.id), egui::RichText::new(name).color(status_color(status))).clicked() {self.selected=Some(row.id.clone());}
                        self.drawn_rows+=1;
                    }
                });
                let mut details=ui.new_child(egui::UiBuilder::new().max_rect(details_rect));
                details.set_width(details_rect.width());
                egui::ScrollArea::both().auto_shrink([false,false]).max_height(height).id_salt("health-properties").show(&mut details,|ui| {
                    ui.set_min_width(details_rect.width()-ui.spacing().scroll.allocated_width());
                    if let Some(row)=rows.iter().find(|r|Some(&r.id)==self.selected.as_ref()).or_else(||rows.first()) {
                        egui::CollapsingHeader::new("Health details").default_open(true).show(ui,|ui| {
                            egui::Grid::new("health-grid").num_columns(2).min_col_width((ui.available_width()-ui.spacing().item_spacing.x)/2.).striped(true).show(ui,|ui| {
                                for (key,value) in &row.fields {if key!="kind" {
                                    ui.trading_caption(c.text(&format!("health_{key}")));
                                    if key=="Status" {ui.trading_colored_label(status_color(value),status_description(value));}
                                    else {ui.trading_label(value);}
                                    ui.end_row();
                                }}
                            });
                        });
                    }
                });
            });
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
