//! Local, bounded error evidence independent of NATS and API availability.
//! Records contain diagnostic wall-clock UTC (never a trading value date), not
//! request payloads or connection credentials. Each append is flushed to disk.
use std::{
    fs::{self, OpenOptions},
    io::{self, Read, Seek, SeekFrom, Write},
    path::{Path, PathBuf},
    sync::{Mutex, OnceLock},
};

const MAX_BYTES: u64 = 5 * 1024 * 1024;
const ARCHIVES: usize = 4;

/// Persist an error/audit record and mirror it to stderr for Server Manager.
/// Call only on error/controller paths, never normal quote delivery or painting.
/// Disk failures remain visible on stderr; they must never fail a trading call.
pub fn record(event: &str, subject: Option<&str>, operation: Option<&str>, details: &str) {
    static LOCK: OnceLock<Mutex<()>> = OnceLock::new();
    let operation = operation
        .or_else(|| correlated_operation(details))
        .map(str::to_owned)
        .unwrap_or_else(|| uuid::Uuid::new_v4().to_string());
    let parts = detail_parts(details);
    let _guard = LOCK
        .get_or_init(|| Mutex::new(()))
        .lock()
        .unwrap_or_else(|poison| poison.into_inner());
    for (index, part) in parts.iter().enumerate() {
        let value = serde_json::json!({
            "timestampUtc": chrono::DateTime::<chrono::Utc>::from(std::time::SystemTime::now()).to_rfc3339(),
            "processId": std::process::id(), "version": env!("CARGO_PKG_VERSION"),
            "event": event, "requestSubject": subject, "operationId": operation, "details": part,
            "detailPart":index+1,"detailParts":parts.len()
        });
        let line = value.to_string();
        let _ = writeln!(io::stderr().lock(), "{line}");
        if let Err(error) = log_path().and_then(|path| append(&path, line.as_bytes(), MAX_BYTES)) {
            let _ = writeln!(io::stderr().lock(), "UI durable error log write failed: {error}; originalRecord={line}");
        }
    }
}

/// Split large exception chains at UTF-8 boundaries, preserving every detail
/// and correlation field without letting one error defeat the file-size cap.
fn detail_parts(mut details: &str) -> Vec<&str> {
    let mut parts = Vec::new();
    while details.len() > 64 * 1024 {
        let mut end = 64 * 1024;
        while !details.is_char_boundary(end) {
            end -= 1;
        }
        parts.push(&details[..end]);
        details = &details[end..];
    }
    parts.push(details);
    parts
}

fn log_path() -> io::Result<PathBuf> {
    let root = std::env::var_os("LOCALAPPDATA")
        .ok_or_else(|| io::Error::new(io::ErrorKind::NotFound, "LOCALAPPDATA is unavailable"))?;
    // The supported single UI uses the same bounded set across application restarts.
    Ok(PathBuf::from(root).join("TomasAI/IFM/UI.Rust.eGui/Logs/ui-errors.jsonl"))
}

fn archive(path: &Path, index: usize) -> PathBuf {
    PathBuf::from(format!("{}.{}", path.display(), index))
}

/// Preserve transport correlation when a controller wraps its error for display.
fn correlated_operation(details: &str) -> Option<&str> {
    let id = details.rsplit_once("operationId=")?.1.get(..36)?;
    uuid::Uuid::parse_str(id).ok().map(|_| id)
}

/// A hard process termination may interrupt the final append. Retain complete
/// prior records and discard only the unfinished tail before writing new JSON.
fn repair_tail(path: &Path) -> io::Result<()> {
    let mut file = match OpenOptions::new().read(true).write(true).open(path) {
        Ok(file) => file,
        Err(error) if error.kind() == io::ErrorKind::NotFound => return Ok(()),
        Err(error) => return Err(error),
    };
    let mut end = file.metadata()?.len();
    let mut buffer = [0u8; 4096];
    while end > 0 {
        let start = end.saturating_sub(buffer.len() as u64);
        let length = (end - start) as usize;
        file.seek(SeekFrom::Start(start))?;
        file.read_exact(&mut buffer[..length])?;
        if let Some(index) = buffer[..length].iter().rposition(|byte| *byte == b'\n') {
            file.set_len(start + index as u64 + 1)?;
            return Ok(());
        }
        end = start;
    }
    file.set_len(0)
}

