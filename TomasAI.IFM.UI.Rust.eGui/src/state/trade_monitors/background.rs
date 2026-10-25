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
                let(plan,reads)=tokio::join!(plan,legs);let plan=plan?;let mut selected=selected;
                if let Some(plan)=&plan{if let Some(enriched)=crate::services::saved_position::enrich(&selected,&plan["Position"]){selected=enriched;}}
                let mut observations=BTreeMap::new();
                let mut remember=|quote:Json|{if quote["ValueDate"]!=date.to_string(){return}if let Some(contract)=quote["ContractId"].as_str().filter(|c|contracts.iter().any(|expected|expected==c)){
                    let stamp=|q:&Json|q["ObservedAtUtc"].as_str().and_then(|s|chrono::DateTime::parse_from_rfc3339(s).ok());
                    if observations.get(contract).is_none_or(|old|stamp(&quote)>stamp(old)){observations.insert(contract.to_string(),quote);}
                }};
                if let Some(legs)=selected["Legs"].as_array(){for leg in legs{if !leg["OptionTick"].is_null(){remember(leg["OptionTick"].clone());}}}
                for quote in reads{if let Some(quote)=quote?{remember(quote);}}
                Ok::<_,String>((selected,plan,observations))
            }.await;
            let outcome=result.as_ref().map(|_|()).map_err(|_|());
            if let Some(state)=weak.upgrade(){let hub=Hub{state,updates,host_scope};
                if let Ok(current)=hub.owner(&key_owned){if Arc::ptr_eq(&current,&data){
                    let changed={let mut state=data.lock().map_err(|_|())?;
                        if state.incarnation!=incarnation||state.detail_generation!=generation||state.plan_date!=date||state.live||state.starting{false}
                        else{state.observations_loading=false;match result{Ok((selected,plan,observations))=>{
                            if let Some(plan)=plan{state.history.plan(&plan);}state.selected=Some(selected);state.observations=observations;state.observations_error.clear();
                        },Err(error)=>state.observations_error=error}true}};
                    if changed{let _=hub.publish();}
                }}
            }outcome
        }));
        self.install_background(key,"details",&identity,job)
    }
}
