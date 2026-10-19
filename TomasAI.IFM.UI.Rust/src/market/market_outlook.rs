//! Market-outlook caption/value matrix. Published color roles are mapped to the native palette; numeric signals and outlook decisions remain backend-owned.

//! Compact MarketOutlookView row layout and WinForms semantic colors.
use crate::shell::Dashboard;
use eframe::egui;
use serde::Deserialize;

/// Outlook caption row height in logical points.
pub const LABEL_HEIGHT: f32 = 17.0;
/// Outlook value row height in logical points.
pub const VALUE_HEIGHT: f32 = 24.0;
/// Outlook status line height in logical points.
pub const STATUS_HEIGHT: f32 = 18.0;
/// Total reference height of the six-row outlook matrix plus its status line.
pub const HEIGHT: f32 = 6.0 * (LABEL_HEIGHT + VALUE_HEIGHT) + STATUS_HEIGHT;
/// Backend market-outlook display value and semantic foreground/background roles.
#[derive(Clone, Default, Deserialize)]
#[serde(default)]
pub struct OutlookCell {
    /// Published or edited display value; formatting/validation belongs to its managed field contract.
    pub value: String,
    /// Published foreground color role for this outlook cell.
    pub foreground: String,
    /// Published background color role for this outlook cell.
    pub background: String,
}

/// Translate a published market-outlook color role into the native display palette.
pub fn color(role: &str) -> egui::Color32 {
    match role {
        "DarkText" | "DarkSurface" => egui::Color32::BLACK,
        "LightText" => egui::Color32::WHITE,
        "Positive" => egui::Color32::from_rgb(50, 205, 50),
        "PositiveMuted" => egui::Color32::from_rgb(154, 205, 50),
        "Caution" => egui::Color32::YELLOW,
        "Warning" => egui::Color32::from_rgb(255, 140, 0),
        "NegativeMuted" => egui::Color32::from_rgb(255, 69, 0),
        "Negative" => egui::Color32::RED,
        _ => egui::Color32::from_rgb(240, 240, 240),
    }
}
/// Render the outlook caption/value matrix and its published status using semantic cell colors.
pub fn draw(ui: &mut egui::Ui, dashboard: &Dashboard) {
    ui.spacing_mut().item_spacing = egui::Vec2::ZERO;
    pair(
        ui,
        dashboard,
        &[
            ("Market Direction", "marketDirection"),
            ("Market Volatility", "marketVolatility"),
            ("Price Direction", "priceDirection"),
            ("Price Volatility", "priceVolatility"),
        ],
    );
    let (rect, _) = ui.allocate_exact_size(
        egui::vec2(ui.available_width(), STATUS_HEIGHT),
        egui::Sense::hover(),
    );
    ui.painter()
        .rect_filled(rect, 0.0, egui::Color32::from_rgb(32, 32, 32));
    ui.scope_builder(
        egui::UiBuilder::new().max_rect(rect.shrink2(egui::vec2(4.0, 0.0))),
        |ui| {
            ui.add(
                crate::presentation::label_widget(
                    egui::RichText::new(if dashboard.outlook_status.is_empty() {
                        "Market Outlook: no persisted snapshot"
                    } else {
                        &dashboard.outlook_status
                    })
                    .color(egui::Color32::from_rgb(220, 220, 220)),
                )
                .truncate(),
            );
        },
    );
    pair(
        ui,
        dashboard,
        &[
            ("Open", "openPrice"),
            ("High", "highPrice"),
            ("Low", "lowPrice"),
            ("Close", "closePrice"),
            ("Volume", "volume"),
            ("% Change", "dailyPercentChange"),
        ],
    );
    pair(
        ui,
        dashboard,
        &[
            ("Std Dev", "dailyStdDev"),
            ("Upper", "upperBand"),
            ("Lower", "lowerBand"),
            ("20 EMA", "mean"),
            ("50 EMA", "fiftyDMA"),
            ("200 EMA", "twoHundredDMA"),
        ],
    );
    pair(
        ui,
        dashboard,
        &[
            ("VWAP", "vwap"),
            ("MDI", "mdi"),
            ("ADX", "adx"),
            ("ATR", "atr"),
            ("MACD", "macd"),
            ("RSI", "rsi"),
        ],
    );
    pair(
        ui,
        dashboard,
        &[
            ("TDI Direction", "tdiDirection"),
            ("TDI Strength", "tdiStrength"),
            ("Market State", "tdiMarketState"),
            ("Cross", "tdiCross"),
            ("Divergence", "tdiDivergence"),
        ],
    );
    pair(
        ui,
        dashboard,
        &[
            ("UpTrend Limit", "upTrendLimit"),
            ("DownTrend Limit", "downLimitTrigger"),
            ("Trend Extreme", "trendExtreme"),
            ("Trend Reversal", "trendReversal"),
            ("Trend Direction", "trend"),
        ],
    );
}
/// Draw the requested outlook caption/value columns using published semantic cell colors.
fn pair(ui: &mut egui::Ui, dashboard: &Dashboard, columns: &[(&str, &str)]) {
    let (row, _) = ui.allocate_exact_size(
        egui::vec2(ui.available_width(), LABEL_HEIGHT + VALUE_HEIGHT),
        egui::Sense::hover(),
    );
    let width = row.width() / columns.len() as f32;
    for (index, (label, key)) in columns.iter().enumerate() {
        let x = row.left() + index as f32 * width;
        let title = egui::Rect::from_min_size(
            egui::pos2(x + 2.0, row.top()),
            egui::vec2(width - 4.0, LABEL_HEIGHT),
        );
        ui.scope_builder(egui::UiBuilder::new().max_rect(title), |ui| {
            ui.with_layout(
                egui::Layout::centered_and_justified(egui::Direction::LeftToRight),
                |ui| {
                    ui.add(crate::presentation::label_widget(egui::RichText::new(*label).color(crate::presentation::CAPTION_FOREGROUND).strong()).truncate());
                },
            );
        });
        let rect = egui::Rect::from_min_size(
            egui::pos2(x + 2.0, row.top() + LABEL_HEIGHT + 1.0),
            egui::vec2(width - 4.0, VALUE_HEIGHT - 2.0),
        );
        let cell = dashboard.outlook_cells.get(*key);
        let value = cell
            .map(|c| c.value.as_str())
            .or_else(|| dashboard.outlook.get(*key).map(String::as_str))
            .unwrap_or("N/A");
        let foreground = cell
            .map(|c| color(&c.foreground))
            .unwrap_or(egui::Color32::WHITE);
        let background = cell
            .map(|c| color(&c.background))
            .unwrap_or(egui::Color32::BLACK);
        ui.painter().rect(
            rect,
            0.0,
            background,
            egui::Stroke::new(1.0_f32, egui::Color32::from_gray(64)),
            egui::StrokeKind::Inside,
        );
        ui.scope_builder(egui::UiBuilder::new().max_rect(rect.shrink(1.0)), |ui| {
            ui.with_layout(
                egui::Layout::centered_and_justified(egui::Direction::LeftToRight),
                |ui| {
                    ui.add(
                        crate::presentation::label_widget(egui::RichText::new(value).color(foreground).strong())
                            .truncate(),
                    )
                    .on_hover_text(format!("{label}: {value}"));
                },
            );
        });
    }
}
