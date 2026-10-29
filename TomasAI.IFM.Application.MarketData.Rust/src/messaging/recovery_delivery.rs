//! Persistent native UI-error delivery, outside every replaceable dataset.
//! Retries run only with undisplayed terminal records. Broker round trips never
//! acknowledge display or consume the bounded native terminal journal.
use super::{
    publisher::{BrokerSettings, Publisher},
    recovery_notice::Notice,
};
use crate::{
    dataset::transport::Lane,
    runtime::{
        Deadline,
        clock::Clock,
        owner::{Child, Owner},
        recovery::Recovery,
    },
};
use std::{sync::Arc, time::Duration};
use tokio::{sync::watch, task::JoinHandle};
use tokio_util::sync::CancellationToken;

/// Joined persistent notification owner. Only copied dates/status and immutable
/// broker settings cross this boundary; no dataset provider lease is retained.
pub struct Delivery {
    parent: Arc<Child>,
    task: Option<JoinHandle<()>>,
    dates: watch::Sender<[Option<time::Date>; 3]>,
    scope: String,
}
impl Delivery {
    /// Starts an idle event-driven notification owner with its independent root
    /// resource slot. Actual socket I/O starts only when a terminal error exists.
    ///
    /// # Errors
    /// Duplicate/stopped notification admission or invalid broker scope.
    pub fn start(
        root: &Arc<Owner>,
        clock: Arc<Clock>,
        health: [Arc<Recovery>; 3],
        broker: BrokerSettings,
        scope: String,
    ) -> Result<Self, String> {
        if scope.is_empty()
            || scope.len() > 128
            || !scope
                .bytes()
                .all(|b| b.is_ascii_alphanumeric() || b == b'-')
        {
            return Err("Invalid native notification scope".into());
        }
        let parent = Arc::new(
            root.notification_child()
                .map_err(|e| format!("Native notification admission: {e:?}"))?,
        );
        let (dates, mut inputs) = watch::channel([None; 3]);
        let transport_parent = parent.clone();
        let runtime = rand::random::<u128>().max(1).to_le_bytes();
        let configuration = scope.clone();
        let task=parent.spawn_joined(move |stop|async move {
            let mut first=health[0].subscribe();let mut second=health[1].subscribe();let mut third=health[2].subscribe();
            loop {
                if stop.is_cancelled(){break;}
                let dates=*inputs.borrow_and_update();
                let mut pending=Vec::with_capacity(48);
                let mut outcomes=Vec::with_capacity(48);
                for (index,lane) in [Lane::FuturesGlbx,Lane::FuturesXcbf,Lane::OptionsGlbx].into_iter().enumerate() {
                    if let Some(date)=dates[index] {
                        for terminal in health[index].failures(0,16) {pending.push((lane,date,terminal));}
                        for outcome in health[index].outcomes() {outcomes.push((date,outcome));}
                    }
                }
                if !pending.is_empty() || !outcomes.is_empty() {
                    let deadline=Deadline::recovery();
                    if let Ok(mut publisher)=Publisher::start_owned(broker.clone(),configuration.clone(),1,16*1024,deadline,&transport_parent) {
                        let mut socket=publisher.changes();let mut sequence=0;
                        let delivery:Result<(),String>=async {
                            for (lane,date,terminal) in &pending {
                                let metadata=health[*lane as usize-1].audit_notice(terminal.ordinal);
                                let at=metadata.occurred.or_else(||clock.utc_ticks().ok()).ok_or("Native notification clock unavailable")?;
                                let notice=Notice::new(runtime,*lane,*date,terminal,at)?.correlate(metadata.operation.as_deref(),metadata.record)?;
                                publisher.try_recovery_notice(&notice)?;sequence+=1;
                                deadline.run(&stop,async {
                                    loop {
                                        let status=socket.borrow_and_update().clone();
                                        if let Some(error)=status.failure{return Err(error);}
                                        if status.stopped{return Err("Native notification transport stopped");}
                                        if status.flushed>=sequence{return Ok(());}
                                        socket.changed().await.map_err(|_|"Native notification transport closed")?;
                                    }
                                }).await.map_err(|_|"Native notification delivery interrupted")??;
                            }
                            for (date,body) in &outcomes {
                                publisher.try_recovery_outcome(*date,body)?;sequence+=1;
                                deadline.run(&stop,async {
                                    loop {
                                        let current=socket.borrow_and_update().clone();
                                        if let Some(error)=current.failure{return Err(error);}
                                        if current.stopped{return Err("Native outcome transport stopped");}
                                        if current.flushed>=sequence{return Ok(());}
                                        socket.changed().await.map_err(|_|"Native outcome transport closed")?;
                                    }
                                }).await.map_err(|_|"Native outcome delivery interrupted")??;
                            }
                            Ok(())
                        }.await;
                        // Signal/join our socket even when delivery fails. Nothing
                        // here can hold a dataset's retirement or recovery timer.
                        let _=delivery;
                        let _=publisher.shutdown(Deadline::recovery()).await;
                    }
                }
                let has_pending=!pending.is_empty() || !outcomes.is_empty();
                tokio::select!{biased;
                    ()=stop.cancelled()=>break,
                    result=inputs.changed()=>{if result.is_err(){break;}},
                    result=first.changed()=>{if result.is_err(){break;}},
                    result=second.changed()=>{if result.is_err(){break;}},
                    result=third.changed()=>{if result.is_err(){break;}},
                    ()=async {if has_pending {tokio::time::sleep(Duration::from_secs(5)).await;}else{std::future::pending::<()>().await;}}=>{},
                }
            }
        }).map_err(|e|format!("Native notification task admission: {e:?}"))?;
        Ok(Self {
            parent,
            task: Some(task),
            dates,
            scope,
        })
    }
    /// Root-only copied-value services may share this independent notification owner.
    /// This Arc is never a dataset resource and is joined with root shutdown.
    pub fn audit_parent(&self) -> Arc<Child> {
        self.parent.clone()
    }

