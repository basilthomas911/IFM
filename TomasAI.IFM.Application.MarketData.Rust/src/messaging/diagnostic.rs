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
    if worker == [0; 16] || value.attempt == 0 {
        return Err("Invalid diagnostic worker or attempt identity");
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
    #[ignore = "Explicit paired provider diagnostic timing"]
    fn diagnostic_encoding_before_after_timings() {
        use sha2::{Digest, Sha256};
        use std::{hint::black_box, time::Instant};
        let records: Vec<_> = (0..256).map(|at| Diagnostic {
            worker: [1; 16], lane: Lane::OptionsGlbx, attempt: 7, observed: at, replaced: 0,
            event: Event::Error { code: 199, last: true, message: format!("provider recovery {at} {}", "detail ".repeat(20)) },
        }).collect();
        let mut pairs = Vec::new();
        for pair in 0..5 {
            for before in if pair % 2 == 0 { [true, false] } else { [false, true] } {
                let mut samples = Vec::new();
                for iteration in 0..34 {
                    let started = Instant::now();
                    for record in &records {
                        let bytes = encode(record, record.worker).unwrap();
                        black_box(Sha256::digest(&bytes));
                        if before {
                            black_box(encode(record, record.worker).unwrap());
                        } else {
                            black_box(bytes);
                        }
                    }
                    if iteration >= 3 {
                        samples.push(started.elapsed().as_nanos() as u64);
                    }
                }
                samples.sort_unstable();
                pairs.push(serde_json::json!({"pair":pair,"before":before,"recordsPerSample":records.len(),
                    "p50Ns":samples[15],"p95Ns":samples[29],"p99Ns":samples[30],"samplesNs":samples}));
            }
        }
        println!("DIAGNOSTIC_ENCODING_PERFORMANCE {}", serde_json::json!(pairs));
    }

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
        let mut invalid = last.clone();
        invalid.attempt = 0;
        assert!(
            encode(&invalid, [1; 16]).is_err(),
            "The Host rejects zero attempts; do not enqueue an unacknowledgeable diagnostic"
        );
    }
}
