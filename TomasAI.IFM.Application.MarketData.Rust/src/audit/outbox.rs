//! Bounded disk-backed delivery outside dataset ownership. Capture never awaits I/O.
//! Durability begins after file sync+atomic rename, not when try-send returns.
use super::protocol::{Record, RecordIndex};
use crate::{
    messaging::publisher::{BrokerSettings, Publisher},
    runtime::{Deadline, clock::Clock, owner::Child},
};
use std::{
    collections::HashMap,
    path::{Path, PathBuf},
    sync::{
        Arc,
        atomic::{AtomicU64, Ordering},
    },
    time::Duration,
};
use tokio::{
    io::{AsyncReadExt, AsyncWriteExt},
    sync::{Notify, OwnedSemaphorePermit, Semaphore, mpsc},
};
use tokio_util::sync::CancellationToken;

const COMMIT_BATCH: usize = 16;

/// Bounded operational counters. These are audit health, never dataset readiness proofs.
#[derive(Default)]
pub struct Health {
    /// Facts waiting in durable storage.
    pub pending: AtomicU64,
    /// Committed UTF-8 payload bytes.
    pub bytes: AtomicU64,
    /// Failed write, capacity, invalid entry or delivery attempts.
    pub failures: AtomicU64,
    /// Successful durable acknowledgements.
    pub acknowledged: AtomicU64,
    /// Attempts, retries and failure classes remain separate from dataset recovery counters.
    pub attempts: AtomicU64,
    /// Repeated delivery attempts within this runtime.
    pub retries: AtomicU64,
    /// Durable write failures, including capacity.
    pub write_failures: AtomicU64,
    /// Broker/function/acknowledgement failures.
    pub delivery_failures: AtomicU64,
    /// Locally rejected files retained for inspection.
    pub rejected: AtomicU64,
    /// Capture failures before durable commitment.
    pub capture_failures: AtomicU64,
    /// API-clock age of the oldest pending fact; u64::MAX means unknown.
    pub oldest_age_ms: AtomicU64,
    last_error: std::sync::Mutex<Option<String>>,
}
impl Health {
    fn failure(&self, detail: &str) {
        self.failures.fetch_add(1, Ordering::Relaxed);
        let detail: String = detail.chars().take(4096).collect();
        *self.last_error.lock().unwrap_or_else(|e| e.into_inner()) = Some(detail.clone());
        eprintln!("RecoveryAuditUnavailable: {detail}");
    }
}
/// Nonblocking ingress. It holds no cancellation guard, task, provider lease or socket.
#[derive(Clone)]
pub struct Sink {
    sender: mpsc::Sender<Captured>,
    health: Arc<Health>,
    capacity: Arc<Semaphore>,
    ordinary: Arc<Semaphore>,
}
#[derive(Debug)]
pub(crate) struct Captured {
    pub payload: String,
    pub terminal: bool,
    _capacity: OwnedSemaphorePermit,
    _ordinary: Option<OwnedSemaphorePermit>,
}
impl Sink {
    /// Reports a rejected fact without blocking the dataset on diagnostics I/O.
    pub fn reject_capture(&self) {
        self.health.failures.fetch_add(1, Ordering::Relaxed);
        self.health.capture_failures.fetch_add(1, Ordering::Relaxed);
    }
    /// Copies one fact. Capacity failure is explicit, never a reason to delay retirement.
    pub fn capture(&self, payload: String, terminal: bool) {
        let admit = || -> Result<Captured, ()> {
            if payload.len() > 256 * 1024 {
                return Err(());
            }
            let ordinary = if terminal {
                None
            } else {
                Some(self.ordinary.clone().try_acquire_owned().map_err(|_| ())?)
            };
            let capacity = self.capacity.clone().try_acquire_owned().map_err(|_| ())?;
            Ok(Captured {
                payload,
                terminal,
                _capacity: capacity,
                _ordinary: ordinary,
            })
        };
        if admit()
            .and_then(|value| self.sender.try_send(value).map_err(|_| ()))
            .is_err()
        {
            // No stderr/file/broker I/O on the dataset capture path.
            self.reject_capture();
        }
    }
    /// Bounded existing-backend-status extension; observation never controls dataset readiness.
    pub fn snapshot(&self) -> rmpv::Value {
        use rmpv::Value;
        let count = |value: &AtomicU64| {
            Value::from(value.load(Ordering::Relaxed).min(i64::MAX as u64) as i64)
        };
        let age = self.health.oldest_age_ms.load(Ordering::Relaxed);
        let error = if self.health.capture_failures.load(Ordering::Relaxed) > 0 {
            Some("At least one recovery fact was rejected before durable commitment".to_string())
        } else {
            self.health
                .last_error
                .lock()
                .unwrap_or_else(|e| e.into_inner())
                .clone()
        };
        Value::Array(vec![
            count(&self.health.pending),
            count(&self.health.bytes),
            if age == u64::MAX {
                Value::Nil
            } else {
                Value::from(age.min(i64::MAX as u64) as i64)
            },
            count(&self.health.attempts),
            count(&self.health.retries),
            count(&self.health.write_failures),
            count(&self.health.delivery_failures),
            count(&self.health.rejected),
            count(&self.health.capture_failures),
            Value::from((256 - self.capacity.available_permits()) as u64),
            error.map_or(Value::Nil, Value::from),
        ])
    }
    /// Copied audit health counters.
    pub fn health(&self) -> &Arc<Health> {
        &self.health
    }
}
/// Validated durable spool. File names are derived only from validated UUID record IDs.
pub struct Spool {
    directory: PathBuf,
    maximum_records: usize,
    maximum_bytes: u64,
    io: tokio::sync::Mutex<HashMap<String, RecordIndex>>,
    used_bytes: AtomicU64,
    stored_entries: AtomicU64,
    rejected_entries: AtomicU64,
}
impl Spool {
    /// Opens a configured location. It must be outside disposable build/package directories.
    pub fn new(directory: PathBuf) -> Self {
        Self {
            directory,
            maximum_records: 4096,
            maximum_bytes: 128 * 1024 * 1024,
            io: tokio::sync::Mutex::new(HashMap::new()),
            used_bytes: AtomicU64::new(0),
            stored_entries: AtomicU64::new(0),
            rejected_entries: AtomicU64::new(0),
        }
    }
    /// Override bounded capacities for qualification/deployment.
    pub fn limits(mut self, records: usize, bytes: u64) -> Self {
        self.maximum_records = records.min(4096);
        self.maximum_bytes = bytes.min(128 * 1024 * 1024);
        self
    }
    /// Enumerates bounded committed entries. Partial/corrupt files are rejected explicitly.
    /// # Errors
    /// Storage outage, invalid content, wrong identity/digest or exhausted bounds.
    pub async fn read(&self) -> Result<Vec<Record>, String> {
        let mut index = self.io.lock().await;
        self.read_unlocked(&mut index).await
    }
    async fn read_unlocked(
        &self,
        index: &mut HashMap<String, RecordIndex>,
    ) -> Result<Vec<Record>, String> {
        tokio::fs::create_dir_all(&self.directory)
            .await
            .map_err(|e| e.to_string())?;
        let mut directory = tokio::fs::read_dir(&self.directory)
            .await
            .map_err(|e| e.to_string())?;
        let mut records = Vec::new();
        let mut next_index = HashMap::with_capacity(index.len());
        let mut bytes = 0u64;
        let mut entries = 0;
        let mut rejected = 0;
        while let Some(entry) = directory.next_entry().await.map_err(|e| e.to_string())? {
            entries += 1;
            if entries > self.maximum_records + 32 {
                return Err("Recovery audit directory capacity exceeded".into());
            }
            let path = entry.path();
            let disk = entry.metadata().await.map_err(|e| e.to_string())?;
            if disk.is_file() {
                bytes = bytes.saturating_add(disk.len());
            }
            if path.extension().is_some_and(|v| v == "rejected" || v == "blocked") {
                rejected += 1;
            }
            if bytes > self.maximum_bytes {
                return Err("Recovery audit physical storage capacity exceeded".into());
            }
            if path.extension().is_some_and(|v| v == "tmp") {
                let valid = if disk.is_file() && disk.len() <= 256 * 1024 + 65 {
                    read_record(&path).await.ok()
                } else {
                    None
                };
                if let Some(record) =
                    valid.filter(|r| path.file_stem().and_then(|s| s.to_str()) == Some(&r.id))
                {
                    let target = self.directory.join(format!("{}.audit", record.id));
                    if tokio::fs::try_exists(&target)
                        .await
                        .map_err(|e| e.to_string())?
                    {
                        let prior = read_record(&target).await?;
                        if prior.payload != record.payload {
                            return Err("Conflicting incomplete recovery audit write".into());
                        }
                        tokio::fs::remove_file(&path)
                            .await
                            .map_err(|e| e.to_string())?;
                    } else {
                        tokio::fs::rename(&path, target)
                            .await
                            .map_err(|e| e.to_string())?;
                    }
                    return Err(
                        "Recovered complete interrupted audit write; replay will resume".into(),
                    );
                }
                tokio::fs::rename(&path, path.with_extension("rejected"))
                    .await
                    .map_err(|e| e.to_string())?;
                return Err(
                    "Invalid incomplete recovery audit write quarantined for inspection".into(),
                );
            }
            if path.extension().is_none_or(|v| v != "audit") {
                continue;
            }
            if !disk.is_file() || disk.len() > 256 * 1024 + 65 {
                tokio::fs::rename(&path, path.with_extension("rejected"))
                    .await
                    .map_err(|e| e.to_string())?;
                return Err("Invalid audit entry type/size retained for inspection".into());
            }
            if bytes > self.maximum_bytes || records.len() >= self.maximum_records {
                return Err("Recovery audit durable capacity exceeded".into());
            }
            let id = path.file_stem().and_then(|s| s.to_str());
            let cached = id.and_then(|id| index.remove(id));
            let record = match read_indexed_record(&path, cached.as_ref()).await {
                Ok(record) => record,
                Err(error) => {
                    tokio::fs::rename(&path, path.with_extension("rejected"))
                        .await
                        .map_err(|e| e.to_string())?;
                    return Err(error);
                }
            };
            if id != Some(&record.id) {
                tokio::fs::rename(&path, path.with_extension("rejected"))
                    .await
                    .map_err(|e| e.to_string())?;
                return Err("Recovery audit file identity conflict".into());
            }
            let metadata = cached
                .filter(|cached| cached.digest == record.digest)
                .unwrap_or_else(|| record.index());
            next_index.insert(record.id.clone(), metadata);
            records.push(record);
        }
        *index = next_index;
        self.used_bytes.store(bytes, Ordering::Relaxed);
        self.stored_entries.store(entries as u64, Ordering::Relaxed);
        self.rejected_entries.store(rejected, Ordering::Relaxed);
        records.sort_by(|a, b| {
            a.operation
                .cmp(&b.operation)
                .then(a.sequence.cmp(&b.sequence))
        });
        Ok(records)
    }
    /// Syncs one immutable payload before atomic commit. Identical retries are idempotent.
    /// # Errors
    /// Conflicting content, disk/full capacity, invalid partial writes or filesystem failure.
    pub async fn commit(&self, record: &Record, terminal: bool) -> Result<(), String> {
        Self::check_commit(record)?;
        let mut index = self.io.lock().await;
        let mut records = self.read_unlocked(&mut index).await?;
        self.commit_unlocked(record, terminal, &mut records).await
    }

