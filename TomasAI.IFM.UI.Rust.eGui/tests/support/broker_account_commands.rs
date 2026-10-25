use super::*;
use ifm_ui_egui::{backend::schema::Registry,services::{broker_account_commands::prepare,business_context::BusinessContext}};
use std::collections::BTreeMap;
async fn control(b:&Backend,verb:&str,value:Value)->Value{service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&value,None,None).await.unwrap()).unwrap().clone()}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn broker_account_mutations_use_api_time_closed_commands_and_no_uncertain_replay(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();let r=Registry::compiled().unwrap();let context=BusinessContext::load(&b).await.unwrap();
 let fields=BTreeMap::from([("manifestHash".into(),"a".repeat(64)),("evidenceReference".into(),"offline://report".into()),("reviewer".into(),"Reviewer".into()),("reason".into(),"Offline qualification".into())]);
 let deadline=||tokio::time::Instant::now()+Duration::from_millis(250);
 control(&b,"BrokerAccountMutationMode","Normal".into()).await;
 for action in ["qualification-submit","qualification-accept","qualification-revoke","qualification-hold","qualification-release","qualification-resync"]{
  let mut operation=prepare(&r,&context,"IFM-EMULATOR-PAPER",uuid::Uuid::new_v4(),action,&fields).unwrap();operation.submit(&b,deadline()).await.unwrap();assert!(operation.acknowledged);operation.submit(&b,deadline()).await.unwrap();
 }
 assert_eq!(control(&b,"BrokerAccountMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(6));
 for mode in ["LostAck","WrongReceipt","WrongEvent"]{
  control(&b,"BrokerAccountMutationMode",mode.into()).await;
  let mut operation=prepare(&r,&context,"IFM-EMULATOR-PAPER",uuid::Uuid::new_v4(),"qualification-hold",&fields).unwrap();
  let error=operation.submit(&b,deadline()).await.unwrap_err();assert!(error.starts_with("OutcomeUnknown:"),"{error}");assert!(operation.attempted);assert!(!operation.acknowledged);
  assert!(operation.submit(&b,deadline()).await.is_err());assert_eq!(control(&b,"BrokerAccountMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
  if mode=="LostAck"{
   control(&b,"BrokerAccountMutationReleaseReceipt",Value::Nil).await;
   let until=tokio::time::Instant::now()+Duration::from_millis(250);
   while !operation.reconcile()&&tokio::time::Instant::now()<until{tokio::time::sleep(Duration::from_millis(5)).await;}
   assert!(operation.acknowledged);operation.submit(&b,deadline()).await.unwrap();
   assert_eq!(control(&b,"BrokerAccountMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
  }
 }
 control(&b,"BrokerAccountMutationMode","EventBeforeAck".into()).await;
 let mut operation=prepare(&r,&context,"IFM-EMULATOR-PAPER",uuid::Uuid::new_v4(),"qualification-hold",&fields).unwrap();operation.submit(&b,deadline()).await.unwrap();assert!(operation.acknowledged);
 control(&b,"BrokerAccountMutationMode","Reject".into()).await;
 let mut operation=prepare(&r,&context,"IFM-EMULATOR-PAPER",uuid::Uuid::new_v4(),"qualification-release",&fields).unwrap();assert!(!operation.submit(&b,deadline()).await.unwrap_err().starts_with("OutcomeUnknown:"));
 assert!(prepare(&r,&context,"IFM-EMULATOR-PAPER",uuid::Uuid::new_v4(),"anything-else",&fields).is_err());
 assert!(prepare(&r,&context,"IFM-EMULATOR-PAPER",uuid::Uuid::new_v4(),"qualification-submit",&BTreeMap::new()).is_err());
 control(&b,"BrokerAccountMutationMode","Normal".into()).await;
}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn broker_account_ui_retains_uncertainty_across_close_and_reconciles_the_original_receipt(){
 use ifm_ui_egui::{state::trades::TradeController,contract_editor::ContractPublication};use serde_json::{Value as Json,json};
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_millis(400)}).await.unwrap();let r=Registry::compiled().unwrap();
 for(verb,name)in[("TradeWorkspaceMode","ComposeFutures"),("BrokerInitMode","Normal"),("BrokerReadMode","Empty"),("OptionChainMode","ComposeFutures")]{control(&b,verb,name.into()).await;}
 fn intent(generation:u64,action:&str,fields:Json)->Json{json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"fields":fields,"originalId":if action=="load"{Json::Null}else{json!("123")}})}
 async fn act(state:&mut TradeController,b:&Backend,r:&Registry,input:&Json)->ContractPublication{state.execute(b,r,input).await.unwrap()}
 let mut state=TradeController::default();let mut current=act(&mut state,&b,&r,&intent(0,"load",json!({}))).await;
 current=act(&mut state,&b,&r,&intent(current.generation,"compose",json!({"tradeId":"456"}))).await;assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
 control(&b,"BrokerAccountMutationMode","LostAck".into()).await;
 let original=intent(current.generation,"qualification-hold",json!({"reason":"Offline qualification","composerSessionId":current.result.context["composerSessionId"]}));
 current=act(&mut state,&b,&r,&original).await;assert_eq!(current.outcomes[0].state,"Unknown","{}",current.outcomes[0].message);
 current=act(&mut state,&b,&r,&intent(current.generation,"qualification-release",json!({"reason":"Offline qualification"}))).await;assert_eq!(current.outcomes[0].state,"Failed");
 assert_eq!(control(&b,"BrokerAccountMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
 current=act(&mut state,&b,&r,&intent(current.generation,"close",json!({}))).await;assert_eq!(current.outcomes[0].state,"Completed");
 control(&b,"BrokerAccountMutationReleaseReceipt",Value::Nil).await;
 let until=tokio::time::Instant::now()+Duration::from_millis(250);
 loop{
  current=act(&mut state,&b,&r,&original).await;
  if current.outcomes[0].state=="Completed"||tokio::time::Instant::now()>=until{break}tokio::time::sleep(Duration::from_millis(5)).await;
 }
 assert_eq!(current.outcomes[0].state,"Completed");assert!(!current.result.context.contains_key("composerSessionId"));
 assert_eq!(control(&b,"BrokerAccountMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
 current=act(&mut state,&b,&r,&intent(current.generation,"compose",json!({"tradeId":"456"}))).await;assert_eq!(current.outcomes[0].state,"Completed");
 let path=std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("target").join(format!("qualification-{}.txt",uuid::Uuid::new_v4()));let bytes="Frozen qualification evidence: ✓\n".as_bytes();std::fs::write(&path,bytes).unwrap();
 current=act(&mut state,&b,&r,&intent(current.generation,"qualification-report",json!({"reportPath":path.to_string_lossy()}))).await;assert_eq!(current.outcomes[0].state,"Completed");
 let report=current.result.rows.iter().find(|row|row.id=="qualification-report:0").unwrap();let report:Json=ifm_ui_egui::wire::decode(report.data.as_ref().unwrap()).unwrap();use sha2::Digest;
 assert_eq!(report["hash"],format!("{:x}",sha2::Sha256::digest(bytes)));assert_eq!(report["preview"],std::str::from_utf8(bytes).unwrap());std::fs::remove_file(path).unwrap();
 current=act(&mut state,&b,&r,&intent(current.generation,"account",json!({}))).await;assert_eq!(current.outcomes[0].state,"Completed");let report=current.result.rows.iter().find(|row|row.id=="qualification-report:0").unwrap();let report:Json=ifm_ui_egui::wire::decode(report.data.as_ref().unwrap()).unwrap();assert_eq!(report["hash"],"");
 for(verb,name)in[("BrokerInitMode","Disabled"),("TradeWorkspaceMode","Disabled"),("BrokerReadMode","Normal"),("OptionChainMode","Normal"),("BrokerAccountMutationMode","Normal")]{control(&b,verb,name.into()).await;}
}
