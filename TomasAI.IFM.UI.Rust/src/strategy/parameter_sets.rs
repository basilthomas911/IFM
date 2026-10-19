//! Strategy parameter-set editing, comparison, and activation previews. Field paths retain identity across nested component editors; numeric text buffers preserve incomplete edits until managed validation.

use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use std::collections::BTreeMap;
/// Flattened managed parameter field with a stable nested path, scalar kind, group, and editable textual value.
#[derive(Clone, Default, Deserialize, Serialize)]
pub struct Field {
    /// Stable nested parameter-field path or patch target path, according to the enclosing model.
    #[serde(rename = "Path")]
    pub path: String,
    /// Published or edited display value; formatting/validation belongs to its managed field contract.
    #[serde(rename = "Value")]
    pub value: String,
    /// Managed field-kind discriminator selecting the editor widget/representation.
    #[serde(rename = "Kind")]
    pub kind: u8,
    /// Selected parameter group or the group containing a field.
    #[serde(rename = "Group")]
    pub group: String,
    /// Published or edited display name.
    #[serde(rename = "Name")]
    pub name: String,
}
/// Parameter-set browser/editor with nested drafts, comparison identities, and confirmation generation.
#[derive(Default)]
pub struct ParameterSets {
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Publication generation used for change detection and request correlation.
    generation: u64,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
    /// Whether a local edit session is active.
    pub editing: bool,
    /// Tracks which original Reference command owns the edit session.
    pub changing: bool,
    area: String,
    main_section: String,
    /// Parent inspections restored when nested startup evidence/monitoring dialogs close.
    dialog_stack: Vec<String>,
    /// Published or edited display name.
    pub name: String,
    /// Human-readable description supplied by the domain projection or local draft.
    pub description: String,
    /// Edited strategy decision horizon.
    pub horizon: String,
    /// Selected editor section/tab key or index.
    pub section: String,
    /// Selected parameter group or the group containing a field.
    pub group: String,
    /// Named field definitions or display/edit values supplied by the enclosing model.
    pub fields: Vec<Field>,
    /// Local signal-definition draft associated with the selected parameter set.
    pub signals: Value,
    /// Local nested observation definitions from the parameter-set draft.
    pub observations: Value,
    /// Local interval definitions from the parameter-set draft.
    pub intervals: Value,
    /// Published/editable nested parameter component definitions.
    components: Value,
    /// Currently selected parameter component key.
    component: String,
    /// Pending local confirmation action/dialog key.
    pub confirmation: Option<String>,
    /// Publication generation captured when confirmation was opened.
    confirm_generation: u64,
    /// Legacy parameter-set identity retained for compatibility operations.
    legacy_id: String,
    /// Selected comparison/execution run identifier.
    run_id: String,
    /// Other parameter-set identity selected for comparison.
    compare_id: String,
    /// Text buffers preserving incomplete numeric edits across frames.
    numeric_text: BTreeMap<String, String>,
    /// Last submitted action key used to interpret completion and refresh behavior.
    last_action: String,
}
impl ParameterSets {
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns parameter-set browser/editor with nested drafts, comparison identities, and confirmation generation.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let mut result = None;
        egui::CentralPanel::default().show(ctx, |ui| {
            result = self.draw_inside(ui, s, c, live, None, false)
        });
        result
    }
    /// Render with the original Reference selector and command rail.
    pub fn draw_reference(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let auxiliary = !self.section.is_empty()
            && !matches!(
                self.section.as_str(),
                "fields" | "signals" | "observations" | "validation"
            );
        let ready =
            live && !s.busy && self.pending.is_none() && !auxiliary && self.confirmation.is_none();
        let status = self
            .selected
            .as_ref()
            .and_then(|id| s.result.rows.iter().find(|row| &row.id == id))
            .and_then(|row| row.fields.get("status"))
            .map(String::as_str);
        let commands = crate::reference::Commands {
            editing: self.editing,
            changing: self.changing,
            ready,
            add: self.components.as_array().is_some_and(|items| {
                items.iter().any(|item| {
                    item["ComponentCode"] == self.component
                        && item["CanEdit"].as_bool() == Some(true)
                })
            }),
            change: self.selected.is_some()
                && s.result.context.get("canEdit").is_some_and(|x| x == "True"),
            remove: status == Some("Published"),
            save: self.editing
                && !self.name.trim().is_empty()
                && !self.numeric_text.values().any(|text| {
                    serde_json::from_str::<Value>(text).map_or(true, |v| !v.is_number())
                }),
        };
        crate::reference::show(ctx, 2, commands, |ui, command| {
            self.draw_inside(ui, s, c, live, command, true)
        })
    }
    /// Render inside the Reference manager, accepting its original command-rail action.
    pub fn draw_inside(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
        command: Option<&str>,
        reference: bool,
    ) -> Option<String> {
        let ctx = ui.ctx().clone();
        if let Some(outcome) = self.pending.as_ref().and_then(|id| {
            s.outcomes
                .iter()
                .find(|outcome| &outcome.request_id == id && outcome.state != "Pending")
        }) {
            if outcome.state != "Completed" {
                self.last_action.clear();
            }
            self.pending = None;
        }
        let matching = s.result.view == "parameter-sets";
        if matching
            && !s.busy
            && self.pending.is_none()
            && self.last_action == "legacy-import"
            && s.result.context.get("legacyPreviewId") == Some(&self.legacy_id)
            && s.result.documents.contains_key("legacyPreview")
        {
            self.last_action = "legacy-create".into();
            let id = new_request_id();
            self.pending = Some(id.clone());
            return Some(json!({"schema":1,"requestId":id,"view":"parameter-sets","intent":"legacy-create","expectedGeneration":s.generation,"fields":{"legacyId":self.legacy_id}}).to_string());
        }
        if matching && !s.busy && self.pending.is_none() && s.generation != self.generation {
            self.generation = s.generation;

            let context = &s.result.context;
            let editing = context.get("editing").is_some_and(|x| x == "True");
            let selected = context.get("selectedId").filter(|x| !x.is_empty()).cloned();
            let keep_draft = self.editing
                && editing
                && self.selected == selected
                && self.last_action != "intervals";
            self.editing = editing;
            if !keep_draft {
                self.name = context.get("name").cloned().unwrap_or_default();
                self.description = context.get("description").cloned().unwrap_or_default();
                self.selected = context.get("selectedId").filter(|x| !x.is_empty()).cloned();
            }
            self.component = context.get("component").cloned().unwrap_or_default();
            self.components = parse(&s.result, "components");
            if !keep_draft {
                self.numeric_text.clear();
                self.fields = s.result.document("fields").unwrap_or_default();
                self.signals = parse(&s.result, "signals");
                self.observations = parse(&s.result, "observations");
                self.intervals = parse(&s.result, "intervals");
            }
        }
        if self.horizon.is_empty() {
            self.horizon = "Daily".into();
        }
        if self.section.is_empty() {
            self.section = if reference { "signals" } else { "fields" }.into();
            self.main_section = self.section.clone();
        }
        let enabled = live && !s.busy && self.pending.is_none();
        if reference && self.confirmation.is_some() {
            ui.disable();
        }
        if reference
            && !matches!(
                self.section.as_str(),
                "fields" | "signals" | "observations" | "validation"
            )
        {
            ui.disable();
        }
        let invalid_numbers = self.numeric_text.values().any(|text| {
            serde_json::from_str::<Value>(text).map_or(true, |value| !value.is_number())
        });
        let has = matching
            && s.result
                .context
                .get("hasPayload")
                .is_some_and(|x| x == "True");
        let can_edit = matching && s.result.context.get("canEdit").is_some_and(|x| x == "True");
        let saved = has && !self.editing && self.selected.is_some();
        let status = self
            .selected
            .as_ref()
            .and_then(|id| s.result.rows.iter().find(|r| &r.id == id))
            .and_then(|r| r.fields.get("status"))
            .map(String::as_str)
            .unwrap_or("");
        let mut action = None;
        let mut extra = BTreeMap::<String, String>::new();
        let mut target = self.selected.clone();
        if reference && enabled {
            if !matching {
                action = Some("load");
            }
            match command {
                Some("add") if !self.editing => {
                    self.changing = false;
                    action = Some("new-draft");
                }
                Some("change") if !self.editing => {
                    self.changing = true;
                    action = Some("edit-draft");
                }
                Some("add" | "change")
                    if self.editing && !invalid_numbers && !self.name.trim().is_empty() =>
                {
                    action = Some("save-draft")
                }
                Some("close") => action = Some("discard"),
                Some("remove") => {
                    self.confirmation = Some("retire-parameter".into());
                    self.confirm_generation = s.generation;
                }
                _ => {}
            }
        }

        egui::SidePanel::left("parameter-navigation")
            .resizable(true)
            .frame(egui::Frame::NONE.fill(egui::Color32::BLACK))
            .default_width(if reference {
                430.
            } else {
                (ui.available_width() * crate::presentation::PARAMETER_NAVIGATION_RATIO).max(280.0)
            })
            .show_inside(ui, |ui| {
                if !reference {
                    ui.heading(c.text(strings::PARAMETER_SETS));
                    if ui
                        .add_enabled(enabled, egui::Button::new(c.text(strings::PARAM_RESUME)))
                        .clicked()
                    {
                        action = Some("resume-parameter");
                    }
                    if ui
                        .add_enabled(
                            enabled && !self.editing,
                            egui::Button::new(c.text(strings::PARAM_REFRESH)),
                        )
                        .clicked()
                    {
                        action = Some("load");
                    }
                }
                if reference {
                    crate::reference::header(ui, "Parameter Set");
                    let list_height = ((ui.available_height() - 30.) * 0.45).max(40.);
                    if let Some(components) = self.components.as_array() {
                        let mut areas: Vec<_> = components
                            .iter()
                            .filter_map(|item| item["AreaCode"].as_str())
                            .collect();
                        areas.sort();
                        areas.dedup();
                        if !areas.contains(&self.area.as_str()) {
                            self.area = components
                                .iter()
                                .find(|item| item["ComponentCode"] == self.component)
                                .and_then(|item| item["AreaCode"].as_str())
                                .or_else(|| areas.first().copied())
                                .unwrap_or("")
                                .into();
                        }
                        egui::ScrollArea::vertical()
                            .id_salt("reference-parameter-areas")
                            .min_scrolled_height(list_height)
                            .max_height(list_height)
                            .auto_shrink([false, false])
                            .show(ui, |ui| {
                                for area in areas {
                                    let label = components
                                        .iter()
                                        .find(|item| item["AreaCode"] == area)
                                        .and_then(|item| item["AreaName"].as_str())
                                        .unwrap_or(area);
                                    if ui
                                        .add_enabled(
                                            enabled && !self.editing,
                                            egui::Button::new(
                                                egui::RichText::new(label)
                                                    .color(egui::Color32::WHITE),
                                            )
                                            .frame(true)
                                            .fill(if self.area == area {
                                                egui::Color32::from_rgb(0, 120, 215)
                                            } else {
                                                egui::Color32::BLACK
                                            })
                                            .stroke(egui::Stroke::NONE)
                                            .min_size(egui::vec2(ui.available_width(), 23.))
                                            .selected(self.area == area),
                                        )
                                        .clicked()
                                    {
                                        self.area = area.into();
                                        if let Some(item) =
                                            components.iter().find(|item| item["AreaCode"] == area)
                                        {
                                            extra.insert(
                                                "component".into(),
                                                item["ComponentCode"].as_str().unwrap_or("").into(),
                                            );
                                            action = Some("component");
                                        }
                                    }
                                }
                            });
                        let title = components
                            .iter()
                            .find(|item| item["AreaCode"] == self.area)
                            .and_then(|item| item["AreaName"].as_str())
                            .unwrap_or("");
                        crate::reference::header(ui, title);
                        egui::ScrollArea::vertical()
                            .id_salt("reference-parameter-components")
                            .auto_shrink([false, false])
                            .show(ui, |ui| {
                                for item in components
                                    .iter()
                                    .filter(|item| item["AreaCode"] == self.area)
                                {
                                    let code = item["ComponentCode"].as_str().unwrap_or("");
                                    let label = item["Name"].as_str().unwrap_or(code);
                                    if ui
                                        .add_enabled(
                                            enabled && !self.editing,
                                            egui::Button::new(
                                                egui::RichText::new(label)
                                                    .color(egui::Color32::WHITE),
                                            )
                                            .frame(true)
                                            .fill(if self.component == code {
                                                egui::Color32::from_rgb(0, 120, 215)
                                            } else {
                                                egui::Color32::BLACK
                                            })
                                            .stroke(egui::Stroke::NONE)
                                            .min_size(egui::vec2(ui.available_width(), 23.))
                                            .selected(self.component == code),
                                        )
                                        .clicked()
                                    {
                                        extra.insert("component".into(), code.into());
                                        action = Some("component");
                                    }
                                }
                            });
                    }
                } else {
                    if let Some(components) = self.components.as_array() {
                        let mut areas = components
                            .iter()
                            .filter_map(|x| x.get("AreaCode").and_then(Value::as_str))
                            .collect::<Vec<_>>();
                        areas.sort();
                        areas.dedup();
                        egui::ScrollArea::vertical()
                            .id_salt("parameter-components")
                            .max_height(200.)
                            .show(ui, |ui| {
                                for area in areas {
                                    let label = components
                                        .iter()
                                        .find(|x| x["AreaCode"] == area)
                                        .and_then(|x| x["AreaName"].as_str())
                                        .unwrap_or(area);
                                    egui::CollapsingHeader::new(label)
                                        .id_salt(area)
                                        .default_open(true)
                                        .show(ui, |ui| {
                                            for component in
                                                components.iter().filter(|x| x["AreaCode"] == area)
                                            {
                                                let code = component["ComponentCode"]
                                                    .as_str()
                                                    .unwrap_or("");
                                                if ui
                                                    .add_enabled(
                                                        enabled && !self.editing,
                                                        egui::Button::new(
                                                            component["Name"]
                                                                .as_str()
                                                                .unwrap_or(code),
                                                        )
                                                        .selected(self.component == code),
                                                    )
                                                    .clicked()
                                                {
                                                    extra.insert("component".into(), code.into());
                                                    action = Some("component");
                                                }
                                            }
                                        });
                                }
                            });
                    }
                    ui.heading(c.text(strings::PARAM_VERSIONS));
                    let rows = if matching {
                        s.result.rows.as_slice()
                    } else {
                        &[]
                    };
                    egui::ScrollArea::vertical()
                        .id_salt("parameter-versions")
                        .show_rows(ui, 24., rows.len(), |ui, range| {
                            for i in range {
                                let row = &rows[i];
                                let label = format!(
                                    "{} - v{} ({})",
                                    row.fields.get("name").map(String::as_str).unwrap_or(""),
                                    row.fields.get("version").map(String::as_str).unwrap_or(""),
                                    row.fields.get("status").map(String::as_str).unwrap_or("")
                                );
                                if ui
                                    .add_enabled(
                                        enabled && !self.editing,
                                        egui::Button::new(label)
                                            .selected(self.selected.as_ref() == Some(&row.id)),
                                    )
                                    .clicked()
                                {
                                    target = Some(row.id.clone());
                                    action = Some("select");
                                }
                            }
                        });
                }
            });
        egui::CentralPanel::default().show_inside(ui, |ui| {
            if reference {
                egui::TopBottomPanel::bottom("reference-parameter-status")
                    .exact_height(42.)
                    .show_inside(ui, |ui| {
                        ui.add(crate::presentation::label_widget(&s.status).wrap());
                    });
                let title = self
                    .components
                    .as_array()
                    .and_then(|items| {
                        items
                            .iter()
                            .find(|item| item["ComponentCode"] == self.component)
                    })
                    .and_then(|item| item["Name"].as_str())
                    .unwrap_or("");
                crate::reference::header(ui, title);
                egui::ScrollArea::vertical()
                    .id_salt("parameter-versions")
                    .min_scrolled_height(125.)
                    .max_height(125.)
                    .auto_shrink([false, false])
                    .show_rows(
                        ui,
                        23.,
                        if matching { s.result.rows.len() } else { 0 },
                        |ui, range| {
                            for index in range {
                                let row = &s.result.rows[index];
                                let label = format!(
                                    "{} - v{} ({})",
                                    row.fields.get("name").map(String::as_str).unwrap_or(""),
                                    row.fields.get("version").map(String::as_str).unwrap_or(""),
                                    row.fields.get("status").map(String::as_str).unwrap_or("")
                                );
                                if ui
                                    .add_enabled(
                                        enabled && !self.editing,
                                        egui::Button::new(
                                            egui::RichText::new(label).color(egui::Color32::WHITE),
                                        )
                                        .frame(true)
                                        .fill(if self.selected.as_ref() == Some(&row.id) {
                                            egui::Color32::from_rgb(0, 120, 215)
                                        } else {
                                            egui::Color32::BLACK
                                        })
                                        .stroke(egui::Stroke::NONE)
                                        .min_size(egui::vec2(ui.available_width(), 23.))
                                        .selected(self.selected.as_ref() == Some(&row.id)),
                                    )
                                    .clicked()
                                {
                                    target = Some(row.id.clone());
                                    action = Some("select");
                                }
                            }
                        },
                    );
                ui.horizontal_wrapped(|ui| {
                    let caption = ui.add(crate::presentation::caption_widget("New horizon"));
                    ui.add_enabled_ui(enabled && !self.editing, |ui| {
                        egui::ComboBox::from_id_salt("reference-new-horizon")
                            .width(110.)
                            .selected_text(&self.horizon)
                            .show_ui(ui, |ui| {
                                for horizon in ["Daily", "Weekly", "Monthly"] {
                                    ui.selectable_value(&mut self.horizon, horizon.into(), horizon);
                                }
                            })
                            .response
                            .labelled_by(caption.id)
                    });
                    let caption = ui.add(crate::presentation::caption_widget("Name"));
                    ui.add(
                        egui::TextEdit::singleline(&mut self.name)
                            .interactive(enabled && self.editing)
                            .desired_width(260.),
                    )
                    .labelled_by(caption.id);
                    for (label, intent, allowed) in [
                        ("Refresh", "load", !self.editing),
                        ("Legacy versions", "legacy-parameters", true),
                        ("Payload", "payload-parameter", has),
                        ("Schema", "schema-parameter", has),
                        (
                            "Intervals",
                            "show-intervals",
                            self.editing
                                && s.result.context.get("regime").is_some_and(|v| v == "True"),
                        ),
                        ("Validate", "validate-parameter", has),
                        ("Publish", "publish-confirm", saved && status == "Draft"),
                        ("Assignment", "assignment", saved && status == "Published"),
                        ("Startup preview", "startup-preview", matching),
                        ("Recent startups", "startup-runs", matching),
                        ("Compare", "compare-parameter", saved),
                        ("Operations", "audit-parameter", saved),
                    ] {
                        if ui
                            .add_enabled(enabled && allowed, egui::Button::new(label))
                            .clicked()
                        {
                            match intent {
                                "publish-confirm" => {
                                    self.confirmation = Some("publish-parameter".into());
                                    self.confirm_generation = s.generation;
                                }
                                "show-intervals" => self.section = "intervals".into(),
                                _ => action = Some(intent),
                            }
                        }
                    }
                });
            }
            if !reference {
                ui.trading_label(&s.status);
                if invalid_numbers {
                    ui.trading_colored_label(
                        egui::Color32::LIGHT_RED,
                        c.text(strings::PARAM_INVALID_NUMBERS),
                    );
                }
                ui.horizontal_wrapped(|ui| {
                    ui.trading_caption(c.text(strings::PARAM_HORIZON));
                    egui::ComboBox::from_id_salt("new-parameter-horizon")
                        .selected_text(&self.horizon)
                        .show_ui(ui, |ui| {
                            for v in ["Daily", "Weekly", "Monthly"] {
                                ui.selectable_value(&mut self.horizon, v.into(), v);
                            }
                        });
                    if !reference {
                        button(
                            ui,
                            c,
                            strings::PARAM_NEW,
                            enabled && !self.editing && matching,
                            "new-draft",
                            &mut action,
                        );
                        button(
                            ui,
                            c,
                            strings::PARAM_EDIT,
                            enabled && saved && can_edit,
                            "edit-draft",
                            &mut action,
                        );
                        button(
                            ui,
                            c,
                            strings::PARAM_SAVE,
                            enabled
                                && matching
                                && self.editing
                                && !invalid_numbers
                                && !self.name.trim().is_empty(),
                            "save-draft",
                            &mut action,
                        );
                        button(
                            ui,
                            c,
                            strings::PARAM_DISCARD,
                            enabled && matching && self.editing,
                            "discard",
                            &mut action,
                        );
                    }
                    button(
                        ui,
                        c,
                        strings::PARAM_VALIDATE,
                        enabled && has,
                        "validate-parameter",
                        &mut action,
                    );
                    if ui
                        .add_enabled(
                            enabled && saved && status == "Draft",
                            egui::Button::new(c.text(strings::PARAM_PUBLISH)),
                        )
                        .clicked()
                    {
                        self.confirmation = Some("publish-parameter".into());
                        self.confirm_generation = s.generation;
                    }
                    if ui
                        .add_enabled(
                            enabled && saved && status == "Published",
                            egui::Button::new(c.text(strings::PARAM_RETIRE)),
                        )
                        .clicked()
                    {
                        self.confirmation = Some("retire-parameter".into());
                        self.confirm_generation = s.generation;
                    }
                });
                ui.horizontal_wrapped(|ui| {
                    button(
                        ui,
                        c,
                        strings::PARAM_SCHEMA,
                        enabled && has,
                        "schema-parameter",
                        &mut action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_PAYLOAD,
                        enabled && has,
                        "payload-parameter",
                        &mut action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_COMPARE,
                        enabled && saved,
                        "compare-parameter",
                        &mut action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_AUDIT,
                        enabled && saved,
                        "audit-parameter",
                        &mut action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_ASSIGNMENT,
                        enabled
                            && saved
                            && status == "Published"
                            && s.result.context.get("regime").is_some_and(|x| x == "True"),
                        "assignment",
                        &mut action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_STARTUP_PREVIEW,
                        enabled && matching,
                        "startup-preview",
                        &mut action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_STARTUP_RUNS,
                        enabled && matching,
                        "startup-runs",
                        &mut action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_LEGACY,
                        enabled && matching,
                        "legacy-parameters",
                        &mut action,
                    );
                    if ui
                        .add_enabled(
                            enabled && saved,
                            egui::Button::new(c.text(strings::PARAM_RENAME)),
                        )
                        .clicked()
                    {
                        self.section = "rename".into();
                    }
                });
                ui.add_enabled_ui(enabled && self.editing, |ui| {
                    ui.horizontal(|ui| {
                        let label = ui.trading_caption(c.text(strings::PARAM_NAME));
                        ui.text_edit_singleline(&mut self.name)
                            .labelled_by(label.id);
                    });
                    ui.horizontal(|ui| {
                        let label = ui.trading_caption(c.text(strings::PARAM_DESCRIPTION));
                        ui.text_edit_singleline(&mut self.description)
                            .labelled_by(label.id);
                    });
                });
            }
            ui.horizontal_wrapped(|ui| {
                for (key, label) in [
                    ("signals", strings::PARAM_SIGNALS),
                    ("observations", strings::PARAM_OBSERVATIONS),
                    ("fields", strings::PARAM_FIELDS),
                    ("intervals", strings::PARAM_INTERVALS),
                    ("validation", strings::PARAM_VALIDATION),
                ] {
                    if reference && key == "intervals" {
                        continue;
                    }
                    if crate::presentation::tab_label(ui, self.section == key, c.text(label))
                        .clicked()
                    {
                        self.section = key.into();
                        self.main_section = key.into();
                    }
                }
            });
            let auxiliary = reference
                && !matches!(
                    self.section.as_str(),
                    "fields" | "signals" | "observations" | "validation"
                );
            if auxiliary {
                let selected = self.section.clone();
                self.section = if self.main_section.is_empty() {
                    "signals".into()
                } else {
                    self.main_section.clone()
                };
                ui.add_enabled_ui(false, |ui| {
                    self.render_section(ui, s, c, false, saved, reference, &mut action, &mut extra)
                });
                self.section = selected.clone();
                let interval_title = format!("{} signal intervals", self.horizon);
                let (title, width, height) = match selected.as_str() {
                    "payload" => ("Parameter values", 850., 700.),
                    "schema" => ("Parameter contract", 850., 700.),
                    "intervals" => (interval_title.as_str(), 650., 430.),
                    "assignment" => ("Workflow parameter assignment", 590., 220.),
                    "compare" => ("Compare parameter versions", 1000., 600.),
                    "audit" => ("Committed parameter operations", 1050., 500.),
                    "startupPreview" => ("Pending startup plan (preview only)", 1000., 650.),
                    "startupRuns" => ("Recent parameter startup snapshots", 1000., 600.),
                    "startupReport" => ("Startup preparation evidence", 1000., 650.),
                    "signalMonitoring" => ("Monitored signal availability", 1150., 650.),
                    "legacy" => ("Legacy Regime Discovery versions (read only)", 1100., 680.),
                    _ => ("Parameter set metadata", 460., 250.),
                };
                let mut open = true;
                if self.pending.is_none() && !s.busy {
                    egui::Window::new(crate::presentation::dialog_title(title))
                        .id(egui::Id::new(("reference-parameter-dialog", &selected)))
                        .open(&mut open)
                        .collapsible(false)
                        .default_size(
                            egui::vec2(width - 16., height - 39.)
                                .min(ctx.screen_rect().size() - egui::vec2(32., 64.)),
                        )
                        .show(&ctx, |ui| {
                            ui.set_min_size(
                                egui::vec2(width - 16., height - 39.)
                                    .min(ctx.screen_rect().size() - egui::vec2(32., 64.)),
                            );
                            self.render_section(
                                ui,
                                s,
                                c,
                                enabled,
                                saved,
                                reference,
                                &mut action,
                                &mut extra,
                            )
                        });
                    if !open {
                        self.section = self
                            .dialog_stack
                            .pop()
                            .unwrap_or_else(|| self.main_section.clone());
                    }
                }
            } else {
                self.render_section(ui, s, c, enabled, saved, reference, &mut action, &mut extra);
            }
        });
        if let Some(intent) = self.confirmation.clone() {
            egui::Window::new(crate::presentation::dialog_title(
                c.text(strings::PARAM_CONFIRM),
            ))
            .collapsible(false)
            .show(&ctx, |ui| {
                ui.trading_label(if intent == "legacy-create" {
                    &self.legacy_id
                } else {
                    &self.name
                });
                ui.trading_caption(c.text(&format!("param_action_{}", intent.replace('-', "_"))));
                if self.confirm_generation != s.generation {
                    ui.trading_caption(c.text(strings::PARAM_STALE));
                }
                ui.horizontal(|ui| {
                    if ui.button(c.text(strings::CANCEL)).clicked() {
                        self.confirmation = None;
                    }
                    if ui
                        .add_enabled(
                            enabled && self.confirm_generation == s.generation,
                            egui::Button::new(c.text(strings::PARAM_CONFIRM_ACTION)),
                        )
                        .clicked()
                    {
                        action = Some(match intent.as_str() {
                            "publish-parameter" => "publish-parameter",
                            "retire-parameter" => "retire-parameter",
                            "assign-parameter" => "assign-parameter",
                            "disable-assignment" => "disable-assignment",
                            _ => "legacy-create",
                        });
                        self.confirmation = None;
                    }
                });
            });
        }
        action.map(|intent|{if reference&&matches!(intent,"startup-report"|"signal-monitoring"){self.dialog_stack.push(self.section.clone());}if self.last_action!="legacy-import"{self.last_action=intent.into();}self.section=match intent{"validate-parameter"=>"validation","schema-parameter"=>"schema","payload-parameter"=>"payload","compare-parameter"=>"compare","audit-parameter"=>"audit","assignment"|"assign-parameter"|"disable-assignment"=>"assignment","startup-preview"=>"startupPreview","startup-runs"=>"startupRuns","startup-report"=>"startupReport","signal-monitoring"=>"signalMonitoring","legacy-parameters"|"legacy-prev"|"legacy-next"|"legacy-preview"=>"legacy",_=>&self.section}.into();let id=new_request_id();self.pending=Some(id.clone());let mut documents = serde_json::Map::new();if ["validate-parameter","save-draft","intervals"].contains(&intent)&&self.editing{documents.insert("changes".into(),serde_json::to_value(&self.fields).unwrap());documents.insert("signals".into(),self.signals.clone());documents.insert("observations".into(),self.observations.clone());}extra.insert("name".into(),self.name.clone());extra.insert("description".into(),self.description.clone());extra.insert("horizon".into(),self.horizon.clone());documents.insert("intervals".into(),self.intervals.clone());extra.insert("runId".into(),self.run_id.clone());extra.insert("legacyId".into(),self.legacy_id.clone());serde_json::json!({"schema":1,"requestId":id,"view":"parameter-sets","intent":intent,"expectedGeneration":s.generation,"originalId":target,"fields":extra,"documents":documents}).to_string()})
    }
    /// Render one parameter section without moving query or persistence work onto the UI thread.
    fn render_section(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        c: &Catalog,
        enabled: bool,
        saved: bool,
        reference: bool,
        action: &mut Option<&'static str>,
        extra: &mut BTreeMap<String, String>,
    ) {
        match self.section.as_str() {
            "fields" if reference => {
                let mut groups: Vec<_> = self
                    .fields
                    .iter()
                    .map(|field| field.group.clone())
                    .collect();
                groups.sort();
                groups.dedup();
                if !groups.contains(&self.group) {
                    self.group = groups.first().cloned().unwrap_or_default();
                }
                egui::SidePanel::left("reference-calculation-groups")
                    .default_width(190.)
                    .min_width(140.)
                    .resizable(true)
                    .show_inside(ui, |ui| {
                        egui::ScrollArea::vertical()
                            .id_salt("parameter-field-groups")
                            .show(ui, |ui| {
                                for group in groups {
                                    if ui
                                        .add(
                                            egui::Button::new(
                                                egui::RichText::new(&group)
                                                    .color(egui::Color32::WHITE),
                                            )
                                            .frame(true)
                                            .fill(if self.group == group {
                                                egui::Color32::from_rgb(0, 120, 215)
                                            } else {
                                                egui::Color32::BLACK
                                            })
                                            .stroke(egui::Stroke::NONE)
                                            .min_size(egui::vec2(ui.available_width(), 23.))
                                            .selected(self.group == group),
                                        )
                                        .clicked()
                                    {
                                        self.group = group;
                                    }
                                }
                            });
                    });
                egui::CentralPanel::default().show_inside(ui, |ui| {
                    crate::reference::header(ui, &self.group);
                    let indices: Vec<_> = self
                        .fields
                        .iter()
                        .enumerate()
                        .filter(|(_, field)| field.group == self.group)
                        .map(|(index, _)| index)
                        .collect();
                    let name_width = ui.available_width() * 0.65;
                    egui_extras::TableBuilder::new(ui)
                        .id_salt("reference-calculation-properties")
                        .column(egui_extras::Column::initial(name_width))
                        .column(egui_extras::Column::remainder())
                        .header(28., |mut header| {
                            for label in ["Parameter", "Value"] {
                                header.col(|ui| {
                                    ui.add(crate::presentation::label_widget(label));
                                });
                            }
                        })
                        .body(|body| {
                            body.rows(23., indices.len(), |mut row| {
                                let field = &mut self.fields[indices[row.index()]];
                                row.col(|ui| {
                                    ui.label(
                                        egui::RichText::new(&field.name)
                                            .color(egui::Color32::WHITE),
                                    );
                                });
                                row.col(|ui| {
                                    ui.add_enabled(
                                        enabled && self.editing,
                                        egui::TextEdit::singleline(&mut field.value)
                                            .id_salt(&field.path)
                                            .desired_width(f32::INFINITY),
                                    );
                                });
                            })
                        });
                });
            }
            "fields" => {
                let mut groups = self
                    .fields
                    .iter()
                    .map(|x| x.group.clone())
                    .collect::<Vec<_>>();
                groups.sort();
                groups.dedup();
                if !groups.contains(&self.group) {
                    self.group = groups.first().cloned().unwrap_or_default();
                }
                egui::ComboBox::from_id_salt("parameter-field-group")
                    .selected_text(&self.group)
                    .show_ui(ui, |ui| {
                        for group in groups {
                            ui.selectable_value(&mut self.group, group.clone(), group);
                        }
                    });
                let indices = self
                    .fields
                    .iter()
                    .enumerate()
                    .filter(|(_, f)| f.group == self.group)
                    .map(|(i, _)| i)
                    .collect::<Vec<_>>();
                egui::ScrollArea::vertical()
                    .id_salt("parameter-field-values")
                    .show_rows(ui, 26., indices.len(), |ui, range| {
                        for i in range {
                            let field = &mut self.fields[indices[i]];
                            ui.horizontal(|ui| {
                                let label = ui.trading_label(&field.name);
                                ui.add_enabled(
                                    enabled && self.editing,
                                    egui::TextEdit::singleline(&mut field.value)
                                        .id_salt(&field.path)
                                        .desired_width(220.),
                                )
                                .labelled_by(label.id);
                            });
                        }
                    });
            }
            "signals" if reference => edit_reference_rows(
                ui,
                &mut self.signals,
                "signals",
                enabled && self.editing,
                s,
                &mut self.numeric_text,
            ),
            "observations" if reference => edit_reference_rows(
                ui,
                &mut self.observations,
                "observations",
                enabled && self.editing,
                s,
                &mut self.numeric_text,
            ),
            "signals" => edit_rows(
                ui,
                &mut self.signals,
                "signals",
                enabled && self.editing,
                s,
                c,
                &mut self.numeric_text,
            ),
            "observations" => edit_rows(
                ui,
                &mut self.observations,
                "observations",
                enabled && self.editing,
                s,
                c,
                &mut self.numeric_text,
            ),
            "intervals" if reference => {
                egui::TopBottomPanel::bottom("reference-interval-buttons")
                    .exact_height(42.)
                    .show_inside(ui, |ui| {
                        ui.horizontal(|ui| {
                            if ui
                                .add_enabled(enabled && self.editing, egui::Button::new("Apply"))
                                .clicked()
                            {
                                *action = Some("intervals");
                                self.section = self.main_section.clone();
                            }
                            if ui.button("Cancel").clicked() {
                                self.intervals = parse(&s.result, "intervals");
                                self.numeric_text
                                    .retain(|key, _| !key.starts_with("intervals/"));
                                self.section = self.main_section.clone();
                            }
                        })
                    });
                ui.add_sized([ui.available_width(),55.],crate::presentation::label_widget("Every listed interval is included. Applying regenerates its signal requirements and calculation dependencies. Review the resulting signal list before saving.").wrap());
                edit_interval_grid(
                    ui,
                    &mut self.intervals,
                    enabled && self.editing,
                    s,
                    &mut self.numeric_text,
                );
            }
            "intervals" => {
                edit_rows(
                    ui,
                    &mut self.intervals,
                    "intervals",
                    enabled && self.editing,
                    s,
                    c,
                    &mut self.numeric_text,
                );
                button(
                    ui,
                    c,
                    strings::PARAM_APPLY_INTERVALS,
                    enabled && self.editing,
                    "intervals",
                    action,
                );
            }
            "rename" => {
                ui.trading_caption(c.text(strings::PARAM_RENAME));
                ui.text_edit_singleline(&mut self.name);
                ui.text_edit_multiline(&mut self.description);
                button(
                    ui,
                    c,
                    strings::PARAM_SAVE_METADATA,
                    enabled && saved,
                    "rename-parameter",
                    action,
                );
            }
            "assignment" if reference => {
                if let Some(value) = s.result.document::<Value>("assignment") {
                    let workflow = value["Scope"]["ConsumerId"]
                        .as_str()
                        .or_else(|| value["Scope"]["WorkflowCode"].as_str())
                        .unwrap_or("IntrinsicTimeStrategy");
                    let selected_version = self
                        .selected
                        .as_ref()
                        .and_then(|id| s.result.rows.iter().find(|row| &row.id == id))
                        .and_then(|row| row.fields.get("version"))
                        .map(String::as_str)
                        .unwrap_or("");
                    let configured = if value["Assignment"]["Enabled"].as_bool() == Some(true) {
                        format!(
                            "{}, version {}",
                            value["Assignment"]["Reference"]["SetId"],
                            value["Assignment"]["Reference"]["Version"]
                        )
                    } else {
                        "None".into()
                    };
                    ui.label(format!("Workflow: {workflow}\nHorizon: {}\nSelected: {}, version {selected_version}\nConfigured: {configured}\nAssignment changes take effect at the next application startup.",self.horizon,self.name));
                    ui.horizontal(|ui| {
                        if ui
                            .add_enabled(enabled && saved, egui::Button::new("Assign selected"))
                            .clicked()
                        {
                            *action = Some("assign-parameter");
                        }
                        if ui
                            .add_enabled(
                                enabled
                                    && saved
                                    && value["Assignment"]["Enabled"].as_bool() == Some(true),
                                egui::Button::new("Disable"),
                            )
                            .clicked()
                        {
                            *action = Some("disable-assignment");
                        }
                        if ui.button("Cancel").clicked() {
                            self.section = self.main_section.clone();
                        }
                    });
                }
            }
            "assignment" => {
                ui.trading_caption(c.text(strings::PARAM_ASSIGNMENT_NOTICE));
                if let Some(v) = s.result.document::<Value>("assignment") {
                    read_value(ui, &v, "assignment");
                    ui.horizontal(|ui| {
                        if ui
                            .add_enabled(
                                enabled && saved,
                                egui::Button::new(c.text(strings::PARAM_ASSIGN)),
                            )
                            .clicked()
                        {
                            self.confirmation = Some("assign-parameter".into());
                            self.confirm_generation = s.generation;
                        }
                        if ui
                            .add_enabled(
                                enabled && saved,
                                egui::Button::new(c.text(strings::PARAM_DISABLE_ASSIGNMENT)),
                            )
                            .clicked()
                        {
                            self.confirmation = Some("disable-assignment".into());
                            self.confirm_generation = s.generation;
                        }
                    });
                }
            }
            "compare" => {
                if reference {
                    report_footer(
                        ui,
                        "parameter-comparison",
                        35.,
                        &format!("Before: selected comparison version. After: {}.", self.name),
                    );
                }
                if let Some(v) = s
                    .result
                    .document::<Value>("comparisonVersions")
                    .and_then(|x| x.as_array().cloned())
                {
                    if self.compare_id.is_empty() {
                        self.compare_id = v
                            .first()
                            .and_then(|row| row["Id"].as_str())
                            .unwrap_or("")
                            .into();
                    }
                    let caption = v
                        .iter()
                        .find(|row| row["Id"] == self.compare_id)
                        .map(|row| {
                            format!(
                                "{} - v{}",
                                row["Name"].as_str().unwrap_or(""),
                                row["Version"]
                            )
                        })
                        .unwrap_or_default();
                    egui::ComboBox::from_id_salt("parameter-comparison")
                        .selected_text(caption)
                        .show_ui(ui, |ui| {
                            for row in v {
                                let id = row["Id"].as_str().unwrap_or("");
                                if ui
                                    .selectable_value(
                                        &mut self.compare_id,
                                        id.into(),
                                        format!(
                                            "{} - v{}",
                                            row["Name"].as_str().unwrap_or(""),
                                            row["Version"]
                                        ),
                                    )
                                    .changed()
                                {
                                    extra.insert("compareId".into(), id.into());
                                    *action = Some("compare-parameter");
                                }
                            }
                        });
                }
                if let Some(value) = s.result.document::<Value>("differences") {
                    if reference {
                        reference_table(ui, &value, "differences");
                    } else {
                        read_value(ui, &value, "differences");
                    }
                }
            }
            "startupRuns" if reference => {
                if let Some(rows) = s
                    .result
                    .document::<Value>("startupRuns")
                    .and_then(|v| v.as_array().cloned())
                {
                    report_footer(
                        ui,
                        "startup-runs",
                        48.,
                        "Double-click a startup for preparation evidence. Latest 100 snapshots for traceability. Assignments persist until changed or disabled. Running workflows retain their captured version.",
                    );
                    let rows:Vec<_>=rows.into_iter().map(|row|json!({"RunId":row["RunId"],"CreatedAtUtc":row["CreatedAtUtc"],"CreatedBy":row["CreatedBy"],"Assignments":row["Scopes"].as_array().map(Vec::len).unwrap_or(0),"Fingerprint":row["Plan"]["Fingerprint"]})).collect();
                    if let Some(index) = reference_grid(
                        ui,
                        &rows,
                        "startup-runs",
                        &[
                            "RunId",
                            "CreatedAtUtc",
                            "CreatedBy",
                            "Assignments",
                            "Fingerprint",
                        ],
                        true,
                    ) {
                        self.run_id = rows[index]["RunId"].as_str().unwrap_or("").into();
                        *action = Some("startup-report");
                    }
                }
            }
            "startupRuns" => {
                if let Some(v) = s
                    .result
                    .document::<Value>("startupRuns")
                    .and_then(|x| x.as_array().cloned())
                {
                    egui::ScrollArea::vertical()
                        .id_salt("startup-list")
                        .max_height(200.)
                        .show_rows(ui, 24., v.len(), |ui, range| {
                            for i in range {
                                let row = &v[i];
                                let id = row["RunId"].as_str().unwrap_or("");
                                if ui
                                    .selectable_label(
                                        self.run_id == id,
                                        format!("{} {}", row["CreatedAtUtc"], id),
                                    )
                                    .clicked()
                                {
                                    self.run_id = id.into();
                                }
                            }
                        });
                    button(
                        ui,
                        c,
                        strings::PARAM_STARTUP_REPORT,
                        enabled && !self.run_id.is_empty(),
                        "startup-report",
                        action,
                    );
                    button(
                        ui,
                        c,
                        strings::PARAM_SIGNAL_MONITORING,
                        enabled && !self.run_id.is_empty(),
                        "signal-monitoring",
                        action,
                    );
                }
            }
            "startupPreview" if reference => {
                if let Some(value) = s.result.document::<Value>("startupPreview") {
                    report_footer(
                        ui,
                        "startup-preview",
                        50.,
                        &format!(
                            "Fingerprint: {}. Preview does not activate producers or change the current startup. {}",
                            value["Fingerprint"], value["Issues"]
                        ),
                    );
                    let rows:Vec<_>=value["Steps"].as_array().into_iter().flatten().map(|step|json!({"Producer":step["Key"]["Producer"],"Interval":step["Key"]["Interval"],"Period":step["Key"]["Period"],"Prepare":step["Prepare"],"Monitor":step["Monitor"],"Consumers":step["Consumers"].as_array().into_iter().flatten().filter_map(|c|c["Consumer"].as_str()).collect::<Vec<_>>().join(", ")})).collect();
                    reference_grid(
                        ui,
                        &rows,
                        "startup-preview",
                        &[
                            "Producer",
                            "Interval",
                            "Period",
                            "Prepare",
                            "Monitor",
                            "Consumers",
                        ],
                        false,
                    );
                }
            }
            "startupReport" if reference => {
                if ui
                    .add_enabled(enabled, egui::Button::new("Refresh monitored signals"))
                    .clicked()
                {
                    *action = Some("signal-monitoring");
                }
                if let Some(value) = s.result.document::<Value>("startupReport") {
                    report_footer(
                        ui,
                        "startup-report",
                        45.,
                        &format!(
                            "Recorded {}; value date {}; contract {}. Preparation evidence does not imply warm or fresh signals.",
                            value["RecordedAtUtc"], value["ValueDate"], value["ContractId"]
                        ),
                    );
                    let rows:Vec<_>=value["Outcomes"].as_array().into_iter().flatten().map(|row|json!({"Producer":row["Key"]["Producer"],"Interval":row["Key"]["Interval"],"Period":row["Key"]["Period"],"Status":row["Status"],"Detail":row["Detail"]})).collect();
                    reference_grid(
                        ui,
                        &rows,
                        "startup-report",
                        &["Producer", "Interval", "Period", "Status", "Detail"],
                        false,
                    );
                }
            }
            "signalMonitoring" if reference => {
                if let Some(value) = s.result.document::<Value>("signalMonitoring") {
                    report_footer(
                        ui,
                        "signal-monitoring",
                        45.,
                        &format!(
                            "Captured {} for {}. Only included rows with Monitor enabled are shown. This does not gate workflow admission.",
                            value["CapturedAtUtc"], value["ContractId"]
                        ),
                    );
                    let rows:Vec<_>=value["Rows"].as_array().into_iter().flatten().map(|row|json!({"AssignmentId":row["AssignmentId"],"AssignmentRevision":row["AssignmentRevision"],"Metric":row["Observation"]["Metric"],"Interval":row["Observation"]["SignalKey"]["TimeFrame"],"IsRequired":row["IsRequired"],"MaximumAgeSeconds":row["MaximumAgeSeconds"],"Availability":row["Observation"]["Availability"],"IsWarm":row["Observation"]["IsWarm"],"MarketDataAsOfUtc":row["Observation"]["MarketDataAsOfUtc"]})).collect();
                    reference_grid(
                        ui,
                        &rows,
                        "signal-monitoring",
                        &[
                            "AssignmentId",
                            "AssignmentRevision",
                            "Metric",
                            "Interval",
                            "IsRequired",
                            "MaximumAgeSeconds",
                            "Availability",
                            "IsWarm",
                            "MarketDataAsOfUtc",
                        ],
                        false,
                    );
                }
            }
            "legacy" if reference => {
                let rows = s
                    .result
                    .document::<Value>("legacyVersions")
                    .and_then(|v| v.as_array().cloned())
                    .unwrap_or_default();
                if self.legacy_id.is_empty() {
                    self.legacy_id = rows
                        .first()
                        .and_then(|v| v["Id"].as_str())
                        .unwrap_or("")
                        .into();
                }
                ui.horizontal(|ui| {
                    if ui
                        .add_enabled(
                            enabled
                                && s.result
                                    .context
                                    .get("legacyOffset")
                                    .is_some_and(|v| v != "0"),
                            egui::Button::new("Previous"),
                        )
                        .clicked()
                    {
                        *action = Some("legacy-prev");
                    }
                    if ui
                        .add_enabled(
                            enabled
                                && s.result
                                    .context
                                    .get("legacyHasMore")
                                    .is_some_and(|v| v == "True"),
                            egui::Button::new("Next"),
                        )
                        .clicked()
                    {
                        *action = Some("legacy-next");
                    }
                    if ui
                        .add_enabled(
                            enabled && !self.editing && !self.legacy_id.is_empty(),
                            egui::Button::new("Create exact draft"),
                        )
                        .clicked()
                    {
                        *action = Some("legacy-preview");
                        self.last_action = "legacy-import".into();
                    }
                });
                let mut payload = rows
                    .iter()
                    .find(|row| row["Id"] == self.legacy_id)
                    .and_then(|row| row["PayloadJson"].as_str())
                    .unwrap_or("")
                    .to_owned();
                egui::TopBottomPanel::bottom("reference-legacy-payload")
                    .exact_height(180.)
                    .show_inside(ui, |ui| {
                        ui.add_sized(
                            [ui.available_width(), 180.],
                            egui::TextEdit::multiline(&mut payload)
                                .interactive(false)
                                .font(egui::TextStyle::Monospace),
                        );
                    });
                let display:Vec<_>=rows.iter().map(|row|json!({"Id":row["Id"],"Kind":row["Reference"]["Kind"],"SetId":row["Reference"]["SetId"],"Version":row["Reference"]["Version"],"SchemaVersion":row["SchemaVersion"],"Status":row["Status"],"PayloadSha256":row["Reference"]["PayloadSha256"],"Codec":row["Reference"]["Codec"]})).collect();
                if let Some(index) = reference_grid(
                    ui,
                    &display,
                    "legacy-versions",
                    &[
                        "Kind",
                        "SetId",
                        "Version",
                        "SchemaVersion",
                        "Status",
                        "PayloadSha256",
                        "Codec",
                    ],
                    false,
                ) {
                    self.legacy_id = rows[index]["Id"].as_str().unwrap_or("").into();
                }
            }
            "legacy" => {
                if let Some(v) = s
                    .result
                    .document::<Value>("legacyVersions")
                    .and_then(|x| x.as_array().cloned())
                {
                    egui::ScrollArea::vertical()
                        .id_salt("legacy-parameters")
                        .max_height(160.)
                        .show_rows(ui, 24., v.len(), |ui, range| {
                            for i in range {
                                let row = &v[i];
                                let id = row["Id"].as_str().unwrap_or("");
                                if ui.selectable_label(self.legacy_id == id, id).clicked() {
                                    self.legacy_id = id.into();
                                }
                                if self.legacy_id == id {
                                    ui.trading_label(row["PayloadJson"].as_str().unwrap_or(""));
                                }
                            }
                        });
                    ui.horizontal_wrapped(|ui| {
                        button(
                            ui,
                            c,
                            strings::PARAM_PREVIOUS,
                            enabled,
                            "legacy-prev",
                            action,
                        );
                        button(
                            ui,
                            c,
                            strings::PARAM_NEXT,
                            enabled
                                && s.result
                                    .context
                                    .get("legacyHasMore")
                                    .is_some_and(|x| x == "True"),
                            "legacy-next",
                            action,
                        );
                        button(
                            ui,
                            c,
                            strings::PARAM_LEGACY_PREVIEW,
                            enabled && !self.legacy_id.is_empty(),
                            "legacy-preview",
                            action,
                        );
                        if ui
                            .add_enabled(
                                enabled
                                    && !self.editing
                                    && s.result.documents.contains_key("legacyPreview")
                                    && s.result.context.get("legacyPreviewId")
                                        == Some(&self.legacy_id),
                                egui::Button::new(c.text(strings::PARAM_LEGACY_CREATE)),
                            )
                            .clicked()
                        {
                            self.confirmation = Some("legacy-create".into());
                            self.confirm_generation = s.generation;
                        }
                    });
                    if let Some(preview) = s.result.document::<Value>("legacyPreview") {
                        read_value(ui, &preview, "legacy-preview");
                    }
                }
            }
            key => {
                if let Some(value) = s.result.document::<Value>(key).or_else(|| {
                    s.result
                        .context
                        .get(key)
                        .map(|text| Value::String(text.clone()))
                }) {
                    if key == "payload" || key == "schema" {
                        egui::ScrollArea::both()
                            .id_salt(("parameter-exact", key))
                            .show(ui, |ui| {
                                let mut text = value.as_str().unwrap_or("").to_owned();
                                ui.add_sized(
                                    ui.available_size(),
                                    egui::TextEdit::multiline(&mut text)
                                        .interactive(false)
                                        .font(egui::TextStyle::Monospace),
                                );
                            });
                    } else {
                        if reference {
                            reference_table(ui, &value, key);
                        } else {
                            read_value(ui, &value, key);
                        }
                    }
                }
            }
        }
    }
}
/// Decode the requested named managed document into a JSON-shaped editing/display value, using the local empty-value fallback on failure.
fn parse(result: &crate::contract_editor::ContractResult, key: &str) -> Value {
    result.document(key).unwrap_or(json!([]))
}
/// Render a parameter value recursively as read-only data with a stable widget identity.
fn read_value(ui: &mut egui::Ui, value: &Value, id: &str) {
    read_json(ui, &value.to_string(), id);
}
/// Draw a localized eligible button and store its action key when clicked.
fn button(
    ui: &mut egui::Ui,
    c: &Catalog,
    key: &str,
    enabled: bool,
    intent: &'static str,
    action: &mut Option<&'static str>,
) {
    if ui
        .add_enabled(enabled, egui::Button::new(c.text(key)))
        .clicked()
    {
        *action = Some(intent);
    }
}
/// Render parameter/component rows, preserving incomplete numeric text separately from the structured draft.
fn edit_rows(
    ui: &mut egui::Ui,
    data: &mut Value,
    kind: &str,
    editable: bool,
    s: &ContractPublication,
    c: &Catalog,
    buffers: &mut BTreeMap<String, String>,
) {
    let Some(rows) = data.as_array_mut() else {
        return;
    };
    let mut remove = None;
    egui::ScrollArea::both()
        .id_salt(("parameter-rows", kind))
        .max_height(310.)
        .show_rows(ui, 36., rows.len(), |ui, range| {
            for i in range {
                ui.push_id((kind, i), |ui| {
                    ui.horizontal(|ui| {
                        if let Some(object) = rows[i].as_object_mut() {
                            for (key, value) in object.iter_mut().filter(|(key, _)| {
                                key.as_str() != "RequirementId" && key.as_str() != "IsRequired"
                            }) {
                                ui.vertical(|ui| {
                                    ui.trading_caption(c.text(&format!("param_field_{}", key)));
                                    ui.add_enabled_ui(editable, |ui| {
                                        let choice_key = match key.as_str() {
                                            "Metric" => Some(if kind == "signals" {
                                                "signalMetric"
                                            } else {
                                                "observationMetric"
                                            }),
                                            "TimeFrame" => Some("timeframe"),
                                            _ => None,
                                        };
                                        if let Some(choice) = choice_key {
                                            let ids = s
                                                .result
                                                .choices
                                                .get(&format!("{}Ids", choice))
                                                .or_else(|| {
                                                    if choice == "timeframe" {
                                                        s.result.choices.get("timeframes")
                                                    } else {
                                                        None
                                                    }
                                                });
                                            let names =
                                                s.result.choices.get(&format!("{}Names", choice));
                                            let current = value.to_string();
                                            let label = ids
                                                .and_then(|ids| {
                                                    ids.iter().position(|v| v == &current)
                                                })
                                                .and_then(|index| {
                                                    names.and_then(|names| names.get(index))
                                                })
                                                .cloned()
                                                .unwrap_or(current);
                                            egui::ComboBox::from_id_salt(key)
                                                .width(130.)
                                                .selected_text(label)
                                                .show_ui(ui, |ui| {
                                                    if let (Some(ids), Some(names)) = (ids, names) {
                                                        for (id, name) in ids.iter().zip(names) {
                                                            if ui
                                                                .selectable_label(
                                                                    value.to_string() == *id,
                                                                    name,
                                                                )
                                                                .clicked()
                                                            {
                                                                if let Ok(number) =
                                                                    id.parse::<i64>()
                                                                {
                                                                    *value = json!(number);
                                                                }
                                                            }
                                                        }
                                                    }
                                                });
                                        } else if let Some(boolean) = value.as_bool() {
                                            let mut b = boolean;
                                            if ui
                                                .add(crate::presentation::checkbox(&mut b, ""))
                                                .changed()
                                            {
                                                *value = json!(b);
                                            }
                                        } else {
                                            let text = buffers
                                                .entry(format!("{kind}/{i}/{key}"))
                                                .or_insert_with(|| {
                                                    value
                                                        .as_str()
                                                        .map(str::to_owned)
                                                        .unwrap_or_else(|| value.to_string())
                                                });
                                            let response = ui.add(
                                                egui::TextEdit::singleline(text)
                                                    .desired_width(100.),
                                            );
                                            if response.changed() {
                                                if value.is_string() {
                                                    *value = json!(text.clone());
                                                } else if let Ok(parsed) =
                                                    serde_json::from_str::<Value>(text)
                                                {
                                                    if parsed.is_number() {
                                                        *value = parsed;
                                                    }
                                                }
                                            }
                                        }
                                    });
                                });
                            }
                            if ui
                                .add_enabled(
                                    editable,
                                    egui::Button::new(c.text(strings::PARAM_REMOVE_ROW)),
                                )
                                .clicked()
                            {
                                remove = Some(i);
                            }
                        }
                    });
                });
            }
        });
    if let Some(i) = remove {
        rows.remove(i);
        buffers.clear();
    }
    if ui
        .add_enabled(editable, egui::Button::new(c.text(strings::PARAM_ADD_ROW)))
        .clicked()
    {
        rows.push(match kind{"signals"=>json!({"RequirementId":"00000000-0000-0000-0000-000000000000","Enabled":true,"Metric":0,"TimeFrame":0,"PeriodLength":0,"MaximumAgeSeconds":0,"PrepareAtStartup":true,"Monitor":true}),"observations"=>json!({"RequirementId":"00000000-0000-0000-0000-000000000000","Enabled":true,"Metric":0,"MaximumAgeSeconds":0,"PrepareAtStartup":true,"Monitor":true}),_=>json!({"TimeFrame":0,"Weight":1,"MaximumAgeSeconds":0,"IsRequired":true})});
    }
}
/// Parse evidence text into a read-only tree when possible, otherwise show the original text.
fn read_json(ui: &mut egui::Ui, text: &str, salt: &str) {
    if let Ok(value) = serde_json::from_str::<Value>(text) {
        egui::ScrollArea::both()
            .id_salt(("parameter-report", salt))
            .show(ui, |ui| {
                json_node(ui, salt, &value);
            });
    } else {
        ui.trading_label(text);
    }
}
/// Render a nested JSON object/array/scalar using its full path as stable widget identity.
fn json_node(ui: &mut egui::Ui, path: &str, value: &Value) {
    match value {
        Value::Object(fields) => {
            for (k, v) in fields {
                if v.is_array() || v.is_object() {
                    egui::CollapsingHeader::new(k)
                        .id_salt((path, k))
                        .show(ui, |ui| {
                            json_node(ui, &format!("{path}/{k}"), v);
                        });
                } else {
                    ui.horizontal(|ui| {
                        ui.trading_label(k);
                        ui.trading_label(
                            v.as_str()
                                .map(str::to_owned)
                                .unwrap_or_else(|| v.to_string()),
                        );
                    });
                }
            }
        }
        Value::Array(rows) => {
            for (i, v) in rows.iter().enumerate() {
                egui::CollapsingHeader::new(i.to_string())
                    .id_salt((path, i))
                    .show(ui, |ui| {
                        json_node(ui, &format!("{path}/{i}"), v);
                    });
            }
        }
        _ => {
            ui.trading_label(value.to_string());
        }
    }
}

