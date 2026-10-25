use ifm_ui_egui::{backend::{codec,contracts::ExactDecimal,schema::Registry},services::{broker_market_display,broker_preview,option_draft::Selected},wire};
use serde_json::Value as Json;
#[test]
fn copied_grid_documents_match_hybrid_decimal_text_offsets_and_fields(){
 let root=std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend");let r=Registry::compiled().unwrap();
 let read=|name:&str|std::fs::read(root.join(format!("{name}.msgpack"))).unwrap();
 let market=ifm_ui_egui::services::parameters::document(&r,"TomasAI.IFM.Domain.MarketData.Shared.ViewModels.EvaluatedOptionChainReadModel",&codec::decode(&read("broker-market-input")).unwrap(),true).unwrap();
 let expected:Json=wire::decode(&read("broker-market-display")).unwrap();
 assert_eq!(broker_market_display::market(&market).unwrap(),expected);
 let row:Json=serde_json::from_slice(&std::fs::read(root.join("broker-market-leg-input.json")).unwrap()).unwrap();
 let decimal=|key:&str|if row[key].is_null(){None}else{Some(ExactDecimal::parse(&row[key].to_string()).unwrap())};
 let leg=Selected{quote:broker_preview::Leg{contract:row["ContractId"].as_str().unwrap().into(),side:row["Sign"].as_i64().unwrap() as i32,strike:decimal("Strike"),is_call:row["IsCall"].as_bool().unwrap(),is_future:row["IsFuture"].as_bool().unwrap(),bid:decimal("Bid"),ask:decimal("Ask"),delta:row["Delta"].as_f64(),vega:row["Vega"].as_f64(),multiplier:decimal("Multiplier")},role:row["Role"].as_str().unwrap().into(),quote_at:row["QuoteAtUtc"].clone(),frozen:row["Frozen"].as_bool().unwrap()};
 let expected:Json=wire::decode(&read("broker-market-leg-display")).unwrap();
 assert_eq!(broker_market_display::leg(&leg).unwrap(),expected);
}
#[test]
fn quote_age_uses_only_api_instants_and_never_the_workstation_clock(){
 assert_eq!(broker_market_display::quote_age("2026-10-26T08:30:01-04:00","2026-10-26T12:30:00Z"),"1s");
 assert_eq!(broker_market_display::quote_age("2026-10-26T12:30:00Z","2026-10-26T12:30:01Z"),"0s");
 assert_eq!(broker_market_display::quote_age("","2026-10-26T12:30:01Z"),"Unavailable");
}
