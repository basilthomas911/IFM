//! Closed broker-account command adapter. API time replaces the hybrid's
//! workstation clock. A prepared operation owns its IDs before transmission;
//! an uncertain transmission is retained and is never blindly republished.
use crate::backend::{contracts::*,schema::Registry,transport::Backend};
use super::{business_context::BusinessContext,portfolio_commands::Prepared};
use rmpv::Value;
use std::collections::BTreeMap;
use futures_util::StreamExt;
const ROOT:&str="TomasAI.IFM.Domain.BrokerAccount.Contracts";
struct ReceiptWatch{receiver:tokio::sync::watch::Receiver<Option<Value>>,task:tokio::task::JoinHandle<()>}
impl Drop for ReceiptWatch{fn drop(&mut self){self.task.abort();}}
pub struct Operation{pub prepared:Prepared,pub attempted:bool,pub acknowledged:bool,receipt:Option<ReceiptWatch>}
pub fn prepare(registry:&Registry,context:&BusinessContext,alias:&str,id:uuid::Uuid,
 action:&str,fields:&BTreeMap<String,String>)->Result<Operation,String>{
 if alias.trim().is_empty()||alias.len()>256||id.is_nil(){return Err("Invalid broker account command identity".into())}
 let (name,verb,at)=match action{
  "qualification-submit"=>("SubmitAccountQualificationEvidenceCommand","SubmitAccountQualificationEvidence","SubmittedAtUtc"),
  "qualification-accept"=>("AcceptAccountQualificationCommand","AcceptAccountQualification","ReviewedAtUtc"),
  "qualification-revoke"=>("RevokeAccountQualificationCommand","RevokeAccountQualification","RevokedAtUtc"),
  "qualification-hold"=>("SetManualTradingHoldCommand","SetManualTradingHold","EffectiveAtUtc"),
  "qualification-release"=>("ReleaseManualTradingHoldCommand","ReleaseManualTradingHold","EffectiveAtUtc"),
  "qualification-resync"=>("RequestBrokerAccountResynchronizationCommand","RequestBrokerAccountResynchronization","RequestedAtUtc"),
  _=>return Err("Unsupported broker account action".into()),
 };
 let subject=ActorSubject{kind:ActorKind::Command,name:"BrokerAccountCommand".into(),verb:verb.into(),entity:alias.into()};subject.route()?;
 let entity=registry.encode(&format!("{ROOT}.BrokerAccountId"),&BTreeMap::from([("AccountAlias".into(),alias.into())]))?;
 let mut values=BTreeMap::from([("CommandId".into(),id.to_string().into()),("Subject".into(),subject.wire()),("EntityId".into(),entity),("PostEvents".into(),true.into()),(at.into(),timestamp_wire(context.as_of_utc))]);
 let get=|key:&str|fields.get(key).cloned().unwrap_or_default();
 if matches!(action,"qualification-submit"|"qualification-accept"){
  let hash=get("manifestHash");if hash.len()!=64||!hash.bytes().all(|v|v.is_ascii_hexdigit()){return Err("Manifest hash must contain 64 hexadecimal characters".into())}
  values.insert("ManifestHash".into(),hash.into());
 }
 if action=="qualification-submit"{values.insert("EvidenceReference".into(),get("evidenceReference").into());}
 if action=="qualification-accept"{values.insert("ApprovalId".into(),uuid::Uuid::new_v4().to_string().into());}
 if matches!(action,"qualification-accept"|"qualification-revoke"){values.insert("AuthorizedBy".into(),get("reviewer").into());}
 if matches!(action,"qualification-revoke"|"qualification-hold"|"qualification-release"){values.insert("Reason".into(),get("reason").into());}
 Ok(Operation{prepared:Prepared{command_id:id,body:registry.encode(&format!("{ROOT}.{name}"),&values)?,subject},attempted:false,acknowledged:false,receipt:None})
}
impl Operation {
 /// A late, correlated committed event resolves a lost reply without sending
 /// another command. Keep this operation alive across Blotter close/reopen.
 pub fn reconcile(&mut self)->bool{
  if self.receipt.as_ref().is_some_and(|watch|watch.receiver.borrow().is_some()){self.acknowledged=true;}
  self.acknowledged
 }
 pub async fn submit(&mut self,backend:&Backend,deadline:tokio::time::Instant)->Result<(),String>{
  if self.reconcile(){return Ok(())}
  if self.attempted{return Err("OutcomeUnknown: reconcile the original broker account command before another attempt".into())}
  // Install and flush the subscription before sending. The event may precede
  // the actor reply, and it remains observable after the request deadline.
  let mut subscription=backend.subscribe("Event","BrokerAccountEvent").await?;
  backend.flush_until(deadline).await?;
  let (sender,receiver)=tokio::sync::watch::channel(None);
  let id=self.prepared.command_id.to_string();let alias=self.prepared.subject.entity.clone();
  let task=tokio::spawn(async move{
   let route=format!("Event.BrokerAccountEvent.BrokerAccountChanged.{alias}");
   while let Some(event)=subscription.next().await{
    if event.subject.as_str()!=route.as_str(){continue}
    let Ok(body)=crate::backend::codec::decode(&event.payload)else{continue};
    let Some(parts)=body.as_array().filter(|p|p.len()>=9)else{continue};
    if parts[0]!=Value::Array(vec![3.into(),"BrokerAccountEvent".into(),"BrokerAccountChanged".into(),alias.clone().into()]){continue}
    if parts[4].as_str()!=Some(id.as_str())||parts[3].as_i64().is_none_or(|v|v<=0)
     ||parts[2].as_array().and_then(|p|p.first()).and_then(Value::as_str)!=Some(alias.as_str())
     ||parts[8].as_array().and_then(|p|p.get(1)).and_then(Value::as_array).and_then(|p|p.first()).and_then(Value::as_str)!=Some(alias.as_str()){continue}
    let _=sender.send(Some(body));break;
   }
  });
  self.receipt=Some(ReceiptWatch{receiver,task});
  self.attempted=true;
  let mut receiver=self.receipt.as_ref().unwrap().receiver.clone();
  let reply=super::portfolio_commands::submit(backend,&self.prepared,deadline);tokio::pin!(reply);
  let result=tokio::select!{
   result=&mut reply=>result.map(|_|()),
   event=async{receiver.wait_for(|v|v.is_some()).await.is_ok()}=>{if event{Ok(())}else{reply.await.map(|_|())}},
  };
  result.map_err(|e|e.replace("Portfolio command","Broker account command"))?;
  self.acknowledged=true;Ok(())
 }
}
