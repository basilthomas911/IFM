//! Native shell feed controls. Terminal events and authoritative runtime reads
//! are observed concurrently under one deadline; commands are never retried.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    services::{actor_requests, business_context::BusinessContext, feed_status::FeedStatus},
    sidebar::FeedControl,
};
use rmpv::Value;
use std::collections::BTreeMap;

const FEED_CONTRACTS: &str = "TomasAI.IFM.Domain.MarketData.Feed.Shared.";

/// Resolve the session on the API and forward its committed assignments intact.
async fn prepare_start(
    backend: &Backend,
    registry: &Registry,
    id: uuid::Uuid,
    route_to: i64,
    deadline: tokio::time::Instant,
) -> Result<(chrono::NaiveDate, Value), String> {
    let kind = format!("{FEED_CONTRACTS}ProducerOperations.PrepareMarketDataFeedSessionCommand");
    let (actor, verb) = registry.route(&kind)?;
    let subject = ActorSubject {
        kind: ActorKind::Function,
        name: actor.into(),
        verb: verb.into(),
        entity: id.simple().to_string(),
    };
    let id = id.to_string();
    let body = registry.encode(&kind, &BTreeMap::from([
        ("CommandId".into(), id.clone().into()),
        ("Subject".into(), subject.wire()),
        ("EntityId".into(), Value::Array(vec![subject.entity.clone().into()])),
        ("ErrorCode".into(), registry.error_id(&kind)?.into()),
        ("RouteTo".into(), route_to.into()),
    ]))?;
    let reply = backend.request_until(&subject, &body, deadline, None).await?;
    let [completed, failed] = service_result(&reply)?
        .as_array()
        .map(Vec::as_slice)
        .ok_or("Invalid feed preparation reply")?
    else {
        return Err("Invalid feed preparation reply".into());
    };
    if completed.is_nil() && !failed.is_nil() {
        let kind = format!("{FEED_CONTRACTS}ProducerOperations.FeedSessionPreparationFailedEvent");
        let message = registry.wire_field(&kind, "ErrorMessage", failed)?;
        return Err(message.as_str().unwrap_or("Feed session preparation failed").into());
    }
    let kind = format!("{FEED_CONTRACTS}ProducerOperations.FeedSessionPreparationCompletedEvent");
    if !failed.is_nil()
        || registry.wire_field(&kind, "CommandId", completed)?.as_str() != Some(id.as_str())
    {
        return Err("Feed preparation reply has an invalid operation identity or outcome".into());
    }
    let value_date = date_only(registry.wire_field(&kind, "ValueDate", completed)?)?;
    let contracts = registry.wire_field(&kind, "Contracts", completed)?;
    if contracts.as_array().is_none_or(Vec::is_empty) {
        return Err("Feed session preparation returned no contract assignments".into());
    }
    Ok((value_date, contracts.clone()))
}

/// Feed Observation used by the feed control component.
pub struct FeedObservation {
    /// Session associated with this feed observation.
    pub session: BusinessContext,
    /// Current status text or state exposed by this component.
    pub status: FeedStatus,
}
impl FeedObservation {
    /// Read business time and runtime state as one bounded shell observation.
    pub async fn load(backend: &Backend) -> Result<Self, String> {
        tokio::time::timeout(backend.default_timeout(), async {
            let session = BusinessContext::load(backend).await?;
            let status = FeedStatus::load(backend).await?;
            Ok::<_, String>(Self { session, status })
        })
        .await
        .map_err(|_| "Market-feed observation deadline expired".to_string())?
    }


    /// Same enablement rule as the hybrid: stop an active feed, or start while
    /// the API-owned market session is open. Busy disables overlapping writes.
    pub fn control(&self, busy: bool) -> FeedControl {
        FeedControl {
            ready: true,
            can_toggle: (self.status.running || self.session.market_state != 0) && !busy,
            busy,
            active: self.status.running,
        }
    }
}


