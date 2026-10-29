//! IEEE equality of decimal midpoint preparation, including high64 coefficients.
#[test]
fn nanounit_midpoint_matches_csharp_decimal_half_add_and_cast() {
    let cases: Vec<serde_json::Value> =
        serde_json::from_str(include_str!("../baseline/MidpointOracle.json")).unwrap();
    for (index, c) in cases.iter().enumerate() {
        let actual = ifm_market_data_native::pricing::midpoint::midpoint(
            c["bid"].as_i64().unwrap(),
            c["ask"].as_i64().unwrap(),
        )
        .map(f64::to_bits);
        assert_eq!(actual, c["bits"].as_u64(), "case={index} {c}");
    }
}
