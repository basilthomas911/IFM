use crate::presentation::TradingLabelUi as _;
use eframe::egui;
use serde::Deserialize;
use std::collections::BTreeMap;
#[derive(Clone, Default, Deserialize)]
#[serde(default)]
pub struct ChartSnapshot {
    pub status: String,
    pub start: f64,
    pub end: f64,
    pub minimum: Option<f64>,
    pub maximum: Option<f64>,
    pub series: Vec<ChartSeries>,
}
#[derive(Clone, Default, Deserialize)]
#[serde(default)]
pub struct ChartSeries {
    pub name: String,
    pub color: String,
    pub points: Vec<[f64; 2]>,
}
pub fn series_color(name: &str) -> egui::Color32 {
    match name {
        "Yellow" => egui::Color32::YELLOW,
        "Lime" => egui::Color32::GREEN,
        "Green" => egui::Color32::from_rgb(0, 128, 0),
        "Red" => egui::Color32::RED,
        "Blue" => egui::Color32::BLUE,
        "Fuchsia" => egui::Color32::from_rgb(255, 0, 255),
        _ => egui::Color32::WHITE,
    }
}
pub fn draw(ui: &mut egui::Ui, charts: &BTreeMap<String, ChartSnapshot>, selected: &mut usize) {
    ui.horizontal(|ui| {
        for (index, name) in ["ES", "ES-BB", "VX"].iter().enumerate() {
            crate::presentation::tab_value(ui,selected, index, *name);
        }
    });
    ui.visuals_mut().widgets.noninteractive.bg_stroke =
        egui::Stroke::new(1.0_f32, egui::Color32::from_rgb(45, 45, 45));
    let name = ["ES", "ES-BB", "VX"][*selected];
    let chart = charts.get(name);
    if chart.is_none_or(|c| !c.status.is_empty()) {
        ui.trading_label(
            chart
                .map(|c| c.status.as_str())
                .unwrap_or("Loading market history..."),
        );
    }
    let mut plot = egui_plot::Plot::new(("market-data-chart", name))
        .height(ui.available_height().max(60.0))
        .show_background(false)
        .show_grid(true)
        .allow_drag(false)
        .allow_zoom(false)
        .allow_scroll(false)
        .custom_y_axes(vec![
            egui_plot::AxisHints::new_y().placement(egui_plot::HPlacement::Right),
        ])
        .x_axis_formatter(move |mark, _| {
            chrono::DateTime::from_timestamp(mark.value as i64, 0)
                .map(|time| {
                    time.format(if name == "ES-BB" {
                        "%b %-d"
                    } else {
                        "%-I:%M:%S %p"
                    })
                    .to_string()
                })
                .unwrap_or_default()
        })
        .y_axis_formatter(move |mark, _| price_label(name, mark.value))
        .label_formatter(move |series, point| format!("{series}: {}", price_label(name, point.y)));
    if name != "ES-BB" {
        plot = plot.x_grid_spacer(hour_marks);
    }
    if let Some(chart) = chart {
        if chart.start < chart.end {
            plot = plot.include_x(chart.start).include_x(chart.end);
        }
        if let Some(min) = chart.minimum {
            plot = plot.include_y(min);
        }
        if let Some(max) = chart.maximum {
            plot = plot.include_y(max);
        }
    }
    let graph = plot.show(ui, |plot| {
        if let Some(chart) = chart {
            if let (Some(min), Some(max)) = (chart.minimum, chart.maximum) {
                if chart.start < chart.end && min < max {
                    plot.set_plot_bounds(egui_plot::PlotBounds::from_min_max(
                        [chart.start, min],
                        [chart.end, max],
                    ));
                }
            }
            for series in &chart.series {
                let points: Vec<_> = series
                    .points
                    .iter()
                    .copied()
                    .filter(|p| p[0].is_finite() && p[1].is_finite())
                    .collect();
                if points.len() == 1 {
                    plot.points(
                        egui_plot::Points::new(&series.name, points)
                            .color(series_color(&series.color))
                            .radius(3.0_f32),
                    );
                } else {
                    plot.line(
                        egui_plot::Line::new(&series.name, points)
                            .color(series_color(&series.color))
                            .width(2.0_f32),
                    );
                }
            }
        }
    });
    ui.painter().rect_stroke(
        *graph.transform.frame(),
        0.0,
        egui::Stroke::new(1.0_f32, crate::presentation::BORDER),
        egui::StrokeKind::Inside,
    );
}

/// VX keeps both decimal places, including trailing zeros.
pub fn price_label(symbol: &str, value: f64) -> String {
    match symbol {
        "VX" => format!("{value:.2}"),
        "ES-BB" => format!("{value:.0}"),
        _ => format!("{value:.2}")
            .trim_end_matches('0')
            .trim_end_matches('.')
            .to_owned(),
    }
}
/// Plot coordinates are Eastern wall-clock seconds, already converted by the bridge.
/// Anchor ticks at whole market hours instead of generic decimal-second intervals.
pub fn hour_marks(input: egui_plot::GridInput) -> Vec<egui_plot::GridMark> {
    let (start, end) = input.bounds;
    if !start.is_finite() || !end.is_finite() || end < start {
        return vec![];
    }
    let first = (start / 3600.0).ceil() * 3600.0;
    let count = (((end - first) / 3600.0).floor() + 1.0).max(0.0).min(100.0) as usize;
    (0..count)
        .map(|index| egui_plot::GridMark {
            value: first + index as f64 * 3600.0,
            step_size: 3600.0,
        })
        .collect()
}