    // Amortize the directory scan across a bounded writer batch. Each fact
    // still has its own sync, atomic rename, outcome and recoverable temp file.
    pub(super) async fn commit_batch<'a>(
        &self,
        batch: impl ExactSizeIterator<Item = (&'a Record, bool)>,
    ) -> Vec<Result<(), String>> {
        assert!(batch.len() <= COMMIT_BATCH);
        let mut index = self.io.lock().await;
        let mut records = None;
        let mut results = Vec::with_capacity(batch.len());
        for (record, terminal) in batch {
            let result = async {
                Self::check_commit(record)?;
                let records = match &mut records {
                    Some(records) => records,
                    None => records.insert(self.read_unlocked(&mut index).await?),
                };
                self.commit_unlocked(record, terminal, records).await
            }
            .await;
            if result.is_err() {
                // An interrupted write may have left a partial or committed file.
                // Revalidate the directory before admitting the following record.
                records = None;
            }
            results.push(result);
        }
        results
    }

    fn check_commit(record: &Record) -> Result<(), String> {
        let checked = Record::new(record.payload.clone())?;
        if checked.id != record.id
            || checked.digest != record.digest
            || checked.operation != record.operation
            || checked.sequence != record.sequence
        {
            return Err("Invalid audit commit identity".into());
        }
        Ok(())
    }

