//! Bounded operation ownership and signalled completion delivery.
//!
//! A reservation remains occupied until its completion is drained. Thus slow
//! consumers cause explicit backpressure at submission, never lost completions.
//! Shutdown has a separate cancellation path and cannot queue behind data work.

use super::{Deadline, Interrupted};
use futures_util::FutureExt;
use std::{
    collections::{BTreeMap, VecDeque},
    future::Future,
    panic::AssertUnwindSafe,
    sync::{Arc, Condvar, Mutex},
    time::Duration,
};
use tokio::runtime::Handle;
use tokio_util::sync::CancellationToken;
use tokio_util::task::TaskTracker;

/// Maximum admitted operations plus undrained completions per runtime.
pub const OPERATION_CAPACITY: usize = 256;

/// Stable completion status; no provider text or credentials cross this record.
#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum Outcome {
    /// Operation completed successfully.
    Success = 0,
    /// Caller or runtime cancellation was observed and resources dropped.
    Cancelled = 1,
    /// The original monotonic deadline expired.
    DeadlineExpired = 2,
    /// Input failed domain validation.
    InvalidInput = 3,
    /// A Rust panic was contained at the task boundary.
    InternalFailure = 4,
}

/// Fixed-width completion delivered exactly once after the owned future drops.
#[derive(Debug, Copy, Clone)]
pub struct Completion {
    /// Caller-assigned nonzero operation ID, unique while reserved.
    pub operation: u64,
    /// Result classification.
    pub outcome: Outcome,
    /// Operation result, meaningful only on success.
    pub value: f64,
}

/// Admission/delivery failures, separate from an admitted operation's result.
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum OwnerError {
    /// The runtime is stopping; no new operation may start.
    Closed,
    /// All operation/completion reservations are occupied.
    Capacity,
    /// Zero ID or an ID still retained by an operation/completion.
    InvalidOperation,
    /// A synchronous state invariant was poisoned by an internal panic.
    InternalFailure,
}

struct State {
    notifications: u32,
    stopping: bool,
    operations: BTreeMap<u64, CancellationToken>,
    completed: VecDeque<Completion>,
    waiters: usize,
    children: usize,
    notification_children: usize,
    services: usize,
}

/// Shared task state, independent from the Tokio runtime object itself.
///
/// Tasks retain only this object, never the owning `Runtime`. The last runtime
/// reference therefore cannot be dropped from one of its own worker tasks.
pub struct Owner {
    state: Mutex<State>,
    changed: Condvar,
    tasks: TaskTracker,
    stopping: CancellationToken,
}
struct ServiceTask {
    owner: Arc<Owner>,
    _tracking: tokio_util::task::task_tracker::TaskTrackerToken,
}
impl Drop for ServiceTask {
    fn drop(&mut self) {
        if let Ok(mut state) = self.owner.state.lock() {
            state.services -= 1;
            self.owner.changed.notify_all();
        }
    }
}

impl Default for Owner {
    fn default() -> Self {
        Self {
            state: Mutex::new(State {
                notifications: 0,
                stopping: false,
                operations: BTreeMap::new(),
                completed: VecDeque::with_capacity(OPERATION_CAPACITY),
                waiters: 0,
                children: 0,
                notification_children: 0,
                services: 0,
            }),
            changed: Condvar::new(),
            tasks: TaskTracker::new(),
            stopping: CancellationToken::new(),
        }
    }
}

