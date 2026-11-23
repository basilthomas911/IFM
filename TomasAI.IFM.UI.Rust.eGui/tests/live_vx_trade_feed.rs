//! Explicit read-only acceptance through the same typed queries and revision fences as eGui.
use ifm_ui_egui::{
    backend::{schema::Registry, transport::{Backend, Settings}},
    services::{business_context::BusinessContext, market_history, trade_monitor_queries::{self, Scope}},
    state::monitor_cache::Realtime,
};
use std::{collections::BTreeMap, time::Duration};

#[tokio::test]
#[ignore = "Live read-only acceptance; requires IFM_LIVE_DIAGNOSTIC_NATS and IFM_LIVE_DIAGNOSTIC_TRADE"]
async fn active_vx_chart_and_monitored_option_observations_advance() {
    let ids = std::env::var("IFM_LIVE_DIAGNOSTIC_TRADE").unwrap().split('.')
        .map(|value| value.parse::<i32>().unwrap()).collect::<Vec<_>>();
    assert_eq!(ids.len(), 4);
    let scope = Scope { portfolio: ids[0], fund: ids[1], order: ids[2], trade: ids[3] };
    let backend = Backend::connect(Settings { url: std::env::var("IFM_LIVE_DIAGNOSTIC_NATS").unwrap(),
        credentials_file: None, token: None, request_timeout: Duration::from_secs(30) }).await.unwrap();
    let registry = Registry::compiled().unwrap();
    let mut strategy = ifm_ui_egui::state::strategy::StrategyController::default();
    strategy.refresh(&backend, &registry, false).await.unwrap();
    strategy.snapshot(&registry).unwrap();
    let context = BusinessContext::load(&backend).await.unwrap();
    let trade = trade_monitor_queries::established(&backend, &registry, scope, "IronCondor",
        tokio::time::Instant::now() + Duration::from_secs(30)).await.unwrap();
    let mut mirror = Realtime::new(scope, context.value_date, &trade).unwrap();
    let mut initial = BTreeMap::new();
    let mut latest = BTreeMap::new();
    let mut first_chart_end = None;
    let mut last_chart_end = None;
    for sample in 0..4 {
        let context = BusinessContext::load(&backend).await.unwrap();
        let charts = market_history::load_for_session(&backend, &registry, &context).await;
        let vx = &charts["VX"];
        assert!(vx.status.is_empty(), "VX chart status: {}", vx.status);
        assert_eq!(vx.series.len(), 2);
        assert!(vx.series[0].name.starts_with("Front month"));
        assert!(vx.series[1].name.starts_with("Back month"));
        let ends = vx.series.iter().map(|line| {
            assert!(!line.points.is_empty(), "{} is empty", line.name);
            (line.name.clone(), line.points.last().unwrap()[0])
        }).collect::<BTreeMap<_, _>>();
        first_chart_end.get_or_insert_with(|| ends.clone());
        last_chart_end = Some(ends);
        let snapshot = trade_monitor_queries::realtime(&backend, &registry, scope, context.value_date,
            tokio::time::Instant::now() + Duration::from_secs(30)).await.unwrap().unwrap();
        assert!(mirror.accept(snapshot.clone()), "eGui rejected a current live snapshot");
        for leg in snapshot.legs.as_ref().unwrap() {
            let observation = leg.observation.as_ref().expect("missing option observation");
            let contract = observation.contract_id.clone().unwrap();
            let stamp = observation.observed_at_utc.as_ref().unwrap().as_str().to_owned();
            initial.entry(contract.clone()).or_insert_with(|| stamp.clone());
            latest.insert(contract, stamp);
        }
        let chart_summary = vx.series.iter().map(|line| serde_json::json!({
            "name":line.name,"points":line.points.len(),"last":line.points.last()
        })).collect::<Vec<_>>();
        println!("LIVE_ACCEPTANCE {}", serde_json::json!({"sample":sample,"vx":chart_summary,
            "snapshotRevision":snapshot.snapshot_revision,"observations":latest,
            "planRevision":snapshot.plan.as_ref().and_then(|plan|plan.plan_revision)}));
        if sample < 3 { tokio::time::sleep(Duration::from_secs(20)).await; }
    }
    assert_eq!(initial.len(), 4);
    for (contract, before) in initial { assert!(latest[&contract] > before, "{contract} did not advance"); }
    for (contract, before) in first_chart_end.unwrap() {
        assert!(last_chart_end.as_ref().unwrap()[&contract] > before, "{contract} chart did not advance");
    }
}
