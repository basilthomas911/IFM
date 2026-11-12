//! Trade-order layout helpers for selectors, order lists, leg grids, execution/fills, and drafts. Rectangles convert reference coordinates to the current scale; controls obey the managed eligibility flags and local pending state.

use super::*;
use chrono::{Datelike, NaiveDate};

/// Content measurements shared by the draft window and its positioned controls, in screen pixels.
pub(super) struct DraftLayout {
    /// Vertical distance between single-line fields.
    pub(super) row_height: f32,
    /// Height retained for the multiline order reference.
    pub(super) reference_height: f32,
    /// Top of the centered action row.
    pub(super) buttons_top: f32,
    /// Button height adjusted to the current font.
    pub(super) button_height: f32,
    /// Window content size, excluding title bar and frame.
    pub(super) size: egui::Vec2,
}


/// Fit the draft dialog to its fields and action row without reserving a second block below them.
pub(super) fn draft_layout(ctx: &egui::Context, order: bool, trade_reference: Option<&str>) -> DraftLayout {
    let p = ctx.pixels_per_point();
    let font = egui::TextStyle::Body.resolve(&ctx.style());
    let line = ctx.fonts(|fonts| fonts.row_height(&font)) * p;
    let row_height = (if order { 34_f32 } else { 30_f32 }).max(line + 4.);
    let reference_height = 140_f32.max(4. * line + 8.);
    let button_height = 30_f32.max(line + 8.);
    let width = if order {
        721.
    } else {
        let caption = ctx.fonts(|fonts| fonts.layout_no_wrap(
            "Trade Reference:".into(), font.clone(), Color32::WHITE,
        ).size().x) * p;
        let input_font = egui::FontSelection::Default.resolve(&ctx.style());
        let text = ctx.fonts(|fonts| fonts.layout_no_wrap(
            trade_reference.unwrap_or_default().into(), input_font, Color32::WHITE,
        ).size().x) * p;
        (caption.max(137.) + 20. + text + 48.).max(900.)
            .min((ctx.screen_rect().width() * p - 64.).max(320.))
    };
    let buttons_top = if order {
        12. + 3. * row_height + reference_height + 16.
    } else {
        272_f32.max(2. + 7. * row_height + 26_f32.max(line + 2.) + 16.)
    };
    DraftLayout {
        row_height,
        reference_height,
        buttons_top,
        button_height,
        size: egui::vec2(
            width,
            buttons_top + button_height + 12.,
        ),
    }
}


/// Convert WinForms reference coordinates and dimensions into a scaled egui rectangle relative to the given origin.
pub(super) fn rect(o: egui::Pos2, p: f32, x: f32, y: f32, w: f32, h: f32) -> egui::Rect {
    egui::Rect::from_min_size(o + egui::vec2(x, y) / p, egui::vec2(w, h) / p)
}


/// Run a widget renderer in a child UI with the requested stable ID and maximum rectangle. Callers own any additional clipping requirements.
pub(super) fn at<R>(
    ui: &mut egui::Ui,
    r: egui::Rect,
    id: &str,
    f: impl FnOnce(&mut egui::Ui) -> R,
) -> R {
    ui.scope_builder(egui::UiBuilder::new().id_salt(id).max_rect(r), f)
        .inner
}


/// Keep each form field tall enough for the active font, around its row center.
pub(super) fn form_rect(ui: &egui::Ui, bounds: egui::Rect) -> egui::Rect {
    let p = ui.ctx().pixels_per_point();
    let font = egui::TextStyle::Body.resolve(ui.style());
    let height = bounds
        .height()
        .max(ui.fonts(|fonts| fonts.row_height(&font)) + 2.0 / p);
    egui::Rect::from_center_size(
        bounds.center(),
        egui::vec2(bounds.width(), (height * p).ceil() / p),
    )
}


/// Center control text in a shared row without widget-specific vertical padding.
pub(super) fn form_at<R>(
    ui: &mut egui::Ui,
    bounds: egui::Rect,
    id: &str,
    render: impl FnOnce(&mut egui::Ui) -> R,
) -> R {
    ui.scope_builder(
        egui::UiBuilder::new().id_salt(id).max_rect(bounds).layout(
            egui::Layout::centered_and_justified(egui::Direction::LeftToRight),
        ),
        |ui| {
            let font = egui::TextStyle::Body.resolve(ui.style());
            ui.style_mut()
                .text_styles
                .insert(egui::TextStyle::Button, font.clone());
            ui.style_mut()
                .text_styles
                .insert(egui::TextStyle::Monospace, font);
            ui.spacing_mut().button_padding.y = 0.0;
            ui.spacing_mut().interact_size.y = bounds.height();
            render(ui)
        },
    )
    .inner
}

