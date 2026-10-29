//! Immutable catalog query snapshots exposed through integer identities and
//! caller buffers. Queries never hold live lifecycle or asynchronous I/O locks.
use super::{
    catalog::repository,
    runtime::{Status, boundary, instance, valid},
};
use crate::catalog::{
    queries::{self, Query},
    selection,
};

/// Resolves current/term-structure futures from an immutable native query. All
/// maturity selection, currency policy and public DTO construction stay in Rust.
///
/// # Safety
/// input holds length readable JSON bytes; output holds capacity writable bytes;
/// required is aligned exclusive writable u32 storage. No pointer is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_current_v1(
    handle: u64,
    token: u64,
    input: *const u8,
    length: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if input.is_null()
            || length == 0
            || length > 4096
            || output.is_null()
            || capacity > 2 * 1024 * 1024
            || !valid(required)
        {
            return Err(Status::InvalidInput);
        }
        // SAFETY: The bounded readable input slice belongs to the caller.
        let request: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let symbol = request["symbol"].as_str().ok_or(Status::InvalidInput)?;
        let date = super::reference::date(
            u32::try_from(request["date"].as_u64().ok_or(Status::InvalidInput)?)
                .map_err(|_| Status::InvalidInput)?,
        )?;
        let count = usize::try_from(request["count"].as_u64().ok_or(Status::InvalidInput)?)
            .map_err(|_| Status::InvalidInput)?;
        let fallback = if request["fallback"].is_null() {
            None
        } else {
            Some(
                request["fallback"]
                    .as_str()
                    .filter(|s| s.len() <= 256)
                    .ok_or(Status::InvalidInput)?,
            )
        };
        let rows = instance(handle)?
            .catalog_queries
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .rows(token)
            .map_err(status)?;
        let result = crate::catalog::current::select(&rows, symbol, date, count, fallback)
            .map_err(|_| Status::InvalidInput)?;
        let bytes = crate::messaging::codec::encode(&result.value())
            .map_err(|_| Status::InternalFailure)?;
        if bytes.len() > 2 * 1024 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: required points to caller-owned writable scalar storage.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller supplies writable capacity, validated against length.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
        }
        Ok(())
    })
}

