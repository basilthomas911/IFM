//! Actual legacy stored-input selection over the new copied-fact port. No
//! native caller may replace raw EOD/Bollinger eligibility with a C# decision.
use ifm_market_data_native::{option_chain::browser_inputs, reference::decimal};
#[test]
fn all_managed_fallback_precedence_dates_and_decimal_conversions_match() {
    let rows: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/BrowserInputsOracle/inputs.json"
    ))
    .unwrap();
    for (i, test) in rows.as_array().unwrap().iter().enumerate() {
        let result = browser_inputs::select(
            &serde_json::to_vec(&test["raw"]).unwrap(),
            test["date"].as_i64().unwrap() as i32,
        );
        if test["failed"].as_bool().unwrap() {
            assert!(result.is_err(), "case {i}");
            continue;
        }
        let result = result.unwrap();
        for (actual, expected) in [
            (result.price, &test["price"]),
            (result.deviation, &test["deviation"]),
        ] {
            assert_eq!(actual.is_some(), !expected.is_null(), "case {i}");
            if let Some(actual) = actual {
                assert_eq!(
                    actual.compare(decimal::parse(expected.as_str().unwrap()).unwrap()),
                    Some(std::cmp::Ordering::Equal),
                    "case {i}"
                );
            }
        }
        assert_eq!(
            result.date,
            test["observed"].as_i64().map(|value| value as i32),
            "case {i}"
        );
    }
}
#[test]
fn malformed_shapes_scalars_dates_and_capacity_are_explicit() {
    for raw in [
        r#"null"#,
        r#"[]"#,
        r#"[null,null,null]"#,
        r#"[null,null,null,null,null]"#,
        r#"[[1,"bad","0000000000000000"],null,null,null]"#,
        r#"[[1,"1","not bits"],null,null,null]"#,
        r#"[null,null,["1","1",0,1],null]"#,
        r#"[null,null,["1","1",true,-1],null]"#,
    ] {
        assert!(browser_inputs::select(raw.as_bytes(), 1).is_err(), "{raw}");
    }
    assert!(browser_inputs::select(b"[null,null,null,null]", 0).is_err());
    assert!(browser_inputs::select(&vec![b' '; 65537], 1).is_err());
    let empty = browser_inputs::select(b"[null,null,null,null]", 1).unwrap();
    assert!(empty.price.is_none() && empty.deviation.is_none() && empty.date.is_none());
}
