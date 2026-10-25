use super::*;
impl Hub{
 pub async fn open(&self,backend:&Backend,registry:Arc<Registry>,request:OpenRequest,context:BusinessContext,deadline:tokio::time::Instant)->Result<String,String>{
  let key=request.scope.key()?;
  if self.owner(&key).is_ok(){self.activate(&key)?;return Ok("Trade activated".into())}
  let trade=api::established(backend,&registry,request.scope,&request.strategy,deadline).await?;
  let host_backend=if let Some(url)=crate::backend::configuration::value("IFM_UI_MARKETDATA_NATS_URL")?{
   let mut settings=crate::backend::transport::Settings::from_env()?;settings.url=url;settings.request_timeout=backend.default_timeout();
   tokio::time::timeout_at(deadline,Backend::connect(settings)).await.map_err(|_|"MarketData Host connection deadline expired")??
  }else{backend.clone()};
  let feed=Client::new(host_backend,&self.host_scope)?;
  let data=Arc::new(Mutex::new(data::Monitor::new(request,trade,context.clone())?));let incarnation=data.lock().map_err(|_|"Monitor lock failed")?.incarnation;
  {let mut state=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?;state.order.push(key.clone());state.owners.insert(key.clone(),Owner{feed,data:data.clone(),jobs:vec![]});state.active=Some(key.clone());}
  let mut guard=OpenGuard{hub:self.clone(),key:key.clone(),incarnation,armed:true};
  let result=async{
   let jobs=self.start_events(backend.clone(),registry.clone(),&key,data.clone(),deadline).await?;
   {let mut state=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?;state.owners.get_mut(&key).ok_or("Trade closed during event attachment")?.jobs=jobs;}
   let(scope,iron)={let state=data.lock().map_err(|_|"Monitor lock failed")?;(state.scope,state.iron())};
   if iron{
    let(history,limits)=tokio::join!(api::positions(backend,&registry,scope,&context,deadline),api::limits(backend,&registry,scope,deadline));
    {let mut state=data.lock().map_err(|_|"Monitor lock failed")?;for position in history?{state.history.remember(position);}state.limits=limits?;}
    // Balance availability never hides persisted risk limits.
    let balance=crate::services::financial::query(backend,&registry,&context,"GetAccountBalancesQuery",scope.portfolio,Some(scope.fund),rmpv::Value::Array(vec![]),crate::services::financial::AccessScope::MetricsReader,deadline).await;
    if let Ok(read)=balance{if let Some(value)=read.value{let value=crate::services::parameters::document(&registry,"TomasAI.IFM.Domain.Portfolio.Shared.Financial.FinancialBalanceSnapshot",&value,true)?;data.lock().map_err(|_|"Monitor lock failed")?.balance=value["AvailableCash"].clone();}}
    let count=data.lock().map_err(|_|"Monitor lock failed")?.history.rows().len();if count>0{self.select_position(backend,&registry,&key,count-1,deadline).await?;}else{self.refresh_chart(backend,&registry,&key,deadline).await?;}
    if data.lock().map_err(|_|"Monitor lock failed")?.can_live(){
     if self.feed(&key)?.invoke(Action::Load,scope,&context,uuid::Uuid::new_v4(),deadline).await?{self.set_live(backend,&registry,&key,true,&context,deadline).await?;}
    }
   }
   Ok::<_,String>(())
  }.await;
  if let Err(error)=result{
   // Only this incarnation is removed. A later open of the same trade cannot
   // be detached by a late reply belonging to this failed load.
   let same=self.owner(&key).ok().and_then(|owner|owner.lock().ok().map(|owner|owner.incarnation==incarnation)).unwrap_or(false);if same{self.close(&key)?;}return Err(error)
  }
  guard.armed=false;self.publish()?;Ok("Trade loaded".into())
 }
 pub async fn refresh_chart(&self,backend:&Backend,registry:&Registry,key:&str,deadline:tokio::time::Instant)->Result<(),String>{
  let owner=self.owner(key)?;let(scope,date,incarnation)={let state=owner.lock().map_err(|_|"Monitor lock failed")?;(state.scope,state.plan_date,state.incarnation)};
  let result=api::chart(backend,registry,scope,date,deadline).await;
  {let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;if state.incarnation!=incarnation||state.plan_date!=date{return Ok(())}match result{Ok(chart)=>{state.chart=chart;state.chart_error.clear()},Err(error)=>state.chart_error=error}}
  self.publish()
 }
 pub async fn select_position(&self,backend:&Backend,registry:&Registry,key:&str,index:usize,deadline:tokio::time::Instant)->Result<(),String>{
  let owner=self.owner(key)?;
  let(scope,mut selected,trade,incarnation)={let state=owner.lock().map_err(|_|"Monitor lock failed")?;if state.live||state.starting{return Err("Position selection is unavailable while live".into())}(state.scope,state.history.saved(index).ok_or("Select a saved position")?,state.trade.clone(),state.incarnation)};
  let date=crate::backend::date_input::parse(selected["ValueDate"].as_str().ok_or("Missing saved position date")?)?;
  let plan=if !["Open","Close"].contains(&selected["Phase"].as_str().unwrap_or_default()){api::current_plan(backend,registry,scope,date,deadline).await?}else{None};
  if let Some(plan)=&plan{if let Some(enriched)=crate::services::saved_position::enrich(&selected,&plan["Position"]){selected=enriched;}}
  let contracts=trade["Legs"].as_array().ok_or("Missing established legs")?.iter().map(|leg|leg["ContractId"].as_str().map(str::to_string).ok_or("Missing saved contract")).collect::<Result<Vec<_>,_>>()?;
  let reads=futures_util::future::join_all(contracts.iter().map(|contract|api::saved_leg(backend,registry,contract,date,deadline))).await;
  let mut observations=BTreeMap::new();
  let mut remember=|quote:Json|{if quote["ValueDate"]!=date.to_string(){return}if let Some(contract)=quote["ContractId"].as_str().filter(|c|contracts.iter().any(|expected|expected==c)){let replace=observations.get(contract).is_none_or(|old:&Json|quote["ObservedAtUtc"].as_str().and_then(|s|chrono::DateTime::parse_from_rfc3339(s).ok())>old["ObservedAtUtc"].as_str().and_then(|s|chrono::DateTime::parse_from_rfc3339(s).ok()));if replace{observations.insert(contract.to_string(),quote);}}};
  if let Some(legs)=selected["Legs"].as_array(){for leg in legs{if !leg["OptionTick"].is_null(){remember(leg["OptionTick"].clone());}}}
  for quote in reads{if let Some(quote)=quote?{remember(quote);}}
  {let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;if state.incarnation!=incarnation||state.live||state.starting{return Ok(())}if let Some(plan)=plan{state.history.plan(&plan);}state.selected=Some(selected);state.observations=observations;state.plan_date=date;state.page=None;}
  self.refresh_chart(backend,registry,key,deadline).await?;self.publish()
 }
 pub async fn plans_page(&self,backend:&Backend,registry:&Registry,key:&str,offset:i32,size:i32,refresh:bool,deadline:tokio::time::Instant)->Result<(),String>{
  let owner=self.owner(key)?;let(scope,date,maximum,incarnation)={let state=owner.lock().map_err(|_|"Monitor lock failed")?;if state.live{return Ok(())}(state.scope,state.plan_date,if refresh{None}else{state.page.as_ref().and_then(|p|p["MaximumRevision"].as_i64())},state.incarnation)};
  let result=api::plan_page(backend,registry,scope,date,offset,size,maximum,deadline).await;
  {let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;if state.incarnation!=incarnation||state.live||state.plan_date!=date{return Ok(())}match result{Ok(page)=>{state.page=Some(page);state.plan_error.clear()},Err(error)=>state.plan_error=error}}
  self.publish()
 }
 pub async fn set_live(&self,backend:&Backend,registry:&Registry,key:&str,enabled:bool,context:&BusinessContext,deadline:tokio::time::Instant)->Result<(),String>{
  let owner=self.owner(key)?;let scope;
  {let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;state.context=context.clone();if enabled&&!state.can_live(){return Err("Live feed is unavailable for this trade or API session".into())}if state.starting{return Err("A live-feed change is already running".into())}scope=state.scope;state.context=context.clone();state.starting=true;state.collecting=enabled;if enabled{state.plan_date=context.value_date;state.page=None;state.live_plans.clear();state.realtime=crate::state::monitor_cache::Realtime::new(scope,context.value_date,&state.trade)?;}}
  let _guard=StartGuard{hub:self.clone(),data:owner.clone(),incarnation:owner.lock().map_err(|_|"Monitor lock failed")?.incarnation};
  self.publish()?;
  if enabled{self.refresh_chart(backend,registry,key,deadline).await?;}
  let result=self.feed(key)?.invoke(if enabled{Action::Enable}else{Action::Disable},scope,context,uuid::Uuid::new_v4(),deadline).await;
  match result{
   Ok(true)=>{let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;state.live=enabled;state.starting=false;state.collecting=enabled;state.status.clear();if !enabled{state.live_plans.clear();}},
   Ok(false)|Err(_)=>{let mut state=owner.lock().map_err(|_|"Monitor lock failed")?;state.starting=false;state.collecting=false;state.status=match result{Err(error)=>format!("Live-feed intent is unconfirmed: {error}"),_=>"Live-feed change was not acknowledged by the API".into()};let message=state.status.clone();drop(state);self.publish()?;return Err(message)},
  }
  if enabled{match api::realtime(backend,registry,scope,context.value_date,deadline).await{
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
