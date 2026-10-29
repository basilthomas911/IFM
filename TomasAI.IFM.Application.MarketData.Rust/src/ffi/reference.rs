//! Owned official reference acquisition. Cold JSON configures one client; query
//! results are retained in at most 32 bounded slots until copied or discarded.
//! Cancellation drops HTTP/TLS/DNS futures before signaling completion.
use super::retained::Pending;
use super::runtime::{Status, boundary, instance, valid};
use crate::{
    dns::Resolver,
    reference::treasury_curve::Treasury,
    runtime::owner::{Outcome, foreign_deadline},
};
use std::sync::Arc;
use tokio_util::sync::CancellationToken;
pub(super) fn date(value: u32) -> Result<time::Date, Status> {
    if !(10101..=99991231).contains(&value) {
        return Err(Status::InvalidInput);
    }
    time::Date::from_calendar_date(
        (value / 10000) as i32,
        ((value / 100 % 100) as u8)
            .try_into()
            .map_err(|_| Status::InvalidInput)?,
        (value % 100) as u8,
    )
    .map_err(|_| Status::InvalidInput)
}
/// Configures the official-source client after the API clock has been supplied.
/// Input JSON contains endpoint and one to four numeric DNS server addresses.
/// Reconfiguration is Busy; no background connection is opened.
/// # Safety
/// Input is readable for length bytes (1..8192), borrowed only for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_reference_configure_v1(
    handle: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        if input.is_null() || length == 0 || length > 8192 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller supplies bounded readable bytes; retained values are owned.
        let value: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let endpoint = value["endpoint"].as_str().ok_or(Status::InvalidInput)?;
        let dns = value["dns"].as_array().ok_or(Status::InvalidInput)?;
        if dns.is_empty() || dns.len() > 4 {
            return Err(Status::InvalidInput);
        }
        let servers = dns
            .iter()
            .map(|value| {
                value
                    .as_str()
                    .ok_or(Status::InvalidInput)?
                    .parse()
                    .map_err(|_| Status::InvalidInput)
            })
            .collect::<Result<Vec<_>, _>>()?;
        let instance = instance(handle)?;
        let clock = instance.reference_clock()?;
        let resolver = Arc::new(
            Resolver::with_slots(servers, instance.dns_slots.clone())
                .map_err(|_| Status::InvalidInput)?,
        );
        let source = Treasury::new(endpoint, resolver, clock)
            .map_err(|_| Status::InvalidInput)?
            .with_notifications(instance.owner.clone());
        let mut treasury = instance
            .treasury
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if treasury.is_some() {
            return Err(Status::Busy);
        }
        *treasury = Some(Arc::new(source));
        Ok(())
    })
}
/// Submits mode 0 inclusive range or mode 1 latest at to_date. Dates are explicit
/// yyyyMMdd API dates. The original capped budget includes cache serialization,
/// network, parsing and bounded result serialization. Standard control completion
/// must be taken before copying the retained reference result.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_reference_submit_v1(
    handle: u64,
    operation: u64,
    mode: u32,
    from_date: u32,
    to_date: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = foreign_deadline(budget_ms);
    boundary(|| {
        if mode > 1 {
            return Err(Status::InvalidInput);
        }
        let to = date(to_date)?;
        let from = if mode == 0 { date(from_date)? } else { to };
        let instance = instance(handle)?;
        let source = instance
            .treasury
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let mut pending = Pending::reserve(&instance, operation)?;
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            let token = CancellationToken::new();
            let result=async{
                let rows=if mode==0{source.range(from,to,&token,deadline).await?}else{source.latest(to,&token,deadline).await?.into_iter().collect()};
                let output:Vec<_>=rows.iter().map(|row|serde_json::json!({"date":row.date.year()*10000+row.date.month()as i32*100+i32::from(row.date.day()),
                    "retrieved":row.retrieved,"source":crate::reference::treasury_curve::SOURCE,
                    "rates":row.rates.iter().map(|point|serde_json::json!({"months":point.months,"bits":point.percentage.bits})).collect::<Vec<_>>()})).collect();
                let bytes=serde_json::to_vec(&output).map_err(|_|crate::historical::Failure::Input("Reference serialization failed"))?;
                if bytes.len()>524_288{return Err(crate::historical::Failure::Input("Reference result capacity exceeded"));}
                pending.commit(bytes,524_288).map_err(crate::historical::Failure::Processing)?;Ok(())
            }.await;
            reservation.result = super::live::NativeControlResult::finished(
                result.map_err(|error| format!("Treasury acquisition failed: {error:?}")),
            );
            drop(pending);
            drop(reservation);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok(0.0)
            }
        };
        if let Err(error) = instance.submit(operation, deadline, work) {
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
/// Copies and consumes the complete UTF-8 result; insufficient capacity leaves
/// the result and both outputs untouched. Capacity is at most 16 MiB; official-source queries retain a 512 KiB result cap. Query
/// ownership is operation-scoped and cannot escape into another runtime.
/// # Safety
/// Output is writable for capacity bytes; length is aligned writable exclusive
/// storage disjoint from output. No pointer is retained after this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_reference_take_v1(
    handle: u64,
    operation: u64,
    output: *mut u8,
    capacity: u32,
    length: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(length) || output.is_null() || capacity == 0 || capacity > 16 * 1024 * 1024 {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let mut results = instance
            .reference_results
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        let bytes = results
            .get(&operation)
            .ok_or(Status::InvalidInput)?
            .as_ref()
            .ok_or(Status::Busy)?;
        if bytes.len() > capacity as usize {
            return Err(Status::Capacity);
        }
        // SAFETY: Validated size; caller guarantees disjoint exclusive ranges.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            length.write(bytes.len() as u32);
        }
        results.remove(&operation);
        Ok(())
    })
}
/// Releases an abandoned query slot, including a pending one. The asynchronous
/// completion remains owned and must still be canceled/joined through runtime
/// lifecycle. A late completion cannot recreate a discarded result.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_reference_discard_v1(handle: u64, operation: u64) -> i32 {
    boundary(|| {
        instance(handle)?
            .reference_results
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .remove(&operation);
        Ok(())
    })
}
