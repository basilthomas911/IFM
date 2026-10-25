use ifm_ui_egui::services::composer_market::applied_defaults;
use serde_json::{Value as Json,json};
fn run(at:&str,value:Json)->Json {
 let reference=json!({"ComponentCode":"option-spread-strategy.iron-condor-defaults","SetId":"721dd687-7a5a-4fa6-a404-01545cbab900","Version":2});
 json!({"CreatedAtUtc":at,"Scopes":[{"Enabled":true,"Scope":{"ComponentCode":reference["ComponentCode"]},"Reference":reference}],"Versions":[{"Reference":reference,"Parameters":{"Value":{"Symbols":[value]}}}]})
}
#[test]
fn applied_defaults_follow_latest_enabled_run_and_exact_referenced_symbol(){
 let values=|delta:&str|json!({"Symbol":"es","ShortCallDelta":delta,"ShortPutDelta":"17.5","CallSpreadWidth":"50.000000000000000000000000001","PutSpreadWidth":"75"});
 let old=run("2026-10-25T12:00:00Z",values("10"));
 let current=run("2026-10-26T12:00:00Z",values("16"));
 let selected=applied_defaults(&[old,current.clone()],"ShortIronCondor","ES").unwrap().unwrap();
 assert_eq!(selected.call_delta.as_str(),"16");assert_eq!(selected.put_delta.as_str(),"17.5");assert_eq!(selected.call_width.as_str(),"50.000000000000000000000000001");
 assert!(applied_defaults(&[],"ShortIronCondor","ES").unwrap().is_none());
 let mut historical=current.clone();historical["Versions"][0]["PayloadJson"]=json!(historical["Versions"][0]["Parameters"]["Value"].to_string());historical["Versions"][0]["Parameters"]=Json::Null;
 assert_eq!(applied_defaults(&[historical],"ShortIronCondor","ES").unwrap().unwrap().call_delta.as_str(),"16");
 let mut disabled=current.clone();disabled["Scopes"][0]["Enabled"]=false.into();assert!(applied_defaults(&[disabled],"ShortIronCondor","ES").unwrap().is_none());
 let mut missing=current.clone();missing["Versions"][0]["Reference"]["Version"]=3.into();assert!(applied_defaults(&[missing],"ShortIronCondor","ES").unwrap().is_none());
 let mut duplicate=current.clone();let scope=duplicate["Scopes"][0].clone();duplicate["Scopes"].as_array_mut().unwrap().push(scope);
 assert!(applied_defaults(&[duplicate],"ShortIronCondor","ES").is_err());
 let mut duplicate=current.clone();let version=duplicate["Versions"][0].clone();duplicate["Versions"].as_array_mut().unwrap().push(version);
 assert!(applied_defaults(&[duplicate],"ShortIronCondor","ES").is_err());
 assert!(applied_defaults(&[current],"ShortIronCondor","VX").is_err());
}
