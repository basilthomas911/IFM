use ifm_ui_egui::{backend::{codec,contracts::ExactDecimal,schema::Registry},services::{broker_preview,broker_preview_display,option_draft::Selected}};
use serde_json::Value as Json;
#[test]
fn provisional_economics_display_matches_unchanged_hybrid_presenter(){
 let root=std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");let registry=Registry::compiled().unwrap();
 let cases:Json=serde_json::from_slice(&std::fs::read(root.join("broker-preview-display.json")).unwrap()).unwrap();
 for case in cases.as_array().unwrap(){
  let mut legs=vec![];
  for row in case["legs"].as_array().unwrap(){
   let decimal=|key:&str|if row[key].is_null(){None}else{Some(ExactDecimal::parse(&row[key].to_string()).unwrap())};
   legs.push(Selected{quote:broker_preview::Leg{contract:row["contractId"].as_str().unwrap().into(),side:row["sign"].as_i64().unwrap() as i32,strike:decimal("strike"),is_call:row["isCall"].as_bool().unwrap(),is_future:row["isFuture"].as_bool().unwrap(),bid:decimal("bid"),ask:decimal("ask"),delta:row["delta"].as_f64(),vega:row["vega"].as_f64(),multiplier:decimal("multiplier")},role:row["role"].as_str().unwrap().into(),quote_at:row["quoteAtUtc"].clone(),frozen:row["frozen"].as_bool().unwrap()});
  }
  let name=case["name"].as_str().unwrap();
  let preview=registry.project("TomasAI.IFM.Domain.Trade.Shared.Order.TradeOrderPreview",&codec::decode(&std::fs::read(root.join(format!("broker-preview-{name}.msgpack"))).unwrap()).unwrap()).unwrap();
  assert_eq!(broker_preview_display::document(&legs,&preview,None).unwrap(),case["expected"],"{name}");
 }
}
