#[allow(dead_code)]
#[path = "support/option_chain_plan_baseline.rs"]
mod chain_before;
use ifm_ui_egui::services::option_selection;
#[allow(dead_code)]
#[path = "support/codec_plan_baseline.rs"]
mod codec_before;
// Parity and opt-in CPU measurements for the forward performance plan.
#[allow(dead_code)]
#[path = "support/monitor_cache_plan_baseline.rs"]
mod baseline;
use ifm_ui_egui::{
    backend::codec,
    models::{
        broker_presentation::MarketDisplay, cells::Cells, prepared_views::*, trade_contracts::*,
        wire_types::*,
    },
    state::monitor_cache::History,
};
use std::{hint::black_box, time::Instant};


/// Provides grid for the performance plan implementation.
fn grid(n: usize) -> MarketDisplay {
    let mut market = MarketDisplay::default();
    market.last_underlying_price = Some(DecimalText("5050.25".into()));
    market.contracts = Some(
        (0..n)
            .map(|i| EvaluatedOptionContractReadModel {
                contract_id: Some(i.to_string()),
                strike: Some(DecimalText(format!("{}", 5000. + (i / 2) as f64 * 0.25))),
                is_call: Some(i % 2 == 0),
                bid: Some(DecimalText("1.2500".into())),
                delta: Some(0.25),
                ..Default::default()
            })
            .collect(),
    );
    market
}


/// Provides old grid for the performance plan implementation.
fn old_grid(m: &MarketDisplay) -> Vec<(f64, Option<usize>, Option<usize>)> {
    let c = m.contracts.as_deref().unwrap_or_default();
    let mut strikes: Vec<_> = c.iter().filter_map(|c| c.cell("strike").number()).collect();
    strikes.sort_by(|a, b| b.total_cmp(a));
    strikes.dedup();
    strikes
        .into_iter()
        .map(|s| {
            (
                s,
                c.iter()
                    .position(|c| c.cell("strike").number() == Some(s) && c.is_call == Some(true)),
                c.iter()
                    .position(|c| c.cell("strike").number() == Some(s) && c.is_call == Some(false)),
            )
        })
        .collect()
}


/// Verifies that indexed option rows preserve first duplicates signed zero missing and nearest
/// ties.
#[test]
fn indexed_option_rows_preserve_first_duplicates_signed_zero_missing_and_nearest_ties() {
    let mut m = grid(200);
    let c = m.contracts.as_mut().unwrap();
    c.push(c[0].clone());
    for (strike, call) in [
        (Some("-0"), Some(true)),
        (Some("0.0"), Some(false)),
        (Some("bad"), Some(true)),
        (None, None),
    ] {
        c.push(EvaluatedOptionContractReadModel {
            strike: strike.map(|s| DecimalText(s.into())),
            is_call: call,
            ..Default::default()
        });
    }
    let prepared = OptionGrid::new(&m);
    let actual: Vec<_> = prepared
        .rows
        .iter()
        .enumerate()
        .map(|(i, r)| (prepared.strikes[i], r.call, r.put))
        .collect();
    assert_eq!(actual, old_grid(&m));
    for (i, c) in m.contracts.as_ref().unwrap().iter().enumerate() {
        for (j, k) in OptionGrid::FIELDS.iter().enumerate() {
            assert_eq!(prepared.formatted(i, c)[j], option_text(c, k));
        }
    }
    m.last_underlying_price = Some(DecimalText("5000.125".into()));
    let prepared = OptionGrid::new(&m);
    assert_eq!(prepared.strikes[prepared.nearest.unwrap()], 5000.25);
}


/// Verifies that output line slices keep rust lines semantics for unicode and crlf.
#[test]
fn output_line_slices_keep_rust_lines_semantics_for_unicode_and_crlf() {
    for text in ["", "\n", "a\n", "a\r\n\r\nb", "αβ\n東京\r\nend\r"] {
        let p = PreparedLines::new(text.into());
        assert_eq!(
            (0..p.len()).map(|i| p.line(i)).collect::<Vec<_>>(),
            text.lines().collect::<Vec<_>>()
        );
    }
}