fn append(path: &Path, line: &[u8], max_bytes: u64) -> io::Result<()> {
    // A malformed/oversized error cannot defeat the file-size bound. Preserve
    // its identity and explain truncation, rather than writing invalid JSON.
    let oversized;
    let line = if line.len() as u64 + 1 > max_bytes {
        oversized = serde_json::json!({"event":"ui.error_record_oversized", "originalBytes":line.len(), "details":"Record exceeded the log-file capacity; full details were emitted to stderr"}).to_string();
        oversized.as_bytes()
    } else {
        line
    };
    fs::create_dir_all(
        path.parent()
            .ok_or_else(|| io::Error::other("Missing log parent"))?,
    )?;
    repair_tail(path)?;
    if fs::metadata(path).map(|meta| meta.len()).unwrap_or(0) + line.len() as u64 + 1 > max_bytes {
        for index in (1..=ARCHIVES).rev() {
            let destination = archive(path, index);
            if destination.exists() {
                fs::remove_file(&destination)?;
            }
            let source = if index == 1 {
                path.to_path_buf()
            } else {
                archive(path, index - 1)
            };
            if source.exists() {
                fs::rename(source, destination)?;
            }
        }
    }
    let mut file = OpenOptions::new().create(true).append(true).open(path)?;
    file.write_all(line)?;
    file.write_all(b"\n")?;
    file.sync_data()
}

/// Add request context without losing the full underlying failure chain.
pub fn request_failure(subject: &str, operation: &str, details: String) -> String {
    record(
        "backend.request_failed",
        Some(subject),
        Some(operation),
        &details,
    );
    format!("{details} [subject={subject}; operationId={operation}]")
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rotation_preserves_multiline_json_and_bounds_all_files() {
        let directory =
            std::env::temp_dir().join(format!("ifm-error-log-{}", uuid::Uuid::new_v4()));
        let path = directory.join("errors.jsonl");
        for index in 0..60 {
            let line = serde_json::json!({"operationId":index, "details":"failure\ncaused by: socket closed"}).to_string();
            append(&path, line.as_bytes(), 512).unwrap();
        }
        let files: Vec<_> = fs::read_dir(&directory).unwrap().collect();
        assert_eq!(files.len(), ARCHIVES + 1);
        for file in files {
            let path = file.unwrap().path();
            assert!(fs::metadata(&path).unwrap().len() <= 512);
            for line in fs::read_to_string(path).unwrap().lines() {
                serde_json::from_str::<serde_json::Value>(line).unwrap();
            }
        }
        assert!(fs::read_to_string(&path).unwrap().contains("59"));
        fs::remove_dir_all(directory).unwrap();
    }
    #[test]
    fn disk_errors_are_returned_and_oversized_records_remain_valid_json() {
        let directory =
            std::env::temp_dir().join(format!("ifm-error-log-{}", uuid::Uuid::new_v4()));
        fs::create_dir_all(&directory).unwrap();
        let path = directory.join("errors.jsonl");
        append(&path, &vec![b'x'; 2048], 512).unwrap();
        let value: serde_json::Value =
            serde_json::from_str(&fs::read_to_string(&path).unwrap()).unwrap();
        assert_eq!(value["event"], "ui.error_record_oversized");
        assert!(append(&directory, b"error", 512).is_err());
        fs::remove_dir_all(directory).unwrap();
    }
    #[test]
    fn restart_repairs_only_incomplete_tail_and_preserves_request_identity() {
        let directory =
            std::env::temp_dir().join(format!("ifm-error-log-{}", uuid::Uuid::new_v4()));
        fs::create_dir_all(&directory).unwrap();
        let path = directory.join("errors.jsonl");
        fs::write(&path, b"{\"complete\":true}\n{\"incomplete\":").unwrap();
        append(&path, b"{\"next\":true}", 512).unwrap();
        assert_eq!(
            fs::read_to_string(&path).unwrap(),
            "{\"complete\":true}\n{\"next\":true}\n"
        );
        let id = uuid::Uuid::new_v4().to_string();
        assert_eq!(
            correlated_operation(&format!(
                "wrapped [subject=Query.Test.Read.x; operationId={id}]"
            )),
            Some(id.as_str())
        );
        assert_eq!(correlated_operation("operationId=not-a-guid"), None);
        fs::remove_dir_all(directory).unwrap();
    }
    #[test]
    fn huge_unicode_error_chains_are_preserved_in_bounded_parts() {
        let details = "failure 🦀\ncaused by: \"invalid data\"\n".repeat(80_000);
        let parts = detail_parts(&details);
        assert!(parts.len() > 1);
        assert!(parts.iter().all(|part| part.len() <= 64 * 1024));
        assert_eq!(parts.concat(), details);
    }
}
