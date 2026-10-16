//! Native trading workspace. All persistence and trade eligibility stays in shared managed models.
use crate::control_parity::BorderedTable as _;
use crate::wire::WireNumber as _;
use crate::presentation::TradingLabelUi as _;
mod blotter;
mod order_layout;
mod iron_condor;
mod workspace_tabs;
use crate::{
    contract_editor::{ContractPublication, ContractRow},
    lookup_editor::new_request_id,
};
use eframe::egui::{self, Color32};
use std::collections::BTreeMap;
#[derive(Default)]
pub struct TradeWorkspace {
    pub open: bool,
    tab: usize,
    compose_tab: usize,
    dialog: String,
    draft: BTreeMap<String, String>,
    pending: Option<String>,
    pending_dialog: String,
    pending_action: String,
    deferred_action: Option<&'static str>,
    dismissed_submission_error: String,
    loaded_open: bool,
    loaded_spread_id: String,
    loaded_spread_points: Vec<[f64; 2]>,
    loaded_view_states: BTreeMap<String,(Vec<[f64;2]>,Option<String>)>,
    load_error: String,
    qualification_bounds: Option<egui::Rect>,
    pub drawn_rows: usize,
    pub bounds: Option<egui::Rect>,
    pub loaded_bounds: Option<egui::Rect>,
    pub controls: BTreeMap<String, egui::Rect>,
    source_filter: String,
    last_blotter_refresh: f64,
}
fn data(row: &ContractRow) -> serde_json::Value {
    row.data.as_ref().ok_or_else(|| "Missing binary row data".to_owned()).and_then(|bytes| crate::wire::decode(bytes))
        .unwrap_or_default()
}
fn text(v: &serde_json::Value, key: &str) -> String {
    match &v[key] {
        serde_json::Value::String(s) => s.clone(),
        serde_json::Value::Null => String::new(),
        v => v.to_string(),
    }
}
fn flag(s: &ContractPublication, key: &str) -> bool {
    s.result.context.get(key).is_some_and(|v| v == "True")
}
impl TradeWorkspace {
    /// Explicit opt-in Development profiler. These are display/read actions only.
    pub fn profile_read(&mut self,stage:u8,s:&ContractPublication)->Option<String>{
        if self.pending.is_some()||s.busy{return None;}
        match stage{
            0=>{self.open=true;Some(self.request("load",s))},
            1 if s.result.context.get("tradeId").is_some_and(|id|!id.is_empty())=>{self.tab=1;Some(self.request("compose",s))},
            2 if s.result.context.get("composerSessionId").is_some_and(|id|!id.is_empty())=>{
                self.draft.insert("live".into(),"true".into());Some(self.request("chain",s))
            },_=>None
        }
    }
    pub fn is_visible(&self) -> bool {
        self.open || self.loaded_open || self.pending.is_some() || !self.load_error.is_empty()
    }

