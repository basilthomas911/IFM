//! Bounded detached evidence handoff. External persistence works on immutable
//! bytes and non-owning receipts; it cannot retain a dataset connection or make
//! native cancellation wait for its acknowledgment.
use super::option_evidence::Evidence;
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use tokio::sync::watch;

/// Immutable first attempt retained for external durable persistence and later
/// actor publication. No native feed or pricing resource is referenced here.
pub struct Record {
    /// Canonical contract copied with evidence; never a subscription handle.
    pub contract: String,
    /// Original source ValueDate as CLR day number, never a later session date.
    pub value_date: i32,
    /// Original logical source generation; replacement cannot relabel evidence.
    pub generation: [u8; 16],
    /// Stable economic source identity used by the existing database key.
    pub identity: String,
    /// Immutable economic-content digest used by the existing conflict check.
    pub digest: String,
    /// Original complete evidence bytes, preserved through duplicate submissions.
    pub bytes: Arc<[u8]>,
    /// Handoff identity, independent of dataset generations and resource handles.
    pub receipt: u64,
    /// Monotonic first submission time; retry cannot extend recovery allowance.
    pub submitted: tokio::time::Instant,
    /// Original application UTC clock, preserved through persistence retries.
    pub origin_ticks: i64,
}
struct Item {
    record: Arc<Record>,
    persisted: bool,
}
struct State {
    items: BTreeMap<String, Item>,
    bytes: usize,
    next: u64,
}
/// Global bounded copied-data staging. This object can outlive a dataset reset,
/// but never its native resource ownership. Capacity exhaustion is an explicit
/// recovery fault: authoritative trade attempts are not overwritten by quotes.
pub struct Outbox {
    state: Mutex<State>,
    maximum: usize,
    bytes: usize,
    changed: watch::Sender<u64>,
}
impl Outbox {
    /// Creates explicit record and encoded-byte limits before admission.
    ///
    /// # Errors
    /// Rejects zero or capacities beyond 4,096 records / 64 MiB.
    pub fn new(maximum: usize, bytes: usize) -> Result<Self, &'static str> {
        if !(1..=4096).contains(&maximum) || !(1..=64 * 1024 * 1024).contains(&bytes) {
            return Err("Invalid evidence outbox capacity");
        }
        let (changed, _) = watch::channel(0);
        Ok(Self {
            state: Mutex::new(State {
                items: BTreeMap::new(),
                bytes: 0,
                next: 0,
            }),
            maximum,
            bytes,
            changed,
        })
    }
    /// Admits validated copied bytes, preserving the first complete pricing attempt
    /// for duplicate source keys, including copies with different content.
    ///
    /// # Errors
    /// Invalid evidence, interrupted writer or exhausted bounded
    /// storage fail without partial mutation or waiting for external I/O.
    pub fn retain(&self, evidence: &Evidence) -> Result<Arc<Record>, &'static str> {
        let _checkpoint=crate::checkpoint::transaction().map_err(|_|"Checkpoint evidence owner interrupted")?;
        let identity = evidence.source.identity()?;
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        if let Some(existing) = state.items.get(&identity) {
            return Ok(existing.record.clone());
        }
        let digest = evidence.source.source_digest()?;
        let bytes = evidence.encode()?;
        if state.items.len() >= self.maximum || bytes.len() > self.bytes.saturating_sub(state.bytes)
        {
            return Err("Native evidence outbox capacity exhausted");
        }
        let receipt = state
            .next
            .checked_add(1)
            .ok_or("Evidence receipt identity exhausted")?;
        let record = Arc::new(Record {
            contract: evidence.source.contract.clone(),
            value_date: evidence.source.date.to_julian_day() - 1_721_426,
            generation: evidence.source.generation,
            identity: identity.clone(),
            digest,
            bytes: bytes.into(),
            receipt,
            submitted: tokio::time::Instant::now(),
            origin_ticks: evidence.at,
        });
        state.next = receipt;
        state.bytes += record.bytes.len();
        state.items.insert(
            identity,
            Item {
                record: record.clone(),
                persisted: false,
            },
        );
        self.changed.send_modify(|v| *v = v.saturating_add(1));
        Ok(record)
    }
    /// Applies a durable external acknowledgment to the exact retained record.
    /// This is distinct from NATS PONG, transport flush and transient enqueue.
    ///
    /// # Errors
    /// Unknown/replaced receipts or economic conflicts never authorize publication.
    pub fn acknowledge(
        &self,
        identity: &str,
        digest: &str,
        receipt: u64,
    ) -> Result<(), &'static str> {
        let _checkpoint=crate::checkpoint::transaction().map_err(|_|"Checkpoint evidence owner interrupted")?;
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        let item = state
            .items
            .get_mut(identity)
            .ok_or("Unknown evidence acknowledgment")?;
        if item.record.receipt != receipt || item.record.digest != digest {
            return Err("Stale or conflicting evidence acknowledgment");
        }
        item.persisted = true;
        self.changed.send_modify(|v| *v = v.saturating_add(1));
        Ok(())
    }
    /// Returns a bounded copied page of pending persistence or acknowledged actor
    /// publications. Retaining this page cannot pin any dataset or outbox mutex.
    ///
    /// # Errors
    /// Invalid page size or interrupted state is explicit.
    pub fn capture(
        &self,
        persisted: bool,
        maximum: usize,
    ) -> Result<Vec<Arc<Record>>, &'static str> {
        if !(1..=64).contains(&maximum) {
            return Err("Invalid evidence handoff page size");
        }
        let state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        let mut ordered: Vec<_> = state
            .items
            .values()
            .filter(|item| item.persisted == persisted)
            .map(|item| &item.record)
            .collect();
        ordered.sort_by_key(|item| item.receipt);
        Ok(ordered.into_iter().take(maximum).cloned().collect())
    }
    /// Releases copied staging only after durable persistence and actor publication
    /// were independently confirmed. Repeated/stale completion cannot remove a
    /// newer attempt reusing the same economic source identity.
    ///
    /// # Errors
    /// Missing, unpersisted or replaced attempts cannot be completed.
    pub fn complete(&self, identity: &str, receipt: u64) -> Result<(), &'static str> {
        let _checkpoint=crate::checkpoint::transaction().map_err(|_|"Checkpoint evidence owner interrupted")?;
        self.remove(identity, receipt, None)
    }

    /// Releases a pending copy when storage confirms it already owns a different attempt.
    ///
    /// # Errors
    /// Stale, mismatched or already acknowledged copies cannot be discarded.
    pub fn discard_duplicate(
        &self,
        identity: &str,
        digest: &str,
        receipt: u64,
    ) -> Result<(), &'static str> {
        self.remove(identity, receipt, Some(digest))
    }

    fn remove(
        &self,
        identity: &str,
        receipt: u64,
        duplicate_digest: Option<&str>,
    ) -> Result<(), &'static str> {
        let mut state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        let item = state
            .items
            .get(identity)
            .ok_or("Unknown evidence completion")?;
        let settled = match duplicate_digest {
            Some(digest) => !item.persisted && item.record.digest == digest,
            None => item.persisted,
        };
        if !settled || item.record.receipt != receipt {
            return Err("Unacknowledged or stale evidence completion");
        }
        let size = item.record.bytes.len();
        state.items.remove(identity);
        state.bytes -= size;
        self.changed.send_modify(|v| *v = v.saturating_add(1));
        Ok(())
    }
    /// Native event-driven handoff notification; no polling timer is needed.
    pub fn changes(&self) -> watch::Receiver<u64> {
        self.changed.subscribe()
    }
    /// Exact pending record and byte counts, captured under one short lock.
    /// No persistence callback or dataset resource is consulted.
    ///
    /// # Errors
    /// An interrupted writer is explicit.
    pub fn retained_counts(&self) -> Result<(usize, usize), &'static str> {
        let state = self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?;
        Ok((state.items.len(), state.bytes))
    }
    /// Samples actual pending evidence, durable receipts and configured bounds.
    pub fn counters(&self) -> Result<BTreeMap<String, i64>, &'static str> {
        let state = self.state.lock().map_err(|_| "Evidence diagnostics interrupted")?;
        Ok([
            ("Evidence pending records", state.items.len() as i64),
            ("Evidence durable records pending publication", state.items.values().filter(|item| item.persisted).count() as i64),
            ("Evidence record capacity", self.maximum as i64),
            ("Evidence retained bytes", state.bytes as i64),
            ("Evidence byte capacity", self.bytes as i64),
            ("Evidence records admitted", state.next.min(i64::MAX as u64) as i64),
        ].into_iter().map(|(name, value)| (name.into(), value)).collect())
    }
    /// Exact bytes owned by this bounded staging directory. Detached external
    /// copies have no relationship to dataset ownership or joining.
    ///
    /// # Errors
    /// An interrupted writer is explicit.
    pub fn retained_bytes(&self) -> Result<usize, &'static str> {
        Ok(self
            .state
            .lock()
            .map_err(|_| "Evidence outbox writer interrupted")?
            .bytes)
    }
}

