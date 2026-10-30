use super::*;
use crate::diagnostics::trade_load::{measure, mark};
impl Hub{
 pub async fn open(&self,backend:&Backend,registry:Arc<Registry>,request:OpenRequest,context:BusinessContext,deadline:tokio::time::Instant)->Result<String,String>{
  let key=request.scope.key()?;
  if self.owner(&key).is_ok(){mark("already_open");self.activate(&key)?;return Ok("Trade activated".into())}
  let trade=measure("established_trade",api::established(backend,&registry,request.scope,&request.strategy,deadline)).await?;
  let feed=Client::new(backend.clone(),&self.host_scope)?;
  let data=Arc::new(Mutex::new(data::Monitor::new(request,trade,context.clone())?));let incarnation=data.lock().map_err(|_|"Monitor lock failed")?.incarnation;
  {let mut state=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?;state.order.push(key.clone());state.owners.insert(key.clone(),Owner{feed,data:data.clone(),registry:registry.clone(),background:BTreeMap::new(),jobs:vec![]});state.active=Some(key.clone());}
  let mut guard=OpenGuard{hub:self.clone(),key:key.clone(),incarnation,armed:true};
  let result=async{
   let jobs=measure("event_subscriptions",self.start_events(backend.clone(),registry.clone(),&key,data.clone(),deadline)).await?;
   {let mut state=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?;state.owners.get_mut(&key).ok_or("Trade closed during event attachment")?.jobs=jobs;}
   let(scope,iron)={let state=data.lock().map_err(|_|"Monitor lock failed")?;(state.scope,state.iron())};
   if iron{
    let(history,limits)=tokio::join!(measure("position_history",api::positions(backend,&registry,scope,&context,deadline)),measure("risk_limits",api::limits(backend,&registry,scope,deadline)));
    {let mut state=data.lock().map_err(|_|"Monitor lock failed")?;for position in history?{state.history.remember(position);}state.limits=limits?;}
    // Balance availability never hides persisted risk limits.
    let balance=measure("fund_balance",crate::services::financial::query(backend,&registry,&context,"GetAccountBalancesQuery",scope.portfolio,Some(scope.fund),rmpv::Value::Array(vec![]),crate::services::financial::AccessScope::MetricsReader,deadline)).await;
    if let Ok(read)=balance{if let Some(value)=read.value{let value=crate::services::parameters::document(&registry,"TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialBalanceSnapshot",&value,true)?;data.lock().map_err(|_|"Monitor lock failed")?.balance=value["AvailableCash"].clone();}}
    let count=data.lock().map_err(|_|"Monitor lock failed")?.history.rows().len();if count>0{measure("initial_position_ready",self.select_position(backend,&registry,&key,count-1,deadline)).await?;}else{self.refresh_chart(backend,&registry,&key,deadline).await?;}

   }
   Ok::<_,String>(())
  }.await;
  if let Err(error)=result{
   // Only this incarnation is removed. A later open of the same trade cannot
   // be detached by a late reply belonging to this failed load.
   let same=self.owner(&key).ok().and_then(|owner|owner.lock().ok().map(|owner|owner.incarnation==incarnation)).unwrap_or(false);if same{self.close(&key)?;}return Err(error)
  }
  guard.armed=false;crate::diagnostics::trade_load::measure_sync("publish_loaded_view",||self.publish())?;
  self.schedule_saved_feed(backend,&key)?;Ok("Trade loaded".into())
 }
 /// Start optional history work after publishing the core trade. Background
 /// requests use their own bounded read budget, not the completed UI command.
 pub async fn refresh_chart(&self,backend:&Backend,_registry:&Registry,key:&str,_deadline:tokio::time::Instant)->Result<(),String>{
  self.schedule_chart(backend,key)
 }
 pub async fn select_position(&self,backend:&Backend,_registry:&Registry,key:&str,index:usize,_deadline:tokio::time::Instant)->Result<(),String>{
  let owner=self.owner(key)?;
  {let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;
   if state.live||state.starting{return Err("Position selection is unavailable while live".into())}
   let selected=state.history.saved(index).ok_or("Select a saved position")?;
   let date=crate::backend::date_input::parse(selected["ValueDate"].as_str().ok_or("Missing saved position date")?)?;
   if state.plan_date!=date{state.chart=Json::Null;}state.plan_date=date;
   state.detail_generation+=1;state.selected=Some(selected);state.observations.clear();state.page=None;
   state.observations_loading=true;state.observations_error.clear();
  }
  self.publish()?;
  self.schedule_details(backend,key)?;
  self.schedule_chart(backend,key)
 }
 pub async fn plans_page(&self,backend:&Backend,registry:&Registry,key:&str,offset:i32,size:i32,refresh:bool,deadline:tokio::time::Instant)->Result<(),String>{
  let owner=self.owner(key)?;let(scope,date,maximum,incarnation)={let state=owner.lock().map_err(|_|"Monitor lock failed")?;if state.live{return Ok(())}(state.scope,state.plan_date,if refresh{None}else{state.page.as_ref().and_then(|p|p["MaximumRevision"].as_i64())},state.incarnation)};
  let result=api::plan_page(backend,registry,scope,date,offset,size,maximum,deadline).await;
  {let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;if state.incarnation!=incarnation||state.live||state.plan_date!=date{return Ok(())}match result{Ok(page)=>{state.page=Some(page);state.plan_error.clear()},Err(error)=>state.plan_error=error}}
  self.publish()
 }
 pub async fn set_live(&self,backend:&Backend,registry:&Registry,key:&str,enabled:bool,context:&BusinessContext,deadline:tokio::time::Instant)->Result<(),String>{
  self.cancel_saved_feed(key)?;
  self.connect_feed(backend,key,deadline).await?;
  self.change_live(backend,registry,key,enabled,context,deadline,false).await
 }
 /// A saved-feed Load already reconciles subscriptions. Do not enable them a second time.
 pub(super) async fn change_live(&self,backend:&Backend,registry:&Registry,key:&str,enabled:bool,context:&BusinessContext,deadline:tokio::time::Instant,reconciled:bool)->Result<(),String>{
  let owner=self.owner(key)?;let scope;let chart_needed;
  {let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;state.context=context.clone();if enabled&&!state.can_live(){return Err("Live feed is unavailable for this trade or API session".into())}if state.starting{return Err("A live-feed change is already running".into())}scope=state.scope;chart_needed=state.plan_date!=context.value_date||(state.chart.is_null()&&!state.chart_loading);state.detail_generation+=1;state.observations_loading=false;state.context=context.clone();state.starting=true;state.collecting=enabled;if enabled{if state.plan_date!=context.value_date{state.chart=Json::Null;}state.plan_date=context.value_date;state.live_plans.clear();state.realtime=crate::state::monitor_cache::Realtime::new(scope,context.value_date,&state.trade)?;}}
  let _guard=StartGuard{hub:self.clone(),data:owner.clone(),incarnation:owner.lock().map_err(|_|"Monitor lock failed")?.incarnation};
  self.publish()?;
  if enabled&&chart_needed{self.refresh_chart(backend,registry,key,deadline).await?;}
  let result=if reconciled{Ok(true)}else{measure("live_feed_reconcile",self.feed(key)?.invoke(if enabled{Action::Enable}else{Action::Disable},scope,context,uuid::Uuid::new_v4(),deadline)).await};
  match result{
   Ok(true)=>{let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;state.live=enabled;state.starting=false;state.collecting=enabled;state.status.clear();if !enabled{state.live_plans.clear();}},
   Ok(false)|Err(_)=>{let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;state.starting=false;state.collecting=false;state.status=match result{Err(error)=>format!("Live-feed intent is unconfirmed: {error}"),_=>"Live-feed change was not acknowledged by the API".into()};let message=state.status.clone();drop(state);self.publish()?;return Err(message)},
  }
  if enabled{match measure("initial_realtime_snapshot",api::realtime(backend,registry,scope,context.value_date,deadline)).await{
   Ok(Some(snapshot))=>{owner.lock().map_err(|_|"Monitor lock failed")?.snapshot(snapshot);},Ok(None)=>{},Err(error)=>owner.lock().map_err(|_|"Monitor lock failed")?.status=format!("Live feed ON - waiting for an authoritative snapshot: {error}"),
  }}
  else{self.refresh_chart(backend,registry,key,deadline).await?;self.plans_page(backend,registry,key,0,100,true,deadline).await?;}
  self.publish()
 }
 pub async fn execute(&self,backend:&Backend,registry:&Registry,intent:&Json,context:&BusinessContext,deadline:tokio::time::Instant)->Result<String,String>{
  let action=intent["intent"].as_str().ok_or("Missing monitor action")?;let field=|key:&str|intent["fields"][key].as_str().unwrap_or_default();
  match action{
   "activate-trade"=>self.activate(field("tradeTabId"))?,"close-trade"=>self.close(field("tradeTabId"))?,
   "position"=>{let key=self.active()?;self.select_position(backend,registry,&key,field("index").parse().map_err(|_|"Select a saved position")?,deadline).await?;},
   "plans-page"=>{let key=self.active()?;if field("tradeTabId")!=key{return Err("The trade tab changed".into())}self.plans_page(backend,registry,&key,field("offset").parse().map_err(|_|"Invalid plan offset")?,field("pageSize").parse().map_err(|_|"Invalid plan page size")?,field("refresh")=="true",deadline).await?;},
   "live-on"|"live-off"=>{let key=self.active()?;self.set_live(backend,registry,&key,action=="live-on",context,deadline).await?;},
   _=>return Err("Unsupported monitor action".into()),
  }Ok("Trade monitor updated".into())
 }
}
