//! Identity/revision fences and retained history, without a local valuation or
//! business clock. Lifecycle EOD rows are sealed against later intraday quotes.
use crate::services::trade_monitor_queries::Scope;
use serde_json::{Value as Json,json};
use std::collections::{BTreeMap,BTreeSet,VecDeque};
pub struct Realtime{
 scope:Scope,date:String,position:Json,initial:Json,pub current:Option<Json>,retired:BTreeSet<String>,retired_order:VecDeque<String>,
}
fn stamp(value:&Json)->Option<chrono::DateTime<chrono::FixedOffset>>{value.as_str().and_then(|s|chrono::DateTime::parse_from_rfc3339(s).ok())}
impl Realtime{
 pub fn new(scope:Scope,date:chrono::NaiveDate,trade:&Json)->Result<Self,String>{let(_,position)=scope.position(trade["StrategyKind"].as_str().ok_or("Missing strategy")?)?;Ok(Self{scope,date:date.to_string(),position,initial:trade.clone(),current:None,retired:BTreeSet::new(),retired_order:VecDeque::new()})}
 pub fn accept(&mut self,snapshot:Json)->bool{
  let Some(generation)=snapshot["MonitoringGeneration"].as_str().filter(|s|uuid::Uuid::parse_str(s).is_ok_and(|id|!id.is_nil()))else{return false};
  let Some(legs)=snapshot["Legs"].as_array().filter(|v|(1..=64).contains(&v.len()))else{return false};
  let reference=self.current.as_ref();let composition=reference.map(|v|&v["CompositionVersion"]).unwrap_or(&self.initial["EvidenceRevision"]);
  let expected=reference.map(|v|&v["Legs"]).unwrap_or(&self.initial["Legs"]);
  let mut ids=BTreeSet::new();
  if snapshot["SchemaVersion"]!=1||!self.scope.matches(&snapshot["TradeId"])||snapshot["PositionId"]!=self.position||snapshot["ValueDate"]!=self.date||snapshot["SnapshotRevision"].as_i64().is_none_or(|n|n<0)||snapshot["CompositionVersion"].as_i64().zip(composition.as_i64()).is_none_or(|(new,old)|new<old)||self.retired.contains(generation)||legs.iter().any(|leg|!ids.insert(leg["Definition"]["TradeLegId"].clone().to_string())){return false}
  if snapshot["CompositionVersion"]==*composition{
   let Some(expected)=expected.as_array()else{return false};
   if legs.len()!=expected.len()||legs.iter().any(|leg|!expected.iter().any(|old|{let old=old.get("Definition").unwrap_or(old);old["TradeLegId"]==leg["Definition"]["TradeLegId"]&&old["ContractId"]==leg["Definition"]["ContractId"]})){return false}
  }
  if let Some(prior)=reference{
   if snapshot["MonitoringGeneration"]==prior["MonitoringGeneration"]{
    if snapshot["SnapshotRevision"].as_i64()<=prior["SnapshotRevision"].as_i64(){return false}
   }else{
    if stamp(&snapshot["PublishedAtUtc"]).zip(stamp(&prior["PublishedAtUtc"])).is_none_or(|(new,old)|new<=old){return false}
    let Some(old)=prior["MonitoringGeneration"].as_str()else{return false};self.retired.insert(old.into());self.retired_order.push_back(old.into());
    if self.retired_order.len()>128{self.retired.remove(&self.retired_order.pop_front().unwrap());}
   }
  }
  self.current=Some(snapshot);true
 }
}
pub struct History{trade:Json,trade_type:String,positions:BTreeMap<(String,String),Json>,insertion:BTreeMap<(String,String),usize>}
impl History{
 pub fn new(trade:Json,trade_type:String)->Self{Self{trade,trade_type,positions:BTreeMap::new(),insertion:BTreeMap::new()}}
 pub fn remember(&mut self,position:Json)->bool{
  if position["HistoryValuation"].is_null()||position["Id"]["Trade"]!=self.trade["Id"]{return false}
  let Some(date)=position["HistoryValuation"]["ValueDate"].as_str().filter(|s|*s!="0001-01-01")else{return false};
  let phase=position["Phase"].as_str().unwrap_or_default();let key=(date.to_string(),if ["MarkToMarket","EndOfDay"].contains(&phase){"MarkToMarket"}else{phase}.to_string());
  if let Some(previous)=self.positions.get(&key){
   if previous["Phase"]=="EndOfDay"&&phase=="MarkToMarket"{return false}
   let rank=|v:&Json|(v["RouteGeneration"].as_i64().unwrap_or(-1),v["PositionSequence"].as_i64().unwrap_or(-1));
   if !(phase=="EndOfDay"&&previous["Phase"]=="MarkToMarket")&&rank(&position)<rank(previous){return false}
   if previous==&position{return false}
  }
  let next=self.insertion.len();self.insertion.entry(key.clone()).or_insert(next);
  self.positions.insert(key,position);true
 }
 pub fn positions(&self)->Vec<Json>{let mut rows=self.positions.values().filter(|row|row["Phase"]!="Open"&&!row["HistoryValuation"].is_null()).cloned().collect::<Vec<_>>();rows.sort_by(|a,b|a["HistoryValuation"]["ValueDate"].as_str().cmp(&b["HistoryValuation"]["ValueDate"].as_str()).then_with(||history_time(a).cmp(&history_time(b)).then_with(||{let key=|row:&Json|(row["HistoryValuation"]["ValueDate"].as_str().unwrap_or_default().to_string(),if row["Phase"]=="EndOfDay"{"MarkToMarket"}else{row["Phase"].as_str().unwrap_or_default()}.to_string());self.insertion.get(&key(a)).cmp(&self.insertion.get(&key(b)))})));rows}
 pub fn saved(&self,index:usize)->Option<Json>{if index==0{self.trade.get("OpeningPositionSnapshot").filter(|v|!v.is_null()).cloned()}else{self.positions().get(index-1).cloned()}}
 pub fn rows(&self)->Vec<Json>{
  let opening=&self.trade["OpeningHistoryValuation"];if opening.is_null()||!self.trade["Legs"].as_array().is_some_and(|legs|legs.iter().any(|leg|!leg["Expiry"].is_null())){return vec![]}
  let row=|value:&Json,status:&str,pnl:&Json|{let id=&self.trade["Id"];json!({"orderId":id["OrderId"],"tradeId":id["TradeId"],"tradeType":self.trade_type,"valueDate":value["ValueDate"],"daysToExpiry":value["DaysToExpiry"],"tradeStatus":status,"commission":value["Commission"],"netSpread":value["NetPrice"],"tradePnl":pnl,"isValid":true})};
  let mut rows=vec![row(opening,"Open",&self.trade["OpeningPositionSnapshot"]["DailyPnl"])];
  for position in self.positions(){let value=&position["HistoryValuation"];let status=match position["Phase"].as_str(){Some("EndOfDay")=>"EndOfDay",Some("Close")=>"Close",_=>"IntraDay"};let pnl=&position["DailyPnl"];rows.push(row(value,status,pnl));}rows
 }
}

fn history_time(value:&Json)->Option<chrono::DateTime<chrono::FixedOffset>>{let value=if value["HistoryAsOfUtc"].as_str().is_some_and(|s|s.starts_with("0001-")){&value["AsOfUtc"]}else{&value["HistoryAsOfUtc"]};stamp(value)}