    async fn commit_unlocked(
        &self,
        record: &Record,
        terminal: bool,
        records: &mut Vec<Record>,
    ) -> Result<(), String> {
        if let Some(prior) = records.iter().find(|r| r.id == record.id) {
            return if prior.digest == record.digest && prior.payload == record.payload {
                Ok(())
            } else {
                Err("Recovery audit RecordId conflict".into())
            };
        }
        let reserve = if terminal {
            0
        } else {
            self.maximum_records.min(32)
        };
        let byte_reserve = if terminal {
            0
        } else {
            self.maximum_bytes.min(32 * (256 * 1024 + 65))
        };
        if self.stored_entries.load(Ordering::Relaxed) as usize
            >= self.maximum_records.saturating_sub(reserve)
            || self
                .used_bytes
                .load(Ordering::Relaxed)
                .saturating_add((record.payload.len() + 65) as u64)
                > self.maximum_bytes.saturating_sub(byte_reserve)
        {
            return Err("Recovery audit durable capacity exhausted".into());
        }
        let target = self.directory.join(format!("{}.audit", record.id));
        let partial = self.directory.join(format!("{}.tmp", record.id));
        let mut file = tokio::fs::OpenOptions::new()
            .write(true)
            .create_new(true)
            .open(&partial)
            .await
            .map_err(|e| e.to_string())?;
        file.write_all(format!("{}\n", record.digest).as_bytes())
            .await
            .map_err(|e| e.to_string())?;
        file.write_all(record.payload.as_bytes())
            .await
            .map_err(|e| e.to_string())?;
        file.sync_all().await.map_err(|e| e.to_string())?;
        drop(file);
        tokio::fs::rename(partial, target)
            .await
            .map_err(|e| e.to_string())?;
        self.stored_entries.fetch_add(1, Ordering::Relaxed);
        self.used_bytes
            .fetch_add((record.payload.len() + 65) as u64, Ordering::Relaxed);
        records.push(record.clone());
        // A late genuine creation fact makes the preserved successors retryable.
        // No history is manufactured and no blocked bytes are acknowledged.
        if record.sequence == 1 {
            let mut directory = tokio::fs::read_dir(&self.directory).await.map_err(|e| e.to_string())?;
            while let Some(entry) = directory.next_entry().await.map_err(|e| e.to_string())? {
                let path = entry.path();
                if path.extension().is_some_and(|value| value == "blocked") {
                    let blocked = read_record(&path).await?;
                    if blocked.operation == record.operation {
                        tokio::fs::rename(&path, path.with_extension("audit")).await.map_err(|e| e.to_string())?;
                        records.push(blocked);
                    }
                }
            }
        }
        Ok(())
    }
    /// Deletes only after content-matched durable Function completion.
    /// # Errors
    /// Missing, changed or unacknowledged entry; filesystem deletion failure.
    pub async fn acknowledge(&self, record: &Record, receipt: &[u8]) -> Result<(), String> {
        let checked = Record::new(record.payload.clone())?;
        if checked.id != record.id || checked.digest != record.digest {
            return Err("Invalid audit acknowledgement identity".into());
        }
        record.acknowledge(receipt)?;
        let mut index = self.io.lock().await;
        let path = self.directory.join(format!("{}.audit", record.id));
        let current = read_indexed_record(&path, index.get(&record.id)).await?;
        if current.payload != record.payload {
            return Err("Recovery audit changed before acknowledgement".into());
        }
        tokio::fs::remove_file(path)
            .await
            .map_err(|e| e.to_string())?;
        index.remove(&record.id);
        Ok(())
    }

