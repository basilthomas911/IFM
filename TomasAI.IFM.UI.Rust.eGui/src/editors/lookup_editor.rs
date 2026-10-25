//! Lookup CRUD editing and shared request outcomes. A request ID identifies the local pending action; rendering never treats submission as proof of successful persistence.

use crate::presentation::TradingLabelUi as _;
use crate::{Catalog, strings};
use eframe::egui;
use serde::{Deserialize, Serialize};
/// Published lookup entry and its creation/audit display fields.
#[derive(Clone, Default, Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LookupRow {
    /// Published lookup category forming part of the entry identity.
    pub lookup_type_name: String,
    /// Published lookup short code.
    pub short_code: String,
    /// Published lookup ordering/business identifier.
    pub order_id: i32,
    /// Human-readable description supplied by the domain projection or local draft.
    pub description: String,
    /// Formatted creation timestamp from the managed record.
    pub created_on: String,
    /// Recorded creator identity from the managed record.
    pub created_by: String,
}
/// Managed result for one request ID. Submission and completion are separate states.
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EditorOutcome {
    /// Correlation identifier of the UI request whose outcome is reported.
    pub request_id: String,
    /// Backend lifecycle/execution state or correlated outcome state.
    pub state: String,
    /// Human-readable status or request-outcome explanation.
    pub message: String,
}
/// Lookup publication with generation, busy/status fields, rows, and correlated outcomes.
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct EditorPublication {
    /// Wire schema version used to interpret this publication.
    pub schema: u32,
    /// Publication generation used for change detection and request correlation.
    pub generation: u64,
    /// Display-ready status supplied by the managed projection or current view state.
    pub status: String,
    /// Whether the managed view currently reports work in progress.
    pub busy: bool,
    /// Rows supplied by the enclosing publication/model; they are not fetched during drawing.
    pub rows: Vec<LookupRow>,
    /// Correlated managed results for submitted request IDs.
    pub outcomes: Vec<EditorOutcome>,
}
/// Local lookup draft, stable selection, confirmation, and submission-error state.
#[derive(Default)]
pub struct LookupEditor {
    /// Unsaved local edit values retained across immediate-mode frames.
    pub draft: LookupRow,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<(String, i32)>,
    /// Whether the local remove/delete confirmation is open.
    pub confirm_remove: bool,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// User-visible validation, submission, or transport error text.
    pub error: String,
    /// Last submitted action key used to interpret completion and refresh behavior.
    pub last_action: String,
    /// Count of rows actually drawn in the most recent frame, used by virtualization checks.
    pub drawn_rows: usize,
    /// Original Reference editor mode: view, add, or change.
    pub edit_mode: u8,
    category: String,
    order_text: String,
    reference_loaded: bool,
}
impl LookupEditor {
    /// Serialize the lookup draft/action with a fresh request ID and retain it for outcome correlation.
    fn intent(&mut self, action: &str, snapshot: &EditorPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.last_action = action.into();
        self.error.clear();
        let mut row = self.draft.clone();
        if action == "add" {
            row.created_by = std::env::var("USERNAME").unwrap_or_default();
            // Creation time belongs to the API clock. The service resolves it
            // immediately before submission; a renderer never invents it.
            row.created_on.clear();
        }
        serde_json::json!({"schema":1,"requestId":id,"view":"lookup","intent":action,"expectedGeneration":snapshot.generation,"lookup":if action=="load" {None} else {Some(row)}}).to_string()
    }
    fn observe(&mut self, snapshot: &EditorPublication) {
        if let Some(id) = &self.pending {
            if let Some(outcome) = snapshot
                .outcomes
                .iter()
                .find(|x| &x.request_id == id && x.state != "Pending")
            {
                if outcome.state == "Completed" {
                    self.edit_mode = 0;
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
    }
    /// Original Reference master/detail lists and four-field editor with the shared command rail.
    pub fn draw_reference(
        &mut self,
        ctx: &egui::Context,
        snapshot: &EditorPublication,
        live: bool,
    ) -> Option<String> {
        self.observe(snapshot);
        if self.selected.is_none() && self.edit_mode == 0 {
            if let Some(row) = snapshot.rows.first() {
                self.select_reference(row);
            }
        }
        let valid = !self.draft.lookup_type_name.trim().is_empty()
            && !self.draft.short_code.trim().is_empty();
        let commands = crate::reference::Commands {
            editing: self.edit_mode != 0,
            changing: self.edit_mode == 2,
            ready: live
                && !snapshot.busy
                && self.pending.is_none()
                && !self.confirm_remove
                && self.error.is_empty(),
            add: true,
            change: self.selected.is_some(),
            remove: self.selected.is_some(),
            save: valid,
        };
        crate::reference::show(ctx, 0, commands, |ui, command| {
            let mut action = None;
            if live && !self.reference_loaded && self.pending.is_none() {
                self.reference_loaded = true;
                action = Some("load");
            }
            match command {
                Some("add") if self.edit_mode == 0 => {
                    self.draft = Default::default();
                    self.order_text.clear();
                    self.edit_mode = 1;
                }
                Some("change") if self.edit_mode == 0 => self.edit_mode = 2,
                Some("add" | "change") => {
                    if self.edit_mode == 1 {
                        self.draft.order_id = snapshot
                            .rows
                            .iter()
                            .filter(|r| r.lookup_type_name == self.draft.lookup_type_name)
                            .map(|r| r.order_id)
                            .max()
                            .and_then(|id| id.checked_add(1))
                            .unwrap_or(0);
                    } else if let Ok(order) = self.order_text.parse::<i32>() {
                        if order < 0 {
                            self.error = "Order ID must be a non-negative integer.".into();
                            return None;
                        }
                        self.draft.order_id = order;
                    } else {
                        self.error = "Order ID must be a non-negative integer.".into();
                        return None;
                    }
                    action = Some(if self.edit_mode == 1 { "add" } else { "change" });
                }
                Some("remove") => self.confirm_remove = true,
                Some("close") => {
                    self.edit_mode = 0;
                    if let Some(row) = snapshot.rows.iter().find(|r| {
                        self.selected.as_ref() == Some(&(r.lookup_type_name.clone(), r.order_id))
                    }) {
                        self.select_reference(row);
                    }
                }
                _ => {}
            }
            let ready = live && !snapshot.busy && self.pending.is_none();
            if self.confirm_remove || !self.error.is_empty() {
                ui.disable();
            }
            egui::SidePanel::left("reference-lookup-navigation")
                .default_width(454.)
                .min_width(280.)
                .resizable(true)
                .frame(egui::Frame::NONE.fill(crate::presentation::BACKGROUND))
                .show_inside(ui, |ui| {
                    let width = ui.available_width();
                    let header = |ui: &mut egui::Ui, title: &str| {
                        let (rect, _) =
                            ui.allocate_exact_size(egui::vec2(width, 30.), egui::Sense::hover());
                        ui.painter()
                            .rect_filled(rect, 0., crate::presentation::BACKGROUND);
                        ui.put(rect, crate::presentation::caption_widget(title));
                    };
                    header(ui, "Lookup Type Names");
                    let mut names: Vec<_> = snapshot
                        .rows
                        .iter()
                        .map(|row| row.lookup_type_name.as_str())
                        .collect();
                    names.sort();
                    names.dedup();
                    egui::Frame::NONE.fill(egui::Color32::BLACK).show(ui, |ui| {
                        ui.set_min_width(width);
                        egui::ScrollArea::vertical()
                            .id_salt("reference-lookup-names")
                            .min_scrolled_height(210.)
                            .max_height(210.)
                            .auto_shrink([false, false])
                            .show_rows(ui, 23., names.len(), |ui, range| {
                                for index in range {
                                    let name = names[index];
                                    if ui
                                        .add_enabled(
                                            ready && self.edit_mode == 0,
                                            egui::Button::new(
                                                egui::RichText::new(name)
                                                    .color(egui::Color32::WHITE),
                                            )
                                            .frame(true)
                                            .fill(if self.category == name {
                                                egui::Color32::from_rgb(0, 120, 215)
                                            } else {
                                                egui::Color32::BLACK
                                            })
                                            .stroke(egui::Stroke::NONE)
                                            .min_size(egui::vec2(ui.available_width(), 23.))
                                            .selected(self.category == name),
                                        )
                                        .clicked()
                                    {
                                        if let Some(row) = snapshot
                                            .rows
                                            .iter()
                                            .find(|row| row.lookup_type_name == name)
                                        {
                                            self.select_reference(row);
                                        }
                                    }
                                }
                            });
                    });
                    header(ui, "Lookup Type Short Codes");
                    let rows: Vec<_> = snapshot
                        .rows
                        .iter()
                        .filter(|row| row.lookup_type_name == self.category)
                        .collect();
                    egui::Frame::NONE.fill(egui::Color32::BLACK).show(ui, |ui| {
                        ui.set_min_width(width);
                        let height = ui.available_height();
                        egui::ScrollArea::vertical()
                            .id_salt("reference-lookup-codes")
                            .min_scrolled_height(height)
                            .max_height(height)
                            .auto_shrink([false, false])
                            .show_rows(ui, 23., rows.len(), |ui, range| {
                                self.drawn_rows = range.len();
                                for index in range {
                                    let row = rows[index];
                                    if ui
                                        .add_enabled(
                                            ready && self.edit_mode == 0,
                                            egui::Button::new(
                                                egui::RichText::new(&row.short_code)
                                                    .color(egui::Color32::WHITE),
                                            )
                                            .frame(true)
                                            .fill(
                                                if self.selected.as_ref()
                                                    == Some(&(
                                                        row.lookup_type_name.clone(),
                                                        row.order_id,
                                                    ))
                                                {
                                                    egui::Color32::from_rgb(0, 120, 215)
                                                } else {
                                                    egui::Color32::BLACK
                                                },
                                            )
                                            .stroke(egui::Stroke::NONE)
                                            .min_size(egui::vec2(ui.available_width(), 23.))
                                            .selected(
                                                self.selected.as_ref()
                                                    == Some(&(
                                                        row.lookup_type_name.clone(),
                                                        row.order_id,
                                                    )),
                                            ),
                                        )
                                        .clicked()
                                    {
                                        self.select_reference(row);
                                    }
                                }
                            });
                    });
                });
            egui::CentralPanel::default()
                .frame(
                    egui::Frame::NONE
                        .fill(crate::presentation::BACKGROUND)
                        .inner_margin(4),
                )
                .show_inside(ui, |ui| {
                    egui::Grid::new("reference-lookup-fields")
                        .num_columns(2)
                        .min_row_height(35.)
                        .show(ui, |ui| {
                            for index in 0..4 {
                                let caption = [
                                    "Lookup Type Name:",
                                    "Short Code:",
                                    "Order Id:",
                                    "Description:",
                                ][index];
                                let label = ui.add_sized(
                                    [170., 23.],
                                    crate::presentation::caption_widget(caption)
                                        .halign(egui::Align::Max),
                                );
                                let value = match index {
                                    0 => &mut self.draft.lookup_type_name,
                                    1 => &mut self.draft.short_code,
                                    2 => &mut self.order_text,
                                    _ => &mut self.draft.description,
                                };
                                let width = if index == 2 {
                                    143.
                                } else {
                                    ui.available_width().max(80.)
                                };
                                let edit = if index == 3 {
                                    egui::TextEdit::multiline(value).desired_rows(4)
                                } else {
                                    egui::TextEdit::singleline(value)
                                };
                                ui.add_sized(
                                    [width, if index == 3 { 72. } else { 23. }],
                                    edit.interactive(ready && self.edit_mode != 0),
                                )
                                .labelled_by(label.id);
                                ui.end_row();
                            }
                        });
                });
            if self.confirm_remove {
                egui::Window::new("Remove Lookup Type")
                    .collapsible(false)
                    .resizable(false)
                    .show(ctx, |ui| {
                        ui.label(format!(
                            "Are you sure you want to remove Lookup Type {}:{} ?",
                            self.draft.lookup_type_name, self.draft.order_id
                        ));
                        ui.horizontal(|ui| {
                            if ui.button("Yes").clicked() {
                                action = Some("remove");
                                self.confirm_remove = false;
                            }
                            if ui.button("No").clicked() {
                                self.confirm_remove = false;
                            }
                        });
                    });
            }
            if !self.error.is_empty() {
                egui::Window::new("Lookup Type Editor Error")
                    .collapsible(false)
                    .show(ctx, |ui| {
                        ui.label(&self.error);
                        if ui.button("OK").clicked() {
                            self.error.clear();
                        }
                    });
            }
            action.map(|action| self.intent(action, snapshot))
        })
    }
    fn select_reference(&mut self, row: &LookupRow) {
        self.selected = Some((row.lookup_type_name.clone(), row.order_id));
        self.category = row.lookup_type_name.clone();
        self.draft = row.clone();
        self.order_text = row.order_id.to_string();
    }
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns local lookup draft, stable selection, confirmation, and submission-error state.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        snapshot: &EditorPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        self.observe(snapshot);
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

/// Create a process-local request identifier using time and an atomic counter. Used for UI correlation, not cryptographic identity.
pub fn new_request_id() -> String {
    uuid::Uuid::new_v4().to_string()
}
