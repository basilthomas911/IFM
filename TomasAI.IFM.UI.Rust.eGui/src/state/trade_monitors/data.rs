use super::*;
use crate::state::monitor_cache::{History,Realtime};
pub(super) struct Monitor{
 pub incarnation:uuid::Uuid,pub scope:Scope,pub trade:Json,pub trade_type:String,pub trade_action:String,pub fund_name:String,pub context:BusinessContext,
 pub history:History,pub realtime:Realtime,pub selected:Option<Json>,pub observations:BTreeMap<String,Json>,pub limits:Json,pub balance:Json,
 pub live:bool,pub collecting:bool,pub starting:bool,pub market_stopped:bool,pub cleanup_unconfirmed:bool,pub status:String,pub last_feed_at:Option<chrono::DateTime<chrono::Utc>>,
 pub detail_generation:u64,pub chart_generation:u64,pub chart_loading:bool,pub observations_loading:bool,pub observations_error:String,
 pub plan_date:chrono::NaiveDate,pub page:Option<Json>,pub live_plans:BTreeMap<(i64,i64),Json>,pub chart:Option<crate::models::trade_chart::TradeChartSeries>,pub chart_error:String,pub plan_error:String,
}
fn flag(value:bool)->String{if value{"True"}else{"False"}.into()}
impl Monitor{
 pub fn new(request:OpenRequest,trade:Json,context:BusinessContext)->Result<Self,String>{
  let realtime=Realtime::new(request.scope,context.value_date,&trade)?;let history=History::new(trade.clone(),request.trade_type.clone());
  Ok(Self{incarnation:uuid::Uuid::new_v4(),scope:request.scope,trade,trade_type:request.trade_type,trade_action:request.trade_action,fund_name:request.fund_name,plan_date:context.value_date,context,history,realtime,detail_generation:0,chart_generation:0,chart_loading:false,observations_loading:false,observations_error:String::new(),selected:None,observations:BTreeMap::new(),limits:Json::Null,balance:Json::Null,live:false,collecting:false,starting:false,market_stopped:false,cleanup_unconfirmed:false,status:String::new(),last_feed_at:None,page:None,live_plans:BTreeMap::new(),chart:None,chart_error:String::new(),plan_error:String::new()})
 }
 pub fn iron(&self)->bool{self.trade["StrategyKind"]=="IronCondor"}
 pub fn can_live(&self)->bool{self.iron()&&!self.starting&&!self.market_stopped&&!self.cleanup_unconfirmed&&self.trade["Status"]=="Open"&&(self.context.market_state==2||self.context.allow_development_emulator)}
 pub fn snapshot(&mut self,value:Json)->bool{
  if !value["Position"].is_null()&&value["Position"]["Id"]!=value["PositionId"]{return false}
  if !self.realtime.accept(value.clone()){return false}
  // A newly accepted authoritative snapshot resolves this specific pending
  // read warning. Never clear a terminal/cleanup error from an unrelated event.
  if self.live&&self.status.starts_with("Live feed ON - waiting for an authoritative snapshot:"){self.status.clear();}
  if !value["Position"].is_null(){self.history.remember(value["Position"].clone());}
  if !value["Plan"].is_null(){self.plan(value["Plan"].clone());}true
 }
 pub fn position(&mut self,value:Json)->bool{if self.scope.position("IronCondor").map_or(true,|(_,id)|value["Id"]!=id){return false}let changed=self.history.remember(value.clone());if changed&&!self.live&&value["Phase"]=="EndOfDay"{self.detail_generation+=1;self.observations_loading=false;self.selected=Some(value);}changed}
 pub fn plan(&mut self,value:Json)->bool{
  if self.scope.position("IronCondor").map_or(true,|(_,id)|value["Position"]["Id"]!=id)||value["ValueDate"]!=self.context.value_date.to_string(){return false}
  self.history.plan(&value);
  if !(self.live||self.collecting){return false}
  let Some(key)=value["Position"]["RouteGeneration"].as_i64().zip(value["PlanRevision"].as_i64())else{return false};
  if self.live_plans.get(&key)==Some(&value){return false}
  self.live_plans.insert(key,value);while self.live_plans.len()>100{self.live_plans.pop_first();}true
 }
 pub fn append(&self,result:&mut ContractResult)->Result<(),String>{
  result.rows.push(row("established",&self.scope.key()?,display::document(&self.trade))?);
  if self.iron(){
   if self.live{if let Some(value)=&self.realtime.current{result.rows.push(row("realtime","0",display::document(value))?);}}
   for(index,value)in self.history.rows().into_iter().enumerate(){result.rows.push(row("position",&index.to_string(),value)?);}
   let plans=if self.live&&!self.live_plans.is_empty(){self.live_plans.values().rev().collect::<Vec<_>>()}else{self.page.as_ref().and_then(|page|page["Items"].as_array()).map(|rows|rows.iter().collect()).unwrap_or_default()};
   for(index,plan)in plans.iter().enumerate(){let mut entry=row("plan",&index.to_string(),display::plan_history(plan))?;entry.fields.insert("actionDateTimeEastern".into(),display::action_time(plan)?);result.rows.push(entry);}
   result.rows.push(row("limits","0",json!({"orderId":self.scope.order,"tradeLimit":display::document(&self.limits),"fundBalance":self.balance}))?);
   result.rows.push(typed_row("chart","0",&crate::models::trade_chart::TradeChartRow::from(self.chart.as_ref()))?);
   let position=if self.live{self.realtime.current.as_ref().map(|v|&v["Position"]).or(self.selected.as_ref())}else{self.selected.as_ref()};
   result.rows.push(row("position-details","0",position.map(display::document).unwrap_or(Json::Null))?);
   if !self.live||self.realtime.current.is_none(){for(index,(key,value))in self.observations.iter().enumerate(){result.rows.push(row("observation",&index.to_string(),json!({"key":key,"value":display::document(value)}))?);}}
  }
  let maximum=self.page.as_ref().map(|p|text(&p["MaximumRevision"])).unwrap_or("0".into());
  let latest=self.live_plans.keys().next_back().map(|(_,revision)|revision.to_string()).unwrap_or("0".into());
  for(key,value)in[("latestPlanRevision",if self.live{latest}else{"0".into()}),("planMaximumRevision",maximum),("planOffset",self.page.as_ref().map(|p|text(&p["Offset"])).unwrap_or("0".into())),("planTotal",self.page.as_ref().map(|p|text(&p["TotalCount"])).unwrap_or("1".into())),("planLoaded",flag(self.live||self.page.is_some())),("planDate",self.plan_date.to_string()),("chartError",self.chart_error.clone()),("chartLoading",flag(self.chart_loading)),("observationsLoading",flag(self.observations_loading)),("observationsError",self.observations_error.clone()),("planError",self.plan_error.clone()),("monitorStatus",self.status.clone()),("monitorLive",flag(self.live)),("monitorCanLive",flag(self.can_live())),("monitorCanSelectPosition",flag(!self.starting&&!self.live)),("activeTradeTab",self.scope.key()?),("fundName",self.fund_name.clone()),("compositionType",self.trade_type.clone()),("tradeAction",self.trade_action.clone()),("canLive",flag(self.can_live())),("live",flag(self.live)),("canSelectPosition",flag(!self.starting&&!self.live))]{result.context.insert(key.into(),value);}
  Ok(())
 }
}