/// Provides histories for the performance plan implementation.
fn histories(n: usize) -> (History, baseline::History) {
    let trade = EstablishedTradeDefinition::from_backend(
        &codec::decode(include_bytes!("fixtures/backend/monitor-trade.msgpack")).unwrap(),
    )
    .unwrap();
    let positions = Vec::<StrategyPositionSnapshot>::from_backend(
        &codec::decode(include_bytes!("fixtures/backend/monitor-positions.msgpack")).unwrap(),
    )
    .unwrap();
    let template = positions
        .iter()
        .find(|p| p.history_valuation.is_some() && p.phase != Some(StrategyPositionPhase::Open))
        .unwrap();
    let mut new = History::new(trade.clone(), "ShortIronCondor".into());
    let mut old = baseline::History::new(trade, "ShortIronCondor".into());
    for i in (0..n).rev() {
        let mut p = template.clone();
        p.history_valuation.as_mut().unwrap().value_date = Some(DateText(
            (chrono::NaiveDate::from_ymd_opt(2000, 1, 1).unwrap()
                + chrono::Duration::days(i as i64))
            .to_string(),
        ));
        assert_eq!(new.remember(p.clone()), old.remember(p));
    }
    (new, old)
}


/// Verifies that ordered history matches original insertion replacement and selection.
#[test]
fn ordered_history_matches_original_insertion_replacement_and_selection() {
    let (new, old) = histories(200);
    assert_eq!(new.rows(), old.rows());
    for i in [0, 1, 50, 200, 201] {
        assert_eq!(new.saved(i), old.saved(i));
    }
    assert_eq!(new.positions().count(), 200);
}


/// Provides old visible grid for the performance plan implementation.
fn old_visible_grid(m: &MarketDisplay) {
    let c = m.contracts.as_deref().unwrap_or_default();
    let mut strikes: Vec<_> = c.iter().filter_map(|c| c.cell("strike").number()).collect();
    strikes.sort_by(|a, b| b.total_cmp(a));
    strikes.dedup();
    for strike in strikes.iter().take(30) {
        let call = c
            .iter()
            .find(|c| c.cell("strike").number() == Some(*strike) && c.is_call == Some(true));
        let put = c
            .iter()
            .find(|c| c.cell("strike").number() == Some(*strike) && c.is_call == Some(false));
        let nearest = m
            .cell("lastUnderlyingPrice")
            .number()
            .and_then(|price| {
                strikes
                    .iter()
                    .min_by(|a, b| (*a - price).abs().total_cmp(&(*b - price).abs()))
            })
            .is_some_and(|s| s == strike);
        black_box(nearest);
        black_box(format!("{strike:.2}"));
        for contract in [call, put].into_iter().flatten() {
            for field in OptionGrid::FIELDS {
                black_box(option_text(contract, field));
            }
        }
    }
}


/// Provides measure for the performance plan implementation.
fn measure(mut f: impl FnMut(), count: usize) -> f64 {
    for _ in 0..5 {
        f();
    }
    let t = Instant::now();
    let mut actual = 0;
    loop {
        for _ in 0..count {
            f();
        }
        actual += count;
        if t.elapsed() >= std::time::Duration::from_millis(50) {
            break;
        }
    }
    t.elapsed().as_secs_f64() * 1000. / actual as f64
}


/// Provides paired for the performance plan implementation.
fn paired<F: FnMut(), G: FnMut()>(
    mut before: F,
    before_count: usize,
    mut after: G,
    after_count: usize,
) -> (f64, f64) {
    if std::env::var_os("IFM_PERF_REVERSE").is_some() {
        let b = measure(&mut after, after_count);
        let a = measure(&mut before, before_count);
        (a, b)
    } else {
        let a = measure(&mut before, before_count);
        let b = measure(&mut after, after_count);
        (a, b)
    }
}


