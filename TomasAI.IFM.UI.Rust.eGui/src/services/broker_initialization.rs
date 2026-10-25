//! Parallel read-only hydration for a broker draft. Each missing source is
//! reported separately, matching the hybrid's incomplete-data presentation.
//! All reads share the caller's deadline and API clock; no failure invents
//! balances, risk limits, quotes or authorization.
use crate::backend::{contracts::*,schema::Registry,transport::Backend};
use super::{broker_reads::{self,Scope},business_context::BusinessContext,financial,portfolio};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::BTreeMap;
#[derive(Default)]
pub struct Initialization {
 pub balances:Option<Json>,pub usage:Option<Json>,pub risk:Option<Json>,pub order:Option<Json>,
 pub trade:Option<Json>,pub underlying:Option<Json>,pub price:Option<Json>,pub account:Option<Json>,
 pub reservation:Option<Json>,pub errors:Vec<String>,
}
async fn basic(backend:&Backend,registry:&Registry,kind:&str,result:&str,key:&str,
 mut fields:BTreeMap<String,Value>,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 let allowed=["TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractQuery","TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetFuturesEodDataQuery","TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesEodDataQuery","TomasAI.IFM.Domain.Trade.Shared.Queries.GetOptionTradeQuery"];
 if !allowed.contains(&kind){return Err("Unsupported broker initialization read".into())}
 let(actor,verb)=registry.route(kind)?;let subject=ActorSubject::query(actor,verb,key);
 fields.insert("Subject".into(),subject.wire());fields.insert("EntityId".into(),Value::Array(vec![0.into(),Value::Array(vec![key.into()])]));
 let body=registry.encode(kind,&fields)?;let reply=backend.request_until(&subject,&body,deadline,None).await?;
 // A new option trade legitimately has no persisted trade yet. No other
 // unavailable/unauthorized result is interpreted as an empty record.
 if kind.ends_with(".GetOptionTradeQuery")&&reply.as_array().is_some_and(|a|a.len()>=4&&a[0].as_bool()==Some(false)&&a[1].as_i64()==Some(404)){return Ok(None)}
 let value=service_result(&reply)?;if value.is_nil(){Ok(None)}else{registry.project(result,value).map(Some)}
}
async fn financial_value(backend:&Backend,registry:&Registry,context:&BusinessContext,scope:Scope,
 name:&str,result:&str,parameters:Value,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 let read=financial::query(backend,registry,context,name,scope.portfolio,Some(scope.fund),parameters,financial::AccessScope::MetricsReader,deadline).await?;
 read.value.map(|v|registry.project(&format!("{}.{}",financial::ROOT,result),&v)).transpose()
}
async fn stored_price(backend:&Backend,registry:&Registry,context:&BusinessContext,contract:&str,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 let key=format!("{contract}.{}",context.value_date);
 let fields=BTreeMap::from([("ContractId".into(),contract.into()),("ValueDate".into(),date_only_wire(context.value_date))]);
 let result="TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesEodDataV2ReadModel";
 let current=basic(backend,registry,"TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetFuturesEodDataQuery",result,&key,fields.clone(),deadline).await;
 if let Ok(Some(value))=&current {
  let positive=|key:&str|value[key].as_f64().or_else(||value[key].as_str().and_then(|s|s.parse::<f64>().ok())).is_some_and(|v|v.is_finite()&&v>0.0);
  if positive("ClosePrice")&&positive("DailyStdDevAmount"){return current}
 }
 basic(backend,registry,"TomasAI.IFM.Domain.MarketData.Feed.Shared.Queries.GetLastFuturesEodDataQuery",result,&key,fields,deadline).await
}
pub async fn load(backend:&Backend,registry:&Registry,context:&BusinessContext,scope:Scope,trade_id:i32,
 contract:&str,is_future:bool,alias:&str,deadline:tokio::time::Instant)->Result<Initialization,String>{
 scope.key()?;if trade_id<=0||contract.is_empty(){return Err("Invalid broker initialization identity".into())}
 let risk=async{
  let wire=portfolio::query(backend,registry,context,"GetFundRiskEnvelopeQuery",&format!("{}.{}",scope.portfolio,scope.fund),BTreeMap::from([
   ("PortfolioId".into(),scope.portfolio.into()),("FundId".into(),scope.fund.into()),("AsOfUtc".into(),timestamp_wire(context.as_of_utc))]),deadline).await?;
  if wire.is_nil(){return Ok(None)}
  let row=registry.project(&format!("{}.ViewModels.FundRiskEnvelopeReadModel",portfolio::ROOT),&wire)?;
  if row["PortfolioId"]!=scope.portfolio||row["FundId"]!=scope.fund{return Err("Risk envelope crossed the selected scope".into())}
  Ok::<_,String>(Some(row))
 };
 let order=async{
  let wire=portfolio::query(backend,registry,context,"GetFundOrderByOrderIdQuery",&scope.order.to_string(),BTreeMap::from([("OrderId".into(),scope.order.into())]),deadline).await?;
  if wire.is_nil(){return Ok(None)}
  let row=registry.project(&format!("{}.ViewModels.FundOrderProjectionReadModel",portfolio::ROOT),&wire)?;
  if !scope.matches(&row){return Err("Order initialization crossed the selected scope".into())}
  Ok::<_,String>(Some(row))
 };
 let underlying=async{
  let result=basic(backend,registry,"TomasAI.IFM.Domain.MarketData.Shared.Queries.GetFuturesContractQuery","TomasAI.IFM.Domain.MarketData.Shared.ViewModels.FuturesContractV3ReadModel",contract,BTreeMap::from([("ContractId".into(),contract.into())]),deadline).await?;
  if result.as_ref().is_some_and(|r|r["ContractId"]!=contract){return Err("Underlying definition crossed the selected contract".into())}Ok(result)
 };
 let trade=async{
  if is_future{return Ok(None)}
  let result=basic(backend,registry,"TomasAI.IFM.Domain.Trade.Shared.Queries.GetOptionTradeQuery","TomasAI.IFM.Domain.Trade.Shared.ViewModels.OptionTradeReadModel",&format!("{}.{}",scope.order,trade_id),BTreeMap::from([("OrderId".into(),scope.order.into()),("TradeId".into(),trade_id.into())]),deadline).await?;
  if result.as_ref().is_some_and(|r|r["OrderId"]!=scope.order||r["TradeId"]!=trade_id){return Err("Stored trade crossed the selected identity".into())}Ok(result)
 };
 let balances=financial_value(backend,registry,context,scope,"GetAccountBalancesQuery","FinancialBalanceSnapshot",Value::Array(vec![]),deadline);
 let usage=financial_value(backend,registry,context,scope,"GetCapacityUsageQuery","FinancialCapacityUsage",Value::Array(vec![]),deadline);
 let account=broker_reads::account(backend,registry,alias,deadline);
 let price=stored_price(backend,registry,context,contract,deadline);
 let(balances,usage,risk,order,underlying,trade,account,price)=tokio::join!(balances,usage,risk,order,underlying,trade,account,price);
 let mut errors=vec![];
 let mut read=|name:&str,result:Result<Option<Json>,String>|match result{Ok(value)=>value,Err(error)=>{errors.push(format!("{name}: {error}"));None}};
 let mut result=Initialization{balances:read("Fund balances",balances),usage:read("Fund capacity usage",usage),risk:read("Fund risk envelope",risk),order:read("Order",order),underlying:read("Underlying definition",underlying),trade:read("Stored trade",trade),account:read("Broker account",account),price:read("Stored underlying price",price),..Default::default()};
 if let Some(id)=result.order.as_ref().and_then(|o|o["RiskAuthorization"]["ReservationId"].as_str()){
  let id=uuid::Uuid::parse_str(id).map_err(|_|"Invalid stored reservation identity")?;
  let read=financial_value(backend,registry,context,scope,"GetCapacityReservationQuery","FinancialReservationView",Value::Array(vec![id.to_string().into()]),deadline).await;
  match read{Ok(value)=>result.reservation=value,Err(error)=>errors.push(format!("Order reservation: {error}"))}
 }
 result.errors=errors;Ok(result)
}
