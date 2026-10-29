//! Complete native coverage plans match the existing C# planner, including
//! persistent envelope reuse and all-or-nothing failure reason precedence.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    option_chain::{
        coverage::{Definition, Planner},
        strategy::Parameters,
    },
    reference::{calendar::Zone, decimal},
    runtime::Deadline,
};
use tokio_util::sync::CancellationToken;
fn fixtures() -> serde_json::Value {
    serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/coverage.json"
    ))
    .unwrap()
}
#[test]
fn all_current_csharp_universe_memberships_scope_ids_and_failure_modes_match() {
    let cases = fixtures();
    assert_eq!(cases.as_array().unwrap().len(), 21);
    let mut steps = 0;
    for case in cases.as_array().unwrap() {
        let bytes = STANDARD
            .decode(case["parameters"].as_str().unwrap())
            .unwrap();
        let params =
            Parameters::new(rmpv::decode::read_value(&mut bytes.as_slice()).unwrap()).unwrap();
        let definitions: Vec<_> = case["rows"]
            .as_array()
            .unwrap()
            .iter()
            .map(|row| Definition {
                contract: row["contract"].as_str().unwrap(),
                root: row["root"].as_str().unwrap(),
                underlying: row["underlying"].as_str().unwrap(),
                strike: decimal::parse(row["strike"].as_str().unwrap()).unwrap(),
                expiration: row["expiration"].as_u64(),
            })
            .collect();
        let prices = case["prices"].as_object().map(|items| {
            items
                .iter()
                .map(|(key, value)| {
                    (
                        key.clone(),
                        decimal::parse(value.as_str().unwrap()).unwrap(),
                    )
                })
                .collect()
        });
        let date =
            time::Date::from_julian_day(case["date"].as_i64().unwrap() as i32 + 1721426).unwrap();
        let planner = Planner::default();
        for step in case["steps"].as_array().unwrap() {
            let result = planner
                .plan(
                    &params,
                    date,
                    &definitions,
                    case["complete"].as_bool().unwrap(),
                    decimal::parse(step["forward"].as_str().unwrap()).unwrap(),
                    case["iv"].as_f64().unwrap(),
                    Zone::new(case["zone"].as_str().unwrap()).unwrap(),
                    prices.as_ref(),
                    &CancellationToken::new(),
                    Deadline::recovery(),
                )
                .unwrap();
            assert_eq!(
                result.reason,
                step["reason"].as_str().unwrap(),
                "mode={} forward={}",
                case["mode"],
                step["forward"]
            );
            let actual:Vec<_>=result.universes.iter().map(|u|serde_json::json!({"scope":u.scope,"digest":u.digest,"expiration":u.expiration.to_julian_day()-1721426,"underlying":u.underlying,"contracts":u.contracts.iter().map(|i|definitions[*i].contract).collect::<Vec<_>>()})).collect();
            assert_eq!(
                serde_json::Value::Array(actual),
                step["plans"],
                "mode={} forward={}",
                case["mode"],
                step["forward"]
            );
            steps += 1;
        }
    }
    assert_eq!(steps, 24);
}
#[test]
fn native_coverage_work_is_bounded_and_cancellable() {
    let cases = fixtures();
    let bytes = STANDARD
        .decode(cases[0]["parameters"].as_str().unwrap())
        .unwrap();
    let params = Parameters::new(rmpv::decode::read_value(&mut bytes.as_slice()).unwrap()).unwrap();
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let stop = CancellationToken::new();
    stop.cancel();
    let row = Definition {
        contract: "C",
        root: "ES",
        underlying: "F",
        strike: decimal::parse("6000").unwrap(),
        expiration: Some(1794000000000000000),
    };
    assert!(
        Planner::default()
            .plan(
                &params,
                date,
                &[row],
                true,
                decimal::parse("6000").unwrap(),
                0.2,
                Zone::new("America/New_York").unwrap(),
                None,
                &stop,
                Deadline::recovery()
            )
            .is_err()
    );
}