/// Render the unchanged WinForms requirements grid, retaining exact JSON property identities for saves.
fn edit_reference_rows(
    ui: &mut egui::Ui,
    data: &mut Value,
    kind: &str,
    editable: bool,
    s: &ContractPublication,
    buffers: &mut BTreeMap<String, String>,
) {
    let Some(rows) = data.as_array_mut() else {
        return;
    };
    let signal = kind == "signals";
    let columns: Vec<(&str, &str)> = if signal {
        vec![
            ("Enabled", "Included"),
            ("Metric", "Metric"),
            ("TimeFrame", "Timeframe"),
            ("PeriodLength", "Period length"),
            ("MaximumAgeSeconds", "Maximum age (seconds)"),
            ("PrepareAtStartup", "Prepare at startup"),
            ("Monitor", "Monitor"),
        ]
    } else {
        vec![
            ("Enabled", "Included"),
            ("Metric", "Observation"),
            ("MaximumAgeSeconds", "Maximum age (seconds)"),
            ("PrepareAtStartup", "Prepare at startup"),
            ("Monitor", "Monitor"),
        ]
    };
    let mut table = egui_extras::TableBuilder::new(ui)
        .id_salt(("reference-requirements", kind))
        .resizable(true);
    for _ in &columns {
        table = table.column(egui_extras::Column::remainder().at_least(45.));
    }
    table
        .header(42., |mut header| {
            for (_, label) in &columns {
                header.col(|ui| {
                    ui.label(egui::RichText::new(*label).color(egui::Color32::WHITE));
                });
            }
        })
        .body(|body| {
            body.rows(25., rows.len(), |mut row| {
                let index = row.index();
                for (key, _) in &columns {
                    row.col(|ui| {
                        let Some(value) = rows[index].get_mut(*key) else {
                            return;
                        };
                        ui.add_enabled_ui(editable, |ui| {
                            if let Some(mut checked) = value.as_bool() {
                                if ui.checkbox(&mut checked, "").changed() {
                                    *value = json!(checked);
                                }
                                return;
                            }
                            if matches!(*key, "Metric" | "TimeFrame") {
                                let choice = if *key == "TimeFrame" {
                                    "timeframe"
                                } else if signal {
                                    "signalMetric"
                                } else {
                                    "observationMetric"
                                };
                                let ids =
                                    s.result.choices.get(&format!("{choice}Ids")).or_else(|| {
                                        if *key == "TimeFrame" {
                                            s.result.choices.get("timeframes")
                                        } else {
                                            None
                                        }
                                    });
                                let names =
                                    s.result.choices.get(&format!("{choice}Names")).or_else(|| {
                                        if *key == "TimeFrame" {
                                            s.result.choices.get("timeframeNames")
                                        } else {
                                            None
                                        }
                                    });
                                let current = value
                                    .as_str()
                                    .map(str::to_owned)
                                    .unwrap_or_else(|| value.to_string());
                                let label = ids
                                    .and_then(|ids| ids.iter().position(|id| id == &current))
                                    .and_then(|index| names.and_then(|names| names.get(index)))
                                    .unwrap_or(&current);
                                egui::ComboBox::from_id_salt((kind, index, *key))
                                    .width(ui.available_width())
                                    .selected_text(label)
                                    .show_ui(ui, |ui| {
                                        if let (Some(ids), Some(names)) = (ids, names) {
                                            for (id, name) in ids.iter().zip(names) {
                                                if ui
                                                    .selectable_label(current == *id, name)
                                                    .clicked()
                                                {
                                                    *value = serde_json::from_str(id)
                                                        .unwrap_or_else(|_| json!(id));
                                                }
                                            }
                                        }
                                    });
                            } else {
                                let text = buffers
                                    .entry(format!("{kind}/{index}/{key}"))
                                    .or_insert_with(|| value.to_string());
                                if ui
                                    .add(
                                        egui::TextEdit::singleline(text)
                                            .desired_width(ui.available_width()),
                                    )
                                    .changed()
                                {
                                    if let Ok(number) = serde_json::from_str::<Value>(text) {
                                        if number.is_number() {
                                            *value = number;
                                        }
                                    }
                                }
                            }
                        });
                    });
                }
            })
        });
}

