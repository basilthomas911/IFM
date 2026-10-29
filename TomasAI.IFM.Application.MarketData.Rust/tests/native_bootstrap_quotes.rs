//! Exact quote qualification at the setup-only boundary; no execution IV.
use ifm_market_data_native::{
    dataset::bootstrap_quotes::midpoint,
    latest::{QuoteValue, Tick},
    reference::decimal,
};
fn quote(bid: Option<i64>, ask: Option<i64>, at: i64) -> Tick<QuoteValue> {
    Tick {
        sequence: 1,
        event_ticks: at,
        receive_ticks: at,
        value: QuoteValue {
            bid,
            ask,
            bid_size: 0,
            ask_size: 0,
            bid_count: 0,
            ask_count: 0,
            local_received_ticks: at,
        },
    }
}
#[test]
fn exact_midpoint_handles_nanounits_and_largest_provider_prices_without_overflow() {
    for (bid, ask, expected) in [
        (1, 1, "0.000000001"),
        (1, 2, "0.0000000015"),
        (6_000_000_000_000, 6_000_250_000_000, "6000.125"),
        (i64::MAX, i64::MAX, "9223372036.854775807"),
    ] {
        let actual = midpoint(quote(Some(bid), Some(ask), 100), 100).unwrap();
        assert_eq!(
            actual.compare(decimal::parse(expected).unwrap()),
            Some(std::cmp::Ordering::Equal)
        );
    }
}
#[test]
fn missing_crossed_nonpositive_future_and_stale_quotes_are_never_bootstrap_prices() {
    for (bid, ask) in [
        (None, Some(2)),
        (Some(1), None),
        (Some(0), Some(1)),
        (Some(-1), Some(2)),
        (Some(3), Some(2)),
    ] {
        assert!(midpoint(quote(bid, ask, 100), 100).is_none());
    }
    assert!(midpoint(quote(Some(1), Some(2), 101), 100).is_none());
    assert!(midpoint(quote(Some(1), Some(2), 100), 9_000_000_100).is_some());
    assert!(midpoint(quote(Some(1), Some(2), 100), 9_000_000_101).is_none());
    assert!(midpoint(quote(Some(1), Some(2), i64::MIN), i64::MAX).is_none());
}

#[test]
fn bootstrap_midpoint_matches_every_actual_clr_decimal_bit_pattern() {
    let rows: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/bootstrap-midpoints.json")).unwrap();
    assert_eq!(rows.as_array().unwrap().len(), 105);
    for row in rows.as_array().unwrap() {
        let bid = row["bid"].as_i64().unwrap();
        let ask = row["ask"].as_i64().unwrap();
        let actual = midpoint(quote(Some(bid), Some(ask), 100), 100).unwrap();
        let expected: Vec<u32> = row["bits"]
            .as_array()
            .unwrap()
            .iter()
            .map(|n| n.as_u64().unwrap() as u32)
            .collect();
        assert_eq!(
            actual.bits.as_slice(),
            expected.as_slice(),
            "bid={bid}; ask={ask}"
        );
    }
}