impl Owner {
    /// Starts a bounded native control supervisor outside replaceable dataset
    /// children. A role supervisor can therefore cancel/join/rebuild its child
    /// without waiting for itself. No managed completion reservation is consumed.
    /// The work must observe the token and explicitly join any owned resources.
    ///
    /// # Errors
    /// Rejects stopped roots, missing Tokio runtime or more than four control services.
    pub fn spawn_service<F, Fut>(
        self: &Arc<Self>,
        work: F,
    ) -> Result<tokio::task::JoinHandle<()>, OwnerError>
    where
        F: FnOnce(CancellationToken) -> Fut + Send + 'static,
        Fut: Future<Output = ()> + Send + 'static,
    {
        let runtime = Handle::try_current().map_err(|_| OwnerError::InvalidOperation)?;
        let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        if state.stopping {
            return Err(OwnerError::Closed);
        }
        if state.services == 4 {
            return Err(OwnerError::Capacity);
        }
        state.services += 1;
        let service = ServiceTask {
            owner: self.clone(),
            _tracking: self.tasks.token(),
        };
        let stop = self.stopping.child_token();
        drop(state);
        Ok(runtime.spawn(async move {
            let guard = service;
            {
                work(stop).await;
            }
            drop(guard);
        }))
    }
    /// Coalesces wake-up bits only. Authoritative records and diagnostics retain
    /// separate bounded storage; a wake-up is never the data payload itself.
    pub fn signal(&self, bits: u32) -> Result<(), OwnerError> {
        let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        state.notifications |= bits;
        self.changed.notify_all();
        Ok(())
    }
    /// Atomically consumes accumulated notification bits for the delivery thread.
    pub fn take_signals(&self) -> Result<u32, OwnerError> {
        let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        Ok(std::mem::take(&mut state.notifications))
    }
    /// Registers one of at most three long-lived dataset owners before spawning.
    /// Runtime shutdown is not deliverably complete until every child guard has
    /// dropped after its session resources. Registration and stop are serialized.
    pub fn child(self: &Arc<Self>) -> Result<Child, OwnerError> {
        self.child_kind(false)
    }
    /// Reserves the single persistent terminal-notification I/O subtree. Its
    /// independent slot survives retirement of all three physical datasets and
    /// cannot consume or expand their separate three-slot resource limit.
    ///
    /// # Errors
    /// A stopped root, duplicate notification owner or interrupted state.
    pub fn notification_child(self: &Arc<Self>) -> Result<Child, OwnerError> {
        self.child_kind(true)
    }
    fn child_kind(self: &Arc<Self>, notification: bool) -> Result<Child, OwnerError> {
        let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        if state.stopping {
            return Err(OwnerError::Closed);
        }
        if (notification && state.notification_children == 1)
            || (!notification && state.children == 3)
        {
            return Err(OwnerError::Capacity);
        }
        if notification {
            state.notification_children += 1;
        } else {
            state.children += 1;
        }
        Ok(Child {
            state: Arc::new(ChildState {
                owner: self.clone(),
                notification,
                cancellation: self.stopping.child_token(),
                _tracking: self.tasks.token(),
                tasks: std::sync::atomic::AtomicUsize::new(0),
                changed: tokio::sync::Notify::new(),
                provider: tokio::sync::watch::channel(None).0,
            }),
        })
    }
    /// Reserves bounded completion capacity before scheduling owned async work.
    /// The future must own all cancellable resources and must not spawn detached
    /// jobs. A caught panic reports failure after unwinding/dropping those resources.
    pub fn submit<F>(
        self: &Arc<Self>,
        runtime: &Handle,
        operation: u64,
        deadline: Deadline,
        work: F,
    ) -> Result<(), OwnerError>
    where
        F: Future<Output = Result<f64, Outcome>> + Send + 'static,
    {
        self.submit_owned(runtime, operation, Some(deadline), work)
    }
    /// Reserves a bounded completion for a native-health observer. The observed
    /// dataset supplies its own authoritative deadline; this adds no competing
    /// timer. It must not perform provider IO or own physical dataset resources.
    /// Root/caller cancellation still drops the future before completion.
    ///
    /// # Errors
    /// Closed admission, duplicate identity or exhausted completion capacity.
    pub fn submit_observer<F>(
        self: &Arc<Self>,
        runtime: &Handle,
        operation: u64,
        work: F,
    ) -> Result<(), OwnerError>
    where
        F: Future<Output = Result<f64, Outcome>> + Send + 'static,
    {
        self.submit_owned(runtime, operation, None, work)
    }
    fn submit_owned<F>(
        self: &Arc<Self>,
        runtime: &Handle,
        operation: u64,
        deadline: Option<Deadline>,
        work: F,
    ) -> Result<(), OwnerError>
    where
        F: Future<Output = Result<f64, Outcome>> + Send + 'static,
    {
        let token = CancellationToken::new();
        let tracking = {
            let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
            if state.stopping {
                return Err(OwnerError::Closed);
            }
            if operation == 0 || state.operations.contains_key(&operation) {
                return Err(OwnerError::InvalidOperation);
            }
            if state.operations.len() == OPERATION_CAPACITY {
                return Err(OwnerError::Capacity);
            }
            state.operations.insert(operation, token.clone());
            // Register under the same lock as admission. Stop cannot close an
            // empty tracker between reserving an operation and spawning it.
            self.tasks.token()
        };
        let owner = self.clone();
        // Track every task separately from output delivery. A completion proves
        // work-owned resource drop; wait_idle also awaits task-wrapper exit.
        runtime.spawn(async move {
            let _tracking = tracking;
            let observed=async {
                if let Some(deadline)=deadline {deadline.run(&token,work).await}
                else {tokio::select!{biased;()=token.cancelled()=>Err(Interrupted::Cancelled),result=work=>Ok(result)}}
            };
            let result=AssertUnwindSafe(observed).catch_unwind().await;
            let (outcome, value) = match result {
                Ok(Ok(Ok(value))) => (Outcome::Success, value),
                Ok(Ok(Err(reason))) => (reason, 0.0),
                Ok(Err(Interrupted::Cancelled)) => (Outcome::Cancelled, 0.0),
                Ok(Err(Interrupted::DeadlineExpired)) => (Outcome::DeadlineExpired, 0.0),
                Err(_) => (Outcome::InternalFailure, 0.0),
            };
            // All work-owned resources have dropped before this point. Keep the
            // reservation until drain: a full consumer queue cannot lose a result.
            if let Ok(mut state) = owner.state.lock() {
                state.completed.push_back(Completion {
                    operation,
                    outcome,
                    value,
                });
                owner.changed.notify_all();
            }
        });
        Ok(())
    }

