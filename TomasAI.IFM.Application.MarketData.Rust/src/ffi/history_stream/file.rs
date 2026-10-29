//! Managed-storage-to-native-decoder byte transfer, bounded to four 64 KiB
//! input chunks. Provider/decode tasks never invoke a managed callback.
use super::*;
use crate::historical;
/// Opens a file decoder whose byte input is supplied with file_push_v1. JSON
/// contains a supported `schema` and caller-provided filename `fallback` symbol.
/// No filesystem path is opened in Rust. Slot/completion ownership matches ranges.
/// # Safety
/// Input is readable for length bytes (1..8192), borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_file_open_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
    capacity: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = Deadline::operation(Duration::from_millis(budget_ms));
    boundary(|| {
        if input.is_null() || length == 0 || length > 8192 || capacity == 0 || capacity > 4096 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller provides bounded readable query bytes; retained data is owned.
        let v: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let schema = v["schema"].as_u64().ok_or(Status::InvalidInput)?;
        history_control::schema(schema)?;
        let fallback = v["fallback"]
            .as_str()
            .filter(|s| s.len() <= 4096)
            .ok_or(Status::InvalidInput)?
            .to_owned();
        let instance = instance(handle)?;
        let mut readers = instance
            .history_readers
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if readers.len() >= 4 || readers.contains_key(&operation) {
            return Err(Status::Capacity);
        }
        let mut reservation = instance.reserve_control(operation)?;
        let (sender, receiver) = mpsc::channel(capacity as usize);
        let (input_sender, input_receiver) = mpsc::channel(4);
        let queue = Arc::new(Queue {
            receiver: Some(Mutex::new(receiver)),
            bytes: None,
            owner: instance.owner.clone(),
            input: Mutex::new(Some(input_sender)),
            is_file: true,
        });
        readers.insert(operation, queue.clone());
        drop(readers);
        let signal = Signal(queue.clone());
        let work = async move {
            let token = CancellationToken::new();
            let owner = queue.owner.clone();
            let stream = futures_util::stream::unfold(
                (input_receiver, owner),
                |(mut input, owner)| async move {
                    input.recv().await.map(|bytes| {
                        let result = owner
                            .signal(1 << 6)
                            .map(|()| bytes)
                            .map_err(|_| std::io::Error::other("File input owner interrupted"));
                        (result, (input, owner))
                    })
                },
            );
            let reader = tokio_util::io::StreamReader::new(Box::pin(stream));
            let mut ordinal = 0;
            let result = historical::file::for_each(
                reader,
                &token,
                deadline,
                move |metadata_symbol, record| {
                    let symbol = metadata_symbol.unwrap_or(fallback.as_str());
                    let record = normalize(record, schema as u32, symbol, ordinal);
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
                                .map_err(|_| "File record reader closed".to_owned())?;
                            queue
                                .owner
                                .signal(1 << 5)
                                .map_err(|_| "File output owner interrupted".to_owned())?;
                        }
                        Ok(())
                    }
                },
            )
            .await;
            reservation.result = super::super::live::NativeControlResult::finished(
                result
                    .map(|_| ())
                    .map_err(|e| format!("File decoder failed: {e:?}")),
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
/// Copies a file chunk of at most 65,536 bytes into a free input slot. Capacity
/// rejection consumes/copies nothing; signal bit 6 announces released space.
/// Length zero marks EOF and is idempotent. No pointer crosses an await.
/// # Safety
/// For nonzero length, input is readable for length bytes during this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_file_push_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        if length > 65536 || (length > 0 && input.is_null()) {
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
        if !queue.is_file {
            return Err(Status::InvalidInput);
        }
        let mut sender = queue.input.lock().map_err(|_| Status::InternalFailure)?;
        if length == 0 {
            sender.take();
            return Ok(());
        }
        let sender = sender.as_ref().ok_or(Status::Closed)?;
        let permit = sender.try_reserve().map_err(|e| match e {
            mpsc::error::TrySendError::Full(_) => Status::Capacity,
            mpsc::error::TrySendError::Closed(_) => Status::Closed,
        })?;
        // SAFETY: Validated bounded input, copied only after obtaining capacity.
        let bytes = bytes::Bytes::copy_from_slice(unsafe {
            std::slice::from_raw_parts(input, length as usize)
        });
        permit.send(bytes);
        Ok(())
    })
}
