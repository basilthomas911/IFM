//! Test-only desktop host for exercising the production System views without live administrative writes.
#![cfg_attr(target_os="windows",windows_subsystem="windows")]
use eframe::egui;
use ifm_ui_rust::{Catalog,contract_editor::{ContractPublication,ContractResult,ContractRow},database_backup::DatabaseBackup,scheduled_tasks::ScheduledTasks};
use serde_json::{json,Value};
use std::collections::BTreeMap;
use std::io::Write;
fn doc(s:&mut ContractPublication,key:&str,value:Value){s.result.documents.insert(key.into(),ifm_ui_rust::wire::encode_value(&value).unwrap().into());}
struct App{backup:DatabaseBackup,scheduled:ScheduledTasks,section:usize,catalog:Catalog,b:ContractPublication,s:ContractPublication,path:String}
impl App {
    fn new()->Self {
        let mut b=ContractPublication{schema:1,generation:1,result:ContractResult{view:"database-backup".into(),..Default::default()},..Default::default()};
        doc(&mut b,"dashboard",json!({"protectionSets":[{"id":"core-postgresql","enabled":true}],"recentOperations":[]}));
        doc(&mut b,"setup",json!({"backupHostSettings":{"Host":"fixture"}}));
        doc(&mut b,"history",json!({"LocalWorkstation":[{"operationId":"operation","backupSetId":"fixture-run","source":"LocalWorkstation","engine":"PostgreSql","protectionSet":"core-postgresql","kind":"Backup","requestedMode":"Full","resolvedMode":"Full","createdUtc":"2026-10-19T18:00:00Z","phase":"Running","outcome":"None","progressPercent":25}],"AwsCloud":[]}));
        doc(&mut b,"continuations",json!({"AwsCloud":"fixture-next"}));
        let mut fields=BTreeMap::new();for(key,value)in[("name","Fixture schedule"),("taskKey","fixture-task"),("environment","Development"),("hostId","development"),("expression","0 1 17 ? * MON-FRI"),("timeZoneId","America/New_York"),("maximumRuntimeSeconds","1800"),("dispatchToleranceSeconds","60"),("oneTime","false"),("enabled","false"),("installationStatus","Applied")]{fields.insert(key.into(),value.into());}
        let mut s=ContractPublication{schema:1,generation:1,result:ContractResult{view:"scheduled-tasks".into(),rows:vec![ContractRow{id:"schedule".into(),data:None,fields}],..Default::default()},..Default::default()};
        s.result.choices.insert("taskKey".into(),vec!["fixture-task".into()]);
        doc(&mut s,"projects",json!([{"taskKey":"fixture-task","name":"Fixture task"}]));
        doc(&mut s,"runs",json!([{"id":"run","intendedFireTimeUtc":"2026-10-19T21:01:00Z","status":"Uncertain","stage":"Interrupted","detail":"Fixture receipt"}]));
        doc(&mut s,"histories",json!({"schedule":[{"id":"run","intendedFireTimeUtc":"2026-10-19T21:01:00Z","status":"Uncertain","stage":"Interrupted","detail":"Fixture receipt"}]}));
        s.result.context.insert("outputEnd".into(),"False".into());
        Self{backup:Default::default(),scheduled:Default::default(),section:0,catalog:serde_json::from_str(include_str!("../catalog/strings.en.json")).unwrap(),b,s,path:std::env::var("IFM_SYSTEM_ACTION_LOG").expect("IFM_SYSTEM_ACTION_LOG")}
    }
    fn accept(&mut self,payload:String){
        let value:Value=serde_json::from_str(&payload).unwrap();
        let mut file=std::fs::OpenOptions::new().create(true).append(true).open(&self.path).unwrap();writeln!(file,"{}",payload).unwrap();
        let s=if value["view"]=="database-backup"{&mut self.b}else{&mut self.s};
        s.generation+=1;s.status="Completed".into();s.outcomes.push(ifm_ui_rust::lookup_editor::EditorOutcome{request_id:value["requestId"].as_str().unwrap().into(),state:"Completed".into(),message:"Fixture accepted".into()});
        match value["intent"].as_str().unwrap_or_default(){
            "backup-source"=>{s.result.context.insert("source".into(),value["fields"]["source"].as_str().unwrap().into());},
            "backup-policy-load"=>{doc(s,"policy",json!({"id":"fixture-policy","revision":1,"enforced":true,"daily":5,"weekly":4,"monthly":2}));doc(s,"policyEditor",json!({"rpoMinutes":"60","rtoMinutes":"120","maximumVerificationHours":"48"}));s.result.context.insert("source".into(),value["fields"]["source"].as_str().unwrap().into());},
            "backup-output"|"backup-more-output"=>{s.result.context.insert("output".into(),"Fixture retained backup output".into());},
            "backup-clear-output"=>{s.result.context.insert("output".into(),String::new());},
            "preview"=>{doc(s,"preview",json!({"valid":true,"errors":[],"nextFireTimesUtc":["2026-10-19T21:01:00Z"]}));},
            "output"|"more-output"=>{s.result.context.insert("output".into(),"Fixture retained task output".into());},
            _=>{}
        }
    }
}
impl eframe::App for App {
    fn update(&mut self,ctx:&egui::Context,_:&mut eframe::Frame){
        egui::CentralPanel::default().show(ctx,|_|{});
        if let Some(section)=ctx.data_mut(|d|d.remove_temp::<usize>(egui::Id::new("system-admin-selection"))){self.section=section;if section==1{let load=self.scheduled.request("load",&self.s);self.accept(load);}}
        let action=if self.section==0{self.backup.draw(ctx,&self.b,true)}else{self.scheduled.draw_system(ctx,&self.s,&self.catalog,true)};
        if let Some(action)=action{self.accept(action);ctx.request_repaint();}
    }
}
fn main()->eframe::Result{
    eframe::run_native("System Admin Action Fixture",eframe::NativeOptions{viewport:egui::ViewportBuilder::default().with_inner_size([1600.,1000.]),..Default::default()},Box::new(|cc|{ifm_ui_rust::presentation::apply(&cc.egui_ctx);Ok(Box::new(App::new()))}))
}
