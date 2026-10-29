//! Standalone executable with native services and copied egui screens.
//!
//! `main` creates bounded channels, starts a native service runtime, and creates
//! the eframe application. Controllers perform backend I/O away from paint callbacks
//! and publish display state. There is no CLR or managed host in this executable.
//!
//! Channels bound memory: updates have capacity 4; numeric and structured actions
//! each have capacity 32. A publication is acknowledged only after its update has
//! been queued, so a full update channel does not silently consume the pending data.
//! Shutdown signals the worker before joining it and finishing optional profiling.
//! No trading command is executed directly by an egui paint callback.

#![windows_subsystem = "windows"]
use ifm_ui_egui::presentation::TradingLabelUi as _;
/// Native executable support. The service runtime stays private to the binary; reusable view code lives in the library.
mod app;
use app::runtime;
use eframe::egui;
use ifm_ui_egui::{Catalog, DetailsView, Snapshot, strings};
use std::{sync::mpsc, time::Duration};
/// Owned message from the native service runtime to the application/UI thread.
enum Update {
    /// A worker update paired with its source/publication/native-receipt timing stamp.
    Stamped(Box<Update>, ifm_ui_egui::publication_transfer::Stamp),
    /// Initial localized catalog and strategy snapshot, delivered together after host startup.
    Ready(Catalog, Snapshot),
    /// Changed strategy observation/details publication.
    Snapshot(Snapshot),
    /// Startup-blocking failure; runtime warnings must not use this modal path.
    Error(String),
    /// Recoverable service or action failure, visible in status/console and logs.
    Warning(String),
    /// Request ID and error for an intent rejected before execution.
    SubmissionRejected(String, String),
    /// Changed trading publication with background-prepared display data.
    Trades(ifm_ui_egui::contract_editor::ContractPublication),
    /// Latest-only monitor rows arrive independently of interactive command owners.
    TradeMonitor(std::sync::Arc<ifm_ui_egui::state::trade_monitors::Publication>),
    /// Changed contract/administration publication.
    Contracts(ifm_ui_egui::contract_editor::ContractPublication),
    /// Changed risk/workflow inspection publication.
    Inspection(ifm_ui_egui::strategy_inspection::InspectionPublication),
    /// Changed lookup editor publication.
    Editor(ifm_ui_egui::lookup_editor::EditorPublication),
    /// Loaded deterministic fixture rows for the local virtual-grid demonstration.
    Grid(Vec<ifm_ui_egui::virtual_grid::GridRow>),
    /// Changed main-window dashboard/outlook/chart projection.
    Dashboard(ifm_ui_egui::shell::Dashboard),
    /// Independent chart refreshes cannot replace outlook or sidebar state.
    MarketCharts(std::collections::BTreeMap<String, ifm_ui_egui::market_charts::ChartSnapshot>),
    /// Outlook owns only its cells/status; chart and feed owners publish separately.
    MarketOutlook(Result<ifm_ui_egui::services::outlook::Outlook, String>),
    /// Independent HTTP diagnostics cannot overwrite calendar or outlook fields.
    PipelineHealth(ifm_ui_egui::services::dashboard_health::PipelineHealth),
    /// Changed feed/calendar/status publication.
    Sidebar(ifm_ui_egui::sidebar::SidebarPublication),
}
/// Opt-in staged read-only profiling session; records start time, duration, and progress.
struct ReadProfile {
    /** Monotonic time at which the profiling session began. */
    started: std::time::Instant,
    /** Requested profiling duration in seconds. */
    seconds: u64,
    /** Current profiling or task-run stage. */
    stage: u8,
}
/// UI-thread application state. Owns cached publications/views and bounded channels to the native service runtime.
struct App {
    stopping: std::sync::Arc<std::sync::atomic::AtomicBool>,
    error_dialog: Option<String>,
    /// Retains runtime diagnostics across independent sidebar publications.
    runtime_notices: ifm_ui_egui::services::notifications::Notifications,
    /// UI-thread receiver for owned updates from the intake worker.
    rx: mpsc::Receiver<Update>,
    /// Bounded sender of numeric user actions to the intake worker.
    tx: mpsc::SyncSender<u32>,
    /// Embedded localized catalog published by the native runtime after startup.
    catalog: Option<Catalog>,
    /// Latest immutable strategy publication retained for rendering.
    snapshot: Option<Snapshot>,
    /// Read-only workflow tree/property renderer with persistent selected-node identity.
    view: DetailsView,
    /// Main three-pane shell state.
    shell: ifm_ui_egui::shell::MainShell,
    /// Strategy observation tabs and graph cache.
    strategy: ifm_ui_egui::strategy_workspace::StrategyWorkspace,
    /// Bounded sender of structured intent payloads to the intake worker.
    editor_tx: mpsc::SyncSender<String>,
    /// Lookup editor draft/selection state.
    editor: ifm_ui_egui::lookup_editor::LookupEditor,
    /// Latest lookup publication.
    editor_snapshot: ifm_ui_egui::lookup_editor::EditorPublication,
    /// Whether the catalog view is shown in the application.
    catalog_tab: bool,
    /// Strategy catalog browser/editor state.
    strategy_catalog: ifm_ui_egui::strategy_catalog::StrategyCatalog,
    /// Whether the parameter view is shown in the application.
    parameter_tab: bool,
    /// Parameter-set browser/editor state.
    parameters: ifm_ui_egui::parameter_sets::ParameterSets,
    /// Whether the actor view is shown in the application.
    actor_tab: bool,
    /// Actor health view and confirmation state.
    actor_health: ifm_ui_egui::actor_health::ActorHealth,
    /// Whether the health view is shown in the application.
    health_tab: bool,
    /// Operational health view state.
    health: ifm_ui_egui::operations_health::OperationsHealth,
    /// Whether the scheduled view is shown in the application.
    scheduled_tab: bool,
    /// WinForms-compatible modal System administration owner.
    system_open: bool,
    system_release_pending: bool,
    system_load_pending: bool,
    system_section: usize,
    backup: ifm_ui_egui::database_backup::DatabaseBackup,
    /// Scheduled-task editor/run-history state.
    scheduled: ifm_ui_egui::scheduled_tasks::ScheduledTasks,
    /// Whether the provider view is shown in the application.
    provider_tab: bool,
    /// Provider browsing/mapping state.
    provider_browser: ifm_ui_egui::provider_browser::ProviderBrowser,
    /// Whether the calendar view is shown in the application.
    calendar_tab: bool,
    /// Economic-calendar editor state.
    calendar_editor: ifm_ui_egui::calendar_editor::CalendarEditor,
    /// Trade-order dialogs and established-trade tab state.
    trade_workspace: ifm_ui_egui::trade_workspace::TradeWorkspace,
    /// Latest trading publication, prepared for display outside the UI thread.
    trade_snapshot: ifm_ui_egui::contract_editor::ContractPublication,
    trade_monitor: Option<std::sync::Arc<ifm_ui_egui::state::trade_monitors::Publication>>,
    /// Whether the yield view is shown in the application.
    yield_tab: bool,
    /// Yield-curve editor draft/selection state.
    yield_editor: ifm_ui_egui::yield_curve_editor::YieldCurveEditor,
    /// Combined Market Data dialog state.
    market_data: ifm_ui_egui::market::market_data_manager::MarketDataManager,
    /// Portfolio Administration dialog state.
    portfolio: ifm_ui_egui::portfolio::PortfolioManager,
    /// Whether the contract view is shown in the application.
    contract_tab: bool,
    /// Contract editor instance reused by the enclosing screen.
    contract: ifm_ui_egui::contract_editor::ContractEditor,
    /// Latest shared contract/administration publication.
    publications: ifm_ui_egui::state::publications::Publications,
    /// Whether the lookup view is shown in the application.
    lookup_tab: bool,
    /// Whether the inspection view is shown in the application.
    inspection_tab: bool,
    /// Risk/workflow inspection view state.
    inspection: ifm_ui_egui::strategy_inspection::StrategyInspection,
    /// Latest inspection query publication.
    inspection_snapshot: ifm_ui_egui::strategy_inspection::InspectionPublication,
    /// Display-ready status supplied by the managed projection or current view state.
    status: String,
    /// Optional locally virtualized fixture-grid state.
    grid: Option<ifm_ui_egui::virtual_grid::GridView>,
    /// Whether the grid view is shown in the application.
    grid_tab: bool,
    /// Optional collected frame-duration samples for native profiling.
    frame_times: Vec<f64>,
    /// Optional publication-to-render timing recorder.
    latency: Option<ifm_ui_egui::latency::LatencyTrace>,
    /// Optional staged read-only trade profiling session.
    read_profile: Option<ReadProfile>,
    /// Compiled local string catalog used before the native startup publication.
    fallback: Catalog,
    /// Owned intake thread handle joined during application shutdown.
    worker: Option<std::thread::JoinHandle<()>>,
}
impl eframe::App for App {
    /// Drain worker messages, update cached publications and view state, draw the main window/dialogs, and enqueue user intents without blocking on backend work.
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        let frame_started = std::time::Instant::now();
        let mut rendered_views = Vec::new();
        let mut stamps = [None; 9];
        while let Ok(update) = self.rx.try_recv() {
            let update = if let Update::Stamped(value, stamp) = update {
                stamps[stamp.kind as usize] = Some(stamp);
                *value
            } else {
                update
            };
            match update {
                Update::Stamped(..) => unreachable!("A transfer wraps one UI update"),
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
                    if self.editor.pending.as_ref() == Some(&id) {
                        self.editor.pending = None;
                        self.editor.error = e.clone();
                    }
                    for pending in [
                        &mut self.inspection.pending,
                        &mut self.contract.pending,
                        &mut self.yield_editor.pending,
                        &mut self.calendar_editor.pending,
                        &mut self.provider_browser.pending,
                        &mut self.health.pending,
                        &mut self.actor_health.pending,
                        &mut self.parameters.pending,
                        &mut self.strategy_catalog.pending,
                    ] {
                        if pending.as_ref() == Some(&id) {
                            *pending = None;
                        }
                    }
                    if self.actor_health.refresh_pending.as_ref() == Some(&id) {
                        self.actor_health.refresh_pending = None;
                    }
                    if self.backup.pending.as_ref() == Some(&id) {
                        self.backup.pending = None;
                        if self.system_open {
                            self.system_load_pending = true;
                        } else {
                            self.system_release_pending = true;
                        }
                    }
                    if self.scheduled.pending.as_ref() == Some(&id) {
                        self.scheduled.pending = None;
                        if self.system_open {
                            self.system_load_pending = true;
                        }
                    }

                    self.trade_workspace.submission_rejected(&id);
                    self.market_data.submission_rejected(&id, &e);
                    self.portfolio.submission_rejected(&id, &e);
                    self.status = e;
                }
                Update::Warning(e) => {
                    if self.runtime_notices.warning(format!("runtime:{e}"),e.clone()) {
                        if let Some(row)=self.runtime_notices.publication.status_rows.last() {
                            self.shell.sidebar_snapshot.status_rows.push(row.clone());
                        }
                    }
                    self.status=e;
                }
                Update::Error(e) => {
                    self.error_dialog = Some(e.clone());
                    self.trade_workspace.transport_failed(&e);
                    self.market_data.transport_failed(&e);
                    self.portfolio.transport_failed(&e);
                    self.editor.error = e.clone();
                    self.editor.pending = None;
                    self.inspection.pending = None;
                    self.contract.pending = None;
                    self.yield_editor.pending = None;
                    self.calendar_editor.pending = None;
                    self.provider_browser.pending = None;
                    self.scheduled.pending = None;
                    self.backup.pending = None;
                    self.health.pending = None;
                    self.actor_health.pending = None;
                    self.parameters.pending = None;
                    self.strategy_catalog.pending = None;
                    self.status = e;
                }
                Update::MarketCharts(charts) => {
                    let mut next = charts;
                    ifm_ui_egui::services::market_history::retain_previous(
                        &self.shell.dashboard.market_charts,
                        &mut next,
                    );
                    self.shell.dashboard.market_charts = next;
                }
                Update::PipelineHealth(health) => {
                    self.shell.dashboard.feed_running = health.running;
                    self.shell.dashboard.feed_health = health.health;
                    self.shell.dashboard.feed_health_reason = health.reason;
                    self.shell.dashboard.options_health = health.options_health;
                    self.shell.dashboard.options_health_reason = health.options_reason;
                }
                Update::MarketOutlook(result) => {
                    match result {
                        Ok(outlook) => {
                            self.shell.dashboard.outlook = outlook
                                .cells
                                .iter()
                                .map(|(key, cell)| (key.clone(), cell.value.clone()))
                                .collect();
                            self.shell.dashboard.outlook_cells = outlook.cells;
                            self.shell.dashboard.outlook_status = outlook.status;
                            self.shell.dashboard.status = outlook.summary;
                        }
                        Err(error) => {
                            // An unavailable snapshot clears indicators rather than
                            // presenting an old value as a current observation.
                            self.shell.dashboard.outlook.clear();
                            self.shell.dashboard.outlook_cells.clear();
                            self.shell.dashboard.outlook_status =
                                format!("Market Outlook unavailable: {error}");
                        }
                    }
                }
                Update::Dashboard(s) => {
                    if !s.title.is_empty() {
                        ctx.send_viewport_cmd(egui::ViewportCommand::Title(s.title.clone()));
                    }
                    self.shell.dashboard = s;
                }
                Update::Sidebar(mut s) => {
                    for row in &self.runtime_notices.publication.status_rows {
                        if row.id.starts_with("current-warning|") {
                            if s.status_rows.iter().any(|existing|existing.id.starts_with("current-warning|") && existing.id>=row.id){continue;}
                            s.status_rows.retain(|existing|!existing.id.starts_with("current-warning|"));
                        }else{s.status_rows.retain(|existing|existing.id!=row.id);}
                        let position=s.status_rows.iter().position(|existing|existing.id.as_bytes().get(4)==Some(&b'-')&&existing.id>row.id).unwrap_or(s.status_rows.len());
                        s.status_rows.insert(position,row.clone());
                    }
                    if let Ok(date)=chrono::NaiveDate::parse_from_str(&s.api_today,"%Y-%m-%d") { ifm_ui_egui::date_picker::set_api_today(ctx,date); }
                    self.shell.sidebar_snapshot = s;
                },
                Update::TradeMonitor(monitor)=>{
                    if self.trade_monitor.as_ref().is_none_or(|old|old.revision<monitor.revision){
                        ifm_ui_egui::state::trade_monitors::Hub::merge(&mut self.trade_snapshot.result,&monitor.result);
                        self.trade_snapshot.invalidate_render_cache();self.trade_monitor=Some(monitor);
                    }
                }
                Update::Trades(mut s) => {
                    if let Some(monitor)=&self.trade_monitor{ifm_ui_egui::state::trade_monitors::Hub::merge(&mut s.result,&monitor.result);s.invalidate_render_cache();}
                    ifm_ui_egui::trade_workspace::TradeWorkspace::replace_publication(
                        &mut self.trade_snapshot,
                        s,
                    )
                }
                Update::Contracts(s) => {
                    if let Err(error) = self.publications.apply(s) {
                        // A local contract-validation failure has no request
                        // subject. Reject this publication and retain diagnostics;
                        // an invalid view update must not interrupt the application.
                        ifm_ui_egui::diagnostics::errors::record("ui.publication_invalid",None,None,&error);
                        self.runtime_notices.warning(format!("publication:{error}"),error.clone());
                        self.status=error;
                    }
                }
                Update::Editor(s) => self.editor_snapshot = s,
                Update::Inspection(s) => self.inspection_snapshot = s,
                Update::Grid(rows) => {
                    self.grid = Some(ifm_ui_egui::virtual_grid::GridView::new(rows))
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
                        self.market_data.open();
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
                        self.portfolio.open();
                    }
                    // WinForms Reference opens one manager; its Selection dropdown owns navigation.
                    if ui.button("Reference").clicked() {
                        self.lookup_tab = true;
                        self.catalog_tab = false;
                        self.parameter_tab = false;
                        self.contract_tab = false;
                        self.inspection_tab = false;
                        self.yield_tab = false;
                        self.calendar_tab = false;
                        self.provider_tab = false;
                        self.scheduled_tab = false;
                        self.health_tab = false;
                        self.actor_tab = false;
                        self.grid_tab = false;
                    }
                    if ui.button("System").clicked() {
                        self.system_open=true;self.system_load_pending=true;

                    }
                    // The WinForms toolbar separator sits above the Operations splitter.
                    let left_width = egui::containers::panel::PanelState::load(
                        ctx,
                        egui::Id::new("operations-pane"),
                    )
                    .map(|panel| panel.rect.width())
                    .unwrap_or(ctx.screen_rect().width() * 0.22);
                    ui.add_space((left_width - ui.cursor().left()).max(0.0));
                    ui.separator();
                    if ifm_ui_egui::presentation::feed_command_button(ui, &self.shell.sidebar_snapshot.feed_control) {
                        if self.tx.try_send(ifm_ui_egui::sidebar::FEED_TOGGLE_ACTION).is_err() {
                            self.status = "Feed action queue is unavailable; command was not submitted.".into();
                        }
                    }
                    let health = ifm_ui_egui::presentation::feed_health_text(
                        &self.shell.dashboard.feed_health,
                    );
                    let (health_bg, health_fg) = ifm_ui_egui::presentation::feed_health_palette(
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
                    }
                    let options=&self.shell.dashboard.options_health;
                    if ui.add(egui::Button::new(egui::RichText::new("Options Health").color(ifm_ui_egui::operations::operations_health::status_color(options))))
                        .on_hover_text(&self.shell.dashboard.options_health_reason).clicked() {
                        self.health_tab=true; self.actor_tab=false; self.health.datasets=true;
                    }
                    if ui.button("Operations health").clicked() {
                        self.health_tab = true;
                        self.actor_tab = false;
                    }
                    if ui.button("Actor Health").clicked() {
                        self.actor_tab = true;
                        self.health_tab = false;
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
                ui.add(ifm_ui_egui::presentation::label_widget(message).truncate())
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
                    let id =
                        serde_json::from_str::<serde_json::Value>(&payload).unwrap()["requestId"]
                            .as_str()
                            .unwrap()
                            .to_owned();
                    self.trade_workspace.submission_rejected(&id);
                    self.status = e.to_string();
                }
            }
        }
        if self.trade_workspace.loaded_bounds.is_none()
            || self.lookup_tab
            || self.catalog_tab
            || self.parameter_tab
        {
            if let Some(selected) =
                ctx.data_mut(|data| data.remove_temp::<usize>(egui::Id::new("reference-selection")))
            {
                self.lookup_tab = selected == 0;
                self.catalog_tab = selected == 1;
                self.parameter_tab = selected == 2;
            }
            if ctx
                .data_mut(|data| data.remove_temp::<bool>(egui::Id::new("reference-closed")))
                .unwrap_or(false)
            {
                self.lookup_tab = false;
                self.catalog_tab = false;
                self.parameter_tab = false;
            }
            if self.catalog_tab {
                rendered_views.push("strategy-catalog");
                if let Some(payload) = self.strategy_catalog.draw_reference(
                    ctx,
                    self.publications.get("strategy-catalog"),
                    catalog,
                    self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
                ) {
                    if self.editor_tx.try_send(payload).is_err() {
                        self.strategy_catalog.pending = None;
                        self.status = catalog.text(strings::QUEUE_FULL).into();
                    }
                }
            } else if self.parameter_tab {
                rendered_views.push("parameter-sets");
                if let Some(payload) = self.parameters.draw_reference(
                    ctx,
                    self.publications.get("parameter-sets"),
                    catalog,
                    self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
                ) {
                    if self.editor_tx.try_send(payload).is_err() {
                        self.parameters.pending = None;
                        self.strategy_catalog.pending = None;
                        self.status = catalog.text(strings::QUEUE_FULL).into();
                    }
                }
            } else if self.scheduled_tab {
                rendered_views.push("scheduled-tasks");
                if let Some(payload) = self.scheduled.draw(
                    ctx,
                    self.publications.get("scheduled-tasks"),
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
                rendered_views.push("provider-definitions");
                if let Some(payload) = self.provider_browser.draw(
                    ctx,
                    self.publications.get("provider-definitions"),
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
                rendered_views.push("economic-calendar");
                if let Some(payload) = self.calendar_editor.draw(
                    ctx,
                    self.publications.get("economic-calendar"),
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
                rendered_views.push("yield-curve");
                if let Some(payload) = self.yield_editor.draw(
                    ctx,
                    self.publications.get("yield-curve"),
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
                rendered_views.push(if self.contract.option {
                    "option-contract"
                } else {
                    "futures-contract"
                });
                if let Some(payload) = self.contract.draw(
                    ctx,
                    self.publications.get(if self.contract.option {
                        "option-contract"
                    } else {
                        "futures-contract"
                    }),
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
                if let Some(payload) = self.editor.draw_reference(
                    ctx,
                    &self.editor_snapshot,
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
        // Health dialogs render independently of the selected workspace.
        // Their close state never closes a trade or stops its subscriptions.
        if self.actor_tab {
            rendered_views.push("actor-health");
            if let Some(payload)=self.actor_health.draw_dialog(ctx,self.publications.get("actor-health"),catalog,self.snapshot.as_ref().is_some_and(|s|s.mode=="Live"),&mut self.actor_tab){
                if self.editor_tx.try_send(payload).is_err(){self.actor_health.pending=None;self.status=catalog.text(strings::QUEUE_FULL).into();}
            }
        }
        if self.health_tab {
            rendered_views.push("operations-health");
            if let Some(payload)=self.health.draw_dialog(ctx,self.publications.get("operations-health"),catalog,self.snapshot.as_ref().is_some_and(|s|s.mode=="Live"),&mut self.health_tab){
                if self.editor_tx.try_send(payload).is_err(){self.health.pending=None;self.status=catalog.text(strings::QUEUE_FULL).into();}
            }
        }
        if self.market_data.open {
            rendered_views.push(match self.market_data.selection {
                0 => "futures-contract",
                1 => "option-contract",
                _ => "yield-curve",
            });
        }
        if let Some(payload) = self.market_data.draw(
            ctx,
            self.publications.get(match self.market_data.selection {
                0 => "futures-contract",
                1 => "option-contract",
                _ => "yield-curve",
            }),
            self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
        ) {
            if self.editor_tx.try_send(payload).is_err() {
                self.market_data.reject_submission();
                self.status = catalog.text(strings::QUEUE_FULL).into();
            }
        }
        if self.portfolio.open {
            rendered_views.push("portfolio");
        }
        if let Some(payload) = self.portfolio.draw(
            ctx,
            self.publications.get("portfolio"),
            self.snapshot.as_ref().is_some_and(|s| s.mode == "Live"),
        ) {
            if self.editor_tx.try_send(payload).is_err() {
                self.portfolio.reject_submission();
                self.status = catalog.text(strings::QUEUE_FULL).into();
            }
        }
        if let Some(profile) = &mut self.read_profile {
            if profile.started.elapsed().as_secs() >= profile.seconds {
                ctx.send_viewport_cmd(egui::ViewportCommand::Close);
            } else if let Some(request) = self
                .trade_workspace
                .profile_read(profile.stage, &self.trade_snapshot)
            {
                if self.editor_tx.try_send(request).is_ok() {
                    profile.stage += 1;
                }
            }
            ctx.request_repaint_after(Duration::from_millis(20));
        }
        if let Some(section) =
            ctx.data_mut(|d| d.remove_temp::<usize>(egui::Id::new("system-admin-selection")))
        {
            self.system_section = section;
            self.system_load_pending = true;
        }
        if ctx
            .data_mut(|d| d.remove_temp::<bool>(egui::Id::new("system-admin-closed")))
            .unwrap_or(false)
        {
            self.system_open = false;
            self.system_release_pending = true;
            self.system_load_pending = false;
            self.system_section = 0;
            self.backup = Default::default();
            self.scheduled = Default::default();
        }
        if self.system_release_pending && !self.publications.get("database-backup").busy {
            let payload = self
                .backup
                .request("backup-close", self.publications.get("database-backup"));
            if self.editor_tx.try_send(payload).is_ok() {
                self.system_release_pending = false;
            } else {
                self.backup.pending = None;
            }
        }
        if self.system_open
            && self.system_load_pending
            && !self.system_release_pending
            && !self
                .publications
                .get(if self.system_section == 0 {
                    "database-backup"
                } else {
                    "scheduled-tasks"
                })
                .busy
        {
            if self.system_section == 0 {
                self.backup.reopen();
                self.system_load_pending = false;
            } else if self.scheduled.pending.is_none() {
                let payload = self
                    .scheduled
                    .request("load", self.publications.get("scheduled-tasks"));
                if self.editor_tx.try_send(payload).is_ok() {
                    self.system_load_pending = false;
                } else {
                    self.scheduled.pending = None;
                }
            }
        }
        if self.system_open {
            rendered_views.push(if self.system_section == 0 {
                "database-backup"
            } else {
                "scheduled-tasks"
            });
            let live = self.snapshot.as_ref().is_some_and(|s| s.mode == "Live");
            let payload = if self.system_section == 0 {
                self.backup
                    .draw(ctx, self.publications.get("database-backup"), live)
            } else {
                self.scheduled.draw_system(
                    ctx,
                    self.publications.get("scheduled-tasks"),
                    catalog,
                    live,
                )
            };
            if let Some(payload) = payload {
                if self.editor_tx.try_send(payload).is_err() {
                    self.backup.pending = None;
                    self.scheduled.pending = None;
                    self.status = catalog.text(strings::QUEUE_FULL).into();
                }
            }
        }
        // Multiple windows can consume one view in a frame. Clear its receipts
        // only after every renderer had the opportunity to match its pending ID.
        for view in rendered_views {
            self.publications.acknowledge_rendered(view);
        }
        if !self.trade_workspace.is_visible() {
            stamps[8] = None;
        }
        if let Some(trace) = &self.latency {
            trace.paint(ctx, stamps);
        }
        if let Some(message) = self.error_dialog.clone() {
            egui::Modal::new(egui::Id::new("standalone-runtime-error")).show(ctx, |ui| {
                if ifm_ui_egui::status_dialog::body(ui,&message,ifm_ui_egui::status_dialog::Severity::Error).acknowledged {
                    self.error_dialog = None;
                }
            });
        }
        // Retain only two pages after resize; console display has no ACK or replay.
        let capacity=ifm_ui_egui::console_capacity::current();
        let rows=&mut self.shell.sidebar_snapshot.status_rows;
        ifm_ui_egui::console_capacity::trim_to(rows,capacity);
        if self.frame_times.len() < 10000 {
            self.frame_times
                .push(frame_started.elapsed().as_secs_f64() * 1000.0);
        }
    }
}
impl Drop for App {
    /// Signal worker shutdown, join the intake thread, and finish any optional profiling output.
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

        if let Some(trace) = &self.latency {
            trace.finish();
        }
        self.stopping
            .store(true, std::sync::atomic::Ordering::Release);
        let _ = self.tx.try_send(u32::MAX);
        if let Some(worker) = self.worker.take() {
            let _ = worker.join();
        }
    }
}
/// Create bounded channels and the managed-intake worker, configure the display GPU/window, and run eframe. Returns startup/render-loop errors through eframe.
fn main() -> eframe::Result {
    // Retain the adapted calendar's MIT notice in the one-file executable.
    let _=std::hint::black_box(ifm_ui_egui::date_picker::LICENSE);
    let (updates, rx) = mpsc::sync_channel(4);
    let (tx, actions) = mpsc::sync_channel(32);
    let (editor_tx, editor_actions) = mpsc::sync_channel::<String>(32);
    let maximized = std::env::var("IFM_UI_START_MAXIMIZED")
        .map(|value| value.eq_ignore_ascii_case("true") || value == "1")
        .unwrap_or_else(|_| std::env::var("IFM_RUST_UI_MODE").is_ok_and(|value| value == "live"));
    // A forced initial inner size can override Windows' maximized placement at startup.
    let mut viewport = egui::ViewportBuilder::default().with_maximized(maximized);
    if !maximized {
        viewport = viewport.with_inner_size(ifm_ui_egui::presentation::MAIN_CLIENT_SIZE);
    }
    let options = eframe::NativeOptions {
        viewport,
        renderer: eframe::Renderer::Wgpu,
        ..Default::default()
    };
    eframe::run_native(
        ifm_ui_egui::WINDOW_TITLE,
        options,
        Box::new(move |cc| {
            ifm_ui_egui::apply_winforms_typography(&cc.egui_ctx)?;
            let ctx = cc.egui_ctx.clone();
            let stopping = std::sync::Arc::new(std::sync::atomic::AtomicBool::new(false));
            let worker_stopping = stopping.clone();
            let worker = std::thread::spawn(move || {
                runtime::run(updates, actions, editor_actions, ctx, worker_stopping);
            });
            Ok(Box::new(App {
                stopping,
                error_dialog: None,
                runtime_notices: Default::default(),
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
                system_open: false,
                system_release_pending: false,
                system_load_pending: false,
                system_section: 0,
                backup: Default::default(),
                scheduled: Default::default(),
                provider_tab: false,
                provider_browser: Default::default(),
                calendar_tab: false,
                calendar_editor: Default::default(),
                trade_workspace: Default::default(),
                trade_snapshot: Default::default(),
                trade_monitor:None,
                yield_tab: false,
                yield_editor: Default::default(),
                market_data: Default::default(),
                portfolio: Default::default(),
                contract_tab: false,
                contract: Default::default(),
                publications: Default::default(),
                lookup_tab: false,
                inspection_tab: false,
                inspection: Default::default(),
                inspection_snapshot: Default::default(),
                catalog: None,
                snapshot: None,
                view: DetailsView::default(),
                shell: ifm_ui_egui::shell::MainShell::default(),
                strategy: ifm_ui_egui::strategy_workspace::StrategyWorkspace::for_shell(),
                grid: None,
                grid_tab: false,
                frame_times: Vec::new(),
                latency: ifm_ui_egui::latency::LatencyTrace::from_env(),
                read_profile: if std::env::var("DOTNET_ENVIRONMENT")
                    .is_ok_and(|v| v == "Development")
                    && std::env::var("IFM_RUST_UI_MODE").is_ok_and(|v| v == "live")
                {
                    std::env::var("IFM_UI_PROFILE_TRADE_SECONDS")
                        .ok()
                        .and_then(|v| v.parse::<u64>().ok())
                        .map(|seconds| ReadProfile {
                            started: std::time::Instant::now(),
                            seconds: seconds.clamp(10, 300),
                            stage: 0,
                        })
                } else {
                    None
                },
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
