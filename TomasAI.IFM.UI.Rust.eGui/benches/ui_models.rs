//! Repeatable CPU benchmarks for the standalone UI's production data paths.
//! Inputs are committed, synthetic C# contract fixtures, never live account data.
//! Broker/database latency, registry startup, rendering and GPU work are excluded.
#[allow(dead_code)]
#[path = "../tests/support/monitor_display_baseline.rs"]
mod monitor_display;
use criterion::{BenchmarkId, Criterion, Throughput, criterion_group, criterion_main};
use ifm_ui_egui::{
    backend::{codec, schema::Registry},
    contract_editor::{ContractPublication, ContractResult, ContractRow},
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
fn legacy_pipeline(
    registry: &Registry,
    kind: &str,
    bytes: &[u8],
    history: bool,
) -> serde_json::Value {
    let backend = codec::decode(bytes).unwrap();
    let named = monitor_display::named(registry, kind, &backend).unwrap();
    let display = if history {
        monitor_display::plan_history(&named)
    } else {
        monitor_display::document(&named)
    };
    wire::decode(&wire::encode_value(&display).unwrap()).unwrap()
}
fn typed_pipeline<T>(bytes: &[u8], history: bool) -> T
where
    T: ifm_ui_egui::models::wire_types::FromBackend
        + ifm_ui_egui::models::trade_presentation::PresentationValue
        + serde::de::DeserializeOwned,
{
    let backend = codec::decode(bytes).unwrap();
    let record = T::from_backend(&backend).unwrap();
    let row_bytes = wire::encode(&ifm_ui_egui::models::trade_presentation::DisplayRecord {
        value: &record,
        history,
    })
    .unwrap();
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
        use ifm_ui_egui::models::trade_contracts::{
            StrategyTradePlanSnapshot, TradeRealtimeSnapshot,
        };
        let expected = legacy_pipeline(&registry, kind, bytes, history);
        if history {
            let actual = typed_pipeline::<StrategyTradePlanSnapshot>(bytes, history);
            // History serialization deliberately excludes heavyweight persisted evidence.
            let actual =
                serde_json::to_value(ifm_ui_egui::models::trade_presentation::DisplayRecord {
                    value: &actual,
                    history,
                })
                .unwrap();
            assert_eq!(actual, expected);
            group.bench_function(name, |b| {
                b.iter(|| {
                    black_box(typed_pipeline::<StrategyTradePlanSnapshot>(
                        black_box(bytes),
                        history,
                    ))
                })
            });
        } else {
            assert_eq!(
                serde_json::to_value(typed_pipeline::<TradeRealtimeSnapshot>(bytes, history))
                    .unwrap(),
                expected
            );
            group.bench_function(name, |b| {
                b.iter(|| {
                    black_box(typed_pipeline::<TradeRealtimeSnapshot>(
                        black_box(bytes),
                        history,
                    ))
                })
            });
        }
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

#[path = "../tests/support/operations_health_baseline.rs"]
mod health_baseline;
fn health_models(c: &mut Criterion) {
    use ifm_ui_egui::{
        models::operations_health::MarketDataOperationsHealthSnapshot, state::operations_health,
    };
    let mut group = c.benchmark_group("ui_models/operations_health");
    let at = chrono::DateTime::parse_from_rfc3339("2000-01-03T12:30:00Z")
        .unwrap()
        .to_utc();
    for count in [1usize, 128] {
        let stage = serde_json::json!({"stage":"Options","status":"Green","reasonCode":"Healthy","reason":"Current","required":true,
            "pending":0,"capacity":32,"highWater":4,"received":20,"completed":20,"failed":0,"coalesced":5,"saturated":0,
            "marketDataAsOfUtc":"2000-01-03T12:29:59Z","p50Latency":"00:00:00.0010000","p95Latency":"00:00:00.0100000","p99Latency":"00:00:00.0200000"});
        let input=serde_json::to_vec(&serde_json::json!({"schemaVersion":1,"revision":42,"observedOnUtc":"2000-01-03T12:30:00Z",
            "overallStatus":"Green","sessionState":"LiveTrading","valueDate":"2000-01-03","rejectedStaleGenerationPublications":0,
            "stages":vec![stage;count],"datasets":[]})).unwrap();
        let before =
            health_baseline::project(&serde_json::from_slice(&input).unwrap(), at).unwrap();
        let after = operations_health::project(
            &serde_json::from_slice::<MarketDataOperationsHealthSnapshot>(&input).unwrap(),
            at,
        )
        .unwrap();
        assert_eq!(before.context, after.context);
        assert_eq!(before.rows.len(), after.rows.len());
        for (a, b) in before.rows.iter().zip(&after.rows) {
            assert_eq!(a.id, b.id);
            assert_eq!(a.fields, b.fields);
        }
        group.throughput(Throughput::Elements(count as u64));
        group.bench_with_input(
            BenchmarkId::new("before_json", count),
            &input,
            |b, input| {
                b.iter(|| {
                    let value = serde_json::from_slice(black_box(input)).unwrap();
                    black_box(health_baseline::project(&value, at).unwrap())
                })
            },
        );
        group.bench_with_input(
            BenchmarkId::new("after_typed", count),
            &input,
            |b, input| {
                b.iter(|| {
                    let value = serde_json::from_slice::<MarketDataOperationsHealthSnapshot>(
                        black_box(input),
                    )
                    .unwrap();
                    black_box(operations_health::project(&value, at).unwrap())
                })
            },
        );
    }
    group.finish();
}

#[path = "../tests/support/outlook_baseline.rs"]
mod outlook_baseline;
fn outlook_models(c: &mut Criterion) {
    use ifm_ui_egui::{
        models::{trade_contracts::MarketOutlookReadModel, wire_types::FromBackend},
        services::outlook,
    };
    let registry = Registry::compiled().unwrap();
    let bytes = include_bytes!("../tests/fixtures/backend/outlook-0-True.msgpack");
    let now = chrono::DateTime::parse_from_rfc3339("2026-10-26T12:30:00Z")
        .unwrap()
        .to_utc();
    let kind = MarketOutlookReadModel::KIND;
    let before = outlook_baseline::project(
        &registry
            .project(kind, &codec::decode(bytes).unwrap())
            .unwrap(),
        true,
        now,
    )
    .unwrap();
    let after = outlook::project(
        &MarketOutlookReadModel::from_backend(&codec::decode(bytes).unwrap()).unwrap(),
        true,
        now,
    )
    .unwrap();
    assert_eq!(before.status, after.status);
    assert_eq!(before.cells.len(), after.cells.len());
    for (key, value) in &before.cells {
        let actual = &after.cells[key];
        assert_eq!(value.value, actual.value);
        assert_eq!(value.foreground, actual.foreground);
        assert_eq!(value.background, actual.background);
    }
    let mut group = c.benchmark_group("ui_models/outlook");
    group.throughput(Throughput::Elements(1));
    group.bench_function("before_json", |b| {
        b.iter(|| {
            let value = registry
                .project(kind, &codec::decode(black_box(bytes)).unwrap())
                .unwrap();
            black_box(outlook_baseline::project(&value, true, now).unwrap())
        })
    });
    group.bench_function("after_typed", |b| {
        b.iter(|| {
            let value =
                MarketOutlookReadModel::from_backend(&codec::decode(black_box(bytes)).unwrap())
                    .unwrap();
            black_box(outlook::project(&value, true, now).unwrap())
        })
    });
    group.finish();
}

#[path = "../tests/support/market_history_baseline.rs"]
mod market_history_baseline;
fn market_history_models(c: &mut Criterion) {
    use ifm_ui_egui::{
        backend::contracts::timestamp_wire,
        models::{trade_contracts::FuturesBarDataReadModel, wire_types::FromBackend},
        services::market_history,
    };
    let registry = Registry::compiled().unwrap();
    let start = chrono::DateTime::parse_from_rfc3339("2026-10-26T12:00:00Z")
        .unwrap()
        .to_utc();
    let end = start + chrono::Duration::hours(6);
    let mut group = c.benchmark_group("ui_models/market_history");
    for count in [100usize, 10_000] {
        let records = (0..count)
            .map(|index| {
                registry
                    .encode(
                        FuturesBarDataReadModel::KIND,
                        &BTreeMap::from([
                            ("ContractId".into(), "ESZ6".into()),
                            ("Symbol".into(), "ES".into()),
                            (
                                "BarDate".into(),
                                timestamp_wire(start + chrono::Duration::seconds(index as i64)),
                            ),
                            (
                                "BarRateType".into(),
                                registry
                                    .enum_value(
                                        "TomasAI.IFM.Domain.MarketData.Feed.Shared.BarRateType",
                                        "FifteenSeconds",
                                    )
                                    .unwrap()
                                    .into(),
                            ),
                            ("BarValue".into(), "6000.125".into()),
                            ("UpTrendTrigger".into(), rmpv::Value::F64(6010.)),
                            ("DownTrendTrigger".into(), rmpv::Value::F64(5990.)),
                        ]),
                    )
                    .unwrap()
            })
            .collect::<Vec<_>>();
        let raw = rmpv::Value::Array(records);
        let projected = registry
            .project(&format!("{}[]", FuturesBarDataReadModel::KIND), &raw)
            .unwrap();
        let typed = Vec::<FuturesBarDataReadModel>::from_backend(&raw).unwrap();
        let expected = market_history_baseline::bars("ES", start, end, &projected).unwrap();
        let actual = market_history::bars("ES", start, end, &typed).unwrap();
        assert_eq!(expected.start, actual.start);
        assert_eq!(expected.end, actual.end);
        assert_eq!(expected.minimum, actual.minimum);
        assert_eq!(expected.maximum, actual.maximum);
        assert_eq!(expected.series.len(), actual.series.len());
        for (a, b) in expected.series.iter().zip(&actual.series) {
            assert_eq!(a.name, b.name);
            assert_eq!(a.color, b.color);
            assert_eq!(a.points, b.points);
        }
        group.throughput(Throughput::Elements(count as u64));
        group.bench_with_input(BenchmarkId::new("before_json", count), &raw, |b, raw| {
            b.iter(|| {
                let value = registry
                    .project(
                        &format!("{}[]", FuturesBarDataReadModel::KIND),
                        black_box(raw),
                    )
                    .unwrap();
                black_box(market_history_baseline::bars("ES", start, end, &value).unwrap())
            })
        });
        group.bench_with_input(BenchmarkId::new("after_typed", count), &raw, |b, raw| {
            b.iter(|| {
                let value = Vec::<FuturesBarDataReadModel>::from_backend(black_box(raw)).unwrap();
                black_box(market_history::bars("ES", start, end, &value).unwrap())
            })
        });
    }
    group.finish();
}

#[allow(dead_code)]
#[path = "../tests/support/strategy_display_baseline.rs"]
mod strategy_baseline;
fn strategy_models(c: &mut Criterion) {
    use ifm_ui_egui::services::strategy_display;
    let registry = Registry::compiled().unwrap();
    let bytes = include_bytes!("../tests/fixtures/backend/strategy-view.msgpack");
    let raw = codec::decode(bytes).unwrap();
    let before_row = strategy_baseline::row(&registry, &raw, 1000).unwrap();
    let before_detail = strategy_baseline::details(&registry, &raw).unwrap();
    let after_row =
        serde_json::to_value(strategy_display::row(&registry, &raw, 1000).unwrap()).unwrap();
    let after_detail =
        serde_json::to_value(strategy_display::details(&registry, &raw).unwrap()).unwrap();
    assert_eq!(before_row, after_row);
    for key in ["workflowId", "revision", "roots", "header"] {
        assert_eq!(before_detail[key], after_detail[key], "{key}");
    }
    let mut group = c.benchmark_group("ui_models/strategy");
    group.throughput(Throughput::Elements(1));
    group.bench_function("before_json", |b| {
        b.iter(|| {
            let raw = codec::decode(black_box(bytes)).unwrap();
            black_box((
                strategy_baseline::row(&registry, &raw, 1000).unwrap(),
                strategy_baseline::details(&registry, &raw).unwrap(),
            ))
        })
    });
    group.bench_function("after_typed", |b| {
        b.iter(|| {
            let raw = codec::decode(black_box(bytes)).unwrap();
            black_box((
                strategy_display::row(&registry, &raw, 1000).unwrap(),
                strategy_display::details(&registry, &raw).unwrap(),
            ))
        })
    });
    group.finish();
}

#[allow(dead_code)]
#[path = "../tests/support/inspection_display_baseline.rs"]
mod inspection_baseline;
fn inspection_models(c: &mut Criterion) {
    let registry = Registry::compiled().unwrap();
    let mut group = c.benchmark_group("ui_models/risk_inspection");
    group.throughput(Throughput::Elements(1));
    for (name, bytes) in [
        (
            "pending",
            include_bytes!("../tests/fixtures/backend/risk-observation.msgpack").as_slice(),
        ),
        (
            "completed",
            include_bytes!("../tests/fixtures/backend/risk-completed.msgpack").as_slice(),
        ),
    ] {
        let raw = codec::decode(bytes).unwrap();
        assert_eq!(
            inspection_baseline::risk(&registry, &raw).unwrap(),
            ifm_ui_egui::services::inspection_display::risk(&registry, &raw).unwrap()
        );
        group.bench_with_input(BenchmarkId::new("before_json", name), bytes, |b, bytes| {
            b.iter(|| {
                let raw = codec::decode(black_box(bytes)).unwrap();
                black_box(inspection_baseline::risk(&registry, &raw).unwrap())
            })
        });
        group.bench_with_input(BenchmarkId::new("after_typed", name), bytes, |b, bytes| {
            b.iter(|| {
                let raw = codec::decode(black_box(bytes)).unwrap();
                black_box(ifm_ui_egui::services::inspection_display::risk(&registry, &raw).unwrap())
            })
        });
    }
    group.finish();
}

#[allow(dead_code)]
#[path = "../tests/support/actor_health_baseline.rs"]
mod actor_health_baseline;
fn actor_health_models(c: &mut Criterion) {
    use ifm_ui_egui::{models::actor_health::ActorHealthSnapshot, state::actor_health};
    let bytes = include_bytes!("../tests/fixtures/backend/actor-health-http.json");
    let from = "2026-10-26T11:30:00Z".parse().unwrap();
    let before = actor_health_baseline::project(
        &actor_health_baseline::pascal(&serde_json::from_slice(bytes).unwrap()),
        from,
        &actor_health_baseline::ControlOptions::default(),
    )
    .unwrap();
    let after = actor_health::project(
        &serde_json::from_slice::<ActorHealthSnapshot>(bytes).unwrap(),
        from,
        &actor_health::ControlOptions::default(),
    )
    .unwrap();
    assert_eq!(before.result.context, after.result.context);
    assert_eq!(before.result.choices, after.result.choices);
    assert_eq!(before.result.rows.len(), after.result.rows.len());
    for (a, b) in before.result.rows.iter().zip(&after.result.rows) {
        assert_eq!(a.id, b.id);
        assert_eq!(a.fields, b.fields);
    }
    assert_eq!(before.targets.len(), after.targets.len());
    for (key, target) in before.targets {
        assert_eq!(target.generation, after.targets[&key].generation);
    }
    let mut group = c.benchmark_group("ui_models/actor_health");
    group.throughput(Throughput::Elements(1));
    group.bench_function("before_json", |b| {
        b.iter(|| {
            let raw = serde_json::from_slice(black_box(bytes)).unwrap();
            let value = actor_health_baseline::pascal(&raw);
            black_box(
                actor_health_baseline::project(
                    &value,
                    from,
                    &actor_health_baseline::ControlOptions::default(),
                )
                .unwrap(),
            )
        })
    });
    group.bench_function("after_typed", |b| {
        b.iter(|| {
            let value = serde_json::from_slice::<ActorHealthSnapshot>(black_box(bytes)).unwrap();
            black_box(
                actor_health::project(&value, from, &actor_health::ControlOptions::default())
                    .unwrap(),
            )
        })
    });
    group.finish();
}

#[allow(dead_code)]
#[path = "../tests/support/database_backup_baseline.rs"]
mod backup_baseline;
fn backup_models(c: &mut Criterion) {
    use ifm_ui_egui::state::database_backup;
    let registry = Registry::compiled().unwrap();
    let operation = include_bytes!("../tests/fixtures/backend/backup-operation.wire.msgpack");
    let policy = include_bytes!("../tests/fixtures/backend/backup-policy.wire.msgpack");
    let raw_operation = codec::decode(operation).unwrap();
    let raw_policy = codec::decode(policy).unwrap();
    assert_eq!(
        backup_baseline::operation_document(&registry, &raw_operation).unwrap(),
        serde_json::to_value(
            database_backup::operation_document(&registry, &raw_operation).unwrap()
        )
        .unwrap()
    );
    let before = backup_baseline::policy_document(&registry, &raw_policy).unwrap();
    let after = database_backup::policy_document(&registry, &raw_policy).unwrap();
    assert_eq!(before.0, serde_json::to_value(after.0).unwrap());
    assert_eq!(before.1, serde_json::to_value(after.1).unwrap());
    let mut group = c.benchmark_group("ui_models/database_backup");
    group.throughput(Throughput::Elements(1));
    group.bench_function("before_json", |b| {
        b.iter(|| {
            let operation = codec::decode(black_box(operation)).unwrap();
            let policy = codec::decode(black_box(policy)).unwrap();
            black_box((
                backup_baseline::operation_document(&registry, &operation).unwrap(),
                backup_baseline::policy_document(&registry, &policy).unwrap(),
            ))
        })
    });
    group.bench_function("after_typed", |b| {
        b.iter(|| {
            let operation = codec::decode(black_box(operation)).unwrap();
            let policy = codec::decode(black_box(policy)).unwrap();
            black_box((
                database_backup::operation_document(&registry, &operation).unwrap(),
                database_backup::policy_document(&registry, &policy).unwrap(),
            ))
        })
    });
    group.finish();
}

#[allow(dead_code)]
#[path = "../tests/support/broker_market_display_baseline.rs"]
mod broker_market_display_baseline;
#[allow(dead_code)]
#[path = "../tests/support/eod_baseline.rs"]
mod eod_baseline;
#[allow(dead_code)]
#[path = "../tests/support/option_chain_baseline.rs"]
mod option_chain_baseline;
#[allow(dead_code)]
#[path = "../tests/support/option_selection_baseline.rs"]
mod option_selection_baseline;
#[allow(dead_code)]
#[path = "../tests/support/scheduled_baseline.rs"]
mod scheduled_baseline;
fn remaining_models(c: &mut Criterion) {
    use ifm_ui_egui::{
        models::{trade_contracts::EvaluatedOptionChainReadModel, wire_types::FromBackend},
        services::{broker_market_display, option_chain, parameters},
        state::scheduled_tasks,
    };
    let registry = Registry::compiled().unwrap();
    let kind = "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel";
    let raw = codec::decode(include_bytes!(
        "../tests/fixtures/backend/broker-market-input.msgpack"
    ))
    .unwrap();
    let template = registry
        .wire_field(kind, "Contracts", &raw)
        .unwrap()
        .as_array()
        .unwrap()[0]
        .clone();
    let contracts_index = registry
        .describe(kind)
        .unwrap()
        .members
        .iter()
        .find(|m| m.0 == "Contracts")
        .unwrap()
        .1;
    let contract_kind =
        "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionContractReadModel";
    let id_index = registry
        .describe(contract_kind)
        .unwrap()
        .members
        .iter()
        .find(|m| m.0 == "ContractId")
        .unwrap()
        .1;
    let strike_index = registry
        .describe(contract_kind)
        .unwrap()
        .members
        .iter()
        .find(|m| m.0 == "Strike")
        .unwrap()
        .1;
    let mut group = c.benchmark_group("ui_models/option_chain");
    for count in [100usize, 1000] {
        let mut previous = raw.clone();
        let mut incoming = raw.clone();
        let rows: Vec<_> = (0..count)
            .map(|i| {
                let mut v = template.clone();
                let rmpv::Value::Array(fields) = &mut v else {
                    unreachable!()
                };
                fields[id_index] = format!("fixture-{i}").into();
                fields[strike_index] = format!("{}", 5000 + i).into();
                v
            })
            .collect();
        let rmpv::Value::Array(fields) = &mut previous else {
            unreachable!()
        };
        fields[contracts_index] = rmpv::Value::Array(rows.clone());
        let rmpv::Value::Array(fields) = &mut incoming else {
            unreachable!()
        };
        fields[contracts_index] = rmpv::Value::Array(rows.into_iter().skip(count / 4).collect());
        let previous_json = parameters::document(&registry, kind, &previous, true).unwrap();
        let previous_typed = EvaluatedOptionChainReadModel::from_backend(&previous).unwrap();
        let bytes = codec::raw(&incoming).unwrap();
        let before = |bytes: &[u8]| {
            let wire = codec::decode(bytes).unwrap();
            let next = parameters::document(&registry, kind, &wire, true).unwrap();
            let merged = option_chain_baseline::merge_display(Some(&previous_json), &next).unwrap();
            broker_market_display_baseline::market(&merged).unwrap()
        };
        let after = |bytes: &[u8]| {
            let wire = codec::decode(bytes).unwrap();
            let next = EvaluatedOptionChainReadModel::from_backend(&wire).unwrap();
            let merged = option_chain::merge_display(Some(&previous_typed), &next).unwrap();
            broker_market_display::market(&merged).unwrap()
        };
        assert_eq!(before(&bytes), serde_json::to_value(after(&bytes)).unwrap());
        group.throughput(Throughput::Elements(count as u64));
        group.bench_with_input(
            BenchmarkId::new("before_json", count),
            &bytes,
            |b, bytes| b.iter(|| black_box(before(black_box(bytes)))),
        );
        group.bench_with_input(
            BenchmarkId::new("after_typed", count),
            &bytes,
            |b, bytes| b.iter(|| black_box(after(black_box(bytes)))),
        );
    }
    group.finish();
    let bytes = include_bytes!("../tests/fixtures/backend/scheduled-run.wire.msgpack");
    let wire = codec::decode(bytes).unwrap();
    assert_eq!(
        scheduled_baseline::run_document(&registry, &wire).unwrap(),
        serde_json::to_value(scheduled_tasks::run_document(&registry, &wire).unwrap()).unwrap()
    );
    let mut group = c.benchmark_group("ui_models/scheduler");
    group.bench_function("before_json", |b| {
        b.iter(|| {
            black_box(
                scheduled_baseline::run_document(
                    &registry,
                    &codec::decode(black_box(bytes)).unwrap(),
                )
                .unwrap(),
            )
        })
    });
    group.bench_function("after_typed", |b| {
        b.iter(|| {
            black_box(
                scheduled_tasks::run_document(&registry, &codec::decode(black_box(bytes)).unwrap())
                    .unwrap(),
            )
        })
    });
    group.finish();
    use ifm_ui_egui::{
        models::eod::EodInputs,
        services::{trade_eod, trade_monitor_queries::Scope},
    };
    let scope = Scope {
        portfolio: 1,
        fund: 2,
        order: 3,
        trade: 4,
    };
    let date = chrono::NaiveDate::from_ymd_opt(2026, 10, 26).unwrap();
    let inputs:EodInputs=serde_json::from_value(serde_json::json!({"openPrice":"6000.1250","highPrice":"6010.5000","lowPrice":null,"closePrice":"6005.0000","volume":9223372036854775807i64,"tradePnl":"-50.2500","fundBalance":"123406.5300","lastStatusMessage":"Backend amounts"})).unwrap();
    let mut typed =
        trade_eod::Session::new(scope, "IronCondor".into(), "ESZ6".into(), date).unwrap();
    typed.display = Some(inputs.clone());
    typed.reference = "Exact reference".into();
    let old = eod_baseline::Session {
        scope,
        date,
        reference: typed.reference.clone(),
        display: serde_json::to_value(inputs).unwrap(),
        pending: false,
        completed: false,
    };
    assert_eq!(
        old.document(),
        serde_json::to_value(typed.document()).unwrap()
    );
    let mut group = c.benchmark_group("ui_models/eod");
    group.bench_function("before_json", |b| b.iter(|| black_box(old.document())));
    group.bench_function("after_typed", |b| b.iter(|| black_box(typed.document())));
    group.finish();
}
criterion_group! {
    name = benches;
    config = Criterion::default().sample_size(30).warm_up_time(Duration::from_secs(1)).measurement_time(Duration::from_secs(3));
    targets = production_models, chart_models, health_models, outlook_models, market_history_models, strategy_models, inspection_models, actor_health_models, backup_models, remaining_models
}
criterion_main!(benches);
