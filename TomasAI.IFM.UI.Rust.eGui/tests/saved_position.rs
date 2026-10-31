use ifm_ui_egui::services::saved_position::enrich as typed_enrich;
fn enrich(saved: &serde_json::Value, current: &serde_json::Value) -> Option<serde_json::Value> {
    let saved = serde_json::from_value(saved.clone()).unwrap();
    let current = serde_json::from_value(current.clone()).unwrap();
    typed_enrich(&saved, &current).map(|row| serde_json::to_value(row).unwrap())
}
use serde_json::json;
#[test]
fn saved_row_keeps_its_accounting_when_older_plan_only_enriches_matching_prices() {
    let saved = json!({"Id":{"PositionId":"position"},"ValueDate":"2026-10-26","RouteGeneration":2,"PositionSequence":10,"Phase":"EndOfDay","MarketValue":"-750","Legs":[{"TradeLegId":"leg","ContractId":"ES-P","SignedQuantity":-1,"CurrentPrice":"15.0","OpeningPrice":"14","OptionRisk":null}]});
    let mut current = saved.clone();
    current["PositionSequence"] = 9.into();
    current["Phase"] = "MarkToMarket".into();
    current["MarketValue"] = "999".into();
    current["Legs"][0]["CurrentPrice"] = "15.00".into();
    current["Legs"][0]["OptionRisk"] =
        json!({"ContractId":"ES-P","PricingInputs":{"Multiplier":"50"},"Delta":-0.25});
    let result = enrich(&saved, &current).unwrap();
    assert_eq!(result["phase"], "EndOfDay");
    assert_eq!(result["positionSequence"], 10);
    assert_eq!(result["marketValue"], "-750");
    assert_eq!(result["legs"][0]["optionRisk"]["delta"], -0.25);
    assert_eq!(result["legs"][0]["cashMultiplier"], "50");
    for key in ["Id", "ValueDate", "RouteGeneration"] {
        let mut wrong = current.clone();
        wrong[key] = match key {
            "Id" => json!({"PositionId":"different"}),
            "RouteGeneration" => json!(3),
            _ => json!("2026-10-27"),
        };
        assert!(enrich(&saved, &wrong).is_none());
    }
    current["Legs"][0]["CurrentPrice"] = "15.25".into();
    assert!(enrich(&saved, &current).is_none());
}

#[test]
fn newer_plan_cannot_replace_saved_currency_pnl_or_sealed_accounting() {
    let saved = json!({"Id":{"PositionId":"position"},"ValueDate":"2026-10-16","RouteGeneration":2,"PositionSequence":10,"Phase":"EndOfDay","DailyPnl":"27.40","DailyPnlPoints":"0.60","ContractCashMultiplier":"50","Legs":[{"TradeLegId":"leg","ContractId":"ES-P","SignedQuantity":-1,"CurrentPrice":"15.0","OpeningPrice":"14","CashMultiplier":"50"}]});
    for sequence in [10, 11] {
        let mut plan = saved.clone();
        plan["PositionSequence"] = sequence.into();
        plan["Phase"] = "MarkToMarket".into();
        plan["DailyPnl"] = "0.60".into();
        plan["ContractCashMultiplier"] = "1".into();
        let result = enrich(&saved, &plan).unwrap();
        assert_eq!(result["dailyPnl"], "27.40");
        assert_eq!(result["phase"], "EndOfDay");
        assert_eq!(result["positionSequence"], 10);
        assert_eq!(result["contractCashMultiplier"], "50");
        assert_eq!(result["legs"][0]["cashMultiplier"], "50");
    }
}
