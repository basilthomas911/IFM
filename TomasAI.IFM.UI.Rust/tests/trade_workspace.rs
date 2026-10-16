use egui_kittest::{Harness,kittest::Queryable};
use ifm_ui_rust::{trade_workspace::TradeWorkspace,contract_editor::ContractPublication};
struct State{view:TradeWorkspace,requests:Vec<String>}
fn fixture(count:usize)->ContractPublication{
 let mut rows:Vec<_>=(0..count).map(|i|serde_json::json!({"id":format!("trade:{i}"),"fields":{"kind":"trade","data":serde_json::json!({"index":i,"tradeId":i,"tradeType":"ShortIronCondor","tradeState":"NewTrade","effectiveDate":"2026-10-13"}).to_string()}})).collect();
 if count>0{rows.push(serde_json::json!({"id":"order:27","fields":{"kind":"order","data":serde_json::json!({"orderId":27,"source":"Manual"}).to_string()}}));}
 for row in &mut rows { if let Some(document)=row["fields"].as_object_mut().unwrap().remove("data") {
    let value:serde_json::Value=serde_json::from_str(document.as_str().unwrap()).unwrap();
    row["data"]=serde_json::to_value(ifm_ui_rust::wire::encode_value(&value).unwrap()).unwrap();
 } }
 serde_json::from_value(serde_json::json!({"schema":1,"generation":3,"busy":false,"status":"Completed","outcomes":[],"result":{"view":"trade-orders","rows":rows,"choices":{"portfolio":["1 - Development"],"fund":["2 - Test fund"]},"hasMore":false,"context":{"portfolioIndex":"0","fundIndex":"0","orderId":"27","valueDate":"2026-10-13","canAddTrade":"True","canCreateOrder":"True","canLoadTrade":"False"}}})).unwrap()
}
#[test]fn editor_matches_order_workspace_and_new_trade_cannot_load(){
 let s=fixture(1);let mut v=TradeWorkspace::default();v.open=true;
 let mut h=Harness::new_state(move|ctx,state:&mut State|{if let Some(request)=state.view.draw(ctx,&s){state.requests.push(request);}},State{view:v,requests:vec![]});h.set_size(eframe::egui::vec2(1500.,1000.));h.run();
 for title in ["Portfolio:","Fund:","Trade Orders","Trade Id","Trade Type","Effective Date","Trade State","Add Trade...","Load Trade"]{assert!(h.query_by_label(title).is_some(),"Missing {title}");}
 h.get_by_label("Load Trade").click();h.run();assert!(h.state().requests.is_empty());
 h.get_by_label("Add Trade...").click();h.run();assert!(h.query_by_label("Add Trade").is_some());assert!(h.query_by_label("Trade Date:").is_none());assert!(h.query_by_label("Maturity Date:").is_none());h.get_by_label("Cancel").click();h.run();assert_eq!(h.state().requests.len(),2);assert!(h.state().requests[0].contains("prepare-trade"));
}
#[test]fn trade_history_only_draws_viewport_rows(){let s=fixture(100000);let mut v=TradeWorkspace::default();v.open=true;let mut h=Harness::new_state(move|ctx,state:&mut State|{let _=state.view.draw(ctx,&s);},State{view:v,requests:vec![]});h.set_size(eframe::egui::vec2(1500.,1000.));h.run();assert!(h.state().view.drawn_rows>0);assert!(h.state().view.drawn_rows<50);}

#[test]
fn editor_fits_central_trade_pane_after_shell_panels(){
 let s=fixture(1);let mut v=TradeWorkspace::default();v.open=true;
 let mut h=Harness::new_state(move|ctx,state:&mut State|{
  eframe::egui::TopBottomPanel::top("test-menu").exact_height(27.).show(ctx,|_|{});
  eframe::egui::TopBottomPanel::bottom("test-status").exact_height(24.).show(ctx,|_|{});
  eframe::egui::SidePanel::left("test-left").exact_width(300.).show(ctx,|_|{});
  eframe::egui::SidePanel::right("test-right").exact_width(300.).show(ctx,|_|{});
  let _=state.view.draw(ctx,&s);
 },State{view:v,requests:vec![]});
 h.set_size(eframe::egui::vec2(1800.,1200.));h.run();
 let bounds=h.state().view.bounds.unwrap();
 assert!((bounds.left()-300.).abs()<8.,"{bounds:?}");
 assert!((bounds.width()-1200.).abs()<20.,"{bounds:?}");
 assert!(bounds.top()>=26.&&bounds.bottom()<=1178.,"{bounds:?}");
 assert!(bounds.height()>1050.,"{bounds:?}");
}

#[test]
fn order_date_ranges_use_calendar_dropdowns(){
 let s=fixture(1);let mut v=TradeWorkspace::default();v.open=true;
 let mut h=Harness::new_state(move|ctx,state:&mut State|{if let Some(request)=state.view.draw(ctx,&s){state.requests.push(request);}},State{view:v,requests:vec![]});h.set_size(eframe::egui::vec2(1800.,1200.));h.run();
 h.get_by_label("From calendar").click();h.run();assert!(h.query_by_label("Mon").is_some()||h.query_by_label("Mo").is_some());
 h.get_by_label("15").click();h.run();h.get_by_label("Save").click();h.run();let request:serde_json::Value=serde_json::from_str(h.state().requests.last().unwrap()).unwrap();assert_eq!(request["intent"],"range");assert_eq!(request["fields"]["from"],"2026-10-15");assert_eq!(request["fields"]["to"],"2026-10-31");
}

#[test]
fn empty_editor_has_only_create_order_enabled_and_no_completed_label(){
 let mut s=fixture(0);s.result.context.insert("orderId".into(),String::new());s.result.context.insert("canCreateOrder".into(),"False".into());
 let mut v=TradeWorkspace::default();v.open=true;let mut h=Harness::new_state(move|ctx,state:&mut State|{if let Some(request)=state.view.draw(ctx,&s){state.requests.push(request);}},State{view:v,requests:vec![]});h.set_size(eframe::egui::vec2(1800.,1200.));h.run();assert!(h.query_by_label("Completed").is_none());
 for label in ["Delete Order","Load Trade","Add Trade...","Remove Trade"]{h.get_by_label(label).click();h.run();assert!(h.state().requests.is_empty());assert!(h.query_by_label("Remove the selected record?").is_none());}
 h.get_by_label("Create Order...").click();h.run();assert!(h.state().requests[0].contains("prepare-order"));
}
