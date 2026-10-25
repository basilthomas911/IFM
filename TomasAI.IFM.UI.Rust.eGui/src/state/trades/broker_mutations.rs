//! The workspace owns uncertain broker writes across Blotter incarnations.
//! Only matching actor evidence can resolve a missing acknowledgement.
use super::*;
use crate::services::broker_order_commands;
impl TradeController {
 pub(super) fn reconcile_order_operations(&mut self){
  let completed:Vec<_>=self.order_operations.iter_mut().filter_map(|(request,operation)|operation.reconcile().then(||request.clone())).collect();
  for request in completed{
   self.order_operations.remove(&request);
   if let Some((_,outcome))=self.receipts.get_mut(&request){outcome.state="Completed".into();outcome.message="Broker order command confirmed by its correlated event".into();self.reconciled.push(outcome.clone());}
  }
 }
 pub(super) async fn broker_mutation(&mut self,backend:&Backend,registry:&Registry,context:&BusinessContext,intent:&Json,deadline:tokio::time::Instant)->Result<String,String>{
  self.reconcile_order_operations();
  let composer=self.composer.as_mut().ok_or("Open Trade Blotter first")?;
  if let Some(session)=intent["fields"]["composerSessionId"].as_str(){if session!=composer.session{return Err("Blotter incarnation changed; reload its current evidence".into())}}
  composer.refresh_evidence(backend,registry,deadline).await?;
  let key=field(intent,"brokerOrderId");let(order,execution)=composer.broker_order(key)?;
  let pending=self.order_operations.values().any(|operation|operation.order_key==key);
  let price=if intent["intent"]=="update-limit"{Some(composer::entered(field(intent,"limitPrice")).ok_or("Invalid limit price")?)}else{None};
  let id=uuid::Uuid::parse_str(intent["requestId"].as_str().ok_or("Missing broker mutation identity")?).map_err(|_|"Invalid broker mutation identity")?;
  let operation=broker_order_commands::prepare(registry,context,&order,execution.as_ref(),pending,id,price.as_ref())?;
  // Insert before I/O so a caller timeout cannot lose the only event watch.
  self.order_operations.insert(id.to_string(),operation);
  let result=self.order_operations.get_mut(&id.to_string()).unwrap().submit(backend,registry,deadline).await;
  match result{
   Ok(())=>{self.order_operations.remove(&id.to_string());self.confirmed_write=true;},
   Err(error)=>{if !error.starts_with("OutcomeUnknown:"){self.order_operations.remove(&id.to_string());}return Err(error)},
  }
  self.composer.as_mut().unwrap().refresh_evidence(backend,registry,deadline).await?;
  Ok("Broker mutation accepted; refresh for execution acknowledgement".into())
 }
}
