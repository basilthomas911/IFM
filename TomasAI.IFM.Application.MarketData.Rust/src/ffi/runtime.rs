//! Integer-handle runtime ABI. Never dereference a caller-supplied handle as a
//! pointer. Library lifetime is process-wide; explicit release is allowed only
//! after cancellation, completion delivery and all concurrent calls have ended.

use super::ABI_VERSION;
use crate::runtime::owner::{
    Completion, OPERATION_CAPACITY, Outcome, Owner, OwnerError, foreign_deadline,
};
use std::{
    collections::BTreeMap,
    panic::{AssertUnwindSafe, catch_unwind},
    sync::{Arc, Mutex, OnceLock},
};
use tokio::runtime::{Builder, Runtime};

/// ABI return code, separate from asynchronous operation outcomes.
#[repr(i32)]
#[derive(Debug, Copy, Clone, PartialEq, Eq)]
pub enum Status {
    /// Call accepted or copied successfully.
    Ok = 0,
    /// Invalid scalar, pointer alignment, operation identity or buffer capacity.
    InvalidInput = 1,
    /// Runtime is stopping.
    Closed = 2,
    /// ABI version or DTO size differs from the library contract.
    AbiMismatch = 3,
    /// A hard runtime/operation capacity was reached.
    Capacity = 4,
    /// An internal error/panic was contained.
    InternalFailure = 5,
    /// The handle does not identify a live runtime, including after release.
    InvalidHandle = 6,
    /// Release cannot proceed while work, completions or concurrent callers exist.
    Busy = 7,
}

/// Caller-owned, fixed-width 32-byte completion; no native pointer is retained.
#[repr(C)]
#[derive(Debug, Copy, Clone, Default)]
pub struct NativeCompletion {
    /// Size in bytes, currently 32. Managed callers must validate this field.
    pub size: u32,
    /// Numeric [`Outcome`] classification.
    pub outcome: i32,
    /// Caller-assigned operation ID.
    pub operation: u64,
    /// Numerical result, meaningful only for [`Outcome::Success`].
    pub value: f64,
    /// Reserved for future compatible use; always zero.
    pub reserved: u64,
}

pub(super) struct Instance {
    runtime: Runtime,
    pub(super) pricing: Arc<Mutex<BTreeMap<u64, Option<super::pricing::NativePricingResult>>>>,
    pub(super) control: Arc<Mutex<BTreeMap<u64, Option<super::live::NativeControlResult>>>>,
    pub(super) live: Mutex<Option<super::live::Configured>>,
    pub(super) market_owner: Mutex<Option<Arc<crate::api::owner::MarketData>>>,
    pub(super) api_clock: Mutex<Option<Arc<crate::runtime::clock::Clock>>>,
    pub(super) catalog: Mutex<Option<Arc<Mutex<crate::catalog::repository::Repository>>>>,
    pub(super) history: Mutex<Option<Arc<crate::historical::History>>>,
    pub(super) fmp: Mutex<Option<Arc<crate::reference::fmp::Fmp>>>,
    pub(super) treasury: Mutex<Option<Arc<crate::reference::treasury_curve::Treasury>>>,
    pub(super) history_readers: Mutex<BTreeMap<u64, Arc<super::history_stream::Queue>>>,
    pub(super) reference_results: Arc<Mutex<BTreeMap<u64, Option<Vec<u8>>>>>,
    pub(super) catalog_queries: Arc<Mutex<crate::catalog::queries::Queries>>,
    pub(super) scopes: Arc<super::scopes::Scopes>,
    pub(super) dns_slots: Arc<tokio::sync::Semaphore>,
    pub(super) manifests: Arc<crate::option_chain::manifest::Budget>,
    pub(super) owner: Arc<Owner>,
}
#[derive(Default)]
struct Registry {
    next: u64,
    instances: BTreeMap<u64, Arc<Instance>>,
}
static REGISTRY: OnceLock<Mutex<Registry>> = OnceLock::new();
const MAX_RUNTIMES: usize = 4;

