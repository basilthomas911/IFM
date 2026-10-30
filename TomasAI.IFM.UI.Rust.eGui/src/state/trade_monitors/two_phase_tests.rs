//! Stored financial data remains visible while a live source is being restored.
use super::*;
fn fixture(name:&str,kind:&str)->Json{
 let registry=Registry::compiled().unwrap();let path=std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join(format!("tests/fixtures/backend/{name}.msgpack"));
 display::named(&registry,kind,&crate::backend::codec::decode(&std::fs::read(path).unwrap()).unwrap()).unwrap()
}
fn stored()->data::Monitor{
 let scope=api::Scope{portfolio:1,fund:2,order:701,trade:456};let date=chrono::NaiveDate::from_ymd_opt(2026,10,26).unwrap();
 let context=BusinessContext{value_date:date,active_value_date:Some(date),as_of_utc:chrono::DateTime::parse_from_rfc3339("2026-10-26T12:30:00Z").unwrap().to_utc(),revision:1,market_state:2,allow_development_emulator:false};
 let request=OpenRequest{scope,strategy:"IronCondor".into(),trade_type:"ShortIronCondor".into(),trade_action:"Sell".into(),fund_name:"Fixture".into()};
 let mut state=data::Monitor::new(request,fixture("monitor-trade","TomasAI.IFM.Domain.Trade.Shared.EstablishedTradeDefinition"),context).unwrap();
 let positions=fixture("monitor-positions","TomasAI.IFM.Domain.Trade.Shared.StrategyPositionSnapshot[]");state.selected=Some(positions[1].clone());
 state.page=Some(json!({"Items":[fixture("monitor-plan","TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.StrategyTradePlanSnapshot")],"MaximumRevision":7,"Offset":0,"TotalCount":1}));state.chart=Some(crate::models::trade_chart::TradeChartSeries{end_utc:"2026-10-26T12:30:00Z".into(),..Default::default()});state
}
fn document(state:&data::Monitor,kind:&str)->Json{
 let mut result=ContractResult::default();state.append(&mut result).unwrap();crate::wire::decode(result.rows.iter().find(|r|r.fields["kind"]==kind).unwrap().data.as_ref().unwrap()).unwrap()
}
#[test]
fn starting_and_acknowledged_live_without_snapshot_keep_saved_position_and_plans(){
 let mut state=stored();let position=document(&state,"position-details");let plan=document(&state,"plan");let chart=document(&state,"chart");
 state.starting=true;state.collecting=true;
 assert_eq!(document(&state,"position-details"),position);assert_eq!(document(&state,"plan"),plan);
 state.starting=false;state.live=true;
 assert_eq!(document(&state,"position-details"),position);assert_eq!(document(&state,"plan"),plan);assert_eq!(document(&state,"chart"),chart);
}
#[test]
fn failed_live_restore_keeps_stored_data_and_allows_retry(){
 let mut state=stored();let position=document(&state,"position-details");state.status="Stored trade loaded. Live-feed restoration failed: synthetic timeout".into();
 assert!(state.can_live());assert_eq!(document(&state,"position-details"),position);assert!(!document(&state,"plan").is_null());
}

#[test]
fn authoritative_live_snapshot_replaces_saved_fallback(){
 let mut state=stored();let saved=document(&state,"position-details");state.live=true;
 assert!(state.snapshot(fixture("monitor-realtime","TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshot")));
 assert_ne!(document(&state,"position-details"),saved);assert!(!document(&state,"realtime").is_null());
}