    /// Adds copied authoritative dates, never a provider resource or managed
    /// renewal handle. Deployment scope cannot silently change in one lifetime.
    ///
    /// # Errors
    /// Invalid/stopped control or changing deployment scope.
    pub fn date(&self, lane: Lane, date: time::Date, scope: &str) -> Result<(), String> {
        if scope != self.scope || self.parent.cancellation().is_cancelled() {
            return Err("Native notification configuration changed or stopped".into());
        }
        // The API creates a new native owner for each ValueDate epoch. Reject a
        // cross-date reuse rather than relabeling an old retained failure.
        let mut changed_date = false;
        self.dates.send_modify(|dates| {
            let slot = &mut dates[lane as usize - 1];
            if slot.is_some_and(|previous| previous != date) {
                changed_date = true;
            } else {
                *slot = Some(date);
            }
        });
        if changed_date {
            Err("A new ValueDate requires a new native owner epoch".into())
        } else {
            Ok(())
        }
    }
    /// Signals the persistent subtree independently of failed dataset children.
    pub fn cancel(&self) {
        self.parent.cancellation().cancel();
    }
    /// Joins actual socket/task retirement under the caller's original budget.
    ///
    /// # Errors
    /// Incomplete/failed native cleanup is explicit and retains a pending handle.
    pub async fn shutdown(&mut self, deadline: Deadline) -> Result<(), String> {
        self.cancel();
        if let Some(task) = self.task.as_mut() {
            let joined = deadline
                .run(&CancellationToken::new(), task)
                .await
                .map_err(|_| "Native notification join expired")?;
            self.task.take();
            joined.map_err(|_| "Native notification task failed")?;
        }
        self.parent
            .wait_nested(&CancellationToken::new(), deadline)
            .await
            .map_err(|_| "Native notification nested join expired".into())
    }
}
impl Drop for Delivery {
    fn drop(&mut self) {
        self.cancel();
    }
}

#[cfg(test)]
mod outcome_tests {
    use super::*;
    use crate::{
        audit::{journal::Context, outbox},
        runtime::recovery::{Proof, Requirements},
    };
    use tokio::{
        io::{AsyncBufReadExt, AsyncReadExt, AsyncWriteExt, BufReader},
        net::TcpListener,
    };