fn registry() -> &'static Mutex<Registry> {
    REGISTRY.get_or_init(|| Mutex::new(Registry::default()))
}
pub(super) fn instance(handle: u64) -> Result<Arc<Instance>, Status> {
    registry()
        .lock()
        .map_err(|_| Status::InternalFailure)?
        .instances
        .get(&handle)
        .cloned()
        .ok_or(Status::InvalidHandle)
}
pub(super) fn boundary(call: impl FnOnce() -> Result<(), Status>) -> i32 {
    match catch_unwind(AssertUnwindSafe(call)) {
        Ok(Ok(())) => Status::Ok as i32,
        Ok(Err(status)) => status as i32,
        Err(_) => Status::InternalFailure as i32,
    }
}
pub(super) fn valid<T>(pointer: *mut T) -> bool {
    !pointer.is_null() && (pointer as usize).is_multiple_of(std::mem::align_of::<T>())
}
impl From<OwnerError> for Status {
    fn from(value: OwnerError) -> Self {
        match value {
            OwnerError::Closed => Self::Closed,
            OwnerError::Capacity => Self::Capacity,
            OwnerError::InvalidOperation => Self::InvalidInput,
            OwnerError::InternalFailure => Self::InternalFailure,
        }
    }
}

/// Creates one bounded runtime with 1-4 Tokio workers. At most four independent
/// runtimes may exist; normal Host composition will create exactly one.
/// Failure preserves the caller output. No provider connection is started.
///
/// # Safety
/// `output` must be aligned, exclusive writable storage for one `u64` during the
/// call. ABI identity and completion size must match the compiled caller layout.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_runtime_create_v1(
    abi: u32,
    completion_size: u32,
    threads: u32,
    output: *mut u64,
) -> i32 {
    boundary(|| {
        if !valid(output) || !(1..=4).contains(&threads) {
            return Err(Status::InvalidInput);
        }
        if abi != ABI_VERSION || completion_size as usize != std::mem::size_of::<NativeCompletion>()
        {
            return Err(Status::AbiMismatch);
        }
        crate::runtime::panic_capture::install();
        let mut registry = registry().lock().map_err(|_| Status::InternalFailure)?;
        if registry.instances.len() == MAX_RUNTIMES {
            return Err(Status::Capacity);
        }
        let id = registry.next.checked_add(1).ok_or(Status::Capacity)?;
        let runtime = Builder::new_multi_thread()
            .worker_threads(threads as usize)
            .thread_name("ifm-marketdata")
            .enable_all()
            .build()
            .map_err(|_| Status::InternalFailure)?;
        registry.next = id;
        registry.instances.insert(
            id,
            Arc::new(Instance {
                runtime,
                dns_slots: Arc::new(tokio::sync::Semaphore::new(16)),
                manifests: crate::option_chain::manifest::Budget::new(16 * 1024 * 1024)
                    .map_err(|_| Status::InternalFailure)?,
                owner: Arc::new(Owner::default()),
                pricing: Arc::new(Mutex::new(BTreeMap::new())),
                control: Arc::new(Mutex::new(BTreeMap::new())),
                live: Mutex::new(None),
                market_owner: Mutex::new(None),
                api_clock: Mutex::new(None),
                catalog: Mutex::new(None),
                history: Mutex::new(None),
                fmp: Mutex::new(None),
                treasury: Mutex::new(None),
                history_readers: Mutex::new(BTreeMap::new()),
                reference_results: Arc::new(Mutex::new(BTreeMap::new())),
                catalog_queries: Arc::new(Mutex::new(Default::default())),
                scopes: Arc::new(Default::default()),
            }),
        );
        // SAFETY: Non-null/alignment checked; caller guarantees writable exclusive
        // storage. The integer is an identity, never a native object pointer.
        unsafe {
            output.write(id);
        }
        Ok(())
    })
}