/// Preserve read-only tabular reference inspections instead of flattening them into one JSON editor.
fn reference_table(ui: &mut egui::Ui, value: &Value, id: &str) {
    let rows = value
        .as_array()
        .or_else(|| value.get("Operations").and_then(Value::as_array))
        .or_else(|| value.get("Rows").and_then(Value::as_array))
        .or_else(|| value.get("Steps").and_then(Value::as_array));
    let Some(rows) = rows else {
        read_value(ui, value, id);
        return;
    };
    let columns: Vec<_> = rows
        .first()
        .and_then(Value::as_object)
        .map(|row| row.keys().cloned().collect())
        .unwrap_or_default();
    if columns.is_empty() {
        return;
    }
    egui::ScrollArea::horizontal()
        .id_salt(("reference-table-scroll", id))
        .show(ui, |ui| {
            let mut table = egui_extras::TableBuilder::new(ui)
                .id_salt(("reference-table", id))
                .resizable(true);
            for _ in &columns {
                table = table.column(egui_extras::Column::initial(150.).at_least(65.));
            }
            table
                .header(30., |mut header| {
                    for column in &columns {
                        header.col(|ui| {
                            ui.label(egui::RichText::new(column).color(egui::Color32::WHITE));
                        });
                    }
                })
                .body(|body| {
                    body.rows(23., rows.len(), |mut row| {
                        let item = &rows[row.index()];
                        for column in &columns {
                            row.col(|ui| {
                                let value = &item[column];
                                let text = value.as_str().map(str::to_owned).unwrap_or_else(|| {
                                    if value.is_null() {
                                        String::new()
                                    } else {
                                        value.to_string()
                                    }
                                });
                                ui.add(
                                    egui::Label::new(
                                        egui::RichText::new(text).color(egui::Color32::WHITE),
                                    )
                                    .truncate(),
                                );
                            });
                        }
                    })
                });
        });
}

