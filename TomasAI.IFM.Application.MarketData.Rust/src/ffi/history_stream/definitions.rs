//! Full-universe definition streaming with bounded source-line retention.
use super::*;
/// Streams original latest definition JSON lines for an explicit dataset. No
/// whole-universe catalog or row-count cap is involved. Each of the four output
/// slots holds at most one 65,536-byte line; consumer backpressure consumes the
/// same original caller deadline. Standard byte drain and reader close apply.
/// # Safety
/// Dataset is readable for length bytes (1..64), borrowed only during this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_definitions_open_v1(
    handle: u64,
    operation: u64,
    dataset: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = Deadline::operation(Duration::from_millis(budget_ms));
    boundary(|| {
        if dataset.is_null() || length == 0 || length > 64 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller provides readable bounded dataset bytes, copied before return.
        let dataset =
            std::str::from_utf8(unsafe { std::slice::from_raw_parts(dataset, length as usize) })
                .map_err(|_| Status::InvalidInput)?
                .to_owned();
        if dataset.trim().is_empty() {
            return Err(Status::InvalidInput);
        }
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
            let result = history
                .all_definitions_async(&dataset, &token, deadline, move |line| {
                    let bytes = bytes::Bytes::copy_from_slice(line.as_bytes());
                    let sender = sender.clone();
                    let queue = queue.clone();
                    async move {
                        sender
                            .send(bytes)
                            .await
                            .map_err(|_| "Definition consumer closed".to_owned())?;
                        queue
                            .owner
                            .signal(1 << 5)
                            .map_err(|_| "Definition delivery owner interrupted".to_owned())?;
                        Ok(())
                    }
                })
                .await;
            reservation.result = super::super::live::NativeControlResult::finished(
                result
                    .map(|_| ())
                    .map_err(|e| format!("Definition acquisition failed: {e:?}")),
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
