//! Frozen chart preparation algorithm from commit 045898ff7. Kept in test/bench
//! code only, so parity and timing comparisons do not retain JSON in production.
use ifm_ui_egui::{models::trade_chart::PreparedTradeChart, wire};
use serde_json::Value;

pub fn legacy(bytes: &[u8]) -> PreparedTradeChart {
    let value: Value = wire::decode(bytes).unwrap();
    let mut result = PreparedTradeChart::default();
    result.end_utc_seconds = value["chartEndUtc"]
        .as_str()
        .and_then(|at| chrono::DateTime::parse_from_rfc3339(at).ok())
        .map(|at| at.timestamp_millis() as f64 / 1000.0);
    if let Some(points) = value["points"].as_array() {
        for point in points {
            let net = point["netSpread"]
                .as_f64()
                .or_else(|| point["netSpread"].as_str().and_then(|s| s.parse().ok()));
            if let (Some(net), Some(at)) = (net, point["atUtc"].as_str()) {
                if let Ok(at) = chrono::DateTime::parse_from_rfc3339(at) {
                    let seconds = at.timestamp_millis() as f64 / 1000.0;
                    result.net.push([seconds, net]);
                    if let Some(forward) = point["forwardSpread"]
                        .as_f64()
                        .or_else(|| point["forwardSpread"].as_str().and_then(|s| s.parse().ok()))
                    {
                        result.forward.push([seconds, forward]);
                    }
                }
            }
        }
    }
    result
}

/// Deterministic synthetic published chart. No financial values are recalculated.
pub fn input(count: usize) -> Vec<u8> {
    let start = chrono::DateTime::parse_from_rfc3339("2026-10-30T00:00:00Z").unwrap();
    let points = (0..count)
        .map(|index| {
            serde_json::json!({
                "atUtc": (start + chrono::Duration::seconds(index as i64)).to_rfc3339(),
                "netSpread": "-12.345678901234567890", "forwardSpread": "13.456789012345678901"
            })
        })
        .collect::<Vec<_>>();
    wire::encode_value(&serde_json::json!({"chartEndUtc":"2026-10-30T06:00:00Z", "points":points}))
        .unwrap()
}
