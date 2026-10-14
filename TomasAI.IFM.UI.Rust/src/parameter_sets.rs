use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use std::collections::BTreeMap;
#[derive(Clone, Default, Deserialize, Serialize)]
pub struct Field {
    #[serde(rename = "Path")]
    pub path: String,
    #[serde(rename = "Value")]
    pub value: String,
    #[serde(rename = "Kind")]
    pub kind: u8,
    #[serde(rename = "Group")]
    pub group: String,
    #[serde(rename = "Name")]
    pub name: String,
}
#[derive(Default)]
pub struct ParameterSets {
    pub pending: Option<String>,
    generation: u64,
    pub selected: Option<String>,
    pub editing: bool,
    pub name: String,
    pub description: String,
    pub horizon: String,
    pub section: String,
    pub group: String,
    pub fields: Vec<Field>,
    pub signals: Value,
    pub observations: Value,
    pub intervals: Value,
    components: Value,
    component: String,
    pub confirmation: Option<String>,
    confirm_generation: u64,
    legacy_id: String,
    run_id: String,
    compare_id: String,
    numeric_text: BTreeMap<String, String>,
    last_action: String,
}
impl ParameterSets {
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
        let matching = s.result.view == "parameter-sets";
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
            self.components = parse(context, "components");
            if !keep_draft {
                self.numeric_text.clear();
                self.fields = context
                    .get("fields")
                    .and_then(|x| serde_json::from_str(x).ok())
                    .unwrap_or_default();
                self.signals = parse(context, "signals");
                self.observations = parse(context, "observations");
                self.intervals = parse(context, "intervals");
            }
        }
        if self.horizon.is_empty() {
            self.horizon = "Daily".into();
        }
        if self.section.is_empty() {
            self.section = "fields".into();
        }
        let enabled = live && !s.busy && self.pending.is_none();
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
        egui::SidePanel::left("parameter-navigation")
            .resizable(true)
            .default_width(
                (ctx.available_rect().width() * crate::presentation::PARAMETER_NAVIGATION_RATIO)
                    .max(280.0),
            )
            .show(ctx, |ui| {
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
                                            let code =
                                                component["ComponentCode"].as_str().unwrap_or("");
                                            if ui
                                                .add_enabled(
                                                    enabled && !self.editing,
                                                    egui::Button::new(
                                                        component["Name"].as_str().unwrap_or(code),
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
            });
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.trading_label(&s.status);
            if invalid_numbers {
                ui.trading_colored_label(
                    egui::Color32::LIGHT_RED,
                    c.text(strings::PARAM_INVALID_NUMBERS),
                );
            }
            ui.horizontal_wrapped(|ui| {
                ui.trading_label(c.text(strings::PARAM_HORIZON));
                egui::ComboBox::from_id_salt("new-parameter-horizon")
                    .selected_text(&self.horizon)
                    .show_ui(ui, |ui| {
                        for v in ["Daily", "Weekly", "Monthly"] {
                            ui.selectable_value(&mut self.horizon, v.into(), v);
                        }
                    });
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
                    let label = ui.trading_label(c.text(strings::PARAM_NAME));
                    ui.text_edit_singleline(&mut self.name)
                        .labelled_by(label.id);
                });
                ui.horizontal(|ui| {
                    let label = ui.trading_label(c.text(strings::PARAM_DESCRIPTION));
                    ui.text_edit_singleline(&mut self.description)
                        .labelled_by(label.id);
                });
            });
            ui.horizontal_wrapped(|ui| {
                for (key, label) in [
                    ("signals", strings::PARAM_SIGNALS),
                    ("observations", strings::PARAM_OBSERVATIONS),
                    ("fields", strings::PARAM_FIELDS),
                    ("intervals", strings::PARAM_INTERVALS),
                    ("validation", strings::PARAM_VALIDATION),
                ] {
                    if crate::presentation::tab_label(ui,self.section == key, c.text(label))
                        .clicked()
                    {
                        self.section = key.into();
                    }
                }
            });
            match self.section.as_str() {
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
                        &mut action,
                    );
                }
                "rename" => {
                    ui.trading_label(c.text(strings::PARAM_RENAME));
                    ui.text_edit_singleline(&mut self.name);
                    ui.text_edit_multiline(&mut self.description);
                    button(
                        ui,
                        c,
                        strings::PARAM_SAVE_METADATA,
                        enabled && saved,
                        "rename-parameter",
                        &mut action,
                    );
                }
                "assignment" => {
                    ui.trading_label(c.text(strings::PARAM_ASSIGNMENT_NOTICE));
                    if let Some(v) = s.result.context.get("assignment") {
                        read_json(ui, v, "assignment");
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
                    if let Some(v) = s
                        .result
                        .context
                        .get("comparisonVersions")
                        .and_then(|x| serde_json::from_str::<Value>(x).ok())
                        .and_then(|x| x.as_array().cloned())
                    {
                        egui::ComboBox::from_id_salt("parameter-comparison")
                            .selected_text(&self.compare_id)
                            .show_ui(ui, |ui| {
                                for row in v {
                                    let id = row["Id"].as_str().unwrap_or("");
                                    if ui
                                        .selectable_value(
                                            &mut self.compare_id,
                                            id.into(),
                                            format!("{} v{}", row["Name"], row["Version"]),
                                        )
                                        .changed()
                                    {
                                        extra.insert("compareId".into(), id.into());
                                        action = Some("compare-parameter");
                                    }
                                }
                            });
                    }
                    if let Some(value) = s.result.context.get("differences") {
                        read_json(ui, value, "differences");
                    }
                }
                "startupRuns" => {
                    if let Some(v) = s
                        .result
                        .context
                        .get("startupRuns")
                        .and_then(|x| serde_json::from_str::<Value>(x).ok())
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
                            &mut action,
                        );
                        button(
                            ui,
                            c,
                            strings::PARAM_SIGNAL_MONITORING,
                            enabled && !self.run_id.is_empty(),
                            "signal-monitoring",
                            &mut action,
                        );
                    }
                }
                "legacy" => {
                    if let Some(v) = s
                        .result
                        .context
                        .get("legacyVersions")
                        .and_then(|x| serde_json::from_str::<Value>(x).ok())
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
                                &mut action,
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
                                &mut action,
                            );
                            button(
                                ui,
                                c,
                                strings::PARAM_LEGACY_PREVIEW,
                                enabled && !self.legacy_id.is_empty(),
                                "legacy-preview",
                                &mut action,
                            );
                            if ui
                                .add_enabled(
                                    enabled
                                        && !self.editing
                                        && s.result.context.contains_key("legacyPreview")
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
                        if let Some(preview) = s.result.context.get("legacyPreview") {
                            read_json(ui, preview, "legacy-preview");
                        }
                    }
                }
                key => {
                    if let Some(value) = s.result.context.get(key) {
                        if key == "payload" || key == "schema" {
                            egui::ScrollArea::both()
                                .id_salt(("parameter-exact", key))
                                .show(ui, |ui| {
                                    ui.trading_label(value);
                                });
                        } else {
                            read_json(ui, value, key);
                        }
                    }
                }
            }
        });
        if let Some(intent) = self.confirmation.clone() {
            egui::Window::new(c.text(strings::PARAM_CONFIRM))
                .collapsible(false)
                .show(ctx, |ui| {
                    ui.trading_label(if intent == "legacy-create" {
                        &self.legacy_id
                    } else {
                        &self.name
                    });
                    ui.trading_label(c.text(&format!("param_action_{}", intent.replace('-', "_"))));
                    if self.confirm_generation != s.generation {
                        ui.trading_label(c.text(strings::PARAM_STALE));
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
        action.map(|intent|{self.section=match intent{"validate-parameter"=>"validation","schema-parameter"=>"schema","payload-parameter"=>"payload","compare-parameter"=>"compare","audit-parameter"=>"audit","assignment"|"assign-parameter"|"disable-assignment"=>"assignment","startup-preview"=>"startupPreview","startup-runs"=>"startupRuns","startup-report"=>"startupReport","signal-monitoring"=>"signalMonitoring","legacy-parameters"|"legacy-prev"|"legacy-next"|"legacy-preview"=>"legacy",_=>&self.section}.into();let id=new_request_id();self.pending=Some(id.clone());if ["validate-parameter","save-draft","intervals"].contains(&intent)&&self.editing{extra.insert("changes".into(),serde_json::to_string(&self.fields).unwrap());extra.insert("signals".into(),self.signals.to_string());extra.insert("observations".into(),self.observations.to_string());}extra.insert("name".into(),self.name.clone());extra.insert("description".into(),self.description.clone());extra.insert("horizon".into(),self.horizon.clone());extra.insert("intervals".into(),self.intervals.to_string());extra.insert("runId".into(),self.run_id.clone());extra.insert("legacyId".into(),self.legacy_id.clone());serde_json::json!({"schema":1,"requestId":id,"view":"parameter-sets","intent":intent,"expectedGeneration":s.generation,"originalId":target,"fields":extra}).to_string()})
    }
}
fn parse(context: &BTreeMap<String, String>, key: &str) -> Value {
    context
        .get(key)
        .and_then(|x| serde_json::from_str(x).ok())
        .unwrap_or(json!([]))
}
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
                                    ui.trading_label(c.text(&format!("param_field_{}", key)));
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
                                            if ui.checkbox(&mut b, "").changed() {
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
