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


/// Saved closing legs may use frozen quotes only in the explicit Development emulator chain.
#[test]
fn frozen_closing_legs_retain_prices_without_weakening_live_freshness() {
    let delta = ExactDecimal::parse("16").unwrap();
    let width = ExactDecimal::parse("50").unwrap();
    for (live, development, method, eligible) in [
        (false, true, "Frozen emulator preview (2026-11-06; historical/model quotes)", true),
        (true, true, "Frozen emulator preview", false),
        (false, false, "Frozen emulator preview", false),
        (false, true, "Live", false),
    ] {
        let mut settings = Settings {
            strategy: "ShortIronCondor", call_delta: &delta, put_delta: &delta,
            call_width: &width, put_width: &width, multiplier: &width,
            live: true, development: true, editable: true,
        };
        let mut draft = Draft::default();
        draft.accept(serde_json::from_value(chain()).unwrap(), &settings).unwrap();
        assert_eq!(draft.legs.len(), 4);
        draft.manual = true;
        // Reverse the saved opening position, as the close preview does.
        for leg in &mut draft.legs {
            leg.quote.side = -leg.quote.side;
            leg.role = match leg.role.as_str() {
                "-SC" => "+LC", "+LC" => "-SC", "-SP" => "+LP", "+LP" => "-SP",
                _ => unreachable!(),
            }.into();
        }
        let identities = draft.legs.iter().map(|leg| (leg.quote.contract.clone(), leg.quote.side))
            .collect::<Vec<_>>();
        settings.live = live;
        settings.development = development;
        let mut frozen = chain();
        frozen["WindowMethod"] = json!(method);
        for row in frozen["Contracts"].as_array_mut().unwrap() {
            row["IsStale"] = json!(true);
        }
        draft.accept(serde_json::from_value(frozen).unwrap(), &settings).unwrap();
        assert_eq!(identities, draft.legs.iter()
            .map(|leg| (leg.quote.contract.clone(), leg.quote.side)).collect::<Vec<_>>());
        for leg in &draft.legs {
            assert_eq!(leg.quote.bid.is_some(), eligible, "{live}/{development}/{method}");
            assert_eq!(leg.quote.ask.is_some(), eligible);
            assert_eq!(leg.frozen, eligible);
        }
    }
}

#[test]
fn saved_legs_keep_display_quotes_without_reusing_them_for_order_price() {
    use ifm_ui_egui::services::broker_market_display::{indicative_net_mid, last_known_leg, quote_age};
    let delta = ExactDecimal::parse("16").unwrap();
    let width = ExactDecimal::parse("50").unwrap();
    let settings = Settings {
        strategy: "ShortIronCondor",
        call_delta: &delta,
        put_delta: &delta,
        call_width: &width,
        put_width: &width,
        multiplier: &width,
        live: true,
        development: false,
        editable: true,
    };
    let mut draft = Draft::default();
    let mut first = chain();
    first["Contracts"][1]["Bid"] = json!("2.25");
    first["Contracts"][1]["Ask"] = json!("2.50");
    draft.accept(serde_json::from_value(first).unwrap(), &settings).unwrap();
    draft.manual = true;
    let mut next = chain();
    next["Contracts"][1]["IsStale"] = json!(true);
    next["Contracts"][1]["Bid"] = Json::Null;
    next["Contracts"][1]["Ask"] = Json::Null;
    next["Contracts"][1]["QuoteAtUtc"] = Json::Null;
    next["Contracts"].as_array_mut().unwrap().remove(0);
    draft.accept(serde_json::from_value(next).unwrap(), &settings).unwrap();
    assert_eq!(draft.legs.len(), 4);
    assert!(draft.legs.iter().filter(|leg| leg.quote.is_call)
        .all(|leg| leg.quote.bid.is_none() && leg.quote.ask.is_none()));
    let rows = draft.display.as_ref().unwrap().contracts.as_ref().unwrap();
    let legs = draft.legs.iter().map(|leg| last_known_leg(
        leg,
        rows.iter().find(|row| row.contract_id.as_deref() == Some(&leg.quote.contract)),
        None,
    ).unwrap()).collect::<Vec<_>>();
    assert_eq!(legs.iter().filter(|leg| leg.stale == Some(true)).count(), 2);
    assert!(legs.iter().all(|leg| leg.bid.is_some() && leg.ask.is_some()));
    assert_eq!(indicative_net_mid(&legs).as_deref(), Some("-1.00"));
    assert_eq!(quote_age("2026-10-26T12:32:00Z", legs[0].quote_at_utc.as_ref().unwrap().as_str()), "120s");
    let saved = serde_json::from_value(json!({
        "bidPrice": 3.25, "askPrice": 3.50, "observedAtUtc": "2026-10-26T12:30:10Z",
    })).unwrap();
    let reopened = last_known_leg(&draft.legs[0], None, Some(&saved)).unwrap();
    assert_eq!(reopened.bid.as_ref().unwrap().as_str(), "3.25");
    assert_eq!(reopened.ask.as_ref().unwrap().as_str(), "3.5");
    assert_eq!(reopened.stale, Some(true));
    assert!(draft.legs[0].quote.bid.is_none());
    draft.accept(serde_json::from_value(chain()).unwrap(), &settings).unwrap();
    assert!(draft.legs.iter().all(|leg| leg.quote.bid.is_some() && leg.quote.ask.is_some()));
    draft.expire_live_quotes("2026-10-26T12:30:05Z".parse().unwrap());
    assert!(draft.legs.iter().all(|leg| leg.quote.bid.is_none() && leg.quote.ask.is_none()));
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