impl Outbox {
    pub(crate) fn checkpoint(&self)->Result<rmpv::Value,String>{
        use rmpv::Value as V;let state=self.state.lock().map_err(|_|"Evidence outbox writer interrupted")?;
        let rows=state.items.values().map(|i|{let r=&i.record;V::Array(vec![r.contract.clone().into(),r.value_date.into(),V::Binary(r.generation.to_vec()),r.identity.clone().into(),r.digest.clone().into(),V::Binary(r.bytes.to_vec()),r.receipt.into(),(r.submitted.elapsed().as_nanos().min(u64::MAX as u128) as u64).into(),r.origin_ticks.into(),i.persisted.into()])}).collect();
        Ok(V::Array(vec![1.into(),(self.maximum as u64).into(),(self.bytes as u64).into(),state.next.into(),V::Array(rows)]))
    }
    pub(crate) fn restore_checkpoint(&self,value:&rmpv::Value)->Result<(),String>{
        use crate::checkpoint as c;let f=c::fields(value,5)?;
        if c::unsigned(&f[0])?!=1||c::unsigned(&f[1])?!=self.maximum as u64||c::unsigned(&f[2])?!=self.bytes as u64{return Err("Evidence checkpoint policy mismatch".into());}
        let next=c::unsigned(&f[3])?;let mut state=State{items:BTreeMap::new(),bytes:0,next};let mut receipts=std::collections::BTreeSet::new();
        for row in f[4].as_array().filter(|v|v.len()<=self.maximum).ok_or("Evidence checkpoint count exceeded")?{
            let r=c::fields(row,10)?;let generation=match &r[2]{rmpv::Value::Binary(bytes) if bytes.len()==16=>bytes.as_slice().try_into().unwrap(),_=>return Err("Evidence generation invalid".into())};
            let bytes=match &r[5]{rmpv::Value::Binary(bytes) if !bytes.is_empty()=>bytes.clone(),_=>return Err("Evidence bytes invalid".into())};
            state.bytes=state.bytes.checked_add(bytes.len()).filter(|n|*n<=self.bytes).ok_or("Evidence checkpoint bytes exceeded")?;
            let identity=c::string(&r[3],4096)?;let receipt=c::unsigned(&r[6])?;if receipt==0||receipt>next||!receipts.insert(receipt){return Err("Evidence receipt identity invalid".into());}
            let submitted=tokio::time::Instant::now().checked_sub(std::time::Duration::from_nanos(c::unsigned(&r[7])?)).ok_or("Evidence submission age invalid")?;
            let record=Record{contract:c::string(&r[0],256)?,value_date:c::number(&r[1])?.try_into().map_err(|_|"Evidence date overflow")?,generation,identity:identity.clone(),digest:c::string(&r[4],256)?,bytes:bytes.into(),receipt,submitted,origin_ticks:c::number(&r[8])?};
            if state.items.insert(identity,Item{record:Arc::new(record),persisted:c::boolean(&r[9])?}).is_some(){return Err("Evidence duplicate identity".into());}
        }
        *self.state.lock().map_err(|_|"Evidence outbox writer interrupted")?=state;Ok(())
    }
}

