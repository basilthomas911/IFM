//! Versioned strategy catalog administration. Schema-specific sections and templates edit a local JSON-shaped draft; published reference choices and managed commands control validation, lifecycle, and persistence.

use crate::presentation::TradingLabelUi as _;
use crate::{Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id};
use eframe::egui;
use serde_json::{Value, json};
use std::collections::BTreeMap;
/// Versioned strategy-definition browser/editor with local JSON draft and lifecycle confirmation.
#[derive(Default)]
pub struct StrategyCatalog {
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
    /// Local JSON-shaped strategy catalog draft.
    pub definition: Value,
    /// Whether a local edit session is active.
    pub editing: bool,
    /// Pending local confirmation action/dialog key.
    pub confirmation: Option<String>,
    /// Publication generation used for change detection and request correlation.
    generation: u64,
    /// Publication generation captured when confirmation was opened.
    confirm_generation: u64,
    /// Managed discriminator selecting the schema, field widget, row group, or patch segment behavior.
    kind: String,
    /// Whether catalog filtering includes all lifecycle states.
    show_all: bool,
    /// Selected new-definition template key.
    template: String,
    /// Selected editor section/tab key or index.
    section: String,
    /// Text buffers preserving incomplete numeric definition edits.
    numbers: BTreeMap<String, String>,
}
impl StrategyCatalog {
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns versioned strategy-definition browser/editor with local JSON draft and lifecycle confirmation.
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
        let matching = s.result.view == "strategy-catalog";
        if matching && !s.busy && self.pending.is_none() && self.generation != s.generation {
            let editing = s.result.context.get("editing").is_some_and(|v| v == "True");
            let selected = s
                .result
                .context
                .get("selectedId")
                .filter(|v| !v.is_empty())
                .cloned();
            if !(self.editing && editing && self.selected == selected) {
                self.definition = parse(s, "definition");
                self.numbers.clear();
            }
            self.editing = editing;
            self.selected = selected;
            self.kind = s.result.context.get("kind").cloned().unwrap_or_default();
            self.show_all = s.result.context.get("showAll").is_some_and(|v| v == "True");
            self.generation = s.generation;
        }
        let enabled = live && !s.busy && self.pending.is_none();
        let mut action = None;
        let mut fields = BTreeMap::<String, String>::new();
        let mut target = self.selected.clone();
        let invalid = self
            .numbers
            .values()
            .any(|v| serde_json::from_str::<Value>(v).map_or(true, |v| !v.is_number()));
        egui::SidePanel::left("catalog-navigation")
            .default_width(
                ctx.available_rect().width() * crate::presentation::CATALOG_NAVIGATION_RATIO,
            )
            .resizable(true)
            .show(ctx, |ui| {
                ui.heading(c.text("strategy_catalog"));
                if ui
                    .add_enabled(
                        enabled && !self.editing,
                        egui::Button::new(c.text("catalog_refresh")),
                    )
                    .clicked()
                {
                    action = Some("load");
                }
                if ui
                    .add_enabled(enabled, egui::Button::new(c.text("catalog_resume")))
                    .clicked()
                {
                    action = Some("catalog-resume");
                }
                ui.add_enabled_ui(enabled && !self.editing, |ui| {
                    let old = self.kind.clone();
                    egui::ComboBox::from_id_salt("catalog-kind")
                        .selected_text(&self.kind)
                        .show_ui(ui, |ui| {
                            if let Some(kinds) = s.result.choices.get("kind") {
                                for kind in kinds {
                                    ui.selectable_value(&mut self.kind, kind.clone(), kind);
                                }
                            }
                        });
                    if self.kind != old {
                        action = Some("catalog-section");
                    }
                    if ui
                        .add(crate::presentation::checkbox(&mut self.show_all, c.text("catalog_all")))
                        .changed()
                    {
                        action = Some("catalog-section");
                    }
                });
                if action == Some("catalog-section") {
                    fields.insert("kind".into(), self.kind.clone());
                    fields.insert("showAll".into(), self.show_all.to_string());
                }
                egui::ScrollArea::vertical()
                    .id_salt("catalog-versions")
                    .show_rows(ui, 28., s.result.rows.len(), |ui, range| {
                        for i in range {
                            let row = &s.result.rows[i];
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
                                        .selected(self.selected.as_deref() == Some(&row.id)),
                                )
                                .clicked()
                            {
                                target = Some(row.id.clone());
                                action = Some("catalog-select");
                            }
                        }
                    });
            });
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.trading_label(&s.status);
            ui.horizontal_wrapped(|ui| {
                let templates = parse(s, "templates");
                egui::ComboBox::from_id_salt("catalog-template")
                    .selected_text(if self.template.is_empty() {
                        c.text("catalog_custom")
                    } else {
                        &self.template
                    })
                    .show_ui(ui, |ui| {
                        ui.selectable_value(
                            &mut self.template,
                            String::new(),
                            c.text("catalog_custom"),
                        );
                        if let Some(rows) = templates.as_array() {
                            for t in rows {
                                let id = identity(&t["Key"]);
                                ui.selectable_value(
                                    &mut self.template,
                                    id,
                                    t["Name"].as_str().unwrap_or(""),
                                );
                            }
                        }
                    });
                if ui
                    .add_enabled(
                        enabled && matching && !self.editing,
                        egui::Button::new(c.text("catalog_add")),
                    )
                    .clicked()
                {
                    fields.insert("template".into(), self.template.clone());
                    action = Some("catalog-new");
                }
                if ui
                    .add_enabled(
                        enabled && !self.editing && self.selected.is_some(),
                        egui::Button::new(c.text("catalog_edit")),
                    )
                    .clicked()
                {
                    action = Some("catalog-edit");
                }
                if ui
                    .add_enabled(
                        enabled && self.editing && !invalid,
                        egui::Button::new(c.text("catalog_save")),
                    )
                    .clicked()
                {
                    fields.insert("definition".into(), self.definition.to_string());
                    action = Some("catalog-save");
                }
                if ui
                    .add_enabled(
                        enabled && self.editing,
                        egui::Button::new(c.text("catalog_discard")),
                    )
                    .clicked()
                {
                    action = Some("catalog-discard");
                }
                let status = self
                    .selected
                    .as_ref()
                    .and_then(|id| s.result.rows.iter().find(|r| &r.id == id))
                    .and_then(|r| r.fields.get("status"))
                    .map(String::as_str)
                    .unwrap_or("");
                for (intent, label, state) in [
                    ("catalog-publish", "catalog_publish", "Draft"),
                    ("catalog-retire", "catalog_retire", "Published"),
                ] {
                    if ui
                        .add_enabled(
                            enabled && !self.editing && status == state,
                            egui::Button::new(c.text(label)),
                        )
                        .clicked()
                    {
                        self.confirmation = Some(intent.into());
                        self.confirm_generation = s.generation;
                    }
                }
            });
            if invalid {
                ui.trading_colored_label(egui::Color32::YELLOW, c.text("catalog_number_error"));
            }
            if self.definition.is_null() {
                ui.trading_caption(c.text("catalog_select_notice"));
                return;
            }
            let sections = sections(&self.kind);
            if !sections.contains(&self.section.as_str()) {
                self.section = "Definition".into();
            }
            ui.horizontal_wrapped(|ui| {
                for section in &sections {
                    if crate::presentation::tab_label(ui,
                            &self.section == section,
                            c.text(&format!("catalog_section_{}", section)),
                        )
                        .clicked()
                    {
                        self.section = section.to_string();
                    }
                }
            });
            let refs = parse(s, "references");
            let products = parse(s, "products");
            egui::ScrollArea::vertical()
                .id_salt("catalog-fields")
                .show(ui, |ui| {
                    if self.section == "Definition" {
                        for key in [
                            "Code",
                            "Name",
                            "Description",
                            "Parent",
                            "Horizon",
                            "Side",
                            "Bias",
                            "PremiumMode",
                        ] {
                            if (key == "Parent"
                                && !matches!(
                                    self.kind.as_str(),
                                    "Variant" | "ParameterSet" | "Deployment"
                                ))
                                || (key == "Horizon" && self.kind != "Deployment")
                                || (matches!(key, "Side" | "Bias" | "PremiumMode")
                                    && self.kind != "Variant")
                            {
                                continue;
                            }
                            let mutable = enabled
                                && self.editing
                                && !(key == "Code"
                                    && self.definition["Key"]["Version"].as_u64().unwrap_or(0) > 1);
                            if key == "Horizon" {
                                let horizons = parse(s, "horizons");
                                egui::ComboBox::from_id_salt("catalog-horizon")
                                    .selected_text(
                                        horizons
                                            .as_array()
                                            .and_then(|rows| {
                                                rows.iter()
                                                    .find(|x| x["Value"] == self.definition[key])
                                            })
                                            .and_then(|x| x["Name"].as_str())
                                            .unwrap_or(""),
                                    )
                                    .show_ui(ui, |ui| {
                                        ui.add_enabled_ui(mutable, |ui| {
                                            if let Some(rows) = horizons.as_array() {
                                                for row in rows {
                                                    if ui
                                                        .selectable_label(
                                                            self.definition[key] == row["Value"],
                                                            row["Name"].as_str().unwrap_or(""),
                                                        )
                                                        .clicked()
                                                    {
                                                        self.definition[key] = row["Value"].clone();
                                                    }
                                                }
                                            }
                                        });
                                    });
                                continue;
                            }
                            let mut value = self.definition[key].take();
                            edit(
                                ui,
                                key,
                                &mut value,
                                key,
                                mutable,
                                c,
                                &refs,
                                &products,
                                &mut self.numbers,
                            );
                            self.definition[key] = value;
                        }
                    } else {
                        let key = self.section.clone();
                        let mut value = self.definition[&key].take();
                        edit(
                            ui,
                            &key,
                            &mut value,
                            &key,
                            enabled && self.editing,
                            c,
                            &refs,
                            &products,
                            &mut self.numbers,
                        );
                        self.definition[key] = value;
                    }
                });
        });
        if let Some(intent) = self.confirmation.clone() {
            egui::Window::new(crate::presentation::dialog_title(c.text("catalog_confirm")))
                .collapsible(false)
                .show(ctx, |ui| {
                    ui.trading_label(self.selected.as_deref().unwrap_or(""));
                    ui.trading_caption(c.text(if intent == "catalog-publish" {
                        "catalog_publish"
                    } else {
                        "catalog_retire"
                    }));
                    if self.confirm_generation != s.generation {
                        ui.trading_caption(c.text("catalog_stale"));
                    }
                    if ui
                        .add_enabled(
                            enabled && self.confirm_generation == s.generation,
                            egui::Button::new(c.text("catalog_confirm")),
                        )
                        .clicked()
                    {
                        action = Some(if intent == "catalog-publish" {
                            "catalog-publish"
                        } else {
                            "catalog-retire"
                        });
                        self.confirmation = None;
                    }
                    if ui.button(c.text("catalog_cancel")).clicked() {
                        self.confirmation = None;
                    }
                });
        }
        action.map(|intent|{let request_id=new_request_id();self.pending=Some(request_id.clone());json!({"schema":1,"requestId":request_id,"view":"strategy-catalog","intent":intent,"expectedGeneration":s.generation,"originalId":target,"fields":fields}).to_string()})
    }
}
/// Decode the requested named managed document into a JSON-shaped editing/display value, using the local empty-value fallback on failure.
fn parse(s: &ContractPublication, key: &str) -> Value {
    s.result
        .context
        .get(key)
        .and_then(|x| serde_json::from_str(x).ok())
        .unwrap_or(Value::Null)
}
/// Build a stable display/selection identity from a published strategy-catalog key.
fn identity(key: &Value) -> String {
    let kind = key["Kind"]
        .as_u64()
        .and_then(|k| {
            [
                "",
                "Family",
                "Strategy",
                "Structure",
                "Variant",
                "ParameterSchema",
                "ParameterSet",
                "Deployment",
            ]
            .get(k as usize)
        })
        .copied()
        .unwrap_or("");
    format!(
        "{}:{}:{}",
        kind,
        key["Id"].as_str().unwrap_or(""),
        key["Version"]
    )
}
/// Return the editor section names appropriate to the selected strategy-catalog definition kind.
fn sections(kind: &str) -> Vec<&'static str> {
    let mut rows = vec!["Definition"];
    rows.extend(match kind {
        "Strategy" => vec!["Families", "Structures"],
        "Structure" => vec!["ExpiryGroups", "Legs"],
        "Variant" => vec!["VariantLegs"],
        "Deployment" => vec![
            "Variants",
            "Products",
            "PipelineParameters",
            "Parameters",
            "LegacyFamilies",
        ],
        _ => vec![],
    });
    if kind != "Family" {
        rows.push("Capabilities");
    }
    if kind != "Family" {
        rows.push("Settings");
    }
    rows
}
/// Render schema-aware strategy definition editors using published references and separate numeric text buffers.
fn edit(
    ui: &mut egui::Ui,
    key: &str,
    value: &mut Value,
    path: &str,
    enabled: bool,
    c: &Catalog,
    refs: &Value,
    products: &Value,
    numbers: &mut BTreeMap<String, String>,
) {
    ui.push_id(path, |ui| {
        let field_key = format!("catalog_field_{}", key);
        let label = ui.trading_label(c.strings.get(&field_key).map(String::as_str).unwrap_or(key));
        if key == "Products" && value.is_object() {
            egui::ComboBox::from_id_salt("product")
                .selected_text(value["Symbol"].as_str().unwrap_or(""))
                .show_ui(ui, |ui| {
                    ui.add_enabled_ui(enabled, |ui| {
                        if let Some(rows) = products.as_array() {
                            for row in rows {
                                if ui
                                    .selectable_label(
                                        *value == *row,
                                        format!(
                                            "{} - {} - {} [{}]",
                                            row["Symbol"].as_str().unwrap_or(""),
                                            row["Exchange"].as_str().unwrap_or(""),
                                            row["Currency"].as_str().unwrap_or(""),
                                            row["ProductId"]
                                        ),
                                    )
                                    .clicked()
                                {
                                    *value = row.clone();
                                }
                            }
                        }
                    });
                });
            return;
        }
        let reference_kind = match key {
            "Parent" => None,
            "Families" => Some(1),
            "Structures" => Some(3),
            "Variants" => Some(4),
            "ParameterSet" => Some(6),
            _ => None,
        };
        if reference_kind.is_some() && value.is_object() || key == "Parent" || key == "ParameterSet"
        {
            egui::ComboBox::from_id_salt("reference")
                .selected_text(if value.is_null() {
                    c.text("catalog_none").into()
                } else {
                    identity(value)
                })
                .show_ui(ui, |ui| {
                    ui.add_enabled_ui(enabled, |ui| {
                        if let Some(rows) = refs.as_array() {
                            for row in rows {
                                if reference_kind.is_none()
                                    || row["Key"]["Kind"].as_i64() == reference_kind
                                {
                                    if ui
                                        .selectable_label(
                                            *value == row["Key"],
                                            format!(
                                                "{} v{} ({})",
                                                row["Name"].as_str().unwrap_or(""),
                                                row["Key"]["Version"],
                                                row["Status"]
                                            ),
                                        )
                                        .clicked()
                                    {
                                        *value = row["Key"].clone();
                                    }
                                }
                            }
                        }
                    });
                });
            return;
        }
        match value {
            Value::Object(map) => {
                let dynamic = path.starts_with("Settings");
                let mut remove = None;
                for (k, v) in map.iter_mut() {
                    edit(
                        ui,
                        k,
                        v,
                        &format!("{path}/{k}"),
                        enabled,
                        c,
                        refs,
                        products,
                        numbers,
                    );
                    if dynamic
                        && ui
                            .add_enabled(
                                enabled,
                                egui::Button::new(c.text("catalog_remove_property")),
                            )
                            .clicked()
                    {
                        remove = Some(k.clone());
                    }
                }
                if let Some(k) = remove {
                    map.remove(&k);
                }
                if dynamic {
                    let id = ui.id().with("new-property");
                    let mut name = ui.data_mut(|d| d.get_temp::<String>(id).unwrap_or_default());
                    ui.add_enabled(
                        enabled,
                        egui::TextEdit::singleline(&mut name)
                            .hint_text(c.text("catalog_property_name")),
                    );
                    let type_id = id.with("type");
                    let mut kind = ui.data_mut(|d| {
                        d.get_temp::<String>(type_id)
                            .unwrap_or_else(|| "String".into())
                    });
                    egui::ComboBox::from_id_salt("property-type")
                        .selected_text(&kind)
                        .show_ui(ui, |ui| {
                            for t in ["String", "Number", "Boolean", "Object", "Array"] {
                                ui.selectable_value(&mut kind, t.into(), t);
                            }
                        });
                    if ui
                        .add_enabled(
                            enabled && !name.trim().is_empty() && !map.contains_key(&name),
                            egui::Button::new(c.text("catalog_add_property")),
                        )
                        .clicked()
                    {
                        map.insert(
                            name.clone(),
                            match kind.as_str() {
                                "Number" => json!(0),
                                "Boolean" => json!(false),
                                "Object" => json!({}),
                                "Array" => json!([]),
                                _ => json!(""),
                            },
                        );
                        name.clear();
                    }
                    ui.data_mut(|d| {
                        d.insert_temp(id, name);
                        d.insert_temp(type_id, kind);
                    });
                }
            }
            Value::Array(rows) => {
                let mut remove = None;
                for (i, row) in rows.iter_mut().enumerate() {
                    egui::CollapsingHeader::new(format!(
                        "{} {}",
                        c.text(&format!("catalog_field_{}", key)),
                        i + 1
                    ))
                    .id_salt(i)
                    .default_open(true)
                    .show(ui, |ui| {
                        edit(
                            ui,
                            key,
                            row,
                            &format!("{path}/{i}"),
                            enabled,
                            c,
                            refs,
                            products,
                            numbers,
                        );
                        if ui
                            .add_enabled(enabled, egui::Button::new(c.text("catalog_remove_row")))
                            .clicked()
                        {
                            remove = Some(i);
                        }
                    });
                }
                if let Some(i) = remove {
                    rows.remove(i);
                }
                if ui
                    .add_enabled(
                        enabled && rows.len() < 4096,
                        egui::Button::new(c.text("catalog_add_row")),
                    )
                    .clicked()
                {
                    rows.push(template(key, refs, products));
                }
            }
            Value::Bool(v) => {
                ui.add_enabled_ui(enabled, |ui| {
                    ui.add(crate::presentation::checkbox(v, "")).labelled_by(label.id);
                });
            }
            Value::Number(_) => {
                let text = numbers
                    .entry(path.into())
                    .or_insert_with(|| value.to_string());
                if ui
                    .add_enabled(enabled, egui::TextEdit::singleline(text))
                    .labelled_by(label.id)
                    .changed()
                {
                    if let Ok(number) = serde_json::from_str::<Value>(text) {
                        if number.is_number() {
                            *value = number;
                        }
                    }
                }
            }
            Value::String(v) => {
                ui.add_enabled(enabled, egui::TextEdit::singleline(v))
                    .labelled_by(label.id);
            }
            Value::Null => {
                let mut text = String::new();
                if ui
                    .add_enabled(enabled, egui::TextEdit::singleline(&mut text))
                    .labelled_by(label.id)
                    .changed()
                {
                    *value = Value::String(text);
                }
            }
        }
    });
}
/// Construct a local strategy-definition draft using the selected template and published reference/product choices.
fn template(key: &str, refs: &Value, products: &Value) -> Value {
    match key {
        "Families" | "Structures" | "Variants" => {
            let kind = match key {
                "Families" => 1,
                "Structures" => 3,
                _ => 4,
            };
            refs.as_array()
                .and_then(|rows| rows.iter().find(|x| x["Key"]["Kind"] == kind))
                .map(|x| x["Key"].clone())
                .unwrap_or(
                    json!({"Kind":kind,"Id":"00000000-0000-0000-0000-000000000000","Version":1}),
                )
        }
        "ExpiryGroups" => json!({"Key":"","AfterGroup":null}),
        "Legs" => {
            json!({"Key":"","InstrumentClass":"FuturesOption","Side":"Short","OptionRight":"Put","Ratio":1,"ExpiryGroup":""})
        }
        "VariantLegs" => json!({"LegKey":"","Side":"Short","Ratio":1}),
        "Capabilities" => json!({"Role":"","Code":"","Version":1}),
        "Products" => products
            .as_array()
            .and_then(|rows| rows.first())
            .cloned()
            .unwrap_or(json!({"ProductId":0,"Symbol":"","Exchange":"","Currency":""})),
        "PipelineParameters" => {
            json!({"Role":"","Kind":1,"Id":"00000000-0000-0000-0000-000000000000","Version":1,"Hash":""})
        }
        "Parameters" => {
            json!({"Role":"","ParameterSet":{"Kind":6,"Id":"00000000-0000-0000-0000-000000000000","Version":1}})
        }
        "LegacyFamilies" => json!({"Id":0,"Version":1}),
        _ => Value::String(String::new()),
    }
}
