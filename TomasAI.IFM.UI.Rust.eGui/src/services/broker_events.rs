//! A bounded, operation-scoped committed-event watch. It is installed before a
//! command is sent and can outlive a request deadline. Metadata, correlation,
//! actor route and both entity identities must agree before resolving a write.
use crate::backend::{codec,contracts::*,schema::Registry,transport::Backend};
use futures_util::StreamExt;
use rmpv::Value;
pub struct Watch{receiver:tokio::sync::watch::Receiver<Option<Value>>,task:tokio::task::JoinHandle<()>}
impl Drop for Watch{fn drop(&mut self){self.task.abort();}}
impl Watch {
 pub fn committed(&self)->bool{self.receiver.borrow().is_some()}
 pub fn receiver(&self)->tokio::sync::watch::Receiver<Option<Value>>{self.receiver.clone()}
 pub async fn start(backend:&Backend,registry:&Registry,event_kind:&str,definition:&str,definition_kind:&str,
  command:uuid::Uuid,entity_key:&str,entity:Value,deadline:tokio::time::Instant)->Result<Self,String>{
  let(actor,verb)=registry.route(event_kind)?;
  let slot=|kind:&str,name:&str|registry.describe(kind)?.members.into_iter().find(|(n,_,_)|n==name).map(|(_,index,_)|index).ok_or(format!("Missing committed-event field {name}"));
  let subject_slot=slot(event_kind,"Subject")?;let id_slot=slot(event_kind,"Id")?;let entity_slot=slot(event_kind,"EntityId")?;
  let number_slot=slot(event_kind,"EventId")?;let command_slot=slot(event_kind,"CommandId")?;let definition_slot=slot(event_kind,definition)?;let definition_id=slot(definition_kind,"Id")?;
  let route=ActorSubject{kind:ActorKind::Event,name:actor.into(),verb:verb.into(),entity:entity_key.into()};let route_text=route.route()?;let route_wire=route.wire();
  let command=command.to_string();let mut events=backend.subscribe("Event",actor).await?;backend.flush_until(deadline).await?;
  let(sender,receiver)=tokio::sync::watch::channel(None);
  let task=tokio::spawn(async move{
   while let Some(message)=events.next().await{
    if message.subject.as_str()!=route_text{continue}
    let Ok(value)=codec::decode(&message.payload)else{continue};let Some(parts)=value.as_array()else{continue};
    let at=|index|parts.get(index);
    if at(subject_slot)!=Some(&route_wire)||at(entity_slot)!=Some(&entity)
     ||at(command_slot).and_then(Value::as_str)!=Some(command.as_str())||at(number_slot).and_then(Value::as_i64).is_none_or(|v|v<=0)
     ||at(id_slot).and_then(Value::as_str).and_then(|s|uuid::Uuid::parse_str(s).ok()).is_none_or(|id|id.is_nil())
     ||at(definition_slot).and_then(Value::as_array).and_then(|v|v.get(definition_id))!=Some(&entity){continue}
    let _=sender.send(Some(value));break;
   }
  });
  Ok(Self{receiver,task})
 }
}
