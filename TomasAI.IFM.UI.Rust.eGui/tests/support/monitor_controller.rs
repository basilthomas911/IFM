use super::*;
use ifm_ui_egui::{backend::schema::Registry,state::{trade_monitors::{Hub,OpenRequest},trades::TradeController},services::{business_context::BusinessContext,trade_monitor_queries::Scope},wire};
use serde_json::{Value as Json,json};
async fn control(b:&Backend,verb:&str,value:Value)->Value{service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&value,None,None).await.unwrap()).unwrap().clone()}
fn open_request()->OpenRequest{OpenRequest{scope:Scope{portfolio:1,fund:2,order:701,trade:456},strategy:"IronCondor".into(),trade_type:"ShortIronCondor".into(),trade_action:"Sell".into(),fund_name:"Fixture fund".into()}}
fn data(hub:&Hub,kind:&str)->Option<Json>{hub.current().result.rows.iter().find(|row|row.fields["kind"]==kind).map(|row|wire::decode(row.data.as_ref().unwrap()).unwrap())}
async fn wait(hub:&Hub,predicate:impl Fn(&Hub)->bool){let until=tokio::time::Instant::now()+Duration::from_millis(700);while !predicate(hub)&&tokio::time::Instant::now()<until{tokio::time::sleep(Duration::from_millis(10)).await;}assert!(predicate(hub),"Monitor did not publish its expected state: {:?}",hub.current().result.context);}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn monitor_controller_streams_independently_keeps_eod_and_cleans_cancelled_lifetimes(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();let r=std::sync::Arc::new(Registry::compiled().unwrap());let context=BusinessContext::load(&b).await.unwrap();let deadline=||tokio::time::Instant::now()+Duration::from_secs(2);
 control(&b,"MonitorMode","Normal".into()).await;let hub=Hub::with_host_scope("nativecompat").unwrap();hub.open(&b,r.clone(),open_request(),context.clone(),deadline()).await.unwrap();assert_eq!(hub.current().result.context["monitorLive"],"False");assert!(data(&hub,"chart").unwrap()["points"].as_array().unwrap().len()==1);
 let key="1.2.701.456";hub.set_live(&b,&r,key,true,&context,deadline()).await.unwrap();assert_eq!(hub.current().result.context["monitorLive"],"True");assert_eq!(hub.current().result.context["monitorStatus"],"");assert_eq!(data(&hub,"realtime").unwrap()["snapshotRevision"],10);
 control(&b,"MonitorPublish","Start".into()).await;tokio::time::sleep(Duration::from_millis(70)).await;assert_eq!(hub.current().result.context["monitorLive"],"True");assert!(!hub.current().result.context["monitorStatus"].contains("OFF"));
 control(&b,"MonitorPublish","Newer".into()).await;wait(&hub,|hub|data(hub,"realtime").is_some_and(|v|v["snapshotRevision"].as_i64().unwrap()>10)).await;
 let revision=data(&hub,"realtime").unwrap()["snapshotRevision"].clone();control(&b,"MonitorPublish","Stale".into()).await;control(&b,"MonitorPublish","WrongScope".into()).await;tokio::time::sleep(Duration::from_millis(70)).await;assert_eq!(data(&hub,"realtime").unwrap()["snapshotRevision"],revision);
 control(&b,"MonitorPublish","Eod".into()).await;wait(&hub,|hub|hub.current().result.rows.iter().filter(|row|row.fields["kind"]=="position").any(|row|wire::decode::<Json>(row.data.as_ref().unwrap()).unwrap()["tradeStatus"]=="EndOfDay")).await;
 control(&b,"MonitorPublish","Fail".into()).await;wait(&hub,|hub|hub.current().result.context["monitorLive"]=="False").await;assert!(hub.current().result.context["monitorStatus"].contains("Synthetic terminal option failure"));assert_eq!(data(&hub,"position-details").unwrap()["phase"],"EndOfDay");
 hub.close(key).unwrap();assert!(!hub.current().result.rows.iter().any(|row|row.fields["kind"]=="workspace-tab"));
 // The feed can be acknowledged before its initial snapshot read succeeds.
 // Only a newer accepted snapshot resolves that warning; it must not linger
 // in the trade-status field after authoritative data has arrived.
 control(&b,"MonitorMode","Normal".into()).await;hub.open(&b,r.clone(),open_request(),context.clone(),deadline()).await.unwrap();
 control(&b,"MonitorMode","SnapshotReadFailed".into()).await;hub.set_live(&b,&r,key,true,&context,deadline()).await.unwrap();
 assert!(hub.current().result.context["monitorStatus"].contains("waiting for an authoritative snapshot"));
 control(&b,"MonitorPublish","Newer".into()).await;wait(&hub,|hub|hub.current().result.context["monitorStatus"].is_empty()).await;hub.close(key).unwrap();
 control(&b,"MonitorMode","HangPositions".into()).await;assert!(tokio::time::timeout(Duration::from_millis(100),hub.open(&b,r.clone(),open_request(),context.clone(),deadline())).await.is_err());assert!(hub.current().result.rows.is_empty());
 control(&b,"MonitorMode","Normal".into()).await;hub.open(&b,r.clone(),open_request(),context.clone(),deadline()).await.unwrap();control(&b,"MonitorMode","HostTimeout".into()).await;
 assert!(tokio::time::timeout(Duration::from_millis(100),hub.set_live(&b,&r,key,true,&context,deadline())).await.is_err());assert_eq!(hub.current().result.context["monitorCanLive"],"True");assert_eq!(hub.current().result.context["monitorLive"],"False");
 control(&b,"MonitorMode","Normal".into()).await;hub.set_live(&b,&r,key,true,&context,deadline()).await.unwrap();assert_eq!(hub.current().result.context["monitorLive"],"True");hub.close(key).unwrap();
 // A market-open event must refresh API permissions even when this view
 // originally opened during a closed session with emulator permission off.
 control(&b,"MonitorMode","ClosedSession".into()).await;let closed=BusinessContext::load(&b).await.unwrap();hub.open(&b,r.clone(),open_request(),closed,deadline()).await.unwrap();assert_eq!(hub.current().result.context["monitorCanLive"],"False");
 control(&b,"MonitorMode","Normal".into()).await;control(&b,"MonitorPublish","Start".into()).await;wait(&hub,|hub|hub.current().result.context["monitorCanLive"]=="True").await;hub.close(key).unwrap();
 // Exercise the actual workspace owner: a parallel Blotter RPC holds its async
 // lock while the separate monitor still consumes current backend snapshots.
 for(verb,name)in[("TradeWorkspaceMode","Monitor"),("BrokerInitMode","Disabled"),("OptionChainMode","ComposeOptions"),("MonitorMode","Normal")]{control(&b,verb,name.into()).await;}
 let mut state=TradeController::default();state.monitors=Hub::with_host_scope("nativecompat").unwrap();let hub=state.monitors.clone();
 fn intent(g:u64,action:&str)->Json{json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":g,"fields":{},"originalId":if action=="load"{Json::Null}else{json!("123")}})}
 let mut current=state.execute(&b,&r,&intent(0,"load")).await.unwrap();current=state.execute(&b,&r,&intent(current.generation,"load-trade")).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);current=state.execute(&b,&r,&intent(current.generation,"live-on")).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
 control(&b,"BrokerInitMode","Hang".into()).await;let state=std::sync::Arc::new(tokio::sync::Mutex::new(state));let state2=state.clone();let b2=b.clone();let r2=r.clone();let generation=current.generation;
 let blocked=tokio::spawn(async move{state2.lock().await.execute(&b2,&r2,&intent(generation,"compose")).await});tokio::time::sleep(Duration::from_millis(50)).await;assert!(!blocked.is_finished());
 control(&b,"MonitorPublish","Newer".into()).await;wait(&hub,|hub|data(hub,"realtime").is_some_and(|v|v["snapshotRevision"].as_i64().unwrap()>10)).await;assert!(!blocked.is_finished());blocked.abort();let _=blocked.await;state.lock().await.release_preview(&b,&r,deadline()).await.unwrap();hub.close(key).unwrap();assert_eq!(control(&b,"OptionChainCount",Value::Nil).await.as_array().unwrap()[0],Value::from(0));
 for(verb,name)in[("MonitorMode","Disabled"),("TradeWorkspaceMode","Disabled"),("BrokerInitMode","Disabled"),("OptionChainMode","Normal")]{control(&b,verb,name.into()).await;}
}
