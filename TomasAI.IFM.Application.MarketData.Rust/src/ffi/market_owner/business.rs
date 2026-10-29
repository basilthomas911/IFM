//! Copied command/result operations reuse the existing retained-result ABI.
//! They create non-owning observers; accepted desired intent remains in Rust.
use crate::ffi::runtime::{Instance, Status};
use crate::{
    api::owner::{
        MarketData,
        business::{Release, result},
    },
    dataset::transport::Lane,
};
use serde_json::Value;
use std::sync::Arc;
pub(super) fn submit(
    instance: Arc<Instance>,
    native: Arc<MarketData>,
    operation: u64,
    value: Value,
) -> Result<(), Status> {
    let mut retained = crate::ffi::retained::Pending::reserve(&instance, operation)?;
    let mut reservation = instance.reserve_control(operation)?;
    let release = if value["kind"].as_str() == Some("composition-release") {
        Some(Release::parse(&value))
    } else {
        None
    };
    // Commands are linearized synchronously. Losing an observer never withdraws
    // an accepted handoff or resource release; every provider await stays native.
    let accepted = instance.enter(|| match &release {
        None => native
            .reconcile_business()
            .map(|active| if active { None } else { Some(rmpv::Value::Nil) }),
        Some(Ok(release)) => native.begin_business_release(release),
        Some(Err(code)) => Ok(Some(result(false, Some(code)))),
    });
    let work = async move {
        let completed = async {
            let output = match accepted? {
                Some(output) => output,
                None => {
                    native.wait_ready(Lane::OptionsGlbx, false).await?;
                    match &release {
                        Some(Ok(release)) => native.finish_business_release(release)?,
                        _ => native.wait_committed_state().await?,
                    }
                }
            };
            retained.commit(crate::messaging::codec::encode(&output)?, 65536)?;
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
