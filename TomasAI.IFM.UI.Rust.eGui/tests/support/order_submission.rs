use super::*;
use ifm_ui_egui::{backend::schema::Registry,state::trades::TradeController,contract_editor::ContractPublication,wire};
use serde_json::{Value as Json,json};
async fn control(b:&Backend,verb:&str,value:Value)->Value{service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&value,None,None).await.unwrap()).unwrap().clone()}
fn intent(g:u64,action:&str,fields:Json,original:Json)->Json{json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":g,"fields":fields,"originalId":original})}
async fn open(state:&mut TradeController,b:&Backend,r:&Registry)->ContractPublication{
 let current=state.execute(b,r,&intent(0,"load",json!({}),Json::Null)).await.unwrap();let current=state.execute(b,r,&intent(current.generation,"compose",json!({}),json!("123"))).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);current
}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn broker_order_submission_restores_uncertain_bytes_then_reconciles_without_duplicate_financial_writes(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(3)}).await.unwrap();let r=Registry::compiled().unwrap();
 for failure in ["None","Portfolio","Lifecycle"]{
  for(verb,name)in[("TradeWorkspaceMode","ComposeFutures"),("BrokerInitMode","Normal"),("BrokerReadMode","Submitted"),("OptionChainMode","ComposeFutures"),("OrderPreparationMode",if failure=="Portfolio"{"LostAck"}else{"Normal"}),("TradeLifecycleMode",if failure=="Lifecycle"{"LostAck"}else{"Normal"})]{control(&b,verb,name.into()).await;}
  let mut state=TradeController::default();let mut current=open(&mut state,&b,&r).await;
  current=state.execute(&b,&r,&intent(current.generation,"submit-order",json!({"action":"Open","quantity":"1","price":"6050.50","orderType":"Limit","algorithm":"None","timeInForce":"Day","pace":"Normal"}),json!("123"))).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
  let confirmation:Json=wire::decode(&current.result.documents["confirmation"]).unwrap();
  let confirm=intent(current.generation,"confirm-order",json!({"fillType":"Broker"}),confirmation["id"].clone());let operation=uuid::Uuid::parse_str(confirm["requestId"].as_str().unwrap()).unwrap();
  current=state.execute(&b,&r,&confirm).await.unwrap();assert_eq!(current.outcomes[0].state,if failure=="None"{"Completed"}else{"Unknown"},"{}",current.outcomes[0].message);
  let path=std::path::PathBuf::from(std::env::var("IFM_STANDALONE_ORDER_JOURNAL").unwrap()).join(format!("{}.json",operation.simple()));assert!(path.exists());
  let saved:Json=serde_json::from_slice(&std::fs::read(&path).unwrap()).unwrap();assert_eq!(saved["Request"]["OperationId"],operation.to_string());assert_eq!(saved["Phase"],if failure=="None"{2}else{1});
  drop(state);let mut reopened=TradeController::default();current=open(&mut reopened,&b,&r).await;
  current=reopened.execute(&b,&r,&intent(current.generation,"submit-order",json!({}),json!("123"))).await.unwrap();assert_eq!(current.outcomes[0].state,"Failed");
  control(&b,"OrderPreparationReleaseReceipt",Value::Nil).await;
  current=reopened.execute(&b,&r,&intent(current.generation,"refresh-execution",json!({}),json!("123"))).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
  assert_eq!(current.result.context["submissionUnconfirmed"],"False");assert_eq!(current.result.context["submitted"],"True");assert!(current.result.rows.iter().any(|row|row.id.starts_with("broker-order:1.2.701.")));
  assert_eq!(control(&b,"OrderPreparationCount",Value::Nil).await.as_array().unwrap()[1],Value::from(1));assert_eq!(control(&b,"TradeLifecycleCount",Value::Nil).await.as_array().unwrap()[0],Value::from(4));
  let completed:Json=serde_json::from_slice(&std::fs::read(&path).unwrap()).unwrap();assert_eq!(completed["Phase"],2);assert_eq!(completed["CommandWire"],saved["CommandWire"]);
  // Each synthetic case owns this exact journal, so it cannot constrain the
  // next case's independent setup fixture. Preserve the other test artifacts.
  std::fs::remove_file(path).unwrap();
 }
 for(verb,name)in[("BrokerInitMode","Disabled"),("TradeWorkspaceMode","Disabled"),("BrokerReadMode","Normal"),("OptionChainMode","Normal"),("OrderPreparationMode","Normal"),("TradeLifecycleMode","Normal")]{control(&b,verb,name.into()).await;}
}
