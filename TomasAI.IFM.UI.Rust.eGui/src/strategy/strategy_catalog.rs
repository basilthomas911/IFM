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
    /// Tracks which original Reference command owns the edit session.
    pub changing: bool,
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
    /// Last automatic first-row selection; avoids retry loops when an exact-definition query fails.
    auto_selection: Option<String>,
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
        let ready = live && !s.busy && self.pending.is_none() && self.confirmation.is_none();
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
            add: true,
            change: self.selected.is_some(),
            remove: status == Some("Published"),
            save: self.editing,
        };
        crate::reference::show(ctx, 1, commands, |ui, command| {
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
        if reference && self.confirmation.is_some() {
            ui.disable();
        }
        let mut action = None;
        let mut fields = BTreeMap::<String, String>::new();
        let mut target = self.selected.clone();

        let invalid = self
            .numbers
            .values()
            .any(|v| serde_json::from_str::<Value>(v).map_or(true, |v| !v.is_number()));
        if reference && enabled {
            if matching && !self.editing && self.selected.is_none() {
                if let Some(row) = s.result.rows.first() {
                    if self.auto_selection.as_deref() != Some(&row.id) {
                        self.auto_selection = Some(row.id.clone());
                        target = Some(row.id.clone());
                        action = Some("catalog-select");
                    }
                }
            }
            if !matching {
                action = Some("load");
            }
            match command {
                Some("add") if !self.editing => {
                    self.changing = false;
                    fields.insert("template".into(), self.template.clone());
                    action = Some("catalog-new");
                }
                Some("change") if !self.editing => {
                    self.changing = true;
                    action = Some("catalog-edit");
                }
                Some("add" | "change") if self.editing && !invalid => {
                    fields.insert("definition".into(), self.definition.to_string());
                    action = Some("catalog-save");
                }
                Some("close") => action = Some("catalog-discard"),
                Some("remove") => {
                    self.confirmation = Some("catalog-retire".into());
                    self.confirm_generation = s.generation;
                }
                _ => {}
            }
        }

        if reference {
            egui::TopBottomPanel::top("reference-catalog-toolbar")
                .exact_height(44.)
                .show_inside(ui, |ui| {
                    ui.add_enabled_ui(enabled && !self.editing, |ui| {
                        ui.horizontal(|ui| {
                            ui.add(crate::presentation::caption_widget("Manage"));
                            let previous = self.kind.clone();
                            egui::ComboBox::from_id_salt("catalog-kind")
                                .width(175.)
                                .selected_text(&self.kind)
                                .show_ui(ui, |ui| {
                                    if let Some(kinds) = s.result.choices.get("kind") {
                                        for kind in kinds {
                                            if ui
                                                .selectable_value(
                                                    &mut self.kind,
                                                    kind.clone(),
                                                    kind,
                                                )
                                                .clicked()
                                            {
                                                ui.close();
                                            }
                                        }
                                    }
                                })
                                .response
                                .widget_info(|| {
                                    egui::WidgetInfo::labeled(
                                        egui::WidgetType::ComboBox,
                                        enabled && !self.editing,
                                        "Catalog section",
                                    )
                                });
                            ui.add(crate::presentation::caption_widget("Start from"));
                            egui::ComboBox::from_id_salt("catalog-template")
                                .width(230.)
                                .selected_text(if self.template.is_empty() {
                                    "Custom"
                                } else {
                                    &self.template
                                })
                                .show_ui(ui, |ui| {
                                    ui.selectable_value(
                                        &mut self.template,
                                        String::new(),
                                        "Custom",
                                    );
                                    if let Some(rows) = parse(s, "templates").as_array() {
                                        for row in rows {
                                            ui.selectable_value(
                                                &mut self.template,
                                                identity(&row["Key"]),
                                                row["Name"].as_str().unwrap_or(""),
                                            );
                                        }
                                    }
                                });
                            let draft = s
                                .result
                                .rows
                                .iter()
                                .find(|row| Some(&row.id) == self.selected.as_ref())
                                .and_then(|row| row.fields.get("status"))
                                .is_some_and(|status| status == "Draft");
                            if ui
                                .add_enabled(
                                    enabled && !self.editing && draft,
                                    egui::Button::new("Publish"),
                                )
                                .clicked()
                            {
                                self.confirmation = Some("catalog-publish".into());
                                self.confirm_generation = s.generation;
                            }
                            if ui
                                .add_enabled(enabled && !self.editing, egui::Button::new("Refresh"))
                                .clicked()
                            {
                                action = Some("load");
                            }
                            if ui
                                .add(crate::presentation::checkbox(
                                    &mut self.show_all,
                                    "Show all catalog data",
                                ))
                                .changed()
                                || previous != self.kind
                            {
                                fields.insert("kind".into(), self.kind.clone());
                                fields.insert("showAll".into(), self.show_all.to_string());
                                action = Some("catalog-section");
                            }
                        })
                    });
                });
        }
        egui::SidePanel::left("catalog-navigation")
            .default_width(ui.available_width() * crate::presentation::CATALOG_NAVIGATION_RATIO)
            .resizable(true)
            .show_inside(ui, |ui| {
                if !reference {
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
                            .add(crate::presentation::checkbox(
                                &mut self.show_all,
                                c.text("catalog_all"),
                            ))
                            .changed()
                        {
                            action = Some("catalog-section");
                        }
                    });
                    if action == Some("catalog-section") {
                        fields.insert("kind".into(), self.kind.clone());
                        fields.insert("showAll".into(), self.show_all.to_string());
                    }
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
                                    egui::Button::new(
                                        egui::RichText::new(label).color(egui::Color32::WHITE),
                                    )
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
        egui::CentralPanel::default().show_inside(ui, |ui| {
            ui.trading_label(&s.status);
            if !reference {
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
            }
            if invalid {
                ui.trading_colored_label(egui::Color32::YELLOW, c.text("catalog_number_error"));
            }
            if self.definition.is_null() {
                ui.trading_caption(c.text("catalog_select_notice"));
                return;
            }
            if reference {
                self.reference_details(ui, s, enabled);
                return;
            }
            let sections = sections(&self.kind);
            if !sections.contains(&self.section.as_str()) {
                self.section = "Definition".into();
            }
            ui.horizontal_wrapped(|ui| {
                for section in &sections {
                    if crate::presentation::tab_label(
                        ui,
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
                .show(&ctx, |ui| {
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
        action.map(|intent|{if matches!(intent,"load"|"catalog-section"|"catalog-discard"){self.auto_selection=None;}let request_id=new_request_id();self.pending=Some(request_id.clone());json!({"schema":1,"requestId":request_id,"view":"strategy-catalog","intent":intent,"expectedGeneration":s.generation,"originalId":target,"fields":fields}).to_string()})
    }
    /// Render the original catalog definition tabs and fields without standalone editor commands.
    fn reference_details(&mut self, ui: &mut egui::Ui, s: &ContractPublication, enabled: bool) {
        let mut tabs = vec![("Definition", "Definition")];
        match self.kind.as_str() {
            "Strategy" => tabs.extend([("Families", "Families"), ("Structures", "Structures")]),
            "Structure" => tabs.extend([("ExpiryGroups", "Expiry groups"), ("Legs", "Legs")]),
            "Variant" => tabs.extend([("Variant", "Variant"), ("VariantLegs", "Variant legs")]),
            "Deployment" => tabs.extend([
                ("Variants", "Variants"),
                ("Products", "Products"),
                ("PipelineParameters", "Pipeline profiles"),
                ("Parameters", "Parameter sets"),
                ("LegacyFamilies", "Legacy mappings"),
            ]),
            "ParameterSchema" => tabs.push(("Settings", "Parameter fields")),
            _ => {}
        }
        if self.kind != "Family" {
            tabs.push(("Capabilities", "Capabilities"));
        }
        if !matches!(self.kind.as_str(), "Family" | "ParameterSchema") {
            tabs.push((
                "Settings",
                if self.kind == "ParameterSet" {
                    "Parameter values"
                } else {
                    "Additional parameters"
                },
            ));
        }
        if !tabs.iter().any(|(key, _)| *key == self.section) {
            self.section = "Definition".into();
        }
        ui.horizontal_wrapped(|ui| {
            for (key, label) in &tabs {
                if crate::presentation::tab_label(ui, self.section == *key, *label).clicked() {
                    self.section = (*key).into();
                }
            }
        });
        let editable = enabled && self.editing;
        let refs = parse(s, "references");
        let products = parse(s, "products");
        if self.section == "Definition" {
            let mut fields = vec![
                ("Code", "Code"),
                ("Name", "Name"),
                ("Description", "Description"),
                ("Version", "Version"),
            ];
            match self.kind.as_str() {
                "Variant" => fields.push(("Parent", "Structure")),
                "ParameterSet" => fields.push(("Parent", "Parameter schema")),
                "Deployment" => {
                    fields.push(("Parent", "Strategy"));
                    fields.push(("Horizon", "Timeframe"));
                }
                _ => {}
            }
            egui::Grid::new("reference-catalog-definition")
                .num_columns(2)
                .spacing([8., 8.])
                .show(ui, |ui| {
                    for (key, caption) in fields {
                        let height = if key == "Description" { 77. } else { 30. };
                        let label = ui.add_sized(
                            [157., height],
                            crate::presentation::caption_widget(caption),
                        );
                        let width = ui.available_width();
                        if key == "Version" {
                            ui.add_sized(
                                [width, height],
                                crate::presentation::label_widget(
                                    self.definition["Key"]["Version"].to_string(),
                                ),
                            );
                        } else if key == "Parent" {
                            catalog_reference_choice(
                                ui,
                                &mut self.definition[key],
                                &refs,
                                Some(match self.kind.as_str() {
                                    "Variant" => 3,
                                    "ParameterSet" => 5,
                                    _ => 2,
                                }),
                                editable,
                                label.id,
                            );
                        } else if key == "Horizon" {
                            let choices = parse(s, "horizons");
                            catalog_enum_choice(
                                ui,
                                &mut self.definition[key],
                                &choices,
                                editable,
                                label.id,
                            );
                        } else {
                            let mut text = self.definition[key].as_str().unwrap_or("").to_owned();
                            let mutable = editable
                                && !(key == "Code"
                                    && self.definition["Key"]["Version"].as_u64().unwrap_or(0) > 1);
                            let widget = if key == "Description" {
                                egui::TextEdit::multiline(&mut text)
                            } else {
                                egui::TextEdit::singleline(&mut text)
                            };
                            if ui
                                .add_sized([width, height], widget.interactive(mutable))
                                .labelled_by(label.id)
                                .changed()
                            {
                                self.definition[key] = json!(text);
                            }
                        }
                        ui.end_row();
                    }
                });
        } else if self.section == "Variant" {
            egui::Grid::new("reference-catalog-variant")
                .num_columns(2)
                .spacing([8., 8.])
                .show(ui, |ui| {
                    for (key, caption, choices) in [
                        ("Side", "Structure side", vec!["Long", "Short", "Custom"]),
                        (
                            "Bias",
                            "Directional bias",
                            vec!["Balanced", "Bullish", "Bearish", "Custom"],
                        ),
                        (
                            "PremiumMode",
                            "Premium mode",
                            vec!["None", "Credit", "Debit", "Custom"],
                        ),
                    ] {
                        let label =
                            ui.add_sized([157., 30.], crate::presentation::caption_widget(caption));
                        let mut text = self.definition[key].as_str().unwrap_or("").to_owned();
                        ui.add_enabled_ui(editable, |ui| {
                            egui::ComboBox::from_id_salt(key)
                                .selected_text(&text)
                                .show_ui(ui, |ui| {
                                    for choice in choices {
                                        ui.selectable_value(&mut text, choice.into(), choice);
                                    }
                                })
                                .response
                                .labelled_by(label.id)
                        });
                        if editable {
                            self.definition[key] = json!(text);
                        }
                        ui.end_row();
                    }
                    for (key, caption) in [
                        ("TargetNetDelta", "Target net delta"),
                        ("BalanceTolerance", "Balance tolerance"),
                        ("SymmetricWings", "Wing symmetry"),
                        ("MinimumWingWidth", "Minimum wing width"),
                        ("MaximumWingWidth", "Maximum wing width"),
                    ] {
                        let label =
                            ui.add_sized([157., 30.], crate::presentation::caption_widget(caption));
                        if key == "SymmetricWings" {
                            let mut value =
                                self.definition["Settings"][key].as_bool().unwrap_or(false);
                            if ui
                                .add_enabled(
                                    editable,
                                    crate::presentation::checkbox(&mut value, "Equal wing widths"),
                                )
                                .labelled_by(label.id)
                                .changed()
                            {
                                self.definition["Settings"][key] = json!(value);
                            }
                        } else {
                            let mut value = self.definition["Settings"][key].as_f64().unwrap_or(0.);
                            let maximum = if matches!(key, "MinimumWingWidth" | "MaximumWingWidth")
                            {
                                1_000_000.
                            } else {
                                10_000.
                            };
                            let minimum = if key == "TargetNetDelta" {
                                -10_000.
                            } else {
                                0.
                            };
                            if ui
                                .add_enabled(
                                    editable,
                                    egui::DragValue::new(&mut value)
                                        .range(minimum..=maximum)
                                        .speed(0.01)
                                        .fixed_decimals(4),
                                )
                                .labelled_by(label.id)
                                .changed()
                            {
                                self.definition["Settings"][key] = json!(value);
                            }
                        }
                        ui.end_row();
                    }
                });
        } else if self.section == "Settings" {
            catalog_settings_grid(
                ui,
                &mut self.definition["Settings"],
                editable,
                self.kind == "ParameterSchema",
                &mut self.numbers,
            );
        } else {
            let columns: &[(&str, &str)] = match self.section.as_str() {
                "Families" | "Structures" | "Variants" => &[("", "Definition")],
                "ExpiryGroups" => &[("Key", "Group"), ("AfterGroup", "After group")],
                "Legs" => &[
                    ("Key", "Leg"),
                    ("InstrumentClass", "Instrument class"),
                    ("Side", "Side"),
                    ("OptionRight", "Option right"),
                    ("Ratio", "Ratio"),
                    ("ExpiryGroup", "Expiry group"),
                ],
                "VariantLegs" => &[("LegKey", "Leg"), ("Side", "Side"), ("Ratio", "Ratio")],
                "Products" => &[
                    ("Symbol", "Symbol"),
                    ("ProductId", "Product ID"),
                    ("Exchange", "Exchange"),
                    ("Currency", "Currency"),
                ],
                "PipelineParameters" => &[
                    ("Role", "Role"),
                    ("Kind", "Stage"),
                    ("Id", "Profile ID"),
                    ("Version", "Version"),
                    ("Hash", "Hash"),
                ],
                "Parameters" => &[("Role", "Role"), ("ParameterSet", "Parameter set")],
                "LegacyFamilies" => &[("Id", "Legacy ID"), ("Version", "Legacy version")],
                _ => &[
                    ("Role", "Role"),
                    ("Code", "Capability"),
                    ("Version", "Version"),
                ],
            };
            catalog_rows_grid(
                ui,
                &mut self.definition[&self.section],
                &self.section,
                columns,
                editable,
                &refs,
                &products,
                &mut self.numbers,
            );
        }
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
                    ui.add(crate::presentation::checkbox(v, ""))
                        .labelled_by(label.id);
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

/// Select an exact versioned reference using the same filtered catalogue as WinForms.
fn catalog_reference_choice(
    ui: &mut egui::Ui,
    value: &mut Value,
    refs: &Value,
    kind: Option<i64>,
    editable: bool,
    label: egui::Id,
) -> bool {
    let rows = refs.as_array();
    let selected = rows.and_then(|rows| rows.iter().find(|row| row["Key"] == *value));
    let text = selected
        .map(|row| {
            format!(
                "{} [{}, v{}, {}]",
                row["Name"].as_str().unwrap_or(""),
                row["Code"].as_str().unwrap_or(""),
                row["Key"]["Version"],
                row["Status"]
            )
        })
        .unwrap_or_else(|| {
            if value.is_null() {
                String::new()
            } else {
                identity(value)
            }
        });
    let mut changed = false;
    ui.add_enabled_ui(editable, |ui| {
        egui::ComboBox::from_id_salt(label)
            .width(ui.available_width())
            .selected_text(text)
            .show_ui(ui, |ui| {
                for row in rows
                    .into_iter()
                    .flatten()
                    .filter(|row| kind.is_none() || row["Key"]["Kind"].as_i64() == kind)
                {
                    if ui
                        .selectable_label(
                            row["Key"] == *value,
                            format!(
                                "{} [{}, v{}, {}]",
                                row["Name"].as_str().unwrap_or(""),
                                row["Code"].as_str().unwrap_or(""),
                                row["Key"]["Version"],
                                row["Status"]
                            ),
                        )
                        .clicked()
                    {
                        *value = row["Key"].clone();
                        changed = true;
                    }
                }
            })
            .response
            .labelled_by(label)
    });
    changed
}
/// Select one published enum value without interpreting its numeric discriminator in the UI.
fn catalog_enum_choice(
    ui: &mut egui::Ui,
    value: &mut Value,
    choices: &Value,
    editable: bool,
    label: egui::Id,
) {
    let name = choices
        .as_array()
        .and_then(|rows| rows.iter().find(|row| row["Value"] == *value))
        .and_then(|row| row["Name"].as_str())
        .unwrap_or("");
    ui.add_enabled_ui(editable, |ui| {
        egui::ComboBox::from_id_salt(label)
            .selected_text(name)
            .show_ui(ui, |ui| {
                for row in choices.as_array().into_iter().flatten() {
                    if ui
                        .selectable_label(
                            row["Value"] == *value,
                            row["Name"].as_str().unwrap_or(""),
                        )
                        .clicked()
                    {
                        *value = row["Value"].clone();
                    }
                }
            })
            .response
            .labelled_by(label)
    });
}
/// Original catalog row grids, including the native blank insertion row and keyboard deletion.
fn catalog_rows_grid(
    ui: &mut egui::Ui,
    value: &mut Value,
    section: &str,
    columns: &[(&str, &str)],
    editable: bool,
    refs: &Value,
    products: &Value,
    buffers: &mut BTreeMap<String, String>,
) {
    if value.is_null() {
        *value = json!([]);
    }
    let Some(rows) = value.as_array_mut() else {
        return;
    };
    let mut blank = template(section, refs, products);
    if !blank.is_object() && columns.first().is_some_and(|(key, _)| !key.is_empty()) {
        blank = json!({});
        for (key, _) in columns {
            blank[*key] = json!("");
        }
    }
    let mut insert = false;
    let mut remove = None;
    let reference_kind = match section {
        "Families" => Some(1),
        "Structures" => Some(3),
        "Variants" => Some(4),
        _ => None,
    };
    let width = (ui.available_width() / columns.len().max(1) as f32).max(90.);
    egui::ScrollArea::horizontal()
        .id_salt(("catalog-grid-scroll", section))
        .show(ui, |ui| {
            let mut table = egui_extras::TableBuilder::new(ui)
                .id_salt(("catalog-grid", section))
                .resizable(true);
            for _ in columns {
                table = table.column(egui_extras::Column::initial(width).at_least(60.));
            }
            table
                .header(32., |mut header| {
                    for (_, caption) in columns {
                        header.col(|ui| {
                            ui.add(crate::presentation::caption_widget(*caption));
                        });
                    }
                })
                .body(|body| {
                    body.rows(28., rows.len() + usize::from(editable), |mut row| {
                        let index = row.index();
                        let existing = index < rows.len();
                        let item = if existing {
                            &mut rows[index]
                        } else {
                            &mut blank
                        };
                        for (key, _) in columns {
                            row.col(|ui| {
                                let id = ui.id().with((section, index, key));
                                if key.is_empty() || *key == "ParameterSet" {
                                    let target = if key.is_empty() {
                                        &mut *item
                                    } else {
                                        &mut item[*key]
                                    };
                                    if catalog_reference_choice(
                                        ui,
                                        target,
                                        refs,
                                        if *key == "ParameterSet" {
                                            Some(6)
                                        } else {
                                            reference_kind
                                        },
                                        editable,
                                        id,
                                    ) {
                                        insert = !existing;
                                    }
                                    return;
                                }
                                if section == "Products" && *key == "Symbol" {
                                    let current = item["Symbol"].as_str().unwrap_or("").to_owned();
                                    ui.add_enabled_ui(editable, |ui| {
                                        egui::ComboBox::from_id_salt(id)
                                            .selected_text(current)
                                            .show_ui(ui, |ui| {
                                                for product in
                                                    products.as_array().into_iter().flatten()
                                                {
                                                    if ui
                                                        .selectable_label(
                                                            *item == *product,
                                                            format!(
                                                                "{} - {} - {} [{}]",
                                                                product["Symbol"]
                                                                    .as_str()
                                                                    .unwrap_or(""),
                                                                product["Exchange"]
                                                                    .as_str()
                                                                    .unwrap_or(""),
                                                                product["Currency"]
                                                                    .as_str()
                                                                    .unwrap_or(""),
                                                                product["ProductId"]
                                                            ),
                                                        )
                                                        .clicked()
                                                    {
                                                        *item = product.clone();
                                                        insert = !existing;
                                                    }
                                                }
                                            })
                                    });
                                    return;
                                }
                                let v = &mut item[*key];
                                if *key == "Required" || v.is_boolean() {
                                    let mut boolean = v.as_bool().unwrap_or(false);
                                    if ui
                                        .add_enabled(
                                            editable,
                                            crate::presentation::checkbox(&mut boolean, ""),
                                        )
                                        .changed()
                                    {
                                        *v = json!(boolean);
                                        insert = !existing;
                                    }
                                    return;
                                }
                                let numeric = (v.is_number()
                                    && !matches!(
                                        *key,
                                        "Minimum" | "Maximum" | "MinLength" | "MaxLength"
                                    ))
                                    || matches!(*key, "Ratio" | "Version")
                                    || section == "LegacyFamilies";
                                let mut local = if !existing {
                                    String::new()
                                } else {
                                    v.as_str().map(str::to_owned).unwrap_or_else(|| {
                                        if v.is_null() {
                                            String::new()
                                        } else {
                                            v.to_string()
                                        }
                                    })
                                };
                                let text = if numeric {
                                    buffers
                                        .entry(format!("{section}/{index}/{key}"))
                                        .or_insert_with(|| local.clone())
                                } else {
                                    &mut local
                                };
                                let response = ui.add(
                                    egui::TextEdit::singleline(text)
                                        .desired_width(ui.available_width())
                                        .interactive(editable && section != "Products"),
                                );
                                if response.changed() {
                                    if numeric {
                                        if let Ok(n) = serde_json::from_str::<Value>(text) {
                                            if n.is_number() {
                                                *v = n;
                                                insert = !existing;
                                            }
                                        }
                                    } else {
                                        *v = json!(text);
                                        insert = !existing;
                                    }
                                }
                                if editable
                                    && existing
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
        });
    if let Some(index) = remove {
        rows.remove(index);
        buffers.retain(|key, _| !key.starts_with(&format!("{section}/")));
    }
    if insert {
        rows.push(blank);
    }
}

/// Display and edit leaf parameter paths with the original three-column name/type/value grid.
fn catalog_settings_grid(
    ui: &mut egui::Ui,
    settings: &mut Value,
    editable: bool,
    schema: bool,
    buffers: &mut BTreeMap<String, String>,
) {
    if schema {
        catalog_shape_grid(ui, settings, editable, buffers);
        return;
    }
    fn leaves(value: &Value, path: String, rows: &mut Vec<Value>) {
        match value{Value::Object(map)=>for(key,value)in map{leaves(value,format!("{path}/{}",key.replace('~',"~0").replace('/',"~1")),rows);},Value::Array(items)=>for(index,value)in items.iter().enumerate(){leaves(value,format!("{path}/{index}"),rows);},_=>rows.push(json!({"Path":path,"Type":if value.is_number(){"Number"}else if value.is_boolean(){"Boolean"}else if value.is_null(){"Null"}else{"String"},"Value":value}))}
    }
    let mut rows = vec![];
    leaves(settings, String::new(), &mut rows);
    let draft_id = ui.id().with(if schema {
        "reference-schema-rows"
    } else {
        "reference-settings-rows"
    });
    let mut value = ui
        .data_mut(|data| data.get_temp::<(Value, Value)>(draft_id))
        .filter(|(source, _)| source == settings)
        .map(|(_, rows)| rows)
        .unwrap_or_else(|| json!(rows));
    catalog_rows_grid(
        ui,
        &mut value,
        "additional-parameters",
        &[
            ("Path", "Parameter path"),
            ("Type", "Type"),
            ("Value", "Value"),
        ],
        editable,
        &Value::Null,
        &Value::Null,
        buffers,
    );
    if editable {
        if let Some(rebuilt) = rebuild_settings(&value) {
            *settings = rebuilt;
            buffers.remove("reference-settings-validation");
        } else {
            buffers.insert("reference-settings-validation".into(), "invalid".into());
        }
    }
    ui.data_mut(|data| data.insert_temp(draft_id, (settings.clone(), value)));
}
/// Keep schema fields in the source path/type/constraints grid and rebuild their nested shape.
fn catalog_shape_grid(
    ui: &mut egui::Ui,
    shape: &mut Value,
    editable: bool,
    buffers: &mut BTreeMap<String, String>,
) {
    fn flatten(value: &Value, path: String, required: bool, rows: &mut Vec<Value>) {
        rows.push(json!({"Path":path,"Type":match value["Type"].as_i64(){Some(1)=>"Object",Some(2)=>"Array",Some(3)=>"String",Some(4)=>"Decimal",Some(5)=>"Integer",Some(6)=>"Boolean",_=>"Object"},"Required":required,"Unit":value["Unit"],"Minimum":value["Minimum"],"Maximum":value["Maximum"],"MinLength":value["MinLength"],"MaxLength":value["MaxLength"],"Choices":value["Choices"].as_array().into_iter().flatten().filter_map(Value::as_str).collect::<Vec<_>>().join(",")}));
        if let Some(properties) = value["Properties"].as_object() {
            for (key, child) in properties {
                let required = value["Required"]
                    .as_array()
                    .is_some_and(|items| items.iter().any(|item| item == key));
                flatten(
                    child,
                    format!(
                        "{path}/properties/{}",
                        key.replace('~', "~0").replace('/', "~1")
                    ),
                    required,
                    rows,
                );
            }
        }
        if value["Items"].is_object() {
            flatten(&value["Items"], format!("{path}/items"), false, rows);
        }
    }
    let mut rows = vec![];
    flatten(shape, "$".into(), false, &mut rows);
    let draft_id = ui.id().with("reference-schema-rows");
    let mut value = ui
        .data_mut(|data| data.get_temp::<(Value, Value)>(draft_id))
        .filter(|(source, _)| source == shape)
        .map(|(_, rows)| rows)
        .unwrap_or_else(|| json!(rows));
    catalog_rows_grid(
        ui,
        &mut value,
        "parameter-shape",
        &[
            ("Path", "Parameter"),
            ("Type", "Type"),
            ("Required", "Required"),
            ("Unit", "Unit"),
            ("Minimum", "Minimum"),
            ("Maximum", "Maximum"),
            ("MinLength", "Min length"),
            ("MaxLength", "Max length"),
            ("Choices", "Choices"),
        ],
        editable,
        &Value::Null,
        &Value::Null,
        buffers,
    );
    if editable {
        if let Some(rebuilt) = rebuild_shape(&value) {
            *shape = rebuilt;
            buffers.remove("reference-settings-validation");
        } else {
            buffers.insert("reference-settings-validation".into(), "invalid".into());
        }
    }
    ui.data_mut(|data| data.insert_temp(draft_id, (shape.clone(), value)));
}
/// Reconstruct the bounded schema DSL, preserving property membership and optional constraints.
fn rebuild_shape(rows: &Value) -> Option<Value> {
    let rows = rows.as_array()?;
    let mut by_path = BTreeMap::new();
    for row in rows {
        let path = row["Path"].as_str()?;
        if path.is_empty() {
            continue;
        }
        if by_path.insert(path, row).is_some() {
            return None;
        }
    }
    fn build(path: &str, rows: &BTreeMap<&str, &Value>, depth: usize) -> Option<Value> {
        if depth > 12 {
            return None;
        }
        let row = rows.get(path)?;
        let kind = match row["Type"].as_str()? {
            "Object" => 1,
            "Array" => 2,
            "String" => 3,
            "Decimal" => 4,
            "Integer" => 5,
            "Boolean" => 6,
            _ => return None,
        };
        let mut shape = json!({"Type":kind,"Properties":{},"Required":[],"Items":null,"Unit":row["Unit"].as_str().unwrap_or(""),"Choices":row["Choices"].as_str().unwrap_or("").split(',').map(str::trim).filter(|choice|!choice.is_empty()).collect::<Vec<_>>()});
        for key in ["Minimum", "Maximum", "MinLength", "MaxLength"] {
            let value = &row[key];
            shape[key] = if value.is_null() || value.as_str() == Some("") {
                Value::Null
            } else if value.is_number() {
                value.clone()
            } else {
                serde_json::from_str::<Value>(value.as_str()?).ok()?
            };
            if !shape[key].is_null() && !shape[key].is_number() {
                return None;
            }
        }
        let prefix = format!("{path}/properties/");
        for (child, row) in rows
            .range(prefix.as_str()..)
            .take_while(|(key, _)| key.starts_with(&prefix))
        {
            let suffix = &child[prefix.len()..];
            if suffix.contains('/') {
                continue;
            }
            if kind != 1 {
                return None;
            }
            let key = suffix.replace("~1", "/").replace("~0", "~");
            shape["Properties"][&key] = build(child, rows, depth + 1)?;
            if row["Required"].as_bool() == Some(true) {
                shape["Required"].as_array_mut()?.push(json!(key));
            }
        }
        let items = format!("{path}/items");
        if rows.contains_key(items.as_str()) {
            if kind != 2 {
                return None;
            }
            shape["Items"] = build(&items, rows, depth + 1)?;
        }
        Some(shape)
    }
    build("$", &by_path, 0)
}

/// Build parameter values from path/type/value rows; an invalid draft never replaces the last valid object.
fn rebuild_settings(rows: &Value) -> Option<Value> {
    let mut root = json!({});
    for row in rows.as_array()? {
        let path = row["Path"].as_str()?.trim_start_matches('/');
        if path.is_empty() {
            continue;
        }
        let text = row["Value"]
            .as_str()
            .map(str::to_owned)
            .unwrap_or_else(|| row["Value"].to_string());
        let value = match row["Type"].as_str() {
            Some("Number" | "Boolean" | "Null") => serde_json::from_str::<Value>(&text).ok()?,
            _ => json!(text),
        };
        if !write_parameter_path(
            &mut root,
            &path
                .split('/')
                .map(|key| key.replace("~1", "/").replace("~0", "~"))
                .collect::<Vec<_>>(),
            value,
        ) {
            return None;
        }
    }
    Some(root)
}
/// Insert a bounded JSON parameter path, creating intermediate objects and arrays as required.
fn write_parameter_path(root: &mut Value, path: &[String], value: Value) -> bool {
    let Some((key, remaining)) = path.split_first() else {
        return false;
    };
    if let Some(map) = root.as_object_mut() {
        if remaining.is_empty() {
            return map.insert(key.clone(), value).is_none();
        }
        let next = map.entry(key.clone()).or_insert_with(|| {
            if remaining[0].parse::<usize>().is_ok() {
                json!([])
            } else {
                json!({})
            }
        });
        return write_parameter_path(next, remaining, value);
    }
    if let Some(array) = root.as_array_mut() {
        let Ok(index) = key.parse::<usize>() else {
            return false;
        };
        if index > 127 {
            return false;
        }
        while array.len() <= index {
            array.push(Value::Null);
        }
        if remaining.is_empty() {
            array[index] = value;
            return true;
        }
        if array[index].is_null() {
            array[index] = if remaining[0].parse::<usize>().is_ok() {
                json!([])
            } else {
                json!({})
            };
        }
        return write_parameter_path(&mut array[index], remaining, value);
    }
    false
}

#[cfg(test)]
mod reference_grid_tests {
    use super::*;
    #[test]
    fn parameter_paths_rebuild_nested_arrays_without_losing_decimal_precision() {
        let rows = json!([{"Path":"/Risk/Target","Type":"Number","Value":"0.1234567890123456789012345678"},{"Path":"/Names/0","Type":"String","Value":"ES"},{"Path":"/Enabled","Type":"Boolean","Value":"true"}]);
        let value = rebuild_settings(&rows).unwrap();
        assert!(value.to_string().contains("0.1234567890123456789012345678"));
        assert_eq!(value["Names"][0], "ES");
        assert_eq!(value["Enabled"], true);
    }
    #[test]
    fn schema_rebuild_preserves_required_fields_and_nullable_constraints() {
        let rows = json!([{"Path":"$","Type":"Object","Required":false,"Unit":"","Minimum":null,"Maximum":null,"MinLength":null,"MaxLength":null,"Choices":""},{"Path":"$/properties/Target","Type":"Decimal","Required":true,"Unit":"delta","Minimum":"0.1","Maximum":"1","MinLength":"","MaxLength":"","Choices":""}]);
        let value = rebuild_shape(&rows).unwrap();
        assert_eq!(value["Required"], json!(["Target"]));
        assert_eq!(value["Properties"]["Target"]["Type"], 4);
        assert_eq!(value["Properties"]["Target"]["Minimum"], 0.1);
        assert!(value["MinLength"].is_null());
    }
}
