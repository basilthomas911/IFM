//! Working-order display and input eligibility. Backend actors remain the
//! authority for accepting a cancellation or price change. Decimal price/tick
//! checks never pass through display floats.
use crate::backend::contracts::ExactDecimal;
use super::{broker_reads,option_selection::compare,outlook::decimal_fixed};
use serde_json::{Value as Json,json};
fn dec(value:&Json)->Result<ExactDecimal,String>{ExactDecimal::parse(value.as_str().ok_or("Missing exact broker price")?)}
pub fn component(order:&Json)->Result<&Json,String>{
 let id=&order["Id"]["ComponentId"];
 let found=order["Order"]["Components"].as_array().ok_or("Missing broker components")?.iter().filter(|c|c["ComponentId"]==*id).collect::<Vec<_>>();
 if found.len()!=1{return Err("Broker order component is unavailable or ambiguous".into())}Ok(found[0])
}
fn coefficient(value:&ExactDecimal)->(u128,usize){
 let text=value.as_str().trim_start_matches('-');let scale=text.split_once('.').map(|(_,f)|f.len()).unwrap_or(0);
 (text.bytes().filter(|b|*b!=b'.').fold(0u128,|n,b|n*10+u128::from(b-b'0')),scale)
}
pub fn is_tick_multiple(value:&ExactDecimal,tick:&ExactDecimal)->bool{
 if compare(tick,&ExactDecimal::parse("0").unwrap()).is_le(){return false}
 let (value,vs)=coefficient(value);let(tick,ts)=coefficient(tick);
 if vs>=ts {
  tick.checked_mul(10u128.pow((vs-ts) as u32)).map(|divisor|value%divisor==0).unwrap_or(value==0)
 }else{
  let mut remainder=value%tick;for _ in 0..ts-vs{remainder=(remainder*10)%tick;}remainder==0
 }
}
pub fn can_mutate(order:&Json,execution:Option<&Json>,pending:bool)->Result<bool,String>{
 if pending||!["Working","PartiallyFilled"].contains(&order["Status"].as_str().unwrap_or_default())
  ||execution.is_some_and(|e|["Filled","Cancelled","Rejected"].contains(&e["Status"].as_str().unwrap_or_default())){return Ok(false)}
 let c=component(order)?;
 if ["TickIncrement","MinimumSignedNetDebitLimit","MaximumSignedNetDebitLimit"].iter().any(|k|c[*k].is_null()){return Ok(false)}
 Ok(compare(&dec(&c["TickIncrement"])?,&ExactDecimal::parse("0")?).is_gt()&&compare(&dec(&c["MinimumSignedNetDebitLimit"])?,&dec(&c["MaximumSignedNetDebitLimit"])?).is_le())
}
pub fn validate_limit(order:&Json,price:&ExactDecimal)->Result<(),String>{
 let c=component(order)?;let tick=dec(&c["TickIncrement"])?;
 if compare(price,&dec(&c["MinimumSignedNetDebitLimit"])?).is_lt()||compare(price,&dec(&c["MaximumSignedNetDebitLimit"])?).is_gt()||!is_tick_multiple(price,&tick){return Err("Limit price must be inside the component bounds and match its tick increment".into())}Ok(())
}
fn compact(value:&Json)->Result<String,String>{
 let mut text=decimal_fixed(value.as_str().ok_or("Missing exact broker caption price")?,8,false)?;
 if text.contains('.') {text=text.trim_end_matches('0').trim_end_matches('.').into();}Ok(text)
}
pub fn caption(order:&Json)->Result<String,String>{
 let c=component(order)?;let legs=c["Legs"].as_array().ok_or("Missing broker caption legs")?;
 let leg=|l:&Json|->Result<String,String>{if l["AssetFamily"]=="FuturesOption"{if l["Strike"].is_null(){Ok(String::new())}else{compact(&l["Strike"])}}else{Ok(format!("{} {}",if l["SignedQuantity"].as_i64().unwrap_or_default()>0{"buy"}else{"sell"},l["ContractId"].as_str().unwrap_or_default()))}};
 let mut calls:Vec<_>=legs.iter().filter(|l|l["PutCall"]==1).collect();calls.sort_by_key(|l|std::cmp::Reverse(l["SignedQuantity"].as_i64().unwrap_or_default()));
 let mut puts:Vec<_>=legs.iter().filter(|l|l["PutCall"]==2).collect();puts.sort_by_key(|l|l["SignedQuantity"].as_i64().unwrap_or_default());
 let mut groups=vec![];for side in [&calls,&puts]{let value=side.iter().map(|l|leg(l)).collect::<Result<Vec<_>,_>>()?.join("-");if !value.is_empty(){groups.push(value);}}
 let description=if legs.iter().all(|l|l["AssetFamily"]=="FuturesOption"){groups.join(":")}else{legs.iter().map(leg).collect::<Result<Vec<_>,_>>()?.join("-")};
 let kind=match c["StrategyKind"].as_str().unwrap_or_default(){
  "IronCondor"=>{
   let mut highest:Option<ExactDecimal>=None;for call in &calls{if !call["Strike"].is_null(){let value=dec(&call["Strike"])?;if highest.as_ref().is_none_or(|old|compare(&value,old).is_gt()){highest=Some(value);}}}
   let short=calls.iter().any(|l|l["SignedQuantity"].as_i64().unwrap_or_default()>0&&highest.as_ref().is_some_and(|v|dec(&l["Strike"]).is_ok_and(|s|compare(&s,v).is_eq())));
   if short{"ShortIronCondor"}else{"LongIronCondor"}
  },"VerticalSpread"=>"Vertical Spread","FuturesOutright"=>"Futures",other=>other,
 };
 Ok(format!("{kind} = {description} @ {}",compact(&order["CurrentSignedNetDebitLimit"])?))
}
pub fn summary(order:&Json,execution:Option<&Json>)->Result<Json,String>{
 let terminal=execution.filter(|e|["Filled","Cancelled","Rejected"].contains(&e["Status"].as_str().unwrap_or_default()));
 let nullable=|key:&str|execution.map(|e|e[key].clone()).unwrap_or(Json::Null);
 let remaining=execution.map(|e|(e["OrderQuantity"].as_i64().unwrap_or_default()-e["CumulativeFilledQuantity"].as_i64().unwrap_or_default()).max(0));
 Ok(json!({"orderId":broker_reads::identity(&order["Id"])?,"status":terminal.map(|e|&e["Status"]).unwrap_or(&order["Status"]),"brokerStatus":order["Status"],"executionStatus":nullable("Status"),"price":order["CurrentSignedNetDebitLimit"],"brokerRevision":order["BrokerRevision"],"timeInForce":order["Order"]["TimeInForce"],"brokerAlgorithm":order["Order"]["BrokerAlgorithm"],"algorithmPace":order["Order"]["AlgorithmPace"],"requested":nullable("OrderQuantity"),"filled":nullable("CumulativeFilledQuantity"),"remaining":remaining,"dispatchCategory":order["DispatchCategory"],"dispatchDetail":order["DispatchDetail"],"changedAtUtc":order["ChangedAtUtc"]}))
}
pub fn mutation(order:&Json,execution:Option<&Json>,pending:bool)->Result<Json,String>{
 let c=component(order)?;Ok(json!({"canMutate":can_mutate(order,execution,pending)?,"minimum":c["MinimumSignedNetDebitLimit"],"maximum":c["MaximumSignedNetDebitLimit"],"tick":c["TickIncrement"],"pending":pending}))
}