    /// Retains an entire rejected incident for reconciliation without acknowledging
    /// or rewriting any source bytes. Other incidents can continue to drain.
    /// # Errors
    /// Changed content, conflicting quarantine or unavailable storage.
    pub async fn block_incident(&self, record: &Record) -> Result<(), String> {
        let mut index = self.io.lock().await;
        let records = self.read_unlocked(&mut index).await?;
        if !records.iter().any(|value| value.id == record.id && value.digest == record.digest) {
            return Err("Recovery quarantine identity changed".into());
        }
        for value in records.iter().filter(|value| value.operation == record.operation) {
            let path = self.directory.join(format!("{}.audit", value.id));
            let target = path.with_extension("blocked");
            if tokio::fs::try_exists(&target).await.map_err(|e| e.to_string())? {
                return Err("Recovery quarantine already exists".into());
            }
            tokio::fs::rename(path, target).await.map_err(|e| e.to_string())?;
        }
        Ok(())
    }
}

// Missing source history is not an acknowledgement. Bound repeated explicit
// rejections; transient broker/timeout failures must remain retryable.
fn should_block(error: &str, attempts: u64) -> bool {
    attempts >= 3 && error.contains("Recovery sequence predecessor missing or conflicting.")
}

#[cfg(test)]
mod rejection_tests {
    use super::*;

