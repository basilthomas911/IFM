//! Terminal feed failures reach the same Rust error dialog. Console rows use durable
//! NATS push with render-triggered ACK batches; background feed/calendar queries never gate delivery.
use crate::{
    backend::{codec, schema::Registry, transport::Backend},
    services::feed_control::FeedObservation,
    sidebar::{SidebarPublication, StatusRow},
};
use futures_util::StreamExt;
use std::{collections::{HashSet,VecDeque}, sync::Arc, time::Duration};

#[derive(Default)]
pub struct Notifications {
    pub publication: SidebarPublication,
    seen: VecDeque<String>,
    sequence: u64,
    // Console history is retained for the UI lifetime, including more than 500/1024 rows.
    status_seen: HashSet<String>,
}
impl Notifications {
    /// Status messages are console rows, not modal errors. Preserve the event's
    /// own timestamp and the hybrid's Eastern display format.
    fn status_event(&mut self, registry: &Registry, body: &[u8]) -> Result<bool, String> {
        let value = codec::decode(body)?;
        let kind = "TomasAI.IFM.Shared.StatusConsole.Events.StatusConsoleLoggedEvent";
        let id = registry
            .field(kind, "Id", &value)?
            .as_str()
            .ok_or("Missing status event identity")?
            .to_owned();
        let log = registry.field(kind, "StatusConsoleLog", &value)?;
        let date = chrono::DateTime::parse_from_rfc3339(
            log["StatusDate"].as_str().ok_or("Missing status time")?,
        )
        .map_err(|_| "Invalid status time")?
        .to_utc();
        let message = log["Message"]
            .as_str()
            .ok_or("Missing status message")?
            .to_owned();
        Ok(self.console_message(&id,date,&message))
    }
    /// Live notifications and history use the same identity and chronology.
    pub fn console_message(&mut self,id:&str,date:chrono::DateTime<chrono::Utc>,message:&str)->bool {
        if !self.status_seen.insert(id.to_owned()){return false;}
        let row=StatusRow {
            id: format!(
                "{}.{:07}Z|{id}",
                date.format("%Y-%m-%dT%H:%M:%S"),
                date.timestamp_subsec_nanos() / 100
            ),
            time: date
                .with_timezone(&chrono_tz::America::New_York)
                .format("%I:%M:%S%.3f %p")
                .to_string(),
            message:message.to_owned(),
        };
        // Other diagnostic rows do not share the console's timestamp identity.
        // Compare only console rows so an intervening error cannot disrupt replay order.
        let position=self.publication.status_rows.iter().position(|existing|existing.id.as_bytes().get(4)==Some(&b'-')&&existing.id>row.id).unwrap_or(self.publication.status_rows.len());
        self.publication.status_rows.insert(position,row);
        true
    }
    pub fn error(&mut self, identity: String, message: String) -> bool {
        if self.seen.contains(&identity) {
            return false;
        }
        if self.seen.len() == 1024 {
            self.seen.pop_front();
        }
        self.seen.push_back(identity);
        self.sequence += 1;
        self.publication.error = message.clone();
        self.publication.error_sequence = self.sequence;
        self.publication.status_rows.push(StatusRow {
            id: self.sequence.to_string(),
            time: String::new(),
            message,
        });
        true
    }
    pub fn event(
        &mut self,
        registry: &Registry,
        subject: &str,
        body: &[u8],
    ) -> Result<bool, String> {
        if subject.starts_with("Notify.StatusConsoleEvent.Logged.") {
            return self.status_event(registry, body);
        }
        let mut route = subject.splitn(4, '.');
        if route.next() != Some("Event") || route.next() != Some("MarketDataFeedEvent") {
            return Ok(false);
        }
        let verb = route.next().ok_or("Missing event verb")?;
        let name = match verb {
            "DailyAnalyticsInitializationStatus" => "DailyAnalyticsInitializationStatusEvent",
            "DatasetRecoveryFailed" => "MarketDataDatasetRecoveryFailedEvent",
            "OptionFeedFailed" => "OptionMarketDataFeedFailedEvent",
            "StartedFail" => "MarketDataFeedStartedFailEvent",
            "StoppedFail" => "MarketDataFeedStoppedFailEvent",
            _ => return Ok(false),
        };
        let decoded = codec::decode(body)?;
        let named = registry.project(
            &format!("TomasAI.IFM.Domain.MarketData.Feed.Shared.Events.{name}"),
            &decoded,
        )?;
        let message = named["ErrorMessage"]
            .as_str()
            .ok_or("Failure event has no message")?
            .to_string();
        let operation = named["CommandId"].as_str().unwrap_or_default();
        let received=named["ReceivedOn"].as_str().ok_or("Missing notification receipt time")?;
        let time=chrono::DateTime::parse_from_rfc3339(received).map_err(|_|"Invalid notification receipt time")?.with_timezone(&chrono_tz::America::New_York).format("%I:%M:%S%.3f %p").to_string();
        let scope = match verb {
            "DatasetRecoveryFailed" => format!("{}/{}/{}",named["Role"].as_str().unwrap_or_default(),named["Dataset"].as_str().unwrap_or_default(),named["EventSource"].as_str().unwrap_or_default()),
            "OptionFeedFailed" => format!("Options/{}",named["Dataset"].as_str().unwrap_or_default()),
            _ => verb.to_owned(),
        };
        let identity=format!("{scope}|{operation}");
        // Match the hybrid's operation-scoped deduplication. A repeated error
        // with amended diagnostic text does not open another dialog.
        let title=match verb {
            "DatasetRecoveryFailed" if named["EventSource"].as_str()==Some("ApiRecoveryUiNotifier")=>"API Recovery Failed",
            "DatasetRecoveryFailed" if named["Role"].as_str()==Some("Options")=>"Option Pipeline Recovery Failed",
            "DatasetRecoveryFailed"=>"Market Data Recovery Failed",
            "OptionFeedFailed"=>"Option Feed Recovery Failed",
            "DailyAnalyticsInitializationStatus"=>"Daily EMA / Bollinger Initialization",
            _=>"",
        };
        if verb=="DailyAnalyticsInitializationStatus"&&!named["RefreshFailed"].as_bool().ok_or("Missing daily analytics outcome")? {
            if self.seen.contains(&identity){return Ok(false);}
            if self.seen.len()==1024{self.seen.pop_front();}self.seen.push_back(identity.clone());
            self.publication.status_rows.push(StatusRow{id:identity,time,message});
            return Ok(true);
        }
        let changed=self.error(identity,message.clone());
        // Event metadata supplies the display time. Never invent a business
        // timestamp from the workstation clock when logging a domain failure.
        if changed { if let Some(row)=self.publication.status_rows.last_mut(){row.time=time;} }
        if changed&&!title.is_empty(){self.publication.error=format!("{title}: {message}");}
        Ok(changed)
    }
}

