use ifm_ui_egui::models::cells::Cells;
use super::*;
use ifm_ui_egui::{backend::schema::Registry,services::{broker_initialization::load,broker_reads::Scope,business_context::BusinessContext}};
async fn control(b:&Backend,verb:&str,value:Value)->Value{service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&value,None,None).await.unwrap()).unwrap().clone()}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn broker_init_parallel_sources_keep_api_clock_partial_evidence_and_one_deadline(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();
 let r=Registry::compiled().unwrap();let scope=Scope{portfolio:1,fund:2,order:123};let context=BusinessContext::load(&b).await.unwrap();
 control(&b,"BrokerReadMode","Normal".into()).await;control(&b,"BrokerInitMode","Normal".into()).await;
 let read=|budget|load(&b,&r,&context,scope,456,"ESZ6",false,"IFM-EMULATOR-PAPER",tokio::time::Instant::now()+budget);
 let initial=read(Duration::from_secs(1)).await.unwrap();assert!(initial.errors.is_empty(),"{:?}",initial.errors);
 assert_eq!(initial.balances.as_ref().unwrap().cell("AvailableCash").text(),"123456.78");assert_eq!(initial.usage.as_ref().unwrap().cell("ReservedFundLossCapital").text(),"321.25");assert_eq!(initial.risk.as_ref().unwrap().cell("MaximumRiskPerTrade").text(),"1000");assert_eq!(initial.price.as_ref().unwrap().cell("ClosePrice").text(),"6050.25");assert_eq!(initial.trade.as_ref().unwrap().cell("TradeId").text(),"456");
 control(&b,"BrokerInitMode","WrongScope".into()).await;let partial=read(Duration::from_secs(1)).await.unwrap();assert!(partial.risk.is_none());assert!(partial.order.is_some());assert_eq!(partial.errors.len(),1);assert!(partial.errors[0].contains("Fund risk envelope"));
 control(&b,"BrokerInitMode","TradeNotFound".into()).await;let partial=read(Duration::from_secs(1)).await.unwrap();assert!(partial.trade.is_none());assert!(partial.errors.is_empty());
 control(&b,"BrokerInitMode","EodIncomplete".into()).await;let prior=read(Duration::from_secs(1)).await.unwrap();assert!(prior.errors.is_empty(),"{:?}",prior.errors);assert_eq!(prior.price.as_ref().unwrap().cell("ValueDate").text(),"2026-10-25");assert_eq!(control(&b,"BrokerInitCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
 control(&b,"BrokerInitMode","Hang".into()).await;
 let start=tokio::time::Instant::now();let timeout=read(Duration::from_millis(150)).await.unwrap();assert!(start.elapsed()<Duration::from_secs(1));assert!(timeout.errors.len()>=7);assert!(timeout.account.is_some());assert!(timeout.balances.is_none());
 control(&b,"BrokerInitMode","Disabled".into()).await;
}
