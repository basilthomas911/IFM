//! One native Blotter incarnation owns its detached draft and subscription.
//! Quotes/evidence/pricing are asynchronous API reads. Rendering only projects
//! the retained result, and closing releases this view's owner exclusively.
use super::*;
use crate::services::{broker_initialization::{self,Initialization},broker_reads::{self,Evidence,Scope},
 broker_preview::{self,Preview},broker_market_display,broker_preview_display,composer_market::{Market,Defaults},option_draft};
pub(super) struct Composer {
 pub(super) execution:crate::services::execution_context::Context,
 pub session:String,pub(super) scope:Scope,pub(super) trade:Json,market:Market,initialization:Option<Initialization>,
 evidence:Evidence,preview:Preview,totals:Json,quantity:i32,execution_action:String,
 order_type:String,algorithm:String,time_in_force:String,pace:String,status:String,
 report_path:String,report_hash:String,report_preview:String,
 accepted_orders:Vec<Json>,submission_unknown:bool,submission_error:String,
 pub(super) preparation:Option<crate::services::order_preparation::Prepared>,pub(super) confirmation_id:Option<uuid::Uuid>,
}
fn decimal(value:&Json)->Result<ExactDecimal,String>{ExactDecimal::parse(&value.as_str().map(str::to_string).unwrap_or_else(||value.to_string()))}
/// NumberStyles.Number compatibility for user draft fields. This is parsing,
/// never a conversion of a backend financial value through binary float.
pub(super) fn entered(text:&str)->Option<ExactDecimal>{
 let mut value=text.trim().replace(',',"");
 if value.ends_with('-')||value.ends_with('+'){
  let sign=value.pop()?;if value.starts_with(['-','+']){return None}if sign=='-'{value.insert(0,'-');}
 }
 if value.starts_with('+'){value.remove(0);}
 if value.starts_with('.') {value.insert(0,'0');}else if value.starts_with("-."){value.insert(1,'0');}
 if value.ends_with('.') {value.push('0');}
 ExactDecimal::parse(&value).ok()
}
fn flag(value:bool)->String{if value{"True"}else{"False"}.into()}
impl Composer {
 pub fn new(scope:Scope,trade:Json,underlying:Json,context:&BusinessContext,editable:bool,development:bool)->Result<Self,String>{
  scope.key()?;
  let strategy=trade["TradeType"].as_str().ok_or("Missing selected strategy")?.to_string();let execution_action=if trade["PrimaryTrade"]==false{"Close"}else{"Open"}.into();
  Ok(Self{execution:crate::services::execution_context::Context::from_env()?,session:uuid::Uuid::new_v4().simple().to_string(),scope,trade,market:Market::new(underlying,strategy,context,editable,development),
   initialization:None,evidence:Default::default(),preview:Default::default(),totals:Json::Null,quantity:1,execution_action,order_type:"Limit".into(),algorithm:"None".into(),time_in_force:"Day".into(),pace:"Normal".into(),status:String::new(),report_path:String::new(),report_hash:String::new(),report_preview:String::new(),preparation:None,confirmation_id:None,accepted_orders:vec![],submission_unknown:false,submission_error:String::new()})
 }
 pub async fn initialize(&mut self,backend:&Backend,registry:&Registry,context:&BusinessContext,deadline:tokio::time::Instant)->Result<(),String>{
  let contract=self.market.underlying["ContractId"].as_str().ok_or("Missing underlying identity")?.to_string();
  let trade_id=self.trade["TradeId"].as_i64().and_then(|v|i32::try_from(v).ok()).filter(|v|*v>0).ok_or("Invalid selected trade")?;
  let future=self.market.strategy=="FuturesOutright";
  let requested=crate::backend::date_input::parse(self.trade["RequestedTradeDate"].as_str().ok_or("Missing requested trade date")?)?;
  let through=crate::backend::date_input::parse(self.trade["RequestedMaturityDate"].as_str().unwrap_or(&requested.to_string()))?;
  let initializing=broker_initialization::load(backend,registry,context,self.scope,trade_id,&contract,future,&self.execution.account_alias,deadline);
  let selecting=async{
   if future {
    let side=if self.trade["TradeAction"]=="Sell"{-1}else{1};
    self.market.draft.legs=vec![option_draft::Selected{quote:broker_preview::Leg{contract:contract.clone(),side,strike:None,is_call:false,is_future:true,bid:None,ask:None,delta:None,vega:None,multiplier:Some(decimal(&self.market.underlying["Multiplier"])?)},role:if side<0{"-Future"}else{"+Future"}.into(),quote_at:Json::Null,frozen:false}];Ok(())
   }else{self.market.initialize(backend,registry,context,requested,through,deadline).await}
  };
  let(initialized,selected)=tokio::join!(initializing,selecting);
  self.initialization=Some(initialized?);selected?;
  self.refresh_evidence(backend,registry,deadline).await?;
  if self.trade["TradeState"]!="NewTrade" {
   let previous=self.market.expiry;self.restore_stored_legs()?;
   if self.market.draft.legs.iter().any(|leg|!leg.quote.is_future){
    if self.market.expiry!=previous{self.market.lease.release(backend,registry,deadline).await?;}
    // Stored contract IDs remain authoritative. Refresh their quotes without
    // auto-selecting a different trade from the current chain.
    self.market.refresh(backend,registry,deadline).await?;
   }
  }
  self.refresh_preview(backend,registry,deadline).await?;
  self.refresh_account(backend,registry,deadline).await
 }
 pub(super) async fn refresh_evidence(&mut self,backend:&Backend,registry:&Registry,deadline:tokio::time::Instant)->Result<(),String>{
  let mut scopes=self.accepted_orders.iter().map(|order|{
   let number=|key:&str|order["Id"][key].as_i64().and_then(|n|i32::try_from(n).ok()).filter(|n|*n>0).ok_or("Invalid accepted evidence scope");
   Ok(Scope{portfolio:number("PortfolioId")?,fund:number("FundId")?,order:number("OrderId")?})
  }).collect::<Result<Vec<_>,String>>()?;
  scopes.push(self.scope);
  if let Some(order)=self.trade["ExecutionOrderId"].as_i64().and_then(|v|i32::try_from(v).ok()).filter(|v|*v>0){scopes.push(Scope{order,..self.scope});}
  self.evidence=broker_reads::evidence(backend,registry,&scopes,deadline).await?;Ok(())
 }
 pub(super) fn broker_order(&self,key:&str)->Result<(Json,Option<Json>),String>{
  let rows=self.evidence.orders.iter().filter(|o|broker_reads::identity(&o["Id"]).is_ok_and(|id|id==key)).collect::<Vec<_>>();
  if rows.len()!=1{return Err("Select a current broker order".into())}
  let order=rows[0].clone();
  let execution=self.evidence.executions.iter().find(|e|e["TradeOrderId"]==order["Id"]["Execution"]["TradeOrder"]&&e["ExecutionAttemptId"]==order["Id"]["Execution"]["ExecutionAttemptId"]).cloned();
  Ok((order,execution))
 }
 fn restore_stored_legs(&mut self)->Result<(),String>{
  self.market.draft.manual=true;self.market.draft.legs.clear();
  let id=self.trade["ExecutionTradeId"].as_i64().filter(|id|*id>0).or_else(||self.trade["TradeId"].as_i64()).ok_or("Missing stored trade identity")?;
  let mut orders:Vec<_>=self.evidence.orders.iter().collect();orders.sort_by(|a,b|b["ChangedAtUtc"].as_str().cmp(&a["ChangedAtUtc"].as_str()));
  let component=orders.into_iter().filter_map(|o|o["Order"]["Components"].as_array()).flatten().find(|c|c["ReservedTradeId"]==id);
  let Some(component)=component else{return Ok(())};
  let legs=component["Legs"].as_array().ok_or("Missing stored component legs")?;
  let mut quantity=i32::MAX;
  for row in legs{
   let signed=row["SignedQuantity"].as_i64().and_then(|v|i32::try_from(v).ok()).ok_or("Invalid stored leg quantity")?;
   quantity=quantity.min(signed.checked_abs().ok_or("Stored leg quantity overflow")?);
   let future=row["AssetFamily"]=="Futures";let call=row["PutCall"]==1;let side=if signed<0{-1}else{1};
   let role=if future{format!("{}Future",if side<0{"-"}else{"+"})}else{format!("{}{}",if side<0{"-S"}else{"+L"},if call{"C"}else{"P"})};
   self.market.draft.legs.push(option_draft::Selected{quote:broker_preview::Leg{contract:row["ContractId"].as_str().ok_or("Missing stored contract")?.into(),side,strike:if row["Strike"].is_null(){None}else{Some(decimal(&row["Strike"])?)},is_call:call,is_future:future,bid:None,ask:None,delta:None,vega:None,multiplier:if row["CashMultiplier"].is_null(){None}else{Some(decimal(&row["CashMultiplier"])?) }},role,quote_at:Json::Null,frozen:false});
  }
  if let Some(expiry)=legs.iter().filter_map(|leg|leg["Expiry"].as_str()).map(crate::backend::date_input::parse).collect::<Result<Vec<_>,_>>()?.into_iter().max(){self.market.expiry=expiry;}
  self.quantity=if quantity==i32::MAX{1}else{quantity.max(1)};
  self.market.draft.entered_price=if component["SignedNetDebitLimit"].is_null(){None}else{Some(decimal(&component["SignedNetDebitLimit"])?) };
  Ok(())
 }
 async fn refresh_preview(&mut self,backend:&Backend,registry:&Registry,deadline:tokio::time::Instant)->Result<(),String>{
  let legs=self.market.draft.legs.iter().map(|l|l.quote.clone()).collect::<Vec<_>>();
  let reference=self.initialization.as_ref().and_then(|i|i.price.as_ref()).map(|p|decimal(&p["ClosePrice"])).transpose()?;
  self.totals=self.preview.load(backend,registry,&legs,self.quantity,self.market.draft.entered_price.as_ref(),reference.as_ref(),deadline).await?;Ok(())
 }
 pub async fn execute(&mut self,backend:&Backend,registry:&Registry,context:&BusinessContext,intent:&Json,deadline:tokio::time::Instant)->Result<String,String>{
  if self.preparation.is_some()&&intent["intent"]=="refresh-chain"{return Ok("Awaiting order confirmation".into())}
  if self.preparation.is_some()&&!matches!(intent["intent"].as_str(),Some("account"|"refresh-execution"|"close-blotter")){return Err("Review or cancel the prepared confirmation before changing its draft".into())}
  match intent["intent"].as_str().ok_or("Missing Blotter action")? {
   "refresh-chain"=>{if self.market.live&&self.market.draft.raw.is_some(){self.market.refresh(backend,registry,deadline).await?;}},
   "refresh-execution"=>{self.refresh_evidence(backend,registry,deadline).await?;},
   "clear-staged"=>{if !self.market.editable{return Err("Market selection is read-only".into())}self.market.draft.clear();},
   "remove-leg"=>{if !self.market.editable{return Err("Market selection is read-only".into())}self.market.draft.remove(field(intent,"contractId"));},
   "select-leg"=>{
    let d=self.market.defaults.as_ref().ok_or("Applied startup defaults are unavailable")?;let multiplier=decimal(&self.market.underlying["Multiplier"])?;
    self.market.draft.select(field(intent,"contractId"),field(intent,"role"),&option_draft::Settings{strategy:&self.market.strategy,call_delta:&d.call_delta,put_delta:&d.put_delta,call_width:&d.call_width,put_width:&d.put_width,multiplier:&multiplier,live:self.market.live,development:self.market.development,editable:self.market.editable})?;
   },
   "chain"=>{
    let number=|key:&str|entered(field(intent,key)).ok_or(format!("Invalid {key}"));
    let expiry=crate::backend::date_input::parse(field(intent,"expiry"))?;
    let defaults=Defaults{call_delta:number("callDelta")?,put_delta:number("putDelta")?,call_width:number("callWidth")?,put_width:number("putWidth")?};
    self.market.change(backend,registry,context,expiry,field(intent,"live")=="true",defaults,deadline).await?;
   },
   "preview-draft"=>{
    let quantity=field(intent,"quantity").parse::<i32>().ok().filter(|v|(1..=100000).contains(v)).ok_or("Quantity must be between 1 and 100000")?;
    let order_type=field(intent,"orderType");let algorithm=field(intent,"algorithm");let tif=field(intent,"timeInForce");let pace=field(intent,"pace");
    registry.enum_value("TomasAI.IFM.Domain.Trade.Shared.BrokerOrderType",order_type)?;
    registry.enum_value("TomasAI.IFM.Domain.Trade.Shared.BrokerAlgorithm",algorithm)?;
    if !["Day","GTC"].contains(&tif)||!["Normal","Patient","Urgent"].contains(&pace){return Err("Invalid execution selection".into())}
    self.quantity=quantity;if let Some(price)=entered(field(intent,"price")){self.market.draft.entered_price=Some(price);}
    self.order_type=order_type.into();self.algorithm=algorithm.into();self.time_in_force=tif.into();self.pace=pace.into();
    if ["Open","Close"].contains(&field(intent,"action")){self.execution_action=field(intent,"action").into();}
   },
   "account"=>{
    self.report_path.clear();self.report_hash.clear();self.report_preview.clear();
    let trade_id=self.trade["TradeId"].as_i64().and_then(|v|i32::try_from(v).ok()).ok_or("Invalid trade")?;
    self.initialization=Some(broker_initialization::load(backend,registry,context,self.scope,trade_id,self.market.underlying["ContractId"].as_str().ok_or("Missing underlying")?,self.market.strategy=="FuturesOutright",&self.execution.account_alias,deadline).await?);
   },
   "qualification-report"=>{
    use sha2::Digest;
    let path=std::path::absolute(field(intent,"reportPath")).map_err(|e|format!("Invalid report path: {e}"))?;
    let maximum=crate::backend::codec::MAX_MESSAGE/4;
    let metadata=tokio::fs::metadata(&path).await.map_err(|e|format!("Cannot inspect qualification report: {e}"))?;
    if !metadata.is_file()||metadata.len()>maximum as u64{return Err("Qualification report exceeds the 4 MiB display preview bound".into())}
    let bytes=tokio::fs::read(&path).await.map_err(|e|format!("Cannot read qualification report: {e}"))?;
    if bytes.len()>maximum{return Err("Qualification report grew beyond the display preview bound".into())}
    self.report_path=path.to_string_lossy().into();self.report_hash=format!("{:x}",sha2::Sha256::digest(&bytes));self.report_preview=String::from_utf8_lossy(&bytes).into();
   },
   "close-blotter"=>{self.market.close(backend,registry,deadline).await?;return Ok("Blotter closed".into())},
   action=>return Err(format!("Blotter action {action} is not yet implemented")),
  }
  self.refresh_preview(backend,registry,deadline).await?;self.refresh_account(backend,registry,deadline).await?;Ok("Blotter updated".into())
 }
 pub async fn refresh_account(&mut self,backend:&Backend,registry:&Registry,deadline:tokio::time::Instant)->Result<(),String>{
  let data=self.initialization.get_or_insert_with(Initialization::default);
  match broker_reads::account(backend,registry,&self.execution.account_alias,deadline).await{
   Ok(value)=>data.account=value,
   Err(error)=>{data.account=None;self.status=format!("Account evidence unavailable: {error}");},
  }
  Ok(())
 }
 pub fn append(&self,result:&mut ContractResult,context:&BusinessContext)->Result<(),String>{
  if let Some(document)=self.confirmation_document()?{result.documents.insert("confirmation".into(),crate::wire::encode_value(&document)?.into());}
  if let Some(market)=&self.market.draft.display{result.rows.push(row("market",0,broker_market_display::market(market)?)?);}
  let mut legs:Vec<_>=self.market.draft.legs.iter().collect();legs.sort_by(|a,b|match(&a.quote.strike,&b.quote.strike){(Some(a),Some(b))=>crate::services::option_selection::compare(b,a),_=>std::cmp::Ordering::Equal}.then_with(||b.quote.is_call.cmp(&a.quote.is_call)));
  for(index,leg)in legs.into_iter().enumerate(){result.rows.push(row("leg",index,broker_market_display::leg(leg)?)?);}
  if let Some(chain)=&self.market.draft.raw{if !self.totals.is_null(){result.rows.push(row("market-metrics",0,crate::services::broker_market_metrics::document(chain,&self.market.underlying,&self.totals["ResolvedTickIncrement"],self.market.live)?)?);}}
  if !self.totals.is_null(){
   let mut preview=broker_preview_display::document(&self.market.draft.legs,&self.totals,self.initialization.as_ref())?;
   if let Some(account)=self.initialization.as_ref().and_then(|i|i.account.as_ref()){
    result.rows.push(row("qualification-report",0,self.report_document())?);
    result.rows.push(row("account",0,broker_market_display::account(account)?)?);
    preview["fields"]["Route check"]=if account["DevelopmentQualificationsExempt"]==true{"Development emulator - qualification exempt".into()}else{format!("{} / {}",text(&account["QualificationStatus"]),text(&account["Gate"])).into()};
   }
   result.rows.push(row("broker-preview",0,preview)?);
  }
  if self.initialization.as_ref().and_then(|i|i.account.as_ref()).is_none() || self.totals.is_null(){result.rows.push(row("qualification-report",0,self.report_document())?);}
  result.choices.insert("expiry".into(),self.market.expirations.iter().filter_map(|e|e["ExpiryDate"].as_str().map(str::to_string)).collect::<std::collections::BTreeSet<_>>().into_iter().collect());
  let defaults=self.market.defaults.as_ref();let loaded=defaults.is_some()||self.market.strategy=="FuturesOutright";
  let quantity=self.market.draft.legs.len();let required=if self.market.strategy=="FuturesOutright"{1}else if self.market.strategy.ends_with("IronCondor"){4}else{2};
  let can_place=self.accepted_orders.is_empty()&&!self.submission_unknown&&self.preparation.is_none()&&self.market.editable&&loaded&&!self.totals.is_null()&&quantity==required&&self.market.draft.legs.iter().all(|l|l.quote.is_future||l.quote.side!=0&&l.quote.bid.is_some()&&l.quote.ask.is_some());
  for(key,value)in [
   ("composerSessionId",self.session.clone()),("expiry",self.market.expiry.to_string()),("compositionType",self.market.strategy.clone()),("brokerMode",self.execution.environment.clone()),("composerStatus",if self.status.is_empty(){self.market.status.clone()}else{self.status.clone()}),
   ("effectiveDate",text(&self.trade["RequestedTradeDate"])),("editable",flag(self.market.editable&&loaded&&self.preparation.is_none())),("submitted",flag(!self.accepted_orders.is_empty())),("submissionUnconfirmed",flag(self.submission_unknown)),("submissionError",self.submission_error.clone()),("live",flag(self.market.live)),("quantity",self.quantity.to_string()),("price",text(&self.totals["OrderPrice"])),("previewKey",self.market.draft.legs.iter().map(|l|format!("{}{}",l.quote.contract,l.role)).collect::<Vec<_>>().join("|")),
   ("callDelta",defaults.map(|d|d.call_delta.as_str()).unwrap_or("16").into()),("putDelta",defaults.map(|d|d.put_delta.as_str()).unwrap_or("16").into()),("callWidth",defaults.map(|d|d.call_width.as_str()).unwrap_or("50").into()),("putWidth",defaults.map(|d|d.put_width.as_str()).unwrap_or("50").into()),
   ("underlyingReferencePrice",self.initialization.as_ref().and_then(|d|d.price.as_ref()).map(|p|text(&p["ClosePrice"])).unwrap_or_default()),("underlying",text(&self.market.underlying["ContractId"])),("symbol",text(&self.market.underlying["Symbol"])),("multiplier",text(&self.market.underlying["Multiplier"])),("venue",text(&self.market.underlying["Exchange"])),("valueDate",context.value_date.to_string()),("executionAction",self.execution_action.clone()),("canPlaceOrder",flag(can_place)),
  ]{result.context.insert(key.into(),value);}
  Ok(())
 }
 pub(super) fn accept_submission(&mut self,orders:Vec<Json>,message:&str){self.accepted_orders=orders;self.submission_unknown=false;self.submission_error.clear();self.preparation=None;self.confirmation_id=None;self.status=message.into();}
 pub(super) fn mark_submission_unknown(&mut self,orders:Vec<Json>,message:&str){self.accepted_orders=orders;self.submission_unknown=true;self.submission_error=message.into();self.preparation=None;self.confirmation_id=None;self.status=message.into();}
 pub(super) fn reject_submission(&mut self,error:&str){self.preparation=None;self.confirmation_id=None;self.submission_unknown=false;self.submission_error=error.into();self.status=error.into();}
 pub(super) async fn prepare_confirmation(&mut self,backend:&Backend,registry:&Registry,context:&BusinessContext,intent:&Json,deadline:tokio::time::Instant)->Result<String,String>{
  if self.preparation.is_some(){return Err("A confirmation is already pending".into())}
  if !self.market.editable||self.execution_action!=if self.trade["PrimaryTrade"]==false{"Close"}else{"Open"}{return Err("This New Trade screen submits opening orders only".into())}
  // Parse and publish the same execution controls as a preview before freezing
  // the order. Human review itself never holds this async controller lock.
  let mut preview=intent.clone();preview["intent"]="preview-draft".into();self.execute(backend,registry,context,&preview,deadline).await?;
  if !self.market.draft.legs.iter().all(|leg|leg.quote.is_future||leg.quote.bid.as_ref().is_some_and(|v|crate::services::option_selection::compare(v,&ExactDecimal::parse("0").unwrap()).is_gt())&&leg.quote.ask.as_ref().zip(leg.quote.bid.as_ref()).is_some_and(|(ask,bid)|crate::services::option_selection::compare(ask,bid).is_ge())){return Err("Complete quotes are unavailable for the selected legs".into())}
  let legs=self.market.draft.legs.iter().map(|leg|leg.quote.clone()).collect::<Vec<_>>();
  let quote_inputs=serde_json::to_string(&self.market.draft.legs.iter().map(|leg|broker_market_display::leg(leg)).collect::<Result<Vec<_>,_>>()?).map_err(|e|e.to_string())?;
  let price=entered(field(intent,"price")).ok_or("Enter an explicit order price")?;
  let draft=crate::services::order_preparation::Draft{scope:self.scope,trade:self.trade["TradeId"].as_i64().and_then(|v|i32::try_from(v).ok()).ok_or("Invalid setup Trade identity")?,underlying:text(&self.market.underlying["ContractId"]),strategy:if self.market.strategy=="FuturesOutright"{"FuturesOutright"}else if self.market.strategy.ends_with("IronCondor"){"IronCondor"}else{"VerticalSpread"}.into(),expiry:self.market.expiry,quantity:self.quantity,price,legs,quote_inputs,order_type:self.order_type.clone(),algorithm:self.algorithm.clone(),tif:self.time_in_force.clone(),pace:self.pace.clone(),composition:uuid::Uuid::new_v4(),workflow:uuid::Uuid::new_v4(),component:uuid::Uuid::new_v4()};
  let prepared=draft.prepare_with_context(backend,registry,&self.execution,deadline).await?;
  if prepared.result["Candidate"]["ValueDate"]!=context.value_date.to_string(){return Err("Backend preparation crossed the API ValueDate".into())}
  self.confirmation_id=Some(uuid::Uuid::new_v4());self.preparation=Some(prepared);Ok("Awaiting order confirmation".into())
 }
 pub(super) fn confirmation_document(&self)->Result<Option<Json>,String>{
  let(Some(prepared),Some(id))=(&self.preparation,self.confirmation_id)else{return Ok(None)};
  let commissions=prepared.result["LegCommissions"].as_array().ok_or("Missing prepared commissions")?;
  let legs=self.market.draft.legs.iter().zip(commissions).map(|(leg,commission)|json!({"side":if leg.quote.side>0{"Buy"}else{"Sell"},"role":leg.role,"contractId":leg.quote.contract,"legCommission":commission})).collect::<Vec<_>>();
  Ok(Some(json!({"id":id.to_string(),"submitting":false,"order":{"orderQuantity":self.quantity,"orderPrice":prepared.result["Candidate"]["Components"][0]["SignedNetDebitLimit"],"orderAmount":prepared.result["CashAmount"],"commission":prepared.result["TotalCommission"],"totalAmount":prepared.result["TotalAmount"]},"legs":legs})))
 }
 pub(super) fn append_evidence(&self,result:&mut ContractResult,pending:&BTreeMap<String,crate::services::broker_order_commands::Operation>)->Result<(),String>{
  use crate::services::broker_order_ui as ui;
  for(index,order)in self.accepted_orders.iter().enumerate(){result.rows.push(row("submitted-order",index,ui::display(order))?);}
  for order in &self.evidence.orders{let key=broker_reads::identity(&order["Id"])?;result.rows.push(row("broker-order",&key,ui::display(order))?);}
  let mut fills=vec![];
  for execution in &self.evidence.executions{
   let scope=&execution["TradeOrderId"];let key=format!("{}.{}.{}.{}",text(&scope["PortfolioId"]),text(&scope["FundId"]),text(&scope["OrderId"]),text(&execution["ExecutionAttemptId"]).replace('-',""));
   result.rows.push(row("execution",key,ui::display(execution))?);
   if let Some(rows)=execution["Fills"].as_array(){fills.extend(rows);}
  }
  for(index,fill)in fills.iter().enumerate(){result.rows.push(row("fill",index,ui::display(fill))?);}
  for order in &self.evidence.orders{
   let key=broker_reads::identity(&order["Id"])?;let(_,execution)=self.broker_order(&key)?;
   result.rows.push(row("broker-caption",&key,json!({"text":ui::caption(order)?}))?);
   let summary=ui::summary(order,execution.as_ref())?;
   result.rows.push(row("broker-properties",&key,ui::properties(&summary,ui::SUMMARY_NAMES)?)?);
   result.rows.push(row("broker-summary",&key,summary)?);
   result.rows.push(row("broker-mutation",&key,ui::mutation(order,execution.as_ref(),pending.values().any(|op|op.order_key==key))?)?);
  }
  let mut seen=std::collections::BTreeSet::new();
  for fill in fills{let key=text(&fill["ExecutionFillId"]);if seen.insert(key.clone()){result.rows.push(row("fill-properties",key,ui::properties(fill,ui::FILL_NAMES)?)?);}}
  Ok(())
 }
 pub async fn close(&mut self,backend:&Backend,registry:&Registry,deadline:tokio::time::Instant)->Result<(),String>{self.market.close(backend,registry,deadline).await}
 fn report_document(&self)->Json{json!({"path":self.report_path,"hash":self.report_hash,"preview":self.report_preview})}
}
