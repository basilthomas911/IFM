//! Nonblocking panic capture for the native DLL. The default Rust hook writes to
//! stderr synchronously; a Host with a stalled redirected pipe could otherwise
//! block a Tokio worker before catch_unwind can release its dataset resources.
//! Capture uses fixed storage and try_lock only. Normal native task owners still
//! classify the unwind and drive recovery; this hook is not recovery authority.
use std::sync::{
    Mutex, Once,
    atomic::{AtomicU64, Ordering},
};
const CAPACITY: usize = 16;
#[derive(Clone, Copy)]
/// Copied bounded native panic evidence. It owns no task, socket or lock.
pub struct Record {
    ordinal: u64,
    message: [u8; 4096],
    length: usize,
    file: [u8; 512],
    file_length: usize,
    line: u32,
}
impl Record {
    const EMPTY: Self = Self {
        ordinal: 0,
        message: [0; 4096],
        length: 0,
        file: [0; 512],
        file_length: 0,
        line: 0,
    };
    /// Private raw evidence. Consumers must redact configured credentials before
    /// forwarding this string; the hook never publishes it or calls managed code.
    pub fn message(&self) -> &str {
        std::str::from_utf8(&self.message[..self.length]).unwrap_or("Invalid panic text")
    }
    /// Source file captured without formatting or IO.
    pub fn file(&self) -> &str {
        std::str::from_utf8(&self.file[..self.file_length]).unwrap_or("Invalid panic location")
    }
    /// Source line, or zero when the panic has no location.
    pub fn line(&self) -> u32 {
        self.line
    }
}
struct State {
    records: [Record; CAPACITY],
    next: usize,
}
static STATE: Mutex<State> = Mutex::new(State {
    records: [Record::EMPTY; CAPACITY],
    next: 0,
});
static INSTALLED: Once = Once::new();
static TOTAL: AtomicU64 = AtomicU64::new(0);
static LOST: AtomicU64 = AtomicU64::new(0);
fn bounded(source: &str, maximum: usize) -> usize {
    let mut n = source.len().min(maximum);
    while !source.is_char_boundary(n) {
        n -= 1;
    }
    n
}
/// Installs the native DLL hook once, during ordinary runtime initialization.
/// No previous stderr hook is called from a panicking task.
pub fn install() {
    INSTALLED.call_once(|| {
        std::panic::set_hook(Box::new(|info| {
            let ordinal = TOTAL.fetch_add(1, Ordering::Relaxed).saturating_add(1);
            let message = info
                .payload()
                .downcast_ref::<&str>()
                .copied()
                .or_else(|| info.payload().downcast_ref::<String>().map(String::as_str))
                .unwrap_or("Non-string native panic");
            let Ok(mut state) = STATE.try_lock() else {
                LOST.fetch_add(1, Ordering::Relaxed);
                return;
            };
            let index = state.next;
            let target = &mut state.records[index];
            *target = Record::EMPTY;
            target.ordinal = ordinal;
            target.length = bounded(message, target.message.len());
            target.message[..target.length].copy_from_slice(&message.as_bytes()[..target.length]);
            if let Some(location) = info.location() {
                target.file_length = bounded(location.file(), target.file.len());
                target.file[..target.file_length]
                    .copy_from_slice(&location.file().as_bytes()[..target.file_length]);
                target.line = location.line();
            }
            state.next = (index + 1) % CAPACITY;
        }))
    });
}
/// Explicit diagnostic query only. No timer, callback, IO or task is created.
/// Reading returns copied bounded evidence and never holds the panic mutex while
/// a broker, logger or consumer processes it.
pub fn capture() -> (u64, u64, Vec<Record>) {
    let total = TOTAL.load(Ordering::Relaxed);
    let lost = LOST.load(Ordering::Relaxed);
    let mut records = STATE
        .try_lock()
        .map(|state| {
            state
                .records
                .iter()
                .filter(|r| r.ordinal != 0)
                .copied()
                .collect::<Vec<_>>()
        })
        .unwrap_or_default();
    records.sort_by_key(|r| r.ordinal);
    (total, lost, records)
}
#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn native_hook_is_bounded_and_never_waits_for_a_stalled_consumer() {
        install();
        let before = TOTAL.load(Ordering::Relaxed);
        let original = "native capture boundary ".to_owned() + &"\u{1f980}".repeat(2048);
        // Other fault-injection unit tests panic concurrently. The hook is
        // intentionally process-wide and may drop a record on try_lock failure;
        // neither the global last record nor any one attempt belongs to us.
        let mut own = None;
        for _ in 0..100 {
            assert!(std::panic::catch_unwind(|| panic!("{original}")).is_err());
            let (total, _, records) = capture();
            assert!(total > before);
            own = records
                .into_iter()
                .find(|record| record.message().starts_with("native capture boundary"));
            if own.is_some() {
                break;
            }
            std::thread::yield_now();
        }
        let own = own.expect("bounded retry should observe this test's panic record");
        assert!(own.message().len() <= 4096);
        assert!(own.file().ends_with("panic_capture.rs"));
        assert!(own.line() > 0);
        let locked = STATE.lock().unwrap();
        let lost = LOST.load(Ordering::Relaxed);
        assert!(
            std::panic::catch_unwind(|| panic!("consumer deliberately holds evidence storage"))
                .is_err()
        );
        assert!(LOST.load(Ordering::Relaxed) > lost);
        drop(locked);
        for _ in 0..64 {
            assert!(std::panic::catch_unwind(|| panic!("bounded panic corpus")).is_err());
        }
        assert_eq!(capture().2.len(), CAPACITY);
    }
}
