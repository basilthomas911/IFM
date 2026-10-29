//! Fixed generation-owned quote buffers. Leases return storage through RAII after
//! publication finishes; pressure waits on a semaphore and remains cancellable.
//! No buffer allocation occurs on successful rent/return after pool construction.
use crate::runtime::{Deadline, Interrupted};
use std::sync::{Arc, Mutex};
use tokio::sync::{OwnedSemaphorePermit, Semaphore};
use tokio_util::sync::CancellationToken;
/// Explicit resource outcomes, independent of provider error codes.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Outside current capacity/slot/512-MiB policy, or zero-size payload.
    InvalidInput,
    /// All fixed slots are currently retained by publishers.
    Saturated,
    /// Generation retired; old waiters cannot rent new buffers.
    Retired,
    /// A previous writer panic interrupted a short pool mutation.
    InterruptedWriter,
    /// Original deadline or caller cancellation.
    Interrupted(Interrupted),
    /// Fallible initial buffer reservation failed.
    Allocation,
}
struct State<T> {
    retired: bool,
    free: Vec<Vec<T>>,
    returned: tokio::sync::mpsc::Receiver<Vec<T>>,
}
struct Owner<T> {
    state: Mutex<State<T>>,
    available: Arc<Semaphore>,
    capacity: usize,
    slots: usize,
    returns: tokio::sync::mpsc::Sender<Vec<T>>,
}
/// Shared fixed-size pool for one lane generation. Payloads must be Copy so
/// clearing returned storage cannot execute user destructors on an ingress path.
pub struct Pool<T: Copy> {
    owner: Arc<Owner<T>>,
}
impl<T: Copy> Clone for Pool<T> {
    fn clone(&self) -> Self {
        Self {
            owner: self.owner.clone(),
        }
    }
}
impl<T: Copy> Pool<T> {
    /// Preallocates current 1..4096 item buffers and 1..65536 slots, bounded by
    /// the existing 512-MiB maximum. Failure starts no provider session.
    pub fn new(capacity: usize, slots: usize) -> Result<Self, Failure> {
        let bytes = capacity
            .checked_mul(slots)
            .and_then(|n| n.checked_mul(std::mem::size_of::<T>()));
        if !(1..=4096).contains(&capacity)
            || !(1..=65536).contains(&slots)
            || std::mem::size_of::<T>() == 0
            || bytes.is_none_or(|n| n > 512 * 1024 * 1024)
        {
            return Err(Failure::InvalidInput);
        }
        let mut free = Vec::new();
        free.try_reserve_exact(slots)
            .map_err(|_| Failure::Allocation)?;
        for _ in 0..slots {
            let mut buffer = Vec::new();
            buffer
                .try_reserve_exact(capacity)
                .map_err(|_| Failure::Allocation)?;
            free.push(buffer);
        }
        let (returns, returned) = tokio::sync::mpsc::channel(slots);
        Ok(Self {
            owner: Arc::new(Owner {
                state: Mutex::new(State {
                    returned,
                    retired: false,
                    free,
                }),
                available: Arc::new(Semaphore::new(slots)),
                capacity,
                slots,
                returns,
            }),
        })
    }
    /// Attempts immediate rent. Saturation is distinct from retirement and does
    /// not close any queue/actor or discard an authoritative input contribution.
    pub fn try_rent(&self) -> Result<Lease<T>, Failure> {
        let permit = self
            .owner
            .available
            .clone()
            .try_acquire_owned()
            .map_err(|e| match e {
                tokio::sync::TryAcquireError::Closed => Failure::Retired,
                tokio::sync::TryAcquireError::NoPermits => Failure::Saturated,
            })?;
        self.take(permit)
    }
    /// Waits only for capacity under the caller's original deadline. A cancellation
    /// or reset is independent of buffer return and cannot deadlock behind output.
    pub async fn rent(
        &self,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Lease<T>, Failure> {
        let permit = deadline
            .run(token, self.owner.available.clone().acquire_owned())
            .await
            .map_err(Failure::Interrupted)?
            .map_err(|_| Failure::Retired)?;
        self.take(permit)
    }
    fn take(&self, permit: OwnedSemaphorePermit) -> Result<Lease<T>, Failure> {
        let mut state = self
            .owner
            .state
            .lock()
            .map_err(|_| Failure::InterruptedWriter)?;
        if state.retired {
            return Err(Failure::Retired);
        }
        let buffer = state
            .free
            .pop()
            .or_else(|| state.returned.try_recv().ok())
            .ok_or(Failure::InterruptedWriter)?;
        Ok(Lease {
            owner: self.owner.clone(),
            buffer: Some(buffer),
            _permit: permit,
        })
    }
    /// Retires only this generation, waking capacity waiters immediately. Existing
    /// leases keep valid storage until released; no borrowed memory is freed early.
    pub fn retire(&self) -> Result<(), Failure> {
        // Capacity waiters wake before any copied-state cleanup lock is acquired.
        self.owner.available.close();
        let mut state = self
            .owner
            .state
            .lock()
            .map_err(|_| Failure::InterruptedWriter)?;
        state.retired = true;
        state.returned.close();
        while state.returned.try_recv().is_ok() {}
        state.free.clear();
        Ok(())
    }
    /// Number of preallocated slots still retained by in-flight operations.
    pub fn outstanding(&self) -> Result<usize, Failure> {
        let state = self
            .owner
            .state
            .lock()
            .map_err(|_| Failure::InterruptedWriter)?;
        if state.retired {
            Ok(self.owner.slots - self.owner.available.available_permits())
        } else {
            Ok(self.owner.slots - state.free.len() - state.returned.len())
        }
    }
}
/// Exclusive owned buffer. Publishing may move this lease across tasks; drop
/// returns it only to its original generation and never to a replacement pool.
pub struct Lease<T: Copy> {
    owner: Arc<Owner<T>>,
    buffer: Option<Vec<T>>,
    _permit: OwnedSemaphorePermit,
}
impl<T: Copy> Lease<T> {
    /// Appends without growing storage. An overfull batch remains unchanged.
    pub fn push(&mut self, value: T) -> Result<(), Failure> {
        let buffer = self.buffer.as_mut().expect("live buffer lease");
        if buffer.len() == self.owner.capacity {
            return Err(Failure::Saturated);
        }
        buffer.push(value);
        Ok(())
    }
    /// Immutable initialized items only; spare capacity never crosses FFI.
    pub fn items(&self) -> &[T] {
        self.buffer.as_ref().expect("live buffer lease")
    }
    /// Configured exact logical capacity, independent of allocator size classes.
    pub fn capacity(&self) -> usize {
        self.owner.capacity
    }
}
impl<T: Copy> Drop for Lease<T> {
    fn drop(&mut self) {
        if let Some(mut buffer) = self.buffer.take() {
            buffer.clear();
            // Each lease owns one of exactly `slots` buffers. A bounded return
            // channel therefore has room, without a destructor waiting on rent.
            // Closed means the generation retired and this storage is discarded.
            let _ = self.owner.returns.try_send(buffer);
        }
    }
}
#[cfg(test)]
mod cancellation_tests {
    use super::*;
    #[test]
    fn buffer_return_does_not_wait_for_a_pool_reader() {
        let pool = Pool::<u64>::new(4, 1).unwrap();
        let lease = pool.try_rent().unwrap();
        let reader = pool.owner.state.lock().unwrap();
        drop(lease);
        assert_eq!(reader.returned.len(), 1);
        drop(reader);
        assert!(pool.try_rent().is_ok());
    }
}
