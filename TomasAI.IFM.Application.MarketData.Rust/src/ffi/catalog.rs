//! Caller-buffer catalog ABI. Hydration transfers one source line at a time;
//! neither an unbounded JSON universe nor borrowed managed memory is retained.
use super::runtime::{Status, boundary, instance, valid};
use crate::catalog::repository::{Error, Repository};
use std::sync::{Arc, Mutex};

pub(super) fn repository(handle: u64) -> Result<Arc<Mutex<Repository>>, Status> {
    instance(handle)?
        .catalog
        .lock()
        .map_err(|_| Status::InternalFailure)?
        .clone()
        .ok_or(Status::InvalidInput)
}

fn status(error: Error) -> Status {
    match error {
        Error::Invalid | Error::Ambiguous => Status::InvalidInput,
        Error::Capacity => Status::Capacity,
        Error::Conflict => Status::Busy,
    }
}
unsafe fn text<'a>(pointer: *const u8, length: u32, maximum: u32) -> Result<&'a str, Status> {
    if pointer.is_null() || length == 0 || length > maximum {
        return Err(Status::InvalidInput);
    }
    // SAFETY: The export requires readable caller memory for this bounded slice.
    std::str::from_utf8(unsafe { std::slice::from_raw_parts(pointer, length as usize) })
        .map_err(|_| Status::InvalidInput)
}

/// Configures a runtime's catalog once. Caps bound two retained universes to
/// 128 MiB of raw source and 200,000 total projected rows during refresh.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_catalog_configure_v1(handle: u64, rows: u32, bytes: u32) -> i32 {
    boundary(|| {
        if rows == 0 || rows > 100_000 || bytes == 0 || bytes > 64 * 1024 * 1024 {
            return Err(Status::InvalidInput);
        }
        let instance = instance(handle)?;
        let mut catalog = instance
            .catalog
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if catalog.is_some() {
            return Err(Status::Busy);
        }
        *catalog = Some(Arc::new(Mutex::new(
            Repository::new(rows as usize, bytes as usize).map_err(status)?,
        )));
        Ok(())
    })
}

/// Begins one replacement and returns a unique token.
/// # Safety
/// `output` must point to one aligned exclusively writable `u64`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_begin_v1(handle: u64, output: *mut u64) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        let catalog = repository(handle)?;
        let token = catalog
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .begin()
            .map_err(status)?;
        // SAFETY: Pointer validated above; caller guarantees exclusive storage.
        unsafe { output.write(token) };
        Ok(())
    })
}

/// Appends one source-exact definition; all input is consumed before returning.
/// # Safety
/// Dataset and source pointers must be readable for their supplied lengths.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_append_v1(
    handle: u64,
    token: u64,
    dataset: *const u8,
    dataset_length: u32,
    source: *const u8,
    source_length: u32,
) -> i32 {
    boundary(|| {
        // SAFETY: Export caller supplies readable slices. Bounds checked first.
        let dataset = unsafe { text(dataset, dataset_length, 64)? };
        // SAFETY: Same caller-owned readable source contract, with its own cap.
        let source = unsafe { text(source, source_length, 65_536)? };
        let catalog = repository(handle)?;
        catalog
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .append(token, dataset, source)
            .map_err(status)
    })
}

/// Commits (action 1) or aborts (action 0) one replacement. Returns the committed
/// revision, permitting the caller to detect racing refreshes during lookup.
/// # Safety
/// `output` must point to one aligned exclusively writable `u64`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_finish_v1(
    handle: u64,
    token: u64,
    action: u32,
    output: *mut u64,
) -> i32 {
    boundary(|| {
        if !valid(output) || action > 1 {
            return Err(Status::InvalidInput);
        }
        let repository = repository(handle)?;
        let mut catalog = repository.lock().map_err(|_| Status::InternalFailure)?;
        if action == 1 {
            catalog.commit(token).map_err(status)?;
        } else {
            catalog.abort(token).map_err(status)?;
        }
        // SAFETY: Pointer validated; caller owns the output.
        unsafe { output.write(catalog.revision()) };
        Ok(())
    })
}

/// Copies one uniquely resolved source line at an expected revision. A missing
/// contract succeeds with zero bytes. Insufficient capacity/stale revision leaves
/// caller memory unchanged. The caller supplies a reusable 65,536-byte buffer.
/// # Safety
/// ID is readable for its length, output writable for capacity, and length points
/// to an aligned exclusive `u32`. Input and output must not overlap.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_catalog_resolve_v1(
    handle: u64,
    revision: u64,
    id: *const u8,
    id_length: u32,
    output: *mut u8,
    capacity: u32,
    length: *mut u32,
) -> i32 {
    boundary(|| {
        if !valid(length) || output.is_null() || capacity > 65_536 {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Readable caller input; bounded before dereference.
        let id = unsafe { text(id, id_length, 256)? };
        let repository = repository(handle)?;
        let row = repository
            .lock()
            .map_err(|_| Status::InternalFailure)?
            .resolve(revision, id)
            .map_err(status)?;
        let bytes = row.as_ref().map_or(&[][..], |d| d.json.as_bytes());
        if bytes.len() > capacity as usize {
            return Err(Status::Capacity);
        }
        // SAFETY: Caller guarantees exclusive capacity; immutable native source
        // cannot overlap managed output, and its Arc remains alive for the copy.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            length.write(bytes.len() as u32);
        }
        Ok(())
    })
}

/// Maps one exact provider JSON line through the qualified native parser without
/// retaining a catalog or starting I/O. The original source stays caller-owned;
/// the returned JSON projection contains only already-parsed contract fields.
/// # Safety
/// Dataset/source are readable for their lengths. Output is writable for capacity
/// bytes and length is an aligned disjoint scalar. No caller pointer is retained.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_definition_project_v1(
    dataset: *const u8,
    dataset_length: u32,
    source: *const u8,
    source_length: u32,
    output: *mut u8,
    capacity: u32,
    length: *mut u32,
) -> i32 {
    super::runtime::boundary(|| {
        if dataset.is_null()
            || source.is_null()
            || output.is_null()
            || !super::runtime::valid(length)
            || dataset_length == 0
            || dataset_length > 64
            || source_length == 0
            || source_length > 65536
            || capacity == 0
            || capacity > 262144
        {
            return Err(super::runtime::Status::InvalidInput);
        }
        // SAFETY: All caller-readable lengths were bounded; values are borrowed only here.
        let dataset = std::str::from_utf8(unsafe {
            std::slice::from_raw_parts(dataset, dataset_length as usize)
        })
        .map_err(|_| super::runtime::Status::InvalidInput)?;
        // SAFETY: Source is readable for the bounded source_length per this export's contract.
        let source = std::str::from_utf8(unsafe {
            std::slice::from_raw_parts(source, source_length as usize)
        })
        .map_err(|_| super::runtime::Status::InvalidInput)?;
        let row = crate::catalog::Definition::parse(dataset, source)
            .map_err(|_| super::runtime::Status::InvalidInput)?;
        let bytes =
            crate::catalog::queries::project(&row).map_err(|_| super::runtime::Status::Capacity)?;
        if bytes.len() > capacity as usize {
            return Err(super::runtime::Status::Capacity);
        }
        // SAFETY: Exclusive caller output has sufficient capacity; scalar is disjoint.
        unsafe {
            std::ptr::copy_nonoverlapping(bytes.as_ptr(), output, bytes.len());
            length.write(bytes.len() as u32);
        }
        Ok(())
    })
}
