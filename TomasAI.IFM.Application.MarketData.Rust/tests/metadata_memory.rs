//! Isolated allocator-instrumented large logical streams. Never constructs a huge
//! source Vec. Run this test executable under the qualification memory ceiling.
use ifm_market_data_native::{
    dataset::metadata::{INPUT_CAPACITY, Policy, Retention, decode},
    runtime::{Deadline, Interrupted},
};
use std::{
    alloc::{GlobalAlloc, Layout, System},
    pin::Pin,
    sync::atomic::{AtomicUsize, Ordering::SeqCst},
    task::{Context, Poll},
    time::Duration,
};
use tokio::io::{AsyncRead, ReadBuf};
use tokio_util::sync::CancellationToken;
struct Meter;
static LIVE: AtomicUsize = AtomicUsize::new(0);
static PEAK: AtomicUsize = AtomicUsize::new(0);
static FAIL_SIZE: AtomicUsize = AtomicUsize::new(0);
static REQUEST: AtomicUsize = AtomicUsize::new(0);
fn allocated(n: usize) {
    let live = LIVE.fetch_add(n, SeqCst) + n;
    PEAK.fetch_max(live, SeqCst);
    REQUEST.fetch_max(n, SeqCst);
}
// SAFETY: All operations forward the unchanged pointer/layout to the system
// allocator; the meter only observes successfully allocated byte counts.
unsafe impl GlobalAlloc for Meter {
    unsafe fn alloc(&self, l: Layout) -> *mut u8 {
        // SAFETY: Layout comes from the allocator caller and is forwarded unchanged.
        if FAIL_SIZE
            .compare_exchange(l.size(), 0, SeqCst, SeqCst)
            .is_ok()
        {
            return std::ptr::null_mut();
        }
        // SAFETY: The valid caller layout is forwarded unchanged.
        let p = unsafe { System.alloc(l) };
        if !p.is_null() {
            allocated(l.size());
        }
        p
    }
    unsafe fn dealloc(&self, p: *mut u8, l: Layout) {
        LIVE.fetch_sub(l.size(), SeqCst);
        // SAFETY: Pointer and layout belong to the matching system allocation.
        unsafe { System.dealloc(p, l) };
    }
    unsafe fn realloc(&self, p: *mut u8, l: Layout, n: usize) -> *mut u8 {
        // SAFETY: All arguments are forwarded from the valid allocator contract.
        let out = unsafe { System.realloc(p, l, n) };
        if !out.is_null() {
            LIVE.fetch_sub(l.size(), SeqCst);
            allocated(n);
        }
        out
    }
}
#[global_allocator]
static ALLOCATOR: Meter = Meter;

