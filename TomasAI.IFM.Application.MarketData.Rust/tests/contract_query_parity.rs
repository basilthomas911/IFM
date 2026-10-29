//! Live contract detail date precedence differs from the stored JSON summary.
use ifm_market_data_native::catalog::Definition;
#[test]
fn query_maturity_matches_current_clr_converter_including_conflicts_and_year_zero() {
    let cases: Vec<serde_json::Value> =
        serde_json::from_str(include_str!("../baseline/ContractQueryOracle.json")).unwrap();
    for (index, case) in cases.iter().enumerate() {
        let definition = Definition::parse("GLBX.MDP3", case["json"].as_str().unwrap()).unwrap();
        assert_eq!(
            definition.query_maturity_invalid,
            case["invalid"].as_bool().unwrap(),
            "case {index}"
        );
        if !definition.query_maturity_invalid {
            assert_eq!(
                serde_json::json!(definition.query_maturity.map(|d| d.to_string())),
                case["maturity"],
                "case {index}"
            );
        }
    }
}