/// Submits the qualified Treasury numerical core asynchronously. Input/result
/// semantics match the synchronous export. This exercises real completion
/// delivery; it is not a declaration of the full reference-data API's readiness.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_submit_treasury_v1(
    handle: u64,
    operation: u64,
    half_yield: f64,
    budget_ms: u64,
) -> i32 {
    boundary(|| {
        let instance = instance(handle)?;
        let deadline = foreign_deadline(budget_ms);
        let controls = instance
            .control
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if controls.contains_key(&operation) {
            return Err(Status::InvalidInput);
        }
        let slots = instance
            .pricing
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if slots.contains_key(&operation) {
            return Err(Status::InvalidInput);
        }
        instance
            .owner
            .submit(instance.runtime.handle(), operation, deadline, async move {
                crate::reference::treasury::continuous_rate(half_yield)
                    .map_err(|_| Outcome::InvalidInput)
            })
            .map_err(Status::from)
    })
}

/// Requests cancellation without consuming completion or waiting for cleanup.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_runtime_cancel_v1(handle: u64, operation: u64) -> i32 {
    boundary(|| {
        instance(handle)?
            .owner
            .cancel(operation)
            .map_err(Status::from)
    })
}

/// Closes admission and cancels work; never waits behind a full completion queue.
/// This is idempotent while the handle remains live. Explicit release follows.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_runtime_stop_v1(handle: u64) -> i32 {
    boundary(|| {
        let instance = instance(handle)?;
        // Close root admission before detaching its control directory. Otherwise
        // a retained notification Child in MarketData would keep wait() busy
        // until release(), while release() itself requires wait() to finish.
        let native = match instance.market_owner.lock() {
            Ok(mut directory) => {
                // Holding the directory excludes concurrent owner creation while
                // interruption facts are captured and root admission closes.
                if let Some(native) = directory.as_ref() {
                    native.interrupt_audit("Native runtime shutdown requested");
                }
                instance.owner.stop().map_err(Status::from)?;
                directory.take()
            }
            Err(_) => {
                let _ = instance.owner.stop();
                return Err(Status::InternalFailure);
            }
        };
        if let Some(native) = native {
            native.cancel();
        }
        // Native tasks retain their tracked guards until actual socket/task exit;
        // dropping the command directory is not substituted for that proof.
        Ok(())
    })
}

/// Waits on a native condition variable, for one dedicated managed delivery
/// thread. No timed polling. `available=0` means stopped and fully drained.
/// Never call this blocking operation on a Tokio, UI or actor thread.
///
/// # Safety
/// `available` must be aligned, exclusive writable storage for one `u32` for the
/// entire wait. The DLL must remain loaded and the handle live during the call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_runtime_wait_v1(handle: u64, available: *mut u32) -> i32 {
    boundary(|| {
        if !valid(available) {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let value = instance.owner.wait().map_err(Status::from)?;
        // SAFETY: Alignment checked; caller owns writable storage throughout wait.
        unsafe {
            available.write(u32::from(value));
        }
        Ok(())
    })
}

/// Copies an available completion batch into caller-owned memory and releases
/// those reservations. Invalid arguments consume nothing and preserve output.
///
/// # Safety
/// `output` must hold `capacity` aligned writable `NativeCompletion` records;
/// `count` one aligned writable `u32`. Their storage must not overlap or be
/// accessed concurrently. A zero capacity permits a null output pointer.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_runtime_drain_v1(
    handle: u64,
    output: *mut NativeCompletion,
    capacity: u32,
    count: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(count)
            || capacity as usize > OPERATION_CAPACITY
            || (capacity > 0 && !valid(output))
        {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let mut buffer = [Completion {
            operation: 0,
            outcome: Outcome::InternalFailure,
            value: 0.0,
        }; OPERATION_CAPACITY];
        let copied = instance
            .owner
            .drain(&mut buffer[..capacity as usize])
            .map_err(Status::from)?;
        for (index, completion) in buffer[..copied].iter().enumerate() {
            // SAFETY: Caller guarantees capacity, exclusive aligned output and
            // non-overlap with count; copied is bounded by validated capacity.
            unsafe {
                output.add(index).write(NativeCompletion {
                    size: std::mem::size_of::<NativeCompletion>() as u32,
                    outcome: completion.outcome as i32,
                    operation: completion.operation,
                    value: completion.value,
                    reserved: 0,
                });
            }
        }
        // SAFETY: Non-null/alignment checked and caller guarantees exclusivity.
        unsafe {
            count.write(copied as u32);
        }
        Ok(())
    })
}

