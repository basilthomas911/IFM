//! Deterministic offline performance probes; no sockets, provider keys or running IFM services.
use ifm_market_data_native::{
    dataset::{
        book::Book,
        records::{Header, Observation, Quote as WireQuote},
    },
    latest::Asset,
    messaging::codec,
    option_chain::{
        coalesced::{Coalesced, Context},
        qualification::{Decimal, Quote, Request as Qualification},
    },
    pricing::{Calculator, Request, Settings},
    runtime::Deadline,
};
use std::{
    alloc::{GlobalAlloc, Layout, System},
    hint::black_box,
    sync::atomic::{AtomicBool, AtomicU64, Ordering},
    time::Instant,
};
use tokio_util::sync::CancellationToken;

/// Allocation counting is enabled only in a separate untimed pass.
struct Counting;
static COUNTING: AtomicBool = AtomicBool::new(false);
static ALLOCATIONS: AtomicU64 = AtomicU64::new(0);
static BYTES: AtomicU64 = AtomicU64::new(0);
// SAFETY: every allocation operation delegates unchanged to the system allocator.
unsafe impl GlobalAlloc for Counting {
    unsafe fn alloc(&self, layout: Layout) -> *mut u8 {
        if COUNTING.load(Ordering::Relaxed) {
            ALLOCATIONS.fetch_add(1, Ordering::Relaxed);
            BYTES.fetch_add(layout.size() as u64, Ordering::Relaxed);
        }
        // SAFETY: caller supplies the allocator layout required by System.
        unsafe { System.alloc(layout) }
    }
    unsafe fn dealloc(&self, ptr: *mut u8, layout: Layout) {
        // SAFETY: caller supplies the original allocation and matching layout.
        unsafe { System.dealloc(ptr, layout) }
    }
    unsafe fn realloc(&self, ptr: *mut u8, layout: Layout, size: usize) -> *mut u8 {
        if COUNTING.load(Ordering::Relaxed) {
            ALLOCATIONS.fetch_add(1, Ordering::Relaxed);
            BYTES.fetch_add(size as u64, Ordering::Relaxed);
        }
        // SAFETY: caller supplies a live allocation and valid new size.
        unsafe { System.realloc(ptr, layout, size) }
    }
}
#[global_allocator]
static ALLOCATOR: Counting = Counting;

