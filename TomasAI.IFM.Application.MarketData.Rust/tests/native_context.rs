//! Complete context parity against the existing C# contracts, not a Rust mirror.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    messaging::codec,
    option_chain::{context::Context, convention::Convention},
    reference::{
        calendar::Calendar,
        decimal,
        treasury_curve::{Curve, Point},
        treasury_policy::{Conversion, Observation, Publication, Publications},
    },
};
fn date(day: i32) -> time::Date {
    time::Date::from_julian_day(day + 1721426).unwrap()
}

/// A calendar rollover can change the selected Treasury tenor before the stored
/// publication/contract validity ends. Native typed authority fences both sweeps
/// and refreshes; copying a new numeric template alone cannot bypass that fence.
#[tokio::test]
async fn tenor_cutover_requires_complete_atomic_native_reference_refresh() {
    use ifm_market_data_native::option_chain::{
        coalesced::{Coalesced, Refresh},
        manifest::Budget,
    };
    use ifm_market_data_native::runtime::Deadline;
    use std::{collections::BTreeMap, sync::Arc};
    use tokio_util::sync::CancellationToken;
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let mut value = rmpv::decode::read_value(&mut wire(&rows[0], "contract").as_slice()).unwrap();
    let at = rows[0]["at"].as_i64().unwrap() - 25 * 864_000_000_000;
    let rmpv::Value::Array(ref mut fields) = value else {
        panic!("contract DTO")
    };
    fields[12] = codec::utc_offset(at + 30 * 864_000_000_000).unwrap();
    fields[13] = fields[12].clone();
    let contract = Convention::new(value).unwrap();
    let first = time::Date::from_calendar_date(2026, time::Month::October, 1).unwrap();
    let day = first.to_julian_day() - 1721426;
    let calendar = Calendar::new(
        "fixture/v1".into(),
        "Eastern Standard Time",
        first,
        date(day + 59),
        648_000_000_000,
        (0..60).map(|i| date(day + i)).collect(),
    )
    .unwrap();
    let publications = Publications::new(
        "publication/v1".into(),
        at - 864_000_000_000,
        at + 10 * 864_000_000_000,
        vec![Publication {
            date: first,
            available: at - 864_000_000_000,
        }],
        "Eastern Standard Time",
    )
    .unwrap();
    let observation = Observation::official(Curve {
        date: first,
        retrieved: at - 600_000_000,
        rates: vec![
            Point {
                months: 1,
                percentage: decimal::parse("4.0").unwrap(),
            },
            Point {
                months: 2,
                percentage: decimal::parse("4.1").unwrap(),
            },
        ],
    });
    let prepare = |now| {
        Arc::new(
            Context::prepare(
                contract.clone(),
                calendar.clone(),
                &publications,
                &Conversion::standard(),
                Some(&observation),
                [1; 16],
                contract.engine(),
                now,
                0,
            )
            .unwrap(),
        )
    };
    let prior = prepare(at);
    let next_at = at + 864_000_000_000;
    assert!(prior.valid_until() > next_at);
    let changed = prior.validate_at(next_at).unwrap_err();
    assert_eq!(
        (changed.code, changed.input),
        ("TreasuryContextChanged", "Tenor")
    );
    let strike = decimal::parse("6000").unwrap();
    let engine = Coalesced::new(1, 1, 5000).unwrap();
    engine
        .register(42, 99, prior.bind(at, 1, 7, strike, true).unwrap(), true)
        .unwrap();
    engine.attach_reference(42, 1, prior.clone()).unwrap();
    let captured = engine.trade_context(42, 99).unwrap().unwrap();
    let original_digest = captured.reference.as_ref().unwrap().digest().unwrap();
    let stop = CancellationToken::new();
    assert!(
        engine
            .pass(false, at, |_| None, &stop, Deadline::recovery())
            .await
            .is_ok()
    );
    assert_eq!(
        engine
            .pass(false, next_at, |_| None, &stop, Deadline::recovery())
            .await
            .err(),
        Some("Native option reference requires refresh")
    );
    let next = prepare(next_at);
    let updates = [Refresh {
        instrument: 42,
        expected: 1,
        context: next.bind(next_at, 2, 7, strike, true).unwrap(),
    }];
    assert!(
        engine.refresh(&updates).is_err(),
        "numeric-only refresh cannot remove native reference authority"
    );
    let references = BTreeMap::from([(42, next.clone())]);
    let budget = Budget::new(1024 * 1024).unwrap();
    let manifests = BTreeMap::from([(
        42,
        budget
            .retain(2, &next.manifest(strike, true).unwrap())
            .unwrap(),
    )]);
    engine
        .refresh_references(&updates, &manifests, &references)
        .unwrap();
    let current = engine.trade_context(42, 99).unwrap().unwrap();
    assert_ne!(
        current.reference.as_ref().unwrap().digest().unwrap(),
        original_digest
    );
    assert_eq!(
        captured.reference.as_ref().unwrap().digest().unwrap(),
        original_digest
    );
    assert_eq!(
        captured
            .reference
            .as_ref()
            .unwrap()
            .validate_at(next_at)
            .unwrap_err()
            .code,
        "TreasuryContextChanged"
    );
    assert!(
        engine
            .pass(false, next_at, |_| None, &stop, Deadline::recovery())
            .await
            .is_ok()
    );
    assert!(
        engine
            .refresh_references(&updates, &manifests, &references)
            .is_err(),
        "old CAS fence cannot apply twice"
    );
    assert!(
        prior.validate_at(next_at).is_err(),
        "queued old evidence retains its original validity"
    );
}
fn wire(row: &serde_json::Value, key: &str) -> Vec<u8> {
    STANDARD.decode(row[key].as_str().unwrap()).unwrap()
}
#[tokio::test]
async fn exact_current_context_and_manifest_contents_and_hashes() {
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    for row in rows.as_array().unwrap() {
        let value = rmpv::decode::read_value(&mut wire(row, "contract").as_slice()).unwrap();
        let contract = Convention::new(value).unwrap();
        let at = row["at"].as_i64().unwrap();
        let until = row["until"].as_i64().unwrap();
        let first = time::Date::from_calendar_date(2026, time::Month::October, 1).unwrap();
        let day = first.to_julian_day() - 1721426;
        let calendar = Calendar::new(
            "fixture/v1".into(),
            "Eastern Standard Time",
            first,
            date(day + 59),
            row["rollover"].as_u64().unwrap(),
            (0..60).map(|i| date(day + i)).collect(),
        )
        .unwrap();
        let publications = Publications::new(
            "publication/v1".into(),
            at - 1000,
            until,
            vec![Publication {
                date: date(day + 25),
                available: at - 1000,
            }],
            "Eastern Standard Time",
        )
        .unwrap();
        let observation = Observation::official(Curve {
            date: date(day + 25),
            retrieved: at - 600_000_000,
            rates: vec![
                Point {
                    months: 1,
                    percentage: decimal::parse("4.0000").unwrap(),
                },
                Point {
                    months: 2,
                    percentage: decimal::parse("4.10").unwrap(),
                },
                Point {
                    months: 3,
                    percentage: decimal::parse("4.2").unwrap(),
                },
            ],
        });
        let generation = [
            0xcc, 0xdd, 0xee, 0xff, 0xaa, 0xbb, 0x88, 0x99, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22,
            0x11, 0,
        ];
        let engine = contract.engine();
        let context = Context::prepare(
            contract.clone(),
            calendar.clone(),
            &publications,
            &Conversion::standard(),
            Some(&observation),
            generation,
            engine,
            at,
            123,
        )
        .unwrap();
        assert_eq!(
            codec::encode(&context.value().unwrap()).unwrap(),
            wire(row, "context"),
            "DTO {}",
            row["name"]
        );
        assert_eq!(
            context.digest().unwrap(),
            row["digest"].as_str().unwrap(),
            "hash {}",
            row["name"]
        );
        {
            use ifm_market_data_native::{
                messaging::{
                    option_evidence::{Evidence, Failure, Greeks, Outcome, Quote},
                    option_source::Source,
                },
                option_chain::qualification,
                pricing,
                reference::semantic,
            };
            use std::sync::Arc;
            let source = Source {
                dataset: "GLBX.MDP3".into(),
                publisher: 3,
                instrument: 42,
                contract: "ES20261110C6000".into(),
                date: date(day + 26),
                price: decimal::parse("10.2500").unwrap(),
                size: 3,
                sequence: 77,
                event: 1_793_102_400_123_456_789,
                received: 1_793_102_400_123_456_999,
                generation,
                symbol: "fixture".into(),
            };
            let quote = Quote {
                contract: "ES20261218".into(),
                quote: qualification::Quote {
                    generation_low: u64::from_le_bytes(generation[..8].try_into().unwrap()),
                    generation_high: u64::from_le_bytes(generation[8..].try_into().unwrap()),
                    event: at - 2,
                    received: at - 1,
                    sequence: 76,
                    bid: decimal::parse("6000").unwrap(),
                    ask: decimal::parse("6000.25").unwrap(),
                    bid_size: decimal::parse("2").unwrap(),
                    ask_size: decimal::parse("3").unwrap(),
                    ..Default::default()
                },
            };
            assert_eq!(
                semantic::compute(&quote.semantic().unwrap()).unwrap(),
                row["quoteDigest"].as_str().unwrap()
            );
            let risk = Greeks {
                values: pricing::Values {
                    price: 10.25,
                    volatility: 0.2,
                    delta: 0.3,
                    gamma: 0.004,
                    theta: -1.2,
                    vega: 4.5,
                    rho: -0.1,
                },
                time: 0.04,
                digest: "trade-digest".into(),
            };
            let failure = Failure {
                code: "StaleData".into(),
                input: "Underlying".into(),
                contract: source.contract.clone(),
                detail: "Source-time reference qualification has expired.".into(),
                retryable: false,
            };
            let mut evidence = Evidence {
                source,
                context: Some(Arc::new(context.clone())),
                underlying: Some(quote),
                at,
                outcome: Outcome::Success(risk.clone()),
            };
            {
                use ifm_market_data_native::{
                    dataset::live::Registration,
                    latest::Asset,
                    messaging::{
                        events::Identity,
                        options::{Options, Revisions},
                    },
                    option_chain::coalesced::Coalesced,
                    runtime::Deadline,
                };
                use tokio_util::sync::CancellationToken;
                let cache = Coalesced::new(1, 1, 5000).unwrap();
                let numeric = context
                    .bind(at, 123, 7, decimal::parse("6000").unwrap(), true)
                    .unwrap();
                cache.register(42, 77, numeric, true).unwrap();
                cache
                    .attach_reference(42, 123, Arc::new(context.clone()))
                    .unwrap();
                let mut option = evidence.underlying.as_ref().unwrap().quote;
                option.bid = decimal::parse("10.25").unwrap();
                option.ask = option.bid;
                option.bid_size = decimal::parse("3").unwrap();
                option.ask_size = option.bid_size;
                option.sequence = 77;
                cache.observe(42, 77, option).unwrap();
                let stop = CancellationToken::new();
                cache
                    .pass(
                        true,
                        at,
                        |_| Some(evidence.underlying.as_ref().unwrap().quote),
                        &stop,
                        Deadline::recovery(),
                    )
                    .await
                    .unwrap();
                let registration = Registration {
                    instrument: 42,
                    publisher: 3,
                    contract: "ES20261110C6000".into(),
                    source: "fixture".into(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                };
                let revisions = Arc::new(Revisions::default());
                let mut publications = Options::new(
                    source_date(&evidence),
                    source_date(&evidence),
                    vec![registration.clone()],
                    revisions.clone(),
                )
                .unwrap();
                let first = publications
                    .quote(&cache, 42, 77, at, false, Identity::fresh(at))
                    .unwrap()
                    .unwrap();
                assert_eq!(first.kind, 7);
                assert_eq!(find_snapshot(&first.value).unwrap()[22].as_i64(), Some(1));
                assert!(
                    publications
                        .quote(&cache, 42, 77, at, false, Identity::fresh(at))
                        .unwrap()
                        .is_none()
                );
                assert!(
                    publications
                        .quote(&cache, 42, 88, at, false, Identity::fresh(at))
                        .unwrap()
                        .is_none()
                );
                assert!(
                    publications
                        .quote(&cache, 999, 77, at, false, Identity::fresh(at))
                        .is_err()
                );
                let mut second = Options::new(
                    source_date(&evidence),
                    source_date(&evidence),
                    vec![registration.clone()],
                    revisions,
                )
                .unwrap();
                let other = second
                    .quote(&cache, 42, 77, at, false, Identity::fresh(at))
                    .unwrap()
                    .unwrap();
                assert_eq!(find_snapshot(&other.value).unwrap()[22].as_i64(), Some(2));
                let pricing = publications
                    .quote(&cache, 42, 77, at, true, Identity::fresh(at))
                    .unwrap()
                    .unwrap();
                assert_eq!(find_snapshot(&pricing.value).unwrap()[22].as_i64(), Some(3));
                // New provider sequence at unchanged prices is throttled; full
                // pricing updates remain separate from that raw quote policy.
                option.sequence += 1;
                cache.observe(42, 77, option).unwrap();
                // A new publisher may consume the last completed immutable
                // quote/risk pair while the next quote awaits its calculation.
                // It must retain sequence 77, never attach that risk to 78.
                let mut pending_publisher = Options::new(
                    source_date(&evidence),
                    source_date(&evidence),
                    vec![registration],
                    Arc::new(Revisions::default()),
                )
                .unwrap();
                let pending = pending_publisher
                    .quote(&cache, 42, 77, at, true, Identity::fresh(at))
                    .unwrap()
                    .expect("completed risk must remain publishable during new ingress");
                assert_eq!(find_snapshot(&pending.value).unwrap()[2].as_i64(), Some(77));
                assert!(cache.ready(at).unwrap());
                assert!(
                    publications
                        .quote(&cache, 42, 77, at, true, Identity::fresh(at))
                        .unwrap()
                        .is_none(),
                    "superseded risk must not relabel an older quote as the latest source"
                );
                cache
                    .pass(
                        true,
                        at,
                        |_| Some(evidence.underlying.as_ref().unwrap().quote),
                        &stop,
                        Deadline::recovery(),
                    )
                    .await
                    .unwrap();
                assert!(
                    publications
                        .quote(&cache, 42, 77, at, false, Identity::fresh(at))
                        .unwrap()
                        .is_none()
                );
                publications.retire();
                let restored = publications
                    .quote(&cache, 42, 77, at, false, Identity::fresh(at))
                    .unwrap()
                    .unwrap();
                assert_eq!(
                    find_snapshot(&restored.value).unwrap()[22].as_i64(),
                    Some(4)
                );
                assert_eq!(publications.instruments().collect::<Vec<_>>(), vec![42]);
            }
            {
                use ifm_market_data_native::option_chain::{
                    coalesced::{Calculation, IvObservation},
                    composition::model::{Instrument, Snapshot, Valued},
                    selection::Selection,
                };
                use ifm_market_data_native::pricing::PriceDelta;
                let fixtures = row["compositions"]
                    .as_array()
                    .expect("C# composition models");
                assert_eq!(fixtures.len(), 3);
                for fixture in fixtures {
                    let mode = fixture["mode"].as_str().unwrap();
                    let mut option = evidence.underlying.as_ref().unwrap().quote;
                    option.bid = decimal::parse("10.25").unwrap();
                    option.ask = option.bid;
                    option.bid_size = decimal::parse("3").unwrap();
                    option.ask_size = option.bid_size;
                    option.sequence = 77;
                    let calculation = Calculation {
                        context: 123,
                        source_generation: 77,
                        at,
                        underlying: evidence.underlying.as_ref().unwrap().quote,
                        option,
                        iv_event: Some(option.event),
                        iv: Some(IvObservation {
                            context: 123,
                            event: option.event,
                            volatility: 0.2,
                            underlying: evidence.underlying.as_ref().unwrap().quote,
                            option,
                            at,
                        }),
                        time: 0.04,
                        selection: Some(PriceDelta {
                            price: 10.25,
                            delta: 0.3,
                        }),
                        risk: None,
                        failure: None,
                    };
                    let item = Instrument {
                        contract: evidence.source.contract.clone(),
                        quote: if mode == "missing" {
                            None
                        } else {
                            Some(Quote {
                                contract: evidence.source.contract.clone(),
                                quote: option,
                            })
                        },
                        context: Some(Arc::new(context.clone())),
                        strike: Some(decimal::parse("6000").unwrap()),
                        call: Some(true),
                        underlying: evidence.underlying.clone(),
                        future: None,
                        selection: if mode == "selection" {
                            Selection::from_calculation(
                                &context,
                                &calculation,
                                "OptionRefresh/TestDefaults/v1",
                                5000,
                            )
                            .unwrap()
                        } else {
                            None
                        },
                        volume: Some(100),
                        interest: Some(50),
                        statistics_at: Some(at - 10_000_000),
                        unavailable: if mode == "missing" {
                            Some("QuoteAgeExceeded".into())
                        } else {
                            None
                        },
                        last_event: if mode == "missing" {
                            Some(at - 60_000_000)
                        } else {
                            None
                        },
                        last_received: if mode == "missing" {
                            Some(at - 50_000_000)
                        } else {
                            None
                        },
                        volume_date: Some(evidence.source.date.to_julian_day() - 1721426),
                        interest_date: Some(evidence.source.date.to_julian_day() - 1721427),
                    };
                    let mut snapshot = Snapshot {
                        schema: if mode == "risk" { 1 } else { 2 },
                        id: [
                            0x33, 0x22, 0x11, 0, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb,
                            0xcc, 0xdd, 0xee, 0xff,
                        ],
                        scope: "native-fixture".into(),
                        token: "fixture-digest".into(),
                        horizon: "Daily".into(),
                        generation,
                        at,
                        until: at + 10_000_000,
                        instruments: vec![Valued {
                            instrument: item.clone(),
                            valuation: if mode == "risk" {
                                Some(risk.clone())
                            } else {
                                None
                            },
                        }],
                        digest: String::new(),
                        ranking: None,
                    };
                    snapshot.seal().unwrap();
                    assert_eq!(
                        snapshot.digest,
                        fixture["digest"].as_str().unwrap(),
                        "composition digest {} {mode}",
                        row["name"]
                    );
                    assert_eq!(
                        codec::encode(&snapshot.value().unwrap()).unwrap(),
                        wire(fixture, "content"),
                        "composition wire {} {mode}",
                        row["name"]
                    );
                    if mode == "risk" {
                        use ifm_market_data_native::{
                            messaging::option_cache::Publication, option_chain::cache_read,
                        };
                        let checks: serde_json::Value = serde_json::from_str(include_str!(
                            "../qualification/ContextWireBridge/cache-reads.json"
                        ))
                        .unwrap();
                        let cases = checks
                            .as_array()
                            .unwrap()
                            .iter()
                            .find(|c| c["name"] == row["name"])
                            .unwrap()["cases"]
                            .as_array()
                            .unwrap();
                        for case in cases {
                            let mut prepared = snapshot.clone();
                            prepared.instruments[0].valuation.as_mut().unwrap().digest =
                                "a".repeat(64);
                            let mode = case["mode"].as_str().unwrap();
                            let item = &mut prepared.instruments[0].instrument;
                            match mode {
                                "missing-quote" => item.quote = None,
                                "missing-underlying" => item.underlying = None,
                                "missing-context" => item.context = None,
                                "wrong-quote-id" => {
                                    item.quote.as_mut().unwrap().contract = "wrong".into()
                                }
                                "wrong-underlying-id" => {
                                    item.underlying.as_mut().unwrap().contract = "wrong".into()
                                }
                                "quote-generation" => {
                                    let q = &mut item.quote.as_mut().unwrap().quote;
                                    q.generation_low = 0;
                                    q.generation_high = 0;
                                }
                                "underlying-generation" => {
                                    let q = &mut item.underlying.as_mut().unwrap().quote;
                                    q.generation_low = 0;
                                    q.generation_high = 0;
                                }
                                "stale-quote" => {
                                    item.quote.as_mut().unwrap().quote.event = at - 1001 * 10000
                                }
                                "future-quote" => {
                                    item.quote.as_mut().unwrap().quote.event = at + 124 * 10000
                                }
                                "future-received" => {
                                    item.quote.as_mut().unwrap().quote.received = at + 1
                                }
                                "invalid-bid" => {
                                    item.quote.as_mut().unwrap().quote.bid =
                                        decimal::parse("-1").unwrap()
                                }
                                "crossed-quote" => {
                                    item.quote.as_mut().unwrap().quote.ask =
                                        decimal::parse("9.25").unwrap()
                                }
                                "zero-size" => {
                                    item.quote.as_mut().unwrap().quote.bid_size =
                                        decimal::parse("0").unwrap()
                                }
                                "quote-offset" => {
                                    item.quote.as_mut().unwrap().quote.event_offset = 60
                                }
                                "underlying-offset" => {
                                    item.underlying.as_mut().unwrap().quote.received_offset = 60
                                }
                                "zero-underlying" => {
                                    item.underlying.as_mut().unwrap().quote.bid =
                                        decimal::parse("0").unwrap()
                                }
                                "incoherent" => {
                                    item.quote.as_mut().unwrap().quote.event = at - 251 * 10000;
                                    item.underlying.as_mut().unwrap().quote.event = at;
                                }
                                "age-boundary" => {
                                    item.quote.as_mut().unwrap().quote.event = at - 1000 * 10000
                                }
                                "lead-boundary" => {
                                    item.quote.as_mut().unwrap().quote.event = at + 123 * 10000
                                }
                                _ => {}
                            }
                            if mode == "empty" {
                                prepared.instruments.clear();
                            }
                            // Hostile resident-offset fixtures are deliberately read
                            // directly; sealing correctly rejects non-UTC provenance.
                            if !matches!(mode, "quote-offset" | "underlying-offset") {
                                prepared.seal().unwrap();
                            }
                            let mut publication = Publication::new(
                                if matches!(mode, "quote-offset" | "underlying-offset") {
                                    &snapshot
                                } else {
                                    &prepared
                                },
                                evidence.source.date.to_julian_day() - 1721426,
                                "fixture-config",
                                7,
                                &prepared.scope,
                            )
                            .unwrap();
                            if matches!(mode, "quote-offset" | "underlying-offset") {
                                publication.snapshot = Arc::new(prepared.clone());
                            }
                            let request =
                                cache_read::Request::from_command(&case["request"]).unwrap();
                            let publication =
                                (request.scope == prepared.scope).then_some(&publication);
                            let actual = cache_read::read(
                                &request,
                                publication,
                                case["now"].as_i64().unwrap(),
                            )
                            .unwrap();
                            let fields = actual.as_array().unwrap();
                            assert_eq!(
                                fields[0].as_i64(),
                                case["outcome"].as_i64(),
                                "cache outcome {} {}",
                                row["name"],
                                case["mode"]
                            );
                            assert_eq!(
                                fields[1].as_str(),
                                case["reason"].as_str(),
                                "cache reason {} {}",
                                row["name"],
                                case["mode"]
                            );
                            assert_eq!(
                                fields[3].as_i64(),
                                case["version"].as_i64(),
                                "cache version {} {}",
                                row["name"],
                                case["mode"]
                            );
                        }
                    }
                    if mode == "risk" {
                        use ifm_market_data_native::option_chain::strategy::{
                            self, Parameters, Ranking,
                        };
                        let parameters = Arc::new(
                            Parameters::new(
                                rmpv::decode::read_value(
                                    &mut wire(fixture, "rankingPolicy").as_slice(),
                                )
                                .unwrap(),
                            )
                            .unwrap(),
                        );
                        let ranking = Ranking {
                            parameters,
                            direction: 0,
                            date: evidence.source.date,
                        };
                        let mut raw = snapshot.clone();
                        let mut wing = raw.instruments[0].clone();
                        wing.instrument.contract.push_str("-wing");
                        wing.instrument.strike = Some(decimal::parse("6005").unwrap());
                        wing.instrument.call = Some(true);
                        wing.valuation.as_mut().unwrap().values.delta = 0.01;
                        raw.instruments.push(wing);
                        let stop = tokio_util::sync::CancellationToken::new();
                        let ranked = strategy::prepare(
                            &raw,
                            ranking.clone(),
                            at,
                            &stop,
                            ifm_market_data_native::runtime::Deadline::recovery(),
                        )
                        .unwrap();
                        assert_eq!(
                            codec::encode(&ranked.value().unwrap()).unwrap(),
                            wire(fixture, "rankedContent"),
                            "full prepared ranking {}",
                            row["name"]
                        );
                        stop.cancel();
                        assert!(
                            strategy::prepare(
                                &raw,
                                ranking,
                                at,
                                &stop,
                                ifm_market_data_native::runtime::Deadline::recovery()
                            )
                            .is_err()
                        );
                    }
                    use ifm_market_data_native::{
                        option_chain::composition::capture::{self, Page, Request},
                        runtime::{Deadline, clock::Clock},
                    };
                    let request = Request {
                        id: snapshot.id,
                        scope: snapshot.scope.clone(),
                        generation,
                        horizon: "Daily".into(),
                        at,
                        until: at + 10_000_000,
                        maximum: 2048,
                        age: 1000,
                        skew: 250,
                        options: true,
                        selection: mode != "risk",
                        allow_missing: mode == "missing",
                        risk: Default::default(),
                    };
                    let captured = capture::capture(
                        request,
                        vec![Page {
                            generation,
                            token: "fixture-digest".into(),
                            total: 1,
                            instruments: vec![item],
                            continuation: None,
                        }],
                        &Clock::new(at, false).unwrap(),
                        &tokio_util::sync::CancellationToken::new(),
                        Deadline::recovery(),
                    )
                    .await;
                    if fixture["captureFailure"].is_null() {
                        let captured = captured.unwrap();
                        assert_eq!(
                            codec::encode(&captured.value().unwrap()).unwrap(),
                            wire(fixture, "capture"),
                            "native complete capture {} {mode}",
                            row["name"]
                        );
                    } else {
                        assert_eq!(
                            captured.err().unwrap().code,
                            fixture["captureFailure"].as_str().unwrap()
                        );
                    }
                }
            }
            {
                use ifm_market_data_native::{
                    messaging::option_risk,
                    option_chain::coalesced::{Calculation, Failure as CachedFailure},
                };
                let numeric = context
                    .bind(at, 123, 7, decimal::parse("6000").unwrap(), true)
                    .unwrap();
                let risks = row["risks"].as_array().expect("C# cached-risk fixtures");
                assert_eq!(risks.len(), 7);
                for fixture in risks {
                    let mode = fixture["mode"].as_str().unwrap();
                    let mut option = evidence.underlying.as_ref().unwrap().quote;
                    option.bid = decimal::parse("10.25").unwrap();
                    option.ask = option.bid;
                    option.bid_size = decimal::parse("3").unwrap();
                    option.ask_size = option.bid_size;
                    option.sequence = 77;
                    let mut calculation = Calculation {
                        context: 123,
                        source_generation: 77,
                        at,
                        underlying: if mode == "underlying" {
                            Default::default()
                        } else {
                            evidence.underlying.as_ref().unwrap().quote
                        },
                        option,
                        iv_event: None,
                        iv: None,
                        time: 0.04,
                        selection: None,
                        risk: Some(risk.values),
                        failure: match mode {
                            "pending" => Some(CachedFailure::CalculationPending),
                            "underlying" => Some(CachedFailure::UnderlyingUnavailable),
                            "numerical" => {
                                Some(CachedFailure::Numerical(pricing::Failure::PriceOutOfBounds))
                            }
                            "quote" => Some(CachedFailure::Quote(qualification::Result {
                                size: 16,
                                failure: 4,
                                input: 0,
                                reserved: 0,
                            })),
                            _ => None,
                        },
                    };
                    if calculation.failure.is_some() {
                        calculation.risk = None;
                    }
                    if mode == "success" {
                        use ifm_market_data_native::{
                            option_chain::{coalesced::IvObservation, selection::Selection},
                            pricing::PriceDelta,
                        };
                        let fixtures = row["selections"]
                            .as_array()
                            .expect("current C# selection fixtures");
                        assert_eq!(fixtures.len(), 3);
                        for selected in fixtures {
                            let mut cached = calculation.clone();
                            cached.risk = None;
                            cached.selection = Some(PriceDelta {
                                price: 10.25,
                                delta: 0.3,
                            });
                            let mut iv_option = calculation.option;
                            iv_option.event = at - 10_000_000;
                            iv_option.received = iv_option.event + 1;
                            let mut iv_underlying = calculation.underlying;
                            iv_underlying.event = at - 10_000_001;
                            iv_underlying.received = at - 10_000_000;
                            cached.iv = Some(IvObservation {
                                context: 123,
                                event: iv_option.event,
                                volatility: 0.2,
                                underlying: iv_underlying,
                                option: iv_option,
                                at: at - 9_000_000,
                            });
                            let age = selected["age"].as_u64().unwrap() as u32;
                            let result = Selection::from_calculation(
                                &context,
                                &cached,
                                "OptionRefresh/TestDefaults/v1",
                                age,
                            )
                            .unwrap()
                            .unwrap();
                            assert_eq!(
                                codec::encode(&result.value().unwrap()).unwrap(),
                                wire(selected, "content"),
                                "selection wire {} age {age}",
                                row["name"]
                            );
                            assert_eq!(
                                semantic::compute(&result.semantic().unwrap()).unwrap(),
                                selected["digest"].as_str().unwrap(),
                                "selection semantic {} age {age}",
                                row["name"]
                            );
                            assert_eq!(
                                result.valid_until(),
                                (at - 10_000_000 + i64::from(age) * 10_000).min(at + 49_999_998)
                            );
                            assert!(
                                Selection::from_calculation(
                                    &context,
                                    &cached,
                                    "OptionRefresh/TestDefaults/v1",
                                    0
                                )
                                .is_err()
                            );
                            cached.selection.as_mut().unwrap().price = f64::NAN;
                            assert!(
                                Selection::from_calculation(
                                    &context,
                                    &cached,
                                    "OptionRefresh/TestDefaults/v1",
                                    age
                                )
                                .is_err()
                            );
                            cached.iv = None;
                            assert!(
                                Selection::from_calculation(
                                    &context,
                                    &cached,
                                    "OptionRefresh/TestDefaults/v1",
                                    age
                                )
                                .unwrap()
                                .is_none()
                            );
                        }
                    }
                    let current = fixture["currentAt"].as_i64().unwrap();
                    let actual = option_risk::read(&context, &numeric, &calculation, current)
                        .unwrap()
                        .unwrap();
                    assert_eq!(
                        codec::encode(&actual.value().unwrap()).unwrap(),
                        wire(fixture, "content"),
                        "cached risk {} {}",
                        row["name"],
                        mode
                    );
                    let projected = ifm_market_data_native::messaging::quote_greeks::read(
                        &context,
                        &numeric,
                        &calculation,
                        current,
                    )
                    .unwrap()
                    .unwrap();
                    assert_eq!(
                        codec::encode(&projected).unwrap(),
                        wire(fixture, "quoteGreeks"),
                        "quote Greeks {} {}",
                        row["name"],
                        mode
                    );
                    {
                        use ifm_market_data_native::{
                            dataset::{live::Registration, transport::Lane},
                            latest::Asset,
                            messaging::{
                                events::{Identity, Instrument},
                                option_observation,
                            },
                        };
                        let snapshot = option_observation::snapshot(
                            &context,
                            &numeric,
                            &calculation,
                            date(day + 26),
                            current,
                            1,
                            mode != "success",
                        )
                        .unwrap()
                        .unwrap();
                        assert_eq!(
                            codec::encode(&snapshot).unwrap(),
                            wire(fixture, "snapshot"),
                            "quote snapshot {} {}",
                            row["name"],
                            mode
                        );
                        let instrument = Instrument::new(
                            Registration {
                                instrument: 42,
                                publisher: 3,
                                contract: "ES20261110C6000".into(),
                                source: "fixture".into(),
                                asset: Asset::Option,
                                tick: Some(250_000_000),
                                vwap: false,
                                required: true,
                            },
                            Lane::OptionsGlbx,
                            date(day + 26),
                            date(day + 26),
                        )
                        .unwrap();
                        let event_id = [
                            0x33, 0x22, 0x11, 0, 0x55, 0x44, 0x77, 0x66, 0x88, 0x99, 0xaa, 0xbb,
                            0xcc, 0xdd, 0xee, 0xff,
                        ];
                        let event = instrument
                            .option_quote(
                                calculation.option,
                                snapshot,
                                "ES20261218",
                                Identity {
                                    event: event_id,
                                    command: event_id,
                                    observed: current,
                                },
                            )
                            .unwrap();
                        assert_eq!(
                            codec::encode(&event).unwrap(),
                            wire(fixture, "quoteEvent"),
                            "complete actor quote {} {}",
                            row["name"],
                            mode
                        );
                    }
                    calculation.context = 124;
                    assert!(
                        option_risk::read(&context, &numeric, &calculation, current)
                            .unwrap()
                            .is_none()
                    );
                    calculation.context = 123;
                    calculation.source_generation = 0;
                    assert!(option_risk::read(&context, &numeric, &calculation, current).is_err());
                }
            }
            assert_eq!(
                row["calculations"].as_array().unwrap().len(),
                6,
                "Regenerate fixtures using the current C# pricer and native DLL"
            );
            for fixture in row["calculations"]
                .as_array()
                .expect("C# calculation fixtures")
            {
                use ifm_market_data_native::{
                    messaging::option_pricing,
                    option_chain::{coalesced::TradeContext, trade::Captured},
                    runtime::Deadline,
                };
                use tokio_util::sync::CancellationToken;
                let mut source = evidence.source.clone();
                let value =
                    rmpv::decode::read_value(&mut wire(fixture, "source").as_slice()).unwrap();
                let fields = value.as_array().unwrap();
                source.price = decimal::parse(fields[5].as_str().unwrap()).unwrap();
                source.size = fields[6].as_u64().unwrap() as u32;
                source.event = fields[8].as_i64().unwrap();
                source.received = fields[9].as_i64().unwrap();
                let captured = Captured {
                    prepared: TradeContext {
                        context: Arc::new(
                            context
                                .bind(at, 123, 7, decimal::parse("6000").unwrap(), true)
                                .unwrap(),
                        ),
                        manifest: None,
                        reference: Some(Arc::new(context.clone())),
                    },
                    underlying: if fixture["mode"] == "missing" {
                        None
                    } else {
                        Some(evidence.underlying.as_ref().unwrap().quote)
                    },
                    at,
                };
                let stop = CancellationToken::new();
                let actual = option_pricing::calculate(
                    source.clone(),
                    &captured,
                    &stop,
                    Deadline::recovery(),
                )
                .await
                .unwrap();
                assert_eq!(
                    actual.encode().unwrap(),
                    wire(fixture, "content"),
                    "full trade {} {}",
                    row["name"],
                    fixture["mode"]
                );
                let projected = ifm_market_data_native::messaging::trade_greeks::read(
                    &actual.value().unwrap(),
                    "ES20261218",
                    1,
                )
                .unwrap();
                assert_eq!(
                    codec::encode(&projected.as_array().unwrap()[1]).unwrap(),
                    wire(fixture, "tradeGreeks"),
                    "frozen trade query {} {}",
                    row["name"],
                    fixture["mode"]
                );
                // Feed the actual source through native ingress, not only the
                // financial helper. The complete retained bytes must match C#.
                use ifm_market_data_native::{
                    dataset::{
                        live::{Output, OutputSource, Registration},
                        records::{Header, Observation, Trade},
                        transport::Lane,
                    },
                    latest::Asset,
                    messaging::{evidence_outbox::Outbox, option_ingress::Ingress},
                };
                let registration = Registration {
                    instrument: source.instrument,
                    publisher: source.publisher,
                    contract: source.contract.clone(),
                    source: source.instrument.to_string(),
                    asset: Asset::Option,
                    tick: None,
                    vwap: false,
                    required: true,
                };
                let ingress =
                    Ingress::new(source.date, source.generation, vec![registration.clone()])
                        .unwrap();
                let [lo, mid, hi, flags] = source.price.bits;
                let coefficient = i128::from(lo) | i128::from(mid) << 32 | i128::from(hi) << 64;
                let signed = if flags & (1 << 31) != 0 {
                    -coefficient
                } else {
                    coefficient
                };
                let scale = (flags >> 16) & 255;
                assert!(scale <= 9);
                let raw_price = i64::try_from(signed * 10i128.pow(9 - scale)).unwrap();
                let header = Header {
                    instrument: source.instrument,
                    publisher: source.publisher,
                    flags: 0,
                    event_ns: source.event,
                    received_ns: source.received,
                    sequence: u32::try_from(source.sequence).unwrap(),
                    schema: 4,
                };
                let mut output = Output {
                    observed: at,
                    lane: Lane::OptionsGlbx,
                    generation: 99,
                    source: OutputSource::Observation(Observation::Trade(Trade {
                        header,
                        price: raw_price,
                        size: source.size,
                        action: b'T',
                        side: b'N',
                        dbn_flags: 0,
                        depth: 0,
                        delta_ns: 0,
                    })),
                    state: Default::default(),
                    trade: Some(captured.clone()),
                };
                let outbox = Outbox::new(1, 131072).unwrap();
                let retained = ingress
                    .retain(&output, &outbox, &stop, Deadline::recovery())
                    .await
                    .unwrap()
                    .unwrap();
                assert_eq!(retained.bytes.as_ref(), wire(fixture, "ingressContent"));
                let duplicate = ingress
                    .retain(&output, &outbox, &stop, Deadline::recovery())
                    .await
                    .unwrap()
                    .unwrap();
                assert!(Arc::ptr_eq(&retained, &duplicate));
                let bytes = outbox.retained_bytes().unwrap();
                output.trade = None;
                assert!(
                    ingress
                        .retain(&output, &outbox, &stop, Deadline::recovery())
                        .await
                        .is_err()
                );
                let mut replay = header;
                replay.flags = 2;
                output.source = OutputSource::Observation(Observation::Trade(Trade {
                    header: replay,
                    price: raw_price,
                    size: source.size,
                    action: b'T',
                    side: b'N',
                    dbn_flags: 0,
                    depth: 0,
                    delta_ns: 0,
                }));
                assert!(
                    ingress
                        .retain(&output, &outbox, &stop, Deadline::recovery())
                        .await
                        .unwrap()
                        .is_none()
                );
                assert_eq!(outbox.retained_bytes().unwrap(), bytes);
                assert!(
                    Ingress::new(
                        source.date,
                        source.generation,
                        vec![registration.clone(), registration]
                    )
                    .is_err()
                );
                output.lane = Lane::FuturesGlbx;
                assert!(
                    ingress
                        .retain(&output, &outbox, &stop, Deadline::recovery())
                        .await
                        .is_err()
                );
                stop.cancel();
                assert!(
                    option_pricing::calculate(source, &captured, &stop, Deadline::recovery())
                        .await
                        .is_err()
                );
            }
            assert_eq!(evidence.encode().unwrap(), wire(row, "evidenceSuccess"));
            evidence.outcome = Outcome::Failure(failure.clone());
            assert_eq!(evidence.encode().unwrap(), wire(row, "evidenceFailure"));
            evidence.source.generation = [8; 16];
            assert!(evidence.encode().is_err());
            evidence.source.generation = generation;
            evidence.context = None;
            evidence.underlying = None;
            assert_eq!(evidence.encode().unwrap(), wire(row, "evidenceMissing"));
            evidence.outcome = Outcome::Success(risk.clone());
            assert!(evidence.encode().is_err());
            evidence.context = Some(Arc::new(context.clone()));
            evidence.underlying = Some(Quote {
                contract: "ES20261218".into(),
                quote: Default::default(),
            });
            let mut invalid = risk;
            invalid.values.delta = f64::NAN;
            evidence.outcome = Outcome::Success(invalid);
            assert!(evidence.encode().is_err());
            evidence.outcome = Outcome::Failure(failure);
            evidence.source.dataset = " ".into();
            assert!(evidence.encode().is_err());
        }
        let compressed = context
            .manifest(decimal::parse("6000").unwrap(), true)
            .unwrap();
        let value = rmpv::decode::read_value(&mut compressed.as_slice()).unwrap();
        let array = value.as_array().unwrap();
        let rmpv::Value::Ext(98, length) = &array[0] else {
            panic!("missing manifest framing")
        };
        let size = rmpv::decode::read_value(&mut length.as_slice())
            .unwrap()
            .as_u64()
            .unwrap();
        let content =
            lz4_flex::block::decompress(array[1].as_slice().unwrap(), size as usize).unwrap();
        assert_eq!(
            content,
            wire(row, "manifestContent"),
            "manifest {}",
            row["name"]
        );
        assert!(context.validate_at(at).is_ok());
        let expired = context.validate_at(until).unwrap_err();
        assert_eq!(
            (expired.code, expired.input),
            ("TreasuryStale", "ReferenceValidity")
        );
        let future_observation = context
            .validate_at(observation.curve.retrieved - 1)
            .unwrap_err();
        assert_eq!(
            (future_observation.code, future_observation.input),
            ("TreasuryStale", "ReferenceValidity")
        );
        for (observed, epoch, pricer, code) in [
            (None, generation, engine, "TreasuryUnavailable"),
            (
                Some(&observation),
                [0; 16],
                engine,
                "PricingModelUnsupported",
            ),
            (
                Some(&observation),
                generation,
                "wrong",
                "PricingModelUnsupported",
            ),
        ] {
            let result = Context::prepare(
                contract.clone(),
                calendar.clone(),
                &publications,
                &Conversion::standard(),
                observed,
                epoch,
                pricer,
                at,
                123,
            );
            assert_eq!(result.err().unwrap().code, code);
        }
    }
}

fn find_snapshot(value: &rmpv::Value) -> Option<&[rmpv::Value]> {
    let fields = value.as_array()?;
    if fields.len() == 33 {
        return Some(fields);
    }
    fields.iter().find_map(find_snapshot)
}
fn source_date(
    evidence: &ifm_market_data_native::messaging::option_evidence::Evidence,
) -> time::Date {
    evidence.source.date
}