/// Verifies that benchmark performance plan.
#[test]
#[ignore = "CPU benchmark; run alone using the development profile"]
fn benchmark_performance_plan() {
    let mut report = Vec::new();
    for n in [100, 1000, 10000] {
        let m = grid(n);
        let p = OptionGrid::new(&m);
        assert_eq!(
            old_grid(&m),
            p.rows
                .iter()
                .enumerate()
                .map(|(i, r)| (p.strikes[i], r.call, r.put))
                .collect::<Vec<_>>()
        );
        let (before, after) = paired(
            || {
                old_visible_grid(&m);
            },
            if n == 10000 { 10 } else { 100 },
            || {
                let fresh = OptionGrid::new(&m);
                for row in fresh.rows.iter().take(30) {
                    for index in [row.call, row.put].into_iter().flatten() {
                        black_box(fresh.formatted(index, &m.contracts.as_ref().unwrap()[index]));
                    }
                }
                black_box(fresh);
            },
            100,
        );
        let paint = measure(
            || {
                for r in p.rows.iter().take(30) {
                    black_box((r.call, r.put, &r.strike_text));
                    for index in [r.call, r.put].into_iter().flatten() {
                        black_box(p.formatted(index, &m.contracts.as_ref().unwrap()[index]));
                    }
                }
            },
            1000,
        );
        report.push(serde_json::json!({"case":"option_grid_visible_30_rows","count":n,"before_ms":before,"after_prepare_ms":after,"after_repaint_ms":paint}));
    }
    for n in [100, 1000, 10000] {
        let (new, old) = histories(n);
        assert_eq!(new.rows(), old.rows());
        let (before, after) = paired(
            || {
                black_box(old.saved(n / 2));
            },
            20,
            || {
                black_box(new.saved(n / 2));
            },
            100,
        );
        report.push(serde_json::json!({"case":"history_select","count":n,"before_ms":before,"after_ms":after}));
    }
    for n in [100, 10000, 100000] {
        let text = "A line αβ東京\r\n".repeat(n);
        let lines = PreparedLines::new(text.clone());
        let (before, after) = paired(
            || {
                let rows = text.lines().collect::<Vec<_>>();
                black_box(&rows[..rows.len().min(30)]);
            },
            100,
            || {
                for i in 0..lines.len().min(30) {
                    black_box(lines.line(i));
                }
            },
            1000,
        );
        report.push(serde_json::json!({"case":"output_repaint","count":n,"before_ms":before,"after_ms":after}));
    }
    for n in [100, 1000, 10000] {
        let raw = chain_wire(n);
        let expected = EvaluatedOptionChainReadModel::from_backend(&raw).unwrap();
        let bytes = codec_before::encode(&rmpv::Value::Array(vec![
            true.into(),
            rmpv::Value::Nil,
            rmpv::Value::Nil,
            raw,
        ]))
        .unwrap();
        assert_eq!(
            ifm_ui_egui::models::direct_backend::decode_service::<EvaluatedOptionChainReadModel>(
                &bytes
            )
            .unwrap(),
            expected
        );
        let (before, after) = paired(
            || {
                let v = codec_before::decode(&bytes).unwrap();
                let p = ifm_ui_egui::backend::contracts::into_service_result(v).unwrap();
                black_box(EvaluatedOptionChainReadModel::from_backend(&p).unwrap());
            },
            30,
            || {
                black_box(
                    ifm_ui_egui::models::direct_backend::decode_service::<
                        EvaluatedOptionChainReadModel,
                    >(&bytes)
                    .unwrap(),
                );
            },
            30,
        );
        report.push(serde_json::json!({"case":"chain_streaming_decode","count":n,"before_ms":before,"after_ms":after}));
    }
    {
        use ifm_ui_egui::{
            models::{row_data::RowData, trade_presentation::DisplayRecord},
            wire,
        };
        use std::sync::Arc;
        let plan = Arc::new(
            StrategyTradePlanSnapshot::from_backend(
                &codec::decode(include_bytes!("fixtures/backend/monitor-plan.msgpack")).unwrap(),
            )
            .unwrap(),
        );
        let (before, after) = paired(
            || {
                let bytes = wire::encode(&DisplayRecord {
                    value: plan.as_ref(),
                    history: true,
                })
                .unwrap();
                let mut v: StrategyTradePlanSnapshot = wire::decode(&bytes).unwrap();
                if let Some(d) = v.iron_condor_trade_plan_snapshot.as_mut() {
                    d.present_forward_risk();
                }
                black_box(v);
            },
            100,
            || {
                let payload = RowData::history_plan(plan.clone());
                black_box(payload.native_plan().unwrap());
                assert!(!payload.wire_is_materialized());
            },
            100,
        );
        let retained = RowData::history_plan(plan);
        retained.native_plan();
        let unchanged = measure(
            || {
                black_box(retained.clone().native_plan().unwrap());
            },
            10000,
        );
        report.push(serde_json::json!({"case":"native_plan_publication","before_ms":before,"after_ms":after,"unchanged_ms":unchanged}));
    }
    for n in [100, 1000, 10000] {
        let previous = EvaluatedOptionChainReadModel::from_backend(&chain_wire(n)).unwrap();
        let mut incoming = previous.clone();
        incoming.contracts.as_mut().unwrap().truncate(n * 3 / 4);
        assert_eq!(
            ifm_ui_egui::services::option_chain::merge_display(Some(&previous), &incoming).unwrap(),
            chain_before::merge_display(Some(&previous), &incoming).unwrap()
        );
        let (before, after) = paired(
            || {
                black_box(chain_before::merge_display(Some(&previous), &incoming).unwrap());
            },
            if n == 10000 { 5 } else { 30 },
            || {
                black_box(
                    ifm_ui_egui::services::option_chain::merge_display(Some(&previous), &incoming)
                        .unwrap(),
                );
            },
            30,
        );
        report.push(serde_json::json!({"case":"chain_merge_index","count":n,"before_ms":before,"after_ms":after}));
    }
    {
        use ifm_ui_egui::{
            models::{prepared_views::PreparedMarket, row_data::RowData},
            wire,
        };
        let market = grid(1000);
        let bytes = wire::encode(&market).unwrap();
        let payload = RowData::from(bytes.clone());
        let prepared = payload.presentation::<PreparedMarket>().unwrap();
        assert!(std::sync::Arc::ptr_eq(
            &prepared,
            &payload.presentation::<PreparedMarket>().unwrap()
        ));
        let (before, after) = paired(
            || {
                let m: MarketDisplay = wire::decode(&bytes).unwrap();
                black_box(PreparedMarket::new(m));
            },
            30,
            || {
                black_box(payload.presentation::<PreparedMarket>().unwrap());
            },
            10000,
        );
        report.push(serde_json::json!({"case":"unchanged_market_section","count":1000,"before_ms":before,"after_ms":after}));
    }
    {
        let envelope = rmpv::Value::Array(vec![
            true.into(),
            rmpv::Value::Nil,
            rmpv::Value::Nil,
            chain_wire(10000),
        ]);
        let bytes = std::sync::Arc::new(codec_before::raw(&envelope).unwrap());
        let runtime = tokio::runtime::Builder::new_multi_thread()
            .worker_threads(2)
            .enable_all()
            .build()
            .unwrap();
        let mut before = Vec::new();
        let mut after = Vec::new();
        for iteration in 0..12 {
            let order = if std::env::var_os("IFM_PERF_REVERSE").is_some() {
                [true, false]
            } else {
                [false, true]
            };
            for offload in order {
                let value = decode_timer_lateness(&runtime, bytes.clone(), offload);
                if iteration >= 2 {
                    if offload {
                        after.push(value);
                    } else {
                        before.push(value);
                    }
                }
            }
        }
        before.sort_by(f64::total_cmp);
        after.sort_by(f64::total_cmp);
        report.push(serde_json::json!({"case":"two_worker_decode_timer_lateness","count":10000,"before_ms":before[before.len()/2],"after_ms":after[after.len()/2],"before_p95_ms":before[before.len()-1],"after_p95_ms":after[after.len()-1]}));
    }
    {
        use std::io::Write;
        let root = std::env::var("IFM_PERFORMANCE_PLAN_REPORT")
            .map(std::path::PathBuf::from)
            .unwrap_or_else(|_| std::env::temp_dir().join("ifm-plan.json"));
        let dir = root
            .parent()
            .unwrap()
            .join(format!("diagnostic-bench-{}", uuid::Uuid::new_v4()));
        std::fs::create_dir_all(&dir).unwrap();
        let line =
            serde_json::json!({"event":"synthetic_timing","detail":"CPU publication diagnostic"})
                .to_string();
        let (mut before, mut after, mut total, mut setup) = (0., 0., 0., 0.);
        let order = if std::env::var_os("IFM_PERF_REVERSE").is_some() {
            [true, false]
        } else {
            [false, true]
        };
        for queued in order {
            if queued {
                let started = Instant::now();
                let writer = ifm_ui_egui::diagnostics::writer::DiagnosticWriter::new(
                    dir.join("after.jsonl"),
                    128,
                    5 * 1024 * 1024,
                )
                .unwrap();
                setup = started.elapsed().as_secs_f64() * 1000.;
                let started = Instant::now();
                for _ in 0..64 {
                    assert!(writer.offer(line.clone()));
                }
                after = started.elapsed().as_secs_f64() * 1000. / 64.;
                writer.shutdown(std::time::Duration::from_secs(2)).unwrap();
                total = started.elapsed().as_secs_f64() * 1000. / 64.;
                assert_eq!(writer.rejected(), 0);
            } else {
                let started = Instant::now();
                for _ in 0..64 {
                    let mut file = std::fs::OpenOptions::new()
                        .create(true)
                        .append(true)
                        .open(dir.join("before.jsonl"))
                        .unwrap();
                    writeln!(file, "{line}").unwrap();
                }
                before = started.elapsed().as_secs_f64() * 1000. / 64.;
            }
        }
        assert_eq!(
            std::fs::read_to_string(dir.join("before.jsonl")).unwrap(),
            std::fs::read_to_string(dir.join("after.jsonl")).unwrap()
        );
        report.push(serde_json::json!({"case":"diagnostic_producer","count":64,"before_ms":before,"after_ms":after,"after_including_flush_ms":total,"after_startup_once_ms":setup}));
    }
    let text = serde_json::to_string_pretty(&report).unwrap();
    println!("{text}");
    if let Ok(path) = std::env::var("IFM_PERFORMANCE_PLAN_REPORT") {
        std::fs::write(path, text).unwrap();
    }
}


