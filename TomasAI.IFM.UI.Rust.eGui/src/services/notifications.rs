//! Terminal feed failures reach the same Rust error dialog. Console rows use durable
//! Core NATS push into two viewport pages; background queries never gate console intake.
use crate::{
    backend::{codec, schema::Registry, transport::Backend},
    services::feed_control::FeedObservation,
    sidebar::{SidebarPublication, StatusRow},
};
use futures_util::StreamExt;
use std::{collections::VecDeque, sync::Arc, time::Duration};

#[derive(Default)]
pub struct Notifications {
    pub publication: SidebarPublication,
    seen: VecDeque<String>,
    sequence: u64,
    // An explicit override makes isolated tests independent of GUI geometry.
    console_capacity: Option<usize>,
    last_warning: Option<(String,String)>,
}
impl Notifications {
    /// Explicit size override for nonvisual consumers and deterministic tests.
    pub fn set_console_capacity(&mut self,capacity:usize){self.console_capacity=Some(capacity.max(2));self.trim_console();}
    fn trim_console(&mut self){
        let capacity=self.console_capacity.unwrap_or_else(crate::console_capacity::current);
        crate::console_capacity::trim_to(&mut self.publication.status_rows,capacity);
    }
    fn snapshot(&mut self)->SidebarPublication{self.trim_console();self.publication.clone()}
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
    /// Retained IDs are added once; backend timestamps determine display order.
    pub fn console_message(&mut self,id:&str,date:chrono::DateTime<chrono::Utc>,message:&str)->bool {
        if self.publication.status_rows.iter().any(|row|row.id.ends_with(&format!("Z|{id}"))){return false;}
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
        self.trim_console();
        true
    }
    /// Records a confirmed terminal failure (or an explicit dataset reset
    /// escalation). Retryable diagnostics and failed user actions use warning.
    pub fn error(&mut self, identity: String, message: String) -> bool {
        self.error_correlated(identity,message,None)
    }
    fn error_correlated(&mut self,identity:String,message:String,operation:Option<&str>)->bool {
        let identity=stable_identity(identity);
        if self.seen.contains(&identity) {
            return false;
        }
        let operation=operation.or_else(||identity.rsplit('|').next().filter(|id|uuid::Uuid::parse_str(id).is_ok()));
        crate::diagnostics::errors::record("ui.error_queued", None, operation, &message);
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
    /// Keeps recoverable diagnostics visible and durable without opening,
    /// clearing or replacing an application-error dialog. A separate identity
    /// namespace allows a later terminal outcome to escalate the same operation.
    pub fn warning(&mut self, identity: String, message: String) -> bool {
        let identity=stable_identity(identity);
        if self.last_warning.as_ref().is_some_and(|(prior,_)|prior==&identity){return false;}
        crate::diagnostics::errors::record("ui.warning_replaced",None,None,&message);
        self.last_warning=Some((identity,message.clone()));
        self.publication.status_rows.retain(|row|!row.id.starts_with("current-warning|"));
        let at=chrono::Utc::now();
        self.publication.status_rows.push(StatusRow{id:format!("current-warning|{}.{:07}",at.format("%Y-%m-%dT%H:%M:%S"),at.timestamp_subsec_nanos()/100),time:at.with_timezone(&chrono_tz::America::New_York).format("%I:%M:%S%.3f %p").to_string(),message});
        self.trim_console();
        true
    }
    /// A failed status observation is connection/diagnostic evidence, not a
    /// replacement for the terminal domain error awaiting user acknowledgement.
    /// Keeping the modal sequence unchanged also protects a terminal event when
    /// UI snapshots coalesce during the API restart's no-responder window.
    pub fn feed_status_unavailable(&mut self, error: &str) -> bool {
        self.publication.feed_control.ready = false;
        self.publication.feed_control.can_toggle = false;
        let identity = stable_identity(format!("runtime-query:{error}"));
        if self.seen.contains(&identity) { return false; }
        if self.seen.len() == 1024 { self.seen.pop_front(); }
        self.seen.push_back(identity.clone());
        self.publication.status_rows.push(StatusRow {
            id: identity,
            time: String::new(),
            message: format!("Market feed status unavailable: {error}"),
        });
        true
    }

    pub fn event(
        &mut self, registry: &Registry, subject: &str, body: &[u8],
    ) -> Result<bool,String> {
        self.event_inner(registry,subject,body).map_err(|error| {
            crate::diagnostics::errors::record("backend.notification_decode_failed",Some(subject),None,&error);
            error
        })
    }
    fn event_inner(
        &mut self,
        registry: &Registry,
        subject: &str,
        body: &[u8],
    ) -> Result<bool, String> {
        if subject.starts_with("Notify.MarketDataRecoveryOutcome.") { return self.recovery_outcome(subject,body); }
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
        // Native incidents can emit reset and final failure facts under one operation.
        // Deduplicate those facts by RecordId; preserve existing C# notification behavior.
        let notification=if verb=="DatasetRecoveryFailed" && named["EventSource"].as_str()==Some("RustMarketDataOwner") {
            named["Id"].as_str().filter(|id|!id.is_empty()).ok_or("Missing native notification record identity")?
        }else{operation};
        let identity=format!("{scope}|{notification}");
        // Audit every delivery, including duplicates. Receipt does not assert
        // that the user has seen or acknowledged the dialog.
        if verb!="DailyAnalyticsInitializationStatus" || named["RefreshFailed"].as_bool()==Some(true) {
            crate::diagnostics::errors::record("backend.failure_notification_received", Some(subject), Some(operation),
                &serde_json::json!({"eventId":named["Id"],"eventSource":named["EventSource"],
                    "backendReceivedOn":named["ReceivedOn"],"errorCode":named["ErrorCode"],
                    "scope":scope,"message":message,"notification":&named}).to_string());
        }
        // A repeated native fact does not open another dialog; a later final
        // failure in the same operation remains independently visible.
        let title=match verb {
            "DatasetRecoveryFailed" if named["EventSource"].as_str()==Some("ApiRecoveryUiNotifier")=>"API Recovery Failed",
            "DatasetRecoveryFailed" if named["Role"].as_str()==Some("Options")=>"Option Pipeline Recovery Failed",
            "DatasetRecoveryFailed"=>"Market Data Recovery Failed",
            "OptionFeedFailed"=>"Option Feed Recovery Failed",
            _=>"",
        };
        // A refresh failure is retryable health/status evidence, even when no
        // baseline exists yet. It is not a terminal application failure. Keep
        // the full diagnostic audit and console entry without changing a modal.
        if verb=="DailyAnalyticsInitializationStatus" {
            named["RefreshFailed"].as_bool().ok_or("Missing daily analytics outcome")?;
            if self.seen.contains(&identity){return Ok(false);}
            if self.seen.len()==1024{self.seen.pop_front();}self.seen.push_back(identity.clone());
            self.publication.status_rows.push(StatusRow{id:identity,time,message});
            return Ok(true);
        }
        let changed=if matches!(verb,"DatasetRecoveryFailed"|"OptionFeedFailed") {
            self.error_correlated(identity,message.clone(),Some(operation))
        } else {
            self.warning(identity,message.clone())
        };
        // Event metadata supplies the display time. Never invent a business
        // timestamp from the workstation clock when logging a domain failure.
        if changed { if let Some(row)=self.publication.status_rows.last_mut(){row.time=time;} }
        if changed&&!title.is_empty(){self.publication.error=format!("{title}: {message}");}
        Ok(changed)
    }

    /// Versioned native JSON outcomes are separate from the existing MessagePack
    /// failure event. Only explicit proofs supplied by Rust determine the text.
    fn recovery_outcome(&mut self,subject:&str,body:&[u8])->Result<bool,String> {
        if body.len()>16*1024{return Err("Recovery outcome exceeds capacity".into());}
        let value:serde_json::Value=serde_json::from_slice(body).map_err(|e|e.to_string())?;
        if value["Version"].as_u64()!=Some(1){return Err("Unsupported recovery outcome version".into());}
        let id=value["RecordId"].as_str().ok_or("Missing recovery record identity")?;
        let operation=value["OperationId"].as_str().ok_or("Missing recovery operation identity")?;
        uuid::Uuid::parse_str(id).map_err(|_|"Invalid recovery record identity")?;
        uuid::Uuid::parse_str(operation).map_err(|_|"Invalid recovery operation identity")?;
        let date=value["ValueDate"].as_str().ok_or("Missing recovery ValueDate")?;
        let parsed_date=chrono::NaiveDate::parse_from_str(date,"%Y-%m-%d").map_err(|_|"Invalid recovery ValueDate")?;
        if chrono::Datelike::year(&parsed_date)<1 || parsed_date.format("%Y-%m-%d").to_string()!=date {return Err("Invalid recovery ValueDate".into());}
        if !matches!(value["Role"].as_str(),Some("Options"|"Futures")) || value["Dataset"].as_str().is_none_or(|s|s.is_empty()||s.len()>64) || value["MonitoringReady"].as_bool().is_none() {return Err("Invalid recovery outcome proof/scope".into());}
        if subject!=format!("Notify.MarketDataRecoveryOutcome.{date}"){return Err("Recovery outcome date/subject mismatch".into());}
        let severity=match value["Severity"].as_str(){Some("success")=>crate::status_dialog::Severity::Success,Some("warning")=>crate::status_dialog::Severity::Warning,_=>return Err("Invalid recovery outcome severity".into())};
        let message=value["Message"].as_str().filter(|s|!s.is_empty()&&s.len()<=8192).ok_or("Invalid recovery outcome message")?;
        let occurred=value["OccurredAtUtc"].as_str().ok_or("Missing recovery outcome time")?;
        let at=chrono::DateTime::parse_from_rfc3339(occurred).map_err(|_|"Invalid recovery outcome time")?;
        let identity=format!("outcome:{id}");
        if self.seen.contains(&identity){return Ok(false);}
        crate::diagnostics::errors::record("backend.recovery_outcome_received",Some(subject),Some(operation),&value.to_string());
        if self.seen.len()==1024{self.seen.pop_front();}self.seen.push_back(identity.clone());
        self.sequence+=1;
        // Display has one latest slot; durable recovery history remains in the audit log.
        self.publication.outcome_dialogs.clear();
        self.publication.outcome_dialogs.push(crate::status_dialog::Notice{sequence:self.sequence,message:message.into(),severity});
        self.publication.status_rows.push(StatusRow{id:identity,time:at.with_timezone(&chrono_tz::America::New_York).format("%I:%M:%S%.3f %p").to_string(),message:message.into()});
        Ok(true)
    }
}

/// Diagnostic request IDs change on every attempt. They must not defeat the
/// existing outage/dialog deduplication; backend operation IDs remain intact.
fn stable_identity(mut identity:String)->String {
    let marker="; operationId=";
    let mut offset=0;
    while let Some(index)=identity[offset..].find(marker) {
        let start=offset+index+marker.len();
        let Some(id)=identity.get(start..start+36) else {break;};
        if uuid::Uuid::parse_str(id).is_ok() {
            identity.replace_range(start..start+36,"<request>");offset=start+9;
        }else{offset=start;}
    }
    identity
}

pub async fn monitor(
    backend: Backend,
    registry: Arc<Registry>,
    output: tokio::sync::mpsc::Sender<SidebarPublication>,
    actions: tokio::sync::mpsc::Receiver<u32>,
) -> Result<(), String> {
    let (latest,mut changes)=tokio::sync::watch::channel(SidebarPublication::default());
    // A stalled GUI only delays this relay. Intake keeps the latest bounded state.
    let _relay=AbortOnDrop(Some(tokio::spawn(async move {
        while changes.changed().await.is_ok(){
            let publication=changes.borrow_and_update().clone();
            if output.send(publication).await.is_err(){break;}
        }
    })));
    monitor_live(backend,registry,latest,actions).await
}
async fn monitor_live(backend:Backend,registry:Arc<Registry>,output:tokio::sync::watch::Sender<SidebarPublication>,mut actions:tokio::sync::mpsc::Receiver<u32>)->Result<(),String>{
    let feed = backend.subscribe("Event", "MarketDataFeedEvent").await?;
    let calendar_events = backend.subscribe("Event", "EconomicCalendarEvent").await?;
    let outcomes = backend.subscribe("Notify", "MarketDataRecoveryOutcome").await?;
    let console = backend.subscribe_console().await?;
    // The same push subscription delivers buffered and live messages without polling.
    let mut subscription = futures_util::stream::select_all(vec![feed, calendar_events, console, outcomes]);
    backend.flush().await?;
    let mut notifications = Notifications::default();
    let mut timer = tokio::time::interval(Duration::from_secs(10));
    timer.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    let mut query = AbortOnDrop(None);
    let mut feed_operation = AbortOnDrop(None);
    let mut connection = backend.watch_connection();
    let mut was_connected=connection.borrow().connected;
    let mut calendar = crate::state::sidebar_calendar::SidebarCalendar::default();
    let mut calendar_query = AbortOnDrop(None);
    let mut calendar_dirty = true;
    let mut calendar_revision = 0_u64;

    loop {
        if calendar_dirty && calendar_query.0.is_none() {
            calendar_dirty = false;
            let backend = backend.clone();
            let registry = registry.clone();
            let captured = calendar.clone();
            let revision = calendar_revision;
            notifications.publication.busy = true;
            output
                .send(notifications.snapshot())
                .map_err(|_| "UI closed")?;
            calendar_query.0 = Some(tokio::spawn(async move {
                (revision, captured.refresh(&backend, &registry).await)
            }));
        }
        tokio::select! {
            _ = timer.tick() => {
                calendar_dirty=true;
                if query.0.is_none() && feed_operation.0.is_none() {
                    let backend = backend.clone();
                    query.0 = Some(tokio::spawn(async move { FeedObservation::load(&backend).await }));
                }
            }
            event = subscription.next() => {
                let Some(event) = event else { return Err("Feed notification subscription ended; authoritative refresh required".into()); };
                if event.subject.as_str().starts_with("Notify.StatusConsoleEvent.Logged.") {
                    let changed=match notifications.status_event(&registry,&event.payload) {
                        Ok(changed)=>changed,
                        Err(error)=>{
                            crate::diagnostics::errors::record("backend.console_decode_failed",Some(event.subject.as_str()),None,&error);
                            notifications.warning(format!("console-decode:{error}"),format!("Invalid console message: {error}"))
                        }
                    };
                    if changed {output.send(notifications.snapshot()).map_err(|_|"UI closed")?;}
                    continue;
                }
                if event.subject.starts_with("Event.EconomicCalendarEvent.") {
                    let verb=event.subject.splitn(4,'.').nth(2).unwrap_or_default();
                    match verb {
                        "AddedComplete"|"ChangedComplete"|"RemovedComplete"|"ImportedComplete"=>{calendar_dirty=true;calendar_revision+=1;},
                        "AddedFail"|"ChangedFail"|"RemovedFail"=>{
                            let kind=format!("TomasAI.IFM.Domain.MarketData.Shared.Events.EconomicCalendar{verb}Event");
                            let message=codec::decode(&event.payload).and_then(|value|registry.field(&kind,"ErrorMessage",&value)).ok().and_then(|v|v.as_str().map(str::to_string)).unwrap_or_else(||"Economic-calendar notification could not be decoded".into());
                            notifications.warning(format!("calendar:{verb}:{message}"),message);
                            output.send(notifications.snapshot()).map_err(|_|"UI closed")?;
                        },
                        _=>{},
                    }
                    continue;
                }
                match notifications.event(&registry, event.subject.as_str(), &event.payload) {
                    Ok(false) => continue,
                    Ok(true) => {},
                    Err(error) => { notifications.warning(format!("decode:{error}"), format!("Could not decode a backend feed notification: {error}")); }
                }
                output.send(notifications.snapshot()).map_err(|_| "UI closed")?;
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
                        notifications.feed_status_unavailable(&error);
                    }
                }
                output.send(notifications.snapshot()).map_err(|_| "UI closed")?;
            }
            action=actions.recv(), if !actions.is_closed()=>{
                if let Some(action)=action {
                    if action==crate::sidebar::FEED_TOGGLE_ACTION {
                        if feed_operation.0.is_some(){continue;}
                        if let Some(task)=query.0.take(){task.abort();}
                        let backend=backend.clone();let registry=registry.clone();let id=uuid::Uuid::new_v4();
                        notifications.publication.feed_control.busy=true;notifications.publication.feed_control.can_toggle=false;
                        feed_operation.0=Some(tokio::spawn(async move{crate::services::feed_control::toggle(&backend,&registry,id).await}));
                        output.send(notifications.snapshot()).map_err(|_|"UI closed")?;
                        continue;
                    }
                    match calendar.action(action){Ok(refresh)=>{calendar_dirty|=refresh;calendar_revision+=1;calendar.publish(&mut notifications.publication);},Err(error)=>{notifications.warning(format!("sidebar-action:{action}:{error}"),error);}}
                    output.send(notifications.snapshot()).map_err(|_|"UI closed")?;
                }
            }
            result=async{feed_operation.0.as_mut().unwrap().await},if feed_operation.0.is_some()=>{
                feed_operation.0=None;
                match result.map_err(|e|format!("OutcomeUnknown: feed task failed: {e}")).and_then(|r|r) {
                    Ok(observation)=>{notifications.publication.api_today=observation.session.as_of_utc.with_timezone(&chrono_tz::America::New_York).date_naive().to_string();notifications.publication.feed_control=observation.control(false);},
                    Err(error)=>{notifications.publication.feed_control.busy=false;notifications.publication.feed_control.can_toggle=false;notifications.warning(format!("feed-operation:{error}"),error);},
                }
                output.send(notifications.snapshot()).map_err(|_|"UI closed")?;
            }
            result=async{calendar_query.0.as_mut().unwrap().await}, if calendar_query.0.is_some()=>{
                calendar_query.0=None;notifications.publication.busy=false;
                match result.map_err(|e|e.to_string()) {
                    Ok((revision,Ok(model))) if revision==calendar_revision=>{calendar=model;calendar.publish(&mut notifications.publication);},
                    Ok((revision,Err(error))) if revision==calendar_revision=>{notifications.warning(format!("calendar-refresh:{error}"),format!("Economic calendar unavailable: {error}"));},
                    Ok(_)=>{calendar_dirty=true;},
                    Err(error)=>{notifications.warning(format!("calendar-task:{error}"),format!("Calendar task failed: {error}"));},
                }
                output.send(notifications.snapshot()).map_err(|_|"UI closed")?;
            }
            changed = connection.changed() => {
                changed.map_err(|_| "Connection supervisor ended")?;
                let state = connection.borrow_and_update().clone();
                if !state.connected { notifications.publication.feed_control.ready = false; notifications.publication.feed_control.can_toggle = false; }
                if let Some(message) = state.last_warning { notifications.warning(format!("transport:{message}"),message); }
                let reconnected=state.connected && !was_connected;was_connected=state.connected;
                if reconnected {
                        calendar_dirty=true;calendar_revision+=1;
                    if let Some(task)=calendar_query.0.take(){task.abort();}
                    if let Some(task) = query.0.take() { task.abort(); }
                    if feed_operation.0.is_none(){
                        let backend = backend.clone();
                        query.0 = Some(tokio::spawn(async move { FeedObservation::load(&backend).await }));
                    }
                }
                output.send(notifications.snapshot()).map_err(|_| "UI closed")?;
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