    /// Signals a single pending operation. Its terminal result remains observable.
    pub fn cancel(&self, operation: u64) -> Result<(), OwnerError> {
        let state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        state
            .operations
            .get(&operation)
            .ok_or(OwnerError::InvalidOperation)?
            .cancel();
        Ok(())
    }

    /// Stops admission and cancels all work without waiting for queue capacity.
    /// Existing completions are retained and blocking delivery waiters are woken.
    pub fn stop(&self) -> Result<(), OwnerError> {
        // Root cancellation is independent of the operation/result queue and
        // still reaches provider tasks if another state invariant failed.
        self.stopping.cancel();
        let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        state.stopping = true;
        self.tasks.close();
        for token in state.operations.values() {
            token.cancel();
        }
        self.changed.notify_all();
        Ok(())
    }

    /// Copies and releases at most the caller's batch capacity. No native buffer
    /// ownership crosses this boundary; an empty slice consumes no completion.
    pub fn drain(&self, output: &mut [Completion]) -> Result<usize, OwnerError> {
        let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        let mut count = 0;
        for destination in output {
            let Some(event) = state.completed.pop_front() else {
                break;
            };
            state.operations.remove(&event.operation);
            *destination = event;
            count += 1;
        }
        self.changed.notify_all();
        Ok(count)
    }

    /// Event-driven blocking wait for the managed delivery thread only. It is
    /// never called on Tokio, actor or UI threads. Returns true when a batch is
    /// available, false after stop once all work has completed and been drained.
    /// Stop always wakes the wait, but pending task cancellation may still need
    /// one final completion notification. There is no timer polling.
    pub fn wait(&self) -> Result<bool, OwnerError> {
        let mut state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        state.waiters += 1;
        while state.completed.is_empty()
            && state.notifications == 0
            && !(state.stopping
                && state.operations.is_empty()
                && state.children == 0
                && state.notification_children == 0
                && state.services == 0)
        {
            state = self
                .changed
                .wait(state)
                .map_err(|_| OwnerError::InternalFailure)?;
        }
        state.waiters -= 1;
        Ok(!state.completed.is_empty() || state.notifications != 0)
    }

    /// Whether explicit destruction can proceed without discarding results or
    /// abandoning running work. Concurrent FFI callers are checked separately.
    pub fn disposable(&self) -> Result<bool, OwnerError> {
        let state = self.state.lock().map_err(|_| OwnerError::InternalFailure)?;
        Ok(state.stopping
            && state.operations.is_empty()
            && state.children == 0
            && state.notification_children == 0
            && state.services == 0
            && state.waiters == 0)
    }

