use libloading::Library;
use std::{
    ffi::c_void,
    path::{Path, PathBuf},
};
#[repr(C)]
#[derive(Clone, Copy)]
pub struct Api {
    pub version: u32,
    pub size: u32,
    pub create: unsafe extern "C" fn(*mut u64) -> i32,
    pub copy: unsafe extern "C" fn(u64, u32, *mut u8, i32, *mut i32) -> i32,
    pub action: unsafe extern "C" fn(u64, u32) -> i32,
    pub destroy: unsafe extern "C" fn(u64) -> i32,
}
#[repr(C)]
#[derive(Clone, Copy)]
pub struct ApiV2 {
    pub base: Api,
    pub submit: unsafe extern "C" fn(u64, *const u8, i32) -> i32,
}
pub struct Managed {
    _library: Library,
    api: Api,
    submit: unsafe extern "C" fn(u64, *const u8, i32) -> i32,
    handle: u64,
}
fn wide(s: impl AsRef<std::ffi::OsStr>) -> Vec<u16> {
    use std::os::windows::ffi::OsStrExt;
    s.as_ref().encode_wide().chain(Some(0)).collect()
}
fn checked(code: i32, what: &str) -> Result<(), String> {
    if code < 0 {
        Err(format!("{what}: 0x{code:08x}"))
    } else {
        Ok(())
    }
}
impl Managed {
    pub fn load(root: &Path) -> Result<Self, String> {
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
            let mut table = std::mem::MaybeUninit::<ApiV2>::uninit();
            checked(
                bootstrap(
                    2,
                    table.as_mut_ptr().cast(),
                    std::mem::size_of::<ApiV2>() as u32,
                ),
                "ABI negotiation",
            )?;
            let table = table.assume_init();
            let api = table.base;
            if api.version != 2 || api.size != std::mem::size_of::<ApiV2>() as u32 {
                return Err("ABI layout mismatch".into());
            }
            let mut handle = 0;
            checked((api.create)(&mut handle), "create managed session")?;
            Ok(Self {
                _library: lib,
                api,
                submit: table.submit,
                handle,
            })
        }
    }
    pub fn copy(&self, kind: u32) -> Result<Vec<u8>, String> {
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
    pub fn submit(&self, payload: &str) -> Result<(), String> {
        unsafe {
            checked(
                (self.submit)(self.handle, payload.as_ptr(), payload.len() as i32),
                "editor intent",
            )
        }
    }
    pub fn action(&self, action: u32) -> Result<(), String> {
        unsafe { checked((self.api.action)(self.handle, action), "fixture action") }
    }
}
impl Drop for Managed {
    fn drop(&mut self) {
        unsafe {
            let result = (self.api.destroy)(self.handle);
            if result != 0 {
                eprintln!("Managed shutdown failed: {result}")
            }
        }
    }
}
