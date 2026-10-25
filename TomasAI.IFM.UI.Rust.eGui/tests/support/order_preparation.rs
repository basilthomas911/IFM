use super::*;
use ifm_ui_egui::{backend::schema::Registry,state::trades::TradeController,services::{business_context::BusinessContext,order_preparation::Draft,order_submission::Operation,broker_preview::Leg,broker_reads::Scope},wire};
use serde_json::{Value as Json,json};
async fn mode(b:&Backend,verb:&str,name:&str){service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&name.into(),None,None).await.unwrap()).unwrap();}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn broker_order_preparation_uses_backend_economics_and_native_confirmation_is_nonblocking(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();let r=Registry::compiled().unwrap();let context=BusinessContext::load(&b).await.unwrap();
 let deadline=||tokio::time::Instant::now()+Duration::from_secs(2);
 let draft=Draft{scope:Scope{portfolio:1,fund:2,order:123},trade:456,underlying:"ESZ6".into(),strategy:"FuturesOutright".into(),expiry:context.value_date,quantity:1,price:ExactDecimal::parse("6050.50").unwrap(),legs:vec![Leg{contract:"ESZ6".into(),side:1,strike:None,is_call:false,is_future:true,bid:None,ask:None,delta:None,vega:None,multiplier:Some(ExactDecimal::parse("50").unwrap())}],quote_inputs:"[]".into(),order_type:"Limit".into(),algorithm:"None".into(),tif:"Day".into(),pace:"Normal".into(),composition:uuid::Uuid::new_v4(),workflow:uuid::Uuid::new_v4(),component:uuid::Uuid::new_v4()};
 for setting in ["WrongScope","WrongLeg","Normal"]{
  mode(&b,"OrderPreparationMode",setting).await;
  let result=draft.prepare(&b,&r,deadline()).await;
  if setting!="Normal"{assert!(result.is_err());continue}
  let prepared=result.unwrap();assert_eq!(prepared.result["CashAmount"],"302525.00");assert_eq!(prepared.result["TotalCommission"],"2.50");assert_eq!(prepared.result["TotalAmount"],"302527.50");
  let mut operation=Operation::prepare(&r,&context,1,uuid::Uuid::new_v4(),prepared.candidate_wire).unwrap();operation.evaluate(&b,&r,deadline()).await.unwrap();assert_eq!(operation.accepted.len(),1);assert_eq!(operation.accepted[0]["Id"]["OrderId"],701);assert_eq!(operation.accepted[0]["Components"][0]["Legs"][0]["AssetFamily"],"Futures");
  // This isolated service case does not dispatch its synthetic accepted order.
  // Remove only its own fixture journal before the separate UI case starts.
  let path=std::path::PathBuf::from(std::env::var("IFM_STANDALONE_ORDER_JOURNAL").unwrap()).join(format!("{}.json",operation.id.simple()));std::fs::remove_file(path).unwrap();
 }
 for(verb,name)in[("TradeWorkspaceMode","ComposeFutures"),("BrokerInitMode","Normal"),("BrokerReadMode","Empty"),("OptionChainMode","ComposeFutures"),("OrderPreparationMode","Normal")]{mode(&b,verb,name).await;}
 fn intent(g:u64,action:&str,fields:Json,original:Json)->Json{json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":g,"fields":fields,"originalId":original})}
 let mut state=TradeController::default();let mut current=state.execute(&b,&r,&intent(0,"load",json!({}),Json::Null)).await.unwrap();current=state.execute(&b,&r,&intent(current.generation,"compose",json!({}),json!("123"))).await.unwrap();
 current=state.execute(&b,&r,&intent(current.generation,"submit-order",json!({"action":"Open","quantity":"1","price":"6050.50","orderType":"Limit","algorithm":"None","timeInForce":"Day","pace":"Normal"}),json!("123"))).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
 let confirmation:Json=wire::decode(&current.result.documents["confirmation"]).unwrap();assert_eq!(confirmation["order"]["orderAmount"],"302525.00");assert_eq!(confirmation["legs"][0]["legCommission"],"2.50");assert_eq!(current.result.context["editable"],"False");
 current=state.execute(&b,&r,&intent(current.generation,"preview-draft",json!({"quantity":"2"}),json!("123"))).await.unwrap();assert_eq!(current.outcomes[0].state,"Failed");
 current=state.execute(&b,&r,&intent(current.generation,"cancel-order-confirmation",json!({}),confirmation["id"].clone())).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed");assert!(!current.result.documents.contains_key("confirmation"));assert_eq!(current.result.context["editable"],"True");assert_eq!(current.result.context["status"],"");
 for(verb,name)in[("BrokerInitMode","Disabled"),("TradeWorkspaceMode","Disabled"),("BrokerReadMode","Normal"),("OptionChainMode","Normal"),("OrderPreparationMode","Normal")]{mode(&b,verb,name).await;}
}