pub async fn monitor(
    backend: Backend,
    registry: Arc<Registry>,
    output: tokio::sync::mpsc::Sender<SidebarPublication>,
    actions: tokio::sync::mpsc::Receiver<u32>,
) -> Result<(), String> {
    let endpoint=crate::backend::configuration::value("IFM_UI_STATUS_CONSOLE_URL")?;
    monitor_with_console_history(backend,registry,output,actions,endpoint).await
}

/// Explicit endpoint injection lets isolated protocol tests exercise catch-up
/// without reading a user's Development profile or contacting its API.
pub async fn monitor_with_console_history(
    backend: Backend,
    registry: Arc<Registry>,
    output: tokio::sync::mpsc::Sender<SidebarPublication>,
    mut actions: tokio::sync::mpsc::Receiver<u32>,
    history_endpoint:Option<String>,
) -> Result<(), String> {
    let feed = backend.subscribe("Event", "MarketDataFeedEvent").await?;
    let calendar_events = backend.subscribe("Event", "EconomicCalendarEvent").await?;
    let console = backend.subscribe_console().await?;
    // The same push subscription delivers buffered and live messages without polling.
    let mut subscription = futures_util::stream::select_all(vec![feed, calendar_events, console]);
    backend.flush().await?;
    let mut notifications = Notifications::default();
    let history_client=crate::backend::health_http::HealthClient::new()?;
    let mut connect_needed=true;
    let mut connect_query=AbortOnDrop(None);
    let mut acknowledgement_query=AbortOnDrop(None);
    let mut acknowledgement_retry=false;
    let mut timer = tokio::time::interval(Duration::from_secs(10));
    timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    let mut query = AbortOnDrop(None);
    let mut feed_operation = AbortOnDrop(None);
    let mut connection = backend.watch_connection();
    let mut calendar = crate::state::sidebar_calendar::SidebarCalendar::default();
    let mut calendar_query = AbortOnDrop(None);
    let mut calendar_dirty = true;
    let mut calendar_revision = 0_u64;

    loop {
        if connect_needed && connect_query.0.is_none() {
            connect_needed=false;
            let client=history_client.clone();let endpoint=history_endpoint.clone();let timeout=backend.default_timeout();
            connect_query.0=Some(tokio::spawn(async move {
                crate::services::status_console_history::connect(&client,endpoint.as_deref(),timeout).await
            }));
        }
        if calendar_dirty && calendar_query.0.is_none() {
            calendar_dirty = false;
            let backend = backend.clone();
            let registry = registry.clone();
            let captured = calendar.clone();
            let revision = calendar_revision;
            notifications.publication.busy = true;
            output
                .send(notifications.publication.clone())
                .await
                .map_err(|_| "UI closed")?;
            calendar_query.0 = Some(tokio::spawn(async move {
                (revision, captured.refresh(&backend, &registry).await)
            }));
        }
        tokio::select! {
            result=async{connect_query.0.as_mut().unwrap().await},if connect_query.0.is_some()=>{
                connect_query.0=None;
                if let Err(error)=result.map_err(|e|e.to_string()).and_then(|result|result) {
                    notifications.error(format!("console-connect:{error}"),format!("Status console connection setup failed: {error}"));
                    output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
                }
            }
            _=async{notifications.publication.console_deliveries[0].wait_displayed().await},
                if !notifications.publication.console_deliveries.is_empty() && acknowledgement_query.0.is_none()=>{
                let batch:Vec<_>=notifications.publication.console_deliveries.iter().filter(|delivery|delivery.is_displayed()).take(100).cloned().collect();
                let client=history_client.clone();let endpoint=history_endpoint.clone();let timeout=backend.default_timeout();let retry=acknowledgement_retry;
                acknowledgement_retry=false;
                acknowledgement_query.0=Some(tokio::spawn(async move {
                    // Backoff is only for failed ACK requests, never for receiving/displaying console messages.
                    if retry {tokio::time::sleep(Duration::from_secs(1)).await;}
                    let receipts=batch.iter().flat_map(|delivery|delivery.receipts().iter().cloned()).collect::<Vec<_>>();
                    let result=crate::services::status_console_history::acknowledge(&client,endpoint.as_deref(),&receipts,timeout).await;
                    (batch,result)
                }));
            }
            result=async{acknowledgement_query.0.as_mut().unwrap().await},if acknowledgement_query.0.is_some()=>{
                acknowledgement_query.0=None;
                match result {
                    Ok((batch,Ok(())))=>{notifications.publication.console_deliveries.retain(|delivery|!batch.iter().any(|acknowledged|Arc::ptr_eq(delivery,acknowledged)));},
                    result=>{
                        acknowledgement_retry=true;
                        let error=match result {Ok((_,Err(error)))=>error,Err(error)=>error.to_string(),_=>unreachable!()};
                        notifications.error(format!("console-ack:{error}"),format!("Status console acknowledgement failed; batch will retry: {error}"));
                        output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
                    },
                }
            }
            _ = timer.tick() => {
                calendar_dirty=true;
                if query.0.is_none() && feed_operation.0.is_none() {
                    let backend = backend.clone();
                    query.0 = Some(tokio::spawn(async move { FeedObservation::load(&backend).await }));
                }
            }
            event = subscription.next() => {
                let Some(event) = event else { return Err("Feed notification subscription ended; authoritative refresh required".into()); };
                // JetStream routes through the delivery subscription but preserves the original subject.
                if event.subject.as_str() == "Console.StatusConsoleEvent.Logged" {
                    let receipt=event.reply.as_ref().map(|reply|reply.to_string()).ok_or("Missing console delivery receipt")?;
                    if !receipt.starts_with("$JS.ACK.IFM_STATUS_CONSOLE_QUEUE.console-ui.") {return Err("Invalid console push receipt".into());}
                    notifications.status_event(&registry,&event.payload)?;
                    // Redelivery replaces only the receipt for the same stream sequence. Old ACK completion
                    // cannot clear a newer receipt or any messages received while that ACK was in flight.
                    let sequence=receipt.split('.').nth(5).unwrap_or_default();
                    notifications.publication.console_deliveries.retain(|delivery|delivery.receipts()[0].split('.').nth(5)!=Some(sequence));
                    if notifications.publication.console_deliveries.len()==100 {notifications.publication.console_deliveries.remove(0);}
                    notifications.publication.console_deliveries.push(Arc::new(crate::services::status_console_history::ConsoleDelivery::new(vec![receipt])));
                    output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
                    continue;
                }
                if event.subject.starts_with("Event.EconomicCalendarEvent.") {
                    let verb=event.subject.splitn(4,'.').nth(2).unwrap_or_default();
                    match verb {
                        "AddedComplete"|"ChangedComplete"|"RemovedComplete"|"ImportedComplete"=>{calendar_dirty=true;calendar_revision+=1;},
                        "AddedFail"|"ChangedFail"|"RemovedFail"=>{
                            let kind=format!("TomasAI.IFM.Domain.MarketData.Shared.Events.EconomicCalendar{verb}Event");
                            let message=codec::decode(&event.payload).and_then(|value|registry.field(&kind,"ErrorMessage",&value)).ok().and_then(|v|v.as_str().map(str::to_string)).unwrap_or_else(||"Economic-calendar notification could not be decoded".into());
                            notifications.error(format!("calendar:{verb}:{message}"),message);
                            output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
                        },
                        _=>{},
                    }
                    continue;
                }
                match notifications.event(&registry, event.subject.as_str(), &event.payload) {
                    Ok(false) => continue,
                    Ok(true) => {},
                    Err(error) => { notifications.error(format!("decode:{error}"), format!("Could not decode a backend feed notification: {error}")); }
                }
                output.send(notifications.publication.clone()).await.map_err(|_| "UI closed")?;
            }
            result = async { query.0.as_mut().unwrap().await }, if query.0.is_some() => {
                query.0 = None;
                match result.map_err(|error| error.to_string()).and_then(|result| result) {
                    Ok(status) => {
                        notifications.publication.api_today=status.session.as_of_utc.with_timezone(&chrono_tz::America::New_York).date_naive().to_string();
                        notifications.publication.feed_control=status.control(feed_operation.0.is_some());
                        if let Some(message) = status.status.terminal_error { notifications.error(format!("runtime:{message}"), message); }
                    }
                    Err(error) => {
                        notifications.publication.feed_control.ready = false;
                        notifications.publication.feed_control.can_toggle = false;
                        notifications.error(format!("runtime-query:{error}"), format!("Market feed status unavailable: {error}"));
                    }
                }
                output.send(notifications.publication.clone()).await.map_err(|_| "UI closed")?;
            }
            action=actions.recv(), if !actions.is_closed()=>{
                if let Some(action)=action {
                    if action==crate::sidebar::FEED_TOGGLE_ACTION {
                        if feed_operation.0.is_some(){continue;}
                        if let Some(task)=query.0.take(){task.abort();}
                        let backend=backend.clone();let registry=registry.clone();let id=uuid::Uuid::new_v4();
                        notifications.publication.feed_control.busy=true;notifications.publication.feed_control.can_toggle=false;
                        feed_operation.0=Some(tokio::spawn(async move{crate::services::feed_control::toggle(&backend,&registry,id).await}));
                        output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
                        continue;
                    }
                    match calendar.action(action){Ok(refresh)=>{calendar_dirty|=refresh;calendar_revision+=1;calendar.publish(&mut notifications.publication);},Err(error)=>{notifications.error(format!("sidebar-action:{action}:{error}"),error);}}
                    output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
                }
            }
            result=async{feed_operation.0.as_mut().unwrap().await},if feed_operation.0.is_some()=>{
                feed_operation.0=None;
                match result.map_err(|e|format!("OutcomeUnknown: feed task failed: {e}")).and_then(|r|r) {
                    Ok(observation)=>{notifications.publication.api_today=observation.session.as_of_utc.with_timezone(&chrono_tz::America::New_York).date_naive().to_string();notifications.publication.feed_control=observation.control(false);},
                    Err(error)=>{notifications.publication.feed_control.busy=false;notifications.publication.feed_control.can_toggle=false;notifications.error(format!("feed-operation:{error}"),error);},
                }
                output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
            }
            result=async{calendar_query.0.as_mut().unwrap().await}, if calendar_query.0.is_some()=>{
                calendar_query.0=None;notifications.publication.busy=false;
                match result.map_err(|e|e.to_string()) {
                    Ok((revision,Ok(model))) if revision==calendar_revision=>{calendar=model;calendar.publish(&mut notifications.publication);},
                    Ok((revision,Err(error))) if revision==calendar_revision=>{notifications.error(format!("calendar-refresh:{error}"),format!("Economic calendar unavailable: {error}"));},
                    Ok(_)=>{calendar_dirty=true;},
                    Err(error)=>{notifications.error(format!("calendar-task:{error}"),format!("Calendar task failed: {error}"));},
                }
                output.send(notifications.publication.clone()).await.map_err(|_|"UI closed")?;
            }
            changed = connection.changed() => {
                changed.map_err(|_| "Connection supervisor ended")?;
                let state = connection.borrow_and_update().clone();
                if !state.connected { notifications.publication.feed_control.ready = false; notifications.publication.feed_control.can_toggle = false; }
                if let Some(message) = state.last_warning { notifications.error(format!("transport:{}", state.warning_sequence),message); }
                if state.connected {
                    connect_needed=true;
                        calendar_dirty=true;calendar_revision+=1;
                    if let Some(task)=calendar_query.0.take(){task.abort();}
                    if let Some(task) = query.0.take() { task.abort(); }
                    if feed_operation.0.is_none(){
                        let backend = backend.clone();
                        query.0 = Some(tokio::spawn(async move { FeedObservation::load(&backend).await }));
                    }
                }
                output.send(notifications.publication.clone()).await.map_err(|_| "UI closed")?;
            }
        }
    }
}
struct AbortOnDrop<T>(Option<tokio::task::JoinHandle<T>>);
impl<T> Drop for AbortOnDrop<T> {
    fn drop(&mut self) {
        if let Some(task) = self.0.take() {
            task.abort();
        }
    }
}
