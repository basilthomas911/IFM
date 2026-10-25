//! Portable display documents for the independently copied market grids.
//! Decimal cells stay invariant strings; timestamps preserve .NET's seven
//! fractional digits and original offset. These maps are never order inputs.
use super::option_draft::Selected;
use serde_json::{Value as Json,json};
fn stamp(value:&Json)->Result<Json,String>{
 if value.is_null(){return Ok(Json::Null)}
 let at=chrono::DateTime::parse_from_rfc3339(value.as_str().ok_or("Missing display timestamp")?).map_err(|_|"Invalid display timestamp")?;
 Ok(json!(format!("{}.{:07}{}",at.format("%Y-%m-%dT%H:%M:%S"),at.timestamp_subsec_nanos()/100,at.format("%:z"))))
}
fn mapped(row:&Json,names:&[&str],timestamps:&[&str])->Result<Json,String>{
 let mut result=serde_json::Map::new();
 for name in names {
  let mut key=(*name).to_string();key[..1].make_ascii_lowercase();
  result.insert(key,if timestamps.contains(name){stamp(&row[*name])?}else{row[*name].clone()});
 }
 Ok(Json::Object(result))
}
pub fn market(row:&Json)->Result<Json,String>{
 let mut result=mapped(row,&["UnderlyingContractId","ExpiryDate","UnderlyingPrice","LowerStrikeBound","UpperStrikeBound","WindowMethod","AsOfUtc","BollingerStandardDeviation20","PreviousSessionClose","IvRank","IvRankStatus","BollingerAsOfUtc","LastUnderlyingPrice","LastUnderlyingTradeAtUtc"],&["AsOfUtc","BollingerAsOfUtc","LastUnderlyingTradeAtUtc"])?;
 let contracts=row["Contracts"].as_array().ok_or("Missing market display rows")?.iter().map(|row|mapped(row,&[
  "ContractId","Strike","IsCall","Bid","Ask","BidSize","AskSize","Last","LastSize","ImpliedVolatility","TheoreticalPrice","Delta","Gamma","Vega","Theta","Rho","Volume","OpenInterest","GreeksValid","IsStale","QuoteAtUtc","TradeAtUtc","EvaluatedAtUtc","VolumeValueDate","OpenInterestValueDate","SelectionValid","QuoteUnavailableReason","LastQuoteEventAtUtc","LastQuoteReceivedAtUtc"],&["QuoteAtUtc","TradeAtUtc","EvaluatedAtUtc","LastQuoteEventAtUtc","LastQuoteReceivedAtUtc"])).collect::<Result<Vec<_>,_>>()?;
 result["contracts"]=Json::Array(contracts);Ok(result)
}
pub fn leg(value:&Selected)->Result<Json,String>{
 let decimal=|value:&Option<crate::backend::contracts::ExactDecimal>|value.as_ref().map(|v|json!(v.as_str())).unwrap_or(Json::Null);
 Ok(json!({"contractId":value.quote.contract,"role":value.role,"strike":decimal(&value.quote.strike),"bid":decimal(&value.quote.bid),"ask":decimal(&value.quote.ask),"delta":value.quote.delta,"isCall":value.quote.is_call,"vega":value.quote.vega,"multiplier":decimal(&value.quote.multiplier),"quoteAtUtc":stamp(&value.quote_at)?,"frozen":value.frozen,"isFuture":value.quote.is_future,"sign":value.quote.side}))
}
pub fn account(value:&Json)->Result<Json,String>{
 fn camel(value:&Json)->Json{match value{
  Json::Object(values)=>Json::Object(values.iter().map(|(key,value)|{let mut key=key.clone();if !key.is_empty(){key[..1].make_ascii_lowercase();}(key,camel(value))}).collect()),
  Json::Array(values)=>Json::Array(values.iter().map(camel).collect()),_=>value.clone(),
 }}
 let mut result=camel(value);
 result["id"]["isValid"]=value["Id"]["AccountAlias"].as_str().is_some_and(|s|!s.trim().is_empty()).into();
 Ok(result)
}
/// Quote age advances only with API publications. A disconnected workstation
/// clock cannot change a backtest's age or manufacture a fresh timestamp.
pub fn quote_age(as_of:&str,quote:&str)->String{
 let parsed=chrono::DateTime::parse_from_rfc3339(as_of).ok().zip(chrono::DateTime::parse_from_rfc3339(quote).ok());
 parsed.map(|(now,quote)|format!("{:.0}s",((now-quote).num_milliseconds() as f64/1000.0).max(0.0))).unwrap_or("Unavailable".into())
}
