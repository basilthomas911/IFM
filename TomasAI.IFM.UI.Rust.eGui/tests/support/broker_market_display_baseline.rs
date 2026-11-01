//! Frozen original market publication projection.
use serde_json::{Value as Json, json};


/// Provides stamp for the broker market display baseline implementation.
fn stamp(value: &Json) -> Result<Json, String> {
    if value.is_null() {
        return Ok(Json::Null);
    }
    let at =
        chrono::DateTime::parse_from_rfc3339(value.as_str().ok_or("Missing display timestamp")?)
            .map_err(|_| "Invalid display timestamp")?;
    Ok(json!(format!(
        "{}.{:07}{}",
        at.format("%Y-%m-%dT%H:%M:%S"),
        at.timestamp_subsec_nanos() / 100,
        at.format("%:z")
    )))
}


/// Provides mapped for the broker market display baseline implementation.
fn mapped(row: &Json, names: &[&str], timestamps: &[&str]) -> Result<Json, String> {
    let mut result = serde_json::Map::new();
    for name in names {
        let mut key = (*name).to_string();
        key[..1].make_ascii_lowercase();
        result.insert(
            key,
            if timestamps.contains(name) {
                stamp(&row[*name])?
            } else {
                row[*name].clone()
            },
        );
    }
    Ok(Json::Object(result))
}


/// Provides market for the broker market display baseline implementation.
pub fn market(row: &Json) -> Result<Json, String> {
    let mut result = mapped(
        row,
        &[
            "UnderlyingContractId",
            "ExpiryDate",
            "UnderlyingPrice",
            "LowerStrikeBound",
            "UpperStrikeBound",
            "WindowMethod",
            "AsOfUtc",
            "BollingerStandardDeviation20",
            "PreviousSessionClose",
            "IvRank",
            "IvRankStatus",
            "BollingerAsOfUtc",
            "LastUnderlyingPrice",
            "LastUnderlyingTradeAtUtc",
        ],
        &["AsOfUtc", "BollingerAsOfUtc", "LastUnderlyingTradeAtUtc"],
    )?;
    let contracts = row["Contracts"]
        .as_array()
        .ok_or("Missing market display rows")?
        .iter()
        .map(|row| {
            mapped(
                row,
                &[
                    "ContractId",
                    "Strike",
                    "IsCall",
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
                    "GreeksValid",
                    "IsStale",
                    "QuoteAtUtc",
                    "TradeAtUtc",
                    "EvaluatedAtUtc",
                    "VolumeValueDate",
                    "OpenInterestValueDate",
                    "SelectionValid",
                    "QuoteUnavailableReason",
                    "LastQuoteEventAtUtc",
                    "LastQuoteReceivedAtUtc",
                ],
                &[
                    "QuoteAtUtc",
                    "TradeAtUtc",
                    "EvaluatedAtUtc",
                    "LastQuoteEventAtUtc",
                    "LastQuoteReceivedAtUtc",
                ],
            )
        })
        .collect::<Result<Vec<_>, _>>()?;
    result["contracts"] = Json::Array(contracts);
    Ok(result)
}
