fn j(v: ifm_ui_egui::models::trade_contracts::EvaluatedOptionChainReadModel) -> serde_json::Value {
    ifm_ui_egui::models::backend_document::request_document(&v).unwrap()
}
use ifm_ui_egui::{backend::contracts::ExactDecimal, services::option_selection::*};
#[test]
fn selections_match_unchanged_hybrid_decimal_and_delta_rules() {
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests/fixtures/backend/option-selection.json");
    let cases: serde_json::Value = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    for case in cases.as_array().unwrap() {
        let contracts: Vec<_> = case["contracts"]
            .as_array()
            .unwrap()
            .iter()
            .map(|c| Contract::from_backend(&serde_json::from_value(c.clone()).unwrap()).unwrap())
            .collect();
        let underlying = case["underlying"]
            .as_str()
            .map(|s| ExactDecimal::parse(s).unwrap());
        let candidates = short_candidates(
            &contracts,
            case["call"].as_bool().unwrap(),
            case["maximumDelta"].as_f64().unwrap(),
            underlying.as_ref(),
            case["frozen"].as_bool().unwrap(),
        );
        assert_eq!(
            serde_json::json!(candidates.iter().map(|c| &c.id).collect::<Vec<_>>()),
            case["candidates"],
            "{}",
            case["name"]
        );
        let selected = wing(
            &contracts,
            &ExactDecimal::parse(case["strike"].as_str().unwrap()).unwrap(),
            &ExactDecimal::parse(case["width"].as_str().unwrap()).unwrap(),
            case["up"].as_bool().unwrap(),
        );
        assert_eq!(
            serde_json::json!(selected.map(|c| &c.id)),
            case["wing"],
            "{}",
            case["name"]
        );
    }
    // Leading zeros and signed zero are valid decimal spellings, not extra digits.
    assert_eq!(
        compare(
            &ExactDecimal::parse("0000000000000000000000000000000001").unwrap(),
            &ExactDecimal::parse("1.0").unwrap()
        ),
        std::cmp::Ordering::Equal
    );
    assert_eq!(
        compare(
            &ExactDecimal::parse("-0.0").unwrap(),
            &ExactDecimal::parse("0").unwrap()
        ),
        std::cmp::Ordering::Equal
    );
}
#[test]
fn retained_display_matches_hybrid_without_changing_raw_eligibility() {
    use ifm_ui_egui::{
        backend::{codec, schema::Registry},
        services::option_chain::merge_display,
    };
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");
    let registry = Registry::compiled().unwrap();
    let read = |name: &str| {
        registry
            .project(
                "TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel",
                &codec::decode(
                    &std::fs::read(root.join(format!("option-display-{name}.msgpack"))).unwrap(),
                )
                .unwrap(),
            )
            .unwrap()
    };
    let previous = read("previous");
    let incoming = read("incoming");
    let original = incoming.clone();
    assert_eq!(
        j(merge_display(
            Some(&serde_json::from_value(previous.clone()).unwrap()),
            &serde_json::from_value(incoming.clone()).unwrap()
        )
        .unwrap()),
        read("merged")
    );
    assert_eq!(incoming, original);
    let frozen = read("frozen");
    assert_eq!(
        j(merge_display(
            Some(&serde_json::from_value(previous.clone()).unwrap()),
            &serde_json::from_value(frozen.clone()).unwrap()
        )
        .unwrap()),
        read("frozen-merged")
    );
    let merged = j(merge_display(
        Some(&serde_json::from_value(previous.clone()).unwrap()),
        &serde_json::from_value(incoming.clone()).unwrap(),
    )
    .unwrap());
    let missing = merged["Contracts"]
        .as_array()
        .unwrap()
        .iter()
        .find(|r| r["ContractId"] == "P1")
        .unwrap();
    assert_eq!(missing["IsStale"], true);
    assert_eq!(missing["SelectionValid"], false);
}
