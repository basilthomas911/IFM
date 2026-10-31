//! Portfolio Administration and its embedded Risk Policy editor.
//!
//! Managed documents describe portfolios, funds, metrics, property fields, grids,
//! dialogs, and action eligibility. Native state holds drafts, filters, tab selection,
//! pending request IDs, and measured control rectangles. Portfolio and fund identities
//! are supplied by the bridge; row position is never a business identifier.
//!
//! Portfolio and Funds grids occupy the upper third. Funds embeds Financials/Risk
//! History below its grid, with a State filter and Fund Actions in the right rail.
//! The Portfolio grid has Risk Policy/Versions below it
//! and metrics/status at the bottom. Risk Policy Actions, Save, and Cancel occupy the
//! right rail. Change Policy edits through the managed immutable-version workflow.
//! Dirty policy fields survive unrelated publications; switching selection while
//! editing is blocked until Save/Cancel. Child rectangles are bounded to avoid
//! immediate-mode layout feedback that could grow the dialog on pointer movement.

//! Portfolio administration surfaces; business commands remain in the managed service adapter.
use crate::contract_editor::ContractPublication;
use crate::control_parity::{BorderedTable as _, FilledGrid as _};
use crate::presentation::{self, TradingLabelUi as _};
use eframe::egui;
use std::collections::BTreeMap;