/// Opaque query identity and exact result count. No pointer crosses the ABI.
#[repr(C)]
#[derive(Default, Clone, Copy)]
pub struct NativeCatalogQuery {
    /// DTO size, 24 bytes.
    pub size: u32,
    /// Always zero.
    pub reserved: u32,
    /// Positive runtime-local query identity.
    pub token: u64,
    /// Immutable count, at most the configured catalog row limit.
    pub count: u32,
    /// Always zero.
    pub reserved_tail: u32,
}
fn status(error: queries::Error) -> Status {
    match error {
        queries::Error::Invalid => Status::InvalidInput,
        queries::Error::Capacity => Status::Capacity,
    }
}
fn date(value: &str) -> Result<time::Date, Status> {
    let parts = value.split('-').collect::<Vec<_>>();
    if parts.len() != 3 {
        return Err(Status::InvalidInput);
    }
    let year = parts[0].parse().map_err(|_| Status::InvalidInput)?;
    let month = parts[1]
        .parse::<u8>()
        .ok()
        .and_then(|n| time::Month::try_from(n).ok())
        .ok_or(Status::InvalidInput)?;
    let day = parts[2].parse().map_err(|_| Status::InvalidInput)?;
    time::Date::from_calendar_date(year, month, day).map_err(|_| Status::InvalidInput)
}
/// Opens one immutable all-row or qualified chain query. Expected revision must
/// still be current when pinned; subsequent refreshes cannot change its rows.
/// Cold query JSON is bounded to 8192 bytes. Four snapshots/128 MiB retained raw
/// source are admitted per runtime. Explicit close or runtime disposal releases it.
/// # Safety
/// Input is readable for length; output is aligned exclusive writable storage.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_query_v1(
    handle: u64,
    revision: u64,
    input: *const u8,
    length: u32,
    output: *mut NativeCatalogQuery,
) -> i32 {
    boundary(|| {
        if !valid(output) || input.is_null() || length == 0 || length > 8192 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller input is readable and bounded before dereference.
        let value: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let repository = repository(handle)?;
        let catalog_rows = {
            let repository = repository.lock().map_err(|_| Status::InternalFailure)?;
            if repository.revision() != revision {
                return Err(Status::Busy);
            }
            repository.snapshot()
        };
        let rows = if let Some(token) = value["source_token"].as_u64() {
            instance(handle)?
                .catalog_queries
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .rows(token)
                .map_err(status)?
        } else {
            catalog_rows.clone()
        };
        let dataset = value["dataset"].as_str().unwrap_or("");
        if dataset.len() > 64 {
            return Err(Status::InvalidInput);
        }
        let rows: Vec<_> = rows
            .into_iter()
            .filter(|d| dataset.is_empty() || d.dataset == dataset)
            .collect();
        let mut selected = None;
        let query = match value["kind"].as_str() {
            Some("all") => Query::All,
            Some("chain") => {
                let maturity = date(value["maturity"].as_str().ok_or(Status::InvalidInput)?)?;
                let rights = value["rights"]
                    .as_u64()
                    .filter(|n| (1..=3).contains(n))
                    .ok_or(Status::InvalidInput)? as u8;
                let policy = value["policy"]
                    .as_u64()
                    .filter(|n| (1..=3).contains(n))
                    .ok_or(Status::InvalidInput)? as u8;
                let underlying = value["underlying"]
                    .as_str()
                    .filter(|v| !v.is_empty() && v.len() <= 256)
                    .ok_or(Status::InvalidInput)?;
                if let Some(id) = value["selected"].as_str() {
                    let identity = selection::parse(id).map_err(|_| Status::InvalidInput)?;
                    let mut matches = catalog_rows
                        .iter()
                        .filter(|d| selection::matches(&identity, d));
                    selected = matches.next().cloned();
                    if selected.is_none() || matches.next().is_some() {
                        return Err(Status::InvalidInput);
                    }
                }
                Query::Chain(selection::Chain {
                    maturity,
                    rights,
                    policy,
                    underlying,
                    selected_underlying: selected.as_deref(),
                })
            }
            _ => return Err(Status::InvalidInput),
        };
        let instance = instance(handle)?;
        let (token, count) = instance
            .catalog_queries
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .open(rows, query)
            .map_err(status)?;
        let result = NativeCatalogQuery {
            size: 24,
            token,
            count: count as u32,
            ..Default::default()
        };
        // SAFETY: Caller provides aligned exclusive output, validated above.
        unsafe { output.write(result) };
        Ok(())
    })
}
/// Reads one pinned row as raw source (format 0) or native-parsed projection
/// (format 1). Output capacity is at most 262144 bytes. No partial copy on error.
/// # Safety
/// Output holds capacity writable bytes; length is aligned exclusive u32 storage
/// independent of output. No pointers are retained after return.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_query_row_v1(
    handle: u64,
    token: u64,
    index: u32,
    format: u32,
    output: *mut u8,
    capacity: u32,
    length: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(length) || output.is_null() || capacity > 262_144 || format > 1 {
            return Err(Status::InvalidInput);
        }
        let row = instance(handle)?
            .catalog_queries
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .row(token, index as usize)
            .map_err(status)?;
        let projected;
        let bytes = if format == 0 {
            row.json.as_bytes()
        } else {
            projected = queries::project(&row).map_err(|_| Status::Capacity)?;
            &projected
        };
        if bytes.len() > capacity as usize {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller owns sufficient exclusive non-overlapping output space.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            length.write(bytes.len() as u32);
        }
        Ok(())
    })
}
/// Releases one query. A stale identity is rejected rather than closing a newer
/// query accidentally; no provider resources or threads are joined here.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_catalog_query_close_v1(handle: u64, token: u64) -> i32 {
    boundary(|| {
        instance(handle)?
            .catalog_queries
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .close(token)
            .map_err(status)
    })
}

/// Projects public option read models directly from an immutable native query.
/// Every financial/reference conversion and ordering operation remains in Rust.
///
/// # Safety
/// Input has length readable bytes; required is aligned writable u32; output has
/// capacity writable bytes, or is null for a zero-capacity sizing request.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_option_models_v1(
    handle: u64,
    token: u64,
    input: *const u8,
    length: u32,
    output: *mut u8,
    capacity: u32,
    required: *mut u32,
) -> i32 {
    boundary(|| {
        if input.is_null()
            || length == 0
            || length > 16384
            || capacity > 4 * 1024 * 1024
            || !valid(required)
            || capacity > 0 && output.is_null()
        {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Bounded readable input borrowed only for this synchronous call.
        let request: serde_json::Value =
            serde_json::from_slice(unsafe { std::slice::from_raw_parts(input, length as usize) })
                .map_err(|_| Status::InvalidInput)?;
        let rows = instance(handle)?
            .catalog_queries
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .rows(token)
            .map_err(status)?;
        let model = crate::catalog::option_models::query(&rows, &request)
            .map_err(|_| Status::InvalidInput)?;
        let bytes = crate::messaging::codec::encode(&model).map_err(|_| Status::InternalFailure)?;
        if bytes.len() > 4 * 1024 * 1024 {
            return Err(Status::Capacity);
        }
        // SAFETY: Scalar pointer validated before output mutation.
        unsafe {
            required.write(bytes.len() as u32);
        }
        if (capacity as usize) < bytes.len() {
            return Err(Status::Capacity);
        }
        if !bytes.is_empty() {
            // SAFETY: Complete caller output capacity checked above.
            unsafe {
                std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            }
        }
        Ok(())
    })
}
