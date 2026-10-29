//! Unchanged C# ATM-IV outputs qualify exact native eligibility and grouping.
use ifm_market_data_native::{
    option_chain::browser::atm::{Row, estimate_rows},
    reference::decimal,
};
fn value(input: &serde_json::Value) -> Option<f64> {
    input
        .as_str()
        .map(|bits| f64::from_bits(u64::from_str_radix(bits, 16).unwrap()))
}
#[test]
fn every_current_backend_atm_iv_case_matches_exactly() {
    let tests: serde_json::Value = serde_json::from_slice(
        &zstd::decode_all(include_bytes!("../qualification/AtmIvOracle/cases.json.zst").as_slice())
            .unwrap(),
    )
    .unwrap();
    assert_eq!(tests.as_array().unwrap().len(), 1600);
    for (index, test) in tests.as_array().unwrap().iter().enumerate() {
        let rows = test["rows"]
            .as_array()
            .unwrap()
            .iter()
            .map(|row| Row {
                contract: row["id"].as_str().unwrap(),
                strike: row["strike"].as_str().and_then(decimal::parse),
                call: row["call"].as_bool(),
                quoted: row["quoted"].as_bool().unwrap(),
                valuation: value(&row["full"]),
                selection: value(&row["selection"]).map(|iv| {
                    (
                        iv,
                        row["calculated"].as_i64().unwrap(),
                        row["until"].as_i64().unwrap(),
                    )
                }),
            })
            .collect::<Vec<_>>();
        let actual = estimate_rows(
            &rows,
            decimal::parse(test["forward"].as_str().unwrap()).unwrap(),
            test["at"].as_i64().unwrap(),
        )
        .unwrap();
        assert_eq!(
            actual.map(f64::to_bits),
            value(&test["expected"]).map(f64::to_bits),
            "ATM parity case {index}"
        );
    }
}
#[test]
fn oversized_native_estimator_input_is_rejected_before_calculation() {
    let rows = (0..2049)
        .map(|_| Row {
            contract: "x",
            strike: None,
            call: None,
            quoted: false,
            valuation: None,
            selection: None,
        })
        .collect::<Vec<_>>();
    assert!(estimate_rows(&rows, decimal::parse("6000").unwrap(), 0).is_err());
}
