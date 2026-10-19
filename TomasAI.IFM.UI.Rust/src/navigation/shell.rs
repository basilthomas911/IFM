//! Three-pane application shell and published dashboard model.
//!
//! The side panes start at 22 percent of available width and remain user-resizable.
//! Viewport changes reset remembered pane sizes. The left pane hosts strategy
//! observations; the right pane hosts outlook, charts, calendar, and status. The
//! center workspace is composed by the executable. Optional shell metrics record
//! geometry for tests; they are not market or portfolio data.

use crate::presentation::TradingLabelUi as _;
use crate::{Catalog, DetailsView, Snapshot, strategy_workspace::StrategyWorkspace};
use eframe::egui;
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

/// Managed main-screen dashboard projection for feed health, outlook, calendar labels, and market charts.
#[derive(Clone, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Dashboard {
    /// Published form/window/section title.
    pub title: String,
    /// Optional backend report that the market feed is running.
    #[serde(default)]
    pub feed_running: Option<bool>,
    /// Published feed-health state used for the toolbar indicator.
    pub feed_health: String,
    /// Backend explanation of the current feed-health state.
    pub feed_health_reason: String,
    /// Display-ready status supplied by the managed projection or current view state.
    pub status: String,
    /// Published market-outlook display fields.
    pub outlook: BTreeMap<String, String>,
    /// Published semantic caption/value cells including color roles.
    pub outlook_cells: BTreeMap<String, crate::market_outlook::OutlookCell>,
    /// Backend market-outlook status text.
    pub outlook_status: String,
    /// Published calendar labels exposed by the dashboard.
    pub calendars: Vec<String>,
    /// Published charts indexed by market symbol/tab key.
    pub market_charts: BTreeMap<String, crate::market_charts::ChartSnapshot>,
}
/// Persistent three-pane shell state, selected observations/timeframe, and sidebar/dashboard publications.
#[derive(Default)]
pub struct MainShell {
    /// Current main workspace selection key.
    pub workspace: String,
    /// Latest managed dashboard projection consumed by the market/outlook panes.
    pub dashboard: Dashboard,
    /// Selected Strategy/Latency/Traffic/Errors/Saturation tab index.
    pub observation_tab: usize,
    /// Observation timeframe index: Daily (0), Weekly (1), or Monthly (2).
    pub time_frame: usize,
    /// Selected market chart tab index.
    pub market_tab: usize,
    /// Latest economic-calendar/status publication.
    pub sidebar_snapshot: crate::sidebar::SidebarPublication,
    /// Persistent calendar/status view state.
    pub sidebar_view: crate::sidebar::SidebarView,
    /// Last time optional shell geometry was written; throttles diagnostic I/O.
    last_metrics: Option<std::time::Instant>,
    /// Previous viewport dimensions used to reset remembered pane geometry on resize.
    viewport_size: Option<egui::Vec2>,
}
/// Serializable layout/interaction diagnostics for desktop verification; coordinate arrays use logical points.
#[derive(Serialize)]
pub struct ShellGeometry {
    /// Most recent status-console message captured for diagnostics.
    pub latest_status: String,
    /// Number of published status-console rows recorded for diagnostics.
    pub status_rows: usize,
    /// Count of status rows actually drawn in the diagnostic frame.
    pub status_drawn_rows: usize,
    /// Whether the calendar publication is busy.
    pub calendar_busy: bool,
    /// Number of published calendar rows recorded for diagnostics.
    pub calendar_rows: usize,
    /// Selected calendar period recorded for layout verification.
    pub calendar_period: String,
    /// Selected calendar country recorded for layout verification.
    pub calendar_country: String,
    /// Stable selected calendar event identity recorded for diagnostics.
    pub calendar_selected_id: String,
    /// Selected ES/ES-BB/VX chart tab index recorded for diagnostics.
    pub market_tab: usize,
    /// Left edge or left-pane bounds in the enclosing coordinate system.
    pub left: [f32; 4],
    /// Right edge or right-pane bounds in the enclosing coordinate system.
    pub right: [f32; 4],
    /// Physical pixels per egui logical point for the current viewport.
    pub pixels_per_point: f32,
    /// Viewport dimensions in logical points.
    pub viewport: [f32; 2],
    /// Optional pointer position in logical points for interaction diagnostics.
    pub pointer: Option<[f32; 2]>,
    /// Whether the primary pointer button was held during the diagnostic frame.
    pub primary_down: bool,
    /// Whether the left-pane resize interaction was active.
    pub left_dragging: bool,
}
/// Convert a rectangle to the left/top/right/bottom array used in shell diagnostics.
fn bounds(rect: egui::Rect) -> [f32; 4] {
    [rect.left(), rect.top(), rect.width(), rect.height()]
}
impl MainShell {
    /// Select the requested operational screen while clearing the other system-view visibility flags.
    pub fn select_system(
        &mut self,
        section: u8,
        scheduled: &mut bool,
        actor: &mut bool,
        health: &mut bool,
    ) {
        *scheduled = section == 0;
        *actor = section == 1;
        *health = section == 2;
    }
    /// Lay out the strategy and market side panes, render current publications, and return a numeric user action when a control changes.
    pub fn draw_panes(
        &mut self,
        ctx: &egui::Context,
        snapshot: Option<&Snapshot>,
        catalog: &Catalog,
        strategy: &mut StrategyWorkspace,
        details: &mut DetailsView,
    ) -> Option<u32> {
        let size = ctx.screen_rect().size();
        if self.viewport_size.is_some_and(|previous| previous != size) {
            ctx.data_mut(|data| {
                for name in ["operations-pane", "market-pane", "strategy_graph"] {
                    data.remove::<egui::containers::panel::PanelState>(egui::Id::new(name));
                }
            });
        }
        self.viewport_size = Some(size);
        let width = ctx.available_rect().width();
        let mut action = None;
        let left = egui::SidePanel::left("operations-pane")
            .resizable(true)
            .default_width(width * 0.22)
            .min_width(200.0)
            .show(ctx, |ui| {
                ui.set_min_width(ui.max_rect().width());
                ui.horizontal(|ui| {
                    ui.spacing_mut().item_spacing.x = 2.0;
                    for (index, label) in ["Strategy", "Latency", "Traffic", "Errors", "Saturation"]
                        .iter()
                        .enumerate()
                    {
                        crate::presentation::tab_value(ui,&mut self.observation_tab, index, *label);
                    }
                });
                if self.observation_tab != 0 {
                    let message = [
                        "",
                        "Latency operations are not implemented yet.",
                        "Traffic operations are not implemented yet.",
                        "Error operations are not implemented yet.",
                        "Saturation operations are not implemented yet.",
                    ][self.observation_tab];
                    ui.centered_and_justified(|ui| {
                        ui.trading_label(message);
                    });
                    return;
                }
                ui.horizontal(|ui| {
                    let controls_width = 180.0;
                    let heading_width = (ui.available_width() - controls_width).max(40.0);
                    let contract = snapshot
                        .map(|s| s.contract_id.as_str())
                        .filter(|s| !s.is_empty())
                        .unwrap_or("?");
                    ui.add_sized(
                        [heading_width, 24.0],
                        crate::presentation::label_widget(
                            egui::RichText::new(format!(
                                "Futures ITI - {contract} | Intrinsic Time"
                            ))
                            .color(egui::Color32::from_rgb(50, 205, 50)),
                        )
                        .halign(egui::Align::Min)
                        .truncate(),
                    );
                    ui.trading_caption("Time Frame:");
                    let previous = self.time_frame;
                    egui::ComboBox::from_id_salt("iti-time-frame")
                        .width(72.0)
                        .selected_text(["Daily", "Weekly", "Monthly"][self.time_frame])
                        .show_ui(ui, |ui| {
                            for (index, label) in ["Daily", "Weekly", "Monthly"].iter().enumerate()
                            {
                                ui.selectable_value(&mut self.time_frame, index, *label);
                            }
                        });
                    if previous != self.time_frame {
                        action = Some(10 + self.time_frame as u32);
                    }
                });
                if let Some(snapshot) = snapshot {
                    if let Some(selected) = strategy.draw_inside(ui, snapshot, catalog, details) {
                        action = Some(selected);
                    }
                } else {
                    ui.trading_caption("Loading strategy observations...");
                }
            })
            .response
            .rect;
        let right = egui::SidePanel::right("market-pane")
            .resizable(true)
            .default_width(width * 0.22)
            .min_width(200.0)
            .show(ctx, |ui| {
                ui.set_min_width(ui.max_rect().width());
                egui::TopBottomPanel::top("market-outlook")
                    .frame(egui::Frame::NONE)
                    .exact_height(crate::market_outlook::HEIGHT)
                    .show_inside(ui, |ui| {
                        crate::market_outlook::draw(ui, &self.dashboard);
                    });
                egui::TopBottomPanel::top("market-data-pane")
                    .frame(egui::Frame::NONE.inner_margin(egui::Margin {
                        left: 6,
                        right: 6,
                        top: 0,
                        bottom: 6,
                    }))
                    .exact_height(196.0)
                    .show_inside(ui, |ui| {
                        crate::market_charts::draw(
                            ui,
                            &self.dashboard.market_charts,
                            &mut self.market_tab,
                        );
                    });
                let calendar_height = (ui.available_height() - 80.0).clamp(120.0, 245.0);
                egui::TopBottomPanel::top("economic-calendar-pane")
                    .exact_height(calendar_height)
                    .show_inside(ui, |ui| {
                        if let Some(selected) =
                            self.sidebar_view.calendar(ui, &self.sidebar_snapshot)
                        {
                            action = Some(selected);
                        }
                    });
                egui::CentralPanel::default().show_inside(ui, |ui| {
                    self.sidebar_view.status(ui, &self.sidebar_snapshot);
                });
            })
            .response
            .rect;
        if let Ok(path) = std::env::var("IFM_RUST_SHELL_METRICS") {
            if self
                .last_metrics
                .is_none_or(|time| time.elapsed() > std::time::Duration::from_millis(250))
            {
                let size = ctx.screen_rect().size();
                let geometry = ShellGeometry {
                    latest_status: self.sidebar_snapshot.latest_status().unwrap_or("").into(),
                    status_rows: self.sidebar_snapshot.status_rows.len(),
                    status_drawn_rows: self.sidebar_view.drawn_status_rows,
                    calendar_busy: self.sidebar_snapshot.busy,
                    calendar_rows: self.sidebar_snapshot.calendar_rows.len(),
                    calendar_period: self.sidebar_snapshot.period.clone(),
                    calendar_country: self.sidebar_snapshot.country.clone(),
                    calendar_selected_id: self.sidebar_snapshot.selected_id.clone(),
                    market_tab: self.market_tab,
                    left: bounds(left),
                    right: bounds(right),
                    pixels_per_point: ctx.pixels_per_point(),
                    viewport: [size.x, size.y],
                    pointer: ctx.input(|input| input.pointer.hover_pos().map(|p| [p.x, p.y])),
                    primary_down: ctx.input(|input| input.pointer.primary_down()),
                    left_dragging: ctx
                        .read_response(egui::Id::new("operations-pane").with("__resize"))
                        .is_some_and(|r| r.dragged()),
                };
                if let Ok(bytes) = serde_json::to_vec(&geometry) {
                    let _ = std::fs::write(path, bytes);
                }
                self.last_metrics = Some(std::time::Instant::now());
            }
        }
        action
    }
}
