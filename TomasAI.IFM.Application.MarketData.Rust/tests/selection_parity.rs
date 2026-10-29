//! Current C# canonical parser and chain filtering, including duplicate ordering.
use ifm_market_data_native::catalog::{
    Definition,
    selection::{self, Chain},
};
use serde_json::Value;
use std::sync::Arc;
fn day(s: &str) -> time::Date {
    let v: Vec<i32> = s.split('-').map(|v| v.parse().unwrap()).collect();
    time::Date::from_calendar_date(v[0], time::Month::try_from(v[1] as u8).unwrap(), v[2] as u8)
        .unwrap()
}
#[test]
fn canonical_identity_and_chain_selection_match_csharp() {
    let corpus: Value =
        serde_json::from_str(include_str!("../baseline/SelectionOracle.json")).unwrap();
    let mut mismatches = 0;
    for c in corpus["identities"].as_array().unwrap() {
        let id = c["id"].as_str().unwrap();
        let parsed = selection::parse(id);
        if parsed.is_ok() != c["valid"].as_bool().unwrap() {
            eprintln!("identity mismatch {id:?}: {parsed:?}, expected {c}");
            mismatches += 1;
            continue;
        }
        if let Ok(p) = parsed {
            assert_eq!(p.ticker, c["ticker"]);
            assert_eq!(p.expiration.to_string(), c["expiration"]);
            assert_eq!(u64::from(p.kind), c["kind"].as_u64().unwrap());
            assert_eq!(p.strike, c["strike"].as_i64());
        }
    }
    assert_eq!(mismatches, 0);
    let selected = Definition::parse("GLBX.MDP3", corpus["selected"].as_str().unwrap()).unwrap();
    let definitions: Vec<_> = corpus["definitions"]
        .as_array()
        .unwrap()
        .iter()
        .map(|v| Arc::new(Definition::parse("GLBX.MDP3", v.as_str().unwrap()).unwrap()))
        .collect();
    for c in corpus["chains"].as_array().unwrap() {
        let request = Chain {
            maturity: day(c["maturity"].as_str().unwrap()),
            rights: c["rights"].as_u64().unwrap() as u8,
            policy: c["policy"].as_u64().unwrap() as u8,
            underlying: c["root"].as_str().unwrap(),
            selected_underlying: c["useSelected"].as_bool().unwrap().then_some(&selected),
        };
        let chain = selection::chain(&request, definitions.iter().cloned());
        assert_eq!(
            chain.iter().map(|d| d.instrument).collect::<Vec<_>>(),
            c["ids"]
                .as_array()
                .unwrap()
                .iter()
                .map(|v| v.as_u64().unwrap() as u32)
                .collect::<Vec<_>>(),
            "{c}"
        );
        assert_eq!(
            chain.iter().map(|d| d.symbol.as_str()).collect::<Vec<_>>(),
            c["symbols"]
                .as_array()
                .unwrap()
                .iter()
                .map(|v| v.as_str().unwrap())
                .collect::<Vec<_>>(),
            "{c}"
        );
    }
}
