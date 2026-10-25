use super::*;
use ifm_ui_egui::{backend::schema::Registry,state::trades::TradeController,contract_editor::{ContractPublication,ContractResult},wire};
use serde_json::{Value as Json,json};
async fn mode(b:&Backend,verb:&str,name:&str){service_result(&b.request(&ActorSubject::query("NativeCompatibility",verb,"none"),&name.into(),None,None).await.unwrap()).unwrap();}
async fn act(state:&mut TradeController,b:&Backend,r:&Registry,generation:u64,action:&str,fields:Json,original:Option<&str>)->ContractPublication{
 state.execute(b,r,&json!({"schema":1,"view":"trade-orders","requestId":uuid::Uuid::new_v4().to_string(),"intent":action,"expectedGeneration":generation,"fields":fields,"originalId":original})).await.unwrap()
}
#[tokio::test]
#[ignore="Requires isolated C# compatibility responder"]
async fn composer_futures_publication_matches_unchanged_hybrid_then_edits_and_closes(){
 let b=Backend::connect(Settings{url:std::env::var("IFM_STANDALONE_TEST_NATS_URL").unwrap(),credentials_file:None,token:None,request_timeout:Duration::from_secs(2)}).await.unwrap();let r=Registry::compiled().unwrap();
 mode(&b,"TradeWorkspaceMode","ComposeFutures").await;mode(&b,"BrokerInitMode","Normal").await;mode(&b,"BrokerReadMode","Empty").await;mode(&b,"OptionChainMode","ComposeFutures").await;
 let mut state=TradeController::default();let first=act(&mut state,&b,&r,0,"load",json!({}),None).await;
 assert_eq!(first.outcomes[0].state,"Completed");
 let mut current=act(&mut state,&b,&r,first.generation,"compose",json!({"tradeId":"456"}),Some("123")).await;
 assert_eq!(current.outcomes[0].state,"Completed","{}",current.outcomes[0].message);
 let path=std::path::Path::new(env!("CARGO_MANIFEST_DIR")).join("tests/fixtures/backend/composer-futures-session.msgpack");
 let expected:ContractResult=wire::decode(&std::fs::read(path).unwrap()).unwrap();
 for(key,value)in expected.context {
  if key=="composerSessionId"{continue}
  assert_eq!(current.result.context.get(&key),Some(&value),"composer context {key}");
 }
 assert_eq!(current.result.choices["expiry"],expected.choices["expiry"]);
 let native:Vec<_>=current.result.rows.iter().filter(|row|["leg","qualification-report","account","broker-preview"].contains(&row.fields["kind"].as_str())).collect();
 assert_eq!(native.len(),expected.rows.len());
 for(actual,expected)in native.iter().zip(&expected.rows){
  assert_eq!(actual.id,expected.id);assert_eq!(actual.fields,expected.fields);
  let actual:Json=wire::decode(actual.data.as_ref().unwrap()).unwrap();let expected:Json=wire::decode(expected.data.as_ref().unwrap()).unwrap();assert_eq!(actual,expected,"row {}",expected);
 }
 let session=current.result.context["composerSessionId"].clone();assert!(uuid::Uuid::parse_str(&session).is_ok());
 current=act(&mut state,&b,&r,current.generation,"preview-draft",json!({"tradeId":"456","composerSessionId":session,"action":"Open","quantity":"1","price":"6,070.50","orderType":"Limit","algorithm":"None","timeInForce":"Day","pace":"Normal"}),Some("123")).await;
 assert_eq!(current.outcomes[0].state,"Completed");assert_eq!(current.result.context["price"],"6070.50");
 current=act(&mut state,&b,&r,current.generation,"preview-draft",json!({"tradeId":"456","composerSessionId":session,"quantity":"0"}),Some("123")).await;
 assert_eq!(current.outcomes[0].state,"Failed");assert_eq!(current.result.context["quantity"],"1");assert_eq!(current.result.context["price"],"6070.50");
 current=act(&mut state,&b,&r,current.generation,"refresh-execution",json!({"composerSessionId":uuid::Uuid::new_v4().simple().to_string()}),Some("123")).await;
 assert_eq!(current.outcomes[0].state,"Failed");
 current=act(&mut state,&b,&r,current.generation,"close",json!({}),Some("123")).await;assert_eq!(current.outcomes[0].state,"Completed");assert!(!current.result.context.contains_key("composerSessionId"));
 mode(&b,"BrokerInitMode","Disabled").await;mode(&b,"TradeWorkspaceMode","Disabled").await;mode(&b,"BrokerReadMode","Normal").await;mode(&b,"OptionChainMode","Normal").await;
}
