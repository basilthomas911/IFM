//! Provider instrument browser and draft mapping workflow. Filters, selected instruments, mapping edits, preview evidence, and confirmation are local state; the managed bridge owns discovery and mapping persistence.

use crate::control_parity::BorderedTable as _;
use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
use std::collections::BTreeMap;

/// Provider filters, stable selection, mapping draft, preview binding, and pending-request state.
pub struct ProviderBrowser {
    /// Editable query filters retained between frames.
    pub filters: BTreeMap<String, String>,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Count of rows actually drawn in the most recent frame, used by virtualization checks.
    pub drawn_rows: usize,
    /// Whether the provider mapping editor is visible.
    pub mapping_open: bool,
    /// Unsaved provider instrument mapping fields.
    pub mapping: BTreeMap<String, String>,
    /// Last submitted action key used to interpret completion and refresh behavior.
    last_action: String,
    /// Field values captured for the current preview/confirmation.
    preview_fields: BTreeMap<String, String>,
    /// Filters corresponding to the displayed result, used to distinguish unsent edits.
    published_filters: Option<BTreeMap<String, String>>,
}
impl Default for ProviderBrowser {
    /// Construct the view's initial filters, selection, and draft settings without issuing backend commands.
    fn default() -> Self {
        Self {
            filters: BTreeMap::from([
                ("dataset".into(), "GLBX.MDP3".into()),
                ("root".into(), "ES".into()),
                ("options".into(), "false".into()),
                ("includeHistory".into(), "false".into()),
                ("right".into(), "Unknown".into()),
            ]),
            selected: None,
            pending: None,
            drawn_rows: 0,
            mapping_open: false,
            mapping: BTreeMap::from([("exchangeTimeZoneId".into(), "America/Chicago".into())]),
            last_action: String::new(),
            preview_fields: BTreeMap::new(),
            published_filters: None,
        }
    }
}
impl ProviderBrowser {
    /// Build a correlated user intent against the current publication generation and retain its request ID as pending. The caller must enqueue the returned payload.
    pub fn request(&mut self, action: &str, s: &ContractPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.last_action = action.into();
        if action == "preview" {
            self.preview_fields = self.mapping.clone();
        }
        let mut fields = self.filters.clone();
        let original = if action == "preview" || action == "publish" {
            fields = self.mapping.clone();
            if action == "publish" {
                fields.insert(
                    "previewToken".into(),
                    s.result
                        .context
                        .get("previewToken")
                        .cloned()
                        .unwrap_or_default(),
                );
            }
            self.selected.clone()
        } else {
            self.selected = None;
            self.mapping_open = false;
            None
        };
        serde_json::json!({"schema":1,"requestId":id,"view":"provider-definitions","intent":action,"expectedGeneration":s.generation,"originalId":original,"fields":fields}).to_string()
    }
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns provider filters, stable selection, mapping draft, preview binding, and pending-request state.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let matches = s.result.view == "provider-definitions";
        if let Some(id) = &self.pending {
            if let Some(outcome) = s
                .outcomes
                .iter()
                .find(|o| &o.request_id == id && o.state != "Pending")
            {
                if !s.busy {
                    if outcome.state == "Completed"
                        && (self.last_action == "load" || self.last_action == "next")
                    {
                        self.published_filters = Some(self.filters.clone());
                    }
                    if outcome.state != "Completed" && self.last_action == "preview" {
                        self.last_action.clear();
                    }
                    if outcome.state == "Completed" && self.last_action == "publish" {
                        self.mapping_open = false;
                    }
                    self.pending = None;
                }
            }
        }
        let enabled = live && !s.busy && self.pending.is_none();
        let mut action = None;
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading(c.text(strings::PROVIDER_DEFINITIONS));
            ui.trading_caption(c.text(strings::PROVIDER_READONLY));
            ui.trading_label(&s.status);
            if let Some(outcome) = s.outcomes.last() {
                ui.trading_label(format!("{}: {}", outcome.state, outcome.message));
            }
            ui.add_enabled_ui(enabled && !self.mapping_open, |ui| {
                egui::Grid::new("provider-filters")
                    .num_columns(4)
                    .show(ui, |ui| {
                        for (index, (field, label)) in [
                            ("dataset", strings::PROVIDER_DATASET),
                            ("root", strings::PROVIDER_ROOT),
                            ("exchange", strings::PROVIDER_EXCHANGE),
                            ("expiry", strings::PROVIDER_EXPIRY),
                            ("underlyingInstrumentId", strings::PROVIDER_UNDERLYING),
                            ("minimumStrike", strings::PROVIDER_MINIMUM),
                            ("maximumStrike", strings::PROVIDER_MAXIMUM),
                        ]
                        .iter()
                        .enumerate()
                        {
                            let r = ui.trading_caption(c.text(label));
                            ui.add(
                                egui::TextEdit::singleline(
                                    self.filters.entry((*field).into()).or_default(),
                                )
                                .desired_width(160.)
                                .min_size(egui::vec2(160., 20.)),
                            )
                            .labelled_by(r.id);
                            if index % 2 == 1 {
                                ui.end_row();
                            }
                        }
                        ui.end_row();
                    });
                ui.horizontal_wrapped(|ui| {
                    for (field, label) in [
                        ("options", strings::PROVIDER_OPTIONS),
                        ("includeHistory", strings::PROVIDER_HISTORY),
                    ] {
                        let mut checked = self.filters.get(field).is_some_and(|x| x == "true");
                        if ui.add(crate::presentation::checkbox(&mut checked, c.text(label))).changed() {
                            self.filters.insert(field.into(), checked.to_string());
                        }
                    }
                    egui::ComboBox::from_label(crate::presentation::label_text(c.text(strings::PROVIDER_RIGHT)))
                        .selected_text(
                            self.filters
                                .get("right")
                                .map(String::as_str)
                                .unwrap_or("Unknown"),
                        )
                        .show_ui(ui, |ui| {
                            for right in ["Unknown", "Call", "Put"] {
                                ui.selectable_value(
                                    self.filters.entry("right".into()).or_default(),
                                    right.into(),
                                    right,
                                );
                            }
                        });
                    if ui.button(c.text(strings::PROVIDER_SEARCH)).clicked() {
                        action = Some("load");
                    }
                    if ui
                        .add_enabled(
                            matches
                                && s.result.has_more
                                && self.published_filters.as_ref() == Some(&self.filters),
                            egui::Button::new(c.text(strings::PROVIDER_NEXT)),
                        )
                        .clicked()
                    {
                        action = Some("next");
                    }
                });
            });
            if matches {
                ui.trading_label(format!(
                    "{}: {} / {}",
                    c.text(strings::PROVIDER_SNAPSHOT),
                    s.result
                        .context
                        .get("snapshotId")
                        .map(String::as_str)
                        .unwrap_or(""),
                    s.result
                        .context
                        .get("snapshotCompletedUtc")
                        .map(String::as_str)
                        .unwrap_or("")
                ));
            }
            if ui
                .add_enabled(
                    enabled
                        && matches
                        && self.selected.is_some()
                        && self.published_filters.as_ref() == Some(&self.filters)
                        && !self.mapping_open,
                    egui::Button::new(c.text(strings::PROVIDER_MAP)),
                )
                .clicked()
            {
                self.mapping_open = true;
                self.last_action.clear();
            }
            self.drawn_rows = 0;
            egui::TopBottomPanel::bottom("provider-facts")
                .default_height(260.)
                .min_height(80.)
                .resizable(true)
                .show_inside(ui, |ui| {
                    ui.heading(c.text(strings::PROVIDER_DETAILS));
                    egui::ScrollArea::vertical()
                        .auto_shrink([false, false])
                        .show(ui, |ui| {
                            if matches {
                                if let Some(row) = s
                                    .result
                                    .rows
                                    .iter()
                                    .find(|r| Some(&r.id) == self.selected.as_ref())
                                {
                                    egui::Grid::new("provider-properties").striped(true).show(
                                        ui,
                                        |ui| {
                                            for (field, value) in &row.fields {
                                                ui.trading_caption(field);
                                                ui.trading_label(value);
                                                ui.end_row();
                                            }
                                        },
                                    );
                                }
                            }
                        });
                });
            if matches {
                if s.result.rows.is_empty() {
                    ui.trading_caption(c.text(strings::PROVIDER_EMPTY));
                }
                egui_extras::TableBuilder::new(ui)
                    .id_salt("provider-table")
                    .striped(true)
                    .resizable(true)
                    .column(egui_extras::Column::initial(230.))
                    .column(egui_extras::Column::initial(110.))
                    .column(egui_extras::Column::initial(140.))
                    .column(egui_extras::Column::remainder())
                    .header(24., |mut h| {
                        for text in [
                            strings::PROVIDER_SYMBOL,
                            strings::PROVIDER_INSTRUMENT,
                            strings::PROVIDER_EXPIRY,
                            strings::PROVIDER_CLASS,
                        ] {
                            h.col(|ui| {
                                ui.colored_label(crate::presentation::CAPTION_FOREGROUND, egui::RichText::new(c.text(text)).strong());
                            });
                        }
                    })
                    .bordered_body(24., |body| {
                        body.rows(26., s.result.rows.len(), |mut r| {
                            self.drawn_rows += 1;
                            let item = &s.result.rows[r.index()];
                            for (index, key) in [
                                "rawSymbol",
                                "instrumentId",
                                "expirationUtc",
                                "instrumentClass",
                            ]
                            .iter()
                            .enumerate()
                            {
                                r.col(|ui| {
                                    let text =
                                        item.fields.get(*key).map(String::as_str).unwrap_or("");
                                    if index == 0 {
                                        if ui
                                            .add_enabled(
                                                enabled && !self.mapping_open,
                                                egui::Button::new(text).selected(
                                                    self.selected.as_ref() == Some(&item.id),
                                                ),
                                            )
                                            .clicked()
                                        {
                                            self.selected = Some(item.id.clone());
                                        }
                                    } else {
                                        ui.trading_label(text);
                                    }
                                });
                            }
                        });
                    });
            }
        });
        if self.mapping_open {
            let mut cancel = false;
            egui::Window::new(crate::presentation::dialog_title(c.text(strings::PROVIDER_MAPPING)))
                .collapsible(false)
                .resizable(true)
                .default_width(600.)
                .show(ctx, |ui| {
                    ui.trading_caption(c.text(strings::PROVIDER_DRAFT_NOTICE));
                    ui.add_enabled_ui(enabled, |ui| {
                        egui::Grid::new("provider-mapping-input").show(ui, |ui| {
                            for (key, label) in [
                                ("existingContractId", strings::PROVIDER_TARGET),
                                ("exchangeTimeZoneId", strings::PROVIDER_ZONE),
                                (
                                    "underlyingContractId",
                                    strings::PROVIDER_UNDERLYING_CONTRACT,
                                ),
                            ] {
                                let r = ui.trading_caption(c.text(label));
                                ui.add(
                                    egui::TextEdit::singleline(
                                        self.mapping.entry(key.into()).or_default(),
                                    )
                                    .desired_width(300.)
                                    .min_size(egui::vec2(300., 20.)),
                                )
                                .labelled_by(r.id);
                                ui.end_row();
                            }
                        });
                        if ui.button(c.text(strings::PROVIDER_PREVIEW)).clicked() {
                            action = Some("preview");
                        }
                    });
                    let preview_matches = self.last_action == "preview"
                        && s.result.context.get("previewSelectedId") == self.selected.as_ref()
                        && s.result.context.contains_key("previewToken");
                    if preview_matches {
                        ui.trading_label(
                            s.result
                                .context
                                .get("previewMode")
                                .map(String::as_str)
                                .unwrap_or(""),
                        );
                        if let Some(value) = s.result.document::<serde_json::Value>("previewContract")
                        {
                            egui::ScrollArea::vertical()
                                .max_height(300.)
                                .show(ui, |ui| {
                                    egui::Grid::new("provider-mapping-preview")
                                        .max_col_width(460.)
                                        .striped(true)
                                        .show(ui, |ui| {
                                            if let Some(object) = value.as_object() {
                                                for (key, value) in object {
                                                    ui.trading_caption(key);
                                                    ui.trading_label(
                                                        value
                                                            .as_str()
                                                            .map(String::from)
                                                            .unwrap_or_else(|| value.to_string()),
                                                    );
                                                    ui.end_row();
                                                }
                                            }
                                        });
                                });
                        }
                        // Changing inputs after preview requires another preview before confirmation.
                        if ui
                            .add_enabled(
                                enabled && self.mapping == self.preview_fields,
                                egui::Button::new(c.text(strings::PROVIDER_CONFIRM)),
                            )
                            .clicked()
                        {
                            action = Some("publish");
                        }
                    }
                    if ui
                        .add_enabled(enabled, egui::Button::new(c.text(strings::CANCEL)))
                        .clicked()
                    {
                        cancel = true;
                    }
                });
            if cancel {
                self.mapping_open = false;
                self.last_action.clear();
            }
        }
        action.map(|a| self.request(a, s))
    }
}
