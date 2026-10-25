use super::*;
use ifm_ui_egui::{backend::schema::Registry,services::{broker_order_commands::prepare,business_context::BusinessContext,broker_reads::{self,Scope}},state::trades::TradeController,contract_editor::ContractRow,wire};
use serde_json::{Value as Json,json};
async fn control(b:&Backend,verb:&str,value:Value)->Value{service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&value,None,None).await.unwrap()).unwrap().clone()}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn broker_order_commands_and_ui_keep_current_scope_and_uncertain_receipts(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_millis(400)}).await.unwrap();let r=Registry::compiled().unwrap();let context=BusinessContext::load(&b).await.unwrap();
 for(verb,name)in[("TradeWorkspaceMode","ComposeFutures"),("BrokerInitMode","Normal"),("BrokerReadMode","Working"),("OptionChainMode","ComposeFutures")]{control(&b,verb,name.into()).await;}
 let scope=Scope{portfolio:1,fund:2,order:123};let order=broker_reads::orders(&b,&r,scope,tokio::time::Instant::now()+Duration::from_secs(1)).await.unwrap().remove(0);let key=broker_reads::identity(&order["Id"]).unwrap();
 let deadline=||tokio::time::Instant::now()+Duration::from_millis(250);
 for mode in ["Normal","EventBeforeAck","WrongReceipt","WrongEvent","LostAck","Reject"]{
  control(&b,"BrokerOrderMutationMode",mode.into()).await;
  let price=ExactDecimal::parse("6050.50").unwrap();let mut operation=prepare(&r,&context,&order,None,false,uuid::Uuid::new_v4(),Some(&price)).unwrap();
  let result=operation.submit(&b,&r,deadline()).await;
  if ["Normal","EventBeforeAck"].contains(&mode){result.unwrap();assert!(operation.acknowledged);operation.submit(&b,&r,deadline()).await.unwrap();}
  else if mode=="Reject"{assert!(!result.unwrap_err().starts_with("OutcomeUnknown:"));}
  else{assert!(result.unwrap_err().starts_with("OutcomeUnknown:"));assert!(operation.submit(&b,&r,deadline()).await.is_err());}
  assert_eq!(control(&b,"BrokerOrderMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
  if mode=="LostAck"{control(&b,"BrokerOrderMutationReleaseReceipt",Value::Nil).await;let until=deadline();while !operation.reconcile()&&tokio::time::Instant::now()<until{tokio::time::sleep(Duration::from_millis(5)).await;}assert!(operation.acknowledged);}
 }
 let mut state=TradeController::default();
 fn intent(g:u64,action:&str,fields:Json)->Json{json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":g,"fields":fields,"originalId":if action=="load"{Json::Null}else{json!("123")}})}
 let mut current=state.execute(&b,&r,&intent(0,"load",json!({}))).await.unwrap();current=state.execute(&b,&r,&intent(current.generation,"compose",json!({}))).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
 let expected:Vec<ContractRow>=wire::decode(&std::fs::read(std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend/broker-evidence-rows.msgpack")).unwrap()).unwrap();
 for expected in expected{let actual=current.result.rows.iter().find(|row|row.id==expected.id).unwrap_or_else(||panic!("Missing {}",expected.id));let actual:Json=wire::decode(actual.data.as_ref().unwrap()).unwrap();let expected_data:Json=wire::decode(expected.data.as_ref().unwrap()).unwrap();assert_eq!(actual,expected_data,"{}",expected.id);}
 control(&b,"BrokerOrderMutationMode","Normal".into()).await;
 current=state.execute(&b,&r,&intent(current.generation,"update-limit",json!({"brokerOrderId":key,"limitPrice":"6050.51"}))).await.unwrap();assert_eq!(current.outcomes[0].state,"Failed");assert_eq!(control(&b,"BrokerOrderMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(0));
 control(&b,"BrokerOrderMutationMode","LostAck".into()).await;
 let original=intent(current.generation,"cancel-unfilled",json!({"brokerOrderId":key}));current=state.execute(&b,&r,&original).await.unwrap();assert_eq!(current.outcomes[0].state,"Unknown","{}",current.outcomes[0].message);
 current=state.execute(&b,&r,&intent(current.generation,"cancel-unfilled",json!({"brokerOrderId":key}))).await.unwrap();assert_eq!(current.outcomes[0].state,"Failed");
 current=state.execute(&b,&r,&intent(current.generation,"close",json!({}))).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed");control(&b,"BrokerOrderMutationReleaseReceipt",Value::Nil).await;
 let until=deadline();loop{current=state.execute(&b,&r,&original).await.unwrap();if current.outcomes[0].state=="Completed"||tokio::time::Instant::now()>=until{break}tokio::time::sleep(Duration::from_millis(5)).await;}assert_eq!(current.outcomes[0].state,"Completed");assert_eq!(control(&b,"BrokerOrderMutationCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
 for(verb,name)in[("BrokerInitMode","Disabled"),("TradeWorkspaceMode","Disabled"),("BrokerReadMode","Normal"),("OptionChainMode","Normal"),("BrokerOrderMutationMode","Normal")]{control(&b,verb,name.into()).await;}
}
