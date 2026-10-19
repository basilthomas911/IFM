//! In-process .NET 10 hosting and ABI lifetime management.
//!
//! The loader locates hostfxr, opens the bridge runtime configuration, obtains the
//! managed function table, and creates one managed session. ABI structs use C layout;
//! field order, sizes, calling conventions, and negative status codes are contractual.
//! The loaded library must outlive all function pointers and the session handle.
//!
//! ABI v4 submits MessagePack intents; v5 adds sequence-aware full/delta publication
//! copies. The wrapper owns native buffers and copies managed results before returning.
//! Only the intake worker should call this host; UI code sends intents through channels.
//! Errors are returned as strings for presentation by the application.

use libloading::Library;
use std::{
    ffi::c_void,
    path::{Path, PathBuf},
};
/// C-layout base bridge function table. Keep field order and function-pointer signatures synchronized with the managed ABI.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct Api {
    /// Version identifier supplied by this contract.
    pub version: u32,
    /// Structure size used by the native ABI or platform API.
    pub size: u32,
    /// Managed callback creating a session handle in caller-owned output storage.
    pub create: unsafe extern "C" fn(*mut u64) -> i32,
    /// Managed callback copying a publication kind into a caller-owned byte buffer.
    pub copy: unsafe extern "C" fn(u64, u32, *mut u8, i32, *mut i32) -> i32,
    /// Managed action key or callback for the operation described by this enclosing contract.
    pub action: unsafe extern "C" fn(u64, u32) -> i32,
    /// Managed callback releasing the session handle.
    pub destroy: unsafe extern "C" fn(u64) -> i32,
}
/// ABI v4 extension adding a structured MessagePack intent submission callback.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct ApiV4 {
    /// ABI prefix whose C layout must match the preceding interface version.
    pub base: Api,
    /// Managed callback accepting a MessagePack intent buffer for the session.
    pub submit: unsafe extern "C" fn(u64, *const u8, i32) -> i32,
}
/// ABI v5 callback signature for sequence-aware publication copies. Buffer length/status and UTC tick outputs follow the managed C ABI.
type CopyChanged = unsafe extern "C" fn(u64,u32,*mut u64,*mut u8,i32,*mut i32,*mut i64,*mut i64)->i32;
/// ABI v5 extension adding sequence-aware changed-publication copies.
#[repr(C)]
#[derive(Clone,Copy)]
pub struct ApiV5 {/** ABI prefix whose C layout must match the preceding interface version. */ pub base:ApiV4,/** Required v5 changed-copy callback; the Managed wrapper makes this optional when negotiation falls back to v4. */ pub copy_changed:CopyChanged}
/// Owner of the loaded host library, bridge function table, and managed session handle. Drop destroys the session before unloading the library.
pub struct Managed {
    /// Loaded hostfxr library retained for the full lifetime of its function pointers.
    _library: Library,
    /// Negotiated managed ABI base function table.
    api: Api,
    /// Managed callback accepting a MessagePack intent buffer for the session.
    submit: unsafe extern "C" fn(u64, *const u8, i32) -> i32,
    /// Opaque managed session handle; valid only until destroy succeeds or ownership ends.
    handle: u64,
    /// Optional ABI v5 sequence-aware copy callback.
    copy_changed: Option<CopyChanged>,
}
/// Encode a Windows path/string as null-terminated UTF-16 for the hostfxr API.
fn wide(s: impl AsRef<std::ffi::OsStr>) -> Vec<u16> {
    use std::os::windows::ffi::OsStrExt;
    s.as_ref().encode_wide().chain(Some(0)).collect()
}
/// Accept a nonnegative native status or return an operation-labelled hexadecimal error.
fn checked(code: i32, what: &str) -> Result<(), String> {
    if code < 0 {
        Err(format!("{what}: 0x{code:08x}"))
    } else {
        Ok(())
    }
}
impl Managed {
    /// Load .NET 10 hostfxr and the staged managed bridge, validate the function table, and create a session. Returns a startup error if dependencies or ABI negotiation fail.
    pub fn load(root: &Path) -> Result<Self, String> {
        // SAFETY: hostfxr exports and bridge delegates use the declared native
        // calling conventions. UTF-16 buffers and output storage remain alive
        // for each synchronous call. Only a successful version/size negotiation
        // permits reading the function table; Managed retains the library until
        // after the session is destroyed.
        unsafe {
            let dotnet = std::env::var_os("DOTNET_ROOT")
                .map(PathBuf::from)
                .unwrap_or_else(|| {
                    PathBuf::from(
                        std::env::var_os("ProgramFiles").unwrap_or("C:\\Program Files".into()),
                    )
                    .join("dotnet")
                });
            let mut versions: Vec<_> = std::fs::read_dir(dotnet.join("host/fxr"))
                .map_err(|e| format!(".NET 10 host not found: {e}"))?
                .filter_map(Result::ok)
                .filter(|e| e.file_name().to_string_lossy().starts_with("10."))
                .collect();
            versions.sort_by_key(|e| {
                e.file_name()
                    .to_string_lossy()
                    .split('.')
                    .map(|x| x.parse::<u32>().unwrap_or(0))
                    .collect::<Vec<_>>()
            });
            let fxr = versions
                .last()
                .ok_or("No .NET 10 hostfxr installed")?
                .path()
                .join("hostfxr.dll");
            let lib = Library::new(fxr).map_err(|e| e.to_string())?;
            let init = lib
                .get::<unsafe extern "C" fn(*const u16, *const c_void, *mut *mut c_void) -> i32>(
                    b"hostfxr_initialize_for_runtime_config\0",
                )
                .map_err(|e| e.to_string())?;
            let get = lib
                .get::<unsafe extern "C" fn(*mut c_void, i32, *mut *mut c_void) -> i32>(
                    b"hostfxr_get_runtime_delegate\0",
                )
                .map_err(|e| e.to_string())?;
            let close = lib
                .get::<unsafe extern "C" fn(*mut c_void) -> i32>(b"hostfxr_close\0")
                .map_err(|e| e.to_string())?;
            let config = wide(root.join("TomasAI.IFM.UI.Rust.Bridge.Net.runtimeconfig.json"));
            let mut context = std::ptr::null_mut();
            checked(
                init(config.as_ptr(), std::ptr::null(), &mut context),
                "hostfxr initialize",
            )?;
            let mut loader = std::ptr::null_mut();
            let result = get(context, 5, &mut loader);
            close(context);
            checked(result, "get assembly delegate")?;
            let load: unsafe extern "system" fn(
                *const u16,
                *const u16,
                *const u16,
                *const u16,
                *mut c_void,
                *mut *mut c_void,
            ) -> i32 = std::mem::transmute(loader);
            let assembly = wide(root.join("TomasAI.IFM.UI.Rust.Bridge.Net.dll"));
            let ty = wide(
                "TomasAI.IFM.UI.Rust.Bridge.Net.BridgeBootstrap, TomasAI.IFM.UI.Rust.Bridge.Net",
            );
            let method = wide("GetApi");
            let mut bootstrap = std::ptr::null_mut();
            checked(
                load(
                    assembly.as_ptr(),
                    ty.as_ptr(),
                    method.as_ptr(),
                    usize::MAX as *const u16,
                    std::ptr::null_mut(),
                    &mut bootstrap,
                ),
                "load managed bootstrap",
            )?;
            let bootstrap: unsafe extern "C" fn(u32, *mut Api, u32) -> i32 =
                std::mem::transmute(bootstrap);
            let mut extended=std::mem::MaybeUninit::<ApiV5>::uninit();
            let supported=bootstrap(5,extended.as_mut_ptr().cast(),std::mem::size_of::<ApiV5>() as u32);
            let (api,submit,copy_changed)=if supported>=0 {
                let table=extended.assume_init();
                if table.base.base.version!=5||table.base.base.size!=std::mem::size_of::<ApiV5>() as u32{return Err("ABI v5 layout mismatch".into());}
                (table.base.base,table.base.submit,Some(table.copy_changed))
            } else {
                let mut table=std::mem::MaybeUninit::<ApiV4>::uninit();
                checked(bootstrap(4,table.as_mut_ptr().cast(),std::mem::size_of::<ApiV4>() as u32),"ABI negotiation")?;
                let table=table.assume_init();
                if table.base.version!=4||table.base.size!=std::mem::size_of::<ApiV4>() as u32{return Err("ABI v4 layout mismatch".into());}
                (table.base,table.submit,None)
            };
            let mut handle = 0;
            checked((api.create)(&mut handle), "create managed session")?;
            Ok(Self {
                _library: lib,
                api,
                submit,
                copy_changed,
                handle,
            })
        }
    }
    /// Copy one complete publication into Rust-owned bytes, retrying with the required buffer size. Returns native copy errors to the caller.
    pub fn copy(&self, kind: u32) -> Result<Vec<u8>, String> {
        // SAFETY: the session and library are owned by self. The initial null
        // buffer is a size query; subsequent calls receive a live Vec buffer
        // with its full writable length. Managed code must respect that length.
        unsafe {
            let mut required = 0;
            checked(
                (self.api.copy)(self.handle, kind, std::ptr::null_mut(), 0, &mut required),
                "snapshot size",
            )?;
            for _ in 0..3 {
                if !(0..=16 * 1024 * 1024).contains(&required) {
                    return Err("Invalid publication size".into());
                }
                let mut bytes = vec![0; required as usize];
                let result = (self.api.copy)(
                    self.handle,
                    kind,
                    bytes.as_mut_ptr(),
                    bytes.len() as i32,
                    &mut required,
                );
                if result == 1 {
                    continue;
                }
                checked(result, "snapshot copy")?;
                bytes.truncate(required as usize);
                return Ok(bytes);
            }
            Err("Publication grew beyond bounded retries".into())
        }
    }
    /// Poll one publication kind into the transfer cache. Returns whether an unacknowledged update is ready; v5 reconstruction failures reset the base and trigger a full-copy retry.
    pub fn poll(&self,kind:u32,cache:&mut ifm_ui_rust::publication_transfer::TransferCache)->Result<bool,String>{
        let Some(copy)=self.copy_changed else{return Ok(cache.legacy(kind,self.copy(kind)?));};
        if kind>8{return Err("Invalid publication kind".into());}
        for attempt in 0..4 {
            let mut cursor=cache.sequence(kind);let mut required=0;let mut produced=0;let mut source=0;
            let capacity=cache.scratch.len();
            // SAFETY: output pointers reference live stack values and scratch
            // owns capacity bytes for this synchronous call. Do not resize or
            // consume scratch until the callback has returned.
            let result=unsafe{copy(self.handle,kind,&mut cursor,cache.scratch.as_mut_ptr(),capacity as i32,&mut required,&mut produced,&mut source)};
            if result==2{return Ok(cache.pending(kind));}
            checked(result,"conditional publication copy")?;
            if !(0..=16*1024*1024).contains(&required){return Err("Invalid conditional publication size".into());}
            if result==1{cache.scratch.resize(required as usize,0);continue;}
            cache.scratch.truncate(required as usize);
            match cache.complete(kind,result,cursor,produced,source){Ok(())=>return Ok(true),Err(error)=>{
                cache.reset(kind);if attempt==3{return Err(error);}
            }}
        }
        Err("Publication changed beyond bounded transfer retries".into())
    }
    /// Encode a structured intent as MessagePack and submit it to the managed session. Success means accepted by the callback, not business completion.
    pub fn submit(&self, payload: &str) -> Result<(), String> {
        let payload = ifm_ui_rust::wire::encode_intent(payload)?;
        // SAFETY: payload stays alive for the call; the managed submit contract
        // copies/consumes its bytes synchronously rather than retaining a pointer.
        unsafe {
            checked(
                (self.submit)(self.handle, payload.as_ptr(), payload.len() as i32),
                "editor intent",
            )
        }
    }
    /// Forward a numeric action to the managed session; a successful callback does not imply business completion.
    pub fn action(&self, action: u32) -> Result<(), String> {
        // SAFETY: self owns the valid session and the library backing this delegate.
        unsafe { checked((self.api.action)(self.handle, action), "fixture action") }
    }
}
impl Drop for Managed {
    /// Destroy the managed session while its host library and function pointers remain alive.
    fn drop(&mut self) {
        // SAFETY: this owner destroys its session once, before Rust drops the
        // retained Library field and invalidates the managed function pointers.
        unsafe {
            let result = (self.api.destroy)(self.handle);
            if result != 0 {
                eprintln!("Managed shutdown failed: {result}")
            }
        }
    }
}
