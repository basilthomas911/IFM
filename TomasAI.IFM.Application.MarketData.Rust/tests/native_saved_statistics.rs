//! Raw saved statistics are not permission to change date/negative-value policy.
use ifm_market_data_native::dataset::background::statistics::parse;
use serde_json::json;
fn bytes(value: serde_json::Value) -> Vec<u8> {
    serde_json::to_vec(&value).unwrap()
}
#[test]
fn exact_optional_extreme_and_ineligible_raw_values_survive_for_native_policy() {
    let rows = parse(&bytes(json!([
        ["put", i64::MAX, 739920, null, null],
        ["call", -1, 0, i64::MIN, 3652058]
    ])))
    .unwrap();
    assert_eq!(rows["put"].volume, Some(i64::MAX));
    assert!(rows["put"].open_interest.is_none());
    assert_eq!(rows["call"].volume, Some(-1));
    assert_eq!(rows["call"].open_interest, Some(i64::MIN));
    assert!(parse(b"[]").unwrap().is_empty());
}
#[test]
fn malformed_duplicate_truncated_and_oversized_sources_fail_completely() {
    for value in [
        json!(null),
        json!([["put", 1, 1, 2]]),
        json!([["", 1, 1, 2, 1]]),
        json!([["put", 1, -1, 2, 1]]),
        json!([["put", 1, 1, 2, 3652059]]),
        json!([["put", 1.5, 1, 2, 1]]),
        json!([["put", 1, 1, 2, 1], ["put", 2, 1, 2, 1]]),
    ] {
        assert!(parse(&bytes(value)).is_err());
    }
    assert!(parse(b"[[").is_err());
    assert!(parse(&vec![b' '; 4 * 1024 * 1024 + 1]).is_err());
    assert!(
        parse(&bytes(json!(vec![
            json!(["x", null, null, null, null]);
            100001
        ])))
        .is_err()
    );
}
