use crate::wire::WireNumber as _;
use crate::presentation::TradingLabelUi as _;
use super::order_layout::{at, rect};
use super::*;
fn context<'a>(s: &'a ContractPublication, key: &str) -> &'a str {
    s.result.context.get(key).map(String::as_str).unwrap_or("")
}
fn value(s: &ContractPublication, kind: &str) -> serde_json::Value {
    s.result
        .rows
        .iter()
        .find(|r| r.fields.get("kind").is_some_and(|k| k == kind))
        .map(data)
        .unwrap_or_default()
}
pub(super) const OPTION_COLUMNS: [&str; 13] = [
    "Selected", "Delta", "OI", "Vol", "Bid", "Ask", "Strike", "Bid", "Ask", "Vol", "OI", "Delta", "Selected",
];
pub(super) fn option_header(ui: &mut egui::Ui, label: &str, center_selected: bool) {
    ui.painter().rect_filled(ui.max_rect(), 0., crate::presentation::COMMAND);
    if label == "Strike" || (center_selected && label == "Selected") {
        ui.add_sized(ui.available_size(), crate::presentation::label_widget(label).halign(egui::Align::Center).truncate());
    } else {
        ui.add(crate::presentation::label_widget(label).truncate());
    }
}
pub(super) fn strike_cell(ui: &mut egui::Ui, strike: &str, color: Color32) {
    ui.add_sized(ui.available_size(), crate::presentation::label_widget(
        egui::RichText::new(strike).color(color).strong()).halign(egui::Align::Center).truncate());
}
pub(super) fn option_cell(v: &serde_json::Value, key: &str) -> String {
    let Some(number) = v[key].wire_number() else {
        return if v[key].is_null() {
            "-".into()
        } else {
            text(v, key)
        };
    };
    match key {
        "delta" => {
            if number == 0. {
                "0.000".into()
            } else {
                format!("{number:+.3}")
            }
        }
        "openInterest" | "volume" => {
            if number >= 1_000_000. {
                format!("{:.1}M", number / 1_000_000.)
            } else if number >= 1_000. {
                format!("{:.1}K", number / 1_000.)
            } else {
                format!("{number:.0}")
            }
        }
        "bid" | "ask" | "strike" => format!("{number:.2}"),
        _ => text(v, key),
    }
}
// Position caption/value glyphs on the same text line, independent of control padding.
fn market_header_text(ui: &mut egui::Ui, bounds: egui::Rect, text: &str, align: egui::Align) {
    market_header_text_colored(ui, bounds, text, align, None);
}
fn market_header_text_colored(ui: &mut egui::Ui, bounds: egui::Rect, text: &str, align: egui::Align, color: Option<Color32>) {
    let font = egui::TextStyle::Body.resolve(ui.style());
    let height = ui.fonts(|fonts| fonts.row_height(&font));
    let text_bounds = egui::Rect::from_min_size(
        egui::pos2(bounds.left(), bounds.center().y - height / 2.),
        egui::vec2(bounds.width(), height),
    );
    let mut rich = egui::RichText::new(text).font(font);
    if let Some(color) = color { rich = rich.color(color); }
    let label = if color.is_some() {
        egui::Label::new(rich)
    } else {
        crate::presentation::label_widget(rich)
    }.halign(align);
    ui.place(text_bounds, if align == egui::Align::RIGHT {
        label.wrap_mode(egui::TextWrapMode::Extend)
    } else {
        label.truncate()
    });
}
impl TradeWorkspace {
    fn display(&mut self, ui: &mut egui::Ui, r: egui::Rect, id: &str, value: &str) {
        at(ui, r, id, |ui| {
            ui.painter().rect_filled(r, 0., Color32::BLACK);
            ui.painter().rect_stroke(
                r,
                0.,
                egui::Stroke::new(1. / ui.ctx().pixels_per_point(), Color32::from_gray(80)),
                egui::StrokeKind::Inside,
            );
            ui.add(crate::presentation::label_widget(egui::RichText::new(value).color(Color32::WHITE)).truncate());
        });
        self.controls.insert(id.into(), r);
    }
    fn header_display(&mut self, ui: &mut egui::Ui, bounds: egui::Rect, id: &str, value: &str) {
        at(ui, bounds, id, |ui| {
            ui.painter().rect_filled(bounds, 0., Color32::BLACK);
            ui.painter().rect_stroke(bounds, 0.,
                egui::Stroke::new(1. / ui.ctx().pixels_per_point(), Color32::from_gray(80)),
                egui::StrokeKind::Inside);
            market_header_text(ui, bounds, value, egui::Align::LEFT);
        });
        self.controls.insert(id.into(), bounds);
    }
    fn choice(
        &mut self,
        ui: &mut egui::Ui,
        r: egui::Rect,
        key: &str,
        values: &[&str],
        enabled: bool,
    ) -> bool {
        let selected = self
            .draft
            .entry(key.into())
            .or_insert_with(|| values.first().copied().unwrap_or("").into());
        let old = selected.clone();
        at(ui, r, key, |ui| {
            ui.add_enabled_ui(enabled, |ui| {
                ui.spacing_mut().interact_size.y = r.height();
                ui.visuals_mut().widgets.inactive.weak_bg_fill = Color32::BLACK;
                let response = egui::ComboBox::from_id_salt(key)
                    .width(r.width())
                    .selected_text(&*selected)
                    .show_ui(ui, |ui| {
                        for v in values {
                            ui.selectable_value(selected, (*v).into(), *v);
                        }
                    });
                response.response.widget_info(|| {
                    egui::WidgetInfo::labeled(
                        egui::WidgetType::ComboBox,
                        enabled,
                        format!("{key}: {selected}"),
                    )
                });
            });
        });
        self.controls.insert(key.into(), r);
        old != *selected
    }
    pub(super) fn draw_blotter(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
    ) -> Option<&'static str> {
        let p = ui.ctx().pixels_per_point();
        let parent_o = ui.cursor().min;
        let o = parent_o + egui::vec2(0., 50.) / p;
        let w = ui.available_width() * p;
        let h = ui.available_height() * p - 50.;
        let mut action = None;
        self.controls.insert(
            "blotter-client".into(),
            egui::Rect::from_min_size(o, egui::vec2(w, h) / p),
        );
        let selected_type = s
            .result
            .rows
            .iter()
            .find(|r| r.id == format!("trade:{}", context(s, "tradeId")))
            .map(|r| text(&data(r), "tradeType"))
            .unwrap_or_default();
        let kind = if context(s, "compositionType").is_empty() {
            selected_type.as_str()
        } else {
            context(s, "compositionType")
        };
        let iron = kind.contains("IronCondor");
        let future = kind == "FuturesOutright";
        let editable = !flag(s, "submissionUnconfirmed") && flag(s, "editable") && self.interaction_ready(s);
        let trade_key = context(s, "tradeId");
        if self.draft.get("blotterTrade").map(String::as_str) != Some(trade_key) {
            for key in [
                "submitted",
                "previewKey",
                "quantity",
                "price",
                "orderType",
                "algorithm",
                "timeInForce",
                "pace",
                "live",
                "expiry",
                "parentOrderAction",
                "callDelta",
                "putDelta",
                "callWidth",
                "putWidth",
                "displayEffectiveDate",
                "chainSelectionAnchor",
            ] {
                self.draft.remove(key);
            }
            self.deferred_action = None;
            self.draft.insert("blotterTrade".into(), trade_key.into());
            self.compose_tab = 0;
        }
        let session = context(s, "composerSessionId");
        if !session.is_empty()
            && self.draft.get("composerSessionId").map(String::as_str) != Some(session)
        {
            for key in [
                "quantity",
                "price",
                "expiry",
                "live",
                "callDelta",
                "putDelta",
                "callWidth",
                "putWidth",
                "orderType",
                "algorithm",
                "timeInForce",
                "pace",
                "action",
                "parentOrderAction",
                "submitted",
                "previewKey",
            ] {
                self.draft.remove(key);
            }
            self.draft
                .insert("composerSessionId".into(), session.into());
        }
        let key = context(s, "previewKey");
        if self.draft.get("previewKey").map(String::as_str) != Some(key) {
            self.draft.insert("previewKey".into(), key.into());
            self.draft
                .insert("price".into(), context(s, "price").into());
        }
        for (key, default) in [
            ("quantity", "1"),
            ("callDelta", "16"),
            ("putDelta", "16"),
            ("callWidth", "50"),
            ("putWidth", "50"),
            ("expiry", ""),
            ("live", "false"),
            ("orderType", "Limit"),
            ("algorithm", "None"),
            ("timeInForce", "Day"),
            ("pace", "Normal"),
            ("action", "Open"),
        ] {
            let initial = if context(s, key).is_empty() {
                default
            } else {
                context(s, key)
            };
            self.draft.entry(key.into()).or_insert(initial.into());
        }
        for (i, (label, x, width, v)) in [
            ("Trade Id:", 0., 124., trade_key),
            ("Effective Date:", 320., 118., context(s, "effectiveDate")),
            ("Order Action:", 687., 110., context(s, "executionAction")),
        ].into_iter().enumerate() {
            at(ui, rect(parent_o, p, x, 9., width, 27.), label, |ui| {
                market_header_text(ui, ui.max_rect(), label, egui::Align::RIGHT);
            });
            if i == 1 {
                let initial = context(s, "effectiveDate");
                let stored = self
                    .draft
                    .entry("displayEffectiveDate".into())
                    .or_insert(initial.into());
                if stored.is_empty() {
                    *stored = initial.into();
                }
                at(
                    ui,
                    rect(parent_o, p, x + width + 10., 11.5, 217., 22.),
                    "dtpTradeDate",
                    |ui| {
                        if let Ok(mut date) = chrono::NaiveDate::parse_from_str(stored, "%Y-%m-%d")
                        {
                            ui.add_enabled_ui(editable, |ui| {
                                ui.add_sized(
                                    ui.available_size(),
                                    egui_extras::DatePickerButton::new(&mut date)
                                        .id_salt("blotter-effective-date")
                                        .calendar_week(false),
                                );
                            });
                            *stored = date.to_string();
                        } else {
                            market_header_text(ui, ui.max_rect(), initial, egui::Align::LEFT);
                        }
                    },
                );
            } else if i == 2 {
                if self.choice(
                    ui,
                    rect(parent_o, p, x + width + 10., 9., 171., 27.),
                    "parentOrderAction",
                    &["Open", "Close"],
                    editable,
                ) {
                    self.draft.insert(
                        "action".into(),
                        self.draft
                            .get("parentOrderAction")
                            .cloned()
                            .unwrap_or("Open".into()),
                    );
                    action = Some("preview-draft");
                }
            } else {
                self.header_display(
                    ui,
                    rect(
                        parent_o,
                        p,
                        x + width + 10.,
                        9.5,
                        if i == 0 {
                            166.
                        } else if i == 1 {
                            217.
                        } else {
                            171.
                        },
                        26.,
                    ),
                    label,
                    v,
                );
            }
        }
        at(
            ui,
            rect(parent_o, p, 1004., 8.5, 112., 28.),
            "live-feed",
            |ui| {
                let mut checked = self.draft.get("live").is_some_and(|v| v == "true");
                let r = ui.max_rect();
                ui.painter().rect_filled(
                    r,
                    0.,
                    if !editable {
                        Color32::from_gray(74)
                    } else if checked {
                        Color32::from_rgb(38, 142, 65)
                    } else {
                        Color32::from_rgb(150, 42, 42)
                    },
                );
                ui.visuals_mut().override_text_color = Some(Color32::BLACK);
                if ui
                    .add_enabled(editable, egui::Checkbox::new(&mut checked, "Live Feed"))
                    .changed()
                {
                    self.draft.insert("live".into(), checked.to_string());
                    action = Some("chain");
                }
            },
        );
        for (label, x, width, v, value_width) in [
            (
                "Trade Strategy:",
                0.,
                125.,
                if iron {
                    "Iron Condor"
                } else if future {
                    "Futures Outright"
                } else {
                    "Vertical Spread"
                },
                164.,
            ),
            ("Trade Type:", 320., 119., kind, 219.),
            ("Broker Mode:", 687., 113., context(s, "brokerMode"), 169.),
        ] {
            at(ui, rect(o, p, x, 10., width, 30.), label, |ui| {
                market_header_text(ui, ui.max_rect(), label, egui::Align::RIGHT);
            });
            self.header_display(
                ui,
                rect(o, p, x + width + 10., 10., value_width, 30.),
                label,
                v,
            );
        }
        at(
            ui,
            rect(o, p, 3., 44., w - 6., 28.),
            "sourceModeLabel",
            |ui| {
                ui.trading_label(format!(
                    "Manual submitted / evidence; Trade {}; State {}",
                    context(s, "tradeId"),
                    context(s, "tradeState")
                ));
            },
        );
        ui.painter().line_segment(
            [o + egui::vec2(4., 74.) / p, o + egui::vec2(w - 4., 74.) / p],
            egui::Stroke::new(1. / p, Color32::from_gray(90)),
        );
        at(ui, rect(o, p, 3., 78., w - 6., 27.), "blotter-tabs", |ui| {
            ui.horizontal(|ui| {
                for (i, title) in [
                    "Market Selection",
                    "Broker Trade",
                    "Order Fills",
                    "Leg Staging",
                    "Orders and Fills",
                ]
                .iter()
                .enumerate()
                {
                    if i >= 3 && iron {
                        continue;
                    }
                    let response = crate::presentation::tab_value(ui, &mut self.compose_tab, i, *title);
                    self.controls.insert(format!("tab:{title}"), response.rect);
                    if response.clicked() && i >= 2 {
                        action = Some("refresh-execution");
                    }
                }
            });
        });
        if context(s, "submitted") == "True"
            && self.draft.get("submitted").map(String::as_str) != Some("True")
        {
            self.compose_tab = 2;
            self.draft.insert("submitted".into(), "True".into());
        }
        if self.compose_tab == 0 {
            if !future {
                action = self.market_panel(ui, s, o, p, w, h, editable).or(action);
            } else {
                self.display(
                    ui,
                    rect(o, p, 20., 125., w - 40., 28.),
                    "futures-selection",
                    context(s, "underlying"),
                );
            }
        } else if self.compose_tab == 1 {
            action = self.broker_panel(ui, s, o, p, w, h, editable).or(action);
        } else if self.compose_tab == 3 {
            at(
                ui,
                rect(o, p, 20., 123., w - 40., h - 180.),
                "leg-staging",
                |ui| {
                    self.staging_grid(ui, s);
                },
            );
            for (x, width, label, intent) in [
                (w - 1090., 160., "Clear Staged Legs", "clear-staged"),
                (w - 920., 180., "Recalculate / Validate", "preview-draft"),
                (w - 340., 155., "Submit Opening Order", "submit-order"),
                (w - 175., 155., "Submit Closing Order", "submit-order"),
            ] {
                at(ui, rect(o, p, x, h - 45., width, 29.), label, |ui| {
                    let submit = intent == "submit-order";
                    let correct_mode = !submit
                        || context(s, "executionAction")
                            == if label == "Submit Closing Order" {
                                "Close"
                            } else {
                                "Open"
                            };
                    if ui
                        .add_enabled(
                            editable && correct_mode && (!submit || flag(s, "canPlaceOrder")),
                            egui::Button::new(label).min_size(ui.available_size()),
                        )
                        .clicked()
                    {
                        action = Some(intent);
                    }
                });
            }
            for (x, caption, key, choices) in [
                (
                    w - 730.,
                    "Algorithm",
                    "algorithm",
                    &["None", "Adaptive"][..],
                ),
                (
                    w - 530.,
                    "Order Type",
                    "orderType",
                    &["Limit", "Market"][..],
                ),
            ] {
                at(ui, rect(o, p, x, h - 45., 80., 27.), caption, |ui| {
                    ui.trading_label(caption);
                });
                if self.choice(
                    ui,
                    rect(o, p, x + 80., h - 45., 105., 27.),
                    key,
                    choices,
                    editable,
                ) {
                    action = Some("preview-draft");
                }
            }
        } else if self.compose_tab == 4 {
            at(
                ui,
                rect(o, p, 20., 123., w - 40., h - 190.),
                "ordersAndFillsEvidence",
                |ui| {
                    self.evidence_panel(ui, s);
                },
            );
            at(
                ui,
                rect(o, p, 20., h - 52., w - 40., 35.),
                "orders-evidence-actions",
                |ui| {
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center),|ui|{if ui.add_enabled(flag(s,"canEndOfDay"),egui::Button::new("End Of Day...")).clicked(){self.dialog="eod".into();action=Some("eod-open");}ui.trading_label("Cancel/replace is enabled only by revision-bound application workflow capability.");});
                },
            );
        } else {
            at(
                ui,
                rect(o, p, 19., 116., w - 38., h - 128.),
                "execution-panel",
                |ui| {
                    action = self.draw_execution(ui, s).or(action);
                },
            );
        }
        // Poll off the render thread through the serialized managed gateway. Preserve local drafts.
        let now = ui.ctx().input(|i| i.time);
        if action.is_none()
            && !s.busy
            && self.pending.is_none()
            && self.deferred_action.is_none()
            && (self.compose_tab >= 2 || !ui.ctx().wants_keyboard_input())
            && now - self.last_blotter_refresh >= 1.
        {
            if self.compose_tab >= 2 && self.compose_tab != 3 {
                action = Some("refresh-execution");
            } else if self.draft.get("live").is_some_and(|v| v == "true") {
                action = Some("refresh-chain");
            }
            if action.is_some() {
                self.last_blotter_refresh = now;
            }
        }
        action
    }
    fn market_panel(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        o: egui::Pos2,
        p: f32,
        w: f32,
        h: f32,
        editable: bool,
    ) -> Option<&'static str> {
        let o = o - egui::vec2(4. / p, 0.);
        let w = w + 8.;
        let mut action = None;
        let market = value(s, "market");
        let metrics = value(s, "market-metrics");
        // Add ten physical pixels between successive header groups.
        let xs = [166., 387., 643., 907., 1082., 1360.];
        let widths = [130., 86., 141., 64., 115., 88.];
        let labels = [151., 65., 146., 100., 87., 100.];
        at(
            ui,
            rect(o, p, 11., 109., w - 22., 116.),
            "market-information",
            |ui| {
                ui.painter()
                    .rect_filled(ui.max_rect(), 0., Color32::from_rgb(20, 20, 20));
            },
        );
        let row1 = [
            ("EXPIRY:", "expiry"),
            ("DTE:", "DTE"),
            ("UNDERLYING:", "Underlying"),
            ("MULTIPLIER:", "Multiplier"),
            ("TICK:", "Tick"),
            ("SETTLEMENT:", "Settlement"),
        ];
        let row2 = [
            ("DAILY STD DEV:", "Daily Std Dev"),
            ("LAST:", "Last"),
            ("Daily Price Change:", "Daily Price Change"),
            ("IV:", "IV"),
            ("IV RANK:", "IV Rank"),
            ("QUOTE AGE:", "Quote Age"),
        ];
        for (row, items) in [row1, row2].iter().enumerate() {
            for (i, (caption, key)) in items.iter().enumerate() {
                let y = 116. + row as f32 * 30.;
                at(
                    ui,
                    rect(o, p, xs[i] - labels[i], y, labels[i] - 10., 27.),
                    caption,
                    |ui| {
                        market_header_text_colored(ui, ui.max_rect(), caption, egui::Align::RIGHT, Some(crate::presentation::CAPTION_FOREGROUND));
                    },
                );
                let r = rect(o, p, xs[i], y, widths[i], 27.);
                if *key == "expiry" {
                    let values = s.result.choices.get("expiry").cloned().unwrap_or_default();
                    let values: Vec<_> = values.iter().map(String::as_str).collect();
                    if self.choice(ui, r, "expiry", &values, editable) {
                        action = Some("chain");
                    }
                } else {
                    at(ui, r, key, |ui| {
                        ui.painter().rect_filled(r, 0., Color32::BLACK);
                        ui.painter().rect_stroke(r, 0., egui::Stroke::new(1. / p, Color32::from_gray(80)), egui::StrokeKind::Inside);
                        market_header_text(ui, r, &text(&metrics, key), egui::Align::LEFT);
                    });
                    self.controls.insert((*key).into(), r);
                }
            }
        }
        let iron = context(s, "compositionType").contains("IronCondor");
        for (i, (caption, key, default)) in [
            (
                if iron {
                    "SHORT CALL DELTA:"
                } else {
                    "SHORT LEG DELTA:"
                },
                "callDelta",
                "16",
            ),
            ("WIDTH:", "callWidth", "50"),
            ("SHORT PUT DELTA:", "putDelta", "16"),
            ("WIDTH:", "putWidth", "50"),
        ]
        .iter()
        .enumerate()
        {
            if !iron && i >= 2 {
                continue;
            }
            at(
                ui,
                rect(o, p, xs[i] - labels[i], 176., labels[i] - 10., 27.),
                caption,
                |ui| {
                    market_header_text_colored(ui, ui.max_rect(), caption, egui::Align::RIGHT, Some(crate::presentation::CAPTION_FOREGROUND));
                },
            );
            let r = rect(
                o,
                p,
                xs[i] + if i == 0 { 2. } else { 0. },
                178.5,
                widths[i] - if i == 0 { 4. } else { 0. },
                22.,
            );
            let before = self.draft.get(*key).cloned();
            at(ui, r, key, |ui| {
                ui.add_enabled_ui(editable, |ui| {
                    self.numeric(
                        ui,
                        key,
                        default,
                        if key.ends_with("Delta") { 1. } else { 0.01 },
                        if key.ends_with("Delta") {
                            100.
                        } else {
                            100000.
                        },
                        1.,
                        if key.ends_with("Delta") { 0 } else { 2 },
                    );
                });
            });
            self.controls.insert((*key).into(), r);
            if before != self.draft.get(*key).cloned() {
                if !iron {
                    self.draft.insert(
                        "putDelta".into(),
                        self.draft.get("callDelta").unwrap().clone(),
                    );
                    self.draft.insert(
                        "putWidth".into(),
                        self.draft.get("callWidth").unwrap().clone(),
                    );
                }
                action = Some("chain");
            }
        }
        at(
            ui,
            rect(o, p, xs[4] - labels[4], 176., labels[4] - 10., 27.),
            "liquidity-label",
            |ui| {
                market_header_text_colored(ui, ui.max_rect(), "LIQUIDITY:", egui::Align::RIGHT, Some(crate::presentation::CAPTION_FOREGROUND));
            },
        );
        let liquidity = text(&market, "windowMethod");
        self.draft
            .insert("liquiditySelector".into(), liquidity.clone());
        self.choice(
            ui,
            rect(o, p, xs[4], 176., widths[4], 27.),
            "liquiditySelector",
            &[liquidity.as_str()],
            editable,
        );
        let market_viewport = rect(o, p, 14., 233., w - 28., (h - 233. - 195.).max(100.));
        at(
            ui,
            market_viewport,
            "marketSelectionGrid",
            |ui| {
                ui.set_clip_rect(ui.clip_rect().intersect(market_viewport.shrink(1. / p)));
                action = ui
                    .add_enabled_ui(editable, |ui| self.option_grid(ui, s, &market))
                    .inner
                    .or(action);
            },
        );
        let grid_bounds = rect(o, p, 14., 261., w - 28., (h - 261. - 195.).max(100.));
        ui.painter().rect_stroke(
            grid_bounds,
            0.,
            egui::Stroke::new(1. / p, crate::presentation::BORDER),
            egui::StrokeKind::Inside,
        );
        self.controls.insert("marketSelectionGrid".into(), grid_bounds);
        at(
            ui,
            rect(o, p, 14., h - 191., w - 28., 26.),
            "selectionStatusLabel",
            |ui| {
                let count = s
                    .result
                    .rows
                    .iter()
                    .filter(|r| r.fields.get("kind").is_some_and(|k| k == "leg"))
                    .count();
                ui.trading_label(if context(s, "composerStatus").is_empty() {
                    format!(
                        "Selected legs: {count} / {}     Click Call or Put side to stage a leg",
                        if iron { 4 } else { 2 }
                    )
                } else {
                    context(s, "composerStatus").into()
                });
            },
        );
        at(
            ui,
            rect(o, p, 14., h - 161., w - 28., 150.),
            "selectedStrategyLegsGrid",
            |ui| {
                let bounds = ui.max_rect().shrink(1. / p);
                ui.set_clip_rect(ui.clip_rect().intersect(bounds));
                self.selected_grid(ui, s);
            },
        );
        ui.painter().rect_stroke(
            rect(o, p, 14., h - 161., w - 28., 150.),
            0., egui::Stroke::new(1. / p, crate::presentation::BORDER), egui::StrokeKind::Inside,
        );
        for (alias, key) in [
            ("expirationSelector", "expiry"),
            ("shortCallDelta", "callDelta"),
            ("shortPutDelta", "putDelta"),
            ("callSpreadWidth", "callWidth"),
            ("putSpreadWidth", "putWidth"),
        ] {
            if let Some(r) = self.controls.get(key).copied() {
                self.controls.insert(alias.into(), r);
            }
        }
        self.controls.insert(
            "selectedStrategyLegsGrid".into(),
            rect(o, p, 14., h - 161., w - 28., 150.),
        );
        action
    }
    fn broker_panel(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        o: egui::Pos2,
        p: f32,
        w: f32,
        h: f32,
        editable: bool,
    ) -> Option<&'static str> {
        let o = o - egui::vec2(4. / p, 0.);
        let w = w + 8.;
        let mut action = None;
        let preview = value(s, "broker-preview");
        let fields = &preview["fields"];
        let leg_grid_bounds = rect(o, p, 26., 123., w - 52., 136.);
        at(
            ui,
            leg_grid_bounds,
            "brokerPreviewLegGrid",
            |ui| {
                // Anchor the border to the assigned viewport, never to measured cell content.
                let bounds = leg_grid_bounds;
                ui.scope_builder(egui::UiBuilder::new().max_rect(bounds.shrink(1. / p)), |ui| {
                    ui.set_clip_rect(ui.clip_rect().intersect(bounds.shrink(1. / p)));
                    if self.leg_grid(ui, s, editable, 134. / p) {
                        action = Some("preview-draft");
                    }
                });
                ui.painter().rect_stroke(bounds, 0., egui::Stroke::new(1. / p,
                    Color32::from_rgb(80, 80, 84)), egui::StrokeKind::Inside);
            },
        );
        self.controls.insert(
            "brokerPreviewLegGrid".into(),
            rect(o, p, 26., 123., w - 52., 136.),
        );
        let rows: Vec<Vec<(&str, &str, f32)>> = vec![
            vec![
                ("Contracts", "quantity", 86.),
                ("Order type", "orderType", 105.),
                ("Time in force", "timeInForce", 115.),
                ("Action", "action", 95.),
                ("Directed venue", "", 100.),
            ],
            vec![
                ("Net bid", "", 90.),
                ("Net mid", "", 90.),
                ("Net ask", "", 90.),
                ("OrderPrice", "price", 126.),
                ("Combo tick", "", 100.),
            ],
            vec![
                ("IFM algorithm", "algorithm", 125.),
                ("Pace", "pace", 125.),
                ("Broker route", "", 190.),
                ("SMART", "", 75.),
                ("Worst approved limit", "", 150.),
            ],
            vec![
                ("Est. credit", "", 105.),
                ("Max profit", "", 110.),
                ("Max loss", "", 105.),
                ("Buying power", "", 120.),
                ("Est. fees", "", 95.),
                ("Fund capacity", "", 120.),
            ],
            vec![
                ("Put width", "", 90.),
                ("Call width", "", 90.),
                ("Net delta", "", 95.),
                ("Net vega", "", 95.),
                ("Fund risk limit", "", 130.),
                ("Validation", "", 150.),
            ],
            vec![
                ("Available funds", "", 120.),
                ("Reserved capital", "", 120.),
                ("Risk margin", "", 115.),
                ("Max return", "", 105.),
                ("Minimum profit", "", 120.),
                ("Put spread", "", 105.),
                ("Call spread", "", 105.),
            ],
            vec![
                ("Value source", "", 210.),
                ("As of", "", 135.),
                ("Route check", "", 175.),
                ("Risk approval", "", 145.),
            ],
        ];
        for (index, row) in rows.iter().enumerate() {
            let y = 279. + index as f32 * 69. + if index >= 3 { 27. } else { 0. };
            let total: f32 = row.iter().map(|(_, _, v)| v).sum();
            let budget = w - 58. - 10. * (row.len() - 1) as f32;
            let mut x = 29.;
            for (caption, key, weight) in row {
                let width = (budget * weight / total).round();
                // Keep captions just above their fields on a shared text baseline.
                at(ui, rect(o, p, x, y + 17., width, 17.), caption, |ui| {
                    market_header_text(ui, ui.max_rect(), &format!("{caption}:"), egui::Align::LEFT);
                });
                let r = rect(
                    o,
                    p,
                    x,
                    y + if *key == "quantity" { 37. } else { 36. },
                    width,
                    if *key == "quantity" { 24. } else { 30. },
                );
                let old = self.draft.get(*key).cloned();
                match *key {
                    "quantity" => {
                        at(ui, r, key, |ui| {
                            ui.add_enabled_ui(editable, |ui| {
                                self.numeric(ui, "quantity", "1", 1., 100000., 1., 0)
                            });
                        });
                    }
                    "price" => {
                        at(ui, r, key, |ui| {
                            ui.visuals_mut().extreme_bg_color = Color32::YELLOW;
                            ui.visuals_mut().override_text_color = Some(Color32::BLACK);
                            ui.add_enabled_ui(editable, |ui| {
                                ui.add_sized(
                                    r.size(),
                                    egui::TextEdit::singleline(
                                        self.draft.entry("price".into()).or_default(),
                                    )
                                    .text_color(Color32::BLACK)
                                    .desired_width(r.width()),
                                )
                            });
                        });
                    }
                    "orderType" | "timeInForce" | "algorithm" | "pace" | "action" => {
                        let choices: &[&str] = match *key {
                            "orderType" => &["Limit", "Market"],
                            "timeInForce" => &["Day", "GTC"],
                            "algorithm" => &["None", "Adaptive"],
                            "pace" => &["Patient", "Normal", "Urgent"],
                            _ => {
                                if context(s, "executionAction") == "Close" {
                                    &["Close"]
                                } else {
                                    &["Open"]
                                }
                            }
                        };
                        if *key == "pace" {
                            self.draft.entry("pace".into()).or_insert("Normal".into());
                        }
                        if self.choice(
                            ui,
                            r,
                            key,
                            choices,
                            editable
                                && (*key != "pace"
                                    || self.draft.get("algorithm").is_some_and(|v| v != "None")),
                        ) {
                            action = Some("preview-draft");
                        }
                    }
                    _ => {
                        let displayed = match *caption {
                            "Directed venue" => context(s, "venue").into(),
                            "Combo tick" => {
                                if context(s, "compositionType") == "FuturesOutright" {
                                    "0.25".into()
                                } else {
                                    "0.05".into()
                                }
                            }
                            "Broker route" => {
                                if context(s, "brokerMode") == "Emulator" {
                                    "InteractiveBrokersEmulator".into()
                                } else {
                                    "InteractiveBrokers".into()
                                }
                            }
                            "SMART" => "Not configured".into(),
                            _ => text(fields, caption),
                        };
                        self.display(
                            ui,
                            r,
                            caption,
                            if displayed.is_empty() {
                                "Unavailable"
                            } else {
                                &displayed
                            },
                        );
                    }
                }
                self.controls.insert(format!("broker-field-{caption}"), r);
                let id = match *key {
                    "quantity" => "brokerPreviewContractsValue".to_string(),
                    "price" => "brokerPreviewNetLimitTicks".into(),
                    "orderType" | "timeInForce" | "action" | "algorithm" | "pace" => {
                        format!("brokerPreview{}Selector", caption.replace(" ", ""))
                    }
                    _ => format!("brokerPreview{}Value", caption.replace(" ", "")),
                };
                self.controls.insert(id, r);
                if !key.is_empty() && old != self.draft.get(*key).cloned() {
                    action = Some("preview-draft");
                }
                x += width + 10.;
            }
        }
        at(
            ui,
            rect(o, p, 26., 486., w - 52., 27.),
            "broker-economics-header",
            |ui| {
                ui.painter().rect_filled(ui.max_rect(), 0., Color32::BLACK);
                ui.trading_label("Fund Economics and Limits:");
            },
        );
        at(
            ui,
            rect(o, p, w - 343., h - 52., 180., 29.),
            "manageBrokerQualification",
            |ui| {
                if ui
                    .add_sized(
                        ui.available_size(),
                        egui::Button::new(egui::RichText::new("Manage qualification...").size(12.))
                            .wrap_mode(egui::TextWrapMode::Extend),
                    )
                    .clicked()
                {
                    for key in [
                        "qualificationRevision",
                        "loadedReport",
                        "reviewer",
                        "reason",
                    ] {
                        self.draft.remove(key);
                    }
                    self.dialog = "qualification".into();
                    action = Some("account");
                }
            },
        );
        at(
            ui,
            rect(o, p, w - 159., h - 52., 130., 29.),
            "brokerPreviewPlaceOrder",
            |ui| {
                if ui
                    .add_enabled(
                        editable && flag(s, "canPlaceOrder"),
                        egui::Button::new("Place Order").min_size(egui::vec2(130., 29.) / p),
                    )
                    .clicked()
                {
                    action = Some("submit-order");
                }
            },
        );
        self.controls.insert(
            "brokerPreviewPlaceOrder".into(),
            rect(o, p, w - 159., h - 52., 130., 29.),
        );
        self.controls.insert(
            "manageBrokerQualification".into(),
            rect(o, p, w - 343., h - 52., 180., 29.),
        );
        action
    }
    pub(super) fn inspection_properties(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        fallback: &serde_json::Value,
    ) {
        let key = self
            .draft
            .get("fillId")
            .map(|id| format!("fill-properties:{id}"))
            .or_else(|| {
                self.draft
                    .get("brokerOrderId")
                    .map(|id| format!("broker-properties:{id}"))
            });
        let prepared = key
            .and_then(|key| s.result.rows.iter().find(|r| r.id == key))
            .map(data)
            .unwrap_or_default();
        let Some(properties) = prepared["properties"].as_array() else {
            Self::properties(ui, fallback);
            return;
        };
        let p = ui.ctx().pixels_per_point();
        let width = ui.available_width();
        let height = ui.available_height();
        let selected = self
            .draft
            .get("propertySelected")
            .cloned()
            .unwrap_or_default();
        egui_extras::TableBuilder::new(ui)
            .id_salt("broker-property-grid")
            .resizable(true)
            .sense(egui::Sense::click())
            .max_scroll_height(height)
            .column(egui_extras::Column::initial(width / 3.).at_least(60. / p))
            .column(egui_extras::Column::remainder())
            .body(|body| {
                body.rows(22. / p, properties.len(), |mut row| {
                    let item = &properties[row.index()];
                    let name = text(item, "name");
                    row.set_selected(selected == name);
                    for key in ["name", "display"] {
                        row.col(|ui| {
                            let response = ui.add(crate::presentation::label_widget(text(item, key)).truncate());
                            response.context_menu(|ui| {
                                if ui.button("Copy value").clicked() {
                                    ui.ctx().copy_text(text(item, "display"));
                                    ui.close();
                                }
                            });
                        });
                    }
                    if row.response().clicked() {
                        self.draft.insert("propertySelected".into(), name);
                    }
                });
            });
        if ui.input(|i| i.modifiers.command && i.key_pressed(egui::Key::C)) {
            if let Some(item) = properties.iter().find(|v| text(v, "name") == selected) {
                ui.ctx().copy_text(text(item, "display"));
            }
        }
    }
    fn staging_grid(&mut self, ui: &mut egui::Ui, s: &ContractPublication) {
        let p = ui.ctx().pixels_per_point();
        let legs: Vec<_> = s
            .result
            .rows
            .iter()
            .filter(|r| r.fields.get("kind").is_some_and(|k| k == "leg"))
            .map(data)
            .collect();
        let headers = [
            "Leg",
            "Delta",
            "Broker Side",
            "Contract",
            "Right",
            "Strike",
            "Bid",
            "Ask",
            "Quantity",
            "Validation / Review",
        ];
        let height = ui.available_height();
        let mut table = egui_extras::TableBuilder::new(ui)
            .id_salt("legStagingGrid")
            .resizable(true)
            .max_scroll_height(height);
        for _ in headers {
            table = table.column(egui_extras::Column::remainder());
        }
        table
            .header(25. / p, |mut row| {
                for header in headers {
                    row.col(|ui| {
                        ui.painter()
                            .rect_filled(ui.max_rect(), 0., Color32::from_rgb(45, 48, 54));
                        ui.trading_label(header);
                    });
                }
            })
            .body(|body| {
                body.rows(27. / p, legs.len(), |mut row| {
                    let v = &legs[row.index()];
                    for column in 0..10 {
                        row.col(|ui| {
                            ui.trading_label(match column {
                                0 => text(v, "role"),
                                1 => text(v, "delta"),
                                2 => {
                                    if text(v, "role").starts_with('-') {
                                        "SELL".into()
                                    } else {
                                        "BUY".into()
                                    }
                                }
                                3 => text(v, "contractId"),
                                4 => {
                                    if v["isFuture"] == true {
                                        "FUT".into()
                                    } else if v["isCall"] == true {
                                        "CALL".into()
                                    } else {
                                        "PUT".into()
                                    }
                                }
                                5 => text(v, "strike"),
                                6 => text(v, "bid"),
                                7 => text(v, "ask"),
                                8 => self.draft.get("quantity").cloned().unwrap_or("1".into()),
                                _ => {
                                    if v["bid"].is_null() {
                                        "Quote unavailable".into()
                                    } else {
                                        "Selected".into()
                                    }
                                }
                            });
                        });
                    }
                });
            });
    }
    fn evidence_panel(&mut self, ui: &mut egui::Ui, s: &ContractPublication) {
        egui::ScrollArea::both().id_salt("durable-evidence-text").show(ui,|ui|{
   let account=value(s,"account");ui.trading_label(format!("Account: {}; Gate={}; Qualification={}; Cash={}; Available={}",text(&account["id"],"accountAlias"),text(&account,"gate"),text(&account,"qualificationStatus"),text(&account["snapshot"],"cashBalance"),text(&account["snapshot"],"availableFunds")));ui.add_space(16.);
   let orders:Vec<_>=s.result.rows.iter().filter(|r|r.fields.get("kind").is_some_and(|k|k=="broker-order")).map(data).collect();if orders.is_empty(){ui.trading_label("Broker orders: none submitted");}for order in orders{ui.trading_label(format!("Broker order {}: {}; Limit={}; Revision={}; Generation={}; Operation={}; Dispatch={} {}",text(&order["id"],"componentId"),text(&order,"status"),text(&order,"currentSignedNetDebitLimit"),text(&order,"brokerRevision"),text(&order["lastObservation"],"sourceEpoch"),text(&order,"operationId"),text(&order,"dispatchCategory"),text(&order,"dispatchDetail")));if !order["lastObservation"].is_null(){ui.trading_label(format!("Latest broker fact: {}",order["lastObservation"]));}ui.add_space(16.);}
   for execution in s.result.rows.iter().filter(|r|r.fields.get("kind").is_some_and(|k|k=="execution")).map(data){ui.trading_label(format!("Execution {}: {}; Requested={}; Cumulative={}; Revision={}",text(&execution["id"],"executionAttemptId"),text(&execution,"status"),text(&execution,"orderQuantity"),text(&execution,"cumulativeFilledQuantity"),text(&execution,"orderRevision")));if let Some(fills)=execution["fills"].as_array(){for fill in fills{ui.trading_label(format!("  {}: Quantity={}; Price={}; Commission={}; External={}; At={}",text(fill,"contractId"),text(fill,"signedQuantity"),text(fill,"price"),text(fill,"commission"),text(fill,"externalExecutionId"),text(fill,"filledAtUtc")));}}ui.add_space(16.);}
  });
    }
    fn leg_grid(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        editable: bool,
        height: f32,
    ) -> bool {
        ui.style_mut()
            .text_styles
            .insert(egui::TextStyle::Body, egui::FontId::proportional(12.));
        let legs: Vec<_> = s
            .result
            .rows
            .iter()
            .filter(|r| r.fields.get("kind").is_some_and(|k| k == "leg"))
            .map(data)
            .collect();
        let mut changed = false;
        let p = ui.ctx().pixels_per_point();
        let titles = [
            "Leg", "Side", "Role", "Contract", "Strike", "Qty", "Bid", "Ask", "Mid", "Delta",
            "Age", "Selected",
        ];
        let mut table = egui_extras::TableBuilder::new(ui)
            .id_salt("broker-leg-grid")
            .resizable(true)
            .max_scroll_height(height - 25. / p);
        for (i, _) in titles.iter().enumerate() {
            table = table.column(if i == 3 {
                egui_extras::Column::remainder().at_least(150. / p)
            } else {
                egui_extras::Column::remainder()
            });
        }
        table
            .header(25. / p, |mut row| {
                for title in titles {
                    row.col(|ui| {
                        ui.painter()
                            .rect_filled(ui.max_rect(), 0., Color32::from_rgb(45, 48, 54));
                        ui.add(crate::presentation::label_widget(title).truncate());
                    });
                }
            })
            .body(|body| {
                body.rows(24. / p, legs.len(), |mut row| {
                    let index = row.index();
                    let leg = &legs[index];
                    for column in 0..12 {
                        row.col(|ui| {
                            let mid = leg["bid"]
                                .wire_number()
                                .zip(leg["ask"].wire_number())
                                .map(|(b, a)| format!("{:.2}", (b + a) / 2.))
                                .unwrap_or_default();
                            if column == 5 {
                                let before = self.draft.get("quantity").cloned();
                                ui.add_enabled_ui(editable, |ui| {
                                    self.numeric(ui, "quantity", "1", 1., 100000., 1., 0)
                                });
                                changed |= before != self.draft.get("quantity").cloned();
                            } else {
                                ui.trading_label(match column {
                                    0 => (index + 1).to_string(),
                                    1 => {
                                        if text(leg, "role").starts_with('-') {
                                            "SELL".into()
                                        } else {
                                            "BUY".into()
                                        }
                                    }
                                    2 => text(leg, "role"),
                                    3 => text(leg, "contractId"),
                                    4 => text(leg, "strike"),
                                    6 => text(leg, "bid"),
                                    7 => text(leg, "ask"),
                                    8 => mid.clone(),
                                    11 => {
                                        if leg["isFuture"] == true {
                                            context(s, "underlyingReferencePrice").into()
                                        } else {
                                            mid
                                        }
                                    }
                                    9 => leg["delta"].wire_number()
                                        .map(|delta| format!("{delta:.6}"))
                                        .unwrap_or_default(),
                                    _ => chrono::DateTime::parse_from_rfc3339(&text(
                                        leg,
                                        "quoteAtUtc",
                                    ))
                                    .ok()
                                    .map(|date| {
                                        format!(
                                            "{:.0}s",
                                            (std::time::SystemTime::now()
                                                .duration_since(std::time::UNIX_EPOCH)
                                                .unwrap_or_default()
                                                .as_secs_f64()
                                                - date.timestamp_millis() as f64 / 1000.)
                                                .max(0.)
                                        )
                                    })
                                    .unwrap_or("Unavailable".into()),
                                });
                            }
                        });
                    }
                });
            });
        changed
    }
    fn selected_grid(&mut self, ui: &mut egui::Ui, s: &ContractPublication) {
        let p = ui.ctx().pixels_per_point();
        let market = value(s, "market");
        let legs: Vec<_> = s
            .result
            .rows
            .iter()
            .filter(|r| r.fields.get("kind").is_some_and(|k| k == "leg"))
            .map(data)
            .collect();
        let mut table = egui_extras::TableBuilder::new(ui)
            .id_salt("selectedStrategyLegsGrid")
            .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
            .max_scroll_height(126. / p);
        for index in 0..13 {
            let column = self
                .draft
                .get(&format!("chainColumnWidth{index}"))
                .and_then(|v| v.parse::<f32>().ok())
                .map(|width| egui_extras::Column::exact(width).clip(true))
                .unwrap_or_else(egui_extras::Column::remainder);
            table = table.column(column);
        }
        table.header(24. / p, |mut row| {
            for label in OPTION_COLUMNS {
                row.col(|ui| option_header(ui, label, true));
            }
        }).body(|body| {
            body.rows(27. / p, legs.len(), |mut row| {
                let v = &legs[row.index()];
                let call = v["isCall"] == true;
                let role = text(v, "role");
                for column in 0..13 {
                    row.col(|ui| {
                        let side = if call { column < 6 } else { column > 6 };
                        if column == 6 {
                            strike_cell(ui, &option_cell(v, "strike"), Color32::WHITE);
                        } else if side {
                            ui.painter().rect_filled(
                                ui.max_rect(),
                                0.,
                                if role.starts_with('-') {
                                    Color32::from_rgb(110, 24, 30)
                                } else {
                                    Color32::from_rgb(20, 54, 105)
                                },
                            );
                            let key = match column {
                                0 | 12 => "role",
                                1 | 11 => "delta",
                                2 | 10 => "openInterest",
                                3 | 9 => "volume",
                                4 | 7 => "bid",
                                _ => "ask",
                            };
                            let cell = if key == "role" { role.clone() } else {
                                market["contracts"].as_array()
                                    .and_then(|rows| rows.iter().find(|c| c["contractId"] == v["contractId"]))
                                    .map(|c| option_cell(c, key)).unwrap_or_else(|| option_cell(v, key))
                            };
                            ui.add(crate::presentation::label_widget(cell).truncate());
                        }
                    });
                }
            });
        });
    }
}

