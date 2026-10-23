use std::sync::Mutex;

type Callback = unsafe extern "C" fn(i32, i32, i32, i32, *const u8, u32);
static CALLBACK: Mutex<Option<Callback>> = Mutex::new(None);

/// Process-lifetime callback; native threads never retain the supplied message buffer.
#[unsafe(no_mangle)]
pub extern "C" fn dbf_set_diagnostic_callback(callback: Option<Callback>) {
    *CALLBACK.lock().unwrap_or_else(|e| e.into_inner()) = callback;
}

pub fn report(kind: i32, code: i32, fatal: bool, is_last: bool, message: &str) {
    let callback = *CALLBACK.lock().unwrap_or_else(|e| e.into_inner());
    if let Some(callback) = callback {
        unsafe { callback(kind, code, i32::from(fatal), i32::from(is_last), message.as_ptr(), message.len() as u32) };
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    static OBSERVED: Mutex<Vec<(i32, i32, String)>> = Mutex::new(Vec::new());
    unsafe extern "C" fn capture(kind: i32, code: i32, _: i32, _: i32, text: *const u8, len: u32) {
        let text = unsafe { std::slice::from_raw_parts(text, len as usize) };
        OBSERVED.lock().unwrap().push((kind, code, String::from_utf8_lossy(text).into()));
    }
    #[test]
    fn preserves_error_warning_unknown_code_and_full_text() {
        dbf_set_diagnostic_callback(Some(capture));
        report(1, 8, true, true, "Replay unavailable: original detail");
        report(2, 2, false, true, "Reader is slow");
        report(1, 199, true, false, "Unknown future code");
        dbf_set_diagnostic_callback(None);
        let observed = OBSERVED.lock().unwrap();
        assert_eq!(observed.len(), 3);
        assert_eq!(observed[0].2, "Replay unavailable: original detail");
        assert_eq!(observed[1].0, 2);
        assert_eq!(observed[2].1, 199);
    }
}
