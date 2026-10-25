//! Atomic .NET-compatible pending-operation files. Financial validation is
//! supplied by the typed request adapter, before every read or write.
use serde_json::Value;
use std::{
    fs::{self, File, OpenOptions},
    io::Write,
    path::{Path, PathBuf},
    time::{Duration, Instant},
};

const MAX_BYTES: u64 = 2 * 1024 * 1024;
pub struct Journal {
    directory: PathBuf,
}
impl Journal {
    pub fn new(directory: impl Into<PathBuf>) -> Self {
        Self {
            directory: directory.into(),
        }
    }
    fn file(&self, operation: uuid::Uuid) -> Result<PathBuf, String> {
        if operation.is_nil() {
            return Err("OperationId is required".into());
        }
        Ok(self.directory.join(format!("{}.json", operation.simple())))
    }
    pub fn load(
        &self,
        operation: uuid::Uuid,
        validate: &impl Fn(&Value) -> Result<(), String>,
    ) -> Result<Option<Value>, String> {
        let path = self.file(operation)?;
        let metadata = match fs::metadata(&path) {
            Ok(value) => value,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(None),
            Err(error) => return Err(error.to_string()),
        };
        if metadata.len() > MAX_BYTES {
            return Err("Pending financial request exceeds its bound".into());
        }
        let bytes = fs::read(path).map_err(|e| e.to_string())?;
        if bytes.len() as u64 > MAX_BYTES {
            return Err("Pending financial request exceeds its bound".into());
        }
        let value: Value = serde_json::from_slice(&bytes).map_err(|e| e.to_string())?;
        validate(&value)?;
        if value["Request"]["OperationId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            != Some(operation)
        {
            return Err("Pending operation file identity differs from its request".into());
        }
        Ok(Some(value))
    }
    /// Call off the egui thread. `cancelled` is checked while acquiring the
    /// cross-process .NET FileShare.None lock; the lock has a three-second bound.
    pub fn save(
        &self,
        operation: uuid::Uuid,
        value: &Value,
        validate: &impl Fn(&Value) -> Result<(), String>,
        cancelled: &impl Fn() -> bool,
    ) -> Result<Value, String> {
        validate(value)?;
        fs::create_dir_all(&self.directory).map_err(|e| e.to_string())?;
        let path = self.file(operation)?;
        if value["Request"]["OperationId"]
            .as_str()
            .and_then(|s| uuid::Uuid::parse_str(s).ok())
            != Some(operation)
        {
            return Err("OperationId mismatch".into());
        }
        let lock_path = path.with_extension("json.lock");
        let _held = acquire(&lock_path, cancelled)?;
        if let Some(prior) = self.load(operation, validate)? {
            if prior["Request"]["InputSha256"] != value["Request"]["InputSha256"] {
                return Err("OperationId already belongs to another financial request".into());
            }
            if matches!(prior["Phase"].as_u64(), Some(2 | 3)) {
                return Ok(prior);
            }
        }
        let bytes = serde_json::to_vec(value).map_err(|e| e.to_string())?;
        if bytes.len() as u64 > MAX_BYTES {
            return Err("Pending financial request exceeds its bound".into());
        }
        let temporary = self.directory.join(format!(
            "{}.{}.tmp",
            operation.simple(),
            uuid::Uuid::new_v4().simple()
        ));
        let result = (|| {
            let mut output = OpenOptions::new()
                .write(true)
                .create_new(true)
                .open(&temporary)
                .map_err(|e| e.to_string())?;
            output.write_all(&bytes).map_err(|e| e.to_string())?;
            output.sync_all().map_err(|e| e.to_string())?;
            drop(output);
            if cancelled() {
                return Err("Journal write cancelled".into());
            }
            fs::rename(&temporary, &path).map_err(|e| e.to_string())?;
            Ok(value.clone())
        })();
        if temporary.exists() {
            let _ = fs::remove_file(&temporary);
        }
        result
    }
    pub fn list(
        &self,
        validate: &impl Fn(&Value) -> Result<(), String>,
    ) -> Result<Vec<Value>, String> {
        let entries = match fs::read_dir(&self.directory) {
            Ok(entries) => entries,
            Err(error) if error.kind() == std::io::ErrorKind::NotFound => return Ok(vec![]),
            Err(error) => return Err(error.to_string()),
        };
        let mut values = Vec::new();
        for entry in entries {
            let path = entry.map_err(|e| e.to_string())?.path();
            if path.extension().and_then(|s| s.to_str()) != Some("json") {
                continue;
            }
            let Some(operation) = path
                .file_stem()
                .and_then(|s| s.to_str())
                .and_then(|s| uuid::Uuid::parse_str(s).ok())
            else {
                continue;
            };
            if let Some(value) = self.load(operation, validate)? {
                values.push(value);
            }
        }
        values.sort_by(|a, b| {
            b["Request"]["RequestedAtUtc"]
                .as_str()
                .cmp(&a["Request"]["RequestedAtUtc"].as_str())
        });
        Ok(values)
    }
}
fn acquire(path: &Path, cancelled: &impl Fn() -> bool) -> Result<File, String> {
    let start = Instant::now();
    loop {
        if cancelled() {
            return Err("Journal lock cancelled".into());
        }
        let mut options = OpenOptions::new();
        options.create(true).read(true).write(true).truncate(false);
        #[cfg(windows)]
        {
            use std::os::windows::fs::OpenOptionsExt;
            options.share_mode(0);
        }
        match options.open(path) {
            Ok(file) => {
                #[cfg(not(windows))]
                if file.try_lock().is_err() {
                    if start.elapsed() >= Duration::from_secs(3) {
                        return Err("Journal lock deadline expired".into());
                    }
                    std::thread::sleep(Duration::from_millis(25));
                    continue;
                }
                return Ok(file);
            }
            Err(error) => {
                #[cfg(windows)]
                let contention = matches!(error.raw_os_error(), Some(32 | 33));
                #[cfg(not(windows))]
                let contention = error.kind() == std::io::ErrorKind::WouldBlock;
                if !contention {
                    return Err(error.to_string());
                }
                if start.elapsed() >= Duration::from_secs(3) {
                    return Err("Journal lock deadline expired".into());
                }
                std::thread::sleep(Duration::from_millis(25));
            }
        }
    }
}
