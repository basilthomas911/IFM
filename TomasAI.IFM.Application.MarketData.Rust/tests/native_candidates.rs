//! Exact candidate eligibility/shortlist/wing parity with the existing C# policy.
use ifm_market_data_native::{
    option_chain::{
        candidates::{Candidate, Policy, Range, select},
        qualification::Quote,
    },
    reference::decimal,
    runtime::Deadline,
};
use tokio_util::sync::CancellationToken;
fn number(value: &str) -> ifm_market_data_native::option_chain::qualification::Decimal {
    decimal::parse(value).unwrap()
}
fn policy(maximum: usize) -> Policy {
    let range = Range {
        minimum: number("0.1"),
        target: number("0.2"),
        maximum: number("0.3"),
    };
    Policy {
        put: range,
        call: range,
        put_widths: vec![number("5"), number("10")],
        call_widths: vec![number("5"), number("10")],
        age: 1000,
        skew: 250,
        size: 1,
        spread: number("0.25"),
        maximum,
    }
}
fn quote(value: &serde_json::Value) -> Option<Quote> {
    if value.is_null() {
        None
    } else {
        Some(Quote {
            bid: number(value["bid"].as_str().unwrap()),
            ask: number(value["ask"].as_str().unwrap()),
            bid_size: number(value["bidSize"].as_str().unwrap()),
            ask_size: number(value["askSize"].as_str().unwrap()),
            event: value["at"].as_i64().unwrap(),
            received: value["received"].as_i64().unwrap(),
            ..Default::default()
        })
    }
}
#[test]
fn existing_csharp_quality_boundaries_shortlists_and_exact_wings_match() {
    let cases: serde_json::Value = serde_json::from_str(include_str!(
        "../qualification/ContextWireBridge/candidates.json"
    ))
    .unwrap();
    assert_eq!(cases.as_array().unwrap().len(), 69);
    for case in cases.as_array().unwrap() {
        let rows: Vec<_> = case["rows"]
            .as_array()
            .unwrap()
            .iter()
            .map(|row| Candidate {
                id: row["id"].as_str().unwrap(),
                underlying: row["underlying"].as_str().unwrap(),
                expiration: row["expiration"].as_i64().unwrap(),
                strike: number(row["strike"].as_str().unwrap()),
                call: row["call"].as_bool().unwrap(),
                delta: row["delta"].as_f64(),
                until: row["until"].as_i64(),
                quote: quote(&row["quote"]),
                future: quote(&row["future"]),
            })
            .collect();
        let indices = select(
            &rows,
            &policy(case["maximum"].as_u64().unwrap() as usize),
            case["at"].as_i64().unwrap(),
            &CancellationToken::new(),
            Deadline::recovery(),
        )
        .unwrap();
        let ids: Vec<_> = indices.into_iter().map(|i| rows[i].id).collect();
        let expected: Vec<_> = case["selected"]
            .as_array()
            .unwrap()
            .iter()
            .map(|v| v.as_str().unwrap())
            .collect();
        assert_eq!(
            ids, expected,
            "mode={} maximum={}",
            case["mode"], case["maximum"]
        );
    }
}
#[test]
fn invalid_policy_capacity_and_cancellation_are_explicit() {
    let stop = CancellationToken::new();
    let mut rules = policy(0);
    assert!(select(&[], &rules, 0, &stop, Deadline::recovery()).is_err());
    rules = policy(1);
    rules.call_widths.push(number("5.0"));
    assert!(rules.validate().is_err());
    rules = policy(1);
    assert!(
        select(&[], &rules, 0, &stop, Deadline::recovery())
            .unwrap()
            .is_empty()
    );
    let make = || Candidate {
        id: "C",
        underlying: "F",
        expiration: 1,
        strike: number("6000"),
        call: true,
        delta: Some(0.2),
        until: Some(100),
        quote: None,
        future: None,
    };
    let rows: Vec<_> = (0..2049).map(|_| make()).collect();
    assert!(select(&rows, &rules, 0, &stop, Deadline::recovery()).is_err());
    stop.cancel();
    assert!(select(&[make()], &rules, 0, &stop, Deadline::recovery()).is_err());
    rules.put.minimum = number("-0.1");
    assert!(rules.validate().is_err());
}
