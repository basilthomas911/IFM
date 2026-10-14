use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_rust::market_charts::{self, ChartSeries, ChartSnapshot};
use std::collections::BTreeMap;
#[test]
fn all_three_market_tabs_select_their_own_graph_without_blocking() {
    let charts: BTreeMap<_, _> = ["ES", "ES-BB", "VX"]
        .iter()
        .map(|name| {
            (
                (*name).to_string(),
                ChartSnapshot {
                    start: 100.0,
                    end: 200.0,
                    minimum: Some(10.0),
                    maximum: Some(20.0),
                    series: vec![ChartSeries {
                        name: (*name).into(),
                        color: "Yellow".into(),
                        points: vec![[100.0, 12.0], [200.0, 18.0]],
                    }],
                    ..Default::default()
                },
            )
        })
        .collect();
    let mut harness = Harness::builder()
        .with_size(egui::vec2(600.0, 300.0))
        .build_state(
            move |ctx, tab: &mut usize| {
                egui::CentralPanel::default().show(ctx, |ui| market_charts::draw(ui, &charts, tab));
            },
            0usize,
        );
    harness.run();
    for (index, name) in ["ES", "ES-BB", "VX", "ES"].iter().enumerate() {
        harness.get_by_label(name).click();
        harness.run();
        assert_eq!(*harness.state(), index % 3);
    }
    assert_eq!(
        market_charts::series_color("Fuchsia"),
        egui::Color32::from_rgb(255, 0, 255)
    );
    assert_eq!(
        market_charts::series_color("Green"),
        egui::Color32::from_rgb(0, 128, 0)
    );
}

#[test]
fn vx_prices_keep_two_decimals_and_six_hour_ticks_use_whole_market_hours() {
    assert_eq!(market_charts::price_label("VX", 16.9), "16.90");
    assert_eq!(market_charts::price_label("VX", 17.0), "17.00");
    let marks = market_charts::hour_marks(egui_plot::GridInput {
        bounds: (10.5 * 3600.0, 16.5 * 3600.0),
        base_step_size: 1000.0,
    });
    assert_eq!(marks.len(), 6);
    assert_eq!(marks.first().unwrap().value, 11.0 * 3600.0);
    assert_eq!(marks.last().unwrap().value, 16.0 * 3600.0);
    assert!(
        marks
            .windows(2)
            .all(|pair| pair[1].value - pair[0].value == 3600.0)
    );
}
