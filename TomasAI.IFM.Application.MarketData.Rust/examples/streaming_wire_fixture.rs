//! Generates native envelopes for actual managed-consumer compatibility checks.
use base64::Engine;
use ifm_market_data_native::{dataset::transport::Lane, messaging::wire::Frame};
fn main() -> Result<(), Box<dyn std::error::Error>> {
    let rows: Vec<serde_json::Value> = serde_json::from_str(include_str!(
        "../qualification/StreamingWireBridge/envelopes.json"
    ))?;
    let decode = |name: &str, row: &serde_json::Value| {
        base64::engine::general_purpose::STANDARD.decode(row[name].as_str().unwrap())
    };
    let mut output = Vec::new();
    for row in rows {
        let lane = match row["lane"].as_u64().unwrap() {
            1 => Lane::FuturesGlbx,
            2 => Lane::FuturesXcbf,
            _ => Lane::OptionsGlbx,
        };
        let frame = Frame {
            lane,
            value_date: row["valueDate"].as_i64().unwrap() as i32,
            worker: decode("worker", &row)?.try_into().unwrap(),
            generation: decode("generation", &row)?.try_into().unwrap(),
            manifest_revision: row["revision"].as_i64().unwrap(),
            sequence: row["sequence"].as_i64().unwrap(),
            kind: row["kind"].as_u64().unwrap() as u8,
            payload: decode("payload", &row)?,
        };
        output.push(base64::engine::general_purpose::STANDARD.encode(frame.encode()?));
    }
    std::fs::write(
        std::env::args().nth(1).ok_or("output path required")?,
        serde_json::to_vec(&output)?,
    )?;
    Ok(())
}
