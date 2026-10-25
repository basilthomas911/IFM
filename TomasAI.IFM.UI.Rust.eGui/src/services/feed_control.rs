//! Native shell feed controls. Terminal events and authoritative runtime reads
//! are observed concurrently under one deadline; commands are never retried.
use crate::{
    backend::{contracts::*, schema::Registry, transport::Backend},
    services::{actor_requests, business_context::BusinessContext, feed_status::FeedStatus},
    sidebar::FeedControl,
};
use rmpv::Value;
use std::collections::BTreeMap;

pub struct FeedObservation {
    pub session: BusinessContext,
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
        let matches=|observation:&FeedObservation|observation.status.running==expected && (!expected||observation.status.active_date==Some(value_date));
        if matches(&initial){return Ok(initial);}
        if expected && initial.session.market_state==0{return Err("Live market-data APIs are unavailable while the futures session is closed".into());}
        let root="TomasAI.IFM.Domain.MarketData.Feed.Shared.";
        let kind=format!("{root}Commands.{}MarketDataFeedCommand",if expected{"Start"}else{"Stop"});let (actor,verb)=registry.route(&kind)?;
        let subject=ActorSubject{kind:ActorKind::Command,name:actor.into(),verb:verb.into(),entity:if expected{"current".into()}else{initial.session.value_date.to_string()}};
        let mut fields=BTreeMap::from([("CommandId".into(),id.to_string().into()),("Subject".into(),subject.wire()),("PostEvents".into(),false.into()),
            ("EntityId".into(),Value::Array(vec![if expected{0.into()}else{date_only_wire(initial.session.value_date)}])),
            ("ErrorCode".into(),registry.error_id(&kind)?.into()),("RouteTo".into(),registry.enum_value("TomasAI.IFM.Shared.EventSourcing.BoundedContextName","MarketDataFeedBoundedContext")?.into())]);
        if !expected{fields.insert("ValueDate".into(),date_only_wire(initial.session.value_date));}
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
                    if let Ok(observation)=result{if matches(&observation){return Ok(observation);}}
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
