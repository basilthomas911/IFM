//! Attach backend risk observations to a saved row without changing its price,
//! lifecycle sequence or accounting values. This follows the hybrid's saved
//! position enrichment rule; it does not calculate Greeks or P&L.
use serde_json::Value as Json;
fn decimal(value: &Json) -> Option<crate::backend::contracts::ExactDecimal> {
    crate::backend::contracts::ExactDecimal::parse(value.as_str()?).ok()
}
fn equal_amount(a: &Json, b: &Json) -> bool {
    decimal(a)
        .zip(decimal(b))
        .is_some_and(|(a, b)| super::option_selection::compare(&a, &b).is_eq())
}
pub fn enrich(saved: &Json, current: &Json) -> Option<Json> {
    if ["Open", "Close"].contains(&saved["Phase"].as_str().unwrap_or_default())
        || saved["Id"] != current["Id"]
        || saved["ValueDate"] != current["ValueDate"]
        || saved["RouteGeneration"] != current["RouteGeneration"]
    {
        return None;
    }
    if current["PositionSequence"].as_i64()? >= saved["PositionSequence"].as_i64()? {
        return Some(current.clone());
    }
    let before = saved["Legs"].as_array()?;
    let latest = current["Legs"].as_array()?;
    if before.len() != latest.len() {
        return None;
    }
    let mut legs = vec![];
    for leg in before {
        let matching = latest
            .iter()
            .filter(|other| {
                other["TradeLegId"] == leg["TradeLegId"]
                    && other["ContractId"] == leg["ContractId"]
                    && other["SignedQuantity"] == leg["SignedQuantity"]
                    && equal_amount(&other["CurrentPrice"], &leg["CurrentPrice"])
                    && equal_amount(&other["OpeningPrice"], &leg["OpeningPrice"])
            })
            .collect::<Vec<_>>();
        if matching.len() != 1 {
            return None;
        }
        let source = matching[0];
        let mut next = leg.clone();
        for key in ["OptionTick", "OptionRisk", "Scenario"] {
            next[key] = source[key].clone();
        }
        let tick = if source["OptionTick"]["PricingInputs"].is_null() {
            &source["OptionRisk"]
        } else {
            &source["OptionTick"]
        };
        next["CashMultiplier"] = if tick["ContractId"] == source["ContractId"]
            && decimal(&tick["PricingInputs"]["Multiplier"]).is_some_and(|v| {
                super::option_selection::compare(
                    &v,
                    &crate::backend::contracts::ExactDecimal::parse("0").unwrap(),
                )
                .is_gt()
            }) {
            tick["PricingInputs"]["Multiplier"].clone()
        } else {
            Json::Null
        };
        legs.push(next);
    }
    let mut result = saved.clone();
    result["Legs"] = legs.into();
    Some(result)
}
