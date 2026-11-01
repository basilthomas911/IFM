//! Snapshot/cache probes compiled against preserved baseline and candidate libraries.
use ifm_market_data_native::{
    messaging::option_cache::Publication,
    option_chain::composition::model::{Instrument, Snapshot, Valued},
};
use std::{hint::black_box, time::Instant};
pub(crate) fn snapshot(count: usize) -> Snapshot {
    Snapshot {
        ranking: None,
        schema: 2,
        id: [1; 16],
        scope: "fixture".into(),
        token: "policy/v1".into(),
        horizon: "Daily".into(),
        generation: [2; 16],
        at: 17_000_000_000_000_000,
        until: 17_000_000_001_000_000,
        instruments: (0..count)
            .map(|index| Valued {
                instrument: Instrument {
                    contract: format!("Contract{index:04}"),
                    quote: None,
                    context: None,
                    strike: None,
                    call: None,
                    underlying: None,
                    future: None,
                    selection: None,
                    volume: None,
                    interest: None,
                    statistics_at: None,
                    unavailable: Some(
                        "Deterministic unavailable fixture retains full diagnostic context"
                            .repeat(4),
                    ),
                    last_event: None,
                    last_received: None,
                    volume_date: None,
                    interest_date: None,
                },
                valuation: None,
            })
            .collect(),
        digest: String::new(),
    }
}
fn measure(
    name: &str,
    mut setup: impl FnMut() -> Snapshot,
    mut run: impl FnMut(Snapshot),
) -> String {
    for _ in 0..3 {
        run(setup());
    }
    let mut samples = Vec::with_capacity(31);
    for _ in 0..31 {
        let snapshot = setup();
        let start = Instant::now();
        run(snapshot);
        samples.push(start.elapsed().as_nanos() as u64);
    }
    samples.sort_unstable();
    format!(
        "{{\"scenario\":\"{name}\",\"p50Ns\":{},\"p95Ns\":{},\"p99Ns\":{},\"samplesNs\":{samples:?}}}",
        samples[15], samples[29], samples[30]
    )
}
fn main() {
    let mut results = Vec::new();
    for count in [128, 512, 1024] {
        let mut fixture = snapshot(count);
        results.push(measure(
            &format!("snapshot-seal-{count}"),
            || fixture.clone(),
            |mut value| {
                value.seal().unwrap();
                black_box(value);
            },
        ));
        fixture.seal().unwrap();
        results.push(measure(
            &format!("cache-publication-{count}"),
            || fixture.clone(),
            |value| {
                #[cfg(not(optimized))]
                let publication =
                    Publication::new(&value, 739914, "policy/v1", 1, "fixture").unwrap();
                #[cfg(optimized)]
                let publication =
                    Publication::owned(value, 739914, "policy/v1", 1, "fixture").unwrap();
                black_box(publication);
            },
        ));
        let publication = Publication::new(&fixture, 739914, "policy/v1", 1, "fixture").unwrap();
        results.push(measure(
            &format!("cache-chunks-{count}"),
            || Snapshot {
                instruments: vec![],
                ..fixture.clone()
            },
            |_| {
                for index in 0..publication.chunks() {
                    black_box(publication.frame(index).unwrap());
                }
            },
        ));
    }
    println!("{{\"schema\":1,\"results\":[{}]}}", results.join(","));
}