    #[test]
    fn only_repeated_explicit_sequence_rejections_are_blocked() {
        let rejection = "Recovery audit storage rejected: Recovery sequence predecessor missing or conflicting.";
        assert!(!should_block(rejection, 2));
        assert!(should_block(rejection, 3));
        assert!(!should_block("Audit delivery attempt expired or canceled; commit outcome unknown", 1000));
        assert!(!should_block("No responders", 1000));
    }
}
/// Starts independent writer and bounded deliveries in the root notification subtree.
/// A dataset's cancellation cannot stop this outbox, and this outbox holds no dataset child.
/// # Errors
/// Root stopping/task admission is explicit; no dataset command waits for disk or delivery.
pub fn start(
    parent: Arc<Child>,
    broker: BrokerSettings,
    scope: String,
    clock: Arc<Clock>,
    directory: PathBuf,
) -> Result<Sink, String> {
    let health = Arc::new(Health::default());
    let (sender, mut incoming) = mpsc::channel::<Captured>(256);
    let notify = Arc::new(Notify::new());
    let spool = Arc::new(Spool::new(directory));
    let writer_spool = spool.clone();
    let writer_health = health.clone();
    let writer_notify = notify.clone();
    parent.spawn_joined(move|stop|async move {
        let mut pending=std::collections::VecDeque::new();
        'writer: loop {
            tokio::select! {biased;
                ()=stop.cancelled()=>break,
                value=incoming.recv()=>match value {Some(value)=>pending.push_back(value),None=>break},
                ()=async {if pending.is_empty(){std::future::pending::<()>().await;}else{tokio::time::sleep(Duration::from_secs(5)).await;}}=>{}
            }
            while pending.len() < COMMIT_BATCH {
                let Ok(value) = incoming.try_recv() else { break };
                pending.push_back(value);
            }
            let mut remaining = pending.len();
            while remaining != 0 {
                let count = remaining.min(COMMIT_BATCH);
                remaining -= count;
                let batch = pending.drain(..count).filter_map(|captured| {
                    match Record::new(captured.payload.clone()) {
                        Ok(record) => Some((captured, record)),
                        Err(error) => { writer_health.failure(&error); None }
                    }
                }).collect::<Vec<_>>();
                let results = tokio::select! { biased;
                    () = stop.cancelled() => {
                        for (captured, _) in batch.into_iter().rev() {
                            pending.push_front(captured);
                        }
                        break 'writer;
                    }
                    results = writer_spool.commit_batch(
                        batch.iter().map(|(captured, record)| (record, captured.terminal))
                    ) => results,
                };
                for ((captured, _), result) in batch.into_iter().zip(results) {
                    match result {
                        Ok(()) => writer_notify.notify_one(),
                        Err(error) => {
                            writer_health.write_failures.fetch_add(1, Ordering::Relaxed);
                            writer_health.failure(&error);
                            pending.push_back(captured);
                        }
                    }
                }
            }
        }
        // Graceful root stop commits already captured values once; never wait
        // for NATS/actors. This is an audit shutdown budget, not dataset recovery.
        incoming.close();while let Ok(value)=incoming.try_recv(){pending.push_back(value);}
        let flushed=Deadline::recovery().run(&CancellationToken::new(),async {
            for captured in pending {
                match Record::new(captured.payload).map(|record|(record,captured.terminal)) {
                    Ok((record,terminal))=>if let Err(error)=writer_spool.commit(&record,terminal).await {writer_health.failure(&error);},
                    Err(error)=>writer_health.failure(&error),
                }
            }
        }).await;
        if flushed.is_err(){writer_health.failure("Recovery audit shutdown flush incomplete; uncommitted records are not durable");}
    }).map_err(|e|format!("Recovery audit writer admission: {e:?}"))?;
    let delivery_parent = parent.clone();
    let delivery_health = health.clone();
    parent.spawn_joined(move|stop|async move {
        let mut reported=0;let mut attempts=std::collections::BTreeMap::<String,u64>::new();
        loop {
            if stop.is_cancelled(){break;}
            let failures=delivery_health.failures.load(Ordering::Relaxed);
            if failures!=reported {eprintln!("RecoveryAuditHealth failures={failures} pending={} bytes={} acknowledged={}",delivery_health.pending.load(Ordering::Relaxed),delivery_health.bytes.load(Ordering::Relaxed),delivery_health.acknowledged.load(Ordering::Relaxed));reported=failures;}
            match spool.read().await {
                Ok(records)=>{
                    attempts.retain(|id,_|records.iter().any(|record|&record.id==id));
                    delivery_health.rejected.store(spool.rejected_entries.load(Ordering::Relaxed),Ordering::Relaxed);
                    let now=clock.utc_ticks().ok();
                    let oldest=records.iter().filter_map(|record|record.occurred_ticks).min();
                    let age=if records.is_empty(){0}else{now.zip(oldest).and_then(|(now,oldest)|u64::try_from(now.saturating_sub(oldest)/10_000).ok()).unwrap_or(u64::MAX)};
                    delivery_health.oldest_age_ms.store(age,Ordering::Relaxed);
                    delivery_health.pending.store(records.len() as u64,Ordering::Relaxed);
                    delivery_health.bytes.store(records.iter().map(|r|r.payload.len() as u64).sum(),Ordering::Relaxed);
                    let mut operations=std::collections::HashSet::new();
                    let first:Vec<_>=records.into_iter().filter(|r|operations.insert(r.operation.clone())).take(4).collect();
                    for record in &first {let attempt=attempts.entry(record.id.clone()).or_default();if *attempt>0{delivery_health.retries.fetch_add(1,Ordering::Relaxed);}*attempt=attempt.saturating_add(1);delivery_health.attempts.fetch_add(1,Ordering::Relaxed);}
                    let mut failed=false;
                    let deliveries=first.iter().map(|record|deliver(&delivery_parent,broker.clone(),scope.clone(),clock.clone(),record,&stop));
                    for (record,result) in first.iter().zip(futures::future::join_all(deliveries).await) {
                        match result {
                            Ok(reply)=>match spool.acknowledge(record,&reply).await {Ok(())=>{delivery_health.acknowledged.fetch_add(1,Ordering::Relaxed);notify.notify_one();},Err(e)=>{failed=true;delivery_health.delivery_failures.fetch_add(1,Ordering::Relaxed);delivery_health.failure(&e)}},
                            Err(e)=>{
                                failed=true;delivery_health.delivery_failures.fetch_add(1,Ordering::Relaxed);delivery_health.failure(&e);
                                if should_block(&e,attempts.get(&record.id).copied().unwrap_or(0)) {
                                    match spool.block_incident(record).await {
                                        Ok(())=>eprintln!("RecoveryAuditBlocked operation={} record={}: exact files retained for predecessor reconciliation",record.operation,record.id),
                                        Err(error)=>delivery_health.failure(&error),
                                    }
                                }
                            },
                        }
                    }
                    if !failed && spool.rejected_entries.load(Ordering::Relaxed)==0 {*delivery_health.last_error.lock().unwrap_or_else(|e|e.into_inner())=None;}
                },Err(e)=>delivery_health.failure(&e),
            }
            tokio::select!{biased;()=stop.cancelled()=>break,()=notify.notified()=>{},()=tokio::time::sleep(Duration::from_secs(5))=>{}}
        }
    }).map_err(|e|format!("Recovery audit delivery admission: {e:?}"))?;
    Ok(Sink {
        sender,
        health,
        capacity: Arc::new(Semaphore::new(256)),
        ordinary: Arc::new(Semaphore::new(224)),
    })
}
async fn deliver(
    parent: &Child,
    broker: BrokerSettings,
    scope: String,
    clock: Arc<Clock>,
    record: &Record,
    stop: &CancellationToken,
) -> Result<Vec<u8>, String> {
    let deadline = Deadline::recovery();
    let at = clock
        .utc_ticks()
        .map_err(|_| "Audit delivery clock unavailable")?;
    let (subject, bytes) = record.request(at)?;
    let (mut publisher, mut replies) =
        Publisher::start_evidence_exchange(broker, scope, 1, 512 * 1024, deadline, parent)?;
    let mut status = publisher.changes();
    let result = deadline
        .run(stop, async {
            loop {
                let current = status.borrow_and_update().clone();
                if current.failure.is_some() || current.stopped {
                    return Err(current
                        .detail
                        .unwrap_or_else(|| "Audit broker unavailable".into()));
                }
                if current.connected {
                    break;
                }
                status
                    .changed()
                    .await
                    .map_err(|_| "Audit broker channel closed")?;
            }
            publisher.try_recovery_record(subject, bytes, record.occurred_ticks)?;
            let reply = replies.recv().await.ok_or("Audit reply channel closed")?;
            record.acknowledge(&reply)?;
            Ok(reply)
        })
        .await
        .map_err(|_| {
            "Audit delivery attempt expired or canceled; commit outcome unknown".to_string()
        })
        .and_then(|v| v);
    // Independent audit cleanup does not retain dataset resources.
    let _ = publisher.shutdown(deadline).await;
    result
}
/// Standard persistent location, overridable without rebuilding native code.
/// # Errors
/// Missing configured/local application data directory.
pub fn location(scope: &str) -> Result<PathBuf, String> {
    if let Some(path) = std::env::var_os("IFM_MARKETDATA_RECOVERY_OUTBOX") {
        return Ok(Path::new(&path).join(scope));
    }
    std::env::var_os("LOCALAPPDATA")
        .map(|p| {
            Path::new(&p)
                .join("TomasAI/IFM/MarketData/RecoveryOutbox")
                .join(scope)
        })
        .ok_or_else(|| "Recovery audit durable path is not configured".into())
}

