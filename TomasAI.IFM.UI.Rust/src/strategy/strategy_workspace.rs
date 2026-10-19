//! ITI signal chart and Strategy Updates/Details/Summary tabs.
//!
//! Chart points are rebuilt only when epoch or generation changes. Timeframe changes
//! reset prior zoom/pan state. Virtualized workflow rows emit bounded history-page
//! actions rather than rendering every row. Pipeline indicators stop at the first
//! failed actor. Prices, actor states, and decision summaries are backend projections.

use crate::control_parity::BorderedTable as _;
use crate::presentation::TradingLabelUi as _;
use crate::{Catalog, DetailsView, Snapshot, strings};
use eframe::egui;
use serde::Deserialize;
/// One backend workflow execution row with its actor pipeline and selection action.
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WorkflowRow {
    /// Stable identity used to correlate selection, rows, or commands.
    pub id: String,
    /// Numeric bridge action used to open this workflow row.
    pub action: u32,
    /// Stable signal identity linking workflow rows to graph points.
    #[serde(default)]
    pub trigger_identity: String,
    /// Formatted workflow occurrence time.
    pub occurred_on: String,
    /// Published workflow trigger/signal label.
    pub signal: String,
    /// Published market trend label.
    pub trend: String,
    /// Published futures price formatted for the workflow grid.
    pub futures_price: String,
    /// Published workflow result label.
    pub outcome: String,
    /// Published terminal workflow state.
    pub end_state: String,
    /// Published ordered actor-stage results for the pipeline indicator.
    pub actors: Vec<WorkflowActor>,
}
/// Published actor-stage result used to paint the pipeline indicator.
#[derive(Clone, Debug, serde::Deserialize)]
pub struct WorkflowActor {
    /// Published or edited display name.
    pub name: String,
    /// Published or edited display value; formatting/validation belongs to its managed field contract.
    pub value: String,
    /// Backend lifecycle/execution state or correlated outcome state.
    #[serde(default)]
    pub state: String,
}
/// Published ITI point with UTC time, optional market-time coordinate, direction, and tooltip.
#[derive(Clone, Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GraphPoint {
    /// Stable published identity used to link chart signals and workflow rows.
    pub identity: String,
    /// UTC Unix-seconds coordinate of the published ITI signal.
    pub utc_seconds: f64,
    /// Optional backend market-time coordinate used instead of UTC on the ITI chart.
    #[serde(default)]
    pub market_seconds: Option<f64>,
    /// Published ITI signal price used as the chart y coordinate.
    pub price: f64,
    /// Published signal direction selecting the chart marker/color.
    pub direction: String,
    /// Backend-generated explanatory text for the chart point.
    pub tooltip: String,
}
/// Published workflow-summary section and its backend state/explanation.
#[derive(Clone, Debug, Deserialize)]
pub struct SummarySection {
    /// Published form/window/section title.
    pub title: String,
    /// Backend lifecycle/execution state or correlated outcome state.
    pub state: String,
    /// Published workflow-summary sections or summary text.
    pub summary: String,
    /// Detailed published workflow summary/evidence text.
    pub content: String,
}
/// Selected Strategy Updates, Details, or Summary page.
#[derive(Default, PartialEq)]
pub enum StrategyTab {
    /// Workflow history grid and pipeline-stage indicators.
    Updates,
    /// Selected workflow node tree and read-only properties.
    #[default]
    Details,
    /// Published summary sections for the selected workflow.
    Summary,
}
/// Strategy tab selection, graph cache, and deduplicated virtual history-page request state.
#[derive(Default)]
pub struct StrategyWorkspace {
    /// Selected strategy/trading page index or enum.
    pub tab: StrategyTab,
    /// Count of rows actually drawn in the most recent frame, used by virtualization checks.
    pub drawn_rows: usize,
    /// Last workflow/offset/page tuple requested, used to suppress duplicate history loads.
    requested_history_page: Option<(String, usize, usize)>,
    /// Epoch associated with the cached strategy chart points.
    cached_epoch: String,
    /// Generation associated with the cached strategy chart points.
    cached_generation: u64,
    /// Timeframe associated with current chart zoom/cache; a switch resets plot bounds.
    graph_time_frame: String,
    /// Cached ITI x/y points rebuilt only when the publication changes.
    prices: Vec<[f64; 2]>,
    /// Cached upward ITI signal marker points.
    up: Vec<[f64; 2]>,
    /// Cached downward ITI signal marker points.
    down: Vec<[f64; 2]>,
    /// Cached non-directional ITI event marker points.
    other: Vec<[f64; 2]>,
}
impl StrategyWorkspace {
    /// Create the strategy workspace with Strategy Updates initially selected for the main shell.
    pub fn for_shell() -> Self {
        Self {
            tab: StrategyTab::Updates,
            ..Self::default()
        }
    }
    /// Render the ITI chart and workflow tabs as a standalone central panel; return a numeric selection/history action.
    pub fn draw(
        &mut self,
        ctx: &egui::Context,
        snapshot: &Snapshot,
        catalog: &Catalog,
        details: &mut DetailsView,
    ) -> Option<u32> {
        let mut result = None;
        egui::CentralPanel::default()
            .frame(egui::Frame::NONE)
            .show(ctx, |ui| {
                result = self.draw_inside(ui, snapshot, catalog, details)
            });
        result
    }
    /// Update the graph cache for new epoch/generation, draw the chart and virtual workflow tabs, and return a selection/history action without backend I/O.
    pub fn draw_inside(
        &mut self,
        ui: &mut egui::Ui,
        snapshot: &Snapshot,
        catalog: &Catalog,
        details: &mut DetailsView,
    ) -> Option<u32> {
        let mut action = None;
        if self.cached_epoch != snapshot.epoch || self.cached_generation != snapshot.generation {
            self.cached_epoch = snapshot.epoch.clone();
            self.cached_generation = snapshot.generation;
            self.prices.clear();
            self.up.clear();
            self.down.clear();
            self.other.clear();
            for point in &snapshot.signals {
                if !point.utc_seconds.is_finite() || !point.price.is_finite() {
                    continue;
                }
                let xy = [
                    point.market_seconds.unwrap_or(point.utc_seconds),
                    point.price,
                ];
                self.prices.push(xy);
                match point.direction.as_str() {
                    "Up" => self.up.push(xy),
                    "Down" => self.down.push(xy),
                    _ => self.other.push(xy),
                }
            }
        }
        egui::TopBottomPanel::top("strategy_graph")
            .show_separator_line(false)
            .resizable(true)
            .default_height((ui.available_height() / 3.0).max(100.0))
            .min_height(80.0)
            .show_inside(ui, |ui| {
                ui.set_min_height(ui.max_rect().height());
                if self.prices.is_empty() {
                    ui.trading_caption(catalog.text(strings::NO_SIGNALS));
                }
                {
                    let mut plot = egui_plot::Plot::new("iti_price")
                        .x_axis_label("Market Time (ET)")
                        .y_axis_label("ITI Signal Price")
                        .y_axis_formatter(|mark, _| format!("{:.2}", mark.value))
                        .y_grid_spacer(|input| {
                            let (min, max) = input.bounds;
                            let range = max - min;
                            if !range.is_finite() || range <= 0.0 {
                                return Vec::new();
                            }
                            // Aim for six intervals, keeping labels at least 32 points apart.
                            let target = (range / 6.0).max(input.base_step_size * 4.0);
                            let magnitude = 10.0_f64.powf(target.log10().floor());
                            let factor = [1.0, 2.0, 2.5, 5.0, 10.0]
                                .into_iter()
                                .find(|factor| factor * magnitude >= target)
                                .unwrap_or(10.0);
                            let step = factor * magnitude;
                            let first = (min / step).ceil() * step;
                            (0..=8)
                                .map(|index| first + index as f64 * step)
                                .take_while(|value| *value <= max)
                                .map(|value| egui_plot::GridMark {
                                    value,
                                    step_size: step,
                                })
                                .collect()
                        })
                        .label_formatter(|name, point| {
                            snapshot
                                .signals
                                .iter()
                                .find(|s| {
                                    (s.market_seconds.unwrap_or(s.utc_seconds) - point.x).abs()
                                        < 0.001
                                        && (s.price - point.y).abs() < 0.0001
                                })
                                .map(|s| s.tooltip.clone())
                                .unwrap_or_else(|| format!("{name}: {:.2}", point.y))
                        })
                        .x_axis_formatter(|mark, _| {
                            chrono::DateTime::from_timestamp(mark.value as i64, 0)
                                .map(|date| {
                                    date.format(match snapshot.time_frame.as_str() {
                                        "Weekly" => "%a %d-%b\n%-I:%M %p",
                                        "Monthly" => "%d-%b",
                                        _ => "%-I:%M %p",
                                    })
                                    .to_string()
                                })
                                .unwrap_or_default()
                        })
                        .height((ui.available_height() - 44.0).max(40.0));
                    // A timeframe switch must discard the previous timeframe's zoom/pan.
                    // Ordinary live updates preserve the user's current viewport.
                    if self.graph_time_frame != snapshot.time_frame {
                        self.graph_time_frame.clone_from(&snapshot.time_frame);
                        plot = plot.reset();
                    }
                    if snapshot.graph_start_utc < snapshot.graph_end_utc {
                        plot = plot
                            .include_x(
                                snapshot
                                    .graph_start_market
                                    .unwrap_or(snapshot.graph_start_utc),
                            )
                            .include_x(snapshot.graph_end_market.unwrap_or(snapshot.graph_end_utc));
                    }
                    let graph = plot.show(ui, |plot| {
                        if let Some(selected) = snapshot
                            .rows
                            .iter()
                            .find(|row| row.id == snapshot.workflow_id)
                            .and_then(|row| {
                                snapshot
                                    .signals
                                    .iter()
                                    .find(|signal| signal.identity == row.trigger_identity)
                            })
                        {
                            plot.points(
                                egui_plot::Points::new(
                                    catalog.text(strings::SELECTED),
                                    vec![[
                                        selected.market_seconds.unwrap_or(selected.utc_seconds),
                                        selected.price,
                                    ]],
                                )
                                .color(egui::Color32::WHITE)
                                .radius(7.0_f32),
                            );
                        }
                        plot.line(
                            egui_plot::Line::new(catalog.text(strings::PRICE), self.prices.clone())
                                .color(egui::Color32::YELLOW)
                                .width(2.0_f32),
                        );
                        for (key, points, color) in [
                            (
                                strings::UP_EVENT,
                                &self.up,
                                egui::Color32::from_rgb(50, 205, 50),
                            ),
                            (strings::DOWN_EVENT, &self.down, egui::Color32::RED),
                            (
                                strings::OTHER_EVENT,
                                &self.other,
                                egui::Color32::from_rgb(0, 0, 128),
                            ),
                        ] {
                            if key != strings::OTHER_EVENT {
                                continue;
                            }
                            plot.points(
                                egui_plot::Points::new(catalog.text(key), points.clone())
                                    .color(color)
                                    .shape(match key {
                                        strings::UP_EVENT => egui_plot::MarkerShape::Up,
                                        strings::DOWN_EVENT => egui_plot::MarkerShape::Down,
                                        _ => egui_plot::MarkerShape::Circle,
                                    })
                                    .radius(2.0_f32),
                            );
                        }
                    });
                    // WinForms draws direction triangles as labels above the event price.
                    for (points, glyph, color) in [
                        (&self.up, "up", egui::Color32::from_rgb(50, 205, 50)),
                        (&self.down, "down", egui::Color32::RED),
                    ] {
                        for xy in points {
                            let position = graph
                                .transform
                                .position_from_point(&egui_plot::PlotPoint::new(xy[0], xy[1]));
                            let center = position - egui::vec2(0.0, 9.0);
                            let sign = if glyph == "up" { 1.0 } else { -1.0 };
                            let triangle = vec![
                                center + egui::vec2(0.0, -5.0 * sign),
                                center + egui::vec2(-4.5, 4.0 * sign),
                                center + egui::vec2(4.5, 4.0 * sign),
                            ];
                            ui.painter().with_clip_rect(*graph.transform.frame()).add(
                                egui::Shape::convex_polygon(triangle, color, egui::Stroke::NONE),
                            );
                        }
                    }
                    ui.horizontal_wrapped(|ui| {
                        ui.spacing_mut().item_spacing.x = 6.0;
                        for (label, symbol, color) in [
                            ("ITI Price", "line", egui::Color32::YELLOW),
                            (
                                "Other ITI Event",
                                "circle",
                                egui::Color32::from_rgb(0, 0, 128),
                            ),
                            ("Direction Up", "up", egui::Color32::from_rgb(50, 205, 50)),
                            ("Direction Down", "down", egui::Color32::RED),
                        ] {
                            let (rect, _) = ui
                                .allocate_exact_size(egui::vec2(12.0, 14.0), egui::Sense::hover());
                            let center = rect.center();
                            match symbol {
                                "line" => {
                                    ui.painter().line_segment(
                                        [
                                            center - egui::vec2(6.0, 0.0),
                                            center + egui::vec2(6.0, 0.0),
                                        ],
                                        egui::Stroke::new(2.0_f32, color),
                                    );
                                }
                                "circle" => {
                                    ui.painter().circle(
                                        center,
                                        3.0,
                                        color,
                                        egui::Stroke::new(1.0_f32, egui::Color32::LIGHT_GRAY),
                                    );
                                }
                                _ => {
                                    let sign = if symbol == "up" { 1.0 } else { -1.0 };
                                    ui.painter().add(egui::Shape::convex_polygon(
                                        vec![
                                            center + egui::vec2(0.0, -4.0 * sign),
                                            center + egui::vec2(-4.0, 3.0 * sign),
                                            center + egui::vec2(4.0, 3.0 * sign),
                                        ],
                                        color,
                                        egui::Stroke::NONE,
                                    ));
                                }
                            }
                            ui.trading_caption(label);
                        }
                    });
                    if graph.response.clicked() {
                        if let Some(pointer) = graph.response.interact_pointer_pos() {
                            if let Some((signal, distance)) = snapshot
                                .signals
                                .iter()
                                .filter(|s| s.utc_seconds.is_finite() && s.price.is_finite())
                                .map(|s| {
                                    (
                                        s,
                                        graph
                                            .transform
                                            .position_from_point(&egui_plot::PlotPoint::new(
                                                s.market_seconds.unwrap_or(s.utc_seconds),
                                                s.price,
                                            ))
                                            .distance(pointer),
                                    )
                                })
                                .min_by(|a, b| a.1.total_cmp(&b.1))
                            {
                                if distance <= 12.0 {
                                    action = snapshot
                                        .rows
                                        .iter()
                                        .find(|r| r.trigger_identity == signal.identity)
                                        .map(|r| r.action);
                                }
                            }
                        }
                    }
                }
            });
        egui::TopBottomPanel::top("strategy_workspace_tabs").show_separator_line(false).show_inside(ui, |ui| {
            ui.horizontal(|ui| {
                crate::presentation::tab_value(ui,
                    &mut self.tab,
                    StrategyTab::Updates,
                    catalog.text(strings::STRATEGY_UPDATES),
                );
                crate::presentation::tab_value(ui,
                    &mut self.tab,
                    StrategyTab::Details,
                    catalog.text(strings::DETAILS),
                );
                crate::presentation::tab_value(ui,
                    &mut self.tab,
                    StrategyTab::Summary,
                    catalog.text(strings::SUMMARY),
                );
            });
        });
        self.drawn_rows = 0;
        match self.tab {
            StrategyTab::Details => details.draw_inside(ui, snapshot, catalog),
            StrategyTab::Updates => {
                egui::CentralPanel::default().show_inside(ui, |ui| {
                    let grid_border = ui.available_rect_before_wrap();
                    if snapshot.rows.is_empty() {
                        ui.trading_caption(catalog.text(strings::NO_WORKFLOWS));
                    } else {
                        let height = ui.text_style_height(&egui::TextStyle::Body) + 8.0;
                        let mut near_history_end = false;
                        // Keep the horizontal scrollbar below the body instead of over its last row.
                        ui.spacing_mut().scroll.floating = false;
                        let row_stride = height + ui.spacing().item_spacing.y;
                        let body_space = (ui.available_height()
                            - ui.spacing().scroll.allocated_width()
                            - 24.0
                            - ui.spacing().item_spacing.y)
                            .max(0.0);
                        let body_height = (body_space / row_stride).floor() * row_stride;
                        egui::ScrollArea::horizontal()
                            .scroll_bar_visibility(egui::scroll_area::ScrollBarVisibility::AlwaysVisible)
                            .auto_shrink([false, false])
                            .show(ui, |ui| {
                                egui_extras::TableBuilder::new(ui)
                                    .id_salt("strategy-workflows-table")
                                    .min_scrolled_height(0.0)
                                    .max_scroll_height(body_height)
                                    .striped(true)
                                    .resizable(true)
                                    .cell_layout(egui::Layout::left_to_right(egui::Align::Center))
                                    .column(egui_extras::Column::initial(185.0).at_least(120.0))
                                    .column(egui_extras::Column::initial(165.0).at_least(100.0))
                                    .column(egui_extras::Column::initial(90.0).at_least(65.0))
                                    .column(egui_extras::Column::initial(85.0).at_least(65.0))
                                    .column(egui_extras::Column::initial(235.0).at_least(100.0))
                                    .column(egui_extras::Column::initial(135.0).at_least(100.0))
                                    .header(24.0, |mut header| {
                                        for key in [
                                            strings::STRATEGY_DATE_TIME,
                                            strings::STRATEGY_SIGNAL_EVENT,
                                            strings::STRATEGY_TREND_TYPE,
                                            strings::FUTURES_PRICE,
                                            strings::STRATEGY_PIPELINE_STATE,
                                            strings::STRATEGY_END_STATE,
                                        ] {
                                            header.col(|ui| {
                                                // Match StatusConsoleView's DarkTradingTheme ListView headers.
                                                let rect = ui.max_rect().expand2(egui::vec2(
                                                    ui.spacing().item_spacing.x / 2.0,
                                                    0.0,
                                                ));
                                                ui.painter().rect(
                                                    rect,
                                                    0.0,
                                                    crate::presentation::COMMAND,
                                                    egui::Stroke::new(
                                                        1.0_f32,
                                                        crate::presentation::BORDER,
                                                    ),
                                                    egui::StrokeKind::Inside,
                                                );
                                                ui.add(
                                                    crate::presentation::label_widget(
                                                        egui::RichText::new(catalog.text(key))
                                                            .color(crate::presentation::CAPTION_FOREGROUND),
                                                    )
                                                    .truncate(),
                                                );
                                            });
                                        }
                                    })
                                    .bordered_body(24.0, |body| {
                                        body.rows(height, snapshot.rows.len(), |mut table_row| {
                                            self.drawn_rows += 1;
                                            near_history_end |=
                                                table_row.index() + 10 >= snapshot.rows.len();
                                            let row = &snapshot.rows[table_row.index()];
                                            table_row.set_selected(snapshot.workflow_id == row.id);
                                            table_row.col(|ui| {
                                                if ui
                                                    .selectable_label(
                                                        snapshot.workflow_id == row.id,
                                                        &row.occurred_on,
                                                    )
                                                    .clicked()
                                                {
                                                    action = Some(row.action);
                                                }
                                            });
                                            for text in
                                                [&row.signal, &row.trend, &row.futures_price]
                                            {
                                                table_row.col(|ui| {
                                                    ui.trading_label(text);
                                                });
                                            }
                                            table_row.col(|ui| {
                                                ui.horizontal(|ui| {
                                                    ui.spacing_mut().item_spacing.x = 6.0;
                                                    for actor in row
                                                        .actors
                                                        .iter()
                                                        .take(visible_pipeline_count(&row.actors))
                                                    {
                                                        let color = pipeline_color(&actor.state);
                                                        let (rect, response) = ui
                                                            .allocate_exact_size(
                                                                egui::vec2(12.0, 12.0),
                                                                egui::Sense::hover(),
                                                            );
                                                        ui.painter().circle(
                                                            rect.center(),
                                                            6.0,
                                                            color,
                                                            egui::Stroke::new(
                                                                1.0_f32,
                                                                if snapshot.workflow_id == row.id {
                                                                    egui::Color32::WHITE
                                                                } else {
                                                                    egui::Color32::DARK_GRAY
                                                                },
                                                            ),
                                                        );
                                                        response.on_hover_text(&actor.value);
                                                        if color == egui::Color32::RED {
                                                            break;
                                                        }
                                                    }
                                                });
                                            });
                                            table_row.col(|ui| {
                                                ui.trading_label(&row.end_state);
                                            });
                                            if table_row.response().clicked() {
                                                action = Some(row.action);
                                            }
                                        })
                                    });
                            });
                        let page = (
                            snapshot.epoch.clone(),
                            snapshot.page_number,
                            snapshot.rows.len(),
                        );
                        if near_history_end
                            && snapshot.has_more
                            && self.requested_history_page.as_ref() != Some(&page)
                            && action.is_none()
                        {
                            self.requested_history_page = Some(page);
                            if action.is_none() {
                                action = Some(13);
                            }
                        }
                    }
                    ui.painter().rect_stroke(
                        grid_border.intersect(ui.clip_rect()), 0.,
                        egui::Stroke::new(1. / ui.ctx().pixels_per_point(), crate::presentation::BORDER),
                        egui::StrokeKind::Inside,
                    );
                });
            }
            StrategyTab::Summary => {
                egui::CentralPanel::default().show_inside(ui, |ui| {
                    ui.centered_and_justified(|ui| {
                        ui.trading_label(
                            egui::RichText::new(catalog.text(strings::NO_SUMMARY))
                                .color(egui::Color32::from_rgb(192, 192, 192)),
                        );
                    });
                });
            }
        }
        action
    }
}

/// Map backend actor-stage state to the pipeline indicator palette.
pub fn pipeline_color(state: &str) -> egui::Color32 {
    match state {
        "Processing" => egui::Color32::YELLOW,
        "Continued" | "Continue" => egui::Color32::GREEN,
        _ => egui::Color32::RED,
    }
}

/// Return the number of actor indicators through the first failed stage, including that stage.
pub fn visible_pipeline_count(actors: &[WorkflowActor]) -> usize {
    actors
        .iter()
        .position(|actor| pipeline_color(&actor.state) == egui::Color32::RED)
        .map_or(actors.len(), |index| index + 1)
}