struct Generated {
    prefix: Vec<u8>,
    repeated: u64,
    at: u64,
    total: u64,
    max_read: usize,
}
impl Generated {
    fn new(count: u32) -> Self {
        // Fixed v3 header, then a repeated symbol list with maximal u16 width.
        let repeated = u64::from(count) * 65535;
        let length = 120 + repeated;
        assert!(length <= u64::from(u32::MAX));
        let mut prefix = vec![0; 116];
        prefix[..4].copy_from_slice(b"DBN\x03");
        prefix[4..8].copy_from_slice(&(length as u32).to_le_bytes());
        prefix[8..17].copy_from_slice(b"GLBX.MDP3");
        prefix[24..26].copy_from_slice(&u16::MAX.to_le_bytes());
        prefix[50] = u8::MAX;
        prefix[51] = databento::dbn::SType::InstrumentId as u8;
        prefix[53..55].copy_from_slice(&u16::MAX.to_le_bytes());
        prefix[112..116].copy_from_slice(&count.to_le_bytes());
        Self {
            prefix,
            repeated,
            at: 0,
            total: length + 8,
            max_read: 0,
        }
    }
}
impl AsyncRead for Generated {
    fn poll_read(
        mut self: Pin<&mut Self>,
        _: &mut Context<'_>,
        out: &mut ReadBuf<'_>,
    ) -> Poll<std::io::Result<()>> {
        self.max_read = self.max_read.max(out.remaining());
        if self.at == self.total {
            return Poll::Ready(Ok(()));
        }
        let start = self.at;
        let n = ((self.total - start) as usize).min(out.remaining());
        let p = self.prefix.len() as u64;
        if start < p {
            let n = n.min((p - start) as usize);
            out.put_slice(&self.prefix[start as usize..start as usize + n]);
            self.at += n as u64;
        } else if start < p + self.repeated {
            let n = n.min((p + self.repeated - start) as usize);
            let target = out.initialize_unfilled_to(n);
            target.fill(b'A');
            let offset = (start - p) % 65535;
            let mut zero = (65534 - offset) as usize;
            while zero < n {
                target[zero] = 0;
                zero += 65535;
            }
            out.advance(n);
            self.at += n as u64;
        } else {
            out.initialize_unfilled_to(n).fill(0);
            out.advance(n);
            self.at += n as u64;
        }
        Poll::Ready(Ok(()))
    }
}
impl std::io::Read for Generated {
    fn read(&mut self, bytes: &mut [u8]) -> std::io::Result<usize> {
        let mut output = ReadBuf::new(bytes);
        let mut context = Context::from_waker(std::task::Waker::noop());
        match Pin::new(self).poll_read(&mut context, &mut output) {
            Poll::Ready(result) => result.map(|()| output.filled().len()),
            Poll::Pending => unreachable!("generated source is always ready"),
        }
    }
}
#[tokio::test(flavor = "current_thread")]
async fn gigabyte_streams_use_fixed_input_and_cancel_without_resource_accumulation() {
    for count in [0, 1, 2, 1024, 16384, 65536] {
        let mut source = Generated::new(count);
        let baseline = LIVE.load(SeqCst);
        PEAK.store(baseline, SeqCst);
        REQUEST.store(0, SeqCst);
        let (metadata, stats) = decode(
            &mut source,
            Policy {
                retention: Retention::Readiness,
                ..Policy::default()
            },
        )
        .await
        .unwrap();
        let extra = PEAK.load(SeqCst).saturating_sub(baseline);
        assert_eq!(stats.input_capacity, INPUT_CAPACITY);
        assert!(stats.peak_buffered <= INPUT_CAPACITY);
        assert!(source.max_read <= INPUT_CAPACITY);
        assert!(REQUEST.load(SeqCst) <= INPUT_CAPACITY);
        assert!(extra < 128 * 1024, "peak extra={extra}");
        assert!(metadata.symbols.is_empty());
        assert_eq!(source.at, source.total);
        eprintln!(
            "logical_bytes={} input_capacity={} max_read={} peak_extra_allocated={} retained_bytes={}",
            source.total, stats.input_capacity, source.max_read, extra, stats.retained_bytes
        );
    }
    let mut maximum = Generated::new(65536);
    maximum.total = u64::from(u32::MAX) + 8;
    maximum.prefix[4..8].copy_from_slice(&u32::MAX.to_le_bytes());
    let (_, stats) = decode(
        &mut maximum,
        Policy {
            retention: Retention::Readiness,
            ..Policy::default()
        },
    )
    .await
    .unwrap();
    assert_eq!(stats.consumed, u64::from(u32::MAX) + 8);
    assert_eq!(stats.input_capacity, INPUT_CAPACITY);
    eprintln!(
        "exact_u32_boundary_bytes={} capacity={}",
        stats.consumed, stats.input_capacity
    );
    let mut synchronous = Generated::new(65536);
    let (_, stats) = databento::dbn::decode::dbn::streaming_sync::decode(
        &mut synchronous,
        databento::dbn::decode::dbn::streaming_sync::Policy {
            retention: databento::dbn::decode::dbn::streaming_sync::Retention::Readiness,
            ..Default::default()
        },
    )
    .unwrap();
    assert_eq!(stats.input_capacity, INPUT_CAPACITY);
    assert_eq!(stats.consumed, synchronous.total);
    // Deterministic fallible-allocation failures: scratch, retained vector and
    // retained string. No huge real allocation or machine OOM is attempted.
    for size in [65536, 3 * std::mem::size_of::<String>(), 65534] {
        let mut source = Generated::new(3);
        FAIL_SIZE.store(size, SeqCst);
        let result = decode(&mut source, Policy::default()).await;
        FAIL_SIZE.store(0, SeqCst);
        assert!(
            result
                .unwrap_err()
                .to_string()
                .contains("allocation failed")
        );
    }
    for _ in 0..1000 {
        let mut source = Generated::new(65536);
        let token = CancellationToken::new();
        let cancel = token.clone();
        tokio::spawn(async move {
            tokio::task::yield_now().await;
            cancel.cancel();
        });
        assert!(matches!(
            Deadline::recovery()
                .run(
                    &token,
                    decode(
                        &mut source,
                        Policy {
                            retention: Retention::Readiness,
                            ..Policy::default()
                        }
                    )
                )
                .await,
            Err(Interrupted::Cancelled)
        ));
        assert!(source.at < source.total);
    }
    let mut source = Generated::new(65536);
    assert!(matches!(
        Deadline::after(Duration::from_millis(10))
            .run(
                &CancellationToken::new(),
                decode(
                    &mut source,
                    Policy {
                        retention: Retention::Readiness,
                        ..Policy::default()
                    }
                )
            )
            .await,
        Err(Interrupted::DeadlineExpired)
    ));
}