/// Verifies that native plan is shared without wire materialization and matches compatibility
/// projection.
#[test]
fn native_plan_is_shared_without_wire_materialization_and_matches_compatibility_projection() {
    use ifm_ui_egui::{
        models::{row_data::RowData, trade_presentation::DisplayRecord},
        wire,
    };
    use std::sync::Arc;
    let raw = StrategyTradePlanSnapshot::from_backend(
        &codec::decode(include_bytes!("fixtures/backend/monitor-plan.msgpack")).unwrap(),
    )
    .unwrap();
    let expected = wire::encode(&DisplayRecord {
        value: &raw,
        history: true,
    })
    .unwrap();
    let payload = RowData::history_plan(Arc::new(raw));
    let cloned = payload.clone();
    assert!(cloned.ptr_eq(&payload));
    assert!(!payload.wire_is_materialized());
    let actual = payload.native_plan().unwrap();
    assert!(Arc::ptr_eq(&actual, &cloned.native_plan().unwrap()));
    assert!(!payload.wire_is_materialized());
    let mut decoded: StrategyTradePlanSnapshot = wire::decode(&expected).unwrap();
    if let Some(d) = decoded.iron_condor_trade_plan_snapshot.as_mut() {
        d.present_forward_risk();
    }
    assert_eq!(*actual, decoded);
    assert_eq!(payload.as_ref(), expected);
    assert!(cloned.wire_is_materialized());
    let encoded = wire::encode(&payload).unwrap();
    let restored: RowData = wire::decode(&encoded).unwrap();
    assert_eq!(restored.as_ref(), expected);
    assert!(restored.native_plan().is_none());
}


