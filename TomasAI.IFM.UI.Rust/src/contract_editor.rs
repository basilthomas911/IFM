use crate::presentation::TradingLabelUi as _;
use crate::{
    Catalog,
    lookup_editor::{EditorOutcome, new_request_id},
    strings,
};
use eframe::egui;
use serde::Deserialize;
use std::collections::BTreeMap;
#[derive(Clone, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ContractRow {
    pub id: String,
    pub fields: BTreeMap<String, String>,
    #[serde(default)]
    pub data: Option<serde_bytes::ByteBuf>,
}
#[derive(Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ContractResult {
    pub view: String,
    pub rows: Vec<ContractRow>,
    pub choices: BTreeMap<String, Vec<String>>,
    pub has_more: bool,
    #[serde(default)]
    pub context: BTreeMap<String, String>,
    #[serde(default)]
    pub documents: BTreeMap<String, serde_bytes::ByteBuf>,
}
#[derive(Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ContractPublication {
    pub schema: u32,
    pub generation: u64,
    pub status: String,
    pub busy: bool,
    pub result: ContractResult,
    pub outcomes: Vec<EditorOutcome>,
}
impl ContractResult {
    pub fn document<T: serde::de::DeserializeOwned>(&self, key: &str) -> Option<T> {
        self.documents.get(key).and_then(|bytes| crate::wire::decode(bytes).ok())
    }
}
#[derive(Default)]
pub struct ContractEditor {
    pub option: bool,
    pub draft: BTreeMap<String, String>,
    pub selected: Option<String>,
    pub pending: Option<String>,
    pub confirm_remove: bool,
    pub drawn_rows: usize,
    pub review_open: bool,
    pub confirm_review: bool,
    last_action: String,
}
impl ContractEditor {
    pub fn reset(&mut self) {
        self.selected = None;
        self.draft.clear();
        self.confirm_remove = false;
        self.review_open = false;
        self.confirm_review = false;
    }
    fn view(&self) -> &str {
        if self.option {
            "option-contract"
        } else {
            "futures-contract"
        }
    }
    pub fn request(&mut self, action: &str, s: &ContractPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.last_action = action.into();
        serde_json::json!({"schema":1,"requestId":id,"view":self.view(),"intent":action,"expectedGeneration":s.generation,"originalId":self.selected,"fields":self.draft}).to_string()
    }
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
        c: &Catalog,
        live: bool,
    ) -> Option<String> {
        let matches = s.result.view == self.view();
        if let Some(id) = &self.pending {
            if let Some(outcome) = s
                .outcomes
                .iter()
                .find(|x| &x.request_id == id && x.state != "Pending")
            {
                if outcome.state == "Completed" {
                    if self.last_action == "remove" {
                        self.reset();
                    } else if let Some(row) = s.result.rows.iter().find(|r| {
                        r.fields.get("contractId") == self.draft.get("contractId")
                            && r.fields.get("symbol") == self.draft.get("symbol")
                    }) {
                        self.selected = Some(row.id.clone());
                        self.draft = row.fields.clone();
                    } else if self.selected.is_some() {
                        self.reset();
                    }
                }
                self.pending = None;
            }
        }
        let provider = self.draft.get("provider").is_some_and(|x| x == "true");
        let manual_ready = matches
            && [
                "symbol",
                "securityType",
                "currency",
                "exchange",
                "multiplier",
            ]
            .iter()
            .all(|key| s.result.choices.get(*key).is_some_and(|v| !v.is_empty()))
            && (!self.option
                || s.result
                    .choices
                    .get("optionType")
                    .is_some_and(|v| !v.is_empty()));
        let mut action = None;
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading(c.text(if self.option {
                strings::OPTION_CONTRACTS
            } else {
                strings::FUTURES_CONTRACTS
            }));
            ui.trading_label(if self.pending.is_some() {
                c.text(strings::AWAITING_COMPLETION)
            } else {
                &s.status
            });
            if matches && !manual_ready {
                ui.trading_colored_label(
                    egui::Color32::YELLOW,
                    c.text(strings::MISSING_CONTRACT_LOOKUPS),
                );
            }
            if matches && s.result.rows.is_empty() {
                ui.trading_label(c.text(strings::NO_CONTRACT_ROWS));
            }
            if !live {
                ui.trading_label(c.text(strings::LIVE_REQUIRED));
            }
            if let Some(last) = s.outcomes.last() {
                ui.trading_label(format!("{}: {}", last.state, last.message));
            }
            let enabled = live && !s.busy && self.pending.is_none() && !self.review_open;
            ui.horizontal(|ui| {
                if ui
                    .add_enabled(enabled, egui::Button::new(c.text(strings::LOAD_CONTRACTS)))
                    .clicked()
                {
                    action = Some("load");
                }
                if ui
                    .add_enabled(
                        enabled && manual_ready,
                        egui::Button::new(c.text(strings::NEW_CONTRACT)),
                    )
                    .clicked()
                {
                    self.reset();
                }
                if self.option
                    && ui
                        .add_enabled(
                            enabled && matches && s.result.has_more,
                            egui::Button::new(c.text(strings::NEXT_PAGE)),
                        )
                        .clicked()
                {
                    action = Some("next");
                }
            });
            ui.add_enabled_ui(enabled, |ui| {
                egui::Grid::new("contract-editor-fields")
                    .num_columns(4)
                    .show(ui, |ui| {
                        let mut fields = vec![
                            ("contractId", strings::CONTRACT_ID),
                            ("description", strings::DESCRIPTION),
                            ("symbol", strings::SYMBOL),
                            ("localSymbol", strings::LOCAL_SYMBOL),
                            ("securityType", strings::SECURITY_TYPE),
                            ("currency", strings::CURRENCY),
                            ("exchange", strings::EXCHANGE),
                            ("multiplier", strings::MULTIPLIER),
                        ];
                        if self.option {
                            fields.extend([
                                ("contractMonth", strings::CONTRACT_MONTH),
                                ("strikePrice", strings::STRIKE_PRICE),
                                ("optionType", strings::OPTION_TYPE),
                            ]);
                        } else {
                            fields.push(("lastTradeDate", strings::LAST_TRADE_DATE));
                        }
                        for (i, (key, label)) in fields.iter().enumerate() {
                            let response = ui.trading_label(c.text(label));
                            let value = self.draft.entry((*key).into()).or_default();
                            let editable = *key != "contractId"
                                && (!provider || *key == "description")
                                && !(self.option
                                    && self.selected.is_some()
                                    && ["symbol", "contractMonth", "strikePrice", "optionType"]
                                        .contains(key));
                            ui.add_enabled_ui(editable, |ui| {
                                if let Some(choices) =
                                    s.result.choices.get(*key).filter(|_| matches)
                                {
                                    egui::ComboBox::from_id_salt((*key, self.option))
                                        .selected_text(&*value)
                                        .width(160.0)
                                        .show_ui(ui, |ui| {
                                            for choice in choices {
                                                ui.selectable_value(value, choice.clone(), choice);
                                            }
                                        })
                                        .response
                                        .labelled_by(response.id);
                                } else {
                                    ui.add(egui::TextEdit::singleline(value).desired_width(170.0))
                                        .labelled_by(response.id);
                                }
                            });
                            if i % 2 == 1 {
                                ui.end_row();
                            }
                        }
                        ui.end_row();
                    });
                if !self.option {
                    ui.horizontal(|ui| {
                        for (key, label) in [
                            ("onTheRun", strings::ON_THE_RUN),
                            ("rollover", strings::ROLLOVER),
                        ] {
                            let mut value = self
                                .draft
                                .get(key)
                                .is_some_and(|x| x.eq_ignore_ascii_case("true"));
                            ui.add_enabled(
                                !provider || key == "onTheRun",
                                egui::Checkbox::new(&mut value, c.text(label)),
                            );
                            self.draft.insert(key.into(), value.to_string());
                        }
                    });
                }
                let valid = self
                    .draft
                    .get("symbol")
                    .is_some_and(|x| !x.trim().is_empty())
                    && self
                        .draft
                        .get(if self.option {
                            "contractMonth"
                        } else {
                            "lastTradeDate"
                        })
                        .is_some_and(|x| chrono::NaiveDate::parse_from_str(x, "%Y-%m-%d").is_ok());
                ui.horizontal(|ui| {
                    if ui
                        .add_enabled(
                            matches && provider && self.selected.is_some(),
                            egui::Button::new(c.text(strings::REFERENCE_REVIEW)),
                        )
                        .clicked()
                    {
                        self.review_open = true;
                        self.confirm_review = false;
                    }
                    if ui
                        .add_enabled(
                            matches && valid && (provider || manual_ready),
                            egui::Button::new(c.text(if self.selected.is_some() {
                                strings::SAVE_CONTRACT
                            } else {
                                strings::ADD_CONTRACT
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
                            egui::Button::new(c.text(strings::REMOVE_CONTRACT)),
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
                    ui.trading_label(c.text(strings::REMOVE_CONTRACT_WARNING));
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
            if matches {
                egui_extras::TableBuilder::new(ui)
                    .id_salt("contract-table")
                    .striped(true)
                    .resizable(true)
                    .column(egui_extras::Column::auto())
                    .column(egui_extras::Column::auto())
                    .column(egui_extras::Column::auto())
                    .column(egui_extras::Column::remainder())
                    .header(24.0, |mut header| {
                        for label in [
                            strings::CONTRACT_ID,
                            strings::SYMBOL,
                            if self.option {
                                strings::CONTRACT_MONTH
                            } else {
                                strings::LAST_TRADE_DATE
                            },
                            strings::DESCRIPTION,
                        ] {
                            header.col(|ui| {
                                ui.strong(c.text(label));
                            });
                        }
                    })
                    .body(|body| {
                        body.rows(26.0, s.result.rows.len(), |mut row| {
                            self.drawn_rows += 1;
                            let r = &s.result.rows[row.index()];
                            row.col(|ui| {
                                if ui
                                    .add_enabled(
                                        enabled,
                                        egui::Button::new(
                                            r.fields
                                                .get("contractId")
                                                .map(String::as_str)
                                                .unwrap_or(&r.id),
                                        )
                                        .selected(self.selected.as_ref() == Some(&r.id)),
                                    )
                                    .clicked()
                                {
                                    self.selected = Some(r.id.clone());
                                    self.draft = r.fields.clone();
                                    self.confirm_remove = false;
                                }
                            });
                            for key in [
                                "symbol",
                                if self.option {
                                    "contractMonth"
                                } else {
                                    "lastTradeDate"
                                },
                                "description",
                            ] {
                                row.col(|ui| {
                                    ui.trading_label(r.fields.get(key).map(String::as_str).unwrap_or(""));
                                });
                            }
                        });
                    });
            }
        });
        if self.review_open {
            let enabled = live && !s.busy && self.pending.is_none() && matches;
            egui::Window::new(c.text(strings::REFERENCE_REVIEW))
                .id(egui::Id::new("contract-conventions"))
                .collapsible(false)
                .resizable(true)
                .default_width(540.)
                .show(ctx, |ui| {
                    ui.trading_label(self.selected.as_deref().unwrap_or(""));
                    ui.add_enabled_ui(enabled, |ui| {
                        egui::ScrollArea::vertical()
                            .max_height(420.)
                            .show(ui, |ui| {
                                egui::Grid::new("convention-fields")
                                    .striped(true)
                                    .show(ui, |ui| {
                                        for key in [
                                            "reviewState",
                                            "mappingVersion",
                                            "evidenceId",
                                            "effectiveFromUtc",
                                            "effectiveUntilUtc",
                                            "exchangeTimeZoneId",
                                            "calendarVersion",
                                            "lastTradingUtc",
                                            "exerciseCutoffUtc",
                                            "exerciseStyle",
                                            "settlementStyle",
                                            "premiumStyle",
                                            "premiumTickRule",
                                            "tickRuleVersion",
                                            "dayCount",
                                            "underlyingContractId",
                                            "exerciseResultContractId",
                                        ] {
                                            let caption = format!("review_{key}");
                                            let label = ui.trading_label(c.text(&caption));
                                            let value = self.draft.entry(key.into()).or_default();
                                            ui.add_enabled_ui(
                                                key != "underlyingContractId",
                                                |ui| {
                                                    if let Some(choices) = s.result.choices.get(key)
                                                    {
                                                        egui::ComboBox::from_id_salt((
                                                            "convention",
                                                            key,
                                                        ))
                                                        .selected_text(&*value)
                                                        .show_ui(ui, |ui| {
                                                            for choice in choices {
                                                                ui.selectable_value(
                                                                    value,
                                                                    choice.clone(),
                                                                    choice,
                                                                );
                                                            }
                                                        })
                                                        .response
                                                        .labelled_by(label.id);
                                                    } else {
                                                        ui.add(
                                                            egui::TextEdit::singleline(value)
                                                                .desired_width(290.)
                                                                .min_size(egui::vec2(290., 20.)),
                                                        )
                                                        .labelled_by(label.id);
                                                    }
                                                },
                                            );
                                            ui.end_row();
                                        }
                                    });
                            });
                        if !self.confirm_review
                            && ui.button(c.text(strings::REFERENCE_REVIEW_SAVE)).clicked()
                        {
                            self.confirm_review = true;
                        }
                        if self.confirm_review {
                            ui.trading_label(c.text(strings::REFERENCE_REVIEW_WARNING));
                            if ui
                                .button(c.text(strings::REFERENCE_REVIEW_CONFIRM))
                                .clicked()
                            {
                                self.review_open = false;
                                self.confirm_review = false;
                                action = Some("review");
                            }
                        }
                    });
                    if ui.button(c.text(strings::CANCEL)).clicked() {
                        self.review_open = false;
                        self.confirm_review = false;
                        self.draft = self
                            .selected
                            .as_ref()
                            .and_then(|id| s.result.rows.iter().find(|r| &r.id == id))
                            .map(|r| r.fields.clone())
                            .unwrap_or_default();
                    }
                });
        }
        action.map(|action| self.request(action, s))
    }
}
