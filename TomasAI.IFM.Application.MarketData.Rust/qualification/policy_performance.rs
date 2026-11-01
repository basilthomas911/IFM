//! Repeated immutable strategy-policy hash probe using the exported managed fixture.
use ifm_market_data_native::option_chain::strategy;
use std::{hint::black_box, time::Instant};
fn main() {
    let fixture: serde_json::Value =
        serde_json::from_str(include_str!("ContextWireBridge/strategy-policy.json")).unwrap();
    let policy = strategy::from_command(&fixture[0]["command"]["parameters"]).unwrap();
    for _ in 0..128 {
        black_box(policy.hash().unwrap());
    }
    let mut samples = Vec::new();
    for _ in 0..31 {
        let start = Instant::now();
        for _ in 0..10000 {
            black_box(policy.hash().unwrap());
        }
        samples.push(start.elapsed().as_nanos() as u64);
    }
    samples.sort_unstable();
    println!(
        "{{\"scenario\":\"immutable-policy-hash\",\"operationsPerSample\":10000,\"p50NsPerOp\":{},\"p95NsPerOp\":{},\"samplesNs\":{samples:?}}}",
        samples[15] as f64 / 10000.,
        samples[29] as f64 / 10000.
    );
}
