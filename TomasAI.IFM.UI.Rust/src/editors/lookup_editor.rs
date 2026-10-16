use crate::presentation::TradingLabelUi as _;
use crate::{Catalog, strings};
use eframe::egui;
use serde::{Deserialize, Serialize};
#[derive(Clone, Default, Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LookupRow {
    pub lookup_type_name: String,
    pub short_code: String,
    pub order_id: i32,
    pub description: String,
    pub created_on: String,
    pub created_by: String,
}
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EditorOutcome {
    pub request_id: String,
    pub state: String,
    pub message: String,
}
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EditorPublication {
    pub schema: u32,
    pub generation: u64,
    pub status: String,
    pub busy: bool,
    pub rows: Vec<LookupRow>,
    pub outcomes: Vec<EditorOutcome>,
}
#[derive(Default)]
pub struct LookupEditor {
    pub draft: LookupRow,
    pub selected: Option<(String, i32)>,
    pub confirm_remove: bool,
    pub pending: Option<String>,
    pub error: String,
    pub last_action: String,
    pub drawn_rows: usize,
}
impl LookupEditor {
    fn intent(&mut self, action: &str, snapshot: &EditorPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.last_action = action.into();
        self.error.clear();
        let mut row = self.draft.clone();
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap_or_default();
        if action == "add" {
            row.created_by = std::env::var("USERNAME").unwrap_or_default();
            row.created_on = chrono::DateTime::from_timestamp(now.as_secs() as i64, 0)
                .unwrap()
                .to_rfc3339();
        }
        serde_json::json!({"schema":1,"requestId":id,"view":"lookup","intent":action,"expectedGeneration":snapshot.generation,"lookup":if action=="load" {None} else {Some(row)}}).to_string()
    }
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        snapshot: &EditorPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        if let Some(id) = &self.pending {
            if let Some(outcome) = snapshot
                .outcomes
                .iter()
                .find(|x| &x.request_id == id && x.state != "Pending")
            {
                if outcome.state == "Completed" {
                    if self.last_action == "remove" {
                        self.selected = None;
                        self.draft = Default::default();
                    } else if let Some(row) = snapshot.rows.iter().find(|r| {
                        r.lookup_type_name == self.draft.lookup_type_name
                            && r.order_id == self.draft.order_id
                    }) {
                        self.selected = Some((row.lookup_type_name.clone(), row.order_id));
                        self.draft = row.clone();
                    } else if self.selected.is_some() {
                        self.selected = None;
                        self.draft = Default::default();
                    }
                }
                self.pending = None;
            }
        }
        let mut action = None;
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading(c.text(strings::LOOKUP_DEFINITIONS));
            if !live {
                ui.trading_caption(c.text(strings::LIVE_REQUIRED));
            }
            ui.trading_label(&snapshot.status);
            if !self.error.is_empty() {
                ui.trading_colored_label(egui::Color32::YELLOW, &self.error);
            }
            for outcome in snapshot.outcomes.iter().rev().take(1) {
                ui.trading_label(format!("{}: {}", outcome.state, outcome.message));
            }
            let enabled = live && !snapshot.busy && self.pending.is_none();
            ui.horizontal(|ui| {
                if ui
                    .add_enabled(enabled, egui::Button::new(c.text(strings::LOAD_LOOKUPS)))
                    .clicked()
                {
                    action = Some("load");
                }
                if ui
                    .add_enabled(enabled, egui::Button::new(c.text(strings::NEW_LOOKUP)))
                    .clicked()
                {
                    self.selected = None;
                    self.draft = Default::default();
                    self.confirm_remove = false;
                }
            });
            ui.add_enabled_ui(enabled, |ui| {
                egui::Grid::new("lookup-fields").show(ui, |ui| {
                    let category_label = ui.trading_caption(c.text(strings::CATEGORY));
                    ui.add_enabled(
                        self.selected.is_none(),
                        egui::TextEdit::singleline(&mut self.draft.lookup_type_name),
                    )
                    .labelled_by(category_label.id);
                    ui.end_row();
                    let short_code_label = ui.trading_caption(c.text(strings::SHORT_CODE));
                    ui.text_edit_singleline(&mut self.draft.short_code)
                        .labelled_by(short_code_label.id);
                    ui.end_row();
                    ui.trading_caption(c.text(strings::DISPLAY_ORDER));
                    ui.add_enabled(
                        self.selected.is_none(),
                        egui::DragValue::new(&mut self.draft.order_id).range(0..=i32::MAX),
                    );
                    ui.end_row();
                    let description_label = ui.trading_caption(c.text(strings::DESCRIPTION));
                    ui.text_edit_singleline(&mut self.draft.description)
                        .labelled_by(description_label.id);
                    ui.end_row();
                });
                let valid = !self.draft.lookup_type_name.trim().is_empty()
                    && !self.draft.short_code.trim().is_empty();
                ui.horizontal(|ui| {
                    if ui
                        .add_enabled(
                            valid,
                            egui::Button::new(c.text(if self.selected.is_some() {
                                strings::SAVE_LOOKUP
                            } else {
                                strings::ADD_LOOKUP
                            })),
                        )
                        .clicked()
                    {
                        action = Some(if self.selected.is_some() {
                            "change"
                        } else {
                            "add"
                        });
                    }
                    if ui
                        .add_enabled(
                            self.selected.is_some(),
                            egui::Button::new(c.text(strings::REMOVE_LOOKUP)),
                        )
                        .clicked()
                    {
                        self.confirm_remove = true;
                    }
                });
                if ui.button(c.text(strings::CANCEL_EDITING)).clicked() {
                    self.confirm_remove = false;
                    self.draft = self
                        .selected
                        .as_ref()
                        .and_then(|(name, order)| {
                            snapshot
                                .rows
                                .iter()
                                .find(|r| &r.lookup_type_name == name && &r.order_id == order)
                        })
                        .cloned()
                        .unwrap_or_default();
                }
                if self.confirm_remove {
                    ui.trading_caption(c.text(strings::REMOVE_LOOKUP_WARNING));
                    ui.horizontal(|ui| {
                        if ui.button(c.text(strings::CONFIRM_REMOVE)).clicked() {
                            action = Some("remove");
                            self.confirm_remove = false;
                        }
                        if ui.button(c.text(strings::CANCEL)).clicked() {
                            self.confirm_remove = false;
                        }
                    });
                }
            });
            ui.separator();
            self.drawn_rows = 0;
            egui::ScrollArea::vertical()
                .id_salt("lookup-rows")
                .show_rows(ui, 24.0, snapshot.rows.len(), |ui, range| {
                    self.drawn_rows = range.len();
                    for i in range {
                        let row = &snapshot.rows[i];
                        let key = (row.lookup_type_name.clone(), row.order_id);
                        if ui
                            .selectable_label(
                                self.selected.as_ref() == Some(&key),
                                format!(
                                    "{} | {} | {} | {}",
                                    row.lookup_type_name,
                                    row.order_id,
                                    row.short_code,
                                    row.description
                                ),
                            )
                            .clicked()
                            && enabled
                        {
                            self.selected = Some(key);
                            self.draft = row.clone();
                            self.confirm_remove = false;
                        }
                    }
                });
        });
        action.map(|a| self.intent(a, snapshot))
    }
}

pub fn new_request_id() -> String {
    use std::sync::atomic::{AtomicU64, Ordering};
    static NEXT: AtomicU64 = AtomicU64::new(1);
    let now = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap_or_default();
    format!(
        "{:08x}-{:04x}-4000-8000-{:012x}",
        now.as_secs() as u32,
        now.subsec_micros() & 0xffff,
        NEXT.fetch_add(1, Ordering::Relaxed)
    )
}