impl TradeWorkspace {
    /// Right-align a field caption exactly twenty screen pixels before its control.
    /// Shared font metrics put caption and control text on the same row baseline.
    pub(super) fn field_caption(
        &mut self,
        ui: &mut egui::Ui,
        control: egui::Rect,
        id: &str,
        caption: &str,
    ) {
        let response = crate::presentation::field_caption(ui, control, caption);
        self.controls.insert(format!("caption:{id}"), response.rect);
    }


    /// Derive editor-button availability from the published selected order/trade rows and current source filter. Submission/pending gates are applied by the caller.
    pub(super) fn order_button_enabled(&mut self, s: &ContractPublication, flag_key: &str) -> bool {
        let display = self.prepare_trade_display(s);
        match flag_key {
            "canCreateOrder" => true,
            "canRemoveOrder" => s.result.rows.iter().any(|r| {
                r.fields.get("kind").is_some_and(|k| k == "order")
                    && (self.source_filter.is_empty()
                        || self.source_filter == "All"
                        || text(&display.row(&r.id), "source") == self.source_filter)
            }),
            "canLoadTrade" => s
                .result
                .context
                .get("tradeId")
                .filter(|id| !id.is_empty())
                .is_some_and(|id| {
                    s.result.rows.iter().any(|r| {
                        r.id == format!("trade:{id}")
                            && !matches!(text(&display.row(&r.id), "tradeState").as_str(), "NewTrade" | "Rejected")
                    })
                }),
            "canAddTrade" => s
                .result
                .context
                .get("orderId")
                .filter(|id| !id.is_empty())
                .is_some_and(|id| {
                    s.result.rows.iter().any(|r| {
                        r.id == format!("order:{id}")
                            && (self.source_filter.is_empty()
                                || self.source_filter == "All"
                                || text(&display.row(&r.id), "source") == self.source_filter)
                    })
                }),
            "canChangeTrade" => s.result.context.get(flag_key).is_some_and(|value| value == "True"),
            "canRemoveTrade" => {
                let mut trades = s.result.rows.iter().filter(|r| {
                    r.fields.get("kind").is_some_and(|kind| kind == "trade")
                });
                let Some(id) = s.result.context.get("tradeId") else { return false };
                let selected = format!("trade:{id}");
                let Some(trade) = trades.clone().find(|row| row.id == selected) else { return false };
                let mut siblings = trades.by_ref().filter(|row| row.id != selected);
                match text(&display.row(&trade.id), "tradeState").as_str() {
                    "NewTrade" => siblings.next().is_none(),
                    "Rejected" => siblings.next().is_none_or(|row| {
                        text(&display.row(&row.id), "tradeState") == "Open" && siblings.next().is_none()
                    }),
                    _ => false,
                }
            }
            _ => false,
        }
    }


