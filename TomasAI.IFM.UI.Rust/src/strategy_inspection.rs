use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, DetailsView, Snapshot,
    lookup_editor::{EditorOutcome, new_request_id},
    strings,
};
use eframe::egui;
use serde::Deserialize;
use std::collections::BTreeMap;
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InspectionRow {
    pub id: String,
    pub label: String,
    pub fields: BTreeMap<String, String>,
}
#[derive(Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InspectionResult {
    pub view: String,
    pub rows: Vec<InspectionRow>,
    pub detail: Option<Snapshot>,
    pub decision: String,
    pub evidence: String,
    pub paging_state: Option<String>,
    pub context: Option<BTreeMap<String, String>>,
}
#[derive(Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InspectionPublication {
    pub schema: u32,
    pub generation: u64,
    pub status: String,
    pub busy: bool,
    pub result: InspectionResult,
    pub outcomes: Vec<EditorOutcome>,
}
pub struct StrategyInspection {
    pub risk: bool,
    pub fields: BTreeMap<String, String>,
    pub pending: Option<String>,
    pub details: DetailsView,
    pub evidence: bool,
    pub drawn_rows: usize,
    pub selected: Option<String>,
}
impl Default for StrategyInspection {
    fn default() -> Self {
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap_or_default()
            .as_secs();
        Self {
            risk: false,
            fields: BTreeMap::from([
                ("profile".into(), "ES.Standard".into()),
                ("horizon".into(), "Daily".into()),
                ("workflow".into(), String::new()),
                ("invocation".into(), String::new()),
                ("portfolio".into(), "1".into()),
                ("fund".into(), "1".into()),
                (
                    "date".into(),
                    chrono::DateTime::from_timestamp(now as i64, 0)
                        .unwrap()
                        .format("%Y-%m-%d")
                        .to_string(),
                ),
            ]),
            pending: None,
            details: Default::default(),
            evidence: false,
            drawn_rows: 0,
            selected: None,
        }
    }
}
impl StrategyInspection {
    pub fn request(&mut self, action: &str, s: &InspectionPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        let mut fields = self.fields.clone();
        if action == "next" {
            fields.insert(
                "cursor".into(),
                s.result.paging_state.clone().unwrap_or_default(),
            );
        }
        serde_json::json!({"schema":1,"requestId":id,"view":if self.risk{"risk"}else{"observation"},"intent":action,"fields":fields}).to_string()
    }
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &InspectionPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        if self.pending.as_ref().is_some_and(|id| {
            s.outcomes
                .iter()
                .any(|x| &x.request_id == id && x.state != "Pending")
        }) {
            self.pending = None;
        }
        let mut intent = None;
        let view = if self.risk { "risk" } else { "observation" };
        let result_matches = s.result.view == view;
        egui::TopBottomPanel::top("inspection-filters").show(ctx, |ui| {
            ui.heading(c.text(if self.risk {
                strings::RISK_HISTORY
            } else {
                strings::STRATEGY_OBSERVATION
            }));
            ui.trading_label(&s.status);
            ui.add_enabled_ui(live && !s.busy && self.pending.is_none(), |ui| {
                egui::Grid::new("inspection-fields").show(ui, |ui| {
                    let fields = if self.risk {
                        vec![
                            ("portfolio", strings::PORTFOLIO_ID),
                            ("fund", strings::FUND_ID),
                            ("date", strings::EVALUATION_DATE),
                            ("workflow", strings::WORKFLOW_ID),
                            ("invocation", strings::INVOCATION_ID),
                        ]
                    } else {
                        vec![
                            ("profile", strings::MARKET_PROFILE),
                            ("workflow", strings::WORKFLOW_ID),
                        ]
                    };
                    for (key, label) in fields {
                        let response = ui.trading_label(c.text(label));
                        let value = self.fields.entry(key.into()).or_default();
                        ui.horizontal(|ui| {
                            ui.add(egui::TextEdit::singleline(value).desired_width(
                                if key == "workflow" || key == "invocation" {
                                    340.0
                                } else {
                                    180.0
                                },
                            ))
                            .labelled_by(response.id);
                            if key == "date" {
                                if let Ok(mut date) =
                                    chrono::NaiveDate::parse_from_str(value, "%Y-%m-%d")
                                {
                                    if ui
                                        .add(egui_extras::DatePickerButton::new(&mut date))
                                        .changed()
                                    {
                                        *value = date.format("%Y-%m-%d").to_string();
                                    }
                                }
                            }
                        });
                        ui.end_row();
                    }
                    if !self.risk {
                        ui.trading_label(c.text(strings::TIMEFRAME));
                        egui::ComboBox::from_id_salt("inspection-horizon")
                            .selected_text(&self.fields["horizon"])
                            .show_ui(ui, |ui| {
                                for v in ["Daily", "Weekly", "Monthly"] {
                                    if ui
                                        .selectable_label(self.fields["horizon"] == v, v)
                                        .clicked()
                                    {
                                        self.fields.insert("horizon".into(), v.into());
                                    }
                                }
                            });
                        ui.end_row();
                    }
                });
                ui.horizontal(|ui| {
                    if ui.button(c.text(strings::LOAD_HISTORY)).clicked() {
                        intent = Some("history");
                        self.selected = None;
                    }
                    if ui
                        .button(c.text(if self.risk {
                            strings::OPEN_INVOCATION
                        } else {
                            strings::OPEN_WORKFLOW
                        }))
                        .clicked()
                    {
                        intent = Some("open");
                    }
                    if !self.risk
                        && ui
                            .add_enabled(
                                result_matches && s.result.context.is_some(),
                                egui::Button::new(c.text(strings::RISK_DETAILS)),
                            )
                            .clicked()
                    {
                        self.risk = true;
                        for (k, v) in s.result.context.as_ref().unwrap() {
                            self.fields.insert(k.clone(), v.clone());
                        }
                        intent = Some("open");
                    }
                    if self.risk
                        && ui
                            .add_enabled(
                                result_matches && s.result.paging_state.is_some(),
                                egui::Button::new(c.text(strings::NEXT_PAGE)),
                            )
                            .clicked()
                    {
                        intent = Some("next");
                    }
                });
            });
        });
        egui::TopBottomPanel::top("inspection-history")
            .resizable(true)
            .default_height(180.0)
            .min_height(50.0)
            .show(ctx, |ui| {
                self.drawn_rows = 0;
                if result_matches {
                    egui::ScrollArea::vertical()
                        .id_salt("inspection-rows")
                        .show_rows(ui, 24.0, s.result.rows.len(), |ui, range| {
                            self.drawn_rows = range.len();
                            for i in range {
                                let row = &s.result.rows[i];
                                if ui
                                    .selectable_label(
                                        self.selected.as_ref() == Some(&row.id),
                                        &row.label,
                                    )
                                    .clicked()
                                    && live
                                    && !s.busy
                                    && self.pending.is_none()
                                {
                                    self.selected = Some(row.id.clone());
                                    for (k, v) in &row.fields {
                                        self.fields.insert(k.clone(), v.clone());
                                    }
                                    intent = Some("open");
                                }
                            }
                        });
                }
            });
        if !self.risk && result_matches && s.result.detail.is_some() {
            self.details.draw(ctx, s.result.detail.as_ref().unwrap(), c);
        } else {
            egui::CentralPanel::default().show(ctx, |ui| {
                if self.risk {
                    ui.horizontal(|ui| {
                        ui.selectable_value(
                            &mut self.evidence,
                            false,
                            c.text(strings::DECISION_AND_SIZING),
                        );
                        ui.selectable_value(&mut self.evidence, true, c.text(strings::EVIDENCE));
                    });
                    if result_matches {
                        let mut content = if self.evidence {
                            s.result.evidence.as_str()
                        } else {
                            s.result.decision.as_str()
                        };
                        egui::ScrollArea::both().show(ui, |ui| {
                            ui.add(
                                egui::TextEdit::multiline(&mut content)
                                    .desired_width(f32::INFINITY),
                            );
                        });
                    }
                } else {
                    ui.trading_label(c.text(strings::SELECT_WORKFLOW));
                }
            });
        }
        intent.map(|a| self.request(a, s))
    }
}