    /// Awaits all tracked task exits after stop, under the caller's existing
    /// budget. Completed-but-undrained results remain available for delivery.
    /// Before stop this waits for closure, rather than asserting an idle runtime
    /// can never admit more work.
    pub async fn wait_idle(
        &self,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), Interrupted> {
        deadline.run(token, self.tasks.wait()).await
    }

    /// Counts admitted work plus retained results, for bounded-resource evidence.
    pub fn reserved(&self) -> Result<usize, OwnerError> {
        self.state
            .lock()
            .map(|state| state.operations.len())
            .map_err(|_| OwnerError::InternalFailure)
    }
}

/// Copied connection observation, independent of financial quote usability.
pub type ProviderState = Option<Result<(), String>>;
/// Event-driven connection-state receiver; it retains no socket or provider task.
pub type ProviderUpdates = tokio::sync::watch::Receiver<ProviderState>;
/// Owned dataset task registration. Move this guard into the task before spawn;
/// dropping it cancels its child lifetime and signals delivery.
pub struct Child {
    state: Arc<ChildState>,
}
struct ChildState {
    owner: Arc<Owner>,
    notification: bool,
    cancellation: CancellationToken,
    _tracking: tokio_util::task::task_tracker::TaskTrackerToken,
    tasks: std::sync::atomic::AtomicUsize,
    changed: tokio::sync::Notify,
    provider: tokio::sync::watch::Sender<ProviderState>,
}
struct ChildTask {
    state: Arc<ChildState>,
}
impl Drop for ChildTask {
    fn drop(&mut self) {
        let previous = self
            .state
            .tasks
            .fetch_sub(1, std::sync::atomic::Ordering::AcqRel);
        if previous == 1 {
            self.state.changed.notify_waiters();
        }
    }
}
impl Child {
    /// Copied provider-connection state only; it certifies no quote or price.
    /// The SDK read deadline detects silence even when no consumer is polling.
    pub fn provider_state(&self) -> ProviderUpdates {
        self.state.provider.subscribe()
    }
    pub(crate) fn provider_reporter(&self) -> tokio::sync::watch::Sender<ProviderState> {
        self.state.provider.clone()
    }
    #[cfg(test)]
    pub(crate) fn provider_received(&self) {
        self.state.provider.send_if_modified(|state| {
            if matches!(state, Some(Ok(()))) {
                false
            } else {
                *state = Some(Ok(()));
                true
            }
        });
    }
    /// Cancels the whole dataset subtree, aborts its main async task and proves
    /// tracked descendants have exited. No provider/broker graceful drain is used.
    /// Cancellation of this wait does not discard the retained task identity.
    pub async fn reset_task(
        &self,
        task: &mut Option<tokio::task::JoinHandle<()>>,
        deadline: Deadline,
    ) -> Result<(), String> {
        self.state.cancellation.cancel();
        if let Some(handle) = task.as_mut() {
            handle.abort();
            let _terminated = deadline
                .run(&CancellationToken::new(), handle)
                .await
                .map_err(|_| "Native reset main-task termination deadline expired")?;
            task.take();
            // Both cancellation and panic complete task unwinding. The native
            // panic sink retains diagnostics; reset still joins all descendants.
            // A dead task's error must not prevent replacing its failed dataset.
        }
        self.wait_nested(&CancellationToken::new(), deadline)
            .await
            .map_err(|_| "Native reset descendant termination deadline expired".into())
    }
    pub(crate) fn owner(&self) -> Arc<Owner> {
        self.state.owner.clone()
    }
    /// Independent lane token linked to runtime stop. Cancelling it does not
    /// cancel sibling futures/options lanes or their runtime root.
    pub fn cancellation(&self) -> CancellationToken {
        self.state.cancellation.clone()
    }
    /// Waits for all nested physical-session/pricing resources to drop under
    /// the caller's existing budget. Cancel this dataset before using the result
    /// as stable cleanup proof; otherwise it can still admit another sub-owner.
    pub async fn wait_nested(
        &self,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<(), Interrupted> {
        deadline.run(token, self.join_nested()).await
    }
    pub(crate) async fn join_nested(&self) {
        loop {
            let changed = self.state.changed.notified();
            if self.state.tasks.load(std::sync::atomic::Ordering::Acquire) == 0 {
                return;
            }
            changed.await;
        }
    }
    /// Spawns one bounded async sub-owner for a dataset's physical sessions or
    /// pricing tasks. It consumes no additional logical dataset slot. Parent or
    /// runtime cancellation drops the entire future; root cleanup remains
    /// incomplete until its resources have dropped, even if the join handle is
    /// abandoned. At most 256 sub-owners are retained per logical dataset.
    /// Work must not transfer provider resources outside this ownership tree.
    pub fn spawn<F>(&self, work: F) -> Result<tokio::task::JoinHandle<()>, OwnerError>
    where
        F: std::future::Future<Output = ()> + Send + 'static,
    {
        let runtime =
            tokio::runtime::Handle::try_current().map_err(|_| OwnerError::InvalidOperation)?;
        let root = self
            .state
            .owner
            .state
            .lock()
            .map_err(|_| OwnerError::InternalFailure)?;
        if root.stopping || self.state.cancellation.is_cancelled() {
            return Err(OwnerError::Closed);
        }
        self.state
            .tasks
            .fetch_update(
                std::sync::atomic::Ordering::AcqRel,
                std::sync::atomic::Ordering::Acquire,
                |count| if count < 256 { Some(count + 1) } else { None },
            )
            .map_err(|_| OwnerError::Capacity)?;
        let registration = ChildTask {
            state: self.state.clone(),
        };
        drop(root);
        Ok(runtime.spawn(async move {
            {
                // This block must end before the tracking guard is released:
                // cancellation alone is not proof that sockets/trees dropped.
                tokio::pin!(work);
                tokio::select! { biased;
                    () = registration.state.cancellation.cancelled() => {},
                    () = &mut work => {},
                }
            }
            drop(registration);
        }))
    }

    /// Tracks an async resource owner through its explicit cancellation cleanup.
    /// Unlike `spawn`, this does not drop the owner future on parent cancellation:
    /// the supplied token must unblock it, then it must join any socket workers
    /// before returning. This supports libraries whose Drop only signals abort.
    /// It uses the same 256-child bound and original external shutdown deadline.
    ///
    /// # Errors
    /// Returns closed/capacity/runtime admission failure before scheduling work.
    pub fn spawn_joined<F, Fut>(&self, work: F) -> Result<tokio::task::JoinHandle<()>, OwnerError>
    where
        F: FnOnce(CancellationToken) -> Fut + Send + 'static,
        Fut: Future<Output = ()> + Send + 'static,
    {
        let runtime = Handle::try_current().map_err(|_| OwnerError::InvalidOperation)?;
        let root = self
            .state
            .owner
            .state
            .lock()
            .map_err(|_| OwnerError::InternalFailure)?;
        if root.stopping || self.state.cancellation.is_cancelled() {
            return Err(OwnerError::Closed);
        }
        self.state
            .tasks
            .fetch_update(
                std::sync::atomic::Ordering::AcqRel,
                std::sync::atomic::Ordering::Acquire,
                |count| if count < 256 { Some(count + 1) } else { None },
            )
            .map_err(|_| OwnerError::Capacity)?;
        let registration = ChildTask {
            state: self.state.clone(),
        };
        drop(root);
        Ok(runtime.spawn(async move {
            work(registration.state.cancellation.clone()).await;
            drop(registration);
        }))
    }
}
impl Drop for Child {
    fn drop(&mut self) {
        self.state.cancellation.cancel();
    }
}
impl Drop for ChildState {
    fn drop(&mut self) {
        if let Ok(mut state) = self.owner.state.lock() {
            if self.notification {
                state.notification_children -= 1;
            } else {
                state.children -= 1;
            }
            self.owner.changed.notify_all();
        }
    }
}

/// Converts a foreign duration into the original capped budget without overflow.
pub fn foreign_deadline(milliseconds: u64) -> Deadline {
    Deadline::after(Duration::from_millis(milliseconds))
}
