//! Financial ranking uses the actual CLR decimal cast, including boundary
//! rounding. These fixtures are exported independently by the C# oracle.
#[test]
fn every_double_conversion_matches_clr_bits_or_overflow() {
    let cases: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/double-decimal.json"
    ))
    .unwrap();
    assert!(cases.as_array().unwrap().len() > 8000);
    for case in cases.as_array().unwrap() {
        let bits = u64::from_str_radix(case["input"].as_str().unwrap(), 16).unwrap();
        let actual = ifm_market_data_native::reference::decimal::from_double(f64::from_bits(bits));
        let expected = case["bits"].as_array().map(|v| {
            [
                v[0].as_i64().unwrap() as u32,
                v[1].as_i64().unwrap() as u32,
                v[2].as_i64().unwrap() as u32,
                v[3].as_i64().unwrap() as u32,
            ]
        });
        assert_eq!(actual.map(|v| v.bits), expected, "double bits {bits:016x}");
    }
}

#[test]
fn legacy_strikes_match_existing_lossless_read_model_translation() {
    use ifm_market_data_native::reference::decimal::{exact_strike, parse};
    let cases: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/exact-strike.json"
    ))
    .unwrap();
    assert!(cases.as_array().unwrap().len() > 57000);
    for case in cases.as_array().unwrap() {
        let legacy =
            f64::from_bits(u64::from_str_radix(case["input"].as_str().unwrap(), 16).unwrap());
        let exact = case["exact"].as_str().map(|v| parse(v).unwrap());
        let actual = exact_strike(legacy, exact).ok();
        let expected = case["result"].as_str().map(|v| parse(v).unwrap());
        assert_eq!(
            actual.map(|v| v.bits),
            expected.map(|v| v.bits),
            "legacy={} / {}, exact={:?}, parsed={:?}",
            case["input"],
            legacy,
            exact.map(|v| v.bits),
            ifm_market_data_native::reference::decimal::parse_json(&legacy.to_string()).map(|v| (
                v.bits,
                ifm_market_data_native::reference::decimal::to_double(v)
            ))
        );
    }
}
