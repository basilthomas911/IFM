//! Explicit opt-in original-state checkpoint ABI. No provider is started by import/export.
use super::runtime::{Status,boundary};

unsafe fn input(pointer:*const u8,length:u32,maximum:usize)->Result<String,Status>{
    if pointer.is_null()||length==0||length as usize>maximum{return Err(Status::InvalidInput);}
    // SAFETY: ABI caller supplies readable bytes for the checked input length; the string is copied before return.
    let bytes=unsafe{std::slice::from_raw_parts(pointer,length as usize)};
    let text=std::str::from_utf8(bytes).map_err(|_|Status::InvalidInput)?;
    if text.contains('\0'){return Err(Status::InvalidInput);}Ok(text.to_owned())
}

/// Holds the original native state boundary after its source-forwarding lease is drained.
/// # Safety
/// `scope` must address `scope_length` readable UTF-8 bytes for this call.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_native_checkpoint_begin_v1(scope:*const u8,scope_length:u32,lease:u64)->i32{
    boundary(||{
        // SAFETY: Input lifetime and extent are the documented ABI caller contract.
        let scope=unsafe{input(scope,scope_length,128)}?;
        crate::checkpoint::begin_scope(&scope,lease).map_err(|_|Status::Busy)
    })
}
/// Releases only the exact original native checkpoint-state lease; forwarding stays caller-owned.
#[unsafe(no_mangle)]
pub extern "C" fn ifm_market_data_native_checkpoint_end_v1(lease:u64)->i32{boundary(||crate::checkpoint::end_scope(lease).map_err(|_|Status::Busy))}

/// Saves a new fsynced native binary image and returns its full-file SHA256.
/// # Safety
/// Inputs must reference their readable UTF-8 extents and `digest` must address 32 writable bytes.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_native_checkpoint_export_v1(scope:*const u8,scope_length:u32,lease:u64,path:*const u8,path_length:u32,digest:*mut u8)->i32{
    boundary(||{
        if digest.is_null(){return Err(Status::InvalidInput);}
        // SAFETY: Both input extents are checked and copied under the documented ABI contract.
        let (scope,path)=unsafe{(input(scope,scope_length,128)?,input(path,path_length,32768)?)};
        let path=std::path::Path::new(&path);if !path.is_absolute(){return Err(Status::InvalidInput);}
        let hash=crate::checkpoint::export_scope(&scope,lease,path).map_err(|_|Status::InternalFailure)?;
        // SAFETY: Caller promises a writable fixed 32-byte output, never retained after this copy.
        unsafe{std::ptr::copy_nonoverlapping(hash.as_ptr(),digest,32);}Ok(())
    })
}
/// Stages an externally SHA-bound original image for exact owner binding before source startup.
/// # Safety
/// Inputs must reference their readable UTF-8 extents and `digest` must address 32 readable bytes.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_native_checkpoint_import_v1(scope:*const u8,scope_length:u32,path:*const u8,path_length:u32,digest:*const u8)->i32{
    boundary(||{
        if digest.is_null(){return Err(Status::InvalidInput);}
        // SAFETY: The documented input extents and fixed digest are copied for this call only.
        let (scope,path,hash)=unsafe{(input(scope,scope_length,128)?,input(path,path_length,32768)?,std::slice::from_raw_parts(digest,32).try_into().unwrap())};
        let path=std::path::Path::new(&path);if !path.is_absolute(){return Err(Status::InvalidInput);}
        crate::checkpoint::import_scope(&scope,path,hash).map_err(|_|Status::InternalFailure)
    })
}

/// Binds a staged original image to an explicit isolated owned file root, never its live source directory.
/// # Safety
/// Inputs must reference the supplied readable UTF-8 extents for this call only.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_native_checkpoint_restore_root_v1(scope:*const u8,scope_length:u32,path:*const u8,path_length:u32)->i32{
    boundary(||{
        // SAFETY: Both caller-owned extents are checked and copied before this call returns.
        let(scope,path)=unsafe{(input(scope,scope_length,128)?,input(path,path_length,32768)?)};
        crate::checkpoint::configure_restore_root(&scope,std::path::Path::new(&path)).map_err(|_|Status::InvalidInput)
    })
}

#[cfg(test)]mod tests{
    use super::*;
    #[test]fn bad_abi_inputs_never_access_owner_or_start_provider(){assert_ne!(unsafe{ifm_market_data_native_checkpoint_begin_v1(std::ptr::null(),0,0)},0);assert_ne!(ifm_market_data_native_checkpoint_end_v1(0),0);assert_ne!(unsafe{ifm_market_data_native_checkpoint_export_v1(std::ptr::null(),0,0,std::ptr::null(),0,std::ptr::null_mut())},0);assert_ne!(unsafe{ifm_market_data_native_checkpoint_import_v1(std::ptr::null(),0,std::ptr::null(),0,std::ptr::null())},0);}
}
