use ifm_ui_egui::{backend::contracts::ExactDecimal, services::option_draft::*};
use serde_json::{Value as Json, json};


/// Automatic mid pricing survives form recalculation; deliberate signed limits remain fixed.
#[test]
fn order_price_tracks_mid_until_deliberately_overridden() {
    for (mid, equivalent, edited) in [("-3.20", "-3.2", "-3.15"), ("3.20", "3.2", "3.25")] {
        let mut draft = Draft::default();
        let published = ExactDecimal::parse(mid).unwrap();
        draft.update_order_price(Some(ExactDecimal::parse(equivalent).unwrap()), Some(&published));
        assert!(draft.entered_price.is_none(), "Recalculating must not pin the midpoint");
        draft.update_order_price(Some(ExactDecimal::parse(edited).unwrap()), Some(&published));
        assert_eq!(draft.entered_price.as_ref().unwrap().as_str(), edited);
        let published = ExactDecimal::parse(edited).unwrap();
        draft.update_order_price(Some(published.clone()), Some(&published));
        assert_eq!(draft.entered_price.as_ref().unwrap().as_str(), edited);
        draft.update_order_price(None, Some(&published));
        assert!(draft.entered_price.is_none(), "Clearing restores automatic pricing");
    }
}


/// Provides chain for the option draft implementation.
fn chain() -> Json {
    let row = |id: &str, strike: &str, call: bool, delta: f64| json!({"ContractId":id,"Strike":strike,"IsCall":call,"Delta":delta,"Vega":0.3,"IsStale":false,"GreeksValid":true,"SelectionValid":false,"Bid":"1.25","Ask":"1.5","QuoteAtUtc":"2026-10-26T12:30:00Z"});
    json!({"UnderlyingContractId":"ESZ6","ExpiryDate":"2026-10-28","UnderlyingPrice":"6000","WindowMethod":"Live","Contracts":[row("C-HIGH","6100",true,0.10),row("C-LOW","6050",true,0.16),row("P-HIGH","5950",false,-0.16),row("P-LOW","5900",false,-0.10)]})
}


/// Verifies that all manual roles match unchanged managed normalizer.
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
        d.accept(serde_json::from_value(chain()).unwrap(), &settings)
            .unwrap();
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


/// Verifies that automatic selection keeps price for same identity and preserves raw authority.
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
    d.accept(serde_json::from_value(chain()).unwrap(), &settings)
        .unwrap();
    assert_eq!(d.legs.len(), 4);
    assert_eq!(
        d.legs.iter().map(|l| l.role.as_str()).collect::<Vec<_>>(),
        vec!["-SC", "+LC", "-SP", "+LP"]
    );
    d.entered_price = Some(ExactDecimal::parse("-3.25").unwrap());
    let mut update = chain();
    update["Contracts"][0]["Bid"] = json!("0.00");
    d.accept(serde_json::from_value(update).unwrap(), &settings)
        .unwrap();
    assert_eq!(d.entered_price.as_ref().unwrap().as_str(), "-3.25");
    // Raw missing contracts are not restored from the display cache or selected
    // simply because a previously displayed quote remains visible.
    let mut missing = chain();
    missing["Contracts"]
        .as_array_mut()
        .unwrap()
        .retain(|r| r["ContractId"] != "C-HIGH");
    d.accept(serde_json::from_value(missing).unwrap(), &settings)
        .unwrap();
    assert_eq!(d.legs.len(), 2);
    assert!(d.entered_price.is_none());
    assert_eq!(d.raw.as_ref().unwrap().contracts.as_ref().unwrap().len(), 3);
    assert_eq!(
        d.display
            .as_ref()
            .unwrap()
            .contracts
            .as_ref()
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


/// Verifies that emulator and live select the same four condor legs from delta and width.
#[test]
fn emulator_and_live_select_the_same_four_condor_legs_from_delta_and_width() {
    use ifm_ui_egui::services::composer_market::Defaults;
    for (strategy, delta_text, width_text, expected) in [
        (
            "ShortIronCondor",
            "16",
            "50",
            ["C-6100", "C-6150", "P-5900", "P-5850"],
        ),
        (
            "LongIronCondor",
            "16",
            "50",
            ["C-6100", "C-6050", "P-5900", "P-5950"],
        ),
        (
            "ShortIronCondor",
            "16",
            "100",
            ["C-6100", "C-6200", "P-5900", "P-5800"],
        ),
        (
            "ShortIronCondor",
            "10",
            "50",
            ["C-6150", "C-6200", "P-5850", "P-5800"],
        ),
        (
            "LongIronCondor",
            "10",
            "50",
            ["C-6150", "C-6100", "P-5850", "P-5900"],
        ),
    ] {
        for live in [false, true] {
            let mut defaults = Defaults::emulator().unwrap();
            if delta_text != "16" {
                defaults.call_delta = ExactDecimal::parse(delta_text).unwrap();
                defaults.put_delta = ExactDecimal::parse(delta_text).unwrap();
            }
            if width_text != "50" {
                defaults.call_width = ExactDecimal::parse(width_text).unwrap();
                defaults.put_width = ExactDecimal::parse(width_text).unwrap();
            }
            let multiplier = ExactDecimal::parse("50").unwrap();
            let settings = Settings {
                strategy,
                call_delta: &defaults.call_delta,
                put_delta: &defaults.put_delta,
                call_width: &defaults.call_width,
                put_width: &defaults.put_width,
                multiplier: &multiplier,
                live,
                development: true,
                editable: true,
            };
            let row = |id: &str, strike: &str, call: bool, delta: f64| {
                json!({
                "ContractId":id,"Strike":strike,"IsCall":call,"Delta":delta,
                "Vega":0.3,"IsStale":!live,"GreeksValid":true,"SelectionValid":live,
                "Bid":"1.25","Ask":"1.5","QuoteAtUtc":"2026-10-26T12:30:00Z"})
            };
            let mut source = chain();
            source["WindowMethod"] = json!(if live {
                "Live"
            } else {
                "Frozen emulator preview"
            });
            source["Contracts"] = json!([
                row("C-6050", "6050", true, 0.25),
                row("C-6100", "6100", true, 0.16),
                row("C-6150", "6150", true, 0.10),
                row("C-6200", "6200", true, 0.05),
                row("P-5950", "5950", false, -0.25),
                row("P-5900", "5900", false, -0.16),
                row("P-5850", "5850", false, -0.10),
                row("P-5800", "5800", false, -0.05)
            ]);
            let mut draft = Draft::default();
            draft
                .accept(serde_json::from_value(source).unwrap(), &settings)
                .unwrap();
            let actual: Vec<_> = draft
                .legs
                .iter()
                .map(|leg| leg.quote.contract.as_str())
                .collect();
            assert_eq!(
                actual, expected,
                "{strategy}, delta {delta_text}, width {width_text}, live {live}"
            );
        }
    }
}
