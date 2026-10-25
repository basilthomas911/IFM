//! Closed, scope-checked established-trade reads. Historical bounds and live
//! dates come from the API; no workstation business clock or financial model.
use crate::backend::{contracts::*,schema::Registry,transport::Backend};
use super::{parameters,business_context::BusinessContext};
use serde_json::{Value as Json,json};
use rmpv::Value;
use std::collections::BTreeMap;
pub const ROOT:&str="TomasAI.IFM.Domain.Trade.Shared";
#[derive(Clone,Copy,Debug,Eq,PartialEq,Ord,PartialOrd)]
pub struct Scope{pub portfolio:i32,pub fund:i32,pub order:i32,pub trade:i32}
impl Scope{
 pub fn key(self)->Result<String,String>{if [self.portfolio,self.fund,self.order,self.trade].iter().any(|n|*n<=0){return Err("Invalid established trade scope".into())}Ok(format!("{}.{}.{}.{}",self.portfolio,self.fund,self.order,self.trade))}
 pub fn document(self)->Json{json!({"PortfolioId":self.portfolio,"FundId":self.fund,"OrderId":self.order,"TradeId":self.trade})}
 pub fn wire(self)->Result<Value,String>{self.key()?;Ok(Value::Array(vec![self.portfolio.into(),self.fund.into(),self.order.into(),self.trade.into()]))}
 pub fn position(self,strategy:&str)->Result<(String,Json),String>{let purpose=match strategy{"IronCondor"|"VerticalSpread"=>"strategy-position","FuturesOutright"=>"futures-position",_=>return Err("Unsupported established strategy".into())};let key=self.key()?;let id=super::order_submission::stable(&[purpose,&key]);Ok((format!("{key}.{}",id.simple()),json!({"Trade":self.document(),"PositionId":id.to_string()})))}
 pub fn matches(self,id:&Json)->bool{id==&self.document()}
}
async fn query(backend:&Backend,registry:&Registry,kind:&str,result:&str,key:&str,mut fields:BTreeMap<String,Value>,deadline:tokio::time::Instant)->Result<Json,String>{
 let allowed=[format!("{ROOT}.Futures.Option.GetIronCondorOptionTradeQuery"),format!("{ROOT}.Futures.Option.GetVerticalSpreadOptionTradeQuery"),format!("{ROOT}.Futures.GetFuturesTradeQuery"),format!("{ROOT}.Futures.Option.Position.GetIronCondorOptionTradePositionHistoryQuery"),format!("{ROOT}.Trade.Realtime.GetTradeRealtimeSnapshotQuery"),format!("{ROOT}.Queries.GetTradeLimitQuery"),format!("{ROOT}.Trade.Position.Plan.GetCurrentIronCondorTradePlanQuery"),format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanHistoryQuery"),format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanChartQuery"),"TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesOptionTickDataQuery".into()];
 if !allowed.iter().any(|value|value==kind){return Err("Unsupported monitor query".into())}
 let route=if kind.ends_with(".GetIronCondorOptionTradePositionHistoryQuery"){("FuturesOptionTradePositionQuery","GetIronCondorOptionTradePositionHistory")}else{registry.route(kind)?};
 let subject=ActorSubject::query(route.0,route.1,key);
 fields.insert("Subject".into(),subject.wire());fields.entry("EntityId".into()).or_insert_with(||Value::Array(vec![0.into(),Value::Array(vec!["none".into()])]));
 let response=backend.request_until(&subject,&registry.encode(kind,&fields)?,deadline,None).await?;
 super::monitor_display::named(registry,result,service_result(&response)?)
}
pub async fn established(backend:&Backend,registry:&Registry,scope:Scope,strategy:&str,deadline:tokio::time::Instant)->Result<Json,String>{
 let name=match strategy{"IronCondor"=>"Futures.Option.GetIronCondorOptionTradeQuery","VerticalSpread"=>"Futures.Option.GetVerticalSpreadOptionTradeQuery","FuturesOutright"=>"Futures.GetFuturesTradeQuery",_=>return Err("Unsupported established strategy".into())};
 let value=query(backend,registry,&format!("{ROOT}.{name}"),&format!("{ROOT}.EstablishedTradeDefinition"),&scope.key()?,BTreeMap::from([("TradeId".into(),scope.wire()?)]),deadline).await?;
 if !scope.matches(&value["Id"])||value["StrategyKind"]!=strategy{return Err("Established trade response crossed the selected financial identity".into())}Ok(value)
}
pub async fn positions(backend:&Backend,registry:&Registry,scope:Scope,context:&BusinessContext,deadline:tokio::time::Instant)->Result<Vec<Json>,String>{
 let(key,id)=scope.position("IronCondor")?;
 let result=query(backend,registry,&format!("{ROOT}.Futures.Option.Position.GetIronCondorOptionTradePositionHistoryQuery"),&format!("{ROOT}.StrategyPositionSnapshot[]"),&key,BTreeMap::from([("PositionId".into(),parameters::from_document(registry,&format!("{ROOT}.StrategyPositionId"),&id)?),("FromUtc".into(),timestamp_wire(chrono::DateTime::UNIX_EPOCH)),("ToUtc".into(),timestamp_wire(context.as_of_utc)),("PageSize".into(),1000.into()),("LoadAll".into(),true.into())]),deadline).await?;
 let rows=result.as_array().filter(|v|v.len()<=100000).ok_or("Invalid position history")?;
 if rows.iter().any(|position|position["Id"]!=id||position["StrategyKind"]!="IronCondor"){return Err("Position history crossed its requested strategy scope".into())}Ok(rows.clone())
}
pub async fn realtime(backend:&Backend,registry:&Registry,scope:Scope,date:chrono::NaiveDate,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 let result=query(backend,registry,&format!("{ROOT}.Trade.Realtime.GetTradeRealtimeSnapshotQuery"),&format!("{ROOT}.Trade.Realtime.TradeRealtimeSnapshot"),&scope.key()?,BTreeMap::from([("TradeId".into(),scope.wire()?),("ValueDate".into(),date_only_wire(date))]),deadline).await?;
 if result.is_null(){return Ok(None)}if !scope.matches(&result["TradeId"])||result["ValueDate"]!=date.to_string(){return Err("Realtime snapshot crossed its selected trade or API ValueDate".into())}Ok(Some(result))
}
fn plan_scope(registry:&Registry,scope:Scope,date:chrono::NaiveDate)->Result<(String,Value,Json),String>{let(key,id)=scope.position("IronCondor")?;let plan=json!({"Position":id,"ValueDate":date.to_string()});Ok((format!("{key}.{}.IronCondor",date.format("%Y%m%d")),parameters::from_document(registry,&format!("{ROOT}.Trade.Position.Plan.IronCondorTradePlanId"),&plan)?,id))}
pub async fn current_plan(backend:&Backend,registry:&Registry,scope:Scope,date:chrono::NaiveDate,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 let(key,plan,id)=plan_scope(registry,scope,date)?;let result=query(backend,registry,&format!("{ROOT}.Trade.Position.Plan.GetCurrentIronCondorTradePlanQuery"),&format!("{ROOT}.Trade.Position.Plan.StrategyTradePlanSnapshot"),&key,BTreeMap::from([("PlanId".into(),plan)]),deadline).await?;
 if result.is_null(){return Ok(None)}if result["Position"]["Id"]!=id||result["ValueDate"]!=date.to_string(){return Err("Trade Plan crossed the requested trade/date".into())}Ok(Some(result))
}
pub async fn plan_page(backend:&Backend,registry:&Registry,scope:Scope,date:chrono::NaiveDate,offset:i32,size:i32,maximum:Option<i64>,deadline:tokio::time::Instant)->Result<Json,String>{
 if offset<0||!(1..=100).contains(&size)||maximum.is_some_and(|n|n<0){return Err("Invalid Trade Plan page".into())}
 let(key,plan,id)=plan_scope(registry,scope,date)?;let result=query(backend,registry,&format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanHistoryQuery"),&format!("{ROOT}.Trade.Position.Plan.StrategyTradePlanHistoryPage"),&key,BTreeMap::from([("PlanId".into(),plan),("Offset".into(),offset.into()),("PageSize".into(),size.into()),("MaximumRevision".into(),maximum.map(Value::from).unwrap_or(Value::Nil))]),deadline).await?;
 let rows=result["Items"].as_array().filter(|v|v.len()<=size as usize).ok_or("Invalid Trade Plan history page")?;
 if rows.iter().any(|row|row["Position"]["Id"]!=id||row["ValueDate"]!=date.to_string())||maximum.is_some_and(|n|result["MaximumRevision"]!=n)||result["Offset"]!=offset{return Err("Trade Plan page crossed its requested scope or revision cut".into())}Ok(result)
}
pub async fn chart(backend:&Backend,registry:&Registry,scope:Scope,date:chrono::NaiveDate,deadline:tokio::time::Instant)->Result<Json,String>{
 let(key,plan,_)=plan_scope(registry,scope,date)?;let result=query(backend,registry,&format!("{ROOT}.Trade.Position.Plan.GetIronCondorTradePlanChartQuery"),&format!("{ROOT}.Trade.Position.Plan.StrategyTradePlanChartSeries"),&key,BTreeMap::from([("PlanId".into(),plan)]),deadline).await?;
 let points=result["Points"].as_array().filter(|v|v.len()<=100000).ok_or("Invalid bounded chart history")?;
 let instant=|v:&Json|v.as_str().and_then(|s|chrono::DateTime::parse_from_rfc3339(s).ok()).map(|v|v.to_utc()).ok_or("Invalid chart timestamp");
 let start=instant(&result["StartUtc"])?;let end=instant(&result["EndUtc"])?;
 if end<start||end-start>chrono::Duration::hours(6){return Err("Chart history crossed its six-hour backend window".into())}
 let mut previous=None;for point in points{let at=instant(&point["AtUtc"])?;if at<start||at>end||previous.is_some_and(|p|at<p){return Err("Chart points crossed or reordered the backend window".into())}previous=Some(at);}
 Ok(result)
}
pub async fn limits(backend:&Backend,registry:&Registry,scope:Scope,deadline:tokio::time::Instant)->Result<Json,String>{
 // The managed query constructor rebuilds its typed parameter from TradeId.
 // That parameter is outside the IActorEntityId wire union and serializes nil.
 let result=query(backend,registry,&format!("{ROOT}.Queries.GetTradeLimitQuery"),&format!("{ROOT}.ViewModels.TradeLimitReadModel"),&scope.trade.to_string(),BTreeMap::from([("TradeId".into(),scope.trade.into()),("EntityId".into(),Value::Nil)]),deadline).await?;
 if result["TradeId"]!=scope.trade{return Err("Persisted trade limits crossed the selected trade".into())}Ok(result)
}

pub async fn saved_leg(backend:&Backend,registry:&Registry,contract:&str,date:chrono::NaiveDate,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 if contract.trim().is_empty(){return Err("Missing saved option contract".into())}
 // The serialization constructor reconstructs the concrete parameter; the
 // managed writer emits nil for its non-union interface slot.
 let result=query(backend,registry,"TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesOptionTickDataQuery","TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesOptionTickDataV2ReadModel",&format!("{contract}.{date}"),BTreeMap::from([("ContractId".into(),contract.into()),("ValueDate".into(),date_only_wire(date)),("EntityId".into(),Value::Nil)]),deadline).await?;
 if result.is_null(){return Ok(None)}if result["ContractId"]!=contract||result["ValueDate"]!=date.to_string(){return Err("Saved option observation crossed the requested contract/date".into())}Ok(Some(result))
}
