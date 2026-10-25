//! Backend-owned economics for an explicitly selected order. This adapter only
//! validates identities and transfers draft fields; it computes no risk or fees.
use crate::backend::{contracts::*,schema::Registry,transport::Backend};
use super::{broker_preview::Leg,broker_reads::Scope,parameters};
use serde_json::{Value as Json,json};
use rmpv::Value;
use std::collections::BTreeMap;
const ROOT:&str="TomasAI.IFM.Domain.Trade.Shared.Order";
const CANDIDATE:&str="TomasAI.IFM.Domain.Portfolio.Shared.OrderComposition.PortfolioOrderCandidate";
pub struct Draft{pub scope:Scope,pub trade:i32,pub underlying:String,pub strategy:String,pub expiry:chrono::NaiveDate,pub quantity:i32,pub price:ExactDecimal,pub legs:Vec<Leg>,pub quote_inputs:String,pub order_type:String,pub algorithm:String,pub tif:String,pub pace:String,pub composition:uuid::Uuid,pub workflow:uuid::Uuid,pub component:uuid::Uuid}
pub struct Prepared{pub request:Value,pub draft:Json,pub result:Json,pub candidate_wire:Value}
fn guid()->String{uuid::Uuid::new_v4().to_string()}
fn dec(v:&ExactDecimal)->Json{v.as_str().into()}
impl Draft{
 pub fn request(&self,registry:&Registry)->Result<(Value,Json),String>{self.request_with_context(registry,&super::execution_context::Context::from_env()?)}
 pub fn request_with_context(&self,registry:&Registry,route:&super::execution_context::Context)->Result<(Value,Json),String>{
  self.scope.key()?;
  if self.trade<=0||!(1..=100000).contains(&self.quantity)||self.underlying.trim().is_empty()||self.composition.is_nil()||self.workflow.is_nil()||self.component.is_nil(){return Err("Invalid order preparation identity".into())}
  let required=match self.strategy.as_str(){"FuturesOutright"=>1,"IronCondor"=>4,"VerticalSpread"=>2,_=>return Err("Unsupported order preparation strategy".into())};
  if self.legs.len()!=required{return Err("Select every required order leg".into())}
  let mut seen=std::collections::BTreeSet::new();
  for leg in &self.legs{
   if ![-1,1].contains(&leg.side)||!seen.insert(&leg.contract)||leg.contract.trim().is_empty()||leg.is_future!=(required==1)||leg.multiplier.as_ref().is_none_or(|m|super::option_selection::compare(m,&ExactDecimal::parse("0").unwrap()).is_le())||(!leg.is_future&&leg.strike.as_ref().is_none_or(|s|super::option_selection::compare(s,&ExactDecimal::parse("0").unwrap()).is_le())){return Err("Invalid contract direction, strike or multiplier".into())}
  }
  if required==4&&[true,false].iter().any(|call|{let side=self.legs.iter().filter(|l|l.is_call==*call).collect::<Vec<_>>();side.len()!=2||side.iter().map(|l|l.side).sum::<i32>()!=0}){return Err("An iron condor requires a long and short call and put".into())}
  if required==2&&(self.legs[0].is_call!=self.legs[1].is_call||self.legs.iter().map(|l|l.side).sum::<i32>()!=0){return Err("A vertical requires opposing legs on the same side".into())}
  let legs=self.legs.iter().map(|leg|json!({"TradeLegId":guid(),"AssetFamily":if leg.is_future{"Futures"}else{"FuturesOption"},"SignedQuantity":leg.side*self.quantity,"ContractKey":leg.contract,"ContractId":leg.contract,"Expiry":self.expiry.to_string(),"Strike":leg.strike.as_ref().map(dec),"PutCall":if leg.is_future{0}else if leg.is_call{1}else{2},"CashMultiplier":dec(leg.multiplier.as_ref().unwrap())})).collect::<Vec<_>>();
  let draft=json!({"CompositionId":self.composition.to_string(),"WorkflowId":self.workflow.to_string(),"StrategyKind":self.strategy,"Origin":"DesktopTradeOrder","ValueDate":"0001-01-01","ValidUntilUtc":"0001-01-01T00:00:00Z","DecisionHorizon":"","EvidenceHash":"","ProductSymbol":"","ProductExchange":"","ProductCurrency":"","MicroExecutionProfileId":"","MicroExecutionProfileHash":"","AccountPromotionApprovalReference":"","SetupTrade":{"OrderId":self.scope.order,"TradeId":self.trade,"FundId":self.scope.fund},"Components":[{"ComponentId":self.component.to_string(),"StrategyKind":self.strategy,"Legs":legs,"SignedNetDebitLimit":dec(&self.price)}],"PositionType":"Opening","BrokerAccountAlias":route.account_alias,"BrokerEnvironment":route.environment,"BrokerOrderType":self.order_type,"BrokerAlgorithm":self.algorithm,"TimeInForce":self.tif,"AlgorithmPace":self.pace});
  let wire=parameters::from_document(registry,CANDIDATE,&draft)?;
  let request=registry.encode(&format!("{ROOT}.TradeOrderPreparationRequest"),&BTreeMap::from([("PortfolioId".into(),self.scope.portfolio.into()),("UnderlyingContractId".into(),self.underlying.clone().into()),("Quantity".into(),self.quantity.into()),("Draft".into(),wire),("SelectedQuoteInputs".into(),self.quote_inputs.clone().into())]))?;
  Ok((request,draft))
 }
 pub async fn prepare(&self,backend:&Backend,registry:&Registry,deadline:tokio::time::Instant)->Result<Prepared,String>{
  self.prepare_with_context(backend,registry,&super::execution_context::Context::from_env()?,deadline).await
 }
 pub async fn prepare_with_context(&self,backend:&Backend,registry:&Registry,route:&super::execution_context::Context,deadline:tokio::time::Instant)->Result<Prepared,String>{
  let(request,draft)=self.request_with_context(registry,route)?;
  let subject=ActorSubject::query("TradeOrderQuery","PrepareTradeOrder",&self.composition.simple().to_string());
  let body=registry.encode(&format!("{ROOT}.PrepareTradeOrderQuery"),&BTreeMap::from([("Subject".into(),subject.wire()),("EntityId".into(),Value::Array(vec![0.into(),Value::Array(vec!["none".into()])])),("Request".into(),request.clone())]))?;
  let response=backend.request_until(&subject,&body,deadline,None).await?;
  let value=service_result(&response)?;
  let result=parameters::document(registry,&format!("{ROOT}.TradeOrderPreparation"),value,true)?;
  let candidate=&result["Candidate"];let components=candidate["Components"].as_array().ok_or("Missing prepared components")?;
  if candidate["BrokerAccountAlias"]!=route.account_alias||candidate["BrokerEnvironment"]!=route.environment||candidate["CompositionId"]!=self.composition.to_string()||candidate["SetupTrade"]!=draft["SetupTrade"]||components.len()!=1||components[0]["ComponentId"]!=self.component.to_string()||components[0]["SignedNetDebitLimit"].as_str().and_then(|s|ExactDecimal::parse(s).ok()).is_none_or(|p|super::option_selection::compare(&p,&self.price).is_ne())||result["LegCommissions"].as_array().is_none_or(|v|v.len()!=self.legs.len()){return Err("Backend preparation does not match the selected order".into())}
  let legs=components[0]["Legs"].as_array().ok_or("Missing prepared legs")?;
  if legs.len()!=self.legs.len()||legs.iter().zip(&self.legs).any(|(a,b)|a["ContractId"]!=b.contract||a["SignedQuantity"]!=b.side*self.quantity){return Err("Backend preparation changed selected contracts or quantities".into())}
  let candidate_wire=super::strategy::member(registry,&format!("{ROOT}.TradeOrderPreparation"),value,"Candidate")?.clone();
  Ok(Prepared{request,draft,result,candidate_wire})
 }
}
