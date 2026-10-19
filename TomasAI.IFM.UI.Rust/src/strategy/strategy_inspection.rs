//! Risk/workflow inspection with paged results, decision evidence, and the shared details tree. Queries and refreshes are correlated through publication outcomes; the UI does not re-evaluate risk.

use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog, DetailsView, Snapshot,
    lookup_editor::{EditorOutcome, new_request_id},
    strings,
};
use eframe::egui;
use serde::Deserialize;
use std::collections::BTreeMap;
/// Stable inspection result row and its display fields.
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InspectionRow {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: String,
    /// Human-readable caption for this identity/value.
    pub label: String,
    /// Named field definitions or display/edit values supplied by the enclosing model.
    pub fields: BTreeMap<String, String>,
}
/// Inspection query page with optional workflow details, risk decision/evidence, and continuation state.
#[derive(Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InspectionResult {
    /// Managed risk/workflow inspection view key identifying this query result.
    pub view: String,
    /// Rows supplied by the enclosing publication/model; they are not fetched during drawing.
    pub rows: Vec<InspectionRow>,
    /// Optional complete workflow details snapshot for the selected inspection result.
    pub detail: Option<Snapshot>,
    /// Published risk decision explanation.
    pub decision: String,
    /// Backend-provided risk/workflow evidence text; displayed without reevaluating the decision.
    pub evidence: String,
    /// Backend continuation token for the next inspection query page.
    pub paging_state: Option<String>,
    /// Managed view context/eligibility values indexed by stable keys.
    pub context: Option<BTreeMap<String, String>>,
}
/// Versioned inspection result and outcomes for correlated user requests.
#[derive(Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InspectionPublication {
    /// Wire schema version used to interpret this publication.
    pub schema: u32,
    /// Publication generation used for change detection and request correlation.
    pub generation: u64,
    /// Display-ready status supplied by the managed projection or current view state.
    pub status: String,
    /// Whether the managed view currently reports work in progress.
    pub busy: bool,
    /// Latest managed view result projected into this envelope.
    pub result: InspectionResult,
    /// Correlated managed results for submitted request IDs.
    pub outcomes: Vec<EditorOutcome>,
}
/// Risk/workflow inspection filters, result selection, evidence visibility, and pending request.
pub struct StrategyInspection {
    /// Whether the inspection view is in risk-history mode.
    pub risk: bool,
    /// Named field definitions or display/edit values supplied by the enclosing model.
    pub fields: BTreeMap<String, String>,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pub pending: Option<String>,
    /// Shared selected workflow details-tree state.
    pub details: DetailsView,
    /// Whether the inspection view shows the published evidence text.
    pub evidence: bool,
    /// Count of rows actually drawn in the most recent frame, used by virtualization checks.
    pub drawn_rows: usize,
    /// Stable selected identity/index; interpretation follows the enclosing view's row contract.
    pub selected: Option<String>,
}
impl Default for StrategyInspection {
    /// Construct the view's initial filters, selection, and draft settings without issuing backend commands.
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
    /// Build a correlated user intent against the current publication generation and retain its request ID as pending. The caller must enqueue the returned payload.
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
    /// Render the view from its cached publication, keep draft/selection state across frames, and return a user intent when applicable. The caller owns submission.
    ///
    /// This renderer owns risk/workflow inspection filters, result selection, evidence visibility, and pending request.
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
                        let response = ui.trading_caption(c.text(label));
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
                        ui.trading_caption(c.text(strings::TIMEFRAME));
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
                    ui.trading_caption(c.text(strings::SELECT_WORKFLOW));
                }
            });
        }
        intent.map(|a| self.request(a, s))
    }
}