/// Provides chain wire for the performance plan implementation.
fn chain_wire(n: usize) -> rmpv::Value {
    let mut wire = codec::decode(include_bytes!(
        "fixtures/backend/broker-market-input.msgpack"
    ))
    .unwrap();
    let rmpv::Value::Array(fields) = &mut wire else {
        panic!("fixture")
    };
    let template = fields[7].as_array().unwrap()[0].clone();
    fields[7] = rmpv::Value::Array(
        (0..n)
            .map(|i| {
                let mut row = template.clone();
                let rmpv::Value::Array(f) = &mut row else {
                    panic!()
                };
                f[0] = i.to_string().into();
                row
            })
            .collect(),
    );
    wire
}


/// Verifies that streaming chain matches managed scalars compression tails missing and error
/// results.
#[test]
fn streaming_chain_matches_managed_scalars_compression_tails_missing_and_error_results() {
    use ifm_ui_egui::{backend::contracts::into_service_result, models::direct_backend::decode_service};
    for n in [0, 1, 1000] {
        let mut raw = chain_wire(n);
        if let rmpv::Value::Array(f) = &mut raw {
            f.push(rmpv::Value::Array(vec!["future".into()]));
            if let rmpv::Value::Array(rows) = &mut f[7] {
                for row in rows {
                    if let rmpv::Value::Array(fields) = row {
                        fields.push("retired-or-future-slot".into());
                    }
                }
            }
        }
        let expected = EvaluatedOptionChainReadModel::from_backend(&raw).unwrap();
        let envelope = rmpv::Value::Array(vec![
            true.into(),
            rmpv::Value::Nil,
            rmpv::Value::Nil,
            raw,
            "extra-envelope-field".into(),
        ]);
        for bytes in [
            codec::raw(&envelope).unwrap(),
            codec::encode(&envelope).unwrap(),
        ] {
            let actual: EvaluatedOptionChainReadModel = decode_service(&bytes).unwrap();
            assert_eq!(actual, expected);
            let mut trailing = bytes;
            trailing.push(0);
            assert!(decode_service::<EvaluatedOptionChainReadModel>(&trailing).is_err());
        }
    }
    let raw = rmpv::Value::Array(vec!["ES".into()]);
    let expected = EvaluatedOptionChainReadModel::from_backend(&raw).unwrap();
    let envelope = rmpv::Value::Array(vec![true.into(), rmpv::Value::Nil, rmpv::Value::Nil, raw]);
    assert_eq!(
        decode_service::<EvaluatedOptionChainReadModel>(&codec::raw(&envelope).unwrap()).unwrap(),
        expected
    );
    let failed = rmpv::Value::Array(vec![
        false.into(),
        9101.into(),
        "fixture rejection".into(),
        rmpv::Value::Nil,
    ]);
    assert_eq!(
        decode_service::<EvaluatedOptionChainReadModel>(&codec::encode(&failed).unwrap())
            .unwrap_err(),
        into_service_result(failed).unwrap_err()
    );
    for invalid in [
        rmpv::Value::Nil,
        rmpv::Value::Array(vec![]),
        rmpv::Value::Array(vec![
            true.into(),
            rmpv::Value::Nil,
            rmpv::Value::Nil,
            false.into(),
        ]),
    ] {
        assert!(
            decode_service::<EvaluatedOptionChainReadModel>(&codec::raw(&invalid).unwrap())
                .is_err()
        );
    }
}


