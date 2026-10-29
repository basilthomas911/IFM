//! Original CLR decimal outcomes, including scale-28 rounding and overflow.
use ifm_market_data_native::{
    messaging::codec, option_chain::qualification::Decimal, reference::decimal_arithmetic as math,
};
fn value(v: &serde_json::Value) -> Decimal {
    let v = v.as_array().unwrap();
    Decimal {
        bits: [
            v[0].as_i64().unwrap() as u32,
            v[1].as_i64().unwrap() as u32,
            v[2].as_i64().unwrap() as u32,
            v[3].as_i64().unwrap() as u32,
        ],
    }
}
#[test]
fn all_native_arithmetic_matches_clr_values_scale_and_overflow() {
    let cases: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/decimal-arithmetic.json"
    ))
    .unwrap();
    assert!(cases.as_array().unwrap().len() > 12000);
    for (index, case) in cases.as_array().unwrap().iter().enumerate() {
        let a = value(&case["left"]);
        let b = value(&case["right"]);
        let result = match case["op"].as_str().unwrap() {
            "add" => math::add(a, b),
            "subtract" => math::subtract(a, b),
            _ => math::multiply(a, b),
        };
        let expected = case["result"].as_array().map(|_| value(&case["result"]));
        assert_eq!(
            result.is_some(),
            expected.is_some(),
            "overflow case {index} {}",
            case["op"]
        );
        if let (Some(result), Some(expected)) = (result, expected) {
            assert_eq!(
                result.compare(expected),
                Some(std::cmp::Ordering::Equal),
                "value case {index} {}",
                case["op"]
            );
            assert_eq!(
                codec::decimal_bits(result).unwrap().as_str().unwrap(),
                case["text"].as_str().unwrap(),
                "scale case {index} {}",
                case["op"]
            );
        }
    }
}
