use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog,
    contract_editor::{ContractPublication, ContractRow},
    lookup_editor::new_request_id,
    operations_health::status_color,
    strings,
};
use eframe::egui;
#[derive(Default)]
pub struct ActorHealth {
    pub selected: Option<String>,
    pub pending: Option<String>,
    pub reason: String,
    pub operation: String,
    pub from: String,
    pub confirm: bool,
    pub confirm_generation: u64,
    pub confirm_target: Option<String>,
}
impl ActorHealth {
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
            self.reason.clear();
        }
        let matching = s.result.view == "actor-health";
        if self.from.is_empty() && matching {
            self.from = s.result.context.get("fromUtc").cloned().unwrap_or_default();
        }
        if self.operation.is_empty() {
            self.operation = "Restart".into();
        }
        let mut action = None;
        let enabled = live && !s.busy && self.pending.is_none();
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.horizontal_wrapped(|ui| {
                ui.trading_label(c.text(strings::ACTOR_FROM));
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
                    ui.trading_label(c.text(strings::ACTOR_REASON));
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
            egui::Window::new(c.text(strings::ACTOR_CONFIRM))
                .collapsible(false)
                .show(ctx, |ui| {
                    ui.trading_label(format!(
                        "{}: {}",
                        self.operation,
                        self.selected.as_deref().unwrap_or("")
                    ));
                    ui.trading_label(&self.reason);
                    if self.confirm_generation != s.generation
                        || self.confirm_target != self.selected
                    {
                        ui.trading_label(c.text(strings::ACTOR_STALE));
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
        action.map(|intent|{let id=new_request_id();self.pending=Some(id.clone());serde_json::json!({"schema":1,"requestId":id,"view":"actor-health","intent":intent,"expectedGeneration":s.generation,"originalId":self.selected,"fields":{"fromUtc":self.from,"operation":self.operation,"reason":self.reason}}).to_string()})
    }
}
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
