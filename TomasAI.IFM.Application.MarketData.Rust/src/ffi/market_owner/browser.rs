//! Retained copied browser results reuse the existing command/result ABI.
use crate::{
    api::owner::MarketData,
    ffi::runtime::{Instance, Status},
    option_chain::browser::Command,
    runtime::Deadline,
};
use serde_json::Value;
use std::sync::Arc;
pub(super) fn submit(
    instance: Arc<Instance>,
    native: Arc<MarketData>,
    operation: u64,
    value: Value,
) -> Result<(), Status> {
    let command = Command::parse(&value).map_err(|_| Status::InvalidInput)?;
    let frozen = value["frozen"].as_bool().ok_or(Status::InvalidInput)?;
    let mut retained = crate::ffi::retained::Pending::reserve(&instance, operation)?;
    let mut reservation = instance.reserve_control(operation)?;
    let accepted = if frozen && !command.release {
        Ok(())
    } else {
        instance.enter(|| native.begin_browser(command.clone()))
    };
    let work = async move {
        let completed = async {
            accepted?;
            let output = if frozen && !command.release {
                rmpv::Value::Array(vec![
                    rmpv::Value::Nil,
                    400.into(),
                    "Frozen emulator preview is not selected by the native live backend.".into(),
                ])
            } else {
                native
                    .browser_result(
                        &command,
                        &tokio_util::sync::CancellationToken::new(),
                        Deadline::recovery(),
                    )
                    .await?
            };
            retained.commit(crate::messaging::codec::encode(&output)?, 16 * 1024 * 1024)?;
            Ok::<(), String>(())
        }
        .await;
        reservation.result = crate::ffi::live::NativeControlResult::finished(completed);
        drop(retained);
        drop(reservation);
        Ok(0.0)
    };
    if let Err(status) = instance.submit_observer(operation, work) {
        instance
            .control
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .remove(&operation);
        return Err(status);
    }
    Ok(())
}
