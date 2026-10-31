//! Frozen original chain retention for parity/timing.
use serde_json::Value as Json;
pub fn merge_display(previous: Option<&Json>, incoming: &Json) -> Result<Json, String> {
    let frozen = |v: &Json| {
        v["WindowMethod"]
            .as_str()
            .unwrap_or_default()
            .starts_with("Frozen emulator preview")
    };
    let Some(previous) = previous.filter(|p| {
        p["UnderlyingContractId"] == incoming["UnderlyingContractId"]
            && p["ExpiryDate"] == incoming["ExpiryDate"]
            && frozen(p) == frozen(incoming)
    }) else {
        return Ok(incoming.clone());
    };
    let mut rows = previous["Contracts"]
        .as_array()
        .ok_or("Missing previous display contracts")?
        .clone();
    for row in &mut rows {
        row["IsStale"] = true.into();
        row["SelectionValid"] = false.into();
    }
    for next in incoming["Contracts"]
        .as_array()
        .ok_or("Missing incoming display contracts")?
    {
        let Some(index) = rows
            .iter()
            .position(|old| old["ContractId"] == next["ContractId"])
        else {
            rows.push(next.clone());
            continue;
        };
        let mut merged = next.clone();
        for field in [
            "Bid",
            "Ask",
            "BidSize",
            "AskSize",
            "Last",
            "LastSize",
            "ImpliedVolatility",
            "TheoreticalPrice",
            "Delta",
            "Gamma",
            "Vega",
            "Theta",
            "Rho",
            "Volume",
            "OpenInterest",
            "QuoteAtUtc",
            "TradeAtUtc",
            "EvaluatedAtUtc",
            "VolumeValueDate",
            "OpenInterestValueDate",
        ] {
            if merged[field].is_null() {
                merged[field] = rows[index][field].clone();
            }
        }
        rows[index] = merged;
    }
    // Validate before sorting so malformed snapshots produce an explicit error
    // instead of a comparator panic or an approximate floating-point ordering.
    let keys = rows
        .iter()
        .map(super::option_selection_baseline::Contract::from_backend)
        .collect::<Result<Vec<_>, _>>()?;
    let mut order: Vec<_> = (0..rows.len()).collect();
    order.sort_by(|a, b| {
        super::option_selection_baseline::compare(&keys[*b].strike, &keys[*a].strike)
            .then_with(|| keys[*b].is_call.cmp(&keys[*a].is_call))
    });
    let mut result = incoming.clone();
    result["Contracts"] = Json::Array(order.into_iter().map(|i| rows[i].clone()).collect());
    Ok(result)
}
