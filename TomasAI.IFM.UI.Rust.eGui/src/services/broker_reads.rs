//! Read-only broker/account evidence. Account aliases and execution identities
//! are explicit scope, never inferred from displayed captions. Failed refreshes
//! return an error so the owner can retain its last complete evidence cut.
use crate::backend::{contracts::*,schema::Registry,transport::Backend};
use rmpv::Value;
use serde_json::Value as Json;
use std::collections::{BTreeMap,BTreeSet};
const TRADE:&str="TomasAI.IFM.Domain.Trade.Shared";
const ACCOUNT:&str="TomasAI.IFM.Domain.BrokerAccount.Contracts";
#[derive(Clone,Copy,Debug,Eq,PartialEq,Ord,PartialOrd)]
pub struct Scope{pub portfolio:i32,pub fund:i32,pub order:i32}
impl Scope {
 pub fn key(self)->Result<String,String>{
  if self.portfolio<=0||self.fund<=0||self.order<=0{return Err("Invalid broker Portfolio/Fund/order scope".into())}
  Ok(format!("{}.{}.{}",self.portfolio,self.fund,self.order))
 }
 pub fn wire(self)->Result<Value,String>{self.key()?;Ok(Value::Array(vec![self.portfolio.into(),self.fund.into(),self.order.into()]))}
 pub fn matches(self,row:&Json)->bool{row["PortfolioId"]==self.portfolio&&row["FundId"]==self.fund&&row["OrderId"]==self.order}
}
async fn query(backend:&Backend,registry:&Registry,kind:&str,key:&str,
 mut fields:BTreeMap<String,Value>,deadline:tokio::time::Instant)->Result<Value,String>{
 if ![format!("{ACCOUNT}.GetBrokerAccountQuery"),format!("{TRADE}.Order.Broker.GetBrokerOrdersForTradeOrderQuery"),format!("{TRADE}.Order.Execution.GetOrderExecutionQuery")].iter().any(|s|s==kind){return Err("Unsupported broker evidence query".into())}
 let (actor,verb)=registry.route(kind)?;let subject=ActorSubject::query(actor,verb,key);
 fields.insert("Subject".into(),subject.wire());
 fields.insert("EntityId".into(),Value::Array(vec![0.into(),Value::Array(vec![key.into()])]));
 let body=registry.encode(kind,&fields)?;let reply=backend.request_until(&subject,&body,deadline,None).await?;
 Ok(service_result(&reply)?.clone())
}
pub async fn account(backend:&Backend,registry:&Registry,alias:&str,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 if alias.trim().is_empty()||alias.len()>256{return Err("Invalid broker account alias".into())}
 let id=registry.encode(&format!("{ACCOUNT}.BrokerAccountId"),&BTreeMap::from([("AccountAlias".into(),alias.into())]))?;
 let value=query(backend,registry,&format!("{ACCOUNT}.GetBrokerAccountQuery"),alias,BTreeMap::from([("BrokerAccountId".into(),id)]),deadline).await?;
 if value.is_nil(){return Ok(None)}
 let result=super::parameters::document(registry,&format!("{ACCOUNT}.BrokerAccountDefinition"),&value,true)?;
 if result["Id"]["AccountAlias"]!=alias{return Err("Broker account response crossed the selected alias".into())}
 Ok(Some(result))
}
pub async fn orders(backend:&Backend,registry:&Registry,scope:Scope,deadline:tokio::time::Instant)->Result<Vec<Json>,String>{
 let value=query(backend,registry,&format!("{TRADE}.Order.Broker.GetBrokerOrdersForTradeOrderQuery"),&scope.key()?,BTreeMap::from([("TradeOrderId".into(),scope.wire()?)]),deadline).await?;
 let result=super::parameters::document(registry,&format!("{TRADE}.Order.Broker.BrokerOrderDefinition[]"),&value,true)?;
 let rows=result.as_array().filter(|v|v.len()<=10000).ok_or("Invalid broker-order collection")?;
 let mut ids=BTreeSet::new();
 for row in rows {
  if !scope.matches(&row["Id"]["Execution"]["TradeOrder"]){return Err("Broker order crossed the requested order scope".into())}
  let key=identity(&row["Id"])?;
  if !ids.insert(key){return Err("Duplicate broker-order identity".into())}
 }
 Ok(rows.clone())
}
pub fn identity(id:&Json)->Result<String,String>{
 let scope=&id["Execution"]["TradeOrder"];
 let number=|key:&str|scope[key].as_i64().and_then(|v|i32::try_from(v).ok()).ok_or("Invalid broker-order scope");
 let scope=Scope{portfolio:number("PortfolioId")?,fund:number("FundId")?,order:number("OrderId")?};
 let attempt=guid(&id["Execution"]["ExecutionAttemptId"])?;let component=guid(&id["ComponentId"])?;
 Ok(format!("{}.{}.{}",scope.key()?,attempt.simple(),component.simple()))
}
fn guid(value:&Json)->Result<uuid::Uuid,String>{
 uuid::Uuid::parse_str(value.as_str().ok_or("Missing execution identity")?).ok().filter(|v|!v.is_nil()).ok_or("Invalid execution identity".into())
}
pub async fn execution(backend:&Backend,registry:&Registry,scope:Scope,attempt:uuid::Uuid,deadline:tokio::time::Instant)->Result<Option<Json>,String>{
 if attempt.is_nil(){return Err("Invalid execution attempt identity".into())}
 let key=format!("{}.{}",scope.key()?,attempt.simple());
 let value=query(backend,registry,&format!("{TRADE}.Order.Execution.GetOrderExecutionQuery"),&key,BTreeMap::from([
  ("TradeOrderId".into(),scope.wire()?),("ExecutionAttemptId".into(),attempt.to_string().into())]),deadline).await?;
 if value.is_nil(){return Ok(None)}
 let result=super::parameters::document(registry,&format!("{TRADE}.OrderExecutionDefinition"),&value,true)?;
 if !scope.matches(&result["TradeOrderId"])||result["ExecutionAttemptId"]!=attempt.to_string(){return Err("Execution response crossed the requested attempt".into())}
 Ok(Some(result))
}
#[derive(Default)]
pub struct Evidence{pub orders:Vec<Json>,pub executions:Vec<Json>}
pub async fn evidence(backend:&Backend,registry:&Registry,scopes:&[Scope],deadline:tokio::time::Instant)->Result<Evidence,String>{
 let mut result=Evidence::default();let mut attempts=BTreeSet::new();let mut ids=BTreeSet::new();
 let mut seen_scopes=BTreeSet::new();
 for scope in scopes.iter().copied().filter(|scope|seen_scopes.insert(*scope)) {
  for order in orders(backend,registry,scope,deadline).await? {
   let identity=identity(&order["Id"])?;if !ids.insert(identity){return Err("Broker evidence repeated an order across scopes".into())}
   let attempt=guid(&order["Id"]["Execution"]["ExecutionAttemptId"])?;
   if attempts.insert((scope,attempt)){if let Some(value)=execution(backend,registry,scope,attempt,deadline).await?{result.executions.push(value);}}
   result.orders.push(order);
  }
 }
 Ok(result)
}