use crate::models::portfolio::{Dialog, Document, Field, Grid};
impl Dialog {
    /// Transaction feedback uses the administration status line rather than a second line in the editor.
    fn has_inline_status(&self) -> bool {
        self.key != "posting" && !self.status.is_empty()
    }
    /// Place actions directly below the last field in compact Portfolio and Fund editors.
    fn actions_follow_fields(&self) -> bool {
        matches!(
            self.key.as_str(),
            "posting"
                | "allocation"
                | "envelope"
                | "assignment"
                | "new-fund"
                | "version-fund"
                | "fund-state"
                | "new-portfolio"
                | "version-portfolio"
                | "portfolio-state"
        )
    }
    /// Small editors share aligned captions, fixed sizing and a centered action footer.
    fn uses_compact_layout(&self) -> bool {
        matches!(
            self.key.as_str(),
            "posting"
                | "allocation"
                | "envelope"
                | "assignment"
                | "new-fund"
                | "version-fund"
                | "fund-state"
                | "new-portfolio"
                | "version-portfolio"
                | "portfolio-state"
        )
    }
}
/// Native portfolio/fund administration state with local drafts, pending request, tabs, and control geometry.
#[derive(Default)]
pub struct PortfolioManager {
    /// Whether the view/dialog is currently shown.
    pub open: bool,
    /// Whether the initial manager document has been requested/received.
    loaded: bool,
    /// Current asynchronous request/work item awaiting completion; cleared only by its matching outcome or explicit host failure handling.
    pending: Option<String>,
    /// Publication generation used for change detection and request correlation.
    generation: u64,
    /// Immutable decoded Portfolio document shared across rendering scopes.
    document: std::sync::Arc<Document>,
    /// Local draft values indexed by dialog/property path; preserved while edits are pending.
    fields: BTreeMap<String, String>,
    /// Editable query filters retained between frames.
    filters: BTreeMap<String, String>,
    /// Stable row identities indexed by grid key.
    selections: BTreeMap<String, String>,
    /// Selected editor section/tab key or index.
    section: usize,
    /// Selected Fund Details subtab index.
    details_tab: usize,
    /// Last automatic subtab request, preventing failed loads from retrying every frame.
    requested_fund_view: Option<(String, String)>,
    /// Selected child-grid tab indexes keyed by dialog/view identity.
    tabs: BTreeMap<String, usize>,
    /// User-visible validation, submission, or transport error text.
    error: String,
    /// Whether a multiline field owns focus, affecting keyboard shortcuts.
    multiline_focus: bool,
    /// Current bounded policy value-editor width calculated from available form space.
    policy_value_width: f32,
    /// Latest logical-point control rectangles keyed by stable names for layout/accessibility verification.
    pub controls: BTreeMap<String, egui::Rect>,
}
/// Portfolio intent tuple: action key, optional row identity, and named string fields.
type Action = (String, Option<String>, BTreeMap<String, String>);
impl PortfolioManager {
    /// Show Portfolio Administration and mark its initial document unloaded so the next frame requests fresh data.
    pub fn open(&mut self) {
        self.open = true;
        self.loaded = false;
        self.requested_fund_view = None;
    }
    /// Release only the matching request that failed before backend execution and expose its submission error.
    pub fn submission_rejected(&mut self, id: &str, error: &str) {
        if self.pending.as_deref() == Some(id) {
            self.pending = None;
            self.error = error.into();
        }
    }
    /// Record a transport error and release local pending UI state; this does not confirm a business command's outcome.
    pub fn transport_failed(&mut self, error: &str) {
        if self.pending.take().is_some() {
            self.error = error.into();
        }
    }
    /// Release the current pending UI submission after the host reports a queue rejection.
    pub fn reject_submission(&mut self) {
        self.pending = None;
        self.error = "The request queue is full. Close and reopen to retry.".into();
    }
    /// Build a correlated user intent against the current publication generation and retain its request ID as pending. The caller must enqueue the returned payload.
    fn request(&mut self, action: Action, publication: &ContractPublication) -> String {
        let id = crate::lookup_editor::new_request_id();
        self.pending = Some(id.clone());
        self.error.clear();
        serde_json::json!({"schema":1,"requestId":id,"view":"portfolio","intent":action.0,"expectedGeneration":publication.generation,"originalId":action.1,"fields":action.2}).to_string()
    }
    /// Create an action tuple with no row identity or additional fields.
    fn action(name: &str) -> Action {
        (name.into(), None, BTreeMap::new())
    }
    /// Draw an enabled/disabled action and record its control bounds for layout verification.
    fn button(
        ui: &mut egui::Ui,
        controls: &mut BTreeMap<String, egui::Rect>,
        key: &str,
        text: &str,
        enabled: bool,
    ) -> bool {
        let r = ui.add_enabled(
            enabled,
            egui::Button::new(text).min_size(egui::vec2(0., 26.)),
        );
        controls.insert(key.into(), r.rect);
        r.clicked()
    }
    /// Consume a changed Portfolio document and matching outcomes, draw bounded administration/dialog views, and return a correlated user intent.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        publication: &ContractPublication,
        live: bool,
    ) -> Option<String> {
        if !self.open {
            return None;
        }
        if let Some(outcome) = self.pending.as_ref().and_then(|id| {
            publication
                .outcomes
                .iter()
                .find(|o| &o.request_id == id && o.state != "Pending")
        }) {
            if outcome.state != "Completed" {
                self.error = outcome.message.clone();
            }
            self.pending = None;
        }
        if publication.result.view == "portfolio"
            && self.generation != publication.generation
            && !publication.busy
        {
            if let Some(document) = publication.result.document::<Document>("portfolio") {
                let preserve_policy = self
                    .document
                    .dialogs
                    .iter()
                    .chain(self.document.policy.iter())
                    .any(|d| d.key == "risk-policy" && d.fields.iter().any(|f| f.enabled))
                    && document
                        .dialogs
                        .iter()
                        .chain(document.policy.iter())
                        .any(|d| d.key == "risk-policy" && d.fields.iter().any(|f| f.enabled));
                let previous = std::mem::take(&mut self.fields);
                self.document = std::sync::Arc::new(document);
                self.generation = publication.generation;
                self.fields.clear();
                for (key, value) in &self.document.context {
                    self.fields.insert(key.clone(), value.clone());
                }
                for dialog in self
                    .document
                    .dialogs
                    .iter()
                    .chain(self.document.policy.iter())
                    .chain(self.document.fund_view.iter())
                {
                    for field in &dialog.fields {
                        self.fields
                            .insert(format!("{}.{}", dialog.key, field.key), field.value.clone());
                    }
                }
                if preserve_policy {
                    for (key, value) in previous {
                        if key.starts_with("risk-policy.") {
                            self.fields.insert(key, value);
                        }
                    }
                }
            }
        }
        let ready = live && !publication.busy && self.pending.is_none();
        let document = self.document.clone();
        let editing_policy = document
            .policy
            .as_ref()
            .is_some_and(|d| d.fields.iter().any(|f| f.enabled));
        let mut action = None;
        self.multiline_focus = false;
        let mut open = self.open;
        self.controls.clear();
        let size = egui::vec2(1434., 861.)
            .min((ctx.screen_rect().size() - egui::vec2(24., 64.)).max(egui::vec2(650., 450.)));
        let parent = egui::Window::new(presentation::dialog_title("Portfolio Administration"))
            .id(egui::Id::new("portfolio-administration"))
            .open(&mut open)
            .default_pos(ctx.screen_rect().center() - size / 2.)
            .fixed_size(size)
            .resizable(false)
            .collapsible(false)
            .show(ctx, |ui| {
                if !document.dialogs.is_empty() {
                    ui.disable();
                }
                let bounds = ui.available_rect_before_wrap();
                // Keep the fixed window's viewport independent of content minimum
                // sizes. Hover repaints must not enlarge the outer window to fit a grid
                // or a metric value.
                let mut content = ui.new_child(egui::UiBuilder::new().max_rect(bounds));
                ui.allocate_rect(bounds, egui::Sense::hover());
                let ui = &mut content;
                self.controls.insert("manager".into(), bounds);
                ui.set_clip_rect(bounds.intersect(ui.clip_rect()));
                ui.add_enabled_ui(!editing_policy, |ui| {
                    ui.horizontal(|ui| {
                        for (index, title) in
                            ["Portfolios", "Funds", "Fund Details"].iter().enumerate()
                        {
                            if tab(ui, self.section == index, *title).clicked() {
                                self.section = index;
                            }
                        }
                    });
                });
                ui.add_space(6.);
                if self.section == 2 {
                    ui.horizontal(|ui| {
                        for (caption, key) in
                            [("Fund:", "fundName"), ("Portfolio:", "portfolioName")]
                        {
                            ui.add(presentation::caption_widget(caption));
                            ui.add(
                                presentation::label_widget(
                                    document.context.get(key).map(String::as_str).unwrap_or(""),
                                )
                                .truncate(),
                            );
                            ui.add_space(12.);
                        }
                    });
                }
                let key = if self.section == 0 {
                    "portfolios"
                } else if self.section == 1 {
                    "funds"
                } else {
                    ui.horizontal(|ui| {
                        for (i, title) in ["Fund", "Allocation", "Risk", "Assignments"]
                            .iter()
                            .enumerate()
                        {
                            if tab(ui, self.details_tab == i, *title).clicked() {
                                self.details_tab = i;
                            }
                        }
                    });
                    ["fund", "allocation", "envelope", "assignments"][self.details_tab]
                };
                let line_height = ui.text_style_height(&egui::TextStyle::Body);
                let spacing = ui.spacing().item_spacing.y;
                let metrics_height = line_height + spacing + ui.spacing().interact_size.y;
                let footer_height = metrics_height + spacing + line_height;
                let grid_height =
                    (bounds.bottom() - ui.cursor().top() - footer_height - spacing).max(50.);
                if self.section == 0 {
                    self.draw_portfolio_page(
                        ui,
                        &document,
                        bounds,
                        grid_height,
                        ready,
                        &mut action,
                    );
                } else if self.section == 1 {
                    self.draw_funds_page(ui, &document, bounds, grid_height, ready, &mut action);
                } else {
                    let page = egui::Rect::from_min_size(
                        ui.cursor().min,
                        egui::vec2(bounds.width(), grid_height),
                    );
                    let rail_width = 175.;
                    let grid_rect = egui::Rect::from_min_max(
                        page.min,
                        egui::pos2(page.right() - rail_width - 8., page.bottom()),
                    );
                    ui.scope_builder(egui::UiBuilder::new().max_rect(grid_rect), |ui| {
                        ui.set_clip_rect(grid_rect.intersect(ui.clip_rect()));
                        if let Some(grid) = document.grids.iter().find(|g| g.key == key) {
                            draw_grid(
                                ui,
                                grid,
                                grid.selected.as_deref(),
                                grid_height,
                                &mut self.fields,
                                &mut self.controls,
                                ready,
                            );
                        }
                    });
                    let rail = egui::Rect::from_min_max(
                        egui::pos2(grid_rect.right() + 8., page.top()),
                        page.max,
                    );
                    ui.scope_builder(egui::UiBuilder::new().max_rect(rail), |ui| {
                        ui.set_clip_rect(rail.intersect(ui.clip_rect()));
                        let button = ui.add_enabled(
                            ready,
                            egui::Button::new("Fund Detail Actions")
                                .min_size(egui::vec2(rail_width, 26.)),
                        );
                        self.controls
                            .insert("fund-detail-actions".into(), button.rect);
                        egui::Popup::menu(&button).show(|ui| {
                            for (label, view) in [
                                ("Allocation...", "allocation"),
                                ("Risk Envelope...", "envelope"),
                                ("Trade Assignment...", "assignment"),
                            ] {
                                let response = ui.add_enabled(
                                    document.enabled.get(label).copied().unwrap_or(false),
                                    egui::Button::new(label),
                                );
                                self.controls
                                    .insert(format!("fund-detail-menu-{view}"), response.rect);
                                if response.clicked() {
                                    action = Some((
                                        "portfolio-open".into(),
                                        None,
                                        BTreeMap::from([("view".into(), view.into())]),
                                    ));
                                    ui.close();
                                }
                            }
                        });
                    });
                    ui.allocate_rect(page, egui::Sense::hover());
                }
                let footer = egui::Rect::from_min_max(
                    egui::pos2(bounds.left(), bounds.bottom() - footer_height),
                    bounds.max,
                );
                self.controls.insert("metrics-and-status".into(), footer);
                ui.scope_builder(egui::UiBuilder::new().max_rect(footer), |ui| {
                    ui.columns(10, |columns| {
                        for (i, title) in [
                            "Win Rate",
                            "Avg Profit",
                            "Loss Rate",
                            "Avg Loss",
                            "W/L Ratio",
                            "Sharpe Ratio",
                            "P&L",
                            "P&L (%)",
                            "Commission",
                            "Max DD (%)",
                        ]
                        .iter()
                        .enumerate()
                        {
                            columns[i]
                                .add(presentation::caption_widget(*title))
                                .on_hover_text(
                                    document
                                        .context
                                        .get("metricsBasis")
                                        .map(String::as_str)
                                        .unwrap_or(""),
                                );
                            let mut value = document
                                .context
                                .get(&format!("metric{i}"))
                                .cloned()
                                .unwrap_or("N/A".into());
                            columns[i].add_enabled(
                                false,
                                egui::TextEdit::singleline(&mut value).desired_width(f32::INFINITY),
                            );
                        }
                    });
                    let status = document
                        .context
                        .get("status")
                        .map(|message| message.replace(['\r', '\n'], " "))
                        .unwrap_or_default();
                    let status_rect = egui::Rect::from_min_max(
                        egui::pos2(bounds.left(), bounds.bottom() - line_height),
                        bounds.max,
                    );
                    ui.put(
                        status_rect,
                        presentation::label_widget(&status)
                            .truncate()
                            .halign(egui::Align::Min),
                    );
                });
                ui.allocate_rect(bounds, egui::Sense::hover());
            });
        if let Some(parent) = parent {
            if document.dialogs.is_empty() {
                ctx.memory_mut(|m| m.set_modal_layer(parent.response.layer_id));
            }
        }
        self.open &= open;
        for (index, dialog) in document.dialogs.iter().enumerate() {
            let mut visible = true;
            let status_height = if dialog.actions_follow_fields() && dialog.has_inline_status() {
                28.
            } else {
                0.
            };
            let dialog_height = if dialog.key == "ledger" {
                470.
            } else {
                dialog.height
            };
            let size = egui::vec2(dialog.width - 16., dialog_height - 39. + status_height)
                .min((ctx.screen_rect().size() - egui::vec2(24., 64.)).max(egui::vec2(400., 250.)));
            let window = egui::Window::new(presentation::dialog_title(&dialog.title))
                .id(egui::Id::new(("portfolio-dialog", &dialog.key)))
                .open(&mut visible)
                .collapsible(false)
                .default_size(size)
                .min_size(
                    match dialog.key.as_str() {
                        "financials" => egui::vec2(834., 461.),
                        "ledger" => egui::vec2(924., 611.),
                        "setup" | "authority" => egui::vec2(764., 571.),
                        "risk-policy" => egui::vec2(984., 641.),
                        "risk-history" => egui::vec2(834., 511.),
                        "account-version" => egui::vec2(594., 301.),
                        "rule-version" => egui::vec2(594., 441.),
                        _ => egui::vec2(400., 180.),
                    }
                    .min(size),
                )
                .resizable(!dialog.uses_compact_layout() && dialog.key != "ledger")
                .default_pos(ctx.screen_rect().center() - size / 2.);
            let window = if dialog.uses_compact_layout() || dialog.key == "ledger" {
                window.fixed_size(size)
            } else {
                window
            };
            // Modal ownership and area stacking are separate in egui. Keep every
            // child above its ancestors even when an ancestor was raised.
            ctx.move_to_top(egui::LayerId::new(
                egui::Order::Middle,
                egui::Id::new(("portfolio-dialog", &dialog.key)),
            ));
            let response = window.show(ctx, |ui| {
                if index + 1 < document.dialogs.len() {
                    ui.disable();
                }
                self.draw_dialog(ui, dialog, ready, &mut action);
            });
            if let Some(r) = response {
                self.controls
                    .insert(format!("window-{}", dialog.key), r.response.rect);
                if index + 1 == document.dialogs.len() {
                    ctx.memory_mut(|m| m.set_modal_layer(r.response.layer_id));
                }
            }
            if !visible && ready {
                action = Some(self.cancel_action(dialog));
            }
        }
        if ready && !egui::Popup::is_any_open(ctx) {
            if let Some(dialog) = document.dialogs.last() {
                if ctx.input_mut(|i| i.consume_key(egui::Modifiers::NONE, egui::Key::Escape)) {
                    action = Some(self.cancel_action(dialog));
                } else if !self.multiline_focus
                    && matches!(
                        dialog.key.as_str(),
                        "new-portfolio"
                            | "version-portfolio"
                            | "new-fund"
                            | "version-fund"
                            | "allocation"
                            | "envelope"
                            | "assignment"
                            | "portfolio-state"
                            | "fund-state"
                    )
                    && ctx.input_mut(|i| i.consume_key(egui::Modifiers::NONE, egui::Key::Enter))
                {
                    action = Some(("portfolio-save".into(), None, self.dialog_fields(dialog)));
                }
            }
        }
        if !self.error.is_empty() {
            let mut show = true;
            egui::Window::new(presentation::dialog_title("Portfolio Error"))
                .id(egui::Id::new("portfolio-error"))
                .open(&mut show)
                .show(ctx, |ui| {
                    ui.trading_label(&self.error);
                });
            if !show {
                self.error.clear();
            }
        }
        if self.open && ready && !self.loaded {
            self.loaded = true;
            action = Some(Self::action("load"));
        }
        if ready && action.is_none() {
            if let Some(dialog) = document.dialogs.last().or(document.fund_view.as_ref()) {
                if document
                    .context
                    .get("refreshParent")
                    .is_some_and(|key| key == &dialog.key)
                {
                    let mut fields = self.dialog_fields(dialog);
                    fields.insert("action".into(), "Refresh".into());
                    action = Some(("portfolio-financial".into(), None, fields));
                }
            }
        }
        if self.open && ready && self.loaded && self.section == 1 && action.is_none() {
            if let Some(id) = document
                .grids
                .iter()
                .find(|g| g.key == "funds")
                .and_then(|g| g.selected.as_ref())
            {
                let selected = *self.tabs.entry("inline-fund-view".into()).or_default();
                let key = ["financials", "risk-history"][selected];
                let request_key = (id.clone(), key.to_owned());
                if document
                    .fund_view
                    .as_ref()
                    .is_none_or(|view| view.key != key)
                    && self.requested_fund_view.as_ref() != Some(&request_key)
                {
                    self.requested_fund_view = Some(request_key);
                    action = Some((
                        "portfolio-fund-view".into(),
                        Some(id.clone()),
                        BTreeMap::from([("view".into(), key.into())]),
                    ));
                }
            }
        }
        action.map(|a| self.request(a, publication))
    }
    /// Shared action-rail width measured from the visible button captions and normal padding.
    fn portfolio_action_width(ui: &egui::Ui) -> f32 {
        let font = egui::TextStyle::Button.resolve(ui.style());
        let text_width = ["Portfolio Actions", "Risk Policy Actions", "Save", "Cancel"]
            .iter()
            .map(|text| {
                ui.fonts(|fonts| {
                    fonts
                        .layout_no_wrap((*text).into(), font.clone(), presentation::FOREGROUND)
                        .size()
                        .x
                })
            })
            .fold(0.0_f32, f32::max);
        (text_width + 2. * ui.spacing().button_padding.x).ceil()
    }
    /// Allocate the upper Portfolio grid, right action controls, and lower embedded Risk Policy area within fixed bounds.
    fn draw_portfolio_page(
        &mut self,
        ui: &mut egui::Ui,
        document: &Document,
        bounds: egui::Rect,
        height: f32,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let page = egui::Rect::from_min_size(ui.cursor().min, egui::vec2(bounds.width(), height));
        let action_width = Self::portfolio_action_width(ui);
        let grid_height = (bounds.height() / 3.).min((height - 140.).max(80.));
        let grid_rect = egui::Rect::from_min_size(
            page.min,
            egui::vec2((page.width() - action_width - 8.).max(300.), grid_height),
        );
        let editing = document
            .policy
            .as_ref()
            .is_some_and(|d| d.fields.iter().any(|f| f.enabled));
        ui.scope_builder(egui::UiBuilder::new().max_rect(grid_rect), |ui| {
            ui.set_clip_rect(grid_rect.intersect(ui.clip_rect()));
            if let Some(grid) = document.grids.iter().find(|g| g.key == "portfolios") {
                if let Some((id, _)) = draw_grid(
                    ui,
                    grid,
                    grid.selected.as_deref(),
                    grid_height,
                    &mut self.fields,
                    &mut self.controls,
                    ready && !editing,
                ) {
                    *action = Some(("portfolio-select".into(), Some(id), BTreeMap::new()));
                }
            }
        });
        let rail = egui::Rect::from_min_max(
            egui::pos2(grid_rect.right() + 8., page.top()),
            egui::pos2(page.right(), grid_rect.bottom()),
        );
        ui.scope_builder(egui::UiBuilder::new().max_rect(rail), |ui| {
            ui.set_clip_rect(rail.intersect(ui.clip_rect()));
            ui.add_enabled_ui(ready && !editing, |ui| {
                let filter = self
                    .fields
                    .entry("filter".into())
                    .or_insert("Active".into());
                let previous = filter.clone();
                let response = egui::ComboBox::from_id_salt("portfolio-state")
                    .width(rail.width())
                    .selected_text(&*filter)
                    .show_ui(ui, |ui| {
                        for name in [
                            "Draft",
                            "Active",
                            "Paused",
                            "ReduceOnly",
                            "Disabled",
                            "Retired",
                        ] {
                            ui.selectable_value(filter, name.into(), name);
                        }
                    });
                self.controls
                    .insert("portfolio-state".into(), response.response.rect);
                if previous != *filter {
                    *action = Some((
                        "portfolio-refresh".into(),
                        None,
                        BTreeMap::from([("filter".into(), filter.clone())]),
                    ));
                }
                let button =
                    ui.add_sized([rail.width(), 26.], egui::Button::new("Portfolio Actions"));
                self.controls
                    .insert("portfolio-actions".into(), button.rect);
                egui::Popup::menu(&button).show(|ui| {
                    for (label, view) in [
                        ("New Portfolio...", "new-portfolio"),
                        ("New Version...", "version-portfolio"),
                        ("Change State...", "portfolio-state"),
                        ("Delete Draft...", "delete-portfolio"),
                    ] {
                        let response = ui.add_enabled(
                            document.enabled.get(label).copied().unwrap_or(false),
                            egui::Button::new(label),
                        );
                        self.controls
                            .insert(format!("portfolio-menu-{view}"), response.rect);
                        if response.clicked() {
                            *action = Some((
                                "portfolio-open".into(),
                                None,
                                BTreeMap::from([("view".into(), view.into())]),
                            ));
                            ui.close();
                        }
                    }
                });
            });
        });
        let policy_rect =
            egui::Rect::from_min_max(egui::pos2(page.left(), grid_rect.bottom() + 8.), page.max);
        self.controls
            .insert("inline-risk-policy".into(), policy_rect);
        self.draw_policy_tabs(ui, document.policy.as_ref(), policy_rect, ready, action);
        ui.allocate_rect(page, egui::Sense::hover());
    }
    /// Lay out the Funds grid and action rail above the embedded Financials/Risk History tabs.
    fn draw_funds_page(
        &mut self,
        ui: &mut egui::Ui,
        document: &Document,
        bounds: egui::Rect,
        height: f32,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let page = egui::Rect::from_min_size(ui.cursor().min, egui::vec2(bounds.width(), height));
        let grid_height = (bounds.height() / 3.).min((height - 140.).max(80.));
        let labels = [
            ("Create Fund...", "new-fund"),
            ("Change Fund...", "version-fund"),
            ("Change Fund State...", "fund-state"),
        ];
        let font = egui::TextStyle::Button.resolve(ui.style());
        let text_width = labels
            .iter()
            .map(|(label, _)| {
                ui.fonts(|fonts| {
                    fonts
                        .layout_no_wrap((*label).into(), font.clone(), presentation::FOREGROUND)
                        .size()
                        .x
                })
            })
            .fold(0.0_f32, f32::max);
        let action_width = (text_width + 2. * ui.spacing().button_padding.x).ceil();
        let grid_rect = egui::Rect::from_min_size(
            page.min,
            egui::vec2((page.width() - action_width - 8.).max(100.), grid_height),
        );
        ui.scope_builder(egui::UiBuilder::new().max_rect(grid_rect), |ui| {
            ui.set_clip_rect(grid_rect.intersect(ui.clip_rect()));
            if let Some(grid) = document.grids.iter().find(|g| g.key == "funds") {
                if let Some((id, _)) = draw_grid(
                    ui,
                    grid,
                    grid.selected.as_deref(),
                    grid_height,
                    &mut self.fields,
                    &mut self.controls,
                    ready,
                ) {
                    *action = Some(("portfolio-fund".into(), Some(id), BTreeMap::new()));
                }
            }
        });
        let rail = egui::Rect::from_min_max(
            egui::pos2(grid_rect.right() + 8., page.top()),
            egui::pos2(page.right(), grid_rect.bottom()),
        );
        ui.scope_builder(egui::UiBuilder::new().max_rect(rail), |ui| {
            ui.set_clip_rect(rail.intersect(ui.clip_rect()));
            ui.add_enabled_ui(ready, |ui| {
                let filter = self
                    .fields
                    .entry("fundFilter".into())
                    .or_insert("Active".into());
                let previous = filter.clone();
                let response = egui::ComboBox::from_id_salt("fund-state-filter")
                    .width(action_width)
                    .selected_text(&*filter)
                    .show_ui(ui, |ui| {
                        for name in ["Active", "Draft", "Paused", "Disabled", "Retired", "All"] {
                            ui.selectable_value(filter, name.into(), name);
                        }
                    });
                self.controls
                    .insert("fund-state-filter".into(), response.response.rect);
                if previous != *filter {
                    *action = Some((
                        "portfolio-fund-filter".into(),
                        None,
                        BTreeMap::from([("fundFilter".into(), filter.clone())]),
                    ));
                }
                let button = ui.add_sized([action_width, 26.], egui::Button::new("Fund Actions"));
                self.controls.insert("fund-actions".into(), button.rect);
                egui::Popup::menu(&button).show(|ui| {
                    for (label, view) in labels {
                        let response = ui.add_enabled(
                            document.enabled.get(label).copied().unwrap_or(false),
                            egui::Button::new(label).min_size(egui::vec2(action_width, 26.)),
                        );
                        self.controls
                            .insert(format!("fund-menu-{view}"), response.rect);
                        if response.clicked() {
                            *action = Some((
                                "portfolio-open".into(),
                                None,
                                BTreeMap::from([("view".into(), view.into())]),
                            ));
                            ui.close();
                        }
                    }
                });
            });
        });
        let panel =
            egui::Rect::from_min_max(egui::pos2(page.left(), grid_rect.bottom() + 8.), page.max);
        self.controls.insert("fund-tabs".into(), panel);
        let mut selected = *self.tabs.entry("inline-fund-view".into()).or_default();
        ui.scope_builder(egui::UiBuilder::new().max_rect(panel), |ui| {
            ui.set_clip_rect(panel.intersect(ui.clip_rect()));
            ui.add_enabled_ui(ready, |ui| {
                ui.horizontal(|ui| {
                    for (index, label) in ["Financials", "Risk History"].iter().enumerate() {
                        let response = tab(ui, selected == index, label);
                        self.controls
                            .insert(format!("fund-tab-{index}"), response.rect);
                        if response.clicked() {
                            selected = index;
                        }
                    }
                });
            });
            let key = ["financials", "risk-history"][selected];
            if let Some(view) = document.fund_view.as_ref().filter(|d| d.key == key) {
                let content = egui::Rect::from_min_max(ui.cursor().min, panel.max);
                ui.scope_builder(egui::UiBuilder::new().max_rect(content), |ui| {
                    ui.set_clip_rect(content.intersect(ui.clip_rect()));
                    self.draw_dialog(ui, view, ready, action);
                });
            } else {
                ui.trading_label(
                    if document
                        .grids
                        .iter()
                        .find(|g| g.key == "funds")
                        .and_then(|g| g.selected.as_ref())
                        .is_some()
                    {
                        "Loading Fund view..."
                    } else {
                        "Select a Fund."
                    },
                );
            }
            border(ui, panel);
        });
        self.tabs.insert("inline-fund-view".into(), selected);
        ui.allocate_rect(page, egui::Sense::hover());
    }
    /// Render Risk Policy/Versions pages and the right action rail, with stacked Save/Cancel. Editing actions return to the policy form.
    fn draw_policy_tabs(
        &mut self,
        ui: &mut egui::Ui,
        policy: Option<&Dialog>,
        bounds: egui::Rect,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let panel = egui::Rect::from_min_max(
            bounds.min,
            egui::pos2(
                bounds.right() - Self::portfolio_action_width(ui) - 8.,
                bounds.bottom(),
            ),
        );
        let rail =
            egui::Rect::from_min_max(egui::pos2(panel.right() + 8., panel.top()), bounds.max);
        self.controls.insert("risk-policy-tabs".into(), panel);
        let mut selected = *self.tabs.entry("inline-policy-view".into()).or_default();
        ui.scope_builder(egui::UiBuilder::new().max_rect(rail), |ui| {
            ui.set_clip_rect(rail.intersect(ui.clip_rect()));
            ui.add_enabled_ui(ready && policy.is_some(), |ui| {
                let button = ui.add_sized(
                    [rail.width(), 26.],
                    egui::Button::new("Risk Policy Actions"),
                );
                self.controls
                    .insert("risk-policy-actions".into(), button.rect);
                egui::Popup::menu(&button).show(|ui| {
                    if let Some(d) = policy {
                        for label in [
                            "New Policy",
                            "Change Policy...",
                            "New Version",
                            "Activate && Assign",
                            "Retire",
                            "Delete Draft",
                        ] {
                            let response = ui.add_enabled(
                                d.button_enabled
                                    .as_ref()
                                    .and_then(|b| b.get(label))
                                    .copied()
                                    .unwrap_or(false),
                                egui::Button::new(if label == "New Policy" {
                                    "New Policy...".into()
                                } else {
                                    label.replace("&&", "&")
                                }),
                            );
                            self.controls
                                .insert(format!("risk-policy-menu-{label}"), response.rect);
                            if response.clicked() {
                                let mut fields = self.dialog_fields(d);
                                fields.insert("action".into(), label.into());
                                *action = Some(("portfolio-policy".into(), None, fields));
                                if matches!(
                                    label,
                                    "New Policy" | "Change Policy..." | "New Version"
                                ) {
                                    selected = 0;
                                }
                                ui.close();
                            }
                        }
                    }
                });
            });
            if let Some(d) = policy {
                self.dialog_buttons(ui, d, ready, action);
            }
        });
        ui.scope_builder(egui::UiBuilder::new().max_rect(panel), |ui| {
            ui.set_clip_rect(panel.intersect(ui.clip_rect()));
            ui.horizontal(|ui| {
                for (index, label) in ["Risk Policy", "Versions"].iter().enumerate() {
                    if tab(ui, selected == index, label).clicked() {
                        selected = index;
                    }
                }
            });
            if let Some(d) = policy {
                if selected == 1 {
                    if let Some(grid) = d.grids.as_ref().and_then(|g| g.first()) {
                        let editing = d.fields.iter().any(|f| f.enabled);
                        self.dialog_grid(
                            ui,
                            d,
                            grid,
                            ui.available_height(),
                            ready && !editing,
                            action,
                        );
                    }
                } else {
                    let form = egui::Rect::from_min_max(ui.cursor().min, panel.max);
                    ui.scope_builder(egui::UiBuilder::new().max_rect(form), |ui| {
                        ui.set_clip_rect(form.intersect(ui.clip_rect()));
                        egui::ScrollArea::vertical()
                            .id_salt("inline-risk-policy-scroll")
                            .auto_shrink([false, false])
                            .show(ui, |ui| {
                                let rect = egui::Rect::from_min_size(
                                    ui.cursor().min,
                                    egui::vec2(ui.available_width(), form.height().max(380.)),
                                );
                                let mut child = ui.new_child(egui::UiBuilder::new().max_rect(rect));
                                ui.allocate_rect(rect, egui::Sense::hover());
                                self.policy_form(&mut child, d, ready, action, true);
                            });
                    });
                }
            }
            border(ui, panel);
        });
        self.tabs.insert("inline-policy-view".into(), selected);
    }
    /// Draw the managed policy fields and strategy-family limits without calculating risk values locally.
    fn policy_form(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        ready: bool,
        action: &mut Option<Action>,
        embedded: bool,
    ) {
        let right = ui.available_rect_before_wrap();
        self.policy_value_width = ((right.width() - 2. * d.caption_width - 24.) / 2.).max(80.);
        egui::Grid::new("portfolio-policy-fields")
            .num_columns(4)
            .min_row_height(44.)
            .show(ui, |ui| {
                for (i, f) in d.fields.iter().take(10).enumerate() {
                    if i == 9 {
                        ui.add(presentation::caption_widget("Open Positions / Drawdown"));
                        let original_width = self.policy_value_width;
                        self.policy_value_width =
                            ((original_width - ui.spacing().item_spacing.x) / 2.).max(40.);
                        ui.horizontal(|ui| {
                            for field in &d.fields[9..] {
                                let mut field = field.clone();
                                field.caption.clear();
                                self.field(ui, d, &field, ready);
                            }
                        });
                        self.policy_value_width = original_width;
                        ui.end_row();
                        continue;
                    }
                    self.field(ui, d, f, ready);
                    if i % 2 == 1 {
                        ui.end_row();
                    }
                }
            });
        if let Some(g) = d.grids.as_ref().and_then(|g| g.get(1)) {
            let families = egui::Rect::from_min_max(
                egui::pos2(
                    right.left(),
                    if embedded {
                        ui.cursor().top() + 8.
                    } else {
                        right.top() + 300.
                    },
                ),
                right.max,
            );
            ui.scope_builder(egui::UiBuilder::new().max_rect(families), |ui| {
                ui.set_clip_rect(families.intersect(ui.clip_rect()));
                self.dialog_grid(ui, d, g, families.height().max(40.), ready, action);
            });
        }
    }
    /// Collect the current dialog's local fields and strip its namespace prefix for the managed command payload.
    fn dialog_fields(&self, d: &Dialog) -> BTreeMap<String, String> {
        let prefix = format!("{}.", d.key);
        self.fields
            .iter()
            .filter_map(|(k, v)| k.strip_prefix(&prefix).map(|key| (key.into(), v.clone())))
            .collect()
    }
    /// Compare editable local policy/limit values against the published originals to determine whether Save is meaningful.
    fn policy_dirty(&self, d: &Dialog) -> bool {
        self.document
            .context
            .get("policyDirty")
            .is_some_and(|v| v.eq_ignore_ascii_case("true"))
            || d.fields.iter().any(|f| {
                self.fields
                    .get(&format!("{}.{}", d.key, f.key))
                    .is_some_and(|v| v != &f.value)
            })
            || d.grids
                .as_deref()
                .unwrap_or_default()
                .iter()
                .filter(|g| g.key == "family-limits")
                .any(|g| {
                    g.rows.iter().any(|r| {
                        r.fields.iter().any(|(column, value)| {
                            self.fields
                                .get(&format!("risk-policy.limit.{}.{}", r.id, column))
                                .is_some_and(|v| v != value)
                        })
                    })
                })
    }
    /// Build the appropriate close/cancel intent, including dirty-policy information required for discard handling.
    fn cancel_action(&self, d: &Dialog) -> Action {
        (
            "portfolio-cancel".into(),
            None,
            BTreeMap::from([(
                "dirty".into(),
                (d.key == "risk-policy" && self.policy_dirty(d)).to_string(),
            )]),
        )
    }
    /// Render a managed dialog definition within its reference dimensions, dispatching specialized policy/financial/editor layouts.
    fn draw_dialog(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let bounds = ui.available_rect_before_wrap();
        self.controls.insert(format!("dialog-{}", d.key), bounds);
        let toolbar = d.key == "risk-policy";
        if toolbar {
            self.dialog_buttons(ui, d, ready, action);
        }
        if d.key == "risk-policy" {
            let embedded = self.document.policy.is_some();
            if !d.status.is_empty() {
                ui.trading_label(&d.status);
            }
            let height = (ui.available_height() - if embedded { 0. } else { 25. }).max(50.);
            let left = egui::Rect::from_min_size(
                egui::pos2(bounds.left(), ui.cursor().top()),
                egui::vec2(390., height),
            );
            let right = egui::Rect::from_min_max(
                egui::pos2(left.right() + 12., left.top()),
                egui::pos2(bounds.right(), left.bottom()),
            );
            ui.scope_builder(egui::UiBuilder::new().max_rect(left), |ui| {
                ui.set_clip_rect(left.intersect(ui.clip_rect()));
                if let Some(g) = d.grids.as_ref().and_then(|g| g.first()) {
                    self.dialog_grid(ui, d, g, height, ready, action);
                }
            });
            ui.scope_builder(egui::UiBuilder::new().max_rect(right), |ui| {
                ui.set_clip_rect(right.intersect(ui.clip_rect()));
                self.policy_form(ui, d, ready, action, embedded);
            });
        } else if d.key == "ledger" {
            ui.trading_label(&d.status);
            let content = egui::Rect::from_min_max(ui.cursor().min, bounds.max);
            let grid_height = content.height() * (2. / 3.);
            let grid =
                egui::Rect::from_min_size(content.min, egui::vec2(content.width(), grid_height));
            self.controls.insert("ledger.grid-area".into(), grid);
            ui.scope_builder(egui::UiBuilder::new().max_rect(grid), |ui| {
                ui.set_clip_rect(grid.intersect(ui.clip_rect()));
                self.dialog_tabs(ui, d, grid_height, ready, action);
            });
            let gap = 16.;
            let fields_top = grid.bottom() + 8.;
            let left_width = 330.;
            let action_width = 140.;
            let right_left = content.left() + left_width + gap;
            let right_right = content.right() - action_width - gap;
            let actions_rect = egui::Rect::from_min_size(
                egui::pos2(content.right() - action_width, fields_top),
                egui::vec2(action_width, 28.),
            );
            ui.scope_builder(egui::UiBuilder::new().max_rect(actions_rect), |ui| {
                let mut actions = d.clone();
                actions
                    .buttons
                    .retain(|label| !matches!(label.as_str(), "Refresh" | "Close"));
                self.dialog_buttons(ui, &actions, ready, action);
            });
            for group in 0..2 {
                let (left, right) = if group == 0 {
                    (content.left(), content.left() + left_width)
                } else {
                    (right_left, right_right)
                };
                let rect = egui::Rect::from_min_max(
                    egui::pos2(left, fields_top),
                    egui::pos2(right, content.bottom()),
                );
                ui.scope_builder(egui::UiBuilder::new().max_rect(rect), |ui| {
                    ui.set_clip_rect(rect.intersect(ui.clip_rect()));
                    egui::Grid::new(("ledger-bottom-fields", group))
                        .num_columns(2)
                        .min_row_height(24.)
                        .show(ui, |ui| {
                            let range = if group == 0 {
                                0..4.min(d.fields.len())
                            } else {
                                4.min(d.fields.len())..d.fields.len()
                            };
                            for field in &d.fields[range] {
                                if self.field(ui, d, field, ready) {
                                    *action = Some((
                                        "portfolio-choice".into(),
                                        None,
                                        self.dialog_fields(d),
                                    ));
                                }
                                ui.end_row();
                            }
                        });
                });
            }
        } else if d.key == "financials" {
            self.financials(ui, d, ready, action);
        } else if d.key == "risk-history" {
            if let Some(f) = d.fields.first() {
                ui.horizontal(|ui| {
                    let caption = ui.add(presentation::caption_widget("Evaluation Date:"));
                    self.controls
                        .insert(format!("{}.{}.caption", d.key, f.key), caption.rect);
                    let mut date = f.clone();
                    date.caption.clear();
                    if self.field(ui, d, &date, ready) {
                        *action = Some(("portfolio-choice".into(), None, self.dialog_fields(d)));
                    }
                });
            }
            if let Some(g) = d.grids.as_ref().and_then(|g| g.first()) {
                self.dialog_grid(ui, d, g, ui.available_height() * 0.35, ready, action);
            }
            let selected = self.tabs.entry(d.key.clone()).or_default();
            ui.horizontal(|ui| {
                for (i, title) in ["Decision and sizing", "Evidence"].iter().enumerate() {
                    if tab(ui, *selected == i, *title).clicked() {
                        *selected = i;
                    }
                }
            });
            if let Some(f) = d.fields.get(1 + *selected) {
                let value = self
                    .fields
                    .entry(format!("{}.{}", d.key, f.key))
                    .or_insert(f.value.clone());
                ui.add_sized(
                    [ui.available_width(), (ui.available_height() - 35.).max(40.)],
                    egui::TextEdit::multiline(value)
                        .interactive(false)
                        .font(egui::TextStyle::Monospace),
                );
            }
            ui.trading_label(&d.status);
        } else if d.uses_compact_layout() {
            self.compact_editor_form(ui, d, ready, action);
        } else {
            let button_height = 54.;
            let status_height = 48.;
            let content = egui::Rect::from_min_max(
                bounds.min,
                egui::pos2(
                    bounds.right(),
                    bounds.bottom() - button_height - status_height,
                ),
            );
            ui.scope_builder(egui::UiBuilder::new().max_rect(content), |ui| {
                egui::ScrollArea::vertical()
                    .id_salt(("portfolio-editor-fields", &d.key))
                    .max_height(content.height())
                    .auto_shrink([false, false])
                    .show(ui, |ui| {
                        self.fields(ui, d, ready, action);
                    });
            });
            let status = egui::Rect::from_min_size(
                egui::pos2(
                    bounds.left(),
                    bounds.bottom() - button_height - status_height,
                ),
                egui::vec2(bounds.width(), status_height),
            );
            ui.put(status, presentation::label_widget(&d.status));
            let buttons = egui::Rect::from_min_size(
                egui::pos2(bounds.left(), bounds.bottom() - button_height),
                egui::vec2(bounds.width(), button_height),
            );
            ui.scope_builder(egui::UiBuilder::new().max_rect(buttons), |ui| {
                self.dialog_buttons(ui, d, ready, action);
            });
        }
        if !d.uses_compact_layout() && d.key != "ledger" {
            border(ui, bounds);
        }
        ui.allocate_rect(bounds, egui::Sense::hover());
    }
    /// Render a compact creation form without an inner frame or resizable content.
    fn compact_editor_form(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let bounds = ui.available_rect_before_wrap();
        let buttons_top = bounds.bottom() - 34.;
        let status = egui::Rect::from_min_max(
            egui::pos2(bounds.left(), buttons_top - 28.),
            egui::pos2(bounds.right(), buttons_top - 4.),
        );
        let actions_follow_fields = d.actions_follow_fields();
        let form_bottom = if actions_follow_fields {
            bounds.bottom() - 54. - if d.has_inline_status() { 28. } else { 0. }
        } else {
            status.top() - 4.
        };
        let form = egui::Rect::from_min_max(bounds.min, egui::pos2(bounds.right(), form_bottom));
        ui.scope_builder(egui::UiBuilder::new().max_rect(form), |ui| {
            ui.set_clip_rect(form.intersect(ui.clip_rect()));
            self.fields(ui, d, ready, action);
        });
        let buttons_top = if actions_follow_fields {
            d.fields
                .iter()
                .filter_map(|field| self.controls.get(&format!("{}.{}", d.key, field.key)))
                .map(egui::Rect::bottom)
                .fold(bounds.top(), f32::max)
                + 20.
        } else {
            buttons_top
        };
        let status = if actions_follow_fields {
            egui::Rect::from_min_size(
                egui::pos2(bounds.left(), buttons_top + 30.),
                egui::vec2(bounds.width(), 24.),
            )
        } else {
            status
        };
        if d.has_inline_status() {
            ui.put(status, presentation::label_widget(&d.status).truncate());
        }
        let font = egui::TextStyle::Button.resolve(ui.style());
        let button_width = d
            .buttons
            .iter()
            .map(|label| {
                ui.fonts(|fonts| {
                    fonts
                        .layout_no_wrap(label.clone(), font.clone(), presentation::FOREGROUND)
                        .size()
                        .x
                }) + 2. * ui.spacing().button_padding.x
            })
            .fold(90.0_f32, f32::max)
            .ceil();
        let gap = 8.;
        let total_width =
            d.buttons.len() as f32 * button_width + d.buttons.len().saturating_sub(1) as f32 * gap;
        let left = bounds.center().x - total_width / 2.;
        for (index, label) in d.buttons.iter().enumerate() {
            let rect = egui::Rect::from_min_size(
                egui::pos2(left + index as f32 * (button_width + gap), buttons_top),
                egui::vec2(button_width, 26.),
            );
            let enabled = ready
                && d.button_enabled
                    .as_ref()
                    .and_then(|buttons| buttons.get(label))
                    .copied()
                    .unwrap_or(true);
            let response = ui
                .add_enabled_ui(enabled, |ui| {
                    ui.put(rect, egui::Button::new(label.as_str()))
                })
                .inner;
            self.controls
                .insert(format!("{}.button.{label}", d.key), response.rect);
            if response.clicked() {
                *action = Some(if matches!(label.as_str(), "Cancel" | "Close") {
                    self.cancel_action(d)
                } else if d.key == "posting" {
                    let mut fields = self.dialog_fields(d);
                    fields.insert("action".into(), label.clone());
                    ("portfolio-financial".into(), None, fields)
                } else {
                    ("portfolio-save".into(), None, self.dialog_fields(d))
                });
            }
        }
    }
    /// Render cash captions, financial grid tabs, and their right-hand action menu.
    fn financials(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        ui.horizontal_wrapped(|ui| {
            for field in &d.fields {
                if !field.caption.is_empty() {
                    let response = ui.add(presentation::caption_widget(&field.caption));
                    self.controls
                        .insert(format!("financials.caption.{}", field.key), response.rect);
                }
                let response = ui.add(presentation::label_widget(&field.value));
                self.controls
                    .insert(format!("financials.value.{}", field.key), response.rect);
                ui.add_space(12.);
            }
        });
        let mut body = ui.available_rect_before_wrap();
        if !d.status.is_empty() {
            let status_height = ui.text_style_height(&egui::TextStyle::Body);
            let status = egui::Rect::from_min_max(
                egui::pos2(body.left(), body.bottom() - status_height),
                body.max,
            );
            ui.put(status, presentation::label_widget(&d.status).truncate());
            body.max.y = status.top() - ui.spacing().item_spacing.y;
        }
        let labels = [
            ("Next Transactions", "Next transactions"),
            ("Next Reservations", "Next reservations"),
            ("Transaction...", "Transaction"),
            ("Ledger...", "Ledger"),
        ];
        let font = egui::TextStyle::Button.resolve(ui.style());
        let width = labels
            .iter()
            .map(|(label, _)| *label)
            .chain(["Financial Actions"])
            .map(|label| {
                ui.fonts(|fonts| {
                    fonts
                        .layout_no_wrap(label.into(), font.clone(), presentation::FOREGROUND)
                        .size()
                        .x
                })
            })
            .fold(0.0_f32, f32::max)
            + 2. * ui.spacing().button_padding.x;
        let panel = egui::Rect::from_min_max(
            body.min,
            egui::pos2(body.right() - width.ceil() - 8., body.bottom()),
        );
        self.controls.insert("financial-tabs".into(), panel);
        ui.scope_builder(egui::UiBuilder::new().max_rect(panel), |ui| {
            ui.set_clip_rect(panel.intersect(ui.clip_rect()));
            self.dialog_tabs(ui, d, panel.height(), ready, action);
        });
        // Use the rendered grid bounds so wrapped tab headers and DPI scaling
        // keep the action button aligned with the grid, rather than its tabs.
        let grid_top = d
            .grids
            .as_deref()
            .and_then(|grids| grids.get(*self.tabs.get(&d.key).unwrap_or(&0)))
            .and_then(|grid| self.controls.get(&format!("grid-{}", grid.key)))
            .map_or(panel.top(), |grid| grid.top());
        let rail = egui::Rect::from_min_max(egui::pos2(panel.right() + 8., grid_top), body.max);
        ui.scope_builder(egui::UiBuilder::new().max_rect(rail), |ui| {
            ui.set_clip_rect(rail.intersect(ui.clip_rect()));
            ui.add_enabled_ui(ready, |ui| {
                let button =
                    ui.add_sized([rail.width(), 26.], egui::Button::new("Financial Actions"));
                self.controls
                    .insert("financial-actions".into(), button.rect);
                egui::Popup::menu(&button).show(|ui| {
                    for (label, name) in labels {
                        let response = ui.add_enabled(
                            d.button_enabled
                                .as_ref()
                                .and_then(|values| values.get(name))
                                .copied()
                                .unwrap_or(false),
                            egui::Button::new(label),
                        );
                        self.controls
                            .insert(format!("financials.button.{name}"), response.rect);
                        if response.clicked() {
                            let mut fields = self.dialog_fields(d);
                            fields.insert("action".into(), name.into());
                            *action = Some(("portfolio-financial".into(), None, fields));
                            ui.close();
                        }
                    }
                });
            });
            if self
                .document
                .fund_view
                .as_ref()
                .is_none_or(|view| view.key != d.key)
                && Self::button(
                    ui,
                    &mut self.controls,
                    "financials.button.Close",
                    "Close",
                    ready,
                )
            {
                *action = Some(self.cancel_action(d));
            }
        });
    }
    /// Render a dialog's managed field definitions and collect any resulting action.
    fn fields(&mut self, ui: &mut egui::Ui, d: &Dialog, ready: bool, action: &mut Option<Action>) {
        for field in d
            .fields
            .iter()
            .filter(|f| f.kind == "label" && f.caption.is_empty())
        {
            self.field(ui, d, field, ready);
        }
        egui::Grid::new(("portfolio-fields", &d.key))
            .num_columns(2)
            .spacing(egui::vec2(
                if d.uses_compact_layout() {
                    20.
                } else {
                    ui.spacing().item_spacing.x
                },
                ui.spacing().item_spacing.y,
            ))
            .min_row_height(if d.uses_compact_layout() {
                24.
            } else if d.key == "version-fund" {
                46.
            } else {
                38.
            })
            .show(ui, |ui| {
                for f in &d.fields {
                    if f.kind == "label" && f.caption.is_empty() {
                        continue;
                    }
                    if self.field(ui, d, f, ready) {
                        *action = Some(("portfolio-choice".into(), None, self.dialog_fields(d)));
                    }
                    ui.end_row();
                }
            });
    }
    /// Render one typed field with published choices/bounds/eligibility; return whether a selection action should be sent.
    fn field(&mut self, ui: &mut egui::Ui, d: &Dialog, f: &Field, ready: bool) -> bool {
        let prefix = format!("{}.{}", d.key, f.key);
        let value = self.fields.entry(prefix.clone()).or_insert(f.value.clone());
        let caption_id = if !f.caption.is_empty() {
            let caption = if d.uses_compact_layout() {
                format!("{}:", f.caption.trim_end_matches(':'))
            } else {
                f.caption.clone()
            };
            let response = if d.uses_compact_layout() {
                ui.allocate_ui_with_layout(
                    egui::vec2(d.caption_width, 24.),
                    egui::Layout::right_to_left(egui::Align::Center),
                    |ui| {
                        ui.set_min_width(d.caption_width);
                        ui.add(presentation::caption_widget(&caption))
                    },
                )
                .inner
            } else {
                ui.add_sized(
                    [d.caption_width, 24.],
                    presentation::caption_widget(&caption).halign(egui::Align::Max),
                )
            };
            self.controls
                .insert(format!("{prefix}.caption"), response.rect);
            Some(response.id)
        } else {
            None
        };
        let mut selection_changed = false;
        ui.add_enabled_ui(ready && f.enabled, |ui| {
            let width = if d.key == "ledger" && f.key == "reason" {
                (ui.available_width() - 4.).max(60.)
            } else if d.key == "risk-policy" {
                self.policy_value_width
            } else {
                let pane_width = if d.key == "ledger" {
                    330.
                } else {
                    self.controls
                        .get(&format!("dialog-{}", d.key))
                        .map(|r| r.width())
                        .unwrap_or(d.width - 16.)
                };
                let limit = if f.caption.is_empty() {
                    pane_width - 24.
                } else {
                    pane_width - d.caption_width - 32.
                };
                ui.available_width().min(limit).max(60.)
            };
            let response = match f.kind.as_str() {
                "choice" => {
                    egui::ComboBox::from_id_salt(&prefix)
                        .width(width)
                        .selected_text(
                            f.choices
                                .as_ref()
                                .and_then(|c| c.iter().find(|c| c.value == *value))
                                .map(|c| c.label.as_str())
                                .unwrap_or(value),
                        )
                        .show_ui(ui, |ui| {
                            for c in f.choices.as_deref().unwrap_or_default() {
                                if ui
                                    .add_enabled(
                                        c.enabled,
                                        egui::Button::selectable(c.value == *value, &c.label),
                                    )
                                    .clicked()
                                {
                                    *value = c.value.clone();
                                    selection_changed = true;
                                    ui.close();
                                }
                            }
                        })
                        .response
                }
                "multi" | "checklist" => {
                    let filter = self.filters.entry(prefix.clone()).or_default();
                    let mut selected = value
                        .split(',')
                        .filter(|s| !s.is_empty())
                        .map(str::to_owned)
                        .collect::<Vec<_>>();
                    let selected_text = selected
                        .iter()
                        .filter_map(|v| {
                            f.choices
                                .as_ref()?
                                .iter()
                                .find(|c| &c.value == v)
                                .map(|c| c.label.clone())
                        })
                        .collect::<Vec<_>>()
                        .join(", ");
                    let mut draw = |ui: &mut egui::Ui| {
                        if f.kind == "multi" {
                            ui.add(
                                egui::TextEdit::singleline(filter)
                                    .hint_text("Search values")
                                    .desired_width(width),
                            );
                        }
                        let query = filter.to_lowercase();
                        for c in f.choices.as_deref().unwrap_or_default() {
                            if f.kind == "multi"
                                && !c.value.to_lowercase().contains(&query)
                                && !c.label.to_lowercase().contains(&query)
                            {
                                continue;
                            }
                            let mut checked = selected.contains(&c.value);
                            let response = ui.add_enabled(
                                c.enabled || checked,
                                crate::presentation::checkbox(&mut checked, &c.label),
                            );
                            self.controls
                                .insert(format!("{prefix}.choice.{}", c.value), response.rect);
                            if response.changed() {
                                if checked {
                                    selected.push(c.value.clone());
                                } else {
                                    selected.retain(|s| s != &c.value);
                                }
                            }
                        }
                    };
                    let response = if f.kind == "multi" {
                        egui::ComboBox::from_id_salt(&prefix)
                            .width(width)
                            .selected_text(selected_text)
                            .show_ui(ui, draw)
                            .response
                    } else if d.uses_compact_layout() {
                        egui::Frame::new()
                            .stroke(egui::Stroke::new(1.0_f32, presentation::BORDER))
                            .inner_margin(2.)
                            .show(ui, |ui| {
                                ui.set_width(width);
                                egui::ScrollArea::vertical()
                                    .id_salt(&prefix)
                                    .max_height(180.)
                                    .min_scrolled_height(180.)
                                    .auto_shrink([false, false])
                                    .show(ui, |ui| {
                                        ui.set_clip_rect(ui.clip_rect().shrink(1.));
                                        ui.vertical(|ui| draw(ui));
                                    });
                            })
                            .response
                    } else {
                        egui::Frame::new()
                            .stroke(egui::Stroke::new(1.0_f32, presentation::BORDER))
                            .show(ui, |ui| {
                                ui.set_min_width(width);
                                ui.set_min_height(120.);
                                egui::ScrollArea::both()
                                    .id_salt(&prefix)
                                    .max_height(120.)
                                    .show(ui, |ui| draw(ui));
                            })
                            .response
                    };
                    *value = f
                        .choices
                        .as_deref()
                        .unwrap_or_default()
                        .iter()
                        .filter(|c| selected.contains(&c.value))
                        .map(|c| c.value.clone())
                        .collect::<Vec<_>>()
                        .join(",");
                    response
                }
                "check" => {
                    let mut checked = value.eq_ignore_ascii_case("true");
                    let r = ui.add(crate::presentation::checkbox(
                        &mut checked,
                        if d.uses_compact_layout() {
                            ""
                        } else {
                            &f.caption
                        },
                    ));
                    if r.changed() {
                        *value = checked.to_string();
                        selection_changed = true;
                    }
                    r
                }
                "number" => {
                    let mut number = value.parse::<f64>().unwrap_or(0.);
                    let r = ui.add_sized(
                        [width, 24.],
                        egui::DragValue::new(&mut number)
                            .range(f.minimum..=f.maximum)
                            .min_decimals(f.decimals)
                            .max_decimals(f.decimals),
                    );
                    if r.changed() {
                        *value = format!("{number:.precision$}", precision = f.decimals);
                    }
                    r
                }
                "date" => {
                    let old = value.clone();
                    let response = date_field(ui, value);
                    selection_changed = old != *value;
                    response
                }
                "label" => ui.add_sized([width, 40.], presentation::label_widget(&*value).wrap()),
                "review" => ui.add_sized(
                    [width, 230.],
                    egui::TextEdit::multiline(value)
                        .interactive(false)
                        .font(egui::TextStyle::Monospace),
                ),
                "multiline" => ui.add_sized(
                    [width, if d.key == "ledger" { 40. } else { 64. }],
                    egui::TextEdit::multiline(value).desired_rows(if d.key == "ledger" {
                        2
                    } else {
                        4
                    }),
                ),
                _ => ui.add_sized([width, 24.], egui::TextEdit::singleline(value)),
            };
            if f.kind == "multiline" && response.has_focus() {
                self.multiline_focus = true;
            }
            let response = if let Some(id) = caption_id {
                response.labelled_by(id)
            } else {
                response
            };
            self.controls.insert(prefix, response.rect);
        });
        selection_changed
            && matches!(
                f.key.as_str(),
                "deployment"
                    | "accountingDate"
                    | "rule"
                    | "control"
                    | "permit"
                    | "account"
                    | "start"
                    | "end"
                    | "date"
            )
    }
    /// Render eligible dialog actions and capture the selected intent. Embedded policy Save/Cancel use the action rail's width.
    fn dialog_buttons(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let embedded_policy = d.key == "risk-policy" && self.document.policy.is_some();
        let footer = !matches!(
            d.key.as_str(),
            "financials" | "risk-policy" | "risk-history" | "ledger"
        );
        let mut buttons = if footer {
            d.buttons.iter().rev().collect::<Vec<_>>()
        } else {
            d.buttons.iter().collect::<Vec<_>>()
        };
        if self
            .document
            .fund_view
            .as_ref()
            .is_some_and(|view| view.key == d.key)
        {
            buttons.retain(|label| label.as_str() != "Close");
        }
        if embedded_policy {
            buttons.retain(|label| matches!(label.as_str(), "Save" | "Cancel"));
        }
        ui.horizontal(|ui| {
            ui.with_layout(
                if embedded_policy {
                    egui::Layout::top_down(egui::Align::Min)
                } else if footer || d.key == "ledger" {
                    egui::Layout::right_to_left(egui::Align::Center)
                } else {
                    egui::Layout::left_to_right(egui::Align::Center)
                },
                |ui| {
                    for label in buttons {
                        let enabled = ready
                            && (d.key != "delete-portfolio"
                                || label != "Delete Draft"
                                || self.fields.get("delete-portfolio.confirmation")
                                    == self
                                        .document
                                        .grids
                                        .iter()
                                        .find(|g| g.key == "portfolios")
                                        .and_then(|g| g.selected.as_ref())
                                    && self
                                        .fields
                                        .get("delete-portfolio.reason")
                                        .is_some_and(|v| !v.trim().is_empty()))
                            && (d.key != "risk-policy" || label != "Save" || self.policy_dirty(d))
                            && !matches!(label.as_str(), "Saved" | "Expired")
                            && d.button_enabled
                                .as_ref()
                                .and_then(|e| e.get(label))
                                .copied()
                                .unwrap_or(true);
                        let clicked = if d.key == "ledger" {
                            let response = ui.add_enabled(
                                enabled,
                                egui::Button::new(label.as_str()).min_size(egui::vec2(140., 28.)),
                            );
                            self.controls
                                .insert(format!("{}.button.{label}", d.key), response.rect);
                            response.clicked()
                        } else if embedded_policy {
                            let width = self.controls["risk-policy-actions"].width();
                            let response = ui.add_enabled(
                                enabled,
                                egui::Button::new((
                                    egui::Atom::grow(),
                                    label.as_str(),
                                    egui::Atom::grow(),
                                ))
                                .min_size(egui::vec2(width, ui.spacing().interact_size.y)),
                            );
                            self.controls
                                .insert(format!("{}.button.{label}", d.key), response.rect);
                            response.clicked()
                        } else {
                            Self::button(
                                ui,
                                &mut self.controls,
                                &format!("{}.button.{label}", d.key),
                                &label.replace("&&", "&"),
                                enabled,
                            )
                        };
                        if clicked {
                            let mut fields = self.dialog_fields(d);
                            fields.insert("action".into(), label.clone());
                            let intent =
                                if matches!(d.key.as_str(), "risk-policy" | "discard-policy") {
                                    "portfolio-policy"
                                } else if matches!(label.as_str(), "Cancel" | "Close") {
                                    "portfolio-cancel"
                                } else if matches!(
                                    d.key.as_str(),
                                    "financials"
                                        | "ledger"
                                        | "posting"
                                        | "setup"
                                        | "authority"
                                        | "account-version"
                                        | "rule-version"
                                        | "risk-history"
                                ) {
                                    "portfolio-financial"
                                } else {
                                    "portfolio-save"
                                };
                            *action = Some((intent.into(), None, fields));
                        }
                    }
                },
            );
        });
    }
    /// Render a dialog's grid tabs and the selected grid in the remaining body height.
    fn dialog_tabs(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        height: f32,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let grids = d.grids.as_deref().unwrap_or_default();
        let selected = self.tabs.entry(d.key.clone()).or_default();
        ui.horizontal_wrapped(|ui| {
            for (i, g) in grids.iter().enumerate() {
                let title = match g.key.as_str() {
                    "pending-postings" => "Pending operations",
                    "pending-controls" => "Pending",
                    "balances" => "Balances",
                    "transactions" => "Transactions",
                    "reservations" => "Reservations",
                    "journal" => "Journal",
                    "periods" => "Periods",
                    "accounts" => "Accounts",
                    "rules" => "Rules",
                    "reconciliation" => "Reconciliation",
                    s => s,
                };
                if tab(ui, *selected == i, title).clicked() {
                    *selected = i;
                }
            }
        });
        if let Some(g) = grids.get(*selected) {
            self.dialog_grid(ui, d, g, (height - 30.).max(40.), ready, action);
        }
    }
    /// Draw a managed grid and dispatch selection/double-click actions for the grid kinds that require them.
    fn dialog_grid(
        &mut self,
        ui: &mut egui::Ui,
        d: &Dialog,
        g: &Grid,
        height: f32,
        ready: bool,
        action: &mut Option<Action>,
    ) {
        let selected = g
            .selected
            .as_ref()
            .or_else(|| {
                self.selections
                    .get(&g.key)
                    .filter(|id| g.rows.iter().any(|r| &r.id == *id))
            })
            .or_else(|| g.rows.first().map(|r| &r.id));
        if let Some((id, double)) = draw_grid(
            ui,
            g,
            selected.map(String::as_str),
            height,
            &mut self.fields,
            &mut self.controls,
            ready,
        ) {
            self.selections.insert(g.key.clone(), id.clone());
            let activates = match g.key.as_str() {
                "risk-history" | "pending-postings" | "pending-controls" => double,
                "transactions" | "policies" => true,
                _ => false,
            };
            if activates {
                let mut fields = self.dialog_fields(d);
                fields.insert("grid".into(), g.key.clone());
                *action = Some(("portfolio-grid".into(), Some(id), fields));
            }
        }
    }
}
/// Draw a selected/unselected tab using the shared native tab-header conventions.
fn tab(ui: &mut egui::Ui, selected: bool, title: &str) -> egui::Response {
    presentation::tab_label(ui, selected, title)
}
/// Edit an ISO calendar date using the shared date picker and write back a changed date string.
fn date_field(ui: &mut egui::Ui, value: &mut String) -> egui::Response {
    // An unavailable API date must remain unavailable. The workstation's date
    // cannot seed an accounting or replay range for the standalone client.
    let Ok(mut date) = chrono::NaiveDate::parse_from_str(value, "%Y-%m-%d") else {
        return ui.add(egui::TextEdit::singleline(value));
    };
    let response = ui.add(crate::date_picker::DatePickerButton::new(&mut date));
    if response.changed() {
        *value = date.format("%Y-%m-%d").to_string();
    }
    response
}
/// Paint a one-pixel inside border clipped to the control's visible bounds.
fn border(ui: &egui::Ui, rect: egui::Rect) {
    ui.painter().rect_stroke(
        rect.intersect(ui.clip_rect()),
        0.,
        egui::Stroke::new(1. / ui.ctx().pixels_per_point(), presentation::BORDER),
        egui::StrokeKind::Inside,
    );
}
/// Render a bounded managed grid with stable scoped IDs, full-row selection, editable cells, and clipped column rules. Return the selected row ID and double-click flag.
fn draw_grid(
    ui: &mut egui::Ui,
    g: &Grid,
    selected: Option<&str>,
    height: f32,
    fields: &mut BTreeMap<String, String>,
    controls: &mut BTreeMap<String, egui::Rect>,
    enabled: bool,
) -> Option<(String, bool)> {
    let bounds =
        egui::Rect::from_min_size(ui.cursor().min, egui::vec2(ui.available_width(), height));
    controls.insert(format!("grid-{}", g.key), bounds);
    let mut clicked = None;
    let spacing = ui.spacing().item_spacing.y;
    ui.push_id(("portfolio-grid-scope", &g.key), |ui| {
        ui.style_mut().interaction.selectable_labels = false;
        ui.spacing_mut().item_spacing.x = 0.;
        egui::ScrollArea::horizontal()
            .id_salt(("portfolio-grid-scroll", &g.key))
            .max_height(height)
            .auto_shrink([false, false])
            .show(ui, |ui| {
                let list = matches!(g.key.as_str(), "portfolios" | "funds");
                let widths = g
                    .columns
                    .iter()
                    .map(|c| {
                        if g.key == "portfolios" && c == "Name" {
                            420.
                        } else if c == "Property" {
                            220.
                        } else {
                            (c.len() as f32 * 7. + 16.).clamp(90., 240.)
                        }
                    })
                    .collect::<Vec<_>>();
                if !list || g.key == "portfolios" {
                    ui.set_min_width(widths.iter().sum::<f32>());
                }
                let row_ui = ui.new_child(
                    egui::UiBuilder::new()
                        .id_salt(("full-row", &g.key))
                        .max_rect(bounds),
                );
                let horizontal_gutter = if widths.iter().sum::<f32>() > bounds.width() {
                    ui.spacing().scroll.allocated_width()
                } else {
                    0.
                };
                let mut table = egui_extras::TableBuilder::new(ui)
                    .id_salt(("portfolio-grid", &g.key, &g.columns))
                    .sense(egui::Sense::click())
                    .resizable(true)
                    .fill_grid(
                        g.rows.len(),
                        26.,
                        spacing,
                        (height - 24. - spacing - 2. - horizontal_gutter).max(0.),
                    );
                for (i, column) in g.columns.iter().enumerate() {
                    table = table.column(if g.key == "portfolios" {
                        egui_extras::Column::initial(widths[i]).at_least(widths[i])
                    } else if list {
                        match column.as_str() {
                            "FundId" => egui_extras::Column::exact(80.),
                            "Name" => egui_extras::Column::remainder().at_least(180.),
                            "Portfolio" => egui_extras::Column::initial(340.).at_least(180.),
                            "OperatingState" => egui_extras::Column::exact(80.),
                            _ => egui_extras::Column::exact(65.),
                        }
                    } else if i + 1 == g.columns.len() {
                        egui_extras::Column::remainder().at_least(100.)
                    } else {
                        egui_extras::Column::initial(widths[i]).at_least(65.)
                    });
                }
                table
                    .header(24., |mut row| {
                        for column in &g.columns {
                            row.col(|ui| {
                                controls
                                    .insert(format!("header-{}-{column}", g.key), ui.max_rect());
                                ui.painter()
                                    .rect_filled(ui.max_rect(), 0., presentation::COMMAND);
                                ui.add(
                                    presentation::caption_widget(match column.as_str() {
                                        "PortfolioId" => "Portfolio ID",
                                        "FundId" => "Fund ID",
                                        "OperatingState" => "State",
                                        "PortfolioVersion" | "FundMandateVersion" => "Version",
                                        "TradeStrategyFamilyId" => "Strategy deployment",
                                        s => s,
                                    })
                                    .truncate(),
                                );
                                border(ui, ui.max_rect());
                            });
                        }
                    })
                    .bordered_body(24., |body| {
                        body.rows(26., g.rows.len(), |mut row| {
                            let item = &g.rows[row.index()];
                            row.set_selected(selected == Some(item.id.as_str()));
                            for column in &g.columns {
                                row.col(|ui| {
                                    let value =
                                        item.fields.get(column).cloned().unwrap_or_default();
                                    if column == "Property" {
                                        let response = ui
                                            .with_layout(
                                                egui::Layout::right_to_left(egui::Align::Min),
                                                |ui| {
                                                    ui.add_space(20.);
                                                    ui.add(
                                                        presentation::caption_widget(value)
                                                            .truncate(),
                                                    )
                                                },
                                            )
                                            .inner;
                                        controls.insert(
                                            format!("property-{}-{}", g.key, item.id),
                                            response.rect,
                                        );
                                    } else if value.eq_ignore_ascii_case("true")
                                        || value.eq_ignore_ascii_case("false")
                                    {
                                        let key =
                                            format!("risk-policy.limit.{}.{}", item.id, column);
                                        let mut checked = fields
                                            .get(&key)
                                            .unwrap_or(&value)
                                            .eq_ignore_ascii_case("true");
                                        if ui
                                            .add_enabled(
                                                enabled && g.editable,
                                                egui::Checkbox::without_text(&mut checked),
                                            )
                                            .changed()
                                        {
                                            fields.insert(key, checked.to_string());
                                        }
                                    } else if g.editable
                                        && !matches!(
                                            column.as_str(),
                                            "TradeStrategyFamilyId"
                                                | "DefinitionVersion"
                                                | "CatalogDeployment"
                                        )
                                    {
                                        let key =
                                            format!("risk-policy.limit.{}.{}", item.id, column);
                                        let v = fields.entry(key).or_insert(value);
                                        ui.add_enabled(
                                            enabled,
                                            egui::TextEdit::singleline(v)
                                                .desired_width(ui.available_width()),
                                        );
                                    } else {
                                        let response =
                                            ui.add(presentation::label_widget(value).truncate());
                                        if column == "Value" {
                                            controls.insert(
                                                format!("value-{}-{}", g.key, item.id),
                                                response.rect,
                                            );
                                        }
                                    }
                                });
                            }
                            let mut r = row.response();
                            if list {
                                let rect = egui::Rect::from_min_max(
                                    egui::pos2(bounds.left(), r.rect.top()),
                                    egui::pos2(bounds.right(), r.rect.bottom()),
                                );
                                controls.insert(format!("row-{}-{}", g.key, item.id), rect);
                                r = row_ui.interact(
                                    rect.intersect(row_ui.clip_rect()),
                                    row_ui.id().with(("portfolio-list-row", &g.key, &item.id)),
                                    egui::Sense::click(),
                                );
                            }
                            if enabled && (r.clicked() || r.double_clicked()) {
                                r.request_focus();
                                clicked = Some((item.id.clone(), r.double_clicked()));
                            } else if enabled
                                && g.key == "risk-history"
                                && r.has_focus()
                                && r.ctx.input(|i| i.key_pressed(egui::Key::Enter))
                            {
                                clicked = Some((item.id.clone(), true));
                            }
                        });
                    });
            });
    });
    border(ui, bounds);
    clicked
}
