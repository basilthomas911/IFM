//! Bounded asynchronous historical record transfer. A full output channel
//! backpressures the owned HTTP decoder; cancellation drops both before completion.
use super::{
    history_control,
    runtime::{Status, boundary, instance, valid},
};
use crate::{
    historical::record::{Record, normalize},
    runtime::{
        Deadline,
        owner::{Outcome, Owner},
    },
};
use std::{
    sync::{Arc, Mutex},
    time::Duration,
};
use tokio::sync::mpsc;
use tokio_util::sync::CancellationToken;
pub mod definitions;
pub mod download;
pub mod file;
pub mod replay;
struct ByteOutput {
    receiver: mpsc::Receiver<bytes::Bytes>,
    front: Option<bytes::Bytes>,
}
pub(super) struct Queue {
    receiver: Option<Mutex<mpsc::Receiver<Record>>>,
    bytes: Option<Mutex<ByteOutput>>,
    owner: Arc<Owner>,
    input: Mutex<Option<mpsc::Sender<bytes::Bytes>>>,
    is_file: bool,
}
struct Signal(Arc<Queue>);
impl Drop for Signal {
    fn drop(&mut self) {
        let _ = self.0.owner.signal(1 << 5);
    }
}
/// Opens a direct provider range. Query JSON uses the same explicit dataset,
/// symbols, schema/symbology, UTC nanosecond bounds and limit as historical control.
/// At most four readers / 4096 copied rows each are retained per native runtime.
/// Standard operation completion reports success/failure after the decoder ends;
/// the reader slot remains until explicit close. No full-range Vec is constructed.
/// # Safety
/// Input is readable for length bytes (1..256 KiB), copied before returning.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_range_open_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
    capacity: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = Deadline::operation(Duration::from_millis(budget_ms));
    boundary(|| {
        if input.is_null() || length == 0 || length > 262_144 || capacity == 0 || capacity > 4096 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller supplies a bounded readable input; all query data is copied.
        let v: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let query = history_control::request(&v)?;
        let schema = v["schema"].as_u64().ok_or(Status::InvalidInput)? as u32;
        let symbol = v["symbols"][0]
            .as_str()
            .ok_or(Status::InvalidInput)?
            .to_owned();
        let instance = instance(handle)?;
        let history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let mut readers = instance
            .history_readers
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if readers.len() >= 4 || readers.contains_key(&operation) {
            return Err(Status::Capacity);
        }
        let mut reservation = instance.reserve_control(operation)?;
        let (sender, receiver) = mpsc::channel(capacity as usize);
        let queue = Arc::new(Queue {
            receiver: Some(Mutex::new(receiver)),
            bytes: None,
            owner: instance.owner.clone(),
            input: Mutex::new(None),
            is_file: false,
        });
        readers.insert(operation, queue.clone());
        drop(readers);
        let signal = Signal(queue.clone());
        let work = async move {
            let token = CancellationToken::new();
            let mut ordinal = 0;
            let params = databento::historical::timeseries::GetRangeParams::builder()
                .dataset(query.dataset)
                .symbols(query.symbols)
                .schema(query.schema)
                .stype_in(query.stype_in)
                .stype_out(databento::dbn::SType::InstrumentId)
                .date_time_range(query.date_time_range)
                .maybe_limit(query.limit)
                .build();
            let result = history
                .for_each_async(params, &token, deadline, move |source| {
                    let record = normalize(source, schema, &symbol, ordinal);
                    if record.is_some() {
                        ordinal += 1;
                    }
                    let sender = sender.clone();
                    let queue = queue.clone();
                    async move {
                        if let Some(record) = record {
                            sender
                                .send(record)
                                .await
                                .map_err(|_| "Historical reader closed".to_owned())?;
                            queue
                                .owner
                                .signal(1 << 5)
                                .map_err(|_| "Historical delivery owner interrupted".to_owned())?;
                        }
                        Ok(())
                    }
                })
                .await;
            reservation.result = super::live::NativeControlResult::finished(
                result
                    .map(|_| ())
                    .map_err(|e| format!("Historical stream failed: {e:?}")),
            );
            drop(reservation);
            drop(signal);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok(0.0)
            }
        };
        if let Err(error) = instance.submit(operation, deadline, work) {
            instance
                .history_readers
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(error);
        }
        Ok(())
    })
}
/// Copies up to capacity rows and releases channel space for the producer.
/// `ended` is one only after all queued rows are copied and the producer dropped
/// its sender. The caller still observes operation completion before declaring EOF.
/// Empty reads never poll the network; signal bit 5 announces records/completion.
/// # Safety
/// Output is aligned writable for capacity records; count and ended are aligned
/// exclusive writable scalars disjoint from it. No foreign pointer is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_records_v1(
    handle: u64,
    operation: u64,
    output: *mut Record,
    capacity: u32,
    count: *mut u32,
    ended: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(output) || !valid(count) || !valid(ended) || capacity == 0 || capacity > 4096 {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let queue = instance
            .history_readers
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .get(&operation)
            .cloned()
            .ok_or(Status::InvalidInput)?;
        let mut receiver = queue
            .receiver
            .as_ref()
            .ok_or(Status::InvalidInput)?
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        let mut copied = 0;
        while copied < capacity {
            match receiver.try_recv() {
                Ok(row) => {
                    // SAFETY: Caller supplied exclusive aligned capacity; copied is bounded.
                    unsafe {
                        output.add(copied as usize).write(row);
                    }
                    copied += 1;
                }
                Err(mpsc::error::TryRecvError::Empty | mpsc::error::TryRecvError::Disconnected) => {
                    break;
                }
            }
        }
        let done = receiver.is_closed() && receiver.is_empty();
        // SAFETY: Caller supplies disjoint aligned exclusive scalar destinations.
        unsafe {
            count.write(copied);
            ended.write(u32::from(done));
        }
        Ok(())
    })
}
/// Idempotently cancels a reader operation and releases its output slot. Task
/// completion/join remains owned by the standard runtime lifecycle; closing an
/// output slot never claims a blocked task was physically terminated.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_history_reader_close_v1(handle: u64, operation: u64) -> i32 {
    boundary(|| {
        let instance = instance(handle)?;
        let _ = instance.owner.cancel(operation);
        instance
            .history_readers
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .remove(&operation);
        Ok(())
    })
}
