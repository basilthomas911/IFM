//! Economic-calendar CRUD view. Filter date/country, row selection, deletion confirmation, and local drafts survive frames; outcomes release the matching pending request.

use crate::control_parity::BorderedTable as _;
use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
use std::collections::BTreeMap;
/// Calendar field names paired with localized caption keys in editor order.
const FIELDS: [(&str, &str); 12] = [
    ("eventDate", "calendar_event_utc"),
    ("countryCode", "calendar_country"),
    ("eventName", "calendar_name"),
    ("actual", "calendar_actual"),
    ("forecast", "calendar_forecast"),
    ("prior", "calendar_prior"),
    ("impact", "calendar_impact"),
    ("unit", "calendar_unit"),
    ("change", "calendar_change"),
    ("changePercentage", "calendar_change_percentage"),
    ("createdOn", "calendar_created_on"),
    ("createdBy", "calendar_created_by"),
];
/// Persistent calendar editor selection, draft, filter, and pending-request state.
#[derive(Default)]
pub struct CalendarEditor {
    /// Unsaved local edit values retained across immediate-mode frames.
    pub draft: BTreeMap<String, String>,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Calendar date filter sent with the next query.
    pub filter_date: String,
    /// Selected or published calendar country code.
    pub country: String,
    /// Whether the local remove/delete confirmation is open.
    pub confirm_remove: bool,
    /// Count of rows actually drawn in the most recent frame, used by virtualization checks.
    pub drawn_rows: usize,
    /// Last submitted action key used to interpret completion and refresh behavior.
    last_action: String,
}
impl CalendarEditor {
    /// Build a correlated user intent against the current publication generation and retain its request ID as pending. The caller must enqueue the returned payload.
    pub fn request(&mut self, action: &str, s: &ContractPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.last_action = action.into();
        let mut fields = self.draft.clone();
        fields.insert("filterDate".into(), self.filter_date.clone());
        fields.insert("filterCountry".into(), self.country.clone());
        serde_json::json!({"schema":1,"requestId":id,"view":"economic-calendar","intent":action,"expectedGeneration":s.generation,"originalId":self.selected,"fields":fields}).to_string()
    }
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns persistent calendar editor selection, draft, filter, and pending-request state.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let matches = s.result.view == "economic-calendar";
        if let Some(id) = &self.pending {
            if let Some(outcome) = s
                .outcomes
                .iter()
                .find(|o| &o.request_id == id && o.state != "Pending")
            {
                if outcome.state == "Completed" {
                    if self.last_action == "remove" {
                        self.selected = None;
                        self.draft.clear();
                    } else if let Some(row) = s.result.rows.iter().find(|r| {
                        r.fields.get("eventDate") == self.draft.get("eventDate")
                            && r.fields.get("countryCode") == self.draft.get("countryCode")
                            && r.fields.get("eventName") == self.draft.get("eventName")
                    }) {
                        self.selected = Some(row.id.clone());
                        self.draft = row.fields.clone();
                    } else if self.selected.is_some() {
                        self.selected = None;
                        self.draft.clear();
                    }
                    self.filter_date = s
                        .result
                        .context
                        .get("filterDate")
                        .cloned()
                        .unwrap_or_default();
                    self.country = s
                        .result
                        .context
                        .get("filterCountry")
                        .cloned()
                        .unwrap_or_default();
                }
                self.pending = None;
            }
        }
        let enabled = live && !s.busy && self.pending.is_none();
        let mut action = None;
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading(c.text(strings::ECONOMIC_CALENDAR));
            ui.trading_label(if self.pending.is_some() {
                c.text(strings::AWAITING_COMPLETION)
            } else {
                &s.status
            });
            if !live {
                ui.trading_caption(c.text(strings::LIVE_REQUIRED));
            }
            if let Some(o) = s.outcomes.last() {
                ui.trading_label(format!("{}: {}", o.state, o.message));
            }
            ui.add_enabled_ui(enabled, |ui| {
                ui.horizontal_wrapped(|ui| {
                    if ui.button(c.text(strings::CALENDAR_LOAD)).clicked() {
                        action = Some("load");
                    }
                    let label = ui.trading_caption(c.text(strings::CALENDAR_FILTER_DATE));
                    ui.add(
                        egui::TextEdit::singleline(&mut self.filter_date)
                            .desired_width(140.)
                            .min_size(egui::vec2(140., 20.)),
                    )
                    .labelled_by(label.id);
                    egui::ComboBox::from_label(crate::presentation::label_text(c.text(strings::CALENDAR_COUNTRY)))
                        .selected_text(&self.country)
                        .show_ui(ui, |ui| {
                            if matches {
                                for code in
                                    s.result.choices.get("countryCode").into_iter().flatten()
                                {
                                    ui.selectable_value(&mut self.country, code.clone(), code);
                                }
                            }
                        });
                    let valid_filter =
                        chrono::NaiveDate::parse_from_str(&self.filter_date, "%Y-%m-%d").is_ok()
                            && !self.country.is_empty();
                    if ui
                        .add_enabled(
                            matches && valid_filter,
                            egui::Button::new(c.text(strings::CALENDAR_APPLY)),
                        )
                        .clicked()
                    {
                        action = Some("range");
                    }
                    if ui
                        .add_enabled(
                            matches && valid_filter,
                            egui::Button::new(c.text(strings::CALENDAR_IMPORT)),
                        )
                        .clicked()
                    {
                        action = Some("import");
                    }
                    if ui
                        .add_enabled(matches, egui::Button::new(c.text(strings::CALENDAR_NEW)))
                        .clicked()
                    {
                        self.selected = None;
                        self.confirm_remove = false;
                        self.draft = BTreeMap::from([
                            (
                                "eventDate".into(),
                                s.result
                                    .context
                                    .get("newEventUtc")
                                    .cloned()
                                    .unwrap_or_default(),
                            ),
                            ("countryCode".into(), self.country.clone()),
                        ]);
                    }
                });
                egui::Grid::new("calendar-fields")
                    .num_columns(2)
                    .show(ui, |ui| {
                        for (key, label) in FIELDS {
                            let response = ui.trading_caption(c.text(label));
                            let value = self.draft.entry(key.into()).or_default();
                            if key == "countryCode" {
                                egui::ComboBox::from_id_salt("calendar-event-country")
                                    .selected_text(&*value)
                                    .show_ui(ui, |ui| {
                                        if matches {
                                            for code in s
                                                .result
                                                .choices
                                                .get("countryCode")
                                                .into_iter()
                                                .flatten()
                                            {
                                                ui.selectable_value(value, code.clone(), code);
                                            }
                                        }
                                    })
                                    .response
                                    .labelled_by(response.id);
                            } else {
                                ui.add_enabled(
                                    key != "createdOn" && key != "createdBy",
                                    egui::TextEdit::singleline(value)
                                        .desired_width(340.)
                                        .min_size(egui::vec2(340., 20.)),
                                )
                                .labelled_by(response.id);
                            }
                            ui.end_row();
                        }
                    });
                let valid = self.draft.get("eventDate").is_some_and(|v| {
                    chrono::DateTime::parse_from_rfc3339(v)
                        .is_ok_and(|t| t.offset().local_minus_utc() == 0)
                }) && self.draft.get("countryCode").is_some_and(|v| !v.is_empty())
                    && self
                        .draft
                        .get("eventName")
                        .is_some_and(|v| !v.trim().is_empty());
                ui.horizontal(|ui| {
                    if ui
                        .add_enabled(
                            matches && valid,
                            egui::Button::new(c.text(if self.selected.is_some() {
                                strings::CALENDAR_SAVE
                            } else {
                                strings::CALENDAR_ADD
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
                            matches && self.selected.is_some(),
                            egui::Button::new(c.text(strings::CALENDAR_REMOVE)),
                        )
                        .clicked()
                    {
                        self.confirm_remove = true;
                    }
                    if ui.button(c.text(strings::CANCEL_EDITING)).clicked() {
                        self.confirm_remove = false;
                        self.draft = self
                            .selected
                            .as_ref()
                            .and_then(|id| s.result.rows.iter().find(|r| &r.id == id))
                            .map(|r| r.fields.clone())
                            .unwrap_or_default();
                    }
                });
                if self.confirm_remove {
                    ui.trading_caption(c.text(strings::CALENDAR_REMOVE_WARNING));
                    ui.horizontal(|ui| {
                        if ui.button(c.text(strings::CONFIRM_REMOVE)).clicked() {
                            self.confirm_remove = false;
                            action = Some("remove");
                        }
                        if ui.button(c.text(strings::CANCEL)).clicked() {
                            self.confirm_remove = false;
                        }
                    });
                }
            });
            self.drawn_rows = 0;
            if matches {
                if s.result.rows.is_empty() {
                    ui.trading_caption(c.text(strings::CALENDAR_EMPTY));
                }
                egui_extras::TableBuilder::new(ui)
                    .id_salt("calendar-table")
                    .striped(true)
                    .resizable(true)
                    .column(egui_extras::Column::initial(200.))
                    .column(egui_extras::Column::initial(80.))
                    .column(egui_extras::Column::remainder())
                    .header(24., |mut header| {
                        for label in [
                            strings::CALENDAR_EVENT_UTC,
                            strings::CALENDAR_COUNTRY,
                            strings::CALENDAR_NAME,
                        ] {
                            header.col(|ui| {
                                ui.colored_label(crate::presentation::CAPTION_FOREGROUND, egui::RichText::new(c.text(label)).strong());
                            });
                        }
                    })
                    .bordered_body(24., |body| {
                        body.rows(26., s.result.rows.len(), |mut row| {
                            self.drawn_rows += 1;
                            let item = &s.result.rows[row.index()];
                            row.col(|ui| {
                                if ui
                                    .add_enabled(
                                        enabled,
                                        egui::Button::new(
                                            item.fields
                                                .get("eventDate")
                                                .map(String::as_str)
                                                .unwrap_or(""),
                                        )
                                        .selected(self.selected.as_ref() == Some(&item.id)),
                                    )
                                    .clicked()
                                {
                                    self.selected = Some(item.id.clone());
                                    self.draft = item.fields.clone();
                                    self.confirm_remove = false;
                                }
                            });
                            row.col(|ui| {
                                ui.trading_label(
                                    item.fields
                                        .get("countryCode")
                                        .map(String::as_str)
                                        .unwrap_or(""),
                                );
                            });
                            row.col(|ui| {
                                ui.trading_label(
                                    item.fields
                                        .get("eventName")
                                        .map(String::as_str)
                                        .unwrap_or(""),
                                );
                            });
                        })
                    });
            }
        });
        action.map(|a| self.request(a, s))
    }
}
