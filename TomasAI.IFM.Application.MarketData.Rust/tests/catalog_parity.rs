//! Exact provider definition projection compared with the unchanged C# parser.
use ifm_market_data_native::catalog::{Catalog, Definition, Failure};
use serde_json::Value;
#[test]
fn definition_projection_matches_csharp_valid_and_invalid_corpus() {
    let cases: Vec<Value> =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    for (index, c) in cases.iter().enumerate() {
        let json = c["json"].as_str().unwrap();
        let parsed = Definition::parse("GLBX.MDP3", json);
        assert_eq!(
            parsed.is_ok(),
            c["valid"].as_bool().unwrap(),
            "case {index}: {json}"
        );
        if let Ok(d) = parsed {
            let expected = serde_json::json!({"publisher":d.publisher,"instrument":d.instrument,"symbol":d.symbol,"asset":d.asset,"className":d.class,"currency":d.currency,"exchange":d.exchange,"settlement":d.settlement_currency,"security":d.security_type,"cfi":d.cfi,"unit":d.unit,"underlyingSymbol":d.underlying,"kind":d.kind,
              "received":d.received_ns,"at":d.event_ns,"deleted":d.deleted,"underlying":d.underlying_instrument,
              "activation":d.activation,"expiration":d.expiration,"maturity":d.maturity.map(|v|v.to_string()),
              "strike":d.strike,"tick":d.tick,"tickAmount":d.tick_amount,"multiplier":d.multiplier,"week":d.maturity_week});
            for (name, value) in expected.as_object().unwrap() {
                assert_eq!(value, &c[name], "case {index} {name}");
            }
            assert_eq!(d.json.as_ref(), json);
        }
    }
}
#[test]
fn catalog_capacity_is_atomic_and_dataset_scoped() {
    let cases: Vec<Value> =
        serde_json::from_str(include_str!("../baseline/DefinitionOracle.json")).unwrap();
    let json = cases[0]["json"].as_str().unwrap();
    let d = Definition::parse("GLBX.MDP3", json).unwrap();
    let mut catalog = Catalog::new(1, json.len()).unwrap();
    catalog.insert(d.clone()).unwrap();
    let old = catalog.find("GLBX.MDP3", 42).unwrap();
    catalog.insert(d).unwrap();
    assert_eq!(catalog.source_bytes(), json.len());
    let other = Definition::parse("XCBF.PITCH", json).unwrap();
    assert_eq!(catalog.insert(other), Err(Failure::Capacity));
    assert!(catalog.find("XCBF.PITCH", 42).is_none());
    assert_eq!(catalog.find("GLBX.MDP3", 42).unwrap(), old);
    catalog.clear();
    assert_eq!(catalog.source_bytes(), 0);
    assert!(catalog.find("GLBX.MDP3", 42).is_none());
    assert_eq!(old.json.as_ref(), json);
}
