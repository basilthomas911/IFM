//! Native provider diagnostics, separate from market-data queues and retained
//! recovery results. Full codes/text are copied to a dedicated NATS subject.
use super::{codec, wire::guid};
use crate::dataset::{
    live::Diagnostic,
    transport::{Event, Lane},
};
use rmpv::Value;
/// Creates the fixed ten-key external logging contract. A saturated logging path
/// cannot manufacture a market-processing exception or close dataset admission.
/// The explicit replacement count makes bounded diagnostic loss observable.
///
/// # Errors
/// Non-diagnostic events, invalid timestamps and oversized text fail explicitly.
pub fn encode(value: &Diagnostic, worker: [u8; 16]) -> Result<Vec<u8>, &'static str> {
    let worker = if value.worker == [0; 16] {
        worker
    } else {
        value.worker
    };
    if worker == [0; 16] {
        return Err("Invalid diagnostic worker identity");
    }
    let (kind, code, last, message) = match &value.event {
        Event::System { code, message } => (1, *code, false, message),
        Event::Error {
            code,
            last,
            message,
        } => (2, *code, *last, message),
        _ => return Err("Non-diagnostic provider event"),
    };
    if message.len() > 4096 {
        return Err("Native diagnostic text exceeds capacity");
    }
    codec::encode(&Value::Array(vec![
        value.lane.dataset().into(),
        (if value.lane == Lane::OptionsGlbx {
            "Options"
        } else {
            "Futures"
        })
        .into(),
        guid(worker).into(),
        value.attempt.into(),
        codec::utc_offset(value.observed)?,
        kind.into(),
        code.into(),
        last.into(),
        message.clone().into(),
        value.replaced.into(),
    ]))
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::dataset::live::retain_diagnostic;
    use std::collections::VecDeque;
    #[test]
    fn bounded_log_replacement_keeps_latest_full_error_and_exact_loss_count() {
        let mut queue = VecDeque::new();
        for at in 0..1000 {
            retain_diagnostic(
                &mut queue,
                Diagnostic {
                    worker: [0; 16],
                    lane: Lane::OptionsGlbx,
                    attempt: 7,
                    observed: at,
                    replaced: 0,
                    event: Event::Error {
                        code: 199,
                        last: true,
                        message: format!("unknown provider error {at}"),
                    },
                },
            );
        }
        assert_eq!(queue.len(), 256);
        assert_eq!(queue.iter().map(|d| d.replaced).sum::<u64>(), 744);
        let last = queue.back().unwrap();
        assert_eq!(last.observed, 999);
        let wire = encode(last, [1; 16]).unwrap();
        let value = rmpv::decode::read_value(&mut wire.as_slice()).unwrap();
        let fields = value.as_array().unwrap();
        assert_eq!(fields.len(), 10);
        assert_eq!(fields[6].as_i64(), Some(199));
        assert_eq!(fields[7].as_bool(), Some(true));
        assert_eq!(fields[8].as_str(), Some("unknown provider error 999"));
        assert!(encode(last, [0; 16]).is_err());
    }
}
