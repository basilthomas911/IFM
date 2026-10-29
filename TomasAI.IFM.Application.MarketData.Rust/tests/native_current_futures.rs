//! Actual C# resolver fixtures; the native tests do not reproduce its algorithm.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    catalog::{Definition, current::select},
    messaging::codec,
};
use std::sync::Arc;
fn date(text: &str) -> time::Date {
    let parts = text
        .split('-')
        .map(|n| n.parse::<i32>().unwrap())
        .collect::<Vec<_>>();
    time::Date::from_calendar_date(
        parts[0],
        time::Month::try_from(parts[1] as u8).unwrap(),
        parts[2] as u8,
    )
    .unwrap()
}
fn rows(fixture: &serde_json::Value) -> Vec<Arc<Definition>> {
    let baseline: serde_json::Value =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    let template = Definition::parse("GLBX.MDP3", baseline[0]["json"].as_str().unwrap()).unwrap();
    fixture["rows"]
        .as_array()
        .unwrap()
        .iter()
        .map(|item| {
            let mut row = template.clone();
            row.kind = 1;
            row.symbol = item["Symbol"].as_str().unwrap().into();
            row.exchange = "XCME".into();
            row.currency = item["Currency"].as_str().unwrap().into();
            row.settlement_currency = item["Settlement"].as_str().unwrap().into();
            row.multiplier = item["Multiplier"].as_i64().map(|n| n as i32);
            row.query_maturity = item["Maturity"].as_str().map(date);
            row.maturity = row.query_maturity;
            row.expiration = item["Expiration"].as_u64();
            row.query_maturity_invalid = false;
            Arc::new(row)
        })
        .collect()
}
#[test]
fn native_current_and_term_structure_selection_match_ninety_actual_csharp_cases() {
    let fixtures: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/CurrentFuturesBridge/current.json"
    ))
    .unwrap();
    let mut cases = 0;
    for fixture in fixtures.as_array().unwrap() {
        let result = select(
            &rows(fixture),
            fixture["symbol"].as_str().unwrap(),
            time::Date::from_julian_day(fixture["date"].as_i64().unwrap() as i32 + 1721426)
                .unwrap(),
            fixture["count"].as_u64().unwrap() as usize,
            fixture["fallback"].as_str(),
        )
        .unwrap();
        assert_eq!(
            codec::encode(&result.value()).unwrap(),
            STANDARD
                .decode(fixture["content"].as_str().unwrap())
                .unwrap(),
            "{} {} count {}",
            fixture["symbol"],
            fixture["mode"],
            fixture["count"]
        );
        cases += 1;
    }
    assert_eq!(cases, 90);
}
#[test]
fn malformed_maturity_input_bounds_and_day_overflow_are_explicit() {
    let fixture: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/CurrentFuturesBridge/current.json"
    ))
    .unwrap();
    let mut rows = rows(&fixture[0]);
    let mut row = (*rows[0]).clone();
    row.query_maturity_invalid = true;
    rows[0] = Arc::new(row);
    let at = date("2026-08-14");
    assert_eq!(
        select(&rows, "ES", at, 1, None).unwrap().failure,
        Some("InvalidMaturity")
    );
    assert!(select(&rows, "ES", at, 0, None).is_err());
    assert!(select(&rows, "", at, 1, None).is_err());
    assert!(select(&rows, "ES", date("9999-12-31"), 1, None).is_err());
}
