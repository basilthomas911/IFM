use super::*;
use ifm_ui_egui::{backend::schema::Registry,services::broker_reads::*};
async fn mode(b:&Backend,name:&str){service_result(&b.request(&ActorSubject::query("NativeCompatibility","BrokerReadMode","none"),&name.into(),None,None).await.unwrap()).unwrap();}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn broker_reads_keep_account_order_and_execution_evidence_scoped(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();
 let r=Registry::compiled().unwrap();let scope=Scope{portfolio:1,fund:2,order:123};let deadline=||tokio::time::Instant::now()+Duration::from_secs(1);
 mode(&b,"Normal").await;
 let a=account(&b,&r,"IFM-EMULATOR-PAPER",deadline()).await.unwrap().unwrap();assert_eq!(a["Gate"],"Open");assert_eq!(a["DevelopmentQualificationsExempt"],true);
 let proof=evidence(&b,&r,&[scope,scope],deadline()).await.unwrap();assert_eq!(proof.orders.len(),2);assert_eq!(proof.executions.len(),1);assert_eq!(proof.executions[0]["CumulativeFilledQuantity"],1);
 let count=b.request(&ActorSubject::query("NativeCompatibility","BrokerReadCount","none"),&Value::Nil,None,None).await.unwrap();assert_eq!(service_result(&count).unwrap().as_array().unwrap()[0],Value::from(1));
 for fault in ["WrongScope","Duplicate","WrongAttempt","Unavailable"]{mode(&b,fault).await;assert!(evidence(&b,&r,&[scope],deadline()).await.is_err(),"{fault}");}
 mode(&b,"WrongAccount").await;assert!(account(&b,&r,"IFM-EMULATOR-PAPER",deadline()).await.is_err());
 mode(&b,"MissingExecution").await;let proof=evidence(&b,&r,&[scope],deadline()).await.unwrap();assert_eq!(proof.orders.len(),2);assert!(proof.executions.is_empty());
 mode(&b,"Normal").await;
}
