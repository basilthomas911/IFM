//! Read-only maps for the copied monitor. No price, P&L, Greek or risk value is
//! computed here; these adapters add only source DTO display getters.
use serde_json::{Value as Json,json};
pub fn document(value:&Json)->Json{
 let mut result=super::broker_order_ui::display(value);
 fn enrich(source:&Json,target:&mut Json){
  match(source,target){
   (Json::Object(source),Json::Object(target))=>{
    for(name,value)in source{let mut camel=name.clone();if !camel.is_empty(){camel[..1].make_ascii_lowercase();}if let Some(target)=target.get_mut(&camel){enrich(value,target);}}
    let decimal=|name:&str|source.get(name).is_some_and(|v|!v.is_null());
    if source.contains_key("UnavailableReasons")&&source.contains_key("CalculationVersion"){
     let complete=source.get("CalculationStatus").filter(|v|!v.is_null()).map(|v|v=="Complete").unwrap_or_else(||source.get("UnavailableReasons").and_then(Json::as_array).is_some_and(Vec::is_empty)&&source.get("TradeType").is_some_and(|v|!v.is_null())&&["ForwardPrice","ForwardLossRatio","AssetPrice","ShortPutGamma","ShortCallGamma","MaxProfit","MaxLoss"].iter().all(|n|decimal(n)));
     target.insert("isComplete".into(),complete.into());
     let forward=source.get("CombinedForwardPrice").cloned().unwrap_or(Json::Null);
     let forward=if source.get("CalculationVersion").is_some_and(|v|v=="IronCondor/EntryCreditForwardPnl/v2"){
      forward.as_str().map(|price|{let zero=price.bytes().all(|b|b==b'0'||b==b'.'||b==b'-');if zero{price.trim_start_matches('-').to_string()}else if let Some(price)=price.strip_prefix('-'){price.into()}else{format!("-{price}")}}).map(Json::from).unwrap_or(Json::Null)
     }else{forward};
     target.insert("forwardSpreadForChart".into(),forward);
    }
    if source.contains_key("MaxLossLimit")&&source.contains_key("MaxProfitTarget")&&source.contains_key("TradeId"){target.insert("isValid".into(),source["TradeId"].as_i64().is_some_and(|id|id>0).into());}
    if source.contains_key("TickId")&&source.contains_key("ContractId")&&source.contains_key("ValueDate"){
     let valid=source["ContractId"].as_str().is_some()&&source["ValueDate"]!="0001-01-01"&&source["TickId"].as_i64().is_some_and(|n|n>0);
     let empty=source["ContractId"].as_str().is_none_or(str::is_empty)||source["ValueDate"]=="0001-01-01"||source["TickId"]==0;
     target.insert("isEmpty".into(),empty.into());target.insert("isValid".into(),valid.into());
     target.insert("entityId".into(),json!({"contractId":source["ContractId"],"valueDate":source["ValueDate"],"monitoringOwnerId":uuid::Uuid::nil().to_string(),"isValid":source["ContractId"].as_str().is_some_and(|s|!s.is_empty())&&source["ValueDate"]!="0001-01-01"}));
    }
    if source.contains_key("OriginalFills")&&source.contains_key("EstablishedAtUtc"){
     // The query-enriched opening valuation already owns the execution session
     // date. Do not recreate an operational-date provider in the frontend.
     target.insert("tradeDate".into(),source.get("OpeningHistoryValuation").and_then(|v|v.get("ValueDate")).cloned().unwrap_or(Json::Null));
     let expiries=source.get("Legs").and_then(Json::as_array);let maturity=expiries.filter(|legs|!legs.is_empty()&&legs.iter().all(|l|l["Expiry"].as_str().is_some())).and_then(|legs|legs.iter().filter_map(|l|l["Expiry"].as_str()).max());
     target.insert("maturityDate".into(),maturity.map(Json::from).unwrap_or(Json::Null));
    }
   },(Json::Array(source),Json::Array(target))=>for(source,target)in source.iter().zip(target){enrich(source,target)},_=>{},
  }
 }
 enrich(value,&mut result);result
}
pub fn plan_history(plan:&Json)->Json{
 let mut result=document(plan);if let Some(root)=result.as_object_mut(){root.remove("position");root.remove("parameters");}
 if let Some(detail)=result["ironCondorTradePlanSnapshot"].as_object_mut(){for key in ["position","legs","legacyLegs","strategyRiskParameterSet"]{detail.remove(key);}}result
}
/// Eastern time only, matching the status console's 12-hour millisecond display.
pub fn action_time(plan:&Json)->Result<String,String>{
 let value=plan["IronCondorTradePlanSnapshot"].get("ActionDateTime").filter(|v|!v.is_null()).unwrap_or(&plan["CalculatedAtUtc"]);
 let text=value.as_str().ok_or("Missing Trade Plan action time")?;if text.starts_with("0001-"){return Ok(String::new())}
 let utc=chrono::DateTime::parse_from_rfc3339(text).map_err(|_|"Invalid Trade Plan action time")?;
 Ok(utc.with_timezone(&chrono_tz::America::New_York).format("%I:%M:%S%.3f %p").to_string())
}

/// The hybrid's enum-name projection uses Enum.ToString even for an unnamed
/// numeric value. Normalize using compiled types, never by guessing that an
/// arbitrary numeric amount/sequence is an enum.
pub fn named(registry:&crate::backend::schema::Registry,kind:&str,wire:&rmpv::Value)->Result<Json,String>{
 fn enums(registry:&crate::backend::schema::Registry,kind:&str,value:&mut Json)->Result<(),String>{
  if value.is_null(){return Ok(())}
  let kind=kind.strip_prefix("System.Nullable`1[").and_then(|s|s.strip_suffix(']')).unwrap_or(kind);
  if let Some(inner)=kind.strip_suffix("[]").or_else(||kind.strip_prefix("System.Collections.Immutable.ImmutableArray`1[").and_then(|s|s.strip_suffix(']'))){if let Some(values)=value.as_array_mut(){for value in values{enums(registry,inner,value)?;}}return Ok(())}
  if let Ok(shape)=registry.describe(kind){
   if shape.is_enum{if value.is_number(){*value=value.to_string().into();}return Ok(())}
   if shape.unions.is_empty(){for(name,_,inner)in shape.members{if let Some(value)=value.get_mut(&name){enums(registry,&inner,value)?;}}}
  }Ok(())
 }
 let mut value=super::parameters::document(registry,kind,wire,true)?;enums(registry,kind,&mut value)?;Ok(value)
}
