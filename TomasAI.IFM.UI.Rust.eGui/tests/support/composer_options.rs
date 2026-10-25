use super::*;
use ifm_ui_egui::{backend::schema::Registry,state::trades::TradeController,contract_editor::{ContractPublication,ContractResult},wire};
use serde_json::{Value as Json,json};
async fn control(b:&Backend,verb:&str,value:Value)->Value{service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&value,None,None).await.unwrap()).unwrap().clone()}
async fn act(state:&mut TradeController,b:&Backend,r:&Registry,generation:u64,action:&str,fields:Json)->ContractPublication{
 state.execute(b,r,&json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"fields":fields,"originalId":if action=="load"{Json::Null}else{json!("123")}})).await.unwrap()
}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn composer_options_match_hybrid_snapshot_and_keep_manual_clearing_and_owner_lifecycle(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();let r=Registry::compiled().unwrap();
 for(verb,name)in[("TradeWorkspaceMode","ComposeOptions"),("BrokerInitMode","Normal"),("BrokerReadMode","Empty"),("OptionChainMode","ComposeOptions")]{control(&b,verb,name.into()).await;}
 let mut state=TradeController::default();let mut current=act(&mut state,&b,&r,0,"load",json!({})).await;assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
 current=act(&mut state,&b,&r,current.generation,"compose",json!({"tradeId":"456"})).await;assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
 let path=std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend/composer-options-session.msgpack");let expected:ContractResult=wire::decode(&std::fs::read(path).unwrap()).unwrap();
 for(key,value)in expected.context{if key!="composerSessionId"{assert_eq!(current.result.context.get(&key),Some(&value),"composer context {key}");}}
 assert_eq!(current.result.choices["expiry"],expected.choices["expiry"]);
 let native:Vec<_>=current.result.rows.iter().filter(|row|["market","leg","market-metrics","qualification-report","account","broker-preview"].contains(&row.fields["kind"].as_str())).collect();assert_eq!(native.len(),expected.rows.len());
 for(actual,expected)in native.iter().zip(&expected.rows){assert_eq!(actual.id,expected.id);let actual:Json=wire::decode(actual.data.as_ref().unwrap()).unwrap();let data:Json=wire::decode(expected.data.as_ref().unwrap()).unwrap();assert_eq!(actual,data,"row {}",expected.id);}
 assert_eq!(current.result.context["price"],"-14");assert_eq!(current.result.context["canPlaceOrder"],"True");assert_eq!(control(&b,"OptionChainCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
 let session=current.result.context["composerSessionId"].clone();
 let fields=|width:&str|json!({"tradeId":"456","composerSessionId":session,"expiry":"2026-10-28","putDelta":"16","callDelta":"16","putWidth":width,"callWidth":"50","live":"true"});
 current=act(&mut state,&b,&r,current.generation,"chain",fields("50")).await;assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);assert_eq!(current.result.context["live"],"True");
 current=act(&mut state,&b,&r,current.generation,"chain",fields("0")).await;assert_eq!(current.outcomes[0].state,"Failed");assert_eq!(current.result.context["putWidth"],"50");assert_eq!(control(&b,"OptionChainCount",Value::Nil).await.as_array().unwrap()[0],Value::from(1));
 current=act(&mut state,&b,&r,current.generation,"clear-staged",json!({"composerSessionId":session})).await;assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);assert_eq!(current.result.context["canPlaceOrder"],"False");
 current=act(&mut state,&b,&r,current.generation,"refresh-chain",json!({"composerSessionId":session})).await;assert_eq!(current.outcomes[0].state,"Completed");assert!(current.result.rows.iter().all(|row|row.fields["kind"]!="leg"));
 current=act(&mut state,&b,&r,current.generation,"close",json!({})).await;assert_eq!(current.outcomes[0].state,"Completed");assert_eq!(control(&b,"OptionChainCount",Value::Nil).await.as_array().unwrap()[0],Value::from(0));
 for(verb,name)in[("BrokerInitMode","Disabled"),("TradeWorkspaceMode","Disabled"),("BrokerReadMode","Normal"),("OptionChainMode","Normal")]{control(&b,verb,name.into()).await;}
}
