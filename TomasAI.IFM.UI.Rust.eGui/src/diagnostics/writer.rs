//! Bounded writer for non-durable timing diagnostics. Command/error journals
//! retain their independent synchronous persistence guarantees.
use std::{
    fs::{self, File, OpenOptions},
    io::{self, BufWriter, Write},
    path::PathBuf,
    sync::{
        Arc, Mutex, OnceLock,
        atomic::{AtomicU64, Ordering},
        mpsc,
    },
    thread::JoinHandle,
    time::Duration,
};

/// Message used by the writer component.
enum Message {
    Line(String),
    Stop(mpsc::Sender<io::Result<()>>),
}

/// Diagnostic Writer used by the writer component.
pub struct DiagnosticWriter {
    /// Tx associated with this diagnostic writer.
    tx: mpsc::SyncSender<Message>,
    /// Job associated with this diagnostic writer.
    job: Mutex<Option<JoinHandle<()>>>,
    /// Rejected associated with this diagnostic writer.
    rejected: AtomicU64,
    /// Failures associated with this diagnostic writer.
    failures: Arc<AtomicU64>,
}

/// File State used by the writer component.
struct FileState {
    /// Path associated with this file state.
    path: PathBuf,
    /// Max associated with this file state.
    max: u64,
    /// Size associated with this file state.
    size: u64,
    /// Optional file; `None` represents an absent value.
    file: Option<BufWriter<File>>,
}
impl FileState {
    /// Flushes pending transport or buffered output before returning.
    fn flush(&mut self) -> io::Result<()> {
        if let Some(f) = self.file.as_mut() {
            f.flush()?;
        }
        Ok(())
    }


    /// Appends writer for file state.
    fn append(&mut self, line: &str) -> io::Result<()> {
        if line.len() as u64 + 1 > self.max {
            return Err(io::Error::other(
                "Timing record exceeds bounded file capacity",
            ));
        }
        if self.file.is_none() {
            fs::create_dir_all(
                self.path
                    .parent()
                    .ok_or_else(|| io::Error::other("Missing log directory"))?,
            )?;
            self.size = fs::metadata(&self.path).map(|m| m.len()).unwrap_or(0);
            self.file = Some(BufWriter::new(
                OpenOptions::new()
                    .create(true)
                    .append(true)
                    .open(&self.path)?,
            ));
        }
        if self.size + line.len() as u64 + 1 > self.max {
            self.flush()?;
            self.file.take();
            let previous = self.path.with_extension("previous.jsonl");
            match fs::remove_file(&previous) {
                Ok(()) => {}
                Err(e) if e.kind() == io::ErrorKind::NotFound => {}
                Err(e) => return Err(e),
            }
            fs::rename(&self.path, previous)?;
            self.size = 0;
            self.file = Some(BufWriter::new(
                OpenOptions::new()
                    .create(true)
                    .append(true)
                    .open(&self.path)?,
            ));
        }
        let f = self.file.as_mut().unwrap();
        f.write_all(line.as_bytes())?;
        f.write_all(b"\n")?;
        self.size += line.len() as u64 + 1;
        Ok(())
    }
}
impl DiagnosticWriter {
    /// Constructs DiagnosticWriter from the supplied inputs.
    pub fn new(path: PathBuf, capacity: usize, max_bytes: u64) -> io::Result<Self> {
        let (tx, rx) = mpsc::sync_channel(capacity);
        let failures = Arc::new(AtomicU64::new(0));
        let worker_failures = failures.clone();
        let job = std::thread::Builder::new()
            .name("ifm-timing-log".into())
            .spawn(move || {
                let mut state = FileState {
                    path,
                    max: max_bytes,
                    size: 0,
                    file: None,
                };
                loop {
                    match rx.recv_timeout(Duration::from_millis(100)) {
                        Ok(Message::Line(line)) => {
                            if let Err(e) = state.append(&line) {
                                worker_failures.fetch_add(1, Ordering::Relaxed);
                                eprintln!("Timing diagnostic write failed: {e}");
                                state.file.take();
                            }
                        }
                        Ok(Message::Stop(ack)) => {
                            let _ = ack.send(state.flush());
                            break;
                        }
                        Err(mpsc::RecvTimeoutError::Timeout) => {
                            if let Err(e) = state.flush() {
                                worker_failures.fetch_add(1, Ordering::Relaxed);
                                eprintln!("Timing diagnostic flush failed: {e}");
                                state.file.take();
                            }
                        }
                        Err(mpsc::RecvTimeoutError::Disconnected) => {
                            let _ = state.flush();
                            break;
                        }
                    }
                }
            })?;
        Ok(Self {
            tx,
            job: Mutex::new(Some(job)),
            rejected: AtomicU64::new(0),
            failures,
        })
    }


    /// Offers a diagnostic record to the bounded writer queue without waiting for disk I/O.
    pub fn offer(&self, line: String) -> bool {
        if line.len() > 256 * 1024 || self.tx.try_send(Message::Line(line)).is_err() {
            let count = self.rejected.fetch_add(1, Ordering::Relaxed) + 1;
            eprintln!(
                "Timing diagnostic queue unavailable or record too large; rejectedRecords={count}; original record was emitted to stderr"
            );
            false
        } else {
            true
        }
    }


