//! Actor hierarchy, resource health, and operator actions. A confirmation captures generation and target identity so a changed selection cannot silently redirect an operation.

use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog,
    contract_editor::{ContractPublication, ContractRow},
    lookup_editor::new_request_id,
    operations_health::status_color,
    strings,
};
use eframe::egui;
/// Actor diagnostics selection, operation parameters, and generation-bound confirmation state.
#[derive(Default)]
pub struct ActorHealth {
    /// Native-only monotonic schedule; hidden screens create no polling tasks.
    pub next_refresh: Option<std::time::Instant>,
    /// Background refresh identity is separate from an operator command.
    pub refresh_pending: Option<String>,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Operator-entered justification accompanying an administrative action.
    pub reason: String,
    /// Selected actor-administration operation key.
    pub operation: String,
    /// Actor query/filter origin text.
    pub from: String,
    /// Whether the actor operation confirmation is open.
    pub confirm: bool,
    /// Publication generation captured when confirmation was opened.
    pub confirm_generation: u64,
    /// Stable target identity captured for confirmation; later selection changes do not retarget it.
    pub confirm_target: Option<String>,
}
impl ActorHealth {
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns actor diagnostics selection, operation parameters, and generation-bound confirmation state.
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
            self.reason.clear();
        }
        if self.refresh_pending.as_ref().is_some_and(|id| {
            s.outcomes
                .iter()
                .any(|o| &o.request_id == id && o.state != "Pending")
        }) {
            self.refresh_pending = None;
        }
        let matching = s.result.view == "actor-health";
        if self.from.is_empty() && matching {
            self.from = s.result.context.get("fromUtc").cloned().unwrap_or_default();
        }
        if self.operation.is_empty() {
            self.operation = "Restart".into();
        }
        let mut action = None;
        let enabled = live && !s.busy && self.pending.is_none() && self.refresh_pending.is_none();
        super::health_surface(ctx,"Actor Health",open, |ui| {
            ui.horizontal_wrapped(|ui| {
                ui.trading_caption(c.text(strings::ACTOR_FROM));
                ui.add(egui::TextEdit::singleline(&mut self.from).desired_width(250.));
                if ui
                    .add_enabled(enabled, egui::Button::new(c.text(strings::ACTOR_REFRESH)))
                    .clicked()
                {
                    action = Some("load");
                }
            });
            ui.trading_colored_label(
                status_color(
                    s.result
                        .context
                        .get("status")
                        .map(String::as_str)
                        .unwrap_or("Unknown"),
                ),
                if matching {
                    s.result
                        .context
                        .get("summary")
                        .map(String::as_str)
                        .unwrap_or("")
                } else {
                    &s.status
                },
            );
            if matching {
                ui.trading_label(
                    s.result
                        .context
                        .get("observation")
                        .map(String::as_str)
                        .unwrap_or(""),
                );
                if let Some(outcome) = s.result.context.get("outcome") {
                    ui.trading_label(outcome);
                }
            }
            let authority = matching
                && s.result
                    .context
                    .get("controls")
                    .is_some_and(|x| x == "true");
            let selected = s
                .result
                .rows
                .iter()
                .find(|r| Some(&r.id) == self.selected.as_ref());
            ui.add_enabled_ui(enabled && authority, |ui| {
                ui.horizontal_wrapped(|ui| {
                    egui::ComboBox::from_id_salt("actor-operation")
                        .selected_text(&self.operation)
                        .show_ui(ui, |ui| {
                            for value in s.result.choices.get("operations").into_iter().flatten() {
                                ui.selectable_value(&mut self.operation, value.clone(), value);
                            }
                        });
                    ui.trading_caption(c.text(strings::ACTOR_REASON));
                    ui.add(
                        egui::TextEdit::singleline(&mut self.reason)
                            .char_limit(1024)
                            .desired_width(310.),
                    );
                    if ui
                        .add_enabled(
                            selected.is_some_and(|r| {
                                r.fields.get("canExecute").is_some_and(|v| v == "true")
                            }) && !self.reason.trim().is_empty(),
                            egui::Button::new(c.text(strings::ACTOR_EXECUTE)),
                        )
                        .clicked()
                    {
                        self.confirm = true;
                        self.confirm_generation = s.generation;
                        self.confirm_target = self.selected.clone();
                    }
                });
            });
            let rows = if matching {
                s.result.rows.as_slice()
            } else {
                &[]
            };
            let mut groups = rows
                .iter()
                .filter(|r| !rows.iter().any(|p| r.fields.get("parent") == Some(&p.id)))
                .filter_map(|r| r.fields.get("parent"))
                .collect::<Vec<_>>();
            groups.sort();
            groups.dedup();
            egui::ScrollArea::vertical()
                .id_salt("actor-tree")
                .max_height(270.)
                .show(ui, |ui| {
                    for group in groups {
                        egui::CollapsingHeader::new(group)
                            .id_salt(("actor-group", group))
                            .show(ui, |ui| {
                                for row in rows
                                    .iter()
                                    .filter(|r| r.fields.get("parent") == Some(group))
                                {
                                    draw_row(ui, row, rows, &mut self.selected);
                                }
                            });
                    }
                });
            if let Some(row) = rows.iter().find(|r| Some(&r.id) == self.selected.as_ref()) {
                ui.separator();
                ui.trading_label(row.fields.get("name").unwrap_or(&row.id));
                egui::ScrollArea::both()
                    .id_salt("actor-properties")
                    .show(ui, |ui| {
                        egui::Grid::new("actor-properties-grid")
                            .striped(true)
                            .show(ui, |ui| {
                                for (k, v) in &row.fields {
                                    if !["name", "kind", "parent", "status", "canExecute"]
                                        .contains(&k.as_str())
                                    {
                                        ui.trading_label(
                                            c.text(&format!("actor_field_{}", k.replace('.', "_"))),
                                        );
                                        ui.trading_label(v);
                                        ui.end_row();
                                    }
                                }
                            });
                    });
            }
        });
        if self.confirm {
            egui::Window::new(crate::presentation::dialog_title(
                c.text(strings::ACTOR_CONFIRM),
            ))
            .collapsible(false)
            .show(ctx, |ui| {
                ui.trading_label(format!(
                    "{}: {}",
                    self.operation,
                    self.selected.as_deref().unwrap_or("")
                ));
                ui.trading_label(&self.reason);
                if self.confirm_generation != s.generation || self.confirm_target != self.selected {
                    ui.trading_caption(c.text(strings::ACTOR_STALE));
                }
                ui.horizontal(|ui| {
                    if ui.button(c.text(strings::CANCEL)).clicked() {
                        self.confirm = false;
                    }
                    if ui
                        .add_enabled(
                            enabled
                                && matching
                                && self.confirm_generation == s.generation
                                && self.confirm_target == self.selected
                                && s.result
                                    .context
                                    .get("controls")
                                    .is_some_and(|x| x == "true"),
                            egui::Button::new(c.text(strings::ACTOR_EXECUTE)),
                        )
                        .clicked()
                    {
                        action = Some("supervise");
                        self.confirm = false;
                    }
                });
            });
        }
        let now = std::time::Instant::now();
        let first_open=self.next_refresh.is_none();
        if first_open {
            self.next_refresh = Some(now + std::time::Duration::from_secs(15));
        }
        if action.is_none()
            && live
            && enabled
            && !self.confirm
            && (first_open&&!matching || self.next_refresh.is_none_or(|next| now >= next))
        {
            let id = new_request_id();
            self.refresh_pending = Some(id.clone());
            self.next_refresh = Some(now + std::time::Duration::from_secs(15));
            ctx.request_repaint_after(std::time::Duration::from_secs(15));
            return Some(serde_json::json!({"schema":1,"requestId":id,"view":"actor-health","intent":"load","expectedGeneration":s.generation,"originalId":null,"fields":{"fromUtc":s.result.context.get("fromUtc").cloned().unwrap_or_default()}}).to_string());
        }
        if live {
            ctx.request_repaint_after(std::time::Duration::from_secs(1));
        }
        if action.is_some() {
            self.next_refresh = Some(now + std::time::Duration::from_secs(15));
        }
        action.map(|intent|{let id=new_request_id();self.pending=Some(id.clone());serde_json::json!({"schema":1,"requestId":id,"view":"actor-health","intent":intent,"expectedGeneration":s.generation,"originalId":self.selected,"fields":{"fromUtc":self.from,"operation":self.operation,"reason":self.reason}}).to_string()})
    }
}
/// Render an actor row and nested child rows, keeping selection tied to the published actor ID.
fn draw_row(
    ui: &mut egui::Ui,
    row: &ContractRow,
    rows: &[ContractRow],
    selected: &mut Option<String>,
) {
    let name = row.fields.get("name").unwrap_or(&row.id);
    let text = egui::RichText::new(name).color(status_color(
        row.fields
            .get("status")
            .map(String::as_str)
            .unwrap_or("Unknown"),
    ));
    let children = rows
        .iter()
        .filter(|r| r.fields.get("parent") == Some(&row.id))
        .collect::<Vec<_>>();
    if children.is_empty() {
        if ui
            .selectable_label(selected.as_ref() == Some(&row.id), text)
            .clicked()
        {
            *selected = Some(row.id.clone());
        }
    } else {
        let header = egui::CollapsingHeader::new(text)
            .id_salt(&row.id)
            .show(ui, |ui| {
                for child in children {
                    draw_row(ui, child, rows, selected);
                }
            });
        if header.header_response.clicked() {
            *selected = Some(row.id.clone());
        }
    }
}


#[cfg(test)]
mod initial_health_tests {
 use super::*;
 #[test]
 fn opening_actor_dialog_requests_health_immediately_without_market_activity(){
  let ctx=egui::Context::default();let catalog:Catalog=serde_json::from_str(include_str!("../../catalog/strings.en.json")).unwrap();
  let mut screen=ActorHealth::default();let publication=ContractPublication::default();let mut open=true;let mut action=None;
  let _=ctx.run(Default::default(),|ctx|{action=screen.draw_dialog(ctx,&publication,&catalog,true,&mut open);});
  let intent:serde_json::Value=serde_json::from_str(action.as_ref().expect("Opening must fetch health")).unwrap();assert_eq!(intent["intent"],"load");assert_eq!(intent["view"],"actor-health");
  let _=ctx.run(Default::default(),|ctx|{action=screen.draw_dialog(ctx,&publication,&catalog,true,&mut open);});assert!(action.is_none(),"Must not duplicate the pending read");
 }
}
