//! Historical client configuration and provider-to-catalog acquisition ABI.
//! Credentials are copied only at configuration; requests retain the configured
//! shared owner and its bounded concurrency rather than creating ad hoc clients.
use super::runtime::{Status, boundary, instance, valid};
use crate::{
    catalog::hydration::{self, Universe},
    dns::Resolver,
    historical::History,
    runtime::owner::{Outcome, foreign_deadline},
};
use std::sync::Arc;
use tokio_util::sync::CancellationToken;

unsafe fn json(input: *const u8, length: u32, maximum: u32) -> Result<serde_json::Value, Status> {
    if input.is_null() || length == 0 || length > maximum {
        return Err(Status::InvalidInput);
    }
    // SAFETY: Export requires readable caller memory; length was bounded first.
    serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
        .map_err(|_| Status::InvalidInput)
}
/// Configures one shared historical owner without connecting. Cold JSON contains
/// endpoint, key, and one to four numeric DNS server socket addresses.
/// # Safety
/// Input must be readable for length bytes. No caller memory is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_configure_v1(
    handle: u64,
    input: *const u8,
    length: u32,
) -> i32 {
    boundary(|| {
        // SAFETY: Caller-readable bounded configuration.
        let value = unsafe { json(input, length, 8192)? };
        let endpoint = value["endpoint"].as_str().ok_or(Status::InvalidInput)?;
        let key = value["key"].as_str().ok_or(Status::InvalidInput)?;
        if key.bytes().any(|b| b.is_ascii_control()) {
            return Err(Status::InvalidInput);
        }
        let dns = value["dns"].as_array().ok_or(Status::InvalidInput)?;
        if dns.is_empty() || dns.len() > 4 {
            return Err(Status::InvalidInput);
        }
        let servers = dns
            .iter()
            .map(|v| {
                v.as_str()
                    .ok_or(Status::InvalidInput)?
                    .parse()
                    .map_err(|_| Status::InvalidInput)
            })
            .collect::<Result<Vec<_>, _>>()?;
        let instance = instance(handle)?;
        let resolver = Arc::new(
            Resolver::with_slots(servers, instance.dns_slots.clone())
                .map_err(|_| Status::InvalidInput)?,
        );
        let client = History::new(endpoint, key.to_owned(), resolver)
            .map_err(|_| Status::InvalidInput)?
            .with_notifications(instance.owner.clone());
        let mut history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if history.is_some() {
            return Err(Status::Busy);
        }
        *history = Some(Arc::new(client));
        Ok(())
    })
}

/// Copies and consumes one complete redacted HTTP warning as bounded UTF-8
/// JSON. An empty queue succeeds with length zero. Insufficient capacity retains
/// the warning and preserves both caller outputs; delivery uses signal bit four.
/// # Safety
/// Output is writable for capacity bytes; length is aligned exclusive storage
/// independent of output. No pointer or caller allocation is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_history_warning_v1(
    handle: u64,
    output: *mut u8,
    capacity: u32,
    length: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(length) || output.is_null() || capacity == 0 || capacity > 65_536 {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone();
        let treasury = instance
            .treasury
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone();
        let fmp = instance
            .fmp
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone();
        if history.is_none() && treasury.is_none() && fmp.is_none() {
            return Err(Status::InvalidInput);
        }
        let copy = |warning: &crate::historical::Warning| {
            let bytes=serde_json::to_vec(&serde_json::json!({"endpoint":warning.endpoint,"status":warning.status,"request_id":warning.request_id,"text":warning.text}))
                .map_err(|_|crate::historical::Failure::Processing("Warning encoding failed".into()))?;
            if bytes.len() > capacity as usize {
                return Err(crate::historical::Failure::Input(
                    "Warning output capacity is insufficient",
                ));
            }
            Ok(bytes)
        };
        let take = || -> Result<Option<Vec<u8>>, crate::historical::Failure> {
            let value = if let Some(history) = history {
                history.consume_warning(copy)?
            } else {
                None
            };
            if value.is_some() {
                return Ok(value);
            }
            if let Some(treasury) = treasury {
                let value = treasury.consume_warning(copy)?;
                if value.is_some() {
                    return Ok(value);
                }
            }
            if let Some(fmp) = fmp {
                fmp.consume_warning(copy)
            } else {
                Ok(None)
            }
        };
        let value = take().map_err(|failure| match failure {
            crate::historical::Failure::Input(_) => Status::InvalidInput,
            _ => Status::InternalFailure,
        })?;
        let count = value.as_ref().map_or(0, Vec::len);
        if let Some(bytes) = value {
            // SAFETY: Bounded by validated caller capacity before consuming.
            unsafe { std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len()) };
        }
        // SAFETY: Caller guarantees independent writable aligned scalar.
        unsafe { length.write(count as u32) };
        Ok(())
    })
}