/// Uses the Windows common file dialog; file contents are read and hashed by the managed adapter.
#[cfg(target_os = "windows")]
pub(super) fn pick_report() -> Option<String> {
    #[repr(C)]
    struct OpenFileName {
        size: u32,
        owner: isize,
        instance: isize,
        filter: *const u16,
        custom: *mut u16,
        custom_max: u32,
        index: u32,
        file: *mut u16,
        file_max: u32,
        file_title: *mut u16,
        title_max: u32,
        directory: *const u16,
        title: *const u16,
        flags: u32,
        offset: u16,
        extension: u16,
        default_extension: *const u16,
        data: isize,
        hook: isize,
        template: *const u16,
        reserved: *mut core::ffi::c_void,
        reserved_size: u32,
        flags_ex: u32,
    }
    #[link(name = "comdlg32")]
    unsafe extern "system" {
        fn GetOpenFileNameW(value: *mut OpenFileName) -> i32;
    }
    #[link(name = "user32")]
    unsafe extern "system" {
        fn GetActiveWindow() -> *mut core::ffi::c_void;
    }
    let mut file = vec![0u16; 32768];
    let filter: Vec<u16> =
        "Test reports (*.txt;*.trx;*.json)\0*.txt;*.trx;*.json\0All files\0*.*\0\0"
            .encode_utf16()
            .collect();
    let title: Vec<u16> = "Select emulator qualification report\0"
        .encode_utf16()
        .collect();
    let mut options: OpenFileName = unsafe { core::mem::zeroed() };
    options.size = core::mem::size_of::<OpenFileName>() as u32;
    options.owner = unsafe { GetActiveWindow() } as isize;
    options.filter = filter.as_ptr();
    options.file = file.as_mut_ptr();
    options.file_max = file.len() as u32;
    options.title = title.as_ptr();
    options.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
    if unsafe { GetOpenFileNameW(&mut options) } != 0 {
        Some(String::from_utf16_lossy(
            &file[..file.iter().position(|v| *v == 0).unwrap_or(file.len())],
        ))
    } else {
        None
    }
}
#[cfg(not(target_os = "windows"))]
pub(super) fn pick_report() -> Option<String> {
    None
}