#[cfg(test)]
pub(crate) fn channel() -> (Sink, mpsc::Receiver<Captured>) {
    let (sender, receiver) = mpsc::channel(256);
    (
        Sink {
            sender,
            health: Arc::new(Health::default()),
            capacity: Arc::new(Semaphore::new(256)),
            ordinary: Arc::new(Semaphore::new(224)),
        },
        receiver,
    )
}

// The integrity prefix is persisted, not recomputed as a substitute for validation.
// take() also bounds races with an externally growing file after metadata inspection.
async fn read_record(path: &Path) -> Result<Record, String> {
    read_indexed_record(path, None).await
}

async fn read_indexed_record(path: &Path, cached: Option<&RecordIndex>) -> Result<Record, String> {
    let file = tokio::fs::File::open(path)
        .await
        .map_err(|e| e.to_string())?;
    let mut bytes = Vec::new();
    file.take(256 * 1024 + 66)
        .read_to_end(&mut bytes)
        .await
        .map_err(|e| e.to_string())?;
    if bytes.len() < 65 || bytes.len() > 256 * 1024 + 65 || bytes[64] != b'\n' {
        return Err("Invalid audit integrity framing/size".into());
    }
    let prefix = std::str::from_utf8(&bytes[..64]).map_err(|_| "Invalid audit digest")?;
    let payload = String::from_utf8(bytes[65..].to_vec()).map_err(|_| "Invalid audit UTF-8")?;
    let digest = Record::content_digest(payload.as_bytes());
    if digest != prefix {
        return Err("Recovery audit checksum mismatch".into());
    }
    // Compare the exact disk bytes to the stored digest before reusing metadata.
    // A replaced file must pass the same JSON and identity validation as a cold load.
    match cached.filter(|value| value.digest == digest) {
        Some(value) => Ok(value.restore(payload, digest)),
        None => Record::parse_verified(payload, digest),
    }
}
