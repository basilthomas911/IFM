//! Count actual American scratch reservations across bisection and Greek bumps.
use ifm_market_data_native::{
    pricing::{Calculator, Request, Settings},
    runtime::Deadline,
};
use std::{
    alloc::{GlobalAlloc, Layout, System},
    sync::atomic::{AtomicUsize, Ordering},
};
use tokio_util::sync::CancellationToken;
struct Counted;
static TREE_ALLOCATIONS: AtomicUsize = AtomicUsize::new(0);
// SAFETY: All allocations and deallocations preserve System's original pointer,
// layout and ownership. The counter observes size only and never touches memory.
unsafe impl GlobalAlloc for Counted {
    unsafe fn alloc(&self, layout: Layout) -> *mut u8 {
        if layout.size() == 802 * 8 {
            TREE_ALLOCATIONS.fetch_add(1, Ordering::Relaxed);
        }
        // SAFETY: The allocator caller supplies a valid allocation layout.
        unsafe { System.alloc(layout) }
    }
    unsafe fn dealloc(&self, pointer: *mut u8, layout: Layout) {
        // SAFETY: Caller returns the original pointer/layout allocated above.
        unsafe { System.dealloc(pointer, layout) }
    }
}
#[global_allocator]
static ALLOCATOR: Counted = Counted;
#[tokio::test]
async fn american_solver_and_risk_reuse_operation_scratch_instead_of_allocating_per_tree() {
    let calc = Calculator::new(Settings::default()).unwrap();
    let token = CancellationToken::new();
    let request = Request {
        forward: 6500.,
        strike: 6500.,
        time: 0.1,
        rate: 0.04,
        call: false,
        american: true,
        futures_style: false,
    };
    let before = TREE_ALLOCATIONS.load(Ordering::Relaxed);
    let result = calc
        .implied(request, 80., &token, Deadline::recovery())
        .await
        .unwrap();
    assert!(result.volatility > 0.);
    let allocations = TREE_ALLOCATIONS.load(Ordering::Relaxed) - before;
    assert_eq!(
        allocations, 2,
        "one owned buffer for IV bisection, one for all full-Greek repricings"
    );
    let before = TREE_ALLOCATIONS.load(Ordering::Relaxed);
    calc.solve(request, 80., &token, Deadline::recovery())
        .await
        .unwrap();
    assert_eq!(TREE_ALLOCATIONS.load(Ordering::Relaxed) - before, 1);
}
