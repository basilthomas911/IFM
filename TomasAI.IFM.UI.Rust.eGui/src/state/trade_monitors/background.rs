//! Optional read hydration. Each slot owns one cancellable job. Results are
//! fenced by tab incarnation and request generation, not merely by ValueDate.
use super::*;
use crate::diagnostics::trade_load::{measure,background,current_id};
impl Hub {
    fn install_background(&self,key:&str,slot:&'static str,identity:&Arc<Mutex<data::Monitor>>,job:tokio::task::JoinHandle<Result<(),()>>) -> Result<(),String> {
        let mut state=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?;
        let Some(owner)=state.owners.get_mut(key) else {job.abort();return Ok(())};
        if !Arc::ptr_eq(&owner.data,identity){job.abort();return Ok(())}
        if let Some(old)=owner.background.insert(slot,job){old.abort();} Ok(())
    }
    /// Host connectivity is only required for live control, never for stored trade reads.
    pub(super) async fn connect_feed(&self,backend:&Backend,key:&str,deadline:tokio::time::Instant)->Result<(),String>{
        let identity=self.owner(key)?;
        let connection=if let Some(url)=crate::backend::configuration::value("IFM_UI_MARKETDATA_NATS_URL")?{
            let mut settings=crate::backend::transport::Settings::from_env()?;settings.url=url;settings.request_timeout=backend.default_timeout();
            measure("host_connection",tokio::time::timeout_at(deadline,Backend::connect(settings))).await.map_err(|_|"MarketData Host connection deadline expired")??
        }else{backend.clone()};
        let mut state=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?;
        let owner=state.owners.get_mut(key).filter(|owner|Arc::ptr_eq(&owner.data,&identity)).ok_or("Trade closed during feed connection")?;
        owner.feed=Client::new(connection,&self.host_scope)?;Ok(())
    }
    pub(super) fn cancel_saved_feed(&self,key:&str)->Result<(),String>{
        let mut state=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?;
        if let Some(owner)=state.owners.get_mut(key){if let Some(job)=owner.background.remove("saved_feed"){job.abort();}}
        Ok(())
    }
    /// Phase two starts only after the stored view has been published and its
    /// optional database hydration has finished. Weak ownership and incarnation
    /// checks prevent a closed/reopened tab receiving this task's late result.
    pub(super) fn schedule_saved_feed(&self,backend:&Backend,key:&str)->Result<(),String>{
        let data=self.owner(key)?;let identity=data.clone();
        let weak=Arc::downgrade(&self.state);let updates=self.updates.clone();let host_scope=self.host_scope.clone();
        let backend=backend.clone();let key=key.to_string();let mut changes=self.watch();let parent=current_id();
        let job=tokio::spawn(background(json!({"task":"saved_feed_restore","trade":key,"parent_load_id":parent}),async move{
            loop{
                let ready={let state=data.lock().map_err(|_|())?;!state.chart_loading&&!state.observations_loading};
                if ready{break}changes.changed().await.map_err(|_|())?;
            }
            let Some(state)=weak.upgrade() else{return Ok(())};let hub=Hub{state,updates,host_scope};
            if !hub.owner(&key).is_ok_and(|current|Arc::ptr_eq(&current,&data)){return Ok(())}
            let (scope,can_live)={let state=data.lock().map_err(|_|())?;(state.scope,state.can_live())};
            if !can_live{return Ok(())}
            {data.lock().map_err(|_|())?.status="Stored trade loaded; checking saved live-feed intent".into();}let _=hub.publish();
            let result=async{
                // Hydration can outlast the initial API clock observation. Refresh
                // both the authoritative ValueDate and absolute command deadline.
                let context=measure("restore_business_context",BusinessContext::load(&backend)).await?;
                {let mut state=data.lock().map_err(|_|"Monitor lock failed")?;
                    if context.revision<state.context.revision{return Err("API session changed during stored trade loading".into())}
                    state.context=context.clone();if !state.can_live(){state.status.clear();return Ok(())}
                }
                let deadline=tokio::time::Instant::now()+backend.default_timeout();
                hub.connect_feed(&backend,&key,deadline).await?;
                let enabled=measure("saved_live_feed_intent",hub.feed(&key)?.invoke(Action::Load,scope,&context,uuid::Uuid::new_v4(),deadline)).await?;
                if enabled{
                    let registry=hub.state.lock().map_err(|_|"Monitor lifetime lock failed")?.owners.get(&key).ok_or("Trade closed")?.registry.clone();
                    hub.change_live(&backend,&registry,&key,true,&context,deadline,true).await?;
                }else{data.lock().map_err(|_|"Monitor lock failed")?.status.clear();}
                Ok::<_,String>(())
            }.await;
            let outcome=result.as_ref().map(|_|()).map_err(|_|());
            if let Err(error)=result{data.lock().map_err(|_|())?.status=format!("Stored trade loaded. Live-feed restoration failed: {error}");}
            let _=hub.publish();outcome
        }));
        let key=identity.lock().map_err(|_|"Monitor lock failed")?.scope.key()?;
        self.install_background(&key,"saved_feed",&identity,job)
    }
    pub(super) fn schedule_chart(&self,backend:&Backend,key:&str)->Result<(),String> {
        let data=self.owner(key)?;
        let (scope,date,incarnation,generation)={let mut state=data.lock().map_err(|_|"Monitor lock failed")?;
            state.chart_generation+=1;state.chart_loading=true;state.chart_error.clear();
            (state.scope,state.plan_date,state.incarnation,state.chart_generation)};
        let registry=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?.owners.get(key).ok_or("Trade closed during background read")?.registry.clone();
        let weak=Arc::downgrade(&self.state);let updates=self.updates.clone();let host_scope=self.host_scope.clone();
        let backend=backend.clone();let key_owned=key.to_string();let parent=current_id();let identity=data.clone();
        self.publish()?;
        let job=tokio::spawn(background(json!({"task":"chart","trade":key,"parent_load_id":parent}),async move {
            let deadline=tokio::time::Instant::now()+backend.default_timeout();
            let result=measure("six_hour_chart",api::chart(&backend,&registry,scope,date,deadline)).await;
            let outcome=result.as_ref().map(|_|()).map_err(|_|());
            if let Some(state)=weak.upgrade(){
                let hub=Hub{state,updates,host_scope};
                if let Ok(current)=hub.owner(&key_owned){if Arc::ptr_eq(&current,&data){
                    let changed={let mut state=data.lock().map_err(|_|())?;
                        if state.incarnation!=incarnation||state.plan_date!=date||state.chart_generation!=generation{false}
                        else{state.chart_loading=false;match result{Ok(chart)=>{state.chart=chart;state.chart_error.clear()},Err(error)=>state.chart_error=error}true}};
                    if changed{let _=hub.publish();}
                }}
            } outcome
        }));
        self.install_background(key,"chart",&identity,job)
    }
    pub(super) fn schedule_details(&self,backend:&Backend,key:&str)->Result<(),String> {
        let data=self.owner(key)?;
        let(scope,date,incarnation,generation,selected,trade)={let state=data.lock().map_err(|_|"Monitor lock failed")?;
            (state.scope,state.plan_date,state.incarnation,state.detail_generation,state.selected.clone().ok_or("No selected position")?,state.trade.clone())};
        let registry=self.state.lock().map_err(|_|"Monitor lifetime lock failed")?.owners.get(key).ok_or("Trade closed during background read")?.registry.clone();
        let weak=Arc::downgrade(&self.state);let updates=self.updates.clone();let host_scope=self.host_scope.clone();
        let backend=backend.clone();let key_owned=key.to_string();let parent=current_id();let identity=data.clone();
        let job=tokio::spawn(background(json!({"task":"saved_position_details","trade":key,"parent_load_id":parent}),async move {
            let result=async {
                let deadline=tokio::time::Instant::now()+backend.default_timeout();
                let contracts=trade["Legs"].as_array().ok_or("Missing established legs")?.iter().map(|leg|leg["ContractId"].as_str().map(str::to_string).ok_or("Missing saved contract")).collect::<Result<Vec<_>,_>>()?;
                let plan=async{if !["Open","Close"].contains(&selected["Phase"].as_str().unwrap_or_default()){
                    measure("selected_position_plan",api::current_plan(&backend,&registry,scope,date,deadline)).await
                }else{Ok(None)}};
                let legs=futures_util::future::join_all(contracts.iter().map(|contract|measure("saved_option_leg",api::saved_leg(&backend,&registry,contract,date,deadline))));
                let(plan,reads,page)=tokio::join!(plan,legs,api::plan_page(&backend,&registry,scope,date,0,100,None,deadline));let plan=plan?;let mut selected=selected;
                if let Some(plan)=&plan{if let Some(enriched)=crate::services::saved_position::enrich(&selected,&plan["Position"]){selected=enriched;}}
                let mut observations=BTreeMap::new();
                let mut remember=|quote:Json|{if quote["ValueDate"]!=date.to_string(){return}if let Some(contract)=quote["ContractId"].as_str().filter(|c|contracts.iter().any(|expected|expected==c)){
                    let stamp=|q:&Json|q["ObservedAtUtc"].as_str().and_then(|s|chrono::DateTime::parse_from_rfc3339(s).ok());
                    if observations.get(contract).is_none_or(|old|stamp(&quote)>stamp(old)){observations.insert(contract.to_string(),quote);}
                }};
                if let Some(legs)=selected["Legs"].as_array(){for leg in legs{if !leg["OptionTick"].is_null(){remember(leg["OptionTick"].clone());}}}
                for quote in reads{if let Some(quote)=quote?{remember(quote);}}
                Ok::<_,String>((selected,plan,observations,page))
            }.await;
            let outcome=result.as_ref().map(|_|()).map_err(|_|());
            if let Some(state)=weak.upgrade(){let hub=Hub{state,updates,host_scope};
                if let Ok(current)=hub.owner(&key_owned){if Arc::ptr_eq(&current,&data){
                    let changed={let mut state=data.lock().map_err(|_|())?;
                        if state.incarnation!=incarnation||state.detail_generation!=generation||state.plan_date!=date||state.live||state.starting{false}
                        else{state.observations_loading=false;match result{Ok((selected,plan,observations,page))=>{
                            match page{Ok(page)=>{state.page=Some(page);state.plan_error.clear()},Err(error)=>state.plan_error=error}
                            if let Some(plan)=plan{state.history.plan(&plan);}state.selected=Some(selected);state.observations=observations;state.observations_error.clear();
                        },Err(error)=>state.observations_error=error}true}};
                    if changed{let _=hub.publish();}
                }}
            }outcome
        }));
        self.install_background(key,"details",&identity,job)
    }
}
