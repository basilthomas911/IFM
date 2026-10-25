//! Account mutations outlive an individual Blotter. A closed/reopened editor
//! must not turn a lost acknowledgement into a fresh qualification command.
use super::*;
use crate::services::broker_account_commands;
impl TradeController {
 pub(super) fn reconcile_account_operations(&mut self){
  let completed:Vec<_>=self.account_operations.iter_mut().filter_map(|(request,operation)|operation.reconcile().then(||request.clone())).collect();
  for request in completed{
   self.account_operations.remove(&request);
   if let Some((_,outcome))=self.receipts.get_mut(&request){
    outcome.state="Completed".into();outcome.message="Broker account command confirmed by its correlated event".into();self.reconciled.push(outcome.clone());
   }
  }
 }
 pub(super) async fn account_action(&mut self,backend:&Backend,registry:&Registry,context:&BusinessContext,intent:&Json,deadline:tokio::time::Instant)->Result<String,String>{
  self.reconcile_account_operations();
  if self.composer.is_none(){return Err("Open Trade Blotter first".into())}
  if let Some(session)=intent["fields"]["composerSessionId"].as_str(){if session!=self.composer.as_ref().unwrap().session{return Err("Blotter incarnation changed; reload its current draft".into())}}
  if !self.account_operations.is_empty(){return Err("A broker account command is unconfirmed; refresh Account to reconcile its original event before another command".into())}
  let request=uuid::Uuid::parse_str(intent["requestId"].as_str().ok_or("Missing account request identity")?).map_err(|_|"Invalid account request identity")?;
  let fields=intent["fields"].as_object().ok_or("Missing qualification inputs")?.iter().map(|(key,value)|Ok((key.clone(),value.as_str().ok_or("Qualification input must be text")?.to_string()))).collect::<Result<BTreeMap<_,_>,String>>()?;
  let operation=broker_account_commands::prepare(registry,context,&self.composer.as_ref().unwrap().execution.account_alias,request,intent["intent"].as_str().ok_or("Missing account action")?,&fields)?;
  // Retain the operation before awaiting any network action. The receiver task
  // continues observing its correlation even if the caller deadline expires.
  self.account_operations.insert(request.to_string(),operation);
  let result=self.account_operations.get_mut(&request.to_string()).unwrap().submit(backend,deadline).await;
  match result {
   Ok(())=>{self.account_operations.remove(&request.to_string());self.confirmed_write=true;},
   Err(error)=>{if !error.starts_with("OutcomeUnknown:"){self.account_operations.remove(&request.to_string());}return Err(error)},
  }
  self.composer.as_mut().unwrap().refresh_account(backend,registry,deadline).await?;
  Ok("Broker account command accepted".into())
 }
}