/// Measure batch latency separately from allocation counts; construction is outside each probe.
fn measure(name: &str, operations: usize, mut run: impl FnMut()) -> serde_json::Value {
    for _ in 0..3 {
        run();
    }
    let mut samples = Vec::with_capacity(31);
    for _ in 0..31 {
        let start = Instant::now();
        run();
        samples.push(start.elapsed().as_nanos() as u64);
    }
    samples.sort_unstable();
    ALLOCATIONS.store(0, Ordering::Relaxed);
    BYTES.store(0, Ordering::Relaxed);
    COUNTING.store(true, Ordering::Relaxed);
    run();
    COUNTING.store(false, Ordering::Relaxed);
    serde_json::json!({"scenario":name,"operationsPerSample":operations,"samplesNs":samples,"p50NsPerOp":samples[15] as f64/operations as f64,"p95NsPerOp":samples[29] as f64/operations as f64,"p99NsPerOp":samples[30] as f64/operations as f64,"allocationsPerSample":ALLOCATIONS.load(Ordering::Relaxed),"allocatedBytesPerSample":BYTES.load(Ordering::Relaxed)})
}
const AT: i64 = 17_000_000_000_000_000;
fn decimal(value: u64) -> Decimal {
    Decimal {
        bits: [value as u32, (value >> 32) as u32, 0, 0],
    }
}
fn quote(value: u64) -> Quote {
    Quote {
        generation_low: 1,
        event: AT,
        received: AT,
        sequence: 1,
        bid: decimal(value),
        ask: decimal(value),
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
        model: Request {
            forward: 0.,
            strike: 5000.,
            time: 0.,
            rate: 0.04,
            call: true,
            american: false,
            futures_style: false,
        },
        qualification: Qualification {
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
/// Build a reference from the same exported managed oracle used by parity qualification.
fn reference() -> ifm_market_data_native::option_chain::context::Context {
    use base64::Engine;
    use ifm_market_data_native::{
        option_chain::convention::Convention,
        reference::{
            calendar::Calendar,
            decimal,
            treasury_curve::{Curve, Point},
            treasury_policy::{Conversion, Observation, Publication, Publications},
        },
    };
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/contexts.json"
    ))
    .unwrap();
    let row = &rows[0];
    let bytes = base64::engine::general_purpose::STANDARD
        .decode(row["contract"].as_str().unwrap())
        .unwrap();
    let contract =
        Convention::new(rmpv::decode::read_value(&mut bytes.as_slice()).unwrap()).unwrap();
    let at = row["at"].as_i64().unwrap();
    let first = time::Date::from_calendar_date(2026, time::Month::October, 1).unwrap();
    let calendar = Calendar::new(
        "fixture/v1".into(),
        "Eastern Standard Time",
        first,
        first + time::Duration::days(59),
        row["rollover"].as_u64().unwrap(),
        (0..60).map(|i| first + time::Duration::days(i)).collect(),
    )
    .unwrap();
    let publications = Publications::new(
        "publication/v1".into(),
        at - 1000,
        row["until"].as_i64().unwrap(),
        vec![Publication {
            date: first + time::Duration::days(25),
            available: at - 1000,
        }],
        "Eastern Standard Time",
    )
    .unwrap();
    let observation = Observation::official(Curve {
        date: first + time::Duration::days(25),
        retrieved: at - 600_000_000,
        rates: [(1, "4.0000"), (2, "4.10"), (3, "4.2")]
            .into_iter()
            .map(|(months, p)| Point {
                months,
                percentage: decimal::parse(p).unwrap(),
            })
            .collect(),
    });
    let engine = contract.engine();
    ifm_market_data_native::option_chain::context::Context::prepare(
        contract,
        calendar,
        &publications,
        &Conversion::standard(),
        Some(&observation),
        [1; 16],
        engine,
        at,
        123,
    )
    .unwrap()
}
fn selection() -> Vec<serde_json::Value> {
    use ifm_market_data_native::option_chain::{browser_window as browser, candidates};
    let token = CancellationToken::new();
    let mut samples = Vec::new();
    for count in [128, 1024, 2048] {
        let ids: Vec<_> = (0..count).map(|id| format!("ES-{id:04}")).collect();
        let rows: Vec<_> = ids
            .iter()
            .enumerate()
            .map(|(i, id)| browser::Row {
                contract: id,
                strike: (4000 + i) as f64,
                kind: if i % 2 == 0 { "Call" } else { "Put" },
            })
            .collect();
        let required = Default::default();
        samples.push(measure(&format!("browser-nearest-{count}"), 16, || {
            for _ in 0..16 {
                black_box(
                    browser::select(
                        &rows,
                        Some(decimal(4500)),
                        browser::Method::Bollinger {
                            deviation: None,
                            multiplier: 2.,
                        },
                        &required,
                        &token,
                        Deadline::recovery(),
                    )
                    .unwrap(),
                );
            }
        }));
        let range = candidates::Range {
            minimum: decimal(0),
            target: Decimal {
                bits: [25, 0, 0, 2 << 16],
            },
            maximum: decimal(1),
        };
        let policy = candidates::Policy {
            put: range,
            call: range,
            put_widths: vec![decimal(10)],
            call_widths: vec![decimal(10)],
            age: 1000,
            skew: 1000,
            size: 1,
            spread: decimal(5),
            maximum: 8,
        };
        let candidates: Vec<_> = ids
            .iter()
            .enumerate()
            .map(|(i, id)| candidates::Candidate {
                id,
                underlying: "ES",
                expiration: AT + 10_000_000,
                strike: decimal(4000 + i as u64),
                call: i % 2 == 0,
                delta: Some((i % 50) as f64 / 100.),
                until: Some(AT + 10_000_000),
                quote: Some(quote(100)),
                future: Some(quote(4500)),
            })
            .collect();
        samples.push(measure(&format!("candidate-top-eight-{count}"), 16, || {
            for _ in 0..16 {
                let result =
                    candidates::select(&candidates, &policy, AT, &token, Deadline::recovery())
                        .unwrap();
                assert!(!result.is_empty());
                black_box(result);
            }
        }));
    }
    samples
}

fn main() {
    if std::env::args().nth(1).as_deref() == Some("--selection") {
        println!("{}", serde_json::json!(selection()));
        return;
    }
    let runtime = tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()
        .unwrap();
    let token = CancellationToken::new();
    let mut results = Vec::new();
    for count in [100, 1000, 2048] {
        let engine = Coalesced::new(count, 64, 5000).unwrap();
        for id in 1..=count as u32 {
            engine.register(id, 100, context(id as u128), true).unwrap();
            engine.observe(id, 100, quote(100)).unwrap();
        }
        results.push(measure(&format!("selection-sweep-{count}"), count, || {
            black_box(
                runtime
                    .block_on(engine.sweep(
                        false,
                        || Ok(AT),
                        |_| Some(quote(5000)),
                        &token,
                        Deadline::recovery(),
                    ))
                    .unwrap(),
            );
        }));
        results.push(measure(&format!("risk-sweep-{count}"), count, || {
            black_box(
                runtime
                    .block_on(engine.sweep(
                        true,
                        || Ok(AT),
                        |_| Some(quote(5000)),
                        &token,
                        Deadline::recovery(),
                    ))
                    .unwrap(),
            );
        }));
    }
    let calculator = Calculator::new(Settings::default()).unwrap();
    for american in [false, true] {
        let model = Request {
            forward: 5000.,
            strike: 5000.,
            time: 30. / 365.,
            rate: 0.04,
            call: true,
            american,
            futures_style: false,
        };
        results.push(measure(
            if american {
                "american-price-delta"
            } else {
                "european-price-delta"
            },
            8,
            || {
                for _ in 0..8 {
                    black_box(
                        runtime
                            .block_on(calculator.price_delta(
                                model,
                                0.2,
                                &token,
                                Deadline::recovery(),
                            ))
                            .unwrap(),
                    );
                }
            },
        ));
        results.push(measure(
            if american {
                "american-risk"
            } else {
                "european-risk"
            },
            2,
            || {
                for _ in 0..2 {
                    black_box(
                        runtime
                            .block_on(calculator.price(model, 0.2, &token, Deadline::recovery()))
                            .unwrap(),
                    );
                }
            },
        ));
    }
    for size in [128, 4096] {
        let value = rmpv::Value::Array(vec![
            1.into(),
            "contract".into(),
            rmpv::Value::Binary(vec![42; size]),
        ]);
        results.push(measure(&format!("encode-compress-{size}"), 128, || {
            for _ in 0..128 {
                black_box(codec::compress(codec::encode(&value).unwrap()).unwrap());
            }
        }));
    }
    for size in [128, 4096] {
        let frame = ifm_market_data_native::messaging::wire::Frame {
            lane: ifm_market_data_native::dataset::transport::Lane::FuturesGlbx,
            value_date: 739914,
            worker: [1; 16],
            generation: [2; 16],
            manifest_revision: 1,
            sequence: 1,
            kind: 2,
            payload: vec![42; size],
        };
        results.push(measure(&format!("frame-content-{size}"), 128, || {
            for _ in 0..128 {
                black_box(frame.content().unwrap());
            }
        }));
        results.push(measure(&format!("frame-encode-{size}"), 128, || {
            for _ in 0..128 {
                black_box(frame.encode().unwrap());
            }
        }));
    }
    let reference = reference();
    results.push(measure("reference-digest", 128, || {
        for _ in 0..128 {
            black_box(reference.digest().unwrap());
        }
    }));
    results.push(measure("reference-value", 128, || {
        for _ in 0..128 {
            black_box(reference.value().unwrap());
        }
    }));
    let owner = ifm_market_data_native::runtime::owner::Owner::default();
    results.push(measure("pending-notifications", 10000, || {
        owner.take_signals().unwrap();
        for _ in 0..10000 {
            owner.signal(1).unwrap();
        }
    }));
    let indexed = std::env::args().any(|argument| argument == "--indexed");
    for count in [100_u32, 1000, 2048] {
        let rows: Vec<_> = (1..=count)
            .map(|id| (id, format!("Contract{id}")))
            .collect();
        let instruments: std::collections::BTreeMap<_, _> = rows
            .iter()
            .enumerate()
            .map(|(index, row)| (row.0, index))
            .collect();
        let contracts: std::collections::BTreeMap<_, _> = rows
            .iter()
            .enumerate()
            .map(|(index, row)| (row.1.clone(), index))
            .collect();
        results.push(measure(
            &format!("instrument-lookup-kernel-{count}"),
            10000,
            || {
                for i in 0..10000 {
                    let id = black_box(1 + (i * 37) % count);
                    let row = if indexed {
                        instruments.get(&id).map(|index| &rows[*index])
                    } else {
                        rows.iter().find(|row| row.0 == id)
                    };
                    black_box(row);
                }
            },
        ));
        results.push(measure(
            &format!("contract-lookup-kernel-{count}"),
            10000,
            || {
                for i in 0..10000 {
                    let name = black_box(&rows[((i * 37) % count) as usize].1);
                    let row = if indexed {
                        contracts.get(name).map(|index| &rows[*index])
                    } else {
                        rows.iter().find(|row| &row.1 == name)
                    };
                    black_box(row);
                }
            },
        ));
    }
    let date = time::Date::from_calendar_date(2026, time::Month::October, 26).unwrap();
    let mut book = Book::<()>::new(date, 1, 1000).unwrap();
    for id in 1..=1000 {
        book.register(id, &format!("Contract{id}"), Asset::Futures, 4)
            .unwrap();
    }
    let mut sequence = 0;
    results.push(measure("book-quotes-1000", 10000, || {
        for i in 0..10000 {
            sequence += 1;
            let observation = Observation::Quote(WireQuote {
                header: Header {
                    instrument: 1 + i % 1000,
                    publisher: 1,
                    flags: 0,
                    event_ns: 1_000_000_000 + i64::from(sequence),
                    received_ns: 1_000_000_000 + i64::from(sequence),
                    sequence,
                    schema: 1,
                },
                bid: 5_000_000_000_000 + i64::from(sequence),
                ask: 5_001_000_000_000 + i64::from(sequence),
                bid_size: 1,
                ask_size: 1,
                bid_count: 1,
                ask_count: 1,
            });
            black_box(book.apply(1, observation, AT).unwrap());
        }
    }));
    println!(
        "{}",
        serde_json::to_string_pretty(
            &serde_json::json!({"schema":1,"profile":"release","results":results})
        )
        .unwrap()
    );
}