/// Verifies that indexed chain merge preserves stale retention duplicates and scope.
#[test]
fn indexed_chain_merge_preserves_stale_retention_duplicates_and_scope() {
    use ifm_ui_egui::services::option_chain;
    let previous = EvaluatedOptionChainReadModel::from_backend(&chain_wire(1000)).unwrap();
    let mut incoming = previous.clone();
    let rows = incoming.contracts.as_mut().unwrap();
    rows.truncate(500);
    rows[0].bid = None;
    rows[0].delta = None;
    rows[0].is_stale = Some(false);
    rows.push(rows[0].clone());
    assert_eq!(
        option_chain::merge_display(Some(&previous), &incoming).unwrap(),
        chain_before::merge_display(Some(&previous), &incoming).unwrap()
    );
    incoming.underlying_contract_id = Some("another-contract".into());
    assert_eq!(
        option_chain::merge_display(Some(&previous), &incoming).unwrap(),
        incoming
    );
}


/// Verifies that streaming contract keeps decimal scale integer extremes and invalid scalar
/// rejection.
#[test]
fn streaming_contract_keeps_decimal_scale_integer_extremes_and_invalid_scalar_rejection() {
    use ifm_ui_egui::models::direct_backend::decode_service;
    for decimal in [
        "0001.2300",
        "79228162514264337593543950335",
        "79228162514264337593543950336",
        "0.00000000000000000000000000001",
        "invalid",
    ] {
        let mut raw = chain_wire(1);
        let rmpv::Value::Array(root) = &mut raw else {
            unreachable!()
        };
        let stamp = root[6].clone();
        let rmpv::Value::Array(rows) = &mut root[7] else {
            unreachable!()
        };
        let rmpv::Value::Array(fields) = &mut rows[0] else {
            unreachable!()
        };
        fields[1] = decimal.into();
        fields[17] = i64::MAX.into();
        fields[20] = stamp;
        let expected = EvaluatedOptionChainReadModel::from_backend(&raw);
        let envelope =
            rmpv::Value::Array(vec![true.into(), rmpv::Value::Nil, rmpv::Value::Nil, raw]);
        let actual =
            decode_service::<EvaluatedOptionChainReadModel>(&codec::encode(&envelope).unwrap());
        match expected {
            Ok(expected) => assert_eq!(actual.unwrap(), expected),
            Err(_) => assert!(actual.is_err()),
        }
    }
    let mut tail = rmpv::Value::Nil;
    for _ in 0..100 {
        tail = rmpv::Value::Array(vec![tail]);
    }
    let envelope = rmpv::Value::Array(vec![
        true.into(),
        rmpv::Value::Nil,
        rmpv::Value::Nil,
        chain_wire(1),
        tail,
    ]);
    let bytes = codec::raw(&envelope).unwrap();
    assert!(codec_before::decode(&bytes).is_err());
    assert!(decode_service::<EvaluatedOptionChainReadModel>(&bytes).is_err());
}


