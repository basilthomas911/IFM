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
}
impl Ordered {
    /// Validates the logical envelope before any submission. The port's owner
    /// must be dedicated to this stream; control/diagnostic requests use other IO.
    ///
    /// # Errors
    /// Invalid logical identity or retained snapshot bounds fail construction.
    pub fn new(port: Port, header: Header) -> Result<Arc<Self>, &'static str> {
        Frame {
            lane: header.lane,
            value_date: header.value_date,
            worker: header.worker,
            generation: header.generation,
            manifest_revision: header.revision,
            sequence: 1,
            kind: 3,
            payload: vec![0xc0],
        }
        .content()?;
        Ok(Arc::new(Self {
            port,
            header,
            sequence: Mutex::new(0),
            current: Arc::new(Current::new(4096, 32 * 1024 * 1024)?),
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
    /// Event-driven protocol progress, with no polling receiver.
    pub fn changes(&self) -> watch::Receiver<Status> {
        self.port.changes()
    }
    /// Detached latest-value query storage; retaining it cannot pin the writer.
    pub fn current(&self) -> Arc<Current> {
        self.current.clone()
    }
}