#[cfg(test)]
mod checkpoint_tests{
    use super::*;
    #[test]
    fn checkpoint_retains_original_pending_and_persisted_economic_bytes(){
        let outbox=Outbox::new(4,4096).unwrap();let record=Arc::new(Record{contract:"ESZ6C6000".into(),value_date:739900,generation:[7;16],identity:"original-source".into(),digest:"original-digest".into(),bytes:Arc::from([0,1,255]),receipt:9,submitted:tokio::time::Instant::now()-std::time::Duration::from_secs(17),origin_ticks:123});
        {let mut s=outbox.state.lock().unwrap();s.next=9;s.bytes=3;s.items.insert(record.identity.clone(),Item{record,persisted:true});}
        let value=outbox.checkpoint().unwrap();let restored=Outbox::new(4,4096).unwrap();restored.restore_checkpoint(&value).unwrap();
        let s=restored.state.lock().unwrap();let i=&s.items["original-source"];assert!(i.persisted);assert_eq!(i.record.bytes.as_ref(),[0,1,255]);assert_eq!(i.record.receipt,9);assert_eq!(i.record.origin_ticks,123);assert!(i.record.submitted.elapsed()>=std::time::Duration::from_secs(17));assert_eq!(s.next,9);drop(s);
        assert!(Outbox::new(3,4096).unwrap().restore_checkpoint(&value).is_err());
        let mut malformed=value.clone();if let rmpv::Value::Array(f)=&mut malformed {if let rmpv::Value::Array(rows)=&mut f[4]{rows.push(rows[0].clone());}}
        assert!(restored.restore_checkpoint(&malformed).is_err());assert_eq!(restored.state.lock().unwrap().items.len(),1);
    }
}
