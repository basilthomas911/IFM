//! Equivalent of the managed ITradeFeedRecoveryApi over the existing versioned
//! MarketData Host NATS contract. Only these three per-trade operations are reachable.
use crate::backend::{contracts::*,transport::Backend};
use super::{business_context::BusinessContext,trade_monitor_queries::Scope};
use rmpv::Value;
#[derive(Clone)]
pub struct Client{backend:Backend,scope:String}
#[derive(Clone,Copy)]pub enum Action{Load,Enable,Disable}
fn raw(value:&Value)->Result<Vec<u8>,String>{let mut bytes=vec![];rmpv::encode::write_value(&mut bytes,value).map_err(|e|e.to_string())?;Ok(bytes)}
impl Client{
 pub fn new(backend:Backend,scope:&str)->Result<Self,String>{if scope.is_empty()||!scope.bytes().all(|b|b.is_ascii_alphanumeric()||b==b'-'){return Err("Invalid MarketData Host deployment scope".into())}Ok(Self{backend,scope:scope.into()})}
 pub async fn invoke(&self,action:Action,trade:Scope,context:&BusinessContext,id:uuid::Uuid,deadline:tokio::time::Instant)->Result<bool,String>{
  if id.is_nil(){return Err("Missing trade-feed request identity".into())}
  let operation=match action{Action::Load=>"LoadTradeFeedRecoveryAsync",Action::Enable=>"EnableTradeFeedRecoveryAsync",Action::Disable=>"DisableTradeFeedRecoveryAsync"};
  let signature=format!("{operation}(System.String,System.DateOnly,System.Threading.CancellationToken)");
  let arguments=Value::Array(vec![Value::Binary(raw(&trade.key()?.into())?),Value::Binary(raw(&date_only_wire(context.value_date))?),Value::Binary(vec![])]);
  let call=Value::Array(vec![signature.into(),arguments]);
  // API time supplies the absolute transport deadline too. Monotonic local
  // waits limit duration, but never choose a different business/value date.
  let until=context.as_of_utc+chrono::Duration::from_std(self.backend.default_timeout()).map_err(|_|"Invalid Host deadline")?;
  let request=Value::Array(vec![format!("api/{operation}").into(),Value::Binary(raw(&call)?),id.to_string().into(),timestamp_wire(until)]);
  let reply=self.backend.marketdata_control_until(&self.scope,&request,deadline).await?;
  let parts=reply.as_array().filter(|v|v.len()==2).ok_or("Invalid MarketData Host reply")?;
  if let Some(error)=parts[1].as_str(){return Err(format!("MarketData Host: {error}"))}
  let payload=parts[0].as_slice().ok_or("Missing MarketData Host payload")?;
  crate::backend::codec::decode(payload)?.as_bool().ok_or("Invalid trade live-feed recovery acknowledgement".into())
 }
}