    fn background_refresh(&self) -> bool {
        self.pending.is_some() && matches!(self.pending_action.as_str(), "refresh-chain" | "refresh-execution")
    }
    fn interaction_ready(&self, s: &ContractPublication) -> bool {
        (!s.busy && self.pending.is_none()) || (self.background_refresh() && self.deferred_action.is_none())
    }
    /// Release only a request that the bridge explicitly rejected before execution.
    pub fn submission_rejected(&mut self, request_id: &str) {
        if self.pending.as_deref() == Some(request_id) {
            if self.pending_action == "load-trade" {
                self.load_error = "Load Trade request could not be queued.".into();
            }
            self.pending = None;
            self.pending_action.clear();
            self.pending_dialog.clear();
            self.deferred_action = None;
        }
    }
    pub fn request(&mut self, action: &str, s: &ContractPublication) -> String {
        let id = new_request_id();
        self.pending = Some(id.clone());
        self.pending_action = action.into();
        if let Some(trade) = s.result.context.get("tradeId") {
            self.draft.insert("tradeId".into(), trade.clone());
        }
        if matches!(action, "create-order" | "create-trade") {
            self.pending_dialog = self.dialog.clone();
        }
        serde_json::json!({"schema":1,"requestId":id,"view":"trade-orders","intent":action,"expectedGeneration":s.generation,"originalId":s.result.context.get("orderId"),"fields":self.draft}).to_string()
    }
    pub fn draw(&mut self, ctx: &egui::Context, s: &ContractPublication) -> Option<String> {
        if self.pending.as_ref().is_some_and(|id| {
            s.outcomes
                .iter()
                .any(|o| &o.request_id == id && o.state != "Pending")
        }) {
            if let Some(outcome) = s
                .outcomes
                .iter()
                .find(|o| Some(&o.request_id) == self.pending.as_ref())
            {
                if outcome.state == "Completed" && !self.pending_dialog.is_empty() {
                    self.dialog.clear();
                }
                if self.pending_action == "load-trade" {
                    self.open = false;
                    let loaded = outcome.state == "Completed";
                    self.loaded_open |= loaded;
                    if !loaded {
                        self.load_error = if outcome.message.is_empty() {
                            "The selected trade could not be loaded.".into()
                        } else { outcome.message.clone() };
                    }
                }
            }
            if self.pending_action=="close-trade" {
                self.loaded_open=s.result.rows.iter().any(|r|r.fields.get("kind").is_some_and(|k|k=="established"));
            }
            self.pending_dialog.clear();
            self.pending_action.clear();
            self.pending = None;
        }
        self.loaded_bounds = None;
        let mut action = None;
        let mut open = self.open;
        self.controls.clear();
        // FitToTradeView adjusts width/left only. CenterParent retains the native
        // 1100-pixel client height instead of stretching to the central pane.
        let pane = ctx.available_rect();
        let scale = ctx.pixels_per_point();
        let gutter = 7.0 / scale; // Main WinForms frame and splitter gutter, in screen pixels.
        let target_height = (1100.0 / scale + 38.0).min(ctx.screen_rect().height());
        let parent_center = crate::control_parity::native_parent_center(scale)
            .or_else(|| {
                ctx.input(|i| {
                    i.viewport()
                        .outer_rect
                        .zip(i.viewport().inner_rect)
                        .map(|(outer, inner)| outer.center() - inner.min.to_vec2())
                })
            })
            .unwrap_or_else(|| ctx.screen_rect().center());
        let position = egui::pos2(pane.left() + gutter, parent_center.y - target_height / 2.0);
        let frame = egui::Frame::window(&ctx.style())
            .fill(crate::presentation::COMMAND)
            .inner_margin(3)
            .stroke(egui::Stroke::new(3.0_f32, Color32::GRAY));
        let style = ctx.style();
        let title_height = ctx
            .fonts(|fonts| fonts.row_height(&egui::TextStyle::Heading.resolve(&style)))
            .max(style.spacing.interact_size.y)
            + frame.inner_margin.sum().y;
        let chrome = frame.total_margin().sum()
            + egui::vec2(
                0.,
                title_height + frame.stroke.width + style.spacing.item_spacing.y,
            );
        let editor_size = egui::vec2(
            (pane.width() - 2.0 * gutter - chrome.x).max(1.),
            (target_height - chrome.y).max(1.),
        );
        let response = egui::Window::new("Trade Order Editor")
            .enabled(self.dialog != "qualification")
            .id(egui::Id::new("trade-order-editor"))
            .open(&mut open)
            .fixed_pos(position)
            .fixed_size(editor_size + egui::vec2(0., 5. / scale))
            .constrain_to(ctx.screen_rect())
            .frame(frame)
            .collapsible(false)
            .show(ctx, |ui| {
                ui.set_min_size(editor_size + egui::vec2(0., 5. / scale));
                ui.set_max_size(editor_size + egui::vec2(0., 5. / scale));
                ui.painter()
                    .rect_filled(ui.max_rect(), 0., crate::presentation::BACKGROUND);
                ui.visuals_mut().panel_fill = Color32::BLACK;
                ui.scope(|ui| {
                    action = self
                        .draw_selectors(ui, s, position, editor_size + chrome)
                        .or(action);
                    let y = position.y + 139. / scale;
                    ui.painter().line_segment(
                        [
                            egui::pos2(position.x + 15. / scale, y),
                            egui::pos2(position.x + (editor_size.x * scale - 2.) / scale, y),
                        ],
                        egui::Stroke::new(1. / scale, Color32::from_gray(90)),
                    );
                    ui.scope_builder(
                        egui::UiBuilder::new().max_rect(egui::Rect::from_min_size(
                            position + egui::vec2(15., 147.) / scale,
                            egui::vec2(editor_size.x, 32. / scale),
                        )),
                        |ui| {
                            ui.horizontal(|ui| {
                                crate::presentation::tab_value(
                                    ui,
                                    &mut self.tab,
                                    0,
                                    "Trade Orders",
                                );
                                if ui.add_enabled_ui(
                                    self.interaction_ready(s)
                                        && s.result.context.get("tradeId").is_some_and(|id| !id.is_empty()),
                                    |ui| crate::presentation::tab_label(ui, self.tab == 1, "Trade Blotter"),
                                ).inner.clicked()
                                {
                                    self.tab = 1;
                                    action = Some("compose");
                                }
                            });
                        },
                    );
                    if self.tab == 0 {
                        action = self
                            .draw_order_lists(ui, s, position, editor_size + chrome)
                            .or(action);
                    } else {
                        ui.scope_builder(
                            egui::UiBuilder::new().max_rect(egui::Rect::from_min_size(
                                position + egui::vec2(27., 180.) / scale,
                                egui::vec2(
                                    (editor_size + chrome).x - 54. / scale,
                                    (editor_size + chrome).y - 203. / scale,
                                ),
                            )),
                            |ui| {
                                action = self.draw_composer(ui, s).or(action);
                            },
                        );
                    }
                });
                if !s.status.is_empty() && s.status != "Completed" && !(self.background_refresh() && s.status == "Pending") {
                    ui.scope_builder(
                        egui::UiBuilder::new().max_rect(egui::Rect::from_min_size(
                            position + egui::vec2(15., target_height * scale - 27.) / scale,
                            egui::vec2(editor_size.x, 22. / scale),
                        )),
                        |ui| {
                            ui.trading_label(&s.status);
                        },
                    );
                }
            });
        self.bounds = response.map(|shown| shown.response.rect);
        if action == Some("load-trade") { open = false; }
        if self.open && !open && action.is_none() {action=Some("close");}
        self.open = open;
        action = self.draw_loaded_trade(ctx, s).or(action);
        if self.dialog == "eod" {
            action = self.eod(ctx, s).or(action);
        }
        if self.dialog == "qualification" {
            action = self.qualification(ctx, s).or(action);
        }
        if !self.dialog.is_empty() && self.dialog != "qualification" && self.dialog != "eod" {
            let kind = self.dialog.clone();
            let mut keep = true;
            egui::Window::new(match kind.as_str() {
                "create-order" => "Add Fund Order",
                "create-trade" => "Add Trade",
                "remove-order" => "Delete Order",
                _ => "Remove Trade",
            })
            .id(egui::Id::new("trade-dialog"))
            .open(&mut keep)
            .collapsible(false)
            .resizable(false)
            .fixed_size(match kind.as_str() {
                "create-order" => egui::vec2(721., 350.) / scale,
                "create-trade" => egui::vec2(621., 382.) / scale,
                _ => egui::vec2(420., 180.),
            })
            .anchor(
                egui::Align2::CENTER_CENTER,
                pane.center() - ctx.screen_rect().center(),
            )
            .show(ctx, |ui| {
                ui.add_enabled_ui(!s.busy, |ui| {
                    if kind == "create-order" || kind == "create-trade" {
                        if let Some(draft_action) = self.draw_draft(ui, s, &kind) {
                            if draft_action == "cancel-draft" {
                                self.dialog.clear();
                            }
                            action = Some(draft_action);
                        }
                    } else {
                        ui.trading_caption("Remove the selected record?");
                        ui.trading_caption("Reason:");
                        ui.text_edit_singleline(self.draft.entry("reason".into()).or_default());
                    }
                    if !kind.starts_with("create") {
                        ui.separator();
                        ui.horizontal(|ui| {
                            if ui
                                .add_sized(
                                    [120., 32.],
                                    egui::Button::new(if kind.starts_with("create") {
                                        "Save"
                                    } else {
                                        "Remove"
                                    }),
                                )
                                .clicked()
                            {
                                action = Some(match kind.as_str() {
                                    "create-order" => "create-order",
                                    "create-trade" => "create-trade",
                                    "remove-order" => "remove-order",
                                    _ => "remove-trade",
                                });
                                self.dialog.clear();
                            }
                            if ui
                                .add_sized([120., 32.], egui::Button::new("Cancel"))
                                .clicked()
                            {
                                self.dialog.clear();
                            }
                        });
                    }
                });
            });
            if !keep {
                self.dialog.clear();
            }
        }
        if let Some(raw) = s.result.documents.get("confirmation") {
            if let Ok(value) = crate::wire::decode::<serde_json::Value>(raw) {
                let mut keep = true;
                let mut decision = None;
                let submitting = value["submitting"].as_bool().unwrap_or(false);
                egui::Window::new("Submit Order")
                    .enabled(!submitting)
                    .id(egui::Id::new("trade-confirmation"))
                    .open(&mut keep)
                    .fixed_size([620., 405.])
                    .anchor(
                        egui::Align2::CENTER_CENTER,
                        pane.center() - ctx.screen_rect().center(),
                    )
                    .collapsible(false)
                    .resizable(false)
                    .show(ctx, |ui| {
                        if submitting { ui.label("Submitting ? awaiting broker acceptance?"); }
                        Self::confirmation_legs_grid(ui, &value["legs"], s);
                        ui.separator();
                        for (key, label) in [
                            ("orderQuantity", "Order Quantity:"),
                            ("orderPrice", "Order Price:"),
                            ("orderAmount", "Order Amount:"),
                            ("commission", "Total Commission:"),
                            ("totalAmount", "Total Amount:"),
                        ] {
                            Self::confirmation_field(ui, label, |ui| {
                                ui.trading_label(text(&value["order"], key));
                            });
                        }
                        let fill = self
                            .draft
                            .entry("fillType".into())
                            .or_insert("Broker".into());
                        Self::confirmation_field(ui, "Broker Type:", |ui| {
                            egui::ComboBox::from_id_salt("confirm-fill")
                                .width(ui.available_width())
                                .selected_text(&*fill)
                                .show_ui(ui, |ui| {
                                    ui.selectable_value(fill, "Manual".into(), "Manual");
                                    ui.selectable_value(fill, "Broker".into(), "Broker");
                                });
                        });
                        ui.horizontal(|ui| {
                            let buttons_width = 280. + ui.spacing().item_spacing.x;
                            ui.add_space(((ui.available_width() - buttons_width) / 2.).max(0.));
                            if ui
                                .add_sized([140., 32.], egui::Button::new("Confirm"))
                                .clicked()
                            {
                                decision = Some("confirm-order");
                            }
                            if ui
                                .add_sized([140., 32.], egui::Button::new("Cancel"))
                                .clicked()
                            {
                                decision = Some("cancel-order-confirmation");
                            }
                        });
                    });
                if !keep && !submitting {
                    decision = Some("cancel-order-confirmation");
                }
                if let Some(intent) = decision {
                    return Some(serde_json::json!({"schema":1,"requestId":new_request_id(),"view":"trade-orders","intent":intent,"expectedGeneration":s.generation,"originalId":value["id"],"fields":self.draft}).to_string());
                }
            }
        }
        if let Some(error) = s.result.context.get("submissionError")
            .filter(|_| !s.result.documents.contains_key("confirmation")) {
            if !error.is_empty() && *error != self.dismissed_submission_error {
                egui::Window::new("Order Submission Error")
                    .id(egui::Id::new("submission-error"))
                    .collapsible(false).resizable(false)
                    .anchor(egui::Align2::CENTER_CENTER, egui::Vec2::ZERO)
                    .show(ctx, |ui| {
                        ui.set_max_width(560.);
                        ui.label(error);
                        if ui.button("OK").clicked() { self.dismissed_submission_error = error.clone(); }
                    });
            }
        }
        if !self.load_error.is_empty() {
            egui::Window::new("Load Trade Error")
                .id(egui::Id::new("load-trade-error"))
                .collapsible(false).resizable(false)
                .anchor(egui::Align2::CENTER_CENTER, egui::Vec2::ZERO)
                .show(ctx, |ui| {
                    ui.set_max_width(560.);
                    ui.label(&self.load_error);
                    if ui.button("OK").clicked() { self.load_error.clear(); }
                });
        }
        if !open && !self.loaded_open && self.pending.is_none()
            && self.load_error.is_empty() && action != Some("load-trade") {
            self.deferred_action = None;
            action = Some("close");
        }
        if let Ok(path) = std::env::var("IFM_RUST_TRADE_METRICS") {
            let controls: BTreeMap<_, _> = self
                .controls
                .iter()
                .map(|(id, r)| (id, [r.left(), r.top(), r.width(), r.height()]))
                .collect();
            let _ = std::fs::write(
                path,
                serde_json::json!({"pixelsPerPoint":ctx.pixels_per_point(),"controls":controls,"backgroundRefresh":self.background_refresh(),"gatewayBusy":s.busy,"pendingRequestId":self.pending,"pendingAction":self.pending_action,"deferredAction":self.deferred_action,"composeTab":self.compose_tab,"frameTime":ctx.input(|i|i.time),"pointer":ctx.input(|i|i.pointer.latest_pos().map(|p|[p.x,p.y]))})
                    .to_string(),
            );
        }
        // Refreshes stay serialized at the managed boundary. Queue user input until their acknowledgement,
        // keeping its draft and the currently displayed rows intact rather than rejecting a busy submission.
        if self.background_refresh() {
            if let Some(next) = action { self.deferred_action = Some(next); }
            return None;
        }
        if action.is_none() && self.pending.is_none() && !s.busy {
            action = self.deferred_action.take();
        }
        action.map(|a| {
            let mut payload: serde_json::Value = serde_json::from_str(&self.request(a, s)).unwrap();
            if a == "order" {
                payload["originalId"] = self
                    .draft
                    .get("orderId")
                    .cloned()
                    .unwrap_or_default()
                    .into();
            }
            payload.to_string()
        })
    }
    fn draw_composer(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
    ) -> Option<&'static str> {
        self.draw_blotter(ui, s)
    }
    fn eod(&mut self, ctx: &egui::Context, s: &ContractPublication) -> Option<&'static str> {
        let mut action = None;
        let mut open = true;
        egui::Window::new("Trade End Of Day")
            .open(&mut open)
            .fixed_size([543., 477.])
            .anchor(
                egui::Align2::CENTER_CENTER,
                ctx.available_rect().center() - ctx.screen_rect().center(),
            )
            .show(ctx, |ui| {
                let value = s
                    .result
                    .rows
                    .iter()
                    .find(|r| r.fields.get("kind").is_some_and(|k| k == "eod"))
                    .map(data)
                    .unwrap_or_default();
                for (key, label) in [
                    ("fundId", "Fund Id"),
                    ("orderId", "Order Id"),
                    ("tradeId", "Trade Id"),
                ] {
                    ui.horizontal(|ui| {
                        ui.add_sized([170., 28.], crate::presentation::caption_widget(label));
                        ui.trading_label(text(&value, key));
                    });
                }
                ui.horizontal(|ui| {
                    ui.add_sized([170., 28.], crate::presentation::caption_widget("Value Date:"));
                    let date = s
                        .result
                        .context
                        .get("valueDate")
                        .cloned()
                        .unwrap_or_default();
                    ui.text_edit_singleline(self.draft.entry("eodDate".into()).or_insert(date));
                });
                for (key, label) in [
                    ("openPrice", "Open Price"),
                    ("highPrice", "High Price"),
                    ("lowPrice", "Low Price"),
                    ("closePrice", "Close Price"),
                    ("volume", "Volume"),
                    ("tradePnl", "Trade Pnl"),
                    ("fundBalance", "Fund Balance"),
                ] {
                    ui.horizontal(|ui| {
                        ui.add_sized([170., 28.], crate::presentation::caption_widget(label));
                        ui.trading_label(text(&value, key));
                    });
                }
                ui.horizontal(|ui| {
                    ui.add_sized([170., 28.], crate::presentation::caption_widget("Reference:"));
                    ui.text_edit_singleline(self.draft.entry("reference".into()).or_default());
                });
                ui.horizontal(|ui| {
                    if ui.add_enabled(!s.busy, egui::Button::new("Load")).clicked() {
                        action = Some("eod-load");
                    }
                    if ui
                        .add_enabled(
                            !s.busy && value["canRun"] == true,
                            egui::Button::new("Run End Of Day"),
                        )
                        .clicked()
                    {
                        action = Some("eod-run");
                    }
                });
                ui.trading_label(text(&value, "lastStatusMessage"));
            });
        if !open {
            self.dialog.clear();
        }
        action
    }
    fn qualification(
        &mut self,
        ctx: &egui::Context,
        s: &ContractPublication,
    ) -> Option<&'static str> {
        use order_layout::{at, rect};
        let mut action = None;
        let mut open = true;
        let p = ctx.pixels_per_point();
        let reference_scale = 1.25;
        let account = s
            .result
            .rows
            .iter()
            .find(|r| r.fields.get("kind").is_some_and(|k| k == "account"))
            .map(data)
            .unwrap_or_default();
        let available = !account.is_null();
        let revision = text(&account, "revision");
        if available && self.draft.get("qualificationRevision") != Some(&revision) {
            for (key, field) in [
                ("manifestHash", "manifestHash"),
                ("evidenceReference", "evidenceReference"),
            ] {
                self.draft.insert(key.into(), text(&account, field));
            }
            self.draft.insert("qualificationRevision".into(), revision);
        }
        self.draft
            .entry("reviewer".into())
            .or_insert_with(|| std::env::var("USERNAME").unwrap_or_default());
        let report = s
            .result
            .rows
            .iter()
            .find(|r| {
                r.fields
                    .get("kind")
                    .is_some_and(|k| k == "qualification-report")
            })
            .map(data)
            .unwrap_or_default();
        let hash = text(&report, "hash");
        if !hash.is_empty() && self.draft.get("loadedReport") != Some(&hash) {
            self.draft.insert("manifestHash".into(), hash.clone());
            self.draft
                .insert("evidenceReference".into(), text(&report, "path"));
            self.draft.insert("loadedReport".into(), hash);
        }
        let style = ctx.style();
        let frame = egui::Frame::window(&style);
        let title_height = ctx
            .fonts(|fonts| fonts.row_height(&egui::TextStyle::Heading.resolve(&style)))
            .max(style.spacing.interact_size.y)
            + frame.inner_margin.sum().y;
        let chrome = frame.total_margin().sum()
            + egui::vec2(
                0.,
                title_height + frame.stroke.width + style.spacing.item_spacing.y,
            );
        let size = egui::vec2(840., 600.);
        let parent = self.bounds.unwrap_or(ctx.available_rect());
        let response=egui::Window::new("Emulator Broker Account Qualification").open(&mut open).default_pos(parent.center()-size/2.).fixed_size(size-chrome+egui::vec2(0.,5./p)).show(ctx,|ui|{
            ui.set_min_size(size-chrome+egui::vec2(0.,5./p));
            ui.painter().rect_filled(ui.max_rect(),0.,Color32::BLACK);
            let origin=self.qualification_bounds.map(|r|r.min).unwrap_or(ui.cursor().min-egui::vec2(frame.total_margin().sum().x/2.,title_height));
            let r=|x,y,w,h|rect(origin,reference_scale,x,y,w,h);
            let approval=text(&account,"approvalId");let approval=if approval.replace("-","").chars().all(|c|c=='0'){"None".into()}else{approval.replace("-","")};let state=if available{format!("{} / {} | Qualification={} | Gate={} | Hold={} | Approval={} | Revision={} | {}",text(&account["id"],"accountAlias"),text(&account,"environment"),text(&account,"qualificationStatus"),text(&account,"gate"),text(&account,"manualHold"),approval,text(&account,"revision"),text(&account,"reason"))}else{"Account unavailable".into()};
            for(caption,y)in[("Current state",65.),("Qualification report:",119.),("Report contents:",163.),("Manifest SHA-256",320.),("Evidence reference",356.),("Authorized reviewer",393.),("Hold / revoke reason",429.),("Durable actions",468.)]{at(ui,r(34.,y,260.,29.),caption,|ui|{ui.trading_label(egui::RichText::new(caption).color(crate::presentation::CAPTION_FOREGROUND));});}
            at(ui,r(313.,65.,700.,49.),"qualification-state",|ui|{ui.trading_label(state);});
            at(ui,r(311.,119.,155.,38.),"loadQualificationReport",|ui|{if ui.add_sized(ui.available_size(),egui::Button::new("Load test report...")).clicked(){if let Some(path)=blotter::pick_report(){self.draft.insert("reportPath".into(),path);action=Some("qualification-report");}}});
            at(ui,r(313.,163.,700.,150.),"qualificationReportPreview",|ui|{let mut content=text(&report,"preview");ui.add_sized(ui.available_size(),egui::TextEdit::multiline(&mut content).interactive(false));});
            for(key,id,y)in[("manifestHash","qualificationManifestHash",320.),("evidenceReference","qualificationEvidenceReference",356.),("reviewer","qualificationReviewer",393.),("reason","qualificationReason",429.)]{let bounds=r(313.,y,700.,29.);at(ui,bounds,id,|ui|{ui.visuals_mut().extreme_bg_color=Color32::from_gray(30);ui.add_sized(ui.available_size(),egui::TextEdit::singleline(self.draft.entry(key.into()).or_default()));});self.controls.insert(id.into(),bounds);}
            for(label,intent,id,x,y,width)in[("Submit evidence","qualification-submit","submitQualificationEvidence",315.,468.,148.),("Accept reviewed manifest","qualification-accept","acceptQualification",468.,468.,216.),("Revoke","qualification-revoke","revokeQualification",689.,468.,94.),("Set hold","qualification-hold","setTradingHold",788.,468.,94.),("Release hold","qualification-release","releaseTradingHold",886.,468.,121.),("Resynchronize","qualification-resync","resynchronizeBrokerAccount",315.,510.,135.)]{let enabled=!s.busy&&self.pending.is_none()&&available&&match intent{"qualification-accept"=>text(&account,"qualificationStatus")=="ReviewPending","qualification-revoke"=>text(&account,"qualificationStatus")=="Accepted","qualification-hold"=>account["manualHold"]==false,"qualification-release"=>account["manualHold"]==true,_=>true};let bounds=r(x,y,width,38.);at(ui,bounds,id,|ui|{if ui.add_enabled(enabled,egui::Button::new(label).min_size(ui.available_size())).clicked(){if intent=="qualification-accept"{self.draft.insert("qualificationConfirm".into(),"true".into());}else{action=Some(intent);}}});self.controls.insert(id.into(),bounds);}
            if self.draft.get("qualificationConfirm").is_some_and(|v|v=="true"){at(ui,r(34.,560.,975.,70.),"qualification-confirm",|ui|{ui.trading_caption("Accept emulator qualification for the reviewed manifest?");ui.horizontal(|ui|{if ui.button("Yes, accept manifest").clicked(){self.draft.remove("qualificationConfirm");action=Some("qualification-accept");}if ui.button("No").clicked(){self.draft.remove("qualificationConfirm");}});});}
            if !s.status.is_empty()&&s.status!="Completed"{at(ui,r(34.,650.,975.,55.),"qualification-status",|ui|{ui.trading_label(&s.status);});}
        });
        if let Some(previous) = self.qualification_bounds {
            let r = |x, y, w, h| order_layout::rect(previous.min, 1.25, x, y, w, h);
            self.controls
                .insert("loadQualificationReport".into(), r(311., 119., 155., 38.));
            self.controls.insert(
                "qualificationReportPreview".into(),
                r(313., 163., 700., 150.),
            );
        }
        if let Some(response) = response {
            self.qualification_bounds = Some(response.response.rect);
            self.controls
                .insert("qualificationWindow".into(), response.response.rect);
        }
        if ctx.input(|i| i.key_pressed(egui::Key::Escape)) {
            open = false;
        }
        if !open {
            self.dialog.clear();
        }
        action
    }
    fn table(
        &mut self,
        ui: &mut egui::Ui,
        s: &ContractPublication,
        kind: &str,
        columns: &[(&str, &str)],
        height: f32,
    ) -> Option<String> {
        let rows: Vec<_> = s
            .result
            .rows
            .iter()
            .filter(|r| r.fields.get("kind").is_some_and(|k| k == kind))
            .filter(|r| {
                kind != "order"
                    || self.source_filter.is_empty()
                    || self.source_filter == "All"
                    || text(&data(r), "source") == self.source_filter
            })
            .collect();
        let widths: &[f32] = match kind {
            "order" => &[80., 110., 125., 800., 150.],
            "trade" => &[80., 110., 119., 110., 387.],
            _ => &[],
        };
        let layout: Vec<_> = columns
            .iter()
            .enumerate()
            .map(|(i, (_, title))| (*title, widths.get(i).copied().unwrap_or(180.)))
            .collect();
        let selected = rows.iter().position(|r| {
            s.result
                .context
                .get(if kind == "order" {
                    "orderId"
                } else {
                    "tradeId"
                })
                .is_some_and(|id| r.id == format!("{kind}:{id}"))
        });
        let (clicked, drawn) = crate::control_parity::report_list(
            ui,
            kind,
            &layout,
            rows.len(),
            selected,
            height,
            |row, column| text(&data(rows[row]), columns[column].0),
        );
        self.drawn_rows = drawn;
        clicked.map(|index| rows[index].id.split_once(':').unwrap().1.to_string())
    }

    fn properties(ui: &mut egui::Ui, value: &serde_json::Value) {
        egui::ScrollArea::both().show(ui, |ui| {
            egui::Grid::new("trade-properties")
                .striped(true)
                .show(ui, |ui| {
                    if let Some(fields) = value.as_object() {
                        for (key, value) in fields {
                            ui.trading_caption(key);
                            ui.trading_label(match value {
                                serde_json::Value::String(s) => s.clone(),
                                _ => value.to_string(),
                            });
                            ui.end_row();
                        }
                    }
                });
        });
    }
    fn confirmation_field(ui: &mut egui::Ui, label: &str, value: impl FnOnce(&mut egui::Ui)) {
        let (row, _) = ui.allocate_exact_size(egui::vec2(ui.available_width(), 28.), egui::Sense::hover());
        let label_rect = egui::Rect::from_min_size(row.min, egui::vec2(170., row.height()));
        let value_rect = egui::Rect::from_min_max(
            egui::pos2(label_rect.right() + 6., row.top()), row.max);
        for rect in [label_rect, value_rect] {
            ui.painter().rect_stroke(rect, 0.,
                egui::Stroke::new(1.0_f32, Color32::from_rgb(80, 80, 84)), egui::StrokeKind::Inside);
        }
        let layout = egui::Layout::left_to_right(egui::Align::Center);
        ui.scope_builder(egui::UiBuilder::new().max_rect(label_rect.shrink2(egui::vec2(5., 0.)))
            .layout(layout), |ui| { ui.add(crate::presentation::caption_widget(label)); });
        ui.scope_builder(egui::UiBuilder::new().max_rect(value_rect.shrink2(egui::vec2(5., 0.)))
            .layout(layout), value);
    }

    fn confirmation_legs_grid(ui: &mut egui::Ui, value: &serde_json::Value, s: &ContractPublication) {
        let selected: Vec<_> = s.result.rows.iter()
            .filter(|row| row.fields.get("kind").is_some_and(|kind| kind == "leg"))
            .map(data).collect();
        let mut rows: Vec<_> = value.as_array().into_iter().flatten().collect();
        // Use the displayed Broker Trade order; only the dialog's presentation is reordered.
        rows.sort_by_key(|leg| selected.iter().position(|selected|
            selected["contractId"] == leg["contractId"]).unwrap_or(usize::MAX));
        egui::Frame::new()
            .stroke(egui::Stroke::new(1.0_f32, Color32::from_rgb(80, 80, 84)))
            .show(ui, |ui| {
                ui.set_min_width(ui.available_width());
                egui_extras::TableBuilder::new(ui)
                    .id_salt("confirmation-order-legs")
                    .striped(true)
                    .vscroll(true)
                    .max_scroll_height(135.)
                    .column(egui_extras::Column::remainder().clip(true))
                    .column(egui_extras::Column::exact(125.).clip(true))
                    .column(egui_extras::Column::exact(65.).clip(true))
                    .column(egui_extras::Column::exact(135.).clip(true))
                    .header(26., |mut row| {
                        for label in ["Contract ID", "Role", "Side", "Leg Commission"] {
                            row.col(|ui| {
                                ui.painter().rect_filled(ui.max_rect(), 0., Color32::from_rgb(45, 45, 48));
                                ui.add(egui::Label::new(egui::RichText::new(label).color(crate::presentation::CAPTION_FOREGROUND)).truncate());
                            });
                        }
                    })
                    .bordered_body(26., |body| {
                        body.rows(27., rows.len(), |mut row| {
                            let leg = &rows[row.index()];
                            let commission = leg["legCommission"].wire_number().or_else(||leg["legCommission"].as_str().and_then(|text|text.parse::<f64>().ok()))
                                .map(|v| format!("USD {v:.2}"))
                                .unwrap_or_else(|| text(leg, "legCommission"));
                            for value in [text(leg, "contractId"), text(leg, "role"), text(leg, "side"), commission] {
                                row.col(|ui| {
                                    ui.add(egui::Label::new(&value).truncate()).on_hover_text(&value);
                                });
                            }
                        });
                    });
            });
    }

}