/// Set an explicit desired state. Read-after-write reconciliation handles a
/// lost terminal event without issuing a second command or inventing success.
pub async fn set_running(
    backend: &Backend,
    registry: &Registry,
    expected: bool,
    id: uuid::Uuid,
) -> Result<FeedObservation, String> {
    if id.is_nil() {
        return Err("Feed command identity is required".into());
    }
    let deadline = tokio::time::Instant::now() + backend.default_timeout();
    tokio::time::timeout_at(deadline,async {
        let mut initial=FeedObservation::load(backend).await?;
        let value_date=initial.session.value_date;
        let matches=|observation:&FeedObservation, date|observation.status.running==expected && (!expected||observation.status.active_date==Some(date));
        if matches(&initial,value_date){return Ok(initial);}
        if expected && initial.session.market_state==0{return Err("Live market-data APIs are unavailable while the futures session is closed".into());}
        let root=FEED_CONTRACTS;
        let route_to=registry.enum_value("TomasAI.IFM.Shared.EventSourcing.BoundedContextName","MarketDataFeedBoundedContext")?;
        let (value_date, prepared_contracts) = if expected {
            let (date, contracts) = prepare_start(backend, registry, id, route_to, deadline).await?;
            (date, Some(contracts))
        } else {
            (value_date, None)
        };
        let kind=format!("{root}Commands.{}MarketDataFeedCommand",if expected{"Start"}else{"Stop"});let (actor,verb)=registry.route(&kind)?;
        let subject=ActorSubject{kind:ActorKind::Command,name:actor.into(),verb:verb.into(),entity:value_date.to_string()};
        let mut fields=BTreeMap::from([("CommandId".into(),id.to_string().into()),("Subject".into(),subject.wire()),("PostEvents".into(),false.into()),
            ("EntityId".into(),Value::Array(vec![date_only_wire(value_date)])),
            ("ErrorCode".into(),registry.error_id(&kind)?.into()),("RouteTo".into(),route_to.into())]);
        if let Some(contracts) = prepared_contracts {
            fields.insert("PreparedContracts".into(), contracts);
        } else {
            fields.insert("ValueDate".into(),date_only_wire(value_date));
        }
        let body=registry.encode(&kind,&fields)?;let id=id.to_string();
        let success=format!("{root}Events.MarketDataFeed{}CompleteEvent",if expected{"Started"}else{"Stopped"});
        let failure=format!("{root}Events.MarketDataFeed{}FailEvent",if expected{"Started"}else{"Stopped"});
        let command=actor_requests::command(backend,registry,&subject,&body,&success,&failure,&id,deadline);tokio::pin!(command);
        let mut completed=false;
        let mut tick=tokio::time::interval(std::time::Duration::from_millis(250));tick.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
        let mut reading:Option<std::pin::Pin<Box<dyn std::future::Future<Output=Result<FeedObservation,String>>+Send+'_>>>=None;
        loop {
            tokio::select! {
                result=&mut command,if !completed=>{
                    match result {Ok(_)=>{initial.status.running=expected;initial.status.active_date=if expected{Some(value_date)}else{None};return Ok(initial);},Err(error) if error.starts_with("OutcomeUnknown:")=>completed=true,Err(error)=>return Err(error)}
                },
                _=tick.tick(),if reading.is_none()=>{reading=Some(Box::pin(FeedObservation::load(backend)));},
                result=async{reading.as_mut().unwrap().await},if reading.is_some()=>{
                    reading=None;
                    if let Ok(observation)=result{if matches(&observation,value_date){return Ok(observation);}}
                },
            }
        }
    }).await.map_err(|_|"OutcomeUnknown: market-feed command deadline expired; authoritative runtime did not confirm completion".to_string())?
}


/// Resolve a toolbar click against current API state immediately before sending.
pub async fn toggle(
    backend: &Backend,
    registry: &Registry,
    id: uuid::Uuid,
) -> Result<FeedObservation, String> {
    tokio::time::timeout(backend.default_timeout(), async {
        let observation = FeedObservation::load(backend).await?;
        if !observation.control(false).can_toggle {
            return Err("Market feed action is unavailable while the session is closed".into());
        }
        set_running(backend, registry, !observation.status.running, id).await
    })
    .await
    .map_err(|_| {
        "OutcomeUnknown: market-feed toggle deadline expired; reconcile runtime before retrying"
            .to_string()
    })?
}
