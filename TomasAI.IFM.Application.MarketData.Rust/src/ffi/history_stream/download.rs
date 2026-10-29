//! Provider download bytes flow through four fixed 64 KiB queue slots. Only
//! native-owned copies cross the ABI; storage completion remains with the caller.
use super::*;
/// Begins one descriptor-checked download. JSON contains job, filename, size and
/// hash from the application's immutable file descriptor. The provider descriptor
/// is re-read and must match before any download bytes are admitted. Standard
/// control completion is successful only after full size/checksum verification.
/// # Safety
/// Input is readable for length bytes (1..16 KiB); no pointer is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_download_open_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = Deadline::operation(Duration::from_millis(budget_ms));
    boundary(|| {
        if input.is_null() || length == 0 || length > 16384 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller supplies readable bounded query storage, copied here.
        let v: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let text = |key: &str, max: usize| {
            v[key]
                .as_str()
                .filter(|s| !s.is_empty() && s.len() <= max && !s.chars().any(char::is_control))
                .map(str::to_owned)
                .ok_or(Status::InvalidInput)
        };
        let job = text("job", 256)?;
        let name = text("filename", 4096)?;
        let hash = text("hash", 2048)?;
        let size = v["size"].as_u64().ok_or(Status::InvalidInput)?;
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
        let (sender, receiver) = mpsc::channel(4);
        let queue = Arc::new(Queue {
            receiver: None,
            bytes: Some(Mutex::new(ByteOutput {
                receiver,
                front: None,
            })),
            owner: instance.owner.clone(),
            input: Mutex::new(None),
            is_file: false,
        });
        readers.insert(operation, queue.clone());
        drop(readers);
        let signal = Signal(queue.clone());
        let work = async move {
            let token = CancellationToken::new();
            let result =
                async {
                    let file = history
                        .batch_files(&job, &token, deadline)
                        .await?
                        .into_iter()
                        .find(|f| f.filename == name)
                        .ok_or(crate::historical::Failure::Input(
                            "Immutable historical file is missing",
                        ))?;
                    if file.size != size
                        || file.hash.strip_prefix("sha256:").unwrap_or(&file.hash) != hash
                    {
                        return Err(crate::historical::Failure::Input(
                            "Historical file descriptor changed",
                        ));
                    }
                    history
                        .download_batch_async(&file, &token, deadline, move |bytes| {
                            let bytes = bytes::Bytes::copy_from_slice(bytes);
                            let sender = sender.clone();
                            let queue = queue.clone();
                            async move {
                                sender.send(bytes).await.map_err(|_| {
                                    "Historical download consumer closed".to_owned()
                                })?;
                                queue.owner.signal(1 << 5).map_err(|_| {
                                    "Historical download owner interrupted".to_owned()
                                })?;
                                Ok(())
                            }
                        })
                        .await?;
                    Ok(())
                }
                .await;
            reservation.result = super::super::live::NativeControlResult::finished(
                result.map_err(|e| format!("Historical download failed: {e:?}")),
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
/// Copies and consumes one chunk, at most 65,536 bytes. Insufficient capacity
/// preserves the queued chunk and all caller output bytes/scalars. `ended` is one
/// only when the producer is gone and its complete output has been consumed.
/// # Safety
/// Output is writable for capacity bytes; length and ended are disjoint aligned
/// writable scalar destinations. Foreign memory is borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_download_take_v1(
    handle: u64,
    operation: u64,
    output: *mut u8,
    capacity: u32,
    length: *mut u32,
    ended: *mut u32,
) -> i32 {
    boundary(|| {
        if output.is_null() || capacity == 0 || capacity > 65536 || !valid(length) || !valid(ended)
        {
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
        let mut bytes = queue
            .bytes
            .as_ref()
            .ok_or(Status::InvalidInput)?
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if bytes.front.is_none() {
            bytes.front = bytes.receiver.try_recv().ok();
        }
        if bytes
            .front
            .as_ref()
            .is_some_and(|b| b.len() > capacity as usize)
        {
            return Err(Status::Capacity);
        }
        let count = bytes.front.as_ref().map_or(0, bytes::Bytes::len);
        if let Some(front) = bytes.front.take() {
            // SAFETY: The chunk length was checked against caller capacity.
            unsafe {
                std::ptr::copy_nonoverlapping(front.as_ptr(), output, front.len());
            }
        }
        let done = bytes.front.is_none() && bytes.receiver.is_closed() && bytes.receiver.is_empty();
        // SAFETY: Caller supplies exclusive aligned independent scalar outputs.
        unsafe {
            length.write(count as u32);
            ended.write(u32::from(done));
        }
        Ok(())
    })
}
