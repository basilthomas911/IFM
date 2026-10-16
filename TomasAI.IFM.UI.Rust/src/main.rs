#![windows_subsystem = "windows"]
use ifm_ui_rust::presentation::TradingLabelUi as _;
mod app;
use app::hosting;
use eframe::egui;
use ifm_ui_rust::{Catalog, DetailsView, Snapshot, strings};
use std::{sync::mpsc, time::Duration};
enum Update {
    Stamped(Box<Update>, ifm_ui_rust::publication_transfer::Stamp),
    Ready(Catalog, Snapshot),
    Snapshot(Snapshot),
    Error(String),
    SubmissionRejected(String, String),
    Trades(ifm_ui_rust::contract_editor::ContractPublication),
    Contracts(ifm_ui_rust::contract_editor::ContractPublication),
    Inspection(ifm_ui_rust::strategy_inspection::InspectionPublication),
    Editor(ifm_ui_rust::lookup_editor::EditorPublication),
    Grid(Vec<ifm_ui_rust::virtual_grid::GridRow>),
    Dashboard(ifm_ui_rust::shell::Dashboard),
    Sidebar(ifm_ui_rust::sidebar::SidebarPublication),
}
struct ReadProfile {started:std::time::Instant,seconds:u64,stage:u8}
struct App {
    rx: mpsc::Receiver<Update>,
    tx: mpsc::SyncSender<u32>,
    catalog: Option<Catalog>,
    snapshot: Option<Snapshot>,
    view: DetailsView,
    shell: ifm_ui_rust::shell::MainShell,
    strategy: ifm_ui_rust::strategy_workspace::StrategyWorkspace,
    editor_tx: mpsc::SyncSender<String>,
    editor: ifm_ui_rust::lookup_editor::LookupEditor,
    editor_snapshot: ifm_ui_rust::lookup_editor::EditorPublication,
    catalog_tab: bool,
    strategy_catalog: ifm_ui_rust::strategy_catalog::StrategyCatalog,
    parameter_tab: bool,
    parameters: ifm_ui_rust::parameter_sets::ParameterSets,
    actor_tab: bool,
    actor_health: ifm_ui_rust::actor_health::ActorHealth,
    health_tab: bool,
    health: ifm_ui_rust::operations_health::OperationsHealth,
    scheduled_tab: bool,
    scheduled: ifm_ui_rust::scheduled_tasks::ScheduledTasks,
    provider_tab: bool,
    provider_browser: ifm_ui_rust::provider_browser::ProviderBrowser,
    calendar_tab: bool,
    calendar_editor: ifm_ui_rust::calendar_editor::CalendarEditor,
    trade_workspace: ifm_ui_rust::trade_workspace::TradeWorkspace,
    trade_snapshot: ifm_ui_rust::contract_editor::ContractPublication,
    yield_tab: bool,
    yield_editor: ifm_ui_rust::yield_curve_editor::YieldCurveEditor,
    contract_tab: bool,
    contract: ifm_ui_rust::contract_editor::ContractEditor,
    contract_snapshot: ifm_ui_rust::contract_editor::ContractPublication,
    lookup_tab: bool,
    inspection_tab: bool,
    inspection: ifm_ui_rust::strategy_inspection::StrategyInspection,
    inspection_snapshot: ifm_ui_rust::strategy_inspection::InspectionPublication,
    status: String,
    grid: Option<ifm_ui_rust::virtual_grid::GridView>,
    grid_tab: bool,
    grid_requested: bool,
    frame_times: Vec<f64>,
    latency: Option<ifm_ui_rust::latency::LatencyTrace>,
    read_profile: Option<ReadProfile>,
    fallback: Catalog,
    worker: Option<std::thread::JoinHandle<()>>,
}
impl eframe::App for App {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        let frame_started = std::time::Instant::now();
        let mut stamps=[None;9];
        while let Ok(update) = self.rx.try_recv() {
            let update=if let Update::Stamped(value,stamp)=update {stamps[stamp.kind as usize]=Some(stamp);*value}else{update};
            match update {
                Update::Stamped(..)=>unreachable!("A transfer wraps one UI update"),
                Update::Ready(c, s) => {
                    self.catalog = Some(c);
                    self.snapshot = Some(s);
                    self.status = self.catalog.as_ref().unwrap().text(strings::READY).into()
                }
                Update::Snapshot(s) => {
                    if self
                        .snapshot
                        .as_ref()
                        .is_none_or(|old| old.epoch != s.epoch || old.generation < s.generation)
                    {
                        if self
                            .snapshot
                            .as_ref()
                            .is_some_and(|old| old.workflow_id != s.workflow_id)
                        {
                            self.view.selected = None
                        }
                        self.snapshot = Some(s);
                    }
                }
                Update::SubmissionRejected(id, e) => {
                    self.trade_workspace.submission_rejected(&id);
                    self.status = e;
                }
                Update::Error(e) => {
                    self.editor.error = e.clone();
                    self.editor.pending = None;
                    self.inspection.pending = None;
                    self.contract.pending = None;
                    self.yield_editor.pending = None;
                    self.calendar_editor.pending = None;
                    self.provider_browser.pending = None;
                    self.scheduled.pending = None;
                    self.health.pending = None;
                    self.actor_health.pending = None;
                    self.parameters.pending = None;
                    self.strategy_catalog.pending = None;
                    self.status = e;
                }
                Update::Dashboard(s) => {
                    if !s.title.is_empty() {
                        ctx.send_viewport_cmd(egui::ViewportCommand::Title(s.title.clone()));
                    }
                    self.shell.dashboard = s;
                }
                Update::Sidebar(s) => self.shell.sidebar_snapshot = s,
                Update::Trades(s) => self.trade_snapshot = s,
                Update::Contracts(s) => self.contract_snapshot = s,
                Update::Editor(s) => self.editor_snapshot = s,
                Update::Inspection(s) => self.inspection_snapshot = s,
                Update::Grid(rows) => {
                    self.grid = Some(ifm_ui_rust::virtual_grid::GridView::new(rows))
                }
            }
        }
        let catalog = self.catalog.as_ref().unwrap_or(&self.fallback);
        egui::TopBottomPanel::top("main-menu")
            .exact_height(27.0)
            .frame(
                egui::Frame::NONE
                    .fill(egui::Color32::BLACK)
                    .inner_margin(egui::Margin::symmetric(4, 0)),
            )
            .show(ctx, |ui| {
                egui::MenuBar::new().ui(ui, |ui| {
                    if ui.button("Trade Orders").clicked() {
                        self.trade_workspace.open = true;
                        let payload = self.trade_workspace.request("load", &self.trade_snapshot);
                        if self.editor_tx.try_send(payload.clone()).is_err() {
                            let id = serde_json::from_str::<serde_json::Value>(&payload).unwrap()["requestId"].as_str().unwrap().to_owned();
                            self.trade_workspace.submission_rejected(&id);
                            self.status = "Trade request queue is unavailable. Please try again.".into();
                        }
                        self.shell.workspace.clear();
                        self.lookup_tab = false;
                        self.contract_tab = false;
                        self.inspection_tab = false;
                        self.parameter_tab = false;
                        self.catalog_tab = false;
                        self.calendar_tab = false;
                        self.yield_tab = false;
                        self.provider_tab = false;
                        self.grid_tab = false;
                        self.actor_tab = false;
                        self.health_tab = false;
                        self.scheduled_tab = false;
                    }
                    if ui.button("Market Data").clicked() {
                        self.shell.workspace = "Market Data".into();
                        self.lookup_tab = false;
                        self.contract_tab = false;
                        self.inspection_tab = false;
                        self.parameter_tab = false;
                        self.catalog_tab = false;
                        self.calendar_tab = false;
                        self.yield_tab = false;
                        self.provider_tab = false;
                        self.grid_tab = false;
                        self.actor_tab = false;
                        self.health_tab = false;
                        self.scheduled_tab = false;
                    }
                    if ui.button("Portfolio").clicked() {
                        self.shell.workspace = "Portfolio".into();
                        self.lookup_tab = false;
                        self.contract_tab = false;
                        self.inspection_tab = false;
                        self.parameter_tab = false;
                        self.catalog_tab = false;
                        self.calendar_tab = false;
                        self.yield_tab = false;
                        self.provider_tab = false;
                        self.grid_tab = false;
                        self.actor_tab = false;
                        self.health_tab = false;
                        self.scheduled_tab = false;
                    }
                    ui.menu_button("Reference", |ui| {
                        if ui
                            .selectable_label(
                                !self.lookup_tab
                                    && !self.grid_tab
                                    && !self.inspection_tab
                                    && !self.contract_tab
                                    && !self.yield_tab
                                    && !self.calendar_tab
                                    && !self.provider_tab
                                    && !self.scheduled_tab
                                    && !self.catalog_tab
                                    && !self.parameter_tab
                                    && !self.actor_tab
                                    && !self.health_tab,
                                catalog.text(strings::DETAILS),
                            )
                            .clicked()
                        {
                            self.yield_tab = false;
                            self.calendar_tab = false;
                            self.provider_tab = false;
                            self.scheduled_tab = false;
                            self.health_tab = false;
                            self.actor_tab = false;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.contract_tab = false;
                            self.grid_tab = false;
                            self.lookup_tab = false;
                            self.inspection_tab = false;
                        }
                        if ui
                            .selectable_label(
                                self.lookup_tab,
                                catalog.text(strings::LOOKUP_DEFINITIONS),
                            )
                            .clicked()
                        {
                            self.contract_tab = false;
                            self.grid_tab = false;
                            self.yield_tab = false;
                            self.calendar_tab = false;
                            self.provider_tab = false;
                            self.scheduled_tab = false;
                            self.health_tab = false;
                            self.actor_tab = false;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.lookup_tab = true;
                            self.inspection_tab = false;
                        }
                        for (label, risk) in [
                            (strings::STRATEGY_OBSERVATION, false),
                            (strings::RISK_HISTORY, true),
                        ] {
                            if ui
                                .selectable_label(
                                    self.inspection_tab && self.inspection.risk == risk,
                                    catalog.text(label),
                                )
                                .clicked()
                            {
                                self.contract_tab = false;
                                self.yield_tab = false;
                                self.calendar_tab = false;
                                self.provider_tab = false;
                                self.scheduled_tab = false;
                                self.health_tab = false;
                                self.actor_tab = false;
                                self.parameter_tab = false;
                                self.catalog_tab = false;
                                self.inspection_tab = true;
                                self.lookup_tab = false;
                                self.grid_tab = false;
                                self.inspection.risk = risk;
                            }
                        }
                        for (label, option) in [
                            (strings::FUTURES_CONTRACTS, false),
                            (strings::OPTION_CONTRACTS, true),
                        ] {
                            if ui
                                .selectable_label(
                                    self.contract_tab && self.contract.option == option,
                                    catalog.text(label),
                                )
                                .clicked()
                            {
                                if self.contract.option != option {
                                    self.contract.reset();
                                }
                                self.contract.option = option;
                                self.yield_tab = false;
                                self.calendar_tab = false;
                                self.provider_tab = false;
                                self.scheduled_tab = false;
                                self.health_tab = false;
                                self.actor_tab = false;
                                self.parameter_tab = false;
                                self.catalog_tab = false;
                                self.contract_tab = true;
                                self.lookup_tab = false;
                                self.inspection_tab = false;
                                self.grid_tab = false;
                            }
                        }
                        if ui
                            .selectable_label(self.yield_tab, catalog.text(strings::YIELD_CURVE))
                            .clicked()
                        {
                            self.yield_tab = true;
                            self.calendar_tab = false;
                            self.provider_tab = false;
                            self.scheduled_tab = false;
                            self.health_tab = false;
                            self.actor_tab = false;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.contract_tab = false;
                            self.lookup_tab = false;
                            self.inspection_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(
                                self.provider_tab,
                                catalog.text(strings::PROVIDER_DEFINITIONS),
                            )
                            .clicked()
                        {
                            self.provider_tab = true;
                            self.calendar_tab = false;
                            self.yield_tab = false;
                            self.contract_tab = false;
                            self.lookup_tab = false;
                            self.inspection_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(
                                self.calendar_tab,
                                catalog.text(strings::ECONOMIC_CALENDAR),
                            )
                            .clicked()
                        {
                            self.calendar_tab = true;
                            self.provider_tab = false;
                            self.scheduled_tab = false;
                            self.health_tab = false;
                            self.actor_tab = false;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.yield_tab = false;
                            self.contract_tab = false;
                            self.lookup_tab = false;
                            self.inspection_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(
                                self.health_tab,
                                catalog.text(strings::OPERATIONS_HEALTH),
                            )
                            .clicked()
                        {
                            self.health_tab = true;
                            self.actor_tab = false;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.scheduled_tab = false;
                            self.lookup_tab = false;
                            self.inspection_tab = false;
                            self.contract_tab = false;
                            self.yield_tab = false;
                            self.calendar_tab = false;
                            self.provider_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(self.catalog_tab, catalog.text("strategy_catalog"))
                            .clicked()
                        {
                            self.catalog_tab = true;
                            self.parameter_tab = false;
                            self.actor_tab = false;
                            self.health_tab = false;
                            self.scheduled_tab = false;
                            self.provider_tab = false;
                            self.calendar_tab = false;
                            self.yield_tab = false;
                            self.contract_tab = false;
                            self.inspection_tab = false;
                            self.lookup_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(
                                self.parameter_tab,
                                catalog.text(strings::PARAMETER_SETS),
                            )
                            .clicked()
                        {
                            self.parameter_tab = true;
                            self.catalog_tab = false;
                            self.actor_tab = false;
                            self.health_tab = false;
                            self.scheduled_tab = false;
                            self.provider_tab = false;
                            self.calendar_tab = false;
                            self.yield_tab = false;
                            self.contract_tab = false;
                            self.inspection_tab = false;
                            self.lookup_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(self.actor_tab, catalog.text(strings::ACTOR_HEALTH))
                            .clicked()
                        {
                            self.actor_tab = true;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.health_tab = false;
                            self.scheduled_tab = false;
                            self.provider_tab = false;
                            self.calendar_tab = false;
                            self.yield_tab = false;
                            self.contract_tab = false;
                            self.inspection_tab = false;
                            self.lookup_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(
                                self.scheduled_tab,
                                catalog.text(strings::SCHEDULED_TASKS),
                            )
                            .clicked()
                        {
                            self.scheduled_tab = true;
                            self.health_tab = false;
                            self.actor_tab = false;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.lookup_tab = false;
                            self.inspection_tab = false;
                            self.contract_tab = false;
                            self.yield_tab = false;
                            self.calendar_tab = false;
                            self.provider_tab = false;
                            self.grid_tab = false;
                        }
                        if ui
                            .selectable_label(self.grid_tab, catalog.text(strings::GRID))
                            .clicked()
                        {
                            self.lookup_tab = false;
                            self.inspection_tab = false;
                            self.contract_tab = false;
                            self.yield_tab = false;
                            self.calendar_tab = false;
                            self.provider_tab = false;
                            self.scheduled_tab = false;
                            self.health_tab = false;
                            self.actor_tab = false;
                            self.parameter_tab = false;
                            self.catalog_tab = false;
                            self.grid_tab = true;
                            if !self.grid_requested && self.tx.try_send(3).is_ok() {
                                self.grid_requested = true;
                            }
                        }
                    });
                    ui.menu_button("System", |ui| {
                        for (label, section) in [
                            ("Scheduled Tasks", 0),
                            ("Actor Health", 1),
                            ("Operations health", 2),
                        ] {
                            if ui.button(label).clicked() {
                                self.shell.select_system(
                                    section,
                                    &mut self.scheduled_tab,
                                    &mut self.actor_tab,
                                    &mut self.health_tab,
                                );
                                self.lookup_tab = false;
                                self.contract_tab = false;
                                self.inspection_tab = false;
                                self.parameter_tab = false;
                                self.catalog_tab = false;
                                self.calendar_tab = false;
                                self.yield_tab = false;
                                self.provider_tab = false;
                                self.grid_tab = false;
                                ui.close();
                            }
                        }
                    });
                    // The WinForms toolbar separator sits above the Operations splitter.
                    let left_width = egui::containers::panel::PanelState::load(
                        ctx,
                        egui::Id::new("operations-pane"),
                    )
                    .map(|panel| panel.rect.width())
                    .unwrap_or(ctx.screen_rect().width() * 0.22);
                    ui.add_space((left_width - ui.cursor().left()).max(0.0));
                    ui.separator();
                    if ifm_ui_rust::presentation::feed_command_button(ui, &self.shell.sidebar_snapshot.feed_control) {
                        if self.tx.try_send(ifm_ui_rust::sidebar::FEED_TOGGLE_ACTION).is_err() {
                            self.status = "Feed action queue is unavailable; command was not submitted.".into();
                        }
                    }
                    let health = ifm_ui_rust::presentation::feed_health_text(
                        &self.shell.dashboard.feed_health,
                    );
                    let (health_bg, health_fg) = ifm_ui_rust::presentation::feed_health_palette(
                        &self.shell.dashboard.feed_health,
                    );
                    if ui
                        .add(
                            egui::Button::new(egui::RichText::new(health).color(health_fg))
                                .fill(health_bg)
                                .frame(true),
                        )
                        .on_hover_text(&self.shell.dashboard.feed_health_reason)
                        .clicked()
                    {
                        self.health_tab = true;
                        self.actor_tab = false;
                        self.scheduled_tab = false;
                        self.catalog_tab = false;
                        self.parameter_tab = false;
                        self.lookup_tab = false;
                        self.contract_tab = false;
                        self.inspection_tab = false;
                        self.provider_tab = false;
                        self.calendar_tab = false;
                        self.yield_tab = false;
                        self.grid_tab = false;
                    }
                    if ui.button("Operations health").clicked() {
                        self.health_tab = true;
                        self.actor_tab = false;
                        self.scheduled_tab = false;
                        self.catalog_tab = false;
                        self.parameter_tab = false;
                        self.lookup_tab = false;
                        self.contract_tab = false;
                        self.inspection_tab = false;
                        self.provider_tab = false;
                        self.calendar_tab = false;
                        self.yield_tab = false;
                        self.grid_tab = false;
                    }
                    if ui.button("Actor Health").clicked() {
                        self.actor_tab = true;
                        self.health_tab = false;
                        self.scheduled_tab = false;
                        self.catalog_tab = false;
                        self.parameter_tab = false;
                        self.lookup_tab = false;
                        self.contract_tab = false;
                        self.inspection_tab = false;
                        self.provider_tab = false;
                        self.calendar_tab = false;
                        self.yield_tab = false;
                        self.grid_tab = false;
                    }
                });
            });
        egui::TopBottomPanel::bottom("main-status")
            .exact_height(24.0)
            .frame(
                egui::Frame::NONE
                    .fill(egui::Color32::BLACK)
                    .inner_margin(egui::Margin::symmetric(4, 2)),
            )
            .show(ctx, |ui| {
                let message = self
                    .shell
                    .sidebar_snapshot
                    .latest_status()
                    .unwrap_or(&self.status);
                ui.add(ifm_ui_rust::presentation::label_widget(message).truncate())
                    .on_hover_text(message);
            });
        let shell_action = self.shell.draw_panes(
            ctx,
            self.snapshot.as_ref(),
            catalog,
            &mut self.strategy,
            &mut self.view,
        );
        if let Some(action) = shell_action {
            let _ = self.tx.try_send(action);
        }
        self.trade_workspace.loaded_bounds = None;
        if self.trade_workspace.is_visible() {
            if let Some(payload) = self.trade_workspace.draw(ctx, &self.trade_snapshot) {
                if let Err(e) = self.editor_tx.try_send(payload.clone()) {
                    let id = serde_json::from_str::<serde_json::Value>(&payload).unwrap()["requestId"].as_str().unwrap().to_owned();
                    self.trade_workspace.submission_rejected(&id);
                    self.status = e.to_string();
                }
            }
        }
        if self.trade_workspace.loaded_bounds.is_none() {
        if self.catalog_tab {
            if let Some(payload) = self.strategy_catalog.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.strategy_catalog.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.parameter_tab {
            if let Some(payload) = self.parameters.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.parameters.pending = None;
                    self.strategy_catalog.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.actor_tab {
            if let Some(payload) = self.actor_health.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.actor_health.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.health_tab {
            if let Some(payload) = self.health.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.health.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.scheduled_tab {
            if let Some(payload) = self.scheduled.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.scheduled.pending = None;
                    self.health.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.provider_tab {
            if let Some(payload) = self.provider_browser.draw(
                ctx,
                &self.contract_snapshot,
                &catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.provider_browser.pending = None;
                    self.scheduled.pending = None;
                    self.health.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.calendar_tab {
            if let Some(payload) = self.calendar_editor.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.calendar_editor.pending = None;
                    self.provider_browser.pending = None;
                    self.scheduled.pending = None;
                    self.health.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.yield_tab {
            if let Some(payload) = self.yield_editor.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.yield_editor.pending = None;
                    self.calendar_editor.pending = None;
                    self.provider_browser.pending = None;
                    self.scheduled.pending = None;
                    self.health.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.contract_tab {
            if let Some(payload) = self.contract.draw(
                ctx,
                &self.contract_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.contract.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.inspection_tab {
            if let Some(payload) = self.inspection.draw(
                ctx,
                &self.inspection_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.inspection.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.lookup_tab {
            if let Some(payload) = self.editor.draw(
                ctx,
                &self.editor_snapshot,
                catalog,
                self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
            ) {
                if self.editor_tx.try_send(payload).is_err() {
                    self.editor.pending = None;
                    self.editor.error = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        } else if self.grid_tab {
            if let Some(grid) = &mut self.grid {
                grid.draw(ctx, catalog);
            } else {
                egui::CentralPanel::default().show(ctx, |ui| {
                    ui.trading_caption(catalog.text(strings::LOADING_GRID));
                });
            }
        } else {
            egui::CentralPanel::default().show(ctx, |ui| {
                ui.trading_label(&self.shell.workspace);
            });
        }
        }
        if let Some(profile)=&mut self.read_profile {
            if profile.started.elapsed().as_secs()>=profile.seconds {ctx.send_viewport_cmd(egui::ViewportCommand::Close);}
            else if let Some(request)=self.trade_workspace.profile_read(profile.stage,&self.trade_snapshot){
                if self.editor_tx.try_send(request).is_ok(){profile.stage+=1;}
            }
            ctx.request_repaint_after(Duration::from_millis(20));
        }
        if !self.trade_workspace.is_visible(){stamps[8]=None;}
        if let Some(trace)=&self.latency {trace.paint(ctx,stamps);}
        if self.frame_times.len() < 10000 {
            self.frame_times
                .push(frame_started.elapsed().as_secs_f64() * 1000.0);
        }
    }
}
impl Drop for App {
    fn drop(&mut self) {
        if let Ok(path) = std::env::var("IFM_RUST_UI_METRICS") {
            let mut warm = self
                .frame_times
                .iter()
                .skip(10)
                .copied()
                .collect::<Vec<_>>();
            warm.sort_by(f64::total_cmp);
            if !warm.is_empty() {
                let report = serde_json::json!({"measurement":"native CPU update/layout; excludes GPU presentation and input latency","warmFrames":warm.len(),"p50Ms":warm[warm.len()/2],"p95Ms":warm[(warm.len()*95/100).min(warm.len()-1)],"maxMs":warm.last(),"gridRows":self.grid.as_ref().map(|g|g.rows.len()),"drawnRows":self.grid.as_ref().map(|g|g.drawn.len())});
                let _ = std::fs::write(path, serde_json::to_vec_pretty(&report).unwrap());
            }
        }

        if let Some(trace)=&self.latency {trace.finish();}
        let _ = self.tx.send(u32::MAX);
        if let Some(worker) = self.worker.take() {
            let _ = worker.join();
        }
    }
}
fn main() -> eframe::Result {
    let root = std::env::current_exe()
        .unwrap()
        .parent()
        .unwrap()
        .join("managed");
    let (updates, rx) = mpsc::sync_channel(4);
    let (tx, actions) = mpsc::sync_channel(32);
    let (editor_tx, editor_actions) = mpsc::sync_channel::<String>(32);
    let maximized = std::env::var("IFM_UI_START_MAXIMIZED")
        .map(|value| value.eq_ignore_ascii_case("true") || value == "1")
        .unwrap_or_else(|_| std::env::var("IFM_RUST_UI_MODE").is_ok_and(|value| value == "live"));
    // A forced initial inner size can override Windows' maximized placement at startup.
    let mut viewport = egui::ViewportBuilder::default().with_maximized(maximized);
    if !maximized {
        viewport = viewport.with_inner_size(ifm_ui_rust::presentation::MAIN_CLIENT_SIZE);
    }
    let mut options = eframe::NativeOptions {
        viewport,
        renderer: eframe::Renderer::Wgpu,
        ..Default::default()
    };
    if let eframe::egui_wgpu::WgpuSetup::CreateNew(setup) = &mut options.wgpu_options.wgpu_setup {
        setup.native_adapter_selector = Some(std::sync::Arc::new(|adapters, surface| {
            let suitable = |a: &&wgpu::Adapter| surface.is_none_or(|s| a.is_surface_supported(s));
            let chosen = adapters
                .iter()
                .filter(suitable)
                .find(|a| a.get_info().name.contains("3060"))
                .ok_or_else(|| {
                    format!(
                        "Required display GPU RTX 3060 Ti unavailable; adapters: {:?}",
                        adapters
                            .iter()
                            .map(|a| a.get_info().name)
                            .collect::<Vec<_>>()
                    )
                })?;
            Ok(chosen.clone())
        }));
    }
    eframe::run_native(
        &serde_json::from_str::<Catalog>(include_str!("../catalog/strings.en.json"))
            .unwrap()
            .text(strings::TITLE)
            .to_owned(),
        options,
        Box::new(move |cc| {
            ifm_ui_rust::apply_winforms_typography(&cc.egui_ctx)?;
            let ctx = cc.egui_ctx.clone();
            let worker = std::thread::spawn(move || {
                let result = (|| -> Result<(), String> {
                    let managed = hosting::Managed::load(&root)?;
                    let catalog: Catalog =
                        ifm_ui_rust::wire::decode(&managed.copy(1)?).map_err(|e| e.to_string())?;
                    if catalog.version != 1 {
                        return Err("Catalog version mismatch".into());
                    }
                    let mut catalog = Some(catalog);
                    let mut generation = 0;
                    let mut publications=ifm_ui_rust::publication_transfer::TransferCache::default();
                    let profiling=std::env::var_os("IFM_UI_LATENCY_OUTPUT").is_some();
                    let stamped=|update,kind,cache:&ifm_ui_rust::publication_transfer::TransferCache|if profiling{Update::Stamped(Box::new(update),cache.stamp(kind))}else{update};
                    let mut want_grid = false;
                    loop {
                        match actions.recv_timeout(Duration::from_millis(20)) {
                            Ok(u32::MAX) => break,
                            Ok(action) => {
                                managed.action(action)?;
                                if action == 3 {
                                    want_grid = true;
                                }
                            }
                            Err(mpsc::RecvTimeoutError::Disconnected) => break,
                            Err(mpsc::RecvTimeoutError::Timeout) => {}
                        }
                        while let Ok(intent) = editor_actions.try_recv() {
                            if let Err(e) = managed.submit(&intent) {
                                let request: serde_json::Value = serde_json::from_str(&intent).unwrap_or_default();
                                if request["view"] == "trade-orders" {
                                    let id = request["requestId"].as_str().unwrap_or_default().to_owned();
                                    // A rejection has no managed outcome; never drop its acknowledgement.
                                    let _ = updates.send(Update::SubmissionRejected(id, e));
                                } else {
                                    let _ = updates.send(Update::Error(e));
                                }
                                ctx.request_repaint();
                            }
                        }
                        if managed.poll(6,&mut publications)? {
                            let dashboard=ifm_ui_rust::wire::decode(publications.bytes(6)).map_err(|e|e.to_string())?;
                            if updates.try_send(stamped(Update::Dashboard(dashboard),6,&publications)).is_ok(){publications.acknowledge(6);ctx.request_repaint();}
                        }
                        if managed.poll(7,&mut publications)? {
                            let sidebar=ifm_ui_rust::wire::decode(publications.bytes(7)).map_err(|e|e.to_string())?;
                            if updates.try_send(stamped(Update::Sidebar(sidebar),7,&publications)).is_ok(){publications.acknowledge(7);ctx.request_repaint();}
                        }
                        if managed.poll(8,&mut publications)? {
                            let trade=ifm_ui_rust::wire::decode(publications.bytes(8)).map_err(|e|e.to_string())?;
                            if updates.try_send(stamped(Update::Trades(trade),8,&publications)).is_ok(){publications.acknowledge(8);ctx.request_repaint();}
                        }
                        if managed.poll(5,&mut publications)? {
                            let contract=ifm_ui_rust::wire::decode(publications.bytes(5)).map_err(|e|e.to_string())?;
                            if updates.try_send(stamped(Update::Contracts(contract),5,&publications)).is_ok(){publications.acknowledge(5);ctx.request_repaint();}
                        }
                        if managed.poll(4,&mut publications)? {
                            let inspection=ifm_ui_rust::wire::decode(publications.bytes(4)).map_err(|e|e.to_string())?;
                            if updates.try_send(stamped(Update::Inspection(inspection),4,&publications)).is_ok(){publications.acknowledge(4);ctx.request_repaint();}
                        }
                        if managed.poll(3,&mut publications)? {
                            let editor=ifm_ui_rust::wire::decode(publications.bytes(3)).map_err(|e|e.to_string())?;
                            if updates.try_send(stamped(Update::Editor(editor),3,&publications)).is_ok(){publications.acknowledge(3);ctx.request_repaint();}
                        }
                        if want_grid {
                            let bytes = managed.copy(2)?;
                            if !bytes.is_empty() {
                                let rows = ifm_ui_rust::wire::decode(&bytes)
                                    .map_err(|e| format!("Grid snapshot: {e}"))?;
                                if updates.try_send(Update::Grid(rows)).is_ok() {
                                    want_grid = false;
                                    ctx.request_repaint();
                                }
                            }
                        }
                        if !managed.poll(0,&mut publications)? {continue;}
                        let data = publications.bytes(0);
                        if data.is_empty() {
                            continue;
                        }
                        let payload: serde_json::Value =
                            ifm_ui_rust::wire::decode(&data).map_err(|e| e.to_string())?;
                        if let Some(error) = payload.get("error").and_then(|v| v.as_str()) {
                            return Err(format!("Managed bridge failed: {error}"));
                        }
                        let s: Snapshot = ifm_ui_rust::wire::decode(&data)
                            .map_err(|e| format!("Invalid managed snapshot: {e}"))?;
                        if s.schema != 1 {
                            return Err("Snapshot schema mismatch".into());
                        }
                        if generation == s.generation {publications.acknowledge(0);}
                        if generation != s.generation {
                            let next_generation = s.generation;
                            let update = if let Some(c) = catalog.as_ref() {
                                Update::Ready(c.clone(), s)
                            } else {
                                Update::Snapshot(s)
                            };
                            if updates.try_send(stamped(update,0,&publications)).is_ok() {
                                generation = next_generation;
                                catalog = None;
                                publications.acknowledge(0);
                            }
                            ctx.request_repaint();
                        }
                    }
                    Ok(())
                })();
                if let Err(e) = result {
                    eprintln!("{e}");
                    let _ = updates.try_send(Update::Error(e));
                    ctx.request_repaint();
                }
            });
            Ok(Box::new(App {
                rx,
                tx,
                editor_tx,
                editor: Default::default(),
                editor_snapshot: Default::default(),
                catalog_tab: false,
                strategy_catalog: Default::default(),
                parameter_tab: false,
                parameters: Default::default(),
                actor_tab: false,
                actor_health: Default::default(),
                health_tab: false,
                health: Default::default(),
                scheduled_tab: false,
                scheduled: Default::default(),
                provider_tab: false,
                provider_browser: Default::default(),
                calendar_tab: false,
                calendar_editor: Default::default(),
                trade_workspace: Default::default(),
                trade_snapshot: Default::default(),
                yield_tab: false,
                yield_editor: Default::default(),
                contract_tab: false,
                contract: Default::default(),
                contract_snapshot: Default::default(),
                lookup_tab: false,
                inspection_tab: false,
                inspection: Default::default(),
                inspection_snapshot: Default::default(),
                catalog: None,
                snapshot: None,
                view: DetailsView::default(),
                shell: ifm_ui_rust::shell::MainShell::default(),
                strategy: ifm_ui_rust::strategy_workspace::StrategyWorkspace::for_shell(),
                grid: None,
                grid_tab: false,
                grid_requested: false,
                frame_times: Vec::new(),
                latency: ifm_ui_rust::latency::LatencyTrace::from_env(),
                read_profile: if std::env::var("DOTNET_ENVIRONMENT").is_ok_and(|v|v=="Development") && std::env::var("IFM_RUST_UI_MODE").is_ok_and(|v|v=="live") {
                    std::env::var("IFM_UI_PROFILE_TRADE_SECONDS").ok().and_then(|v|v.parse::<u64>().ok()).map(|seconds|ReadProfile{started:std::time::Instant::now(),seconds:seconds.clamp(10,300),stage:0})
                }else{None},
                status: serde_json::from_str::<Catalog>(include_str!("../catalog/strings.en.json"))
                    .unwrap()
                    .text(strings::STARTING)
                    .into(),
                worker: Some(worker),
                fallback: serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap(),
            }))
        }),
    )
}
