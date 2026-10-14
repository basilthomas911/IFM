use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, contract_editor::ContractPublication, lookup_editor::new_request_id, strings,
};
use eframe::egui;
use std::collections::BTreeMap;
pub const TENORS: [(&str, &str); 12] = [
    ("oneMonth", "rate_1m"),
    ("twoMonth", "rate_2m"),
    ("threeMonth", "rate_3m"),
    ("sixMonth", "rate_6m"),
    ("oneYear", "rate_1y"),
    ("twoYear", "rate_2y"),
    ("threeYear", "rate_3y"),
    ("fiveYear", "rate_5y"),
    ("sevenYear", "rate_7y"),
    ("tenYear", "rate_10y"),
    ("twentyYear", "rate_20y"),
    ("thirtyYear", "rate_30y"),
];
#[derive(Default)]
pub struct YieldCurveEditor {
    pub draft: BTreeMap<String, String>,
    pub selected: Option<String>,
    pub pending: Option<String>,
    pub period: String,
    pub import_date: String,
    pub confirm_remove: bool,
    pub drawn_rows: usize,
    last_action: String,
}
impl YieldCurveEditor {
    pub fn request(&mut self, action: &str, s: &ContractPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.last_action = action.into();
        let mut fields = self.draft.clone();
        fields.insert("timePeriod".into(), self.period.clone());
        fields.insert("importDate".into(), self.import_date.clone());
        serde_json::json!({"schema":1,"requestId":id,"view":"yield-curve","intent":action,"expectedGeneration":s.generation,"originalId":self.selected,"fields":fields}).to_string()
    }
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let matches = s.result.view == "yield-curve";
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
                    } else if let Some(row) = s
                        .result
                        .rows
                        .iter()
                        .find(|r| r.fields.get("valueDate") == self.draft.get("valueDate"))
                    {
                        self.selected = Some(row.id.clone());
                        self.draft = row.fields.clone();
                    } else if self.selected.is_some() {
                        self.selected = None;
                        self.draft.clear();
                    }
                }
                self.pending = None;
            }
        }
        if matches && self.period.is_empty() {
            self.period = s
                .result
                .context
                .get("timePeriod")
                .cloned()
                .unwrap_or_default();
        }
        if matches && self.import_date.is_empty() {
            self.import_date = s
                .result
                .context
                .get("valueDate")
                .cloned()
                .unwrap_or_default();
        }
        let enabled = live && !s.busy && self.pending.is_none();
        let mut action = None;
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading(c.text(strings::YIELD_CURVE));
            ui.trading_label(if self.pending.is_some() {
                c.text(strings::AWAITING_COMPLETION)
            } else {
                &s.status
            });
            if !live {
                ui.trading_label(c.text(strings::LIVE_REQUIRED));
            }
            if let Some(last) = s.outcomes.last() {
                ui.trading_label(format!("{}: {}", last.state, last.message));
            }
            ui.add_enabled_ui(enabled, |ui| {
                ui.horizontal_wrapped(|ui| {
                    if ui.button(c.text(strings::LOAD_RATES)).clicked() {
                        action = Some("load");
                    }
                    egui::ComboBox::from_label(crate::presentation::label_text(c.text(strings::TIME_PERIOD)))
                        .selected_text(&self.period)
                        .show_ui(ui, |ui| {
                            if matches {
                                for period in
                                    s.result.choices.get("timePeriod").into_iter().flatten()
                                {
                                    ui.selectable_value(&mut self.period, period.clone(), period);
                                }
                            }
                        });
                    if ui
                        .add_enabled(matches, egui::Button::new(c.text(strings::APPLY_PERIOD)))
                        .clicked()
                    {
                        action = Some("range");
                    }
                    if ui
                        .add_enabled(matches, egui::Button::new(c.text(strings::NEW_RATE)))
                        .clicked()
                    {
                        self.selected = None;
                        self.confirm_remove = false;
                        self.draft = BTreeMap::from([(
                            "valueDate".into(),
                            s.result
                                .context
                                .get("valueDate")
                                .cloned()
                                .unwrap_or_default(),
                        )]);
                        for (key, _) in TENORS {
                            self.draft.insert(key.into(), "0".into());
                        }
                    }
                });
                egui::Grid::new("yield-rate-fields")
                    .num_columns(4)
                    .show(ui, |ui| {
                        let label = ui.trading_label(c.text(strings::VALUE_DATE));
                        ui.add_enabled(
                            self.selected.is_none(),
                            egui::TextEdit::singleline(
                                self.draft.entry("valueDate".into()).or_default(),
                            )
                            .desired_width(160.)
                            .min_size(egui::vec2(160., 20.)),
                        )
                        .labelled_by(label.id);
                        ui.end_row();
                        for (i, (key, label)) in TENORS.iter().enumerate() {
                            let label = ui.trading_label(c.text(label));
                            ui.add(
                                egui::TextEdit::singleline(
                                    self.draft.entry((*key).into()).or_default(),
                                )
                                .desired_width(120.)
                                .min_size(egui::vec2(120., 20.)),
                            )
                            .labelled_by(label.id);
                            if i % 2 == 1 {
                                ui.end_row();
                            }
                        }
                    });
                let valid = self
                    .draft
                    .get("valueDate")
                    .is_some_and(|v| chrono::NaiveDate::parse_from_str(v, "%Y-%m-%d").is_ok())
                    && TENORS.iter().all(|(key, _)| {
                        self.draft
                            .get(*key)
                            .is_some_and(|v| v.parse::<f64>().is_ok_and(f64::is_finite))
                    });
                ui.horizontal_wrapped(|ui| {
                    if ui
                        .add_enabled(
                            matches && valid,
                            egui::Button::new(c.text(if self.selected.is_some() {
                                strings::SAVE_RATE
                            } else {
                                strings::ADD_RATE
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
                            egui::Button::new(c.text(strings::REMOVE_RATE)),
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
                    ui.trading_label(c.text(strings::REMOVE_RATE_WARNING));
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
                ui.horizontal_wrapped(|ui| {
                    let label = ui.trading_label(c.text(strings::IMPORT_DATE));
                    ui.add(egui::TextEdit::singleline(&mut self.import_date).desired_width(160.))
                        .labelled_by(label.id);
                    if ui
                        .add_enabled(matches, egui::Button::new(c.text(strings::IMPORT_RATES)))
                        .clicked()
                    {
                        action = Some("import");
                    }
                });
            });
            self.drawn_rows = 0;
            if matches {
                egui::ScrollArea::horizontal().show(ui, |ui| {
                    let mut table = egui_extras::TableBuilder::new(ui)
                        .id_salt("yield-curve-table")
                        .striped(true)
                        .resizable(true)
                        .column(egui_extras::Column::auto());
                    for _ in TENORS {
                        table = table.column(egui_extras::Column::auto());
                    }
                    table
                        .header(24., |mut header| {
                            header.col(|ui| {
                                ui.strong(c.text(strings::VALUE_DATE));
                            });
                            for (_, label) in TENORS {
                                header.col(|ui| {
                                    ui.strong(c.text(label));
                                });
                            }
                        })
                        .body(|body| {
                            body.rows(26., s.result.rows.len(), |mut row| {
                                self.drawn_rows += 1;
                                let item = &s.result.rows[row.index()];
                                row.col(|ui| {
                                    if ui
                                        .add_enabled(
                                            enabled,
                                            egui::Button::new(&item.id)
                                                .selected(self.selected.as_ref() == Some(&item.id)),
                                        )
                                        .clicked()
                                    {
                                        self.selected = Some(item.id.clone());
                                        self.draft = item.fields.clone();
                                        self.confirm_remove = false;
                                    }
                                });
                                for (key, _) in TENORS {
                                    row.col(|ui| {
                                        ui.trading_label(
                                            item.fields.get(key).map(String::as_str).unwrap_or(""),
                                        );
                                    });
                                }
                            });
                        });
                });
            }
        });
        action.map(|a| self.request(a, s))
    }
}
