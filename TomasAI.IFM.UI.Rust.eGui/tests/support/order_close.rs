use super::*;
use ifm_ui_egui::{backend::schema::Registry,services::{business_context::BusinessContext,order_preparation::Draft,order_submission::Operation,order_close,order_submission_store as store,broker_preview::Leg,broker_reads::Scope}};
async fn control(b:&Backend,verb:&str,name:&str){service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&name.into(),None,None).await.unwrap()).unwrap();}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn closing_order_reverses_remaining_position_and_restores_exact_uncertain_submission(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();let r=Registry::compiled().unwrap();let context=BusinessContext::load(&b).await.unwrap();let deadline=||tokio::time::Instant::now()+Duration::from_secs(2);
 control(&b,"OrderPreparationMode","Normal").await;
 let draft=Draft{scope:Scope{portfolio:1,fund:2,order:123},trade:456,underlying:"ESZ6".into(),strategy:"FuturesOutright".into(),expiry:context.value_date,quantity:1,price:ExactDecimal::parse("6050.50").unwrap(),legs:vec![Leg{contract:"ESZ6".into(),side:-1,strike:None,is_call:false,is_future:true,bid:None,ask:None,delta:None,vega:None,multiplier:Some(ExactDecimal::parse("50").unwrap())}],quote_inputs:"[]".into(),order_type:"Limit".into(),algorithm:"None".into(),tif:"Day".into(),pace:"Normal".into(),composition:uuid::Uuid::new_v4(),workflow:uuid::Uuid::new_v4(),component:uuid::Uuid::new_v4()};

 let prepared=draft.prepare(&b,&r,deadline()).await.unwrap();let primary=ifm_ui_egui::services::trade_monitor_queries::Scope{portfolio:1,fund:2,order:700,trade:455};
 for mode in ["WrongScope","Quantity","Closed","Normal","LostAck"]{
  control(&b,"CloseOrderMode",mode).await;
  let candidate=order_close::candidate(&b,&r,primary,"FuturesOutright",&prepared.candidate_wire,deadline()).await;
  if ["WrongScope","Quantity","Closed"].contains(&mode){assert!(candidate.is_err());continue}
  control(&b,"TradeLifecycleMode","Normal").await;
  let mut operation=Operation::prepare_close(&r,&context,1,uuid::Uuid::new_v4(),candidate.unwrap()).unwrap();
  let evaluated=operation.evaluate(&b,&r,deadline()).await;
  if mode=="LostAck"{
   assert!(evaluated.unwrap_err().starts_with("OutcomeUnknown:"));
   let saved=store::pending().await.unwrap().into_iter().find(|row|row["Request"]["OperationId"]==operation.id.to_string()).unwrap();
   let original=operation.body.clone();operation=store::restore(&r,&saved).unwrap();assert_eq!(operation.body,original);
   control(&b,"CloseOrderRelease","").await;operation.reconcile(&b,&r,deadline()).await.unwrap();
  }else{evaluated.unwrap();}
  assert_eq!(operation.accepted[0]["PositionType"],"Closing");assert_eq!(operation.accepted[0]["TargetPositionId"]["Trade"]["TradeId"],455);
  operation.dispatch(&b,&r,&context,"Manual",deadline()).await.unwrap();assert_eq!(operation.dispatched,1);
  let count=b.request(&ActorSubject::query("NativeCompatibility","CloseOrderCount","none"),&Value::Nil,None,None).await.unwrap();assert_eq!(service_result(&count).unwrap().as_array().unwrap()[0],Value::from(1));
  let path=std::path::PathBuf::from(std::env::var("IFM_STANDALONE_ORDER_JOURNAL").unwrap()).join(format!("{}.json",operation.id.simple()));std::fs::remove_file(path).unwrap();
 }
 control(&b,"CloseOrderMode","Disabled").await;
}
