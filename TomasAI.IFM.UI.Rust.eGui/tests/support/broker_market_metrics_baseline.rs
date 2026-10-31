//! Market Selection captions format backend metrics. Changes, volatility,
//! standard deviation and DTE are never recalculated from local observations.
use serde_json::{Value as Json,json};
use ifm_ui_egui::services::{outlook::decimal_fixed,option_selection::compare};
use ifm_ui_egui::backend::contracts::ExactDecimal;
fn raw(value:&Json)->Result<&str,String>{value.as_str().ok_or("Missing exact market metric".into())}
fn fixed(value:&Json,places:u32)->Result<String,String>{if value.is_null(){Ok("N/A".into())}else{decimal_fixed(raw(value)?,places,false)}}
fn signed(value:&Json,percent:bool)->Result<String,String>{
 let text=raw(value)?;let mut formatted=decimal_fixed(text,2,percent)?;
 if compare(&ExactDecimal::parse(text)?,&ExactDecimal::parse("0")?).is_gt(){formatted.insert(0,'+');}Ok(formatted)
}
pub fn document(chain:&Json,underlying:&Json,tick:&Json,live:bool)->Result<Json,String>{
 let latest=chain["Contracts"].as_array().ok_or("Missing market metric contracts")?.iter().filter_map(|r|r["QuoteAtUtc"].as_str())
  .map(chrono::DateTime::parse_from_rfc3339).collect::<Result<Vec<_>,_>>().map_err(|_|"Invalid market quote timestamp")?.into_iter().max();
 let age=if let Some(latest)=latest{
  let at=chrono::DateTime::parse_from_rfc3339(chain["AsOfUtc"].as_str().ok_or("Missing API market as-of")?).map_err(|_|"Invalid API market as-of")?;
  let delta=at-latest;let milliseconds=delta.num_nanoseconds().map(|n|n as f64/1_000_000.0).unwrap_or(delta.num_milliseconds() as f64);
  format!("{:.0} ms",milliseconds.max(0.0))
 }else{if live{"N/A"}else{"Frozen preview"}.into()};
 let change=if chain["DailyPriceChange"].is_null()||chain["DailyPriceChangeFraction"].is_null(){"N/A".into()}else{format!("{} ({})",signed(&chain["DailyPriceChange"],false)?,signed(&chain["DailyPriceChangeFraction"],true)?)};
 // This one caption explicitly uses InvariantCulture.P1 in the hybrid; its
 // percent pattern includes a space, unlike the user's current P2 pattern.
 let iv=if chain["AtmImpliedVolatility"].is_null(){"N/A".into()}else{decimal_fixed(raw(&chain["AtmImpliedVolatility"])?,1,true)?.replace('%'," %")};
 let rank=if !chain["IvRank"].is_null(){fixed(&chain["IvRank"],1)?}else if chain["IvRankStatus"].as_str().unwrap_or_default().starts_with("InsufficientHistory"){"History needed".into()}else{"N/A".into()};
 Ok(json!({"DTE":chain["DaysToExpiry"].as_i64().map(|v|v.to_string()).unwrap_or("N/A".into()),"Underlying":underlying["ContractId"],"Multiplier":underlying["Multiplier"],"Tick":tick.as_str().unwrap_or("N/A"),"Settlement":"Unavailable","Daily Std Dev":fixed(&chain["BollingerStandardDeviation20"],2)?,"Last":fixed(&chain["LastUnderlyingPrice"],2)?,"Daily Price Change":change,"IV":iv,"IV Rank":rank,"Quote Age":age}))
}