/// Render the original read-only report grid; activation is double-click for startup evidence.
fn reference_grid(
    ui: &mut egui::Ui,
    rows: &[Value],
    id: &str,
    columns: &[&str],
    double_click: bool,
) -> Option<usize> {
    let mut selected = None;
    egui::ScrollArea::horizontal()
        .id_salt(("report-scroll", id))
        .show(ui, |ui| {
            let width = (ui.available_width() / columns.len().max(1) as f32).max(90.);
            let mut table = egui_extras::TableBuilder::new(ui)
                .id_salt(("report", id))
                .resizable(true);
            for _ in columns {
                table = table.column(egui_extras::Column::initial(width).at_least(60.));
            }
            table
                .header(30., |mut header| {
                    for column in columns {
                        header.col(|ui| {
                            ui.label(egui::RichText::new(*column).color(egui::Color32::WHITE));
                        });
                    }
                })
                .body(|body| {
                    body.rows(24., rows.len(), |mut row| {
                        let index = row.index();
                        for column in columns {
                            row.col(|ui| {
                                let value = &rows[index][*column];
                                let text = value.as_str().map(str::to_owned).unwrap_or_else(|| {
                                    if value.is_null() {
                                        String::new()
                                    } else {
                                        value.to_string()
                                    }
                                });
                                let response = ui.add(
                                    egui::Label::new(
                                        egui::RichText::new(text).color(egui::Color32::WHITE),
                                    )
                                    .truncate()
                                    .sense(egui::Sense::click()),
                                );
                                if if double_click {
                                    response.double_clicked()
                                } else {
                                    response.clicked()
                                } {
                                    selected = Some(index);
                                }
                            });
                        }
                    })
                });
        });
    selected
}

