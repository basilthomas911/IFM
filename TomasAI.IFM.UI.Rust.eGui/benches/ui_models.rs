//! Repeatable CPU benchmarks for the standalone UI's production data paths.
//! Inputs are committed, synthetic C# contract fixtures, never live account data.
//! Broker/database latency, registry startup, rendering and GPU work are excluded.
use criterion::{BenchmarkId, Criterion, Throughput, criterion_group, criterion_main};
use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
    services::monitor_display,
    trade_workspace::TradeWorkspace,
    wire,
};
use std::{collections::BTreeMap, hint::black_box, time::Duration};
#[path = "../tests/support/chart_model_baseline.rs"]
mod chart_baseline;

const PLAN: &[u8] = include_bytes!("../tests/fixtures/backend/monitor-plan.msgpack");
const REALTIME: &[u8] = include_bytes!("../tests/fixtures/backend/monitor-realtime.msgpack");
const ESTABLISHED: &[u8] =
    include_bytes!("../tests/fixtures/backend/monitor-established-display.msgpack");
const PLAN_DISPLAY: &[u8] =
    include_bytes!("../tests/fixtures/backend/monitor-plan-history-display.msgpack");

/// Calls the actual decoding/projection/row-delivery path. Future typed adapters
/// must retain these benchmark IDs and equivalent visible outputs for comparison.
fn pipeline(registry: &Registry, kind: &str, bytes: &[u8], history: bool) -> serde_json::Value {
    let wire = codec::decode(bytes).unwrap();
    let named = monitor_display::named(registry, kind, &wire).unwrap();
    let display = if history {
        monitor_display::plan_history(&named)
    } else {
        monitor_display::document(&named)
    };
    let row_bytes = wire::encode_value(&display).unwrap();
    wire::decode(&row_bytes).unwrap()
}

fn production_models(c: &mut Criterion) {
    let registry = Registry::compiled().unwrap();
    let mut group = c.benchmark_group("ui_models/backend_to_display");
    group.throughput(Throughput::Elements(1));
    for (name, kind, bytes, history) in [
        (
            "plan",
            "TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot",
            PLAN,
            true,
        ),
        (
            "realtime",
            "TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot",
            REALTIME,
            false,
        ),
    ] {
        assert!(pipeline(&registry, kind, bytes, history).is_object());
        group.bench_function(name, |b| {
            b.iter(|| black_box(pipeline(&registry, kind, black_box(bytes), history)))
        });
    }
    group.finish();

    let mut group = c.benchmark_group("ui_models/publication_prepare");
    for count in [1usize, 100] {
        let mut rows = vec![ContractRow {
            id: "established:benchmark".into(),
            fields: BTreeMap::from([("kind".into(), "established".into())]),
            data: Some(ESTABLISHED.to_vec().into()),
        }];
        for index in 0..count {
            rows.push(ContractRow {
                id: format!("plan:{index}"),
                fields: BTreeMap::from([
                    ("kind".into(), "plan".into()),
                    ("actionDateTimeEastern".into(), "08:30:00.000 AM".into()),
                ]),
                data: Some(PLAN_DISPLAY.to_vec().into()),
            });
        }
        group.throughput(Throughput::Elements(count as u64));
        // Include publication ownership, preparation and final release. Reusing
        // one publication here would measure OnceLock hits instead of new data.
        group.bench_with_input(BenchmarkId::from_parameter(count), &rows, |b, rows| {
            b.iter(|| {
                let publication = ContractPublication {
                    generation: 1,
                    result: ContractResult {
                        rows: rows.clone(),
                        ..Default::default()
                    },
                    ..Default::default()
                };
                TradeWorkspace::prepare_publication(black_box(&publication));
                black_box(publication);
            })
        });
    }
    group.finish();
}

fn chart_models(c: &mut Criterion) {
    let mut group = c.benchmark_group("ui_models/chart_decode");
    for count in [100usize, 10_000] {
        let bytes = chart_baseline::input(count);
        assert_eq!(
            chart_baseline::legacy(&bytes),
            ifm_ui_egui::models::trade_chart::PreparedTradeChart::from_messagepack(&bytes).unwrap()
        );
        group.throughput(Throughput::Elements(count as u64));
        group.bench_with_input(
            BenchmarkId::new("before_json", count),
            &bytes,
            |b, bytes| b.iter(|| black_box(chart_baseline::legacy(black_box(bytes)))),
        );
        group.bench_with_input(
            BenchmarkId::new("after_typed", count),
            &bytes,
            |b, bytes| {
                b.iter(|| {
                    black_box(
                        ifm_ui_egui::models::trade_chart::PreparedTradeChart::from_messagepack(
                            black_box(bytes),
                        )
                        .unwrap(),
                    )
                })
            },
        );
    }
    group.finish();
}

criterion_group! {
    name = benches;
    config = Criterion::default().sample_size(30).warm_up_time(Duration::from_secs(1)).measurement_time(Duration::from_secs(3));
    targets = production_models, chart_models
}
criterion_main!(benches);