    /// Render portfolio/fund/strategy/order selector controls in their reference layout and return the chosen action key.
    pub(super) fn draw_selectors(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        o: egui::Pos2,
        outer: egui::Vec2,
    ) -> Option<&'static str> {
        let p = ui.ctx().pixels_per_point();
        let w = outer.x * p;
        let mut action = None;
        let font = egui::TextStyle::Body.resolve(ui.style());
        let first_x = 127_f32.max(
            ui.painter()
                .layout_no_wrap("Portfolio:".into(), font, Color32::WHITE)
                .size()
                .x
                * p
                + 40.,
        );
        for (key, label, y) in [("portfolio", "Portfolio:", 51.), ("fund", "Fund:", 101.)] {
            let width = if key == "portfolio" {
                720_f32.min((w - 562.).max(200.))
            } else {
                (w - 319.).max(300.)
            };
            let r = form_rect(ui, rect(o, p, first_x, y, width, 27.));
            self.field_caption(ui, r, key, label);
            let values = s.result.choices.get(key).cloned().unwrap_or_default();
            let index = s
                .result
                .context
                .get(&format!("{key}Index"))
                .and_then(|v| v.parse::<usize>().ok());
            let response = form_at(ui, r, key, |ui| {
                egui::ComboBox::from_id_salt(key)
                    .width(r.width())
                    .selected_text(
                        index
                            .and_then(|i| values.get(i))
                            .cloned()
                            .unwrap_or_default(),
                    )
                    .show_ui(ui, |ui| {
                        for (i, v) in values.iter().enumerate() {
                            if ui.selectable_label(index == Some(i), v).clicked() {
                                self.draft.insert("index".into(), i.to_string());
                                action = Some(key);
                            }
                        }
                    })
            });
            self.controls.insert(
                if key == "portfolio" {
                    "ddlPortfolio"
                } else {
                    "ddlFund"
                }
                .into(),
                response.response.rect,
            );
            if key == "portfolio" {
                let font = egui::TextStyle::Body.resolve(ui.style());
                let caption_width = ui
                    .painter()
                    .layout_no_wrap("Source:".into(), font, Color32::WHITE)
                    .size()
                    .x;
                let x = first_x + width + 40. + caption_width * p;
                let r = form_rect(ui, rect(o, p, x, 51., 220., 27.));
                self.field_caption(ui, r, "source", "Source:");
                let current = if self.source_filter.is_empty() {
                    "All".to_string()
                } else {
                    self.source_filter.clone()
                };
                let response = form_at(ui, r, "source", |ui| {
                    egui::ComboBox::from_id_salt("composition-source")
                        .width(r.width())
                        .selected_text(&current)
                        .show_ui(ui, |ui| {
                            for value in ["All", "Manual", "Strategy Workflow"] {
                                if ui.selectable_label(current == value, value).clicked() {
                                    self.source_filter = value.into();
                                }
                            }
                        })
                });
                self.controls
                    .insert("ddlCompositionSource".into(), response.response.rect);
            }
        }
        action
    }


    /// Render order/expiry selection lists and the selected order summary in the reference layout.
    pub(super) fn draw_order_lists(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        o: egui::Pos2,
        outer: egui::Vec2,
    ) -> Option<&'static str> {
        let display = self.prepare_trade_display(s);
        let p = ui.ctx().pixels_per_point();
        let w = outer.x * p;
        let grid_top = 238.;
        let grid_gap = 20.;
        let grid_bottom = ((ui.clip_rect().bottom() - o.y) * p - 8.).min(outer.y * p - 8.);
        let grid_height = (grid_bottom - grid_top - grid_gap).max(0.);
        let order_height = grid_height * (2. / 3.);
        let trade_height = grid_height - order_height;
        let trade_top = grid_top + order_height + grid_gap;
        let mut action = None;
        let initial = s
            .result
            .context
            .get("valueDate")
            .map(String::as_str)
            .unwrap_or("2026-10-01");
        let date = NaiveDate::parse_from_str(initial, "%Y-%m-%d").unwrap();
        let first = date.with_day(1).unwrap()
            .checked_sub_months(chrono::Months::new(1)).unwrap();
        let last = month_end(date);
        if self.pending_action != "range" && self.deferred_action != Some("range") {
            for key in ["from", "to"] {
                if let Some(value) = s.result.context.get(key)
                    && NaiveDate::parse_from_str(value, "%Y-%m-%d").is_ok()
                {
                    self.draft.insert(key.into(), value.clone());
                }
            }
        }
        let mut from = NaiveDate::parse_from_str(
            self.draft.entry("from".into()).or_insert(first.to_string()),
            "%Y-%m-%d",
        )
        .unwrap_or(first);
        let mut to = NaiveDate::parse_from_str(
            self.draft.entry("to".into()).or_insert(last.to_string()),
            "%Y-%m-%d",
        )
        .unwrap_or(last);
        let r = form_rect(ui, rect(o, p, 133., 188., 424., 27.));
        self.field_caption(ui, r, "dtpFrom", "From:");
        let changed = form_at(ui, r, "from-date", |ui| {
            ui.spacing_mut().button_padding.y = 0.;
            ui.spacing_mut().interact_size.y = r.height();
            let response = ui.add_sized(
                r.size(),
                crate::date_picker::DatePickerButton::new(&mut from)
                    .id_salt("order-from")
                    .format("%A, %B %d, %Y")
                    .calendar_week(false),
            );
            response.widget_info(|| {
                egui::WidgetInfo::labeled(egui::WidgetType::Button, true, "From calendar")
            });
            self.controls.insert("dtpFrom".into(), response.rect);
            response.changed()
        });
        if changed {
            action = Some("range");
        }
        let r = form_rect(ui, rect(o, p, 624., 188., 416., 27.));
        self.field_caption(ui, r, "dtpTo", "To:");
        if form_at(ui, r, "to-date", |ui| {
            ui.spacing_mut().button_padding.y = 0.;
            ui.spacing_mut().interact_size.y = r.height();
            let response = ui.add_sized(
                r.size(),
                crate::date_picker::DatePickerButton::new(&mut to)
                    .id_salt("order-to")
                    .format("%A, %B %d, %Y")
                    .calendar_week(false),
            );
            response.widget_info(|| {
                egui::WidgetInfo::labeled(egui::WidgetType::Button, true, "To calendar")
            });
            self.controls.insert("dtpTo".into(), response.rect);
            response.changed()
        }) {
            action = Some("range");
        }
        self.draft.insert("from".into(), from.to_string());
        self.draft.insert("to".into(), to.to_string());
        let width = (w - 319.).max(300.);
        let bx = w - 180.;
        for (kind, label, y, top, height, columns) in [
            (
                "order",
                "Orders:",
                grid_top,
                0.,
                order_height,
                vec![
                    ("orderId", "Order Id"),
                    ("createdOnUtc", "Order Date"),
                    ("status", "Order Status"),
                    ("operatorReference", "Reference"),
                    ("source", "Source"),
                ],
            ),
            (
                "trade",
                "Trades:",
                trade_top,
                0.,
                trade_height,
                vec![
                    ("tradeId", "Trade Id"),
                    ("tradeType", "Trade Type"),
                    ("effectiveDate", "Effective Date"),
                    ("tradeState", "Trade State"),
                    ("tradeAction", "Trade Action"),
                    ("tradeStatus", "Trade Status"),
                    ("tradeReference", "Trade Reference"),
                    ("instructionReference", "Instruction Reference"),
                ],
            ),
        ] {
            let r = rect(o, p, 131., y + top, width - 8., height);
            let caption_row = form_rect(
                ui,
                egui::Rect::from_min_size(r.min, egui::vec2(r.width(), 24. / p)),
            );
            self.field_caption(ui, caption_row, kind, label);
            let selected = at(ui, r, kind, |ui| {
                self.table(ui, s, kind, &columns, r.height())
            });
            self.controls.insert(
                if kind == "order" {
                    "lstTradeOrders"
                } else {
                    "lstTrades"
                }
                .into(),
                r,
            );
            if let Some(id) = selected {
                if kind == "order" {
                    self.draft.insert("orderId".into(), id);
                    action = Some("order");
                } else if let Some(row) =
                    s.result.rows.iter().find(|r| r.id == format!("trade:{id}"))
                {
                    self.draft
                        .insert("index".into(), text(&display.row(&row.id), "index"));
                    action = Some("trade");
                }
            }
        }
        for (id, label, flag_key, intent, y) in [
            (
                "btnCreateOrder",
                "Create Order...",
                "canCreateOrder",
                "prepare-order",
                grid_top,
            ),
            (
                "btnDeleteOrder",
                "Delete Order",
                "canRemoveOrder",
                "remove-order",
                grid_top + 44.,
            ),
            (
                "btnLoadOrder",
                "Load Trade",
                "canLoadTrade",
                "load-trade",
                trade_top,
            ),
            (
                "btnAddTrade",
                "Add Trade...",
                "canAddTrade",
                "prepare-trade",
                trade_top + 44.,
            ),
            (
                "btnChangeTrade",
                "Change Trade...",
                "canChangeTrade",
                "prepare-change-trade",
                trade_top + 88.,
            ),
            (
                "btnRemoveTrade",
                "Remove Trade",
                "canRemoveTrade",
                "remove-trade",
                trade_top + 132.,
            ),
        ] {
            let r = rect(o, p, bx, y, 140., 32.);
            let enabled = self.order_button_enabled(s, flag_key);
            let response = at(ui, r, id, |ui| {
                ui.add_enabled(enabled, egui::Button::new(label).min_size(r.size()))
            });
            self.controls.insert(id.into(), response.rect);
            if response.clicked() {
                match intent {
                    "prepare-order" => {
                        self.dialog = "create-order".into();
                        self.draft.insert("reference".into(), String::new());
                        action = Some(intent);
                    }
                    "prepare-trade" => {
                        self.dialog = "create-trade".into();
                        self.draft.remove("baseContract");
                        self.draft.remove("tradeReference");
                        self.draft.remove("tradeReferenceDefault");
                        action = Some(intent);
                    }
                    "prepare-change-trade" => action = Some(intent),
                    "load-trade" => {
                        self.open = false;
                        action = Some(intent);
                    }
                    "remove-trade" => {
                        if let Some(id) = s.result.context.get("tradeId") {
                            self.draft.insert("removeTradeId".into(), id.clone());
                            self.dialog = intent.into();
                        }
                    }
                    _ => {
                        self.dialog = intent.into();
                        self.draft.insert("reason".into(), String::new());
                    }
                }
            }
        }
        action
    }


    /// Edit a bounded numeric trade draft value with the configured step and decimal precision.
    pub(super) fn numeric(
        &mut self,
        ui: &mut egui::Ui,
        key: &str,
        default: &str,
        min: f64,
        max: f64,
        step: f64,
        decimals: usize,
    ) {
        ui.visuals_mut().widgets.inactive.weak_bg_fill = Color32::BLACK;
        let stored = self.draft.entry(key.into()).or_insert(default.into());
        let mut value = stored.parse::<f64>().unwrap_or(min).clamp(min, max);
        let response = ui.add_sized(
            ui.available_size(),
            egui::DragValue::new(&mut value)
                .range(min..=max)
                .speed(step)
                .fixed_decimals(decimals),
        );
        response.widget_info(|| {
            let mut info = egui::WidgetInfo::drag_value(ui.is_enabled(), value);
            info.label = Some(key.into());
            info
        });
        if response.changed() {
            *stored = format!("{value:.decimals$}");
        }
    }


    /// Render the option-chain grid from the published contracts/quotes and return selection/refresh actions.
    pub(super) fn option_grid(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        value: &crate::models::broker_presentation::MarketDisplay,
    ) -> Option<&'static str> {
        let p = ui.ctx().pixels_per_point();
        use crate::models::cells::Cells;
        let contracts = value.contracts.as_deref().unwrap_or_default();
        let mut action = None;
        let display = self.prepare_trade_display(s);
        let empty_grid = crate::models::prepared_views::OptionGrid::default();
        let grid = display
            .market
            .as_ref()
            .map(|market| &market.grid)
            .unwrap_or(&empty_grid);
        let strikes = &grid.strikes;
        let selected = &display.selected_legs;
        let group_width = (ui.available_width() - 2. * ui.spacing().item_spacing.x) / 13.;
        ui.horizontal(|ui| {
            ui.add_sized(
                [group_width * 6., 28. / p],
                crate::presentation::caption_widget("CALLS"),
            );
            ui.add_sized(
                [group_width, 28. / p],
                crate::presentation::caption_widget("STRIKE"),
            );
            ui.add_sized(
                [group_width * 6., 28. / p],
                crate::presentation::caption_widget("PUTS"),
            );
        });
        let columns = super::blotter::OPTION_COLUMNS;
        let height = (ui.available_height() - 24. / p - ui.spacing().item_spacing.y).max(0.);
        let row_spacing = ui.spacing().item_spacing.y;
        let mut table = egui_extras::TableBuilder::new(ui)
            .id_salt("option-chain")
            .resizable(true)
            .sense(egui::Sense::click())
            .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
            .fill_grid(strikes.len(), 24. / p, row_spacing, height);
        for _ in columns {
            table = table.column(egui_extras::Column::remainder().clip(true));
        }
        // Reveal a new selection once, then retain the operator's scroll position during quote updates.
        let selection_key = selected
            .iter()
            .map(|leg| super::blotter::cell_text(leg, "contractId"))
            .collect::<Vec<_>>()
            .join("|");
        if !selection_key.is_empty()
            && self.draft.get("chainSelectionAnchor") != Some(&selection_key)
        {
            let selected_strikes: Vec<_> = selected
                .iter()
                .filter_map(|leg| leg.cell("strike").number())
                .collect();
            if !selected_strikes.is_empty() {
                let middle = selected_strikes.iter().sum::<f64>() / selected_strikes.len() as f64;
                if let Some((index, _)) = strikes
                    .iter()
                    .enumerate()
                    .min_by(|(_, a), (_, b)| (**a - middle).abs().total_cmp(&(**b - middle).abs()))
                {
                    table = table.scroll_to_row(index, Some(egui::Align::Center));
                    self.draft
                        .insert("chainSelectionAnchor".into(), selection_key);
                }
            }
        }
        let mut widths = Vec::new();
        table
            .header(24. / p, |mut row| {
                for label in &columns {
                    row.col(|ui| super::blotter::option_header(ui, label, true));
                }
            })
            .bordered_body(24. / p, |body| {
                widths = body.widths().to_vec();
                body.rows(24. / p, strikes.len(), |mut row| {
                    let indexed = &grid.rows[row.index()];
                    let call = indexed.call.map(|i| &contracts[i]);
                    let put = indexed.put.map(|i| &contracts[i]);
                    let call_text = indexed.call.map(|i| grid.formatted(i, &contracts[i]));
                    let put_text = indexed.put.map(|i| grid.formatted(i, &contracts[i]));
                    let nearest = grid.nearest == Some(row.index());
                    for column in 0..13 {
                        let c = if column < 6 { call } else { put };
                        let role = c
                            .and_then(|c| selected.iter().find(|l| l.contract_id == c.contract_id))
                            .map(|l| super::blotter::cell_text(l, "role"));
                        let (_, response) = row.col(|ui| {
                            if nearest {
                                let background = ui
                                    .max_rect()
                                    .expand2(egui::vec2(ui.spacing().item_spacing.x / 2., 0.));
                                ui.painter().rect_filled(background, 0., Color32::YELLOW);
                                ui.visuals_mut().override_text_color = Some(Color32::BLACK);
                            }
                            if column == 6 {
                                super::blotter::strike_cell(
                                    ui,
                                    &indexed.strike_text,
                                    if nearest {
                                        Color32::BLACK
                                    } else {
                                        Color32::WHITE
                                    },
                                );
                            } else if c.is_some() {
                                if let Some(role) = &role {
                                    if !nearest {
                                        ui.painter().rect_filled(
                                            ui.max_rect(),
                                            0.,
                                            if role.starts_with('-') {
                                                Color32::from_rgb(110, 24, 30)
                                            } else {
                                                Color32::from_rgb(20, 54, 105)
                                            },
                                        );
                                    }
                                }
                                let key = match column {
                                    0 | 12 => "",
                                    1 | 11 => "delta",
                                    2 | 10 => "openInterest",
                                    3 | 9 => "volume",
                                    4 | 7 => "bid",
                                    _ => "ask",
                                };
                                ui.add(
                                    crate::presentation::label_widget(if key.is_empty() {
                                        role.as_deref().unwrap_or_default()
                                    } else {
                                        &(if column < 6 {
                                            call_text.as_ref()
                                        } else {
                                            put_text.as_ref()
                                        })
                                        .unwrap()[crate::models::prepared_views::OptionGrid::FIELDS
                                            .iter()
                                            .position(|field| *field == key)
                                            .unwrap()]
                                    })
                                    .truncate(),
                                );
                            }
                        });
                        if column != 6 && response.clicked() {
                            if let Some(c) = c {
                                let short = if column < 6 { "-SC" } else { "-SP" };
                                let long = if column < 6 { "+LC" } else { "+LP" };
                                self.draft.insert(
                                    "contractId".into(),
                                    super::blotter::cell_text(c, "contractId"),
                                );
                                self.draft.insert(
                                    "role".into(),
                                    role.clone().unwrap_or_else(|| {
                                        if selected
                                            .iter()
                                            .any(|l| super::blotter::cell_text(l, "role") == short)
                                        {
                                            long.into()
                                        } else {
                                            short.into()
                                        }
                                    }),
                                );
                                action = Some(if role.is_some() {
                                    "remove-leg"
                                } else {
                                    "select-leg"
                                });
                            }
                        }
                    }
                });
            });
        for (index, width) in widths.into_iter().enumerate() {
            self.draft
                .insert(format!("chainColumnWidth{index}"), width.to_string());
        }
        action
    }


    /// Render an opening/closing trade draft and its eligible preview/submission actions.
    pub(super) fn draw_draft(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        kind: &str,
    ) -> Option<&'static str> {
        let display = self.prepare_trade_display(s);
        let p = ui.ctx().pixels_per_point();
        let o = ui.cursor().min;
        let order = kind == "create-order";
        let changing = kind == "change-trade";
        let font = egui::TextStyle::Body.resolve(ui.style());
        let layout = draft_layout(ui.ctx(), order, self.draft.get("tradeReference")
            .or_else(|| s.result.context.get("preparedTradeLabel")).map(String::as_str));
        let row_height = layout.row_height;
        let form_width = ui.available_width() * p;
        self.controls.insert(
            "draft-client".into(),
            rect(o, p, 0., 0., form_width, layout.size.y),
        );
        let labels = if order {
            vec!["Order Id:", "Order Date:", "Order Status:", "Reference:"]
        } else {
            vec![
                "Trade Id:",
                "Trade Strategy:",
                "Trade Type:",
                "Effective Date:",
                "",
                "Trade State:",
                "Trade Action:",
                "Base Contract:",
                "Trade Reference:",
            ]
        };
        let value_x = labels
            .iter()
            .map(|label| {
                ui.painter()
                    .layout_no_wrap((*label).into(), font.clone(), Color32::WHITE)
                    .size()
                    .x
                    * p
            })
            .fold(if order { 112_f32 } else { 137_f32 }, f32::max)
            + 20.;
        let closing = if changing {
            if s.result.context.get("changeTradeClosing").is_some_and(|value| value == "true") {
                s.result.context.get("changeTradeType").cloned().unwrap_or_default()
            } else {
                String::new()
            }
        } else {
            s.result.context.get("closingType").cloned().unwrap_or_default()
        };
        if !order && !changing && !closing.is_empty() {
            self.draft.insert("tradeType".into(), closing.clone());
            self.draft.insert(
                "baseContract".into(),
                s.result
                    .context
                    .get("closingContract")
                    .cloned()
                    .unwrap_or_default(),
            );
        }
        for (i, label) in labels.iter().enumerate() {
            if !order && label.is_empty() {
                continue;
            }
            let row = if !order && i > 4 { i - 1 } else { i };
            let y = if order { 12. } else { 2. } + row as f32 * row_height;
            let r = rect(
                o,
                p,
                value_x,
                y,
                if order {
                    (form_width - value_x - 12.).max(120.)
                } else if i == 8 {
                    (form_width - value_x - 12.).max(100.)
                } else {
                    311_f32.min((form_width - value_x - 12.).max(100.))
                },
                26.,
            );
            let r = form_rect(ui, r);
            self.field_caption(ui, r, &format!("draft-field-{i}"), label);
            self.controls.insert(format!("draft-field-{i}"), r);
            form_at(ui, r, &format!("draft-field-{i}"), |ui| {
                if order {
                    let row = s
                        .result
                        .rows
                        .iter()
                        .find(|r| r.fields.get("kind").is_some_and(|k| k == "order-draft"))
                        .map(|r| display.row(&r.id))
                        .unwrap_or(&display_cache::BusinessRow::Missing);
                    match i {
                        0..=2 => {
                            let value = match i {
                                0 if text(&row, "orderId") == "0" || row.is_missing() => {
                                    "Allocated on Save".into()
                                }
                                0 => text(&row, "orderId"),
                                1 => text(&row, "orderDate"),
                                _ if row.is_missing() => "Open".into(),
                                _ => text(&row, "orderStatus"),
                            };
                            let size = ui
                                .painter()
                                .layout_no_wrap(value.clone(), font.clone(), Color32::WHITE)
                                .size();
                            let text_bounds = egui::Rect::from_min_size(
                                egui::pos2(r.left() + 4. / p, r.center().y - size.y * 0.5),
                                egui::vec2(size.x.min((r.width() - 8. / p).max(0.)), size.y),
                            );
                            let response = ui.place(
                                text_bounds,
                                crate::presentation::label_widget(
                                    egui::RichText::new(value).font(font.clone()),
                                )
                                .truncate(),
                            );
                            self.controls
                                .insert(format!("draft-value-{i}"), response.rect);
                        }
                        _ => {
                            // Align the multiline reference's first line with its caption.
                            ui.scope_builder(
                                egui::UiBuilder::new()
                                    .max_rect(r)
                                    .layout(egui::Layout::top_down(egui::Align::Min)),
                                |ui| {
                                    let response = ui.add_sized(
                                        egui::vec2(r.width(), layout.reference_height / p),
                                        egui::TextEdit::multiline(
                                            self.draft.entry("reference".into()).or_default(),
                                        )
                                        .margin(egui::vec2(
                                            4. / p,
                                            (r.height()
                                                - ui.fonts(|fonts| fonts.row_height(&font)))
                                                * 0.5,
                                        ))
                                        .hint_text("Optional multiline order reference"),
                                    );
                                    self.controls
                                        .insert("draft-reference".into(), response.rect);
                                },
                            );
                        }
                    }
                } else {
                    match i {
                        0 | 5 | 6 => {
                            let value = match i {
                                0 => s.result.context.get(if changing { "tradeId" } else { "preparedTradeId" }).cloned().unwrap_or_default(),
                                5 => "NewTrade".into(),
                                _ => {
                                    if changing && self.draft.get("tradeType") == s.result.context.get("changeTradeType") {
                                        s.result.context.get("tradeAction").cloned().unwrap_or_default()
                                    } else {
                                    let credit = self.draft.get("tradeType").is_none_or(|kind| matches!(
                                        kind.as_str(), "ShortIronCondor" | "PutCreditSpread" | "CallCreditSpread"
                                    ));
                                    if credit { "Sell" } else { "Buy" }.into()
                                    }
                                }
                            };
                            let size = ui.painter().layout_no_wrap(value.clone(), font.clone(), Color32::WHITE).size();
                            let bounds = egui::Rect::from_min_size(
                                egui::pos2(r.left() + 4. / p, r.center().y - size.y * 0.5),
                                egui::vec2(size.x.min((r.width() - 8. / p).max(0.)), size.y),
                            );
                            let mut text = egui::RichText::new(value).font(font.clone());
                            if i == 5 {
                                text = text.color(crate::presentation::CAPTION_FOREGROUND);
                            }
                            ui.place(bounds, crate::presentation::label_widget(text).truncate());
                        }
                        1 => {
                            let selected_type = self
                                .draft
                                .get("tradeType")
                                .cloned()
                                .unwrap_or_else(|| "ShortIronCondor".into());
                            let initial = if selected_type.contains("IronCondor") {
                                "Iron Condor"
                            } else if selected_type == "FuturesOutright" {
                                "Futures Outright"
                            } else {
                                "Vertical Spread"
                            };
                            let strategy = self
                                .draft
                                .entry("strategy".into())
                                .or_insert(initial.into());
                            let before = strategy.clone();
                            ui.add_enabled_ui(closing.is_empty(), |ui| {
                                egui::ComboBox::from_id_salt("trade-strategy")
                                    .width(r.width())
                                    .selected_text(&*strategy)
                                    .show_ui(ui, |ui| {
                                        for v in
                                            ["Iron Condor", "Vertical Spread", "Futures Outright"]
                                        {
                                            ui.selectable_value(strategy, v.into(), v);
                                        }
                                    });
                            });
                            if *strategy != before {
                                self.draft.remove("tradeType");
                            }
                        }
                        2 | 7 => {
                            let key = if i == 2 { "tradeType" } else { "baseContract" };
                            let values = if i == 2 {
                                match self
                                    .draft
                                    .get("strategy")
                                    .map(String::as_str)
                                    .unwrap_or("Iron Condor")
                                {
                                    "Vertical Spread" => vec![
                                        "PutCreditSpread".into(),
                                        "PutDebitSpread".into(),
                                        "CallCreditSpread".into(),
                                        "CallDebitSpread".into(),
                                    ],
                                    "Futures Outright" => vec!["FuturesOutright".into()],
                                    _ => vec!["ShortIronCondor".into(), "LongIronCondor".into()],
                                }
                            } else {
                                s.result.choices.get(key).cloned().unwrap_or_default()
                            };
                            let selected = self
                                .draft
                                .entry(key.into())
                                .or_insert_with(|| values.first().cloned().unwrap_or_default());
                            ui.add_enabled_ui(closing.is_empty(), |ui| {
                                egui::ComboBox::from_id_salt(format!("draft-{key}"))
                                    .width(r.width())
                                    .selected_text(&*selected)
                                    .show_ui(ui, |ui| {
                                        for v in values {
                                            ui.selectable_value(selected, v.clone(), v);
                                        }
                                    });
                            });
                        }
                        3 => {
                            let date = s
                                .result
                                .context
                                .get("valueDate")
                                .cloned()
                                .unwrap_or_else(|| "2026-10-13".into());
                            let selected = self.draft.entry("effectiveDate".into()).or_insert(date);
                            if let Ok(mut date) = NaiveDate::parse_from_str(selected, "%Y-%m-%d") {
                                ui.add_sized(
                                    r.size(),
                                    crate::date_picker::DatePickerButton::new(&mut date)
                                        .id_salt("trade-effective-date")
                                        .calendar_week(false),
                                );
                                *selected = date.to_string();
                            }
                        }
                        8 => {
                            let trade_type = self.draft.get("tradeType").map(String::as_str).unwrap_or("ShortIronCondor");
                            let action = if matches!(trade_type, "ShortIronCondor" | "PutCreditSpread" | "CallCreditSpread") {
                                "Sell"
                            } else {
                                "Buy"
                            };
                            let label = s.result.context.get("preparedTradeLabel").filter(|label| !label.is_empty())
                                .cloned().unwrap_or_else(|| format!("{}:{} {trade_type}",
                                    s.result.context.get("orderId").map(String::as_str).unwrap_or_default(),
                                    s.result.context.get("preparedTradeId").map(String::as_str).unwrap_or_default()));
                            let default = format!("{action} {label}");
                            let unedited = self.draft.get("tradeReference").is_none()
                                || self.draft.get("tradeReference") == self.draft.get("tradeReferenceDefault");
                            if !changing && unedited {
                                self.draft.insert("tradeReference".into(), default.clone());
                            }
                            self.draft.insert("tradeReferenceDefault".into(), default);
                            let response = ui.add_sized(r.size(), egui::TextEdit::singleline(
                                self.draft.entry("tradeReference".into()).or_default(),
                            ));
                            response.widget_info(|| egui::WidgetInfo::labeled(
                                egui::WidgetType::TextEdit, true, "Trade Reference",
                            ));
                            self.controls.insert("draft-trade-reference".into(), response.rect);
                        }
                        _ => {}
                    }
                }
            });
        }
        let mut action = None;
        let button_width = ["Save", "Cancel"]
            .iter()
            .map(|label| {
                ui.painter()
                    .layout_no_wrap((*label).into(), font.clone(), Color32::WHITE)
                    .size()
                    .x
                    * p
                    + (2. * ui.spacing().button_padding.x * p).max(16.)
            })
            .fold(77_f32, f32::max)
            .ceil();
        let gap = 8.;
        let x = (form_width - 2. * button_width - gap) * 0.5;
        for (label, offset) in [("Save", 0.), ("Cancel", button_width + gap)] {
            let r = rect(
                o,
                p,
                x + offset,
                layout.buttons_top,
                button_width,
                layout.button_height,
            );
            let ready = self.interaction_ready(s)
                && (!order
                    || s.result
                        .rows
                        .iter()
                        .any(|r| r.fields.get("kind").is_some_and(|k| k == "order-draft")))
                && (order || changing
                    || s.result
                        .context
                        .get("preparedTradeId")
                        .is_some_and(|v| !v.is_empty()))
                && (!changing || (!closing.is_empty() || self.draft.get("baseContract").is_some_and(|contract|
                    s.result.choices.get("baseContract").is_some_and(|choices| choices.contains(contract)))));
            let response = form_at(ui, r, label, |ui| {
                ui.add_enabled(
                    label == "Cancel" || ready,
                    egui::Button::new(label).min_size(r.size()),
                )
            });
            self.controls.insert(
                format!("draft-{}", label.to_ascii_lowercase()),
                response.rect,
            );
            if response.clicked() {
                action = Some(if label == "Cancel" {
                    "cancel-draft"
                } else if order {
                    "create-order"
                } else if changing {
                    "change-trade"
                } else {
                    "create-trade"
                });
            }
        }
        // Reserve the content once from its origin; adding height below positioned fields doubles the window.
        ui.allocate_rect(
            rect(o, p, 0., 0., form_width, layout.size.y),
            egui::Sense::hover(),
        );
        action
    }
}


/// Return the last calendar date in the input date's month.
fn month_end(date: NaiveDate) -> NaiveDate {
    let next = if date.month() == 12 {
        NaiveDate::from_ymd_opt(date.year() + 1, 1, 1)
    } else {
        NaiveDate::from_ymd_opt(date.year(), date.month() + 1, 1)
    }
    .unwrap();
    next.pred_opt().unwrap()
}