/// Edit interval rows with the source grid's three columns, blank insertion row, and Delete key.
fn edit_interval_grid(
    ui: &mut egui::Ui,
    data: &mut Value,
    editable: bool,
    s: &ContractPublication,
    buffers: &mut BTreeMap<String, String>,
) {
    let Some(rows) = data.as_array_mut() else {
        return;
    };
    let mut appended = json!({"TimeFrame":0,"Weight":1.0,"MaximumAgeSeconds":0,"IsRequired":true});
    let mut insert = false;
    let mut remove = None;
    egui_extras::TableBuilder::new(ui)
        .id_salt("reference-intervals")
        .column(egui_extras::Column::remainder())
        .column(egui_extras::Column::exact(150.))
        .column(egui_extras::Column::exact(170.))
        .header(30., |mut header| {
            for caption in ["Interval", "Relative weight", "Max age (seconds)"] {
                header.col(|ui| {
                    ui.label(egui::RichText::new(caption).color(egui::Color32::WHITE));
                });
            }
        })
        .body(|body| {
            body.rows(28., rows.len() + usize::from(editable), |mut row| {
                let index = row.index();
                let existing = index < rows.len();
                let value = if existing {
                    &mut rows[index]
                } else {
                    &mut appended
                };
                row.col(|ui| {
                    ui.add_enabled_ui(editable, |ui| {
                        let ids = s
                            .result
                            .choices
                            .get("timeframes")
                            .cloned()
                            .unwrap_or_default();
                        let names = s
                            .result
                            .choices
                            .get("timeframeNames")
                            .cloned()
                            .unwrap_or_default();
                        let current = value["TimeFrame"].to_string();
                        let name = ids
                            .iter()
                            .position(|id| id == &current)
                            .and_then(|i| names.get(i))
                            .cloned()
                            .unwrap_or_else(|| {
                                if existing {
                                    current.clone()
                                } else {
                                    String::new()
                                }
                            });
                        let response = egui::ComboBox::from_id_salt(("interval-timeframe", index))
                            .selected_text(name)
                            .show_ui(ui, |ui| {
                                for (id, name) in ids.iter().zip(&names) {
                                    if ui.selectable_label(current == *id, name).clicked() {
                                        if let Ok(number) = id.parse::<i64>() {
                                            value["TimeFrame"] = json!(number);
                                            insert = !existing;
                                        }
                                    }
                                }
                            })
                            .response;
                        if existing
                            && response.has_focus()
                            && ui.input(|input| input.key_pressed(egui::Key::Delete))
                        {
                            remove = Some(index);
                        }
                    });
                });
                for key in ["Weight", "MaximumAgeSeconds"] {
                    row.col(|ui| {
                        let text = buffers
                            .entry(format!("intervals/{index}/{key}"))
                            .or_insert_with(|| {
                                if existing {
                                    value[key]
                                        .as_str()
                                        .map(str::to_owned)
                                        .unwrap_or_else(|| value[key].to_string())
                                } else {
                                    String::new()
                                }
                            });
                        let response = ui.add_enabled(
                            editable,
                            egui::TextEdit::singleline(text).desired_width(ui.available_width()),
                        );
                        if response.changed() {
                            if let Ok(number) = serde_json::from_str::<Value>(text) {
                                if number.is_number() {
                                    value[key] = number;
                                    insert = !existing;
                                }
                            }
                        }
                        if existing
                            && response.has_focus()
                            && text.is_empty()
                            && ui.input(|input| input.key_pressed(egui::Key::Delete))
                        {
                            remove = Some(index);
                        }
                    });
                }
            })
        });
    if let Some(index) = remove {
        rows.remove(index);
        buffers.retain(|key, _| !key.starts_with("intervals/"));
    }
    if insert {
        rows.push(appended);
    }
}

/// Reserve the original report information strip below its fill-sized grid.
fn report_footer(ui: &mut egui::Ui, id: &str, height: f32, text: &str) {
    egui::TopBottomPanel::bottom(egui::Id::new(("reference-report-footer", id)))
        .exact_height(height)
        .show_inside(ui, |ui| {
            ui.add(crate::presentation::label_widget(text).wrap());
        });
}
