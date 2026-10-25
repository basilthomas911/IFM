use ifm_ui_egui::{backend::contracts::ExactDecimal, services::option_draft::*};
use serde_json::{Value as Json, json};
fn chain() -> Json {
    let row = |id: &str, strike: &str, call: bool, delta: f64| json!({"ContractId":id,"Strike":strike,"IsCall":call,"Delta":delta,"Vega":0.3,"IsStale":false,"GreeksValid":true,"SelectionValid":false,"Bid":"1.25","Ask":"1.5","QuoteAtUtc":"2026-10-26T12:30:00Z"});
    json!({"UnderlyingContractId":"ESZ6","ExpiryDate":"2026-10-28","UnderlyingPrice":"6000","WindowMethod":"Live","Contracts":[row("C-HIGH","6100",true,0.10),row("C-LOW","6050",true,0.16),row("P-HIGH","5950",false,-0.16),row("P-LOW","5900",false,-0.10)]})
}
#[test]
fn all_manual_roles_match_unchanged_managed_normalizer() {
    let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
        .join("tests/fixtures/backend/option-draft-roles.json");
    let cases: Json = serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
    let delta = ExactDecimal::parse("16").unwrap();
    let width = ExactDecimal::parse("50").unwrap();
    let multiplier = ExactDecimal::parse("50").unwrap();
    for case in cases.as_array().unwrap() {
        let settings = Settings {
            strategy: case["strategy"].as_str().unwrap(),
            call_delta: &delta,
            put_delta: &delta,
            call_width: &width,
            put_width: &width,
            multiplier: &multiplier,
            live: true,
            development: false,
            editable: true,
        };
        let mut d = Draft::default();
        d.manual = true;
        d.accept(chain(), &settings).unwrap();
        for leg in case["input"].as_array().unwrap() {
            d.select(
                leg["id"].as_str().unwrap(),
                leg["role"].as_str().unwrap(),
                &settings,
            )
            .unwrap();
        }
        let actual: Vec<_> = d
            .legs
            .iter()
            .map(|l| json!({"id":l.quote.contract,"role":l.role,"sign":l.quote.side}))
            .collect();
        assert_eq!(json!(actual), case["expected"], "{}", case["strategy"]);
        let before = d.legs.len();
        assert!(d.select("C-HIGH", "+LC", &settings).is_err());
        assert_eq!(d.legs.len(), before);
    }
}
#[test]
fn automatic_selection_keeps_price_for_same_identity_and_preserves_raw_authority() {
    let delta = ExactDecimal::parse("16").unwrap();
    let width = ExactDecimal::parse("50").unwrap();
    let multiplier = ExactDecimal::parse("50").unwrap();
    let settings = Settings {
        strategy: "ShortIronCondor",
        call_delta: &delta,
        put_delta: &delta,
        call_width: &width,
        put_width: &width,
        multiplier: &multiplier,
        live: true,
        development: false,
        editable: true,
    };
    let mut d = Draft::default();
    d.accept(chain(), &settings).unwrap();
    assert_eq!(d.legs.len(), 4);
    assert_eq!(
        d.legs.iter().map(|l| l.role.as_str()).collect::<Vec<_>>(),
        vec!["-SC", "+LC", "-SP", "+LP"]
    );
    d.entered_price = Some(ExactDecimal::parse("-3.25").unwrap());
    let mut update = chain();
    update["Contracts"][0]["Bid"] = json!("0.00");
    d.accept(update, &settings).unwrap();
    assert_eq!(d.entered_price.as_ref().unwrap().as_str(), "-3.25");
    // Raw missing contracts are not restored from the display cache or selected
    // simply because a previously displayed quote remains visible.
    let mut missing = chain();
    missing["Contracts"]
        .as_array_mut()
        .unwrap()
        .retain(|r| r["ContractId"] != "C-HIGH");
    d.accept(missing, &settings).unwrap();
    assert_eq!(d.legs.len(), 2);
    assert!(d.entered_price.is_none());
    assert_eq!(
        d.raw.as_ref().unwrap()["Contracts"]
            .as_array()
            .unwrap()
            .len(),
        3
    );
    assert_eq!(
        d.display.as_ref().unwrap()["Contracts"]
            .as_array()
            .unwrap()
            .len(),
        4
    );
    d.clear();
    assert!(d.legs.is_empty());
    assert!(d.manual);
    assert!(d.select("P-HIGH", "+LC", &settings).is_err());
    assert!(d.select("MISSING", "-SP", &settings).is_err());
}