/// Verifies that inactive native plan cache does not keep payloads alive.
#[test]
fn inactive_native_plan_cache_does_not_keep_payloads_alive() {
    use ifm_ui_egui::models::row_data::RowData;
    use std::sync::Arc;
    let raw = Arc::new(
        StrategyTradePlanSnapshot::from_backend(
            &codec::decode(include_bytes!("fixtures/backend/monitor-plan.msgpack")).unwrap(),
        )
        .unwrap(),
    );
    let source = Arc::downgrade(&raw);
    let payload = RowData::history_plan(raw);
    let weak = payload.downgrade();
    assert!(
        source.upgrade().is_none(),
        "History views must not retain the full authoritative source snapshot"
    );
    let display = payload.native_plan().unwrap();
    assert!(weak.upgrade().is_some());
    drop(payload);
    assert!(
        weak.upgrade().is_none(),
        "An inactive cache must not retain payloads or wire buffers"
    );
    assert!(display.is_history_view());
}


/// Verifies that typed row cache reuses only its immutable payload and requested schema.
#[test]
fn typed_row_cache_reuses_only_its_immutable_payload_and_requested_schema() {
    use ifm_ui_egui::{models::row_data::RowData, wire};
    use std::sync::Arc;
    let market = grid(100);
    let payload = RowData::from(wire::encode(&market).unwrap());
    let cloned = payload.clone();
    let first = payload.presentation::<MarketDisplay>().unwrap();
    let second = cloned.presentation::<MarketDisplay>().unwrap();
    assert!(Arc::ptr_eq(&first, &second));
    let _other = payload.presentation::<ifm_ui_egui::models::broker_presentation::MarketMetrics>();
    assert!(Arc::ptr_eq(
        &first,
        &payload.presentation::<MarketDisplay>().unwrap()
    ));
    let mut changed = market;
    changed.last_underlying_price = Some(DecimalText("6000.2500".into()));
    let replacement = RowData::from(wire::encode(&changed).unwrap());
    let changed = replacement.presentation::<MarketDisplay>().unwrap();
    assert!(!Arc::ptr_eq(&first, &changed));
    assert_eq!(
        changed.last_underlying_price.as_ref().unwrap().as_str(),
        "6000.2500"
    );
    let weak = Arc::downgrade(&first);
    drop(first);
    drop(second);
    drop(payload);
    drop(cloned);
    assert!(weak.upgrade().is_none());
}


// Reproduce contention with actual large decode work on the native runtime's
// two async workers. This measures a synthetic service timer, not live UI/GPU latency.
fn decode_timer_lateness(
    runtime: &tokio::runtime::Runtime,
    bytes: std::sync::Arc<Vec<u8>>,
    offload: bool,
) -> f64 {
    runtime.block_on(async move {
        let gate = std::sync::Arc::new(tokio::sync::Barrier::new(3));
        let due = tokio::time::Instant::now() + std::time::Duration::from_millis(1);
        let mut jobs = Vec::new();
        for _ in 0..2 {
            let gate = gate.clone();
            let bytes = bytes.clone();
            jobs.push(tokio::spawn(async move {
                gate.wait().await;
                let result: EvaluatedOptionChainReadModel = if offload {
                    ifm_ui_egui::services::compute::decode(
                        std::borrow::Cow::Borrowed(bytes.as_slice()),
                        tokio::time::Instant::now() + std::time::Duration::from_secs(10),
                    )
                    .await
                    .unwrap()
                } else {
                    ifm_ui_egui::models::direct_backend::decode_plain_service(&bytes).unwrap()
                };
                black_box(result);
            }));
        }
        let timer = tokio::spawn(async move {
            gate.wait().await;
            tokio::time::sleep_until(due).await;
            tokio::time::Instant::now()
                .saturating_duration_since(due)
                .as_secs_f64()
                * 1000.
        });
        let late = timer.await.unwrap();
        for job in jobs {
            job.await.unwrap();
        }
        late
    })
}
