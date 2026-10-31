//! Repeatable CPU timing of the actual native portfolio publication producer.
//! Run with --ignored --exact benchmark_portfolio_publication --nocapture.
use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    state::portfolio::PortfolioController,
};
use std::{hint::black_box, time::Instant};
#[test]
#[ignore = "CPU benchmark; run separately from other compiler/benchmark work"]
fn benchmark_portfolio_publication() {
    let registry = Registry::compiled().unwrap();
    let portfolio = codec::decode(include_bytes!(
        "fixtures/backend/portfolio-create-input.msgpack"
    ))
    .unwrap();
    let fund = codec::decode(include_bytes!("fixtures/backend/portfolio-fund.msgpack")).unwrap();
    let mut report = Vec::new();
    for count in [1usize, 100] {
        let mut controller = PortfolioController::default();
        controller.data.portfolio = Some(portfolio.clone());
        controller.data.portfolios = vec![portfolio.clone()];
        controller.data.fund = Some(fund.clone());
        controller.data.funds = vec![fund.clone(); count];
        let result = controller.result(&registry).unwrap();
        let document: serde_json::Value =
            ifm_ui_egui::wire::decode(&result.documents["portfolio"]).unwrap();
        assert_eq!(
            document["grids"][1]["rows"].as_array().unwrap().len(),
            count
        );
        for _ in 0..20 {
            black_box(controller.result(&registry).unwrap());
        }
        let mut samples = Vec::new();
        for _ in 0..101 {
            let start = Instant::now();
            black_box(controller.result(&registry).unwrap());
            samples.push(start.elapsed().as_secs_f64() * 1000.0);
        }
        samples.sort_by(f64::total_cmp);
        report.push(serde_json::json!({"fundRows":count,"samples":samples.len(),"p50Ms":samples[50],"p95Ms":samples[95],"document":document}));
    }
    let text = serde_json::to_string_pretty(&report).unwrap();
    if let Ok(path) = std::env::var("IFM_PORTFOLIO_BENCHMARK") {
        std::fs::write(path, &text).unwrap();
    }
    println!("{}",serde_json::to_string(&report.iter().map(|r|serde_json::json!({"fundRows":r["fundRows"],"p50Ms":r["p50Ms"],"p95Ms":r["p95Ms"]})).collect::<Vec<_>>()).unwrap());
}
