use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::{
    market_outlook::{self, OutlookCell},
    shell::Dashboard,
};
#[test]
fn compact_outlook_contains_all_winforms_rows_and_live_cell_values() {
    let mut dashboard = Dashboard::default();
    dashboard.outlook_cells.insert(
        "closePrice".into(),
        OutlookCell {
            value: "7858.25".into(),
            foreground: "Caution".into(),
            background: "DarkSurface".into(),
        },
    );
    let mut harness = Harness::builder()
        .with_size(egui::vec2(750.0, 400.0))
        .build(move |ctx| {
            egui::CentralPanel::default().show(ctx, |ui| market_outlook::draw(ui, &dashboard));
        });
    harness.run();
    for label in [
        "Market Direction",
        "Market Volatility",
        "Price Direction",
        "Price Volatility",
        "Open",
        "High",
        "Low",
        "Close",
        "Volume",
        "% Change",
        "Std Dev",
        "Upper",
        "Lower",
        "20 EMA",
        "50 EMA",
        "200 EMA",
        "VWAP",
        "MDI",
        "ADX",
        "ATR",
        "MACD",
        "RSI",
        "TDI Direction",
        "TDI Strength",
        "Market State",
        "Cross",
        "Divergence",
        "UpTrend Limit",
        "DownTrend Limit",
        "Trend Extreme",
        "Trend Reversal",
        "Trend Direction",
        "7858.25",
    ] {
        assert!(harness.query_by_label(label).is_some(), "Missing {label}");
    }
    assert!(harness.query_by_label("Market Outlook").is_none());
}
#[test]
fn semantic_colors_match_winforms_palette() {
    assert_eq!(
        market_outlook::color("Positive"),
        egui::Color32::from_rgb(50, 205, 50)
    );
    assert_eq!(market_outlook::color("Negative"), egui::Color32::RED);
    assert_eq!(market_outlook::color("Caution"), egui::Color32::YELLOW);
    assert_eq!(market_outlook::color("DarkText"), egui::Color32::BLACK);
    assert_eq!(
        market_outlook::color("Warning"),
        egui::Color32::from_rgb(255, 140, 0)
    );
}
