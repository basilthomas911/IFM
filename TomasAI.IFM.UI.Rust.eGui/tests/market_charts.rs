use eframe::egui;
use egui_kittest::{Harness, kittest::Queryable};
use ifm_ui_egui::market_charts::{self, ChartSeries, ChartSnapshot};
use std::collections::BTreeMap;


/// Every VX price mark has the same readable level and a distinct two-decimal label.
#[test]
fn vx_horizontal_marks_all_have_readable_price_labels() {
    for bounds in [(16.85, 17.35), (10.0, 40.0), (16.975, 17.025)] {
        let marks = market_charts::vx_price_marks(egui_plot::GridInput { bounds, base_step_size: (bounds.1 - bounds.0) / 60.0 });
        assert!(marks.len() >= 2);
        assert!(marks.windows(2).all(|pair| pair[0].step_size == pair[1].step_size));
        let labels: std::collections::BTreeSet<_> = marks.iter().map(|mark| market_charts::price_label("VX", mark.value)).collect();
        assert_eq!(labels.len(), marks.len());
        assert!(marks.iter().all(|mark| mark.value >= bounds.0 - 1e-9 && mark.value <= bounds.1 + 1e-9));
    }
    assert_eq!(market_charts::series_color("LightBlue"), egui::Color32::from_rgb(173, 216, 230));
}


/// Inspect painted axis text, because grid labels are not accessibility controls.
#[test]
fn vx_paints_multiple_horizontal_axis_prices_and_keeps_contract_ids_in_tooltips() {
    let context = egui::Context::default();
    let charts = BTreeMap::from([("VX".into(), ChartSnapshot {
        start: 100.0, end: 200.0, minimum: Some(16.85), maximum: Some(17.35),
        series: vec![
            ChartSeries { name: "Front month · VX-FRONT".into(), color: "Fuchsia".into(), points: vec![[100.0,16.9],[200.0,17.0]] },
            ChartSeries { name: "Back month · VX-BACK".into(), color: "LightBlue".into(), points: vec![[100.0,17.2],[200.0,17.3]] },
        ], ..Default::default()
    })]);
    fn texts(shape: &egui::Shape, labels: &mut std::collections::BTreeSet<String>) {
        match shape {
            egui::Shape::Text(text) => { labels.insert(text.galley.job.text.clone()); }
            egui::Shape::Vec(shapes) => for shape in shapes { texts(shape, labels); },
            _ => {}
        }
    }
    let mut labels = std::collections::BTreeSet::new();
    for _ in 0..4 {
        let output = context.run(egui::RawInput { screen_rect: Some(egui::Rect::from_min_size(egui::Pos2::ZERO,egui::vec2(600.,360.))), ..Default::default() }, |ctx| {
            egui::CentralPanel::default().show(ctx, |ui| market_charts::draw(ui, &charts, &mut 2));
        });
        labels.clear();
        for shape in &output.shapes { texts(&shape.shape, &mut labels); }
    }
    assert!(labels.iter().filter(|label| label.parse::<f64>().is_ok()).count() >= 3, "Missing VX axis prices: {labels:?}");
    assert!(!labels.contains("Front month · VX-FRONT"));
    assert!(!labels.contains("Back month · VX-BACK"));
    assert_eq!(market_charts::series_tooltip("VX", "Front month · VX-FRONT", 16.9), "Front month · VX-FRONT: 16.90");
    assert_eq!(market_charts::series_tooltip("VX", "Back month · VX-BACK", 17.3), "Back month · VX-BACK: 17.30");
}


/// Verifies that all three market tabs select their own graph without blocking.
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


/// Verifies that vx prices keep two decimals and six hour ticks use whole market hours.
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
