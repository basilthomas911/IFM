use crate::presentation::TradingLabelUi as _;
use crate::{Catalog, DetailsView, Snapshot, strategy_workspace::StrategyWorkspace};
use eframe::egui;
use serde::{Deserialize, Serialize};
use std::collections::BTreeMap;

#[derive(Clone, Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
pub struct Dashboard {
    pub title: String,
    #[serde(default)]
    pub feed_running: Option<bool>,
    pub feed_health: String,
    pub feed_health_reason: String,
    pub status: String,
    pub outlook: BTreeMap<String, String>,
    pub outlook_cells: BTreeMap<String, crate::market_outlook::OutlookCell>,
    pub outlook_status: String,
    pub calendars: Vec<String>,
    pub market_charts: BTreeMap<String, crate::market_charts::ChartSnapshot>,
}
#[derive(Default)]
pub struct MainShell {
    pub workspace: String,
    pub dashboard: Dashboard,
    pub observation_tab: usize,
    pub time_frame: usize,
    pub market_tab: usize,
    pub sidebar_snapshot: crate::sidebar::SidebarPublication,
    pub sidebar_view: crate::sidebar::SidebarView,
    last_metrics: Option<std::time::Instant>,
    viewport_size: Option<egui::Vec2>,
}
#[derive(Serialize)]
pub struct ShellGeometry {
    pub latest_status: String,
    pub status_rows: usize,
    pub status_drawn_rows: usize,
    pub calendar_busy: bool,
    pub calendar_rows: usize,
    pub calendar_period: String,
    pub calendar_country: String,
    pub calendar_selected_id: String,
    pub market_tab: usize,
    pub left: [f32; 4],
    pub right: [f32; 4],
    pub pixels_per_point: f32,
    pub viewport: [f32; 2],
    pub pointer: Option<[f32; 2]>,
    pub primary_down: bool,
    pub left_dragging: bool,
}
fn bounds(rect: egui::Rect) -> [f32; 4] {
    [rect.left(), rect.top(), rect.width(), rect.height()]
}
impl MainShell {
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
                ui.separator();
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
                    ui.trading_label("Time Frame:");
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
                    ui.trading_label("Loading strategy observations...");
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
