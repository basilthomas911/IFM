//! Stateful option scheduler acceptance: source ordering, refresh boundaries,
//! demand/fairness, cancellation and context/physical-generation fencing.
use ifm_market_data_native::{
    option_chain::{
        coalesced::{Coalesced, Context, Failure},
        qualification::{Decimal, Quote, Request},
    },
    pricing::{Calculator, Request as Model, Settings},
    runtime::Deadline,
};
use std::time::Duration;
use tokio_util::sync::CancellationToken;
const AT: i64 = 17_000_000_000_000_000;
fn decimal(value: u64) -> Decimal {
    Decimal {
        bits: [value as u32, (value >> 32) as u32, 0, 0],
    }
}
fn quote(price: u64, event: i64, sequence: i64) -> Quote {
    Quote {
        generation_low: 1,
        event,
        received: event,
        sequence,
        bid: decimal(price),
        ask: decimal(price),
        bid_size: decimal(10),
        ask_size: decimal(10),
        ..Default::default()
    }
}
fn context(identity: u128) -> Context {
    Context {
        identity,
        underlying: 9,
        expiration: AT + 30 * 864000000000,
        days_in_year: 365,
        valid_until: AT + 864000000000,
        model: Model {
            forward: 0.0,
            strike: 5000.0,
            time: 0.0,
            rate: 0.04,
            call: true,
            american: false,
            futures_style: false,
        },
        qualification: Request {
            size: 304,
            flags: 3 | 4 | 8 | 16,
            generation_low: 1,
            maximum_age: 5000,
            maximum_skew: 2000,
            maximum_lead: 0,
            strike: decimal(5000),
            definition_strike: decimal(5000),
            ..Default::default()
        },
    }
}
async fn pass(engine: &Coalesced, risk: bool, at: i64) {
    engine
        .pass(
            risk,
            at,
            |_| Some(quote(5000, at, 1)),
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
}
#[tokio::test]
async fn continuous_ingress_does_not_starve_completed_risk_or_relabel_its_source() {
    let engine = Coalesced::new(1, 1, 5000).unwrap();
    engine.register(1, 100, context(1), true).unwrap();
    engine.observe(1, 100, quote(100, AT, 1)).unwrap();
    // Deterministically advance ingress after the pass captures its work. This
    // previously discarded every calculation during a sufficiently busy feed.
    engine
        .pass(
            true,
            AT,
            |_| {
                engine.observe(1, 100, quote(101, AT + 1, 2)).unwrap();
                Some(quote(5000, AT, 1))
            },
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
    let completed = engine.completed_risk(1, AT + 1).unwrap().unwrap();
    assert!(completed.risk.is_some());
    assert_eq!(completed.option.sequence, 1);
    assert_eq!(completed.option.bid, decimal(100));
    assert!(engine.ready(AT + 1).unwrap());
    assert_eq!(
        engine.read(1, true, AT + 1).unwrap().unwrap().failure,
        Some(Failure::CalculationPending)
    );
    pass(&engine, true, AT + 1).await;
    let next = engine.completed_risk(1, AT + 1).unwrap().unwrap();
    assert_eq!(next.option.sequence, 2);
    assert_eq!(next.option.bid, decimal(101));
    assert!(
        engine
            .completed_risk(1, AT + 50_000_002)
            .unwrap()
            .unwrap()
            .risk
            .is_none()
    );
    engine.rebind(101).unwrap();
    assert!(engine.completed_risk(1, AT + 1).unwrap().is_none());
}
#[tokio::test]
async fn selection_reuses_iv_risk_is_independent_and_exact_refresh_boundary_is_preserved() {
    let engine = Coalesced::new(2, 16, 5000).unwrap();
    engine.register(1, 100, context(1), true).unwrap();
    engine.observe(1, 100, quote(100, AT, 1)).unwrap();
    pass(&engine, false, AT).await;
    let first = engine.read(1, false, AT).unwrap().unwrap();
    let selected = first.selection.unwrap();
    assert!(selected.price > 0.0);
    assert!(selected.delta > 0.0);
    assert!(first.risk.is_none());
    assert_eq!(engine.counters().unwrap(), (1, 1, 1));
    // A newer market mark does not trigger another IV solve before 5 seconds.
    engine.observe(1, 100, quote(200, AT + 10000, 2)).unwrap();
    pass(&engine, false, AT + 10000).await;
    assert_eq!(engine.counters().unwrap(), (2, 2, 1));
    assert_eq!(
        engine.read(1, false, AT + 10000).unwrap().unwrap().iv_event,
        Some(AT)
    );
    pass(&engine, true, AT + 10000).await;
    let risk = engine
        .read(1, true, AT + 10000)
        .unwrap()
        .unwrap()
        .risk
        .unwrap();
    assert_eq!(engine.counters().unwrap(), (2, 2, 1));
    assert!((risk.price - 200.0).abs() < 1e-8);
    // Read freshness is >5000; refresh is >=5000, both relative to IV event.
    let boundary = AT + 50_000_000;
    assert!(
        engine
            .read(1, false, boundary)
            .unwrap()
            .unwrap()
            .failure
            .is_none()
    );
    assert_eq!(
        engine
            .read(1, false, boundary + 1)
            .unwrap()
            .unwrap()
            .failure,
        Some(Failure::Stale)
    );
    engine.observe(1, 100, quote(200, boundary, 3)).unwrap();
    pass(&engine, false, boundary).await;
    assert_eq!(engine.counters().unwrap(), (3, 3, 2));
    assert_eq!(
        engine.read(1, false, boundary).unwrap().unwrap().iv_event,
        Some(boundary)
    );
    // A cached full-risk result cannot remain current after a newer quote.
    assert_eq!(
        engine.read(1, true, boundary).unwrap().unwrap().failure,
        Some(Failure::CalculationPending)
    );
}
#[tokio::test]
async fn timestamp_first_ingress_generation_fences_and_context_refresh_discard_old_results() {
    let engine = Coalesced::new(1, 1, 5000).unwrap();
    engine.register(1, 100, context(1), true).unwrap();
    assert!(engine.observe(1, 100, quote(100, AT, 100)).unwrap());
    assert!(!engine.observe(1, 100, quote(200, AT - 1, 101)).unwrap());
    assert!(!engine.observe(1, 100, quote(200, AT, 100)).unwrap());
    assert!(engine.observe(1, 100, quote(110, AT + 1, 1)).unwrap());
    pass(&engine, false, AT + 1).await;
    assert_eq!(
        engine
            .read(1, false, AT + 1)
            .unwrap()
            .unwrap()
            .option
            .sequence,
        1
    );
    engine.register(1, 101, context(2), true).unwrap();
    assert!(engine.read(1, false, AT + 1).unwrap().is_none());
    assert!(!engine.observe(1, 100, quote(100, AT + 2, 102)).unwrap());
    assert!(engine.observe(1, 101, quote(100, AT + 2, 1)).unwrap());
    engine.remove(1, 100).unwrap();
    pass(&engine, false, AT + 2).await;
    assert_eq!(engine.read(1, false, AT + 2).unwrap().unwrap().context, 2);
    engine.register(1, 101, context(3), true).unwrap();
    pass(&engine, false, AT + 2).await;
    assert_eq!(engine.read(1, false, AT + 2).unwrap().unwrap().context, 3);
    engine.remove(1, 101).unwrap();
    assert!(engine.read(1, false, AT + 2).unwrap().is_none());
}
#[tokio::test]
async fn bounded_fair_batches_cover_every_registered_contract_and_only_required_risk() {
    let engine = Coalesced::new(40, 16, 5000).unwrap();
    for id in 1..=40 {
        engine
            .register(id, 100, context(id.into()), id % 2 == 0)
            .unwrap();
        engine.observe(id, 100, quote(100, AT, 1)).unwrap();
    }
    assert!(engine.register(41, 100, context(41), true).is_err());
    for _ in 0..3 {
        pass(&engine, false, AT).await;
    }
    for id in 1..=40 {
        assert!(engine.read(id, false, AT).unwrap().is_some(), "{id}");
    }
    for _ in 0..2 {
        pass(&engine, true, AT).await;
    }
    for id in 1..=40 {
        assert_eq!(engine.read(id, true, AT).unwrap().is_some(), id % 2 == 0);
    }
    engine.require_risk(1, true).unwrap();
    for _ in 0..2 {
        pass(&engine, true, AT).await;
    }
    assert!(engine.read(1, true, AT).unwrap().is_some());
}
#[tokio::test]
async fn absent_underlying_clears_cached_results_and_invalid_quotes_never_manufacture_risk() {
    let engine = Coalesced::new(1, 16, 5000).unwrap();
    engine.register(1, 100, context(1), true).unwrap();
    engine.observe(1, 100, quote(100, AT, 1)).unwrap();
    pass(&engine, false, AT).await;
    pass(&engine, true, AT).await;
    engine
        .pass(
            false,
            AT,
            |_| None,
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
    assert!(engine.read(1, false, AT).unwrap().is_none());
    assert!(engine.read(1, true, AT).unwrap().is_none());
    let mut bad = quote(100, AT + 1, 2);
    bad.ask = decimal(99);
    engine.observe(1, 100, bad).unwrap();
    pass(&engine, false, AT + 1).await;
    // ReadSelection applies the baseline's current-source stale override.
    let value = engine.read(1, false, AT + 1).unwrap().unwrap();
    assert!(value.selection.is_none());
    assert_eq!(value.failure, Some(Failure::Stale));
    pass(&engine, true, AT + 1).await;
    assert!(
        engine
            .read(1, true, AT + 1)
            .unwrap()
            .unwrap()
            .risk
            .is_none()
    );
}
#[tokio::test]
async fn expired_or_cancelled_pass_does_not_commit_and_old_context_cannot_win_a_refresh_race() {
    let engine = Coalesced::new(1, 16, 5000).unwrap();
    let mut c = context(1);
    c.model.american = true;
    engine.register(1, 100, c, true).unwrap();
    engine.observe(1, 100, quote(100, AT, 1)).unwrap();
    let cancellation = CancellationToken::new();
    cancellation.cancel();
    assert!(
        engine
            .pass(
                false,
                AT,
                |_| Some(quote(5000, AT, 1)),
                &cancellation,
                Deadline::recovery()
            )
            .await
            .is_err()
    );
    assert!(
        engine
            .pass(
                false,
                AT,
                |_| Some(quote(5000, AT, 1)),
                &CancellationToken::new(),
                Deadline::after(Duration::ZERO)
            )
            .await
            .is_err()
    );
    assert!(engine.read(1, false, AT).unwrap().is_none());
    // Numerical work yields; replacing the registration during an American IV
    // tree must fence the obsolete result even when source time is unchanged.
    let active = CancellationToken::new();
    let calculation = engine.pass(
        false,
        AT,
        |_| Some(quote(5000, AT, 1)),
        &active,
        Deadline::recovery(),
    );
    let replace = async {
        tokio::task::yield_now().await;
        engine.register(1, 101, context(2), true).unwrap();
    };
    let (result, ()) = tokio::join!(calculation, replace);
    result.unwrap();
    assert!(engine.read(1, false, AT).unwrap().is_none());
    assert!(!engine.observe(1, 100, quote(100, AT + 1, 2)).unwrap());
}
#[tokio::test]
async fn risk_matches_existing_qualified_kernel_without_using_coalesced_iv() {
    let engine = Coalesced::new(1, 1, 5000).unwrap();
    let c = context(1);
    engine.register(1, 100, c, true).unwrap();
    engine.observe(1, 100, quote(100, AT, 1)).unwrap();
    pass(&engine, true, AT).await;
    let actual = engine.read(1, true, AT).unwrap().unwrap().risk.unwrap();
    let expected = Calculator::new(Settings::default())
        .unwrap()
        .implied(
            Model {
                forward: 5000.0,
                time: 30.0 / 365.0,
                ..c.model
            },
            100.0,
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
    assert_eq!(actual, expected);
}

#[tokio::test]
async fn full_sweep_has_no_duplicate_tail_and_owned_loops_stop_without_timer_backlog() {
    use std::sync::{
        Arc,
        atomic::{AtomicUsize, Ordering},
    };
    let engine = Arc::new(Coalesced::new(40, 16, 5000).unwrap());
    for id in 1..=40 {
        engine.register(id, 100, context(id.into()), true).unwrap();
        engine.observe(id, 100, quote(100, AT, 1)).unwrap();
    }
    assert_eq!(
        engine
            .sweep(
                false,
                || Ok(AT),
                |_| Some(quote(5000, AT, 1)),
                &CancellationToken::new(),
                Deadline::recovery()
            )
            .await
            .unwrap(),
        40
    );
    assert_eq!(engine.counters().unwrap(), (40, 40, 40));
    let calls = Arc::new(AtomicUsize::new(0));
    let stopping = CancellationToken::new();
    let owned = engine.clone();
    let count = calls.clone();
    let cancel = stopping.clone();
    let worker = tokio::spawn(async move {
        owned
            .run(
                10,
                20,
                || Ok(AT),
                |_| {
                    count.fetch_add(1, Ordering::SeqCst);
                    Some(quote(5000, AT, 1))
                },
                &cancel,
            )
            .await
    });
    tokio::time::timeout(Duration::from_secs(2), async {
        while calls.load(Ordering::SeqCst) == 0 {
            tokio::task::yield_now().await;
        }
    })
    .await
    .unwrap();
    stopping.cancel();
    tokio::time::timeout(Duration::from_secs(1), worker)
        .await
        .unwrap()
        .unwrap()
        .unwrap();
    let after = calls.load(Ordering::SeqCst);
    tokio::time::sleep(Duration::from_millis(50)).await;
    assert_eq!(calls.load(Ordering::SeqCst), after);
    // Clock failures propagate to recovery rather than leaving healthy-looking
    // loops alive. try_join drops the other timer and all in-progress work.
    assert_eq!(
        engine
            .run(
                10,
                20,
                || Err("Clock owner failed"),
                |_| Some(quote(5000, AT, 1)),
                &CancellationToken::new()
            )
            .await,
        Err("Clock owner failed")
    );
}

#[tokio::test]
async fn native_wire_ingress_preserves_nullable_prices_and_never_readmits_a_retired_source() {
    use ifm_market_data_native::dataset::records::{Header, Quote as WireQuote};
    let engine = Coalesced::new(1, 1, 5000).unwrap();
    engine.register(1, 100, context(1), true).unwrap();
    let mut wire = WireQuote {
        header: Header {
            instrument: 1,
            publisher: 3,
            flags: 0,
            event_ns: AT * 100,
            received_ns: AT * 100,
            sequence: 1,
            schema: 1,
        },
        bid: 100_000_000_000,
        ask: 100_000_000_000,
        bid_size: 10,
        ask_size: 10,
        bid_count: 2,
        ask_count: 3,
    };
    assert!(engine.observe_wire(100, wire, AT).unwrap());
    pass(&engine, true, AT).await;
    assert!(engine.read(1, true, AT).unwrap().unwrap().risk.is_some());
    wire.header.sequence = 2;
    wire.header.event_ns += 100;
    wire.ask = i64::MAX;
    assert!(engine.observe_wire(100, wire, AT + 1).unwrap());
    assert_eq!(
        engine.read(1, true, AT + 1).unwrap().unwrap().failure,
        Some(Failure::CalculationPending)
    );
    assert_eq!(
        engine
            .pass(
                true,
                AT + 1,
                |_| Some(quote(5000, AT + 1, 1)),
                &CancellationToken::new(),
                Deadline::recovery()
            )
            .await
            .unwrap(),
        0
    );
    engine.rebind(0).unwrap();
    assert!(engine.read(1, true, AT + 1).unwrap().is_none());
    assert!(!engine.observe(1, 0, quote(100, AT + 2, 3)).unwrap());
    assert!(!engine.observe_wire(100, wire, AT + 1).unwrap());
    engine.rebind(101).unwrap();
    wire.ask = 100_000_000_000;
    assert!(!engine.observe_wire(100, wire, AT + 1).unwrap());
    assert!(engine.observe_wire(101, wire, AT + 1).unwrap());
    pass(&engine, true, AT + 1).await;
    assert_eq!(
        engine
            .read(1, true, AT + 1)
            .unwrap()
            .unwrap()
            .source_generation,
        101
    );
    assert_eq!(
        engine
            .run_checked(
                10,
                20,
                || Ok(AT + 1),
                |_| Err("Underlying writer failed"),
                &CancellationToken::new()
            )
            .await,
        Err("Underlying writer failed")
    );
}

#[tokio::test]
async fn actual_360_is_preserved_instead_of_silently_assuming_actual_365() {
    let engine = Coalesced::new(1, 1, 5000).unwrap();
    let mut c = context(1);
    c.days_in_year = 360;
    engine.register(1, 100, c, true).unwrap();
    engine.observe(1, 100, quote(100, AT, 1)).unwrap();
    pass(&engine, true, AT).await;
    let actual = engine.read(1, true, AT).unwrap().unwrap().risk.unwrap();
    let expected = Calculator::new(Settings::default())
        .unwrap()
        .implied(
            Model {
                forward: 5000.0,
                time: 30.0 / 360.0,
                ..c.model
            },
            100.0,
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .await
        .unwrap();
    assert_eq!(actual, expected);
    c.days_in_year = 364;
    assert!(engine.register(1, 100, c, true).is_err());
    // Invalid admission is atomic; it does not destroy the prior valid result.
    assert!(engine.read(1, true, AT).unwrap().unwrap().risk.is_some());
}

#[tokio::test]
async fn complete_context_refresh_is_atomic_rejects_redefinition_and_fences_cached_iv() {
    use ifm_market_data_native::option_chain::coalesced::Refresh;
    let engine = Coalesced::new(2, 16, 5000).unwrap();
    for id in 1..=2 {
        engine.register(id, 100, context(id.into()), true).unwrap();
        engine.observe(id, 100, quote(100, AT, 1)).unwrap();
    }
    pass(&engine, false, AT).await;
    pass(&engine, true, AT).await;
    let first = engine.read(1, true, AT).unwrap().unwrap();
    let second = engine.read(2, true, AT).unwrap().unwrap();
    let mut a = context(101);
    a.model.rate = 0.05;
    let mut b = context(102);
    b.model.rate = 0.05;
    let invalid = [
        Refresh {
            instrument: 1,
            expected: 1,
            context: a,
        },
        Refresh {
            instrument: 2,
            expected: 999,
            context: b,
        },
    ];
    assert!(engine.refresh(&invalid).is_err());
    assert!(std::sync::Arc::ptr_eq(
        &first,
        &engine.read(1, true, AT).unwrap().unwrap()
    ));
    assert!(std::sync::Arc::ptr_eq(
        &second,
        &engine.read(2, true, AT).unwrap().unwrap()
    ));
    assert!(engine.refresh(&invalid[..1]).is_err());
    let mut relaxed = b;
    relaxed.qualification.maximum_skew += 1;
    assert!(
        engine
            .refresh(&[
                Refresh {
                    instrument: 1,
                    expected: 1,
                    context: a
                },
                Refresh {
                    instrument: 2,
                    expected: 2,
                    context: relaxed
                }
            ])
            .is_err()
    );
    let mut redefined = b;
    redefined.model.call = false;
    assert!(
        engine
            .refresh(&[
                Refresh {
                    instrument: 1,
                    expected: 1,
                    context: a
                },
                Refresh {
                    instrument: 2,
                    expected: 2,
                    context: redefined
                }
            ])
            .is_err()
    );
    let updates = [
        Refresh {
            instrument: 1,
            expected: 1,
            context: a,
        },
        Refresh {
            instrument: 2,
            expected: 2,
            context: b,
        },
    ];
    engine.refresh(&updates).unwrap();
    assert!(engine.read(1, true, AT).unwrap().is_none());
    assert!(!engine.ready(AT).unwrap());
    assert!(
        engine.refresh(&updates).is_err(),
        "stale whole-batch CAS cannot win twice"
    );
    pass(&engine, false, AT).await;
    pass(&engine, true, AT).await;
    assert!(engine.ready(AT).unwrap());
    assert_eq!(engine.read(1, true, AT).unwrap().unwrap().context, 101);
    assert_eq!(engine.read(2, true, AT).unwrap().unwrap().context, 102);
    assert_eq!(
        engine.read(1, true, AT).unwrap().unwrap().option.sequence,
        1
    );
    assert_eq!(engine.counters().unwrap(), (2, 4, 4));
    let mut malformed = context(103);
    malformed.model.strike = 6000.0;
    assert!(
        engine.register(1, 100, malformed, true).is_err(),
        "validator strike must match the numerical model strike"
    );
}

#[tokio::test]
async fn complete_business_risk_union_is_atomic_context_fenced_and_changes_no_source() {
    use ifm_market_data_native::option_chain::coalesced::RiskRequirement;
    let engine = Coalesced::new(3, 16, 5000).unwrap();
    for id in 1..=3 {
        engine.register(id, 100, context(id.into()), false).unwrap();
        engine.observe(id, 100, quote(100, AT, 1)).unwrap();
    }
    let demand = |instrument, context, required| RiskRequirement {
        instrument,
        context,
        required,
    };
    assert!(engine.set_risk_requirements(&[]).is_err());
    assert!(engine.set_risk_requirements(&[demand(1, 1, true)]).is_err());
    assert!(
        engine
            .set_risk_requirements(&[demand(1, 1, true), demand(1, 1, false), demand(3, 3, true)])
            .is_err()
    );
    assert!(
        engine
            .set_risk_requirements(&[demand(1, 1, true), demand(2, 99, true), demand(3, 3, true)])
            .is_err()
    );
    pass(&engine, true, AT).await;
    for id in 1..=3 {
        assert!(engine.read(id, true, AT).unwrap().is_none());
    }
    engine
        .set_risk_requirements(&[demand(3, 3, true), demand(1, 1, true), demand(2, 2, false)])
        .unwrap();
    pass(&engine, true, AT).await;
    for id in [1, 3] {
        let result = engine.read(id, true, AT).unwrap().unwrap();
        assert!(result.risk.is_some());
        assert_eq!(result.source_generation, 100);
    }
    assert!(engine.read(2, true, AT).unwrap().is_none());
    assert!(
        engine
            .set_risk_requirements(&[demand(1, 1, false), demand(2, 2, true), demand(4, 4, false)])
            .is_err()
    );
    engine.observe(1, 100, quote(101, AT + 1, 2)).unwrap();
    pass(&engine, true, AT + 1).await;
    assert_eq!(
        engine
            .read(1, true, AT + 1)
            .unwrap()
            .unwrap()
            .option
            .sequence,
        2
    );
    assert!(engine.read(2, true, AT + 1).unwrap().is_none());
    engine
        .set_risk_requirements(&[
            demand(1, 1, false),
            demand(2, 2, false),
            demand(3, 3, false),
        ])
        .unwrap();
    engine.observe(1, 100, quote(102, AT + 2, 3)).unwrap();
    pass(&engine, true, AT + 2).await;
    assert_eq!(
        engine.read(1, true, AT + 2).unwrap().unwrap().failure,
        Some(Failure::CalculationPending)
    );
}

#[test]
fn queued_trade_context_and_binary_evidence_survive_reference_and_physical_replacement() {
    use ifm_market_data_native::option_chain::{coalesced::Refresh, manifest::Budget};
    let budget = Budget::new(64).unwrap();
    let engine = Coalesced::new(1, 1, 5000).unwrap();
    engine.register(1, 100, context(1), true).unwrap();
    let old = budget.retain(1, b"old").unwrap();
    engine.attach_manifest(1, 1, old.clone()).unwrap();
    drop(old);
    let queued = engine.trade_context(1, 100).unwrap().unwrap();
    assert!(engine.trade_context(1, 101).unwrap().is_none());
    assert_eq!(queued.context.identity, 1);
    let mut next = context(2);
    next.model.rate = 0.05;
    assert!(
        engine
            .refresh(&[Refresh {
                instrument: 1,
                expected: 1,
                context: next
            }])
            .is_err()
    );
    assert_eq!(
        engine
            .trade_context(1, 100)
            .unwrap()
            .unwrap()
            .context
            .identity,
        1
    );
    let replacements = std::collections::BTreeMap::from([(1, budget.retain(2, b"new").unwrap())]);
    engine
        .refresh_prepared(
            &[Refresh {
                instrument: 1,
                expected: 1,
                context: next,
            }],
            &replacements,
        )
        .unwrap();
    drop(replacements);
    let current = engine.trade_context(1, 100).unwrap().unwrap();
    assert_eq!(current.context.model.rate, 0.05);
    assert_eq!(current.manifest.as_ref().unwrap().bytes(), b"new");
    assert_eq!(queued.context.model.rate, 0.04);
    assert_eq!(queued.manifest.as_ref().unwrap().bytes(), b"old");
    engine.rebind(101).unwrap();
    assert!(engine.trade_context(1, 100).unwrap().is_none());
    assert_eq!(
        engine
            .trade_context(1, 101)
            .unwrap()
            .unwrap()
            .context
            .identity,
        2
    );
    drop(engine);
    assert_eq!(budget.used(), 6);
    drop(current);
    assert_eq!(budget.used(), 3);
    drop(queued);
    assert_eq!(budget.used(), 0);
}