    /// Returns the count of diagnostic records rejected by the writer.
    pub fn rejected(&self) -> u64 {
        self.rejected.load(Ordering::Relaxed)
    }


    /// Requests shutdown and waits for owned work to finish within its configured bounds.
    pub fn shutdown(&self, timeout: Duration) -> io::Result<()> {
        let mut job = self
            .job
            .lock()
            .map_err(|_| io::Error::other("Timing writer lock poisoned"))?;
        if job.is_none() {
            return Ok(());
        }
        let (tx, rx) = mpsc::channel();
        let started = std::time::Instant::now();
        let mut message = Message::Stop(tx);
        loop {
            match self.tx.try_send(message) {
                Ok(()) => break,
                Err(mpsc::TrySendError::Disconnected(_)) => {
                    return Err(io::Error::other("Timing writer disconnected"));
                }
                Err(mpsc::TrySendError::Full(m)) => {
                    message = m;
                    if started.elapsed() >= timeout {
                        return Err(io::Error::new(
                            io::ErrorKind::TimedOut,
                            "Timing writer shutdown queue remained full",
                        ));
                    }
                    std::thread::sleep(Duration::from_millis(1));
                }
            }
        }
        let flushed = rx
            .recv_timeout(timeout.saturating_sub(started.elapsed()))
            .map_err(|e| io::Error::other(e.to_string()))?;
        if let Some(job) = job.take() {
            job.join()
                .map_err(|_| io::Error::other("Timing writer panicked"))?;
        }
        flushed?;
        if self.failures.load(Ordering::Relaxed) > 0 {
            return Err(io::Error::other(
                "Timing diagnostic writes failed; see stderr",
            ));
        }
        Ok(())
    }
}
static TIMINGS: OnceLock<Option<DiagnosticWriter>> = OnceLock::new();


/// Provides timing for the writer implementation.
pub fn timing(line: String) {
    if let Some(writer) = TIMINGS.get_or_init(|| {
        let base = std::env::var_os("LOCALAPPDATA")?;
        let path = PathBuf::from(base)
            .join("TomasAI/IFM/UI.Rust.eGui/Logs")
            .join(format!("trade-load-{}.jsonl", std::process::id()));
        match DiagnosticWriter::new(path, 64, 5 * 1024 * 1024) {
            Ok(w) => Some(w),
            Err(e) => {
                eprintln!("Timing diagnostic worker could not start: {e}");
                None
            }
        }
    }) {
        writer.offer(line);
    }
}


/// Requests shutdown and waits for owned work to finish within its configured bounds.
pub fn shutdown() {
    if let Some(Some(writer)) = TIMINGS.get() {
        if let Err(e) = writer.shutdown(Duration::from_secs(1)) {
            eprintln!("Timing diagnostic shutdown failed: {e}");
        }
    }
}
#[cfg(test)]
mod tests {
    use super::*;


    /// Verifies that queued records flush rotate and join on shutdown.
    #[test]
    fn queued_records_flush_rotate_and_join_on_shutdown() {
        let dir = std::env::temp_dir().join(format!("ifm-timing-{}", uuid::Uuid::new_v4()));
        let path = dir.join("timing.jsonl");
        let writer = DiagnosticWriter::new(path.clone(), 128, 128).unwrap();
        for i in 0..50 {
            assert!(writer.offer(format!("{{\"index\":{i}}}")));
        }
        writer.shutdown(Duration::from_secs(2)).unwrap();
        assert!(fs::read_to_string(&path).unwrap().contains("49"));
        assert!(fs::read_dir(&dir).unwrap().count() <= 2);
        for p in fs::read_dir(&dir).unwrap() {
            let p = p.unwrap().path();
            assert!(fs::metadata(&p).unwrap().len() <= 128);
            for line in fs::read_to_string(p).unwrap().lines() {
                serde_json::from_str::<serde_json::Value>(line).unwrap();
            }
        }
        assert!(!writer.offer("closed".into()));
        assert_eq!(writer.rejected(), 1);
        fs::remove_dir_all(dir).unwrap();
    }


    /// Verifies that disk failure is returned on flush and shutdown is bounded.
    #[test]
    fn disk_failure_is_returned_on_flush_and_shutdown_is_bounded() {
        let dir = std::env::temp_dir().join(format!("ifm-timing-{}", uuid::Uuid::new_v4()));
        fs::create_dir_all(&dir).unwrap();
        let writer = DiagnosticWriter::new(dir.clone(), 1, 128).unwrap();
        assert!(writer.offer("disk-failure".into()));
        assert!(!writer.offer("x".repeat(256 * 1024 + 1)));
        assert_eq!(writer.rejected(), 1);
        assert!(writer.shutdown(Duration::from_secs(2)).is_err());
        assert!(writer.job.lock().unwrap().is_none());
        fs::remove_dir(dir).unwrap();
    }
}
