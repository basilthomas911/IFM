use egui_kittest::{Harness,kittest::Queryable};
use ifm_ui_rust::{Catalog,contract_editor::{ContractPublication,ContractResult,ContractRow},database_backup::DatabaseBackup,scheduled_tasks::ScheduledTasks};
use serde_json::{json,Value};
use std::collections::BTreeMap;
fn document(s:&mut ContractPublication,key:&str,value:Value){s.result.documents.insert(key.into(),ifm_ui_rust::wire::encode_value(&value).unwrap().into());}
fn complete(s:&mut ContractPublication,payload:&str){let value:Value=serde_json::from_str(payload).unwrap();s.generation+=1;s.outcomes.push(ifm_ui_rust::lookup_editor::EditorOutcome{request_id:value["requestId"].as_str().unwrap().into(),state:"Completed".into(),message:"Fixture accepted".into()});}
#[test]
fn backup_original_controls_dispatch_through_service_intents_without_growing_the_manager(){
    let mut s=ContractPublication{schema:1,generation:1,result:ContractResult{view:"database-backup".into(),..Default::default()},..Default::default()};
    document(&mut s,"dashboard",json!({"protectionSets":[{"id":"core-postgresql","enabled":true}],"recentOperations":[]}));
    let mut view=DatabaseBackup::default();let load=view.request("load",&s);complete(&mut s,&load);
    let mut h=Harness::new_state(|ctx,state:&mut(DatabaseBackup,ContractPublication,Vec<Value>)|{if let Some(payload)=state.0.draw(ctx,&state.1,true){state.2.push(serde_json::from_str(&payload).unwrap());complete(&mut state.1,&payload);}},(view,s,vec![]));
    h.set_size(eframe::egui::vec2(1600.,1000.));h.run();h.get_by_label("Setup").click();h.run();
    let initial=h.get_by_label("System Admin Manager").rect();
    h.get_by_label("core-postgresql").click();h.run();assert_eq!(h.state().2.last().unwrap()["intent"],"backup-select");
    h.get_by_label("Request Backup").click();h.run();let action=h.state().2.last().unwrap();assert_eq!(action["intent"],"backup-run");assert_eq!(action["fields"]["protectionSets"],"core-postgresql");assert_eq!(action["fields"]["mode"],"Full");assert_eq!(action["fields"]["targetProfile"],"development-on-demand");assert_eq!(action["fields"]["validationProfile"],"development-restore");
    h.get_by_label("Load policy").click();h.run();assert_eq!(h.state().2.last().unwrap()["intent"],"backup-policy-load");
    h.get_by_label("Restore Backup (fresh copy)").click();h.run();assert_eq!(h.state().2.last().unwrap()["intent"],"backup-restore");
    h.get_by_label("AWS Backup").click();h.run();let action=h.state().2.last().unwrap();assert_eq!(action["intent"],"backup-source");assert_eq!(action["fields"]["targetProfile"],"");
    h.run();assert_eq!(initial,h.get_by_label("System Admin Manager").rect());
}
#[test]
fn scheduler_manager_retains_original_editor_actions_and_field_labels(){
    let c:Catalog=serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap();
    let mut s=ContractPublication{schema:1,generation:1,result:ContractResult{view:"scheduled-tasks".into(),rows:vec![ContractRow{id:"id".into(),data:None,fields:BTreeMap::from([("name".into(),"Market close".into()),("environment".into(),"Development".into()),("hostId".into(),"development".into()),("expression".into(),"0 1 17 ? * MON-FRI".into()),("timeZoneId".into(),"America/New_York".into())])}],..Default::default()},..Default::default()};
    let mut view=ScheduledTasks::default();let load=view.request("load",&s);complete(&mut s,&load);
    let mut h=Harness::new_state(move|ctx,state:&mut(ScheduledTasks,ContractPublication,Vec<Value>)|{if let Some(payload)=state.0.draw_system(ctx,&state.1,&c,true){state.2.push(serde_json::from_str(&payload).unwrap());complete(&mut state.1,&payload);}},(view,s,vec![]));
    h.set_size(eframe::egui::vec2(1600.,1000.));h.run();h.get_by_label("Setup").click();h.run();
    h.get_by_label("New").click();h.run();h.get_by_label("Preview").click();h.run();assert_eq!(h.state().2.last().unwrap()["intent"],"preview");
    h.get_by_label("Save").click();h.run();assert_eq!(h.state().2.last().unwrap()["intent"],"add");
    h.get_by_label("Logs").click();h.run();h.get_by_label("Refresh logs").click();h.run();assert_eq!(h.state().2.last().unwrap()["intent"],"load");
}
