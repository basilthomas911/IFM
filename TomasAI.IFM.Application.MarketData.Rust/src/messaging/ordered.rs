//! One serialized actor publication stream per native dataset. Quotes and
//! durably acknowledged trade evidence share both sequence allocation and the
//! same broker queue; separate TCP writers cannot reorder their envelope IDs.
use super::{
    current::Current,
    publisher::{Port, Status},
    wire::Frame,
};
use crate::{dataset::transport::Lane, runtime::Deadline};
use std::sync::{Arc, Mutex};
use tokio::sync::watch;
use tokio_util::sync::CancellationToken;
/// Immutable native logical dataset envelope identity.
#[derive(Clone)]
pub struct Header {
    /// Dataset and role.
    pub lane: Lane,
    /// API trading date as CLR day number.
    pub value_date: i32,
    /// Native logical worker in CLR Guid byte order.
    pub worker: [u8; 16],
    /// Native logical generation in CLR Guid byte order.
    pub generation: [u8; 16],
    /// Positive desired-manifest revision.
    pub revision: i64,
}
/// Bounded shared sequencing; contains copied publications and no provider owner.
pub struct Ordered {
    port: Port,
    header: Header,
    sequence: Mutex<i64>,
    current: Arc<Current>,
    clock: Option<Arc<crate::runtime::clock::Clock>>,
}
impl Ordered {
    pub(crate) fn checkpoint_publications_owned(&self)->Result<bool,String>{self.port.checkpoint_has_durable_ownership()}

    /// Validates the logical envelope before any submission. The port's owner
    /// must be dedicated to this stream; control/diagnostic requests use other IO.
    ///
    /// # Errors
    /// Invalid logical identity or retained snapshot bounds fail construction.
    pub fn new(port: Port, header: Header) -> Result<Arc<Self>, &'static str> {
        Self::create(port, header, None)
    }
    /// Creates a production sequencer using the application-supplied clock.
    ///
    /// # Errors
    /// Invalid source identity or retained bounds.
    pub fn new_with_clock(port: Port, header: Header, clock: Arc<crate::runtime::clock::Clock>) -> Result<Arc<Self>, &'static str> {
        Self::create(port, header, Some(clock))
    }
    fn create(port: Port, header: Header, clock: Option<Arc<crate::runtime::clock::Clock>>) -> Result<Arc<Self>, &'static str> {
        Frame {
            lane: header.lane,
            value_date: header.value_date,
            worker: header.worker,
            generation: header.generation,
            manifest_revision: header.revision,
            sequence: 1,
            kind: 3,
            payload: vec![0xc0],
            origin_ticks: None,
        }
        .content()?;
        Ok(Arc::new(Self {
            port,
            header,
            clock,
            sequence: Mutex::new(0),
            current: Arc::new(Current::new(crate::option_chain::MAX_LIVE_OPTIONS, 32 * 1024 * 1024)?),
        }))
    }
    /// Atomically allocates and queues one sequence, with no asynchronous work
    /// under the short sequencer lock. Optional latest retention is copied only.
    ///
    /// # Errors
    /// Stale generation, overflow, stopped transport or bounded capacity is a
    /// native recovery fault. Failed enqueue does not consume a sequence number.
    pub fn submit(
        &self,
        kind: u8,
        payload: Vec<u8>,
        generation: [u8; 16],
        contract: Option<&str>,
    ) -> Result<u64, &'static str> {
        let _checkpoint=crate::checkpoint::transaction().map_err(|_|"Checkpoint publication owner interrupted")?;
        if generation != self.header.generation {
            return Err("Retired native publication generation");
        }
        let mut sequence = self
            .sequence
            .lock()
            .map_err(|_| "Native publication sequencer interrupted")?;
        let next = sequence
            .checked_add(1)
            .ok_or("Native publication sequence exhausted")?;
        let frame = Frame {
            lane: self.header.lane,
            value_date: self.header.value_date,
            worker: self.header.worker,
            generation,
            manifest_revision: self.header.revision,
            sequence: next,
            kind,
            payload,
            origin_ticks: self.clock.as_ref().map(|clock| clock.utc_ticks().map_err(|_| "Publication clock unavailable")).transpose()?,
        };
        if let Some(contract) = contract {
            self.current.retain(contract, &frame)?;
        }
        self.port.try_publish(&frame)?;
        *sequence = next;
        Ok(next as u64)
    }
    /// Waits for the actual shared writer's PONG under the original budget.
    /// A broker ACK is transport evidence, never a durable database receipt.
    ///
    /// # Errors
    /// Cancelled/deadline-expired/failed transport cannot confirm a publication.
    pub async fn confirmed(
        &self,
        sequence: u64,
        stop: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), &'static str> {
        if sequence == 0 {
            return Err("Invalid native publication confirmation sequence");
        }
        let mut progress = self.port.changes();
        loop {
            if stop.is_cancelled() || deadline.remaining().is_zero() {
                return Err("Native ordered publication original deadline expired");
            }
            let state = progress.borrow_and_update().clone();
            if state.failure.is_some() || state.stopped {
                return Err("Native ordered publication transport failed");
            }
            if state.flushed >= sequence {
                return Ok(());
            }
            deadline
                .run(stop, progress.changed())
                .await
                .map_err(|_| "Native ordered publication original deadline expired")?
                .map_err(|_| "Native ordered publication observation closed")?;
        }
    }
    /// Copied transport state for native readiness supervision.
    pub fn status(&self) -> Status {
        self.port.status()
    }
    /// Copies enqueue sequence and actual shared broker queue diagnostics.
    pub fn counters(&self) -> Result<std::collections::BTreeMap<String, i64>, &'static str> {
        let mut values = self.port.counters();
        values.insert("Publication frames submitted".into(), *self.sequence.lock().map_err(|_| "Publication diagnostics interrupted")?);
        Ok(values)
    }
    /// Event-driven protocol progress, with no polling receiver.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.port.changes()
    }
    /// Detached latest-value query storage; retaining it cannot pin the writer.
    pub fn current(&self) -> Arc<Current> {
        self.current.clone()
    }
}

impl Ordered{
    pub(crate) fn checkpoint(&self)->Result<rmpv::Value,String>{
        use rmpv::Value as V;let sequence=*self.sequence.lock().map_err(|_|"Ordered sequencer interrupted")?;let h=&self.header;
        Ok(V::Array(vec![1.into(),(h.lane as u8).into(),h.value_date.into(),V::Binary(h.worker.to_vec()),V::Binary(h.generation.to_vec()),h.revision.into(),sequence.into(),self.current.checkpoint()?]))
    }
    pub(crate) fn restore_checkpoint(&self,value:&rmpv::Value)->Result<(),String>{
        use crate::checkpoint as c;let f=c::fields(value,8)?;let h=&self.header;
        if c::unsigned(&f[0])?!=1||c::unsigned(&f[1])?!=h.lane as u64||c::number(&f[2])?!=i64::from(h.value_date)||f[3].as_slice()!=Some(h.worker.as_slice())||f[4].as_slice()!=Some(h.generation.as_slice())||c::number(&f[5])?!=h.revision{return Err("Original option publication header mismatch".into());}
        let next=c::number(&f[6])?;if next<0{return Err("Negative ordered source sequence".into());}let mut sequence=self.sequence.lock().map_err(|_|"Ordered sequencer interrupted")?;if *sequence!=0{return Err("Ordered restore cannot replace active publisher".into());}self.current.import_checkpoint(&f[7])?;*sequence=next;Ok(())
    }
}
