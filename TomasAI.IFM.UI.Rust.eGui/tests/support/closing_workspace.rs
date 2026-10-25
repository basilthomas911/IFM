use super::*;
use ifm_ui_egui::{backend::schema::Registry,state::trades::TradeController,wire};use serde_json::{Value as Json,json};
async fn control(b:&Backend,verb:&str,name:&str){service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&name.into(),None,None).await.unwrap()).unwrap();}
fn intent(g:u64,action:&str,fields:Json,original:Json)->Json{json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":g,"fields":fields,"originalId":original})}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn closing_workspace_confirmation_submits_reduce_only_and_reopens_without_duplicate(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(3)}).await.unwrap();let r=Registry::compiled().unwrap();
 for(verb,name)in[("TradeWorkspaceMode","ComposeCloseFutures"),("BrokerInitMode","Normal"),("BrokerReadMode","Empty"),("OptionChainMode","ComposeCloseFutures"),("OrderPreparationMode","Normal"),("TradeLifecycleMode","Normal"),("CloseOrderMode","Normal")]{control(&b,verb,name).await;}
 let mut state=TradeController::default();let mut p=state.execute(&b,&r,&intent(0,"load",json!({}),Json::Null)).await.unwrap();
 for(action,fields)in[("trade",json!({"index":"1"})),("compose",json!({})),("submit-order",json!({"action":"Close","quantity":"1","price":"6050.50","orderType":"Limit","algorithm":"None","timeInForce":"Day","pace":"Normal"}))]{p=state.execute(&b,&r,&intent(p.generation,action,fields,json!("123"))).await.unwrap();assert_eq!(p.outcomes[0].state,"Completed","{}",p.outcomes[0].message);}
 let confirmation:Json=wire::decode(&p.result.documents["confirmation"]).unwrap();let confirm=intent(p.generation,"confirm-order",json!({"fillType":"Manual"}),confirmation["id"].clone());let operation=uuid::Uuid::parse_str(confirm["requestId"].as_str().unwrap()).unwrap();
 p=state.execute(&b,&r,&confirm).await.unwrap();assert_eq!(p.outcomes[0].state,"Completed","{}",p.outcomes[0].message);assert_eq!(p.result.context["submitted"],"True");
 let accepted:Json=wire::decode(p.result.rows.iter().find(|row|row.fields["kind"]=="submitted-order").unwrap().data.as_ref().unwrap()).unwrap();assert_eq!(accepted["positionType"],"Closing");assert_eq!(accepted["targetPositionId"]["trade"]["tradeId"],455);
 drop(state);let mut state=TradeController::default();p=state.execute(&b,&r,&intent(0,"load",json!({}),Json::Null)).await.unwrap();p=state.execute(&b,&r,&intent(p.generation,"trade",json!({"index":"1"}),json!("123"))).await.unwrap();p=state.execute(&b,&r,&intent(p.generation,"compose",json!({}),json!("123"))).await.unwrap();assert_eq!(p.result.context["submitted"],"True");
 p=state.execute(&b,&r,&intent(p.generation,"submit-order",json!({}),json!("123"))).await.unwrap();assert_eq!(p.outcomes[0].state,"Failed");
 let path=std::path::PathBuf::from(std::env::var("IFM_STANDALONE_ORDER_JOURNAL").unwrap()).join(format!("{}.json",operation.simple()));std::fs::remove_file(path).unwrap();
 for(verb,name)in[("TradeWorkspaceMode","Disabled"),("BrokerInitMode","Disabled"),("BrokerReadMode","Normal"),("OptionChainMode","Normal"),("CloseOrderMode","Disabled")]{control(&b,verb,name).await;}
}
