use super::*;
use ifm_ui_egui::{backend::schema::Registry,state::trades::TradeController,wire};use serde_json::{Value as Json,json};
async fn control(b:&Backend,verb:&str,name:&str){service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&name.into(),None,None).await.unwrap()).unwrap();}
fn intent(g:u64,action:&str,fields:Json)->Json{json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":g,"fields":fields,"originalId":"123"})}
fn doc(p:&ifm_ui_egui::contract_editor::ContractPublication)->Json{wire::decode(p.result.rows.iter().find(|r|r.fields["kind"]=="eod").unwrap().data.as_ref().unwrap()).unwrap()}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn trade_eod_uses_backend_amounts_and_reconciles_lost_ack_without_another_command(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();let r=Registry::compiled().unwrap();
 for mode in ["WrongDisplay","Normal","LostAck"]{
  for(verb,name)in[("TradeWorkspaceMode","Monitor"),("BrokerInitMode","Normal"),("EodMode",mode)]{control(&b,verb,name).await;}
  let mut state=TradeController::default();let mut current=state.execute(&b,&r,&intent(0,"load",json!({}))).await.unwrap();assert_eq!(current.result.context["canEndOfDay"],"True");
  current=state.execute(&b,&r,&intent(current.generation,"eod-open",json!({}))).await.unwrap();
  if mode=="WrongDisplay"{assert_eq!(current.outcomes[0].state,"Failed");assert_eq!(doc(&current)["canRun"],false);continue}
  assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);assert_eq!(doc(&current)["tradePnl"],"-50.25");assert_eq!(doc(&current)["fundBalance"],"123406.53");assert_eq!(doc(&current)["canRun"],true);
  current=state.execute(&b,&r,&intent(current.generation,"eod-run",json!({}))).await.unwrap();
  assert_eq!(current.outcomes[0].state,if mode=="LostAck"{"Unknown"}else{"Completed"},"{}",current.outcomes[0].message);
  if mode=="LostAck"{
   current=state.execute(&b,&r,&intent(current.generation,"eod-run",json!({}))).await.unwrap();assert_eq!(current.outcomes[0].state,"Failed");
   current=state.execute(&b,&r,&intent(current.generation,"eod-load",json!({"eodDate":"2026-10-26","reference":"Offline verification"}))).await.unwrap();assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
  }
  assert_eq!(doc(&current)["canRun"],false);
  let count=b.request(&ActorSubject::query("NativeCompatibility","EodCount","none"),&Value::Nil,None,None).await.unwrap();assert_eq!(service_result(&count).unwrap().as_array().unwrap()[0],Value::from(1));
 }
 for(verb,name)in[("EodMode","Disabled"),("TradeWorkspaceMode","Disabled"),("BrokerInitMode","Disabled")]{control(&b,verb,name).await;}
}