    #[tokio::test]
    async fn reset_success_is_published_directly_and_retries_keep_the_same_record() {
        let listener = TcpListener::bind("127.0.0.1:0").await.unwrap();
        let address = listener.local_addr().unwrap();
        let (send, mut received) = tokio::sync::mpsc::channel(8);
        let broker = tokio::spawn(async move {
            let mut tasks = tokio::task::JoinSet::new();
            loop {
                let (socket, _) = listener.accept().await.unwrap();
                let send = send.clone();
                tasks.spawn(async move {
                    let (read,mut write)=socket.into_split();let mut read=BufReader::new(read);
                    write.write_all(b"INFO {\"server_id\":\"outcome-fixture\",\"version\":\"2.10.0\",\"proto\":1,\"max_payload\":1048576}\r\n").await.unwrap();
                    let mut line=String::new();
                    loop {
                        line.clear();if read.read_line(&mut line).await.unwrap_or(0)==0{break;}
                        if line.starts_with("PING"){if write.write_all(b"PONG\r\n").await.is_err(){break;}}
                        else if line.starts_with("PUB ") {
                            let size=line.split_whitespace().last().unwrap().parse::<usize>().unwrap();assert!(size<=16384);
                            let mut bytes=vec![0;size+2];read.read_exact(&mut bytes).await.unwrap();
                            if line.contains(".MarketDataRecoveryOutcome.") {send.send(serde_json::from_slice::<serde_json::Value>(&bytes[..size]).unwrap()).await.unwrap();}
                        }
                    }
                });
            }
        });
        let root = Arc::new(Owner::default());
        let clock = Arc::new(Clock::new(17_932_320_000_000_000, false).unwrap());
        let health = std::array::from_fn(|_| {
            Recovery::new(
                Requirements::new(&[
                    Proof::Transport,
                    Proof::Reference,
                    Proof::Pricing,
                    Proof::Publication,
                    Proof::Persistence,
                ]),
                Deadline::recovery(),
            )
        });
        let options = health[2].clone();
        let (sink, _receiver) = outbox::channel();
        let date = time::Date::from_calendar_date(2026, time::Month::October, 29).unwrap();
        options.audit_context(Context {
            runtime: [1; 16],
            lane: Lane::OptionsGlbx,
            date,
            clock: clock.clone(),
            sink,
            workloads: vec![],
        });
        options.monitor_provider();
        options.plan_final_reset(true);
        let settings = BrokerSettings::new(
            format!("nats://{address}"),
            vec!["127.0.0.1:1".parse().unwrap()],
        )
        .unwrap();
        let mut delivery =
            Delivery::start(&root, clock, health, settings, "outcome-test".into()).unwrap();
        delivery
            .date(Lane::OptionsGlbx, date, "outcome-test")
            .unwrap();
        options.fault(
            options.status().ticket,
            "fixture reset",
            Deadline::after(Duration::ZERO),
        );
        options.audit_reset_started();
        options.replace(
            Requirements::new(&[
                Proof::Transport,
                Proof::Reference,
                Proof::Pricing,
                Proof::Publication,
                Proof::Persistence,
            ]),
            Deadline::recovery(),
        );
        options.audit_owner(Some(crate::messaging::ordered::Header {
            lane: Lane::OptionsGlbx,
            value_date: 1,
            worker: [2; 16],
            generation: [3; 16],
            revision: 2,
        }));
        let ticket = options.status().ticket;
        options.prove(ticket, Proof::Retired);
        options.prove(ticket, Proof::Transport);
        let first = tokio::time::timeout(Duration::from_secs(5), received.recv())
            .await
            .unwrap()
            .unwrap();
        assert_eq!(first["Severity"], "success");
        assert_eq!(first["MonitoringReady"], false);
        options.prove(ticket, Proof::Reference);
        let repeated = tokio::time::timeout(Duration::from_secs(7), received.recv())
            .await
            .unwrap()
            .unwrap();
        assert_eq!(
            first, repeated,
            "Broker retries must retain the immutable outcome and original time"
        );
        delivery.shutdown(Deadline::recovery()).await.unwrap();
        drop(delivery);
        root.stop().unwrap();
        root.wait_idle(&CancellationToken::new(), Deadline::recovery())
            .await
            .unwrap();
        assert!(root.disposable().unwrap());
        broker.abort();
        let _ = broker.await;
    }
}