/// Submits atomic provider hydration under the original remaining recovery
/// budget. JSON is an array of one to three dataset/parents objects. Completion
/// uses the existing retained control-result/take contract, including cancellation.
/// # Safety
/// Input is readable for length bytes and copied before the call returns.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_refresh_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = foreign_deadline(budget_ms);
    boundary(|| {
        // SAFETY: Export's readable input contract; length independently bounded.
        let value = unsafe { json(input, length, 8192)? };
        let array = value.as_array().ok_or(Status::InvalidInput)?;
        if array.is_empty() || array.len() > 3 {
            return Err(Status::InvalidInput);
        }
        let mut universes = Vec::with_capacity(array.len());
        for item in array {
            let dataset = item["dataset"]
                .as_str()
                .filter(|s| !s.is_empty() && s.len() <= 64)
                .ok_or(Status::InvalidInput)?;
            let parents = item["parents"].as_array().ok_or(Status::InvalidInput)?;
            if parents.len() > 16 {
                return Err(Status::InvalidInput);
            }
            let parents = parents
                .iter()
                .map(|v| {
                    v.as_str()
                        .filter(|s| s.len() <= 64)
                        .map(str::to_owned)
                        .ok_or(Status::InvalidInput)
                })
                .collect::<Result<Vec<_>, _>>()?;
            if universes.iter().any(|u: &Universe| u.dataset == dataset) {
                return Err(Status::InvalidInput);
            }
            universes.push(Universe {
                dataset: dataset.into(),
                parents,
            });
        }
        let repository = super::catalog::repository(handle)?;
        let instance = instance(handle)?;
        let history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            let cancellation = CancellationToken::new();
            let result =
                hydration::refresh(&history, repository, &universes, &cancellation, deadline).await;
            reservation.result = super::live::NativeControlResult::finished(
                result
                    .map(|_| ())
                    .map_err(|e| format!("Catalog acquisition failed: {e:?}")),
            );
            drop(reservation);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok(0.0)
            }
        };
        if let Err(status) = instance.submit(operation, deadline, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}

/// Reads the committed revision after a completed refresh; no provider request.
/// # Safety
/// Output points to one aligned exclusive writable u64.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_revision_v1(handle: u64, output: *mut u64) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        let repository = super::catalog::repository(handle)?;
        let revision = repository
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .revision();
        // SAFETY: Validated output, exclusively owned by caller.
        unsafe { output.write(revision) };
        Ok(())
    })
}

/// Acquires exact provider-scoped definitions into an immutable query without
/// changing the active live catalog. Control result payload identifies the query;
/// consume/take it on every terminal outcome and close any returned query token.
/// # Safety
/// Input points to length readable bytes; no caller borrow survives this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_definitions_query_v1(
    handle: u64,
    operation: u64,
    input: *const u8,
    length: u32,
    budget_ms: u64,
) -> i32 {
    let deadline = foreign_deadline(budget_ms);
    boundary(|| {
        // SAFETY: Bounded readable cold request supplied by the ABI caller.
        let value = unsafe { json(input, length, 2 * 1024 * 1024)? };
        let dataset = value["dataset"]
            .as_str()
            .filter(|s| !s.is_empty() && s.len() <= 64)
            .ok_or(Status::InvalidInput)?
            .to_owned();
        let exact = value.get("symbols").is_some();
        if exact && value.get("parents").is_some() {
            return Err(Status::InvalidInput);
        }
        let parents = value[if exact { "symbols" } else { "parents" }]
            .as_array()
            .filter(|p| p.len() <= if exact { 2048 } else { 16 } && (!exact || !p.is_empty()))
            .ok_or(Status::InvalidInput)?;
        let parents = parents
            .iter()
            .map(|p| {
                p.as_str()
                    .filter(|s| s.len() <= if exact { 70 } else { 64 })
                    .map(str::to_owned)
                    .ok_or(Status::InvalidInput)
            })
            .collect::<Result<Vec<_>, _>>()?;
        let instance = instance(handle)?;
        let history = instance
            .history
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .clone()
            .ok_or(Status::InvalidInput)?;
        let queries = instance.catalog_queries.clone();
        let mut reservation = instance.reserve_control(operation)?;
        let work = async move {
            let result = async {
                let mut catalog = crate::catalog::Catalog::new(100_000, 64 * 1024 * 1024)
                    .map_err(|e| format!("{e:?}"))?;
                let append = |line: &str| {
                    crate::catalog::Definition::parse(&dataset, line)
                        .and_then(|row| catalog.insert(row))
                        .map_err(|e| format!("Invalid provider definition: {e:?}"))
                };
                let token = CancellationToken::new();
                if exact {
                    history
                        .exact_definitions(&dataset, &parents, &token, deadline, append)
                        .await
                } else {
                    history
                        .latest_definitions(&dataset, &parents, &token, deadline, append)
                        .await
                }
                .map_err(|e| format!("{e:?}"))?;
                if deadline.remaining().is_zero() {
                    return Err("Original definition-query deadline expired".into());
                }
                let mut rows = crate::catalog::queries::provider_details(catalog.definitions());
                if exact {
                    rows.retain(|row| parents.contains(&row.symbol));
                }
                let (token, count) = queries
                    .lock()
                    .map_err(|_| "Definition query writer interrupted".to_owned())?
                    .open(rows, crate::catalog::queries::Query::All)
                    .map_err(|e| format!("{e:?}"))?;
                Ok::<_, String>(serde_json::json!({"token": token, "count": count}).to_string())
            }
            .await;
            reservation.result = match result {
                Ok(payload) => super::live::NativeControlResult::payload(&payload)
                    .expect("bounded query identity payload"),
                Err(error) => super::live::NativeControlResult::finished(Err(error)),
            };
            drop(reservation);
            if deadline.remaining().is_zero() {
                Err(Outcome::DeadlineExpired)
            } else {
                Ok(0.0)
            }
        };
        if let Err(status) = instance.submit(operation, deadline, work) {
            instance
                .control
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}