/// Releases an explicitly stopped, completely drained runtime. Returns Busy
/// without removing its handle if any work, waiter or concurrent call remains.
/// Stale identities never become valid again. Must be called outside Tokio.
/// The managed finalizer must only request stop; normal async disposal drains
/// completion and joins delivery before requesting this explicit release.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_runtime_release_v1(handle: u64) -> i32 {
    boundary(|| {
        if tokio::runtime::Handle::try_current().is_ok() {
            return Err(Status::Busy);
        }
        let removed = {
            let mut registry = registry().lock().map_err(|_| Status::InternalFailure)?;
            let instance = registry
                .instances
                .get(&handle)
                .ok_or(Status::InvalidHandle)?;
            if !instance
                .history_readers
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .is_empty()
                || Arc::strong_count(instance) != 1
                || !instance.owner.disposable().map_err(Status::from)?
                || !instance
                    .pricing
                    .lock()
                    .map_err(|_| Status::InternalFailure)?
                    .is_empty()
                || !instance
                    .control
                    .lock()
                    .map_err(|_| Status::InternalFailure)?
                    .is_empty()
                || !instance
                    .reference_results
                    .lock()
                    .map_err(|_| Status::InternalFailure)?
                    .is_empty()
            {
                return Err(Status::Busy);
            }
            registry.instances.remove(&handle)
        };
        // Drop outside the registry lock. Only qualified owned async work runs
        // here; there is no spawn_blocking pool job or provider thread to abandon.
        drop(removed);
        Ok(())
    })
}

impl Instance {
    /// Enters only for bounded native control construction. No caller's thread
    /// runs a network wait or blocks on an asynchronous dataset operation.
    pub(super) fn enter<R>(&self, call: impl FnOnce() -> R) -> R {
        let _entered = self.runtime.enter();
        call()
    }
    /// Reserves the shared control result slot before admitting async work.
    /// Lock order matches live/pricing admission; no two operation kinds can
    /// overwrite each other's retained result identity.
    pub(super) fn reference_clock(&self) -> Result<Arc<crate::runtime::clock::Clock>, Status> {
        self.api_clock
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)
    }
    pub(super) fn reserve_control(
        &self,
        operation: u64,
    ) -> Result<super::live::Reservation, Status> {
        let mut slots = self.control.lock().map_err(|_| Status::InternalFailure)?;
        let pricing = self.pricing.lock().map_err(|_| Status::InternalFailure)?;
        if operation == 0 || slots.contains_key(&operation) || pricing.contains_key(&operation) {
            return Err(Status::InvalidInput);
        }
        if slots.len() >= OPERATION_CAPACITY {
            return Err(Status::Capacity);
        }
        slots.insert(operation, None);
        Ok(super::live::Reservation {
            slots: self.control.clone(),
            operation,
            result: Default::default(),
        })
    }
    pub(super) fn submit_observer<F>(&self, operation: u64, work: F) -> Result<(), Status>
    where
        F: std::future::Future<Output = Result<f64, Outcome>> + Send + 'static,
    {
        self.owner
            .submit_observer(self.runtime.handle(), operation, work)
            .map_err(Status::from)
    }
    pub(super) fn submit<F>(
        &self,
        operation: u64,
        deadline: crate::runtime::Deadline,
        work: F,
    ) -> Result<(), Status>
    where
        F: std::future::Future<Output = Result<f64, Outcome>> + Send + 'static,
    {
        self.owner
            .submit(self.runtime.handle(), operation, deadline, work)
            .map_err(Status::from)
    }
}
