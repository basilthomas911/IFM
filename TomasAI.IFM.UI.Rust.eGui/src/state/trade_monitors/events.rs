use super::*;
use crate::backend::codec;
use crate::models::{trade_contracts as typed, wire_types::FromBackend};
use futures_util::StreamExt;
impl Hub {
    pub(super) async fn start_events(
        &self,
        backend: Backend,
        registry: Arc<Registry>,
        key: &str,
        data: Arc<Mutex<data::Monitor>>,
        deadline: tokio::time::Instant,
    ) -> Result<Vec<tokio::task::JoinHandle<()>>, String> {
        let mut realtime = backend.subscribe("Notify", "TradeRealtimeSnapshot").await?;
        let mut positions = backend
            .subscribe("Event", "FuturesIronCondorTradePositionEvent")
            .await?;
        let mut plans = backend
            .subscribe("Event", "IronCondorTradePlanFunction")
            .await?;
        let mut feeds = backend.subscribe("Event", "MarketDataFeedEvent").await?;
        backend.flush_until(deadline).await?;
        let weak = Arc::downgrade(&self.state);
        let updates = self.updates.clone();
        let host_scope = self.host_scope.clone();
        let key = key.to_string();
        let incarnation = data.lock().map_err(|_| "Monitor lock failed")?.incarnation;
        let task = tokio::spawn(async move {
            let mut dirty = false;
            let mut tick = tokio::time::interval(std::time::Duration::from_millis(50));
            tick.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
            let mut connection = backend.watch_connection();
            let mut refresh: Option<
                tokio::task::JoinHandle<
                    Result<(BusinessContext, Option<typed::TradeRealtimeSnapshot>), String>,
                >,
            > = None;
            loop {
                let incoming = tokio::select! {
                 message=realtime.next()=>Some((0,message)),message=positions.next()=>Some((1,message)),message=plans.next()=>Some((2,message)),message=feeds.next()=>Some((3,message)),
                 _=tick.tick()=>{
                  if dirty{if let Some(state)=weak.upgrade(){let hub=Hub{state,updates:updates.clone(),host_scope:host_scope.clone()};let _=hub.publish();}else{break}dirty=false;}
                  None
                 },
                 changed=connection.changed()=>{
                  if changed.is_err(){break}
                  let observed=connection.borrow().clone();
                  if !observed.connected{if let Ok(mut state)=data.lock(){state.status="Market-data connection unavailable; displayed prices may be stale".into();}dirty=true;}
                  else if refresh.is_none(){
                   let backend=backend.clone();let registry=registry.clone();let scope=match data.lock(){Ok(state)=>state.scope,Err(_)=>break};
                   refresh=Some(tokio::spawn(async move{let deadline=tokio::time::Instant::now()+backend.default_timeout();let context=BusinessContext::load(&backend).await?;let snapshot=api::realtime(&backend,&registry,scope,context.value_date,deadline).await?;Ok((context,snapshot))}));
                  }None
                 },
                 result=async{refresh.as_mut().unwrap().await},if refresh.is_some()=>{
                  refresh=None;
                  if let Ok(mut state)=data.lock(){if state.incarnation!=incarnation{break}match result{
                   Ok(Ok((context,snapshot)))=>{
                    if context.revision>=state.context.revision{
                     if state.live&&context.value_date!=state.context.value_date{state.live=false;state.collecting=false;state.status="Live feed OFF - API value date changed; enable monitoring to resume".into();}
                     state.context=context;if let Some(snapshot)=snapshot{state.snapshot(snapshot);}
                     if state.status.starts_with("Market-data connection unavailable"){state.status="Monitor reconnected; authoritative snapshot refreshed".into();}
                    }
                   },
                   Ok(Err(error))=>state.status=format!("Authoritative monitor refresh failed: {error}"),Err(error)=>state.status=format!("Monitor refresh task failed: {error}"),
                  }}dirty=true;None
                 },
                };
                let Some((class, message)) = incoming else {
                    continue;
                };
                let Some(message) = message else {
                    if let Ok(mut state) = data.lock() {
                        state.status =
                            "Monitor event stream ended; reopen the trade to reattach".into();
                    }
                    if let Some(state) = weak.upgrade() {
                        let _ = Hub {
                            state,
                            updates: updates.clone(),
                            host_scope: host_scope.clone(),
                        }
                        .publish();
                    }
                    break;
                };
                let kind = match class {
                    0 => {
                        "TomasAI.IFM.Domain.Trade.Shared.Trade.Realtime.TradeRealtimeSnapshotChangedEvent"
                    }
                    1 => {
                        "TomasAI.IFM.Domain.Trade.Shared.Futures.Option.Position.IronCondorPositionChangedEvent"
                    }
                    2 => {
                        "TomasAI.IFM.Domain.Trade.Shared.Trade.Position.Plan.IronCondorTradePlanUpdatedEvent"
                    }
                    _ => {
                        let verb = message
                            .subject
                            .as_str()
                            .split('.')
                            .nth(2)
                            .unwrap_or_default();
                        match verb {
                            "OptionFeedFailed" => {
                                "TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.OptionMarketDataFeedFailedEvent"
                            }
                            "StoppedComplete" => {
                                "TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataFeedStoppedCompleteEvent"
                            }
                            "StartedComplete" => {
                                "TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.MarketDataFeedStartedCompleteEvent"
                            }
                            _ => continue,
                        }
                    }
                };
                let result = (|| -> Result<bool, String> {
                    let value = codec::decode(&message.payload)?;
                    let parts = message.subject.as_str().splitn(4, '.').collect::<Vec<_>>();
                    if parts.len() != 4 {
                        return Err("Invalid monitor event route".into());
                    }
                    let subject = ActorSubject {
                        kind: if class == 0 {
                            ActorKind::Notify
                        } else {
                            ActorKind::Event
                        },
                        name: parts[1].into(),
                        verb: parts[2].into(),
                        entity: parts[3].into(),
                    };
                    if crate::services::strategy::member(&registry, kind, &value, "Subject")?
                        != subject.wire()
                    {
                        return Err("Monitor event body subject differs from its route".into());
                    }
                    let mut state = data.lock().map_err(|_| "Monitor lock failed")?;
                    if state.incarnation != incarnation {
                        return Ok(false);
                    }
                    if class == 0 {
                        let event = typed::TradeRealtimeSnapshotChangedEvent::from_backend(&value)?;
                        if event.entity_id.as_ref() != Some(&state.scope.typed_id()) {
                            return Ok(false);
                        }
                        let expected = format!(
                            "Notify.TradeRealtimeSnapshot.Changed.{}",
                            state.scope.key()?
                        );
                        if message.subject.as_str() != expected {
                            return Ok(false);
                        }
                        return Ok(event
                            .snapshot
                            .is_some_and(|snapshot| state.snapshot(*snapshot)));
                    }
                    if class == 1 {
                        let event = typed::IronCondorPositionChangedEvent::from_backend(&value)?;
                        if event.entity_id.as_ref().and_then(|id| id.trade.as_ref())
                            != Some(&state.scope.typed_id())
                        {
                            return Ok(false);
                        }
                        let expected = format!(
                            "Event.FuturesIronCondorTradePositionEvent.StrategyPositionChanged.{}",
                            state.scope.typed_position("IronCondor")?.0
                        );
                        if message.subject.as_str() != expected {
                            return Ok(false);
                        }
                        return Ok(event
                            .position_snapshot
                            .is_some_and(|position| state.position(*position)));
                    }
                    if class == 2 {
                        let event = typed::IronCondorTradePlanUpdatedEvent::from_backend(&value)?;
                        if event
                            .entity_id
                            .as_ref()
                            .and_then(|id| id.position.as_ref())
                            .and_then(|id| id.trade.as_ref())
                            != Some(&state.scope.typed_id())
                        {
                            return Ok(false);
                        }
                        return Ok(event.plan.is_some_and(|plan| state.plan(*plan)));
                    }
                    let field = if kind.ends_with(".OptionMarketDataFeedFailedEvent") {
                        "ErrorDate"
                    } else {
                        "ReceivedOn"
                    };
                    let at = crate::services::strategy::member(&registry, kind, &value, field)?;
                    if !at.is_nil() {
                        if let Ok(at) = crate::backend::contracts::timestamp(&at) {
                            if state.last_feed_at.is_some_and(|previous| at <= previous) {
                                return Ok(false);
                            }
                            state.last_feed_at = Some(at);
                        }
                    }
                    match message
                        .subject
                        .as_str()
                        .split('.')
                        .nth(2)
                        .unwrap_or_default()
                    {
                        "OptionFeedFailed" => {
                            state.live = false;
                            state.starting = false;
                            state.collecting = false;
                            state.cleanup_unconfirmed = crate::services::strategy::member(
                                &registry,
                                kind,
                                &value,
                                "CleanupConfirmed",
                            )?
                            .as_bool()
                                != Some(true);
                            let error = crate::services::strategy::member(
                                &registry,
                                kind,
                                &value,
                                "ErrorMessage",
                            )?;
                            state.status = format!(
                                "Live feed OFF - option feed failed: {}",
                                error.as_str().unwrap_or("unknown failure")
                            );
                        }
                        "StoppedComplete" => {
                            state.live = false;
                            state.starting = false;
                            state.collecting = false;
                            state.market_stopped = true;
                            state.status =
                                "Live feed OFF - market data session closed by the API".into();
                        }
                        "StartedComplete" => {
                            state.market_stopped = false;
                            state.cleanup_unconfirmed = false;
                            if !state.live {
                                state.status="Live feed OFF - market data available; enable monitoring to resume".into();
                            }
                        }
                        _ => {}
                    }
                    if !state.live {
                        let count = state.history.rows().len();
                        if count > 0 {
                            state.detail_generation += 1;
                            state.observations_loading = false;
                            state.selected = state.history.saved(count - 1);
                        }
                    }
                    Ok(true)
                })();
                match result {
                    Ok(changed) => {
                        dirty |= changed;
                        if changed
                            && class == 3
                            && message.subject.as_str().split('.').nth(2) == Some("StartedComplete")
                        {
                            // Refresh eligibility from the API immediately at market open. This read
                            // does not wait for quotes or reset the user's selected history row.
                            if let Some(previous) = refresh.take() {
                                previous.abort();
                            }
                            let backend = backend.clone();
                            refresh = Some(tokio::spawn(async move {
                                Ok((BusinessContext::load(&backend).await?, None))
                            }));
                        }
                    }
                    Err(error) => {
                        if let Ok(mut state) = data.lock() {
                            state.status = format!("Monitor event rejected: {error}");
                        }
                        dirty = true;
                    }
                }
            }
            if let Some(refresh) = refresh {
                refresh.abort();
            }
            // No strong Hub reference crosses an await, so closing the final workspace
            // can dispose this owner and abort all consumers without a reference cycle.
            let _ = key;
        });
        Ok(vec![task])
    }
}