/// BridgeDocument adds computed identity getters that are intentionally absent
/// from MessagePack contracts. Keep these presentation-only and recursively scoped.
pub fn display(value:&Json)->Json{
 match value{
  Json::Object(values)=>{
   let mut result:serde_json::Map<String,Json>=values.iter().map(|(key,value)|{let mut key=key.clone();if !key.is_empty(){key[..1].make_ascii_lowercase();}(key,display(value))}).collect();
   let positive=|key:&str|value[key].as_i64().is_some_and(|n|n>0);
   let nonempty=|key:&str|value[key].as_str().and_then(|s|uuid::Uuid::parse_str(s).ok()).is_some_and(|id|!id.is_nil());
   let valid=if values.contains_key("PortfolioId")&&values.contains_key("FundId")&&values.contains_key("OrderId"){
    Some(positive("PortfolioId")&&positive("FundId")&&positive("OrderId")&&(!values.contains_key("TradeId")||positive("TradeId")))
   }else if values.contains_key("TradeOrder")&&values.contains_key("ExecutionAttemptId"){Some(display(&value["TradeOrder"])["isValid"]==true&&nonempty("ExecutionAttemptId"))}
   else if values.contains_key("Execution")&&values.contains_key("ComponentId"){Some(display(&value["Execution"])["isValid"]==true&&nonempty("ComponentId"))}
   else if values.contains_key("Trade")&&values.contains_key("PositionId"){Some(display(&value["Trade"])["isValid"]==true&&nonempty("PositionId"))}else{None};
   if let Some(valid)=valid{result.insert("isValid".into(),valid.into());}
   if values.contains_key("TradeOrderId")&&values.contains_key("ExecutionAttemptId"){
    result.insert("id".into(),display(&json!({"TradeOrder":value["TradeOrderId"],"ExecutionAttemptId":value["ExecutionAttemptId"]})));
   }
   Json::Object(result)
  },Json::Array(values)=>Json::Array(values.iter().map(display).collect()),_=>value.clone(),
 }
}
/// Property order matches the unchanged hybrid descriptor list, rather than
/// JSON map ordering. Decimal strings retain their backend scale.
pub fn properties(value:&Json,names:&[&str])->Result<Json,String>{
 let rows=names.iter().map(|name|{
  let mut key=name.to_string();key[..1].make_ascii_lowercase();let v=value.get(*name).or_else(||value.get(&key)).unwrap_or(&Json::Null);
  let text=if name.ends_with("AtUtc")&&!v.is_null(){crate::backend::date_display::general(v.as_str().ok_or("Missing property timestamp")?)?}
   else{match v{Json::Null=>String::new(),Json::String(s)=>s.clone(),Json::Bool(v)=>if *v{"True"}else{"False"}.into(),_=>v.to_string()}};
  Ok(json!({"name":name,"display":text}))
 }).collect::<Result<Vec<_>,String>>()?;Ok(json!({"properties":rows}))
}
pub const SUMMARY_NAMES:&[&str]=&["OrderId","Status","BrokerStatus","ExecutionStatus","Price","BrokerRevision","TimeInForce","BrokerAlgorithm","AlgorithmPace","Requested","Filled","Remaining","DispatchCategory","DispatchDetail","ChangedAtUtc"];
pub const FILL_NAMES:&[&str]=&["ExecutionFillId","ExecutionAttemptId","ComponentId","TradeLegId","LegacyMarketInstrumentId","SignedQuantity","Price","Commission","FilledAtUtc","ExternalExecutionId","ContractId"];
