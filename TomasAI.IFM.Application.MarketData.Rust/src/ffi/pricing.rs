//! Owned fixed-width pricing requests/results. Caller buffers are copied only
//! during ABI calls; cancellation/unwind marks the retained slot ready for release.
//! Results retain separate bounded reservations until copied, so completion drain
//! cannot free memory before managed delivery has read the complete Greek vector.
use super::runtime::{Status, boundary, instance, valid};
use crate::{
    pricing::{Calculator, EuropeanKernel, Failure, Request, Settings, Values},
    runtime::owner::{OPERATION_CAPACITY, Outcome, foreign_deadline},
};
use std::{
    collections::BTreeMap,
    sync::{Arc, Mutex},
};
use tokio_util::sync::CancellationToken;

/// Validates hot quote inputs after immutable reference/calendar qualification.
/// No allocation, runtime task, provider I/O or pricing calculation is performed.
/// Malformed arguments leave output unchanged; financial failures are results.
/// # Safety
/// Input/output point to aligned, non-overlapping valid 304/16-byte regions.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_qualify_quotes_v1(
    input: *const crate::option_chain::qualification::Request,
    output: *mut crate::option_chain::qualification::Result,
) -> i32 {
    boundary(|| {
        if !valid(input.cast_mut()) || !valid(output) {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Caller-readable aligned input; no borrow survives the call.
        let input = unsafe { input.read() };
        if input.size != 304 {
            return Err(Status::AbiMismatch);
        }
        let value =
            crate::option_chain::qualification::validate(&input).ok_or(Status::InvalidInput)?;
        // SAFETY: Validated aligned output, exclusively writable by the caller.
        unsafe { output.write(value) };
        Ok(())
    })
}

/// Version-one fixed input. Only integer flags cross FFI, never Rust bool/enums.
#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct NativePricingRequest {
    /// DTO size, 80 bytes.
    pub size: u32,
    /// 0 theoretical, 1 full Greeks, 2 IV/Greeks, 3 price/delta, 4 IV-only.
    pub action: u32,
    /// Bit 0 call, bit 1 American, bit 2 futures-style premium; no other bits.
    pub flags: u32,
    /// 0 managed-compatible European arithmetic, 1 existing Rust arithmetic.
    pub profile: u32,
    /// Futures price.
    pub forward: f64,
    /// Option strike.
    pub strike: f64,
    /// Captured year fraction.
    pub time: f64,
    /// Captured continuous rate.
    pub rate: f64,
    /// Volatility for actions 0/1/3, observed mark for actions 2/4.
    pub input: f64,
    /// CRR tree steps.
    pub steps: u32,
    /// IV iterations.
    pub iterations: u32,
    /// IV maximum volatility.
    pub maximum_volatility: f64,
    /// IV tolerance.
    pub tolerance: f64,
}
/// Complete fixed 64-byte output. Error leaves all numerical fields zero.
#[repr(C)]
#[derive(Debug, Clone, Copy, Default)]
pub struct NativePricingResult {
    /// DTO size, 64 bytes.
    pub size: u32,
    /// Stable numerical error: 0 success; see failure_code for mapping.
    pub failure: u32,
    /// Model price.
    pub price: f64,
    /// Volatility.
    pub volatility: f64,
    /// Forward delta.
    pub delta: f64,
    /// Forward gamma.
    pub gamma: f64,
    /// Unit vega.
    pub vega: f64,
    /// Annual theta.
    pub theta: f64,
    /// Unit rho.
    pub rho: f64,
}
pub(super) fn failure_code(f: Failure) -> u32 {
    match f {
        Failure::InvalidInput => 1,
        Failure::Expired => 2,
        Failure::UndefinedGreeks => 3,
        Failure::PriceOutOfBounds => 4,
        Failure::ImpliedVolatilityNotIdentifiable => 5,
        Failure::VolatilityNotBracketed => 6,
        Failure::NonConvergence => 7,
        Failure::NumericalFailure => 8,
        Failure::Interrupted(_) => 9,
        Failure::Capacity => 10,
    }
}
struct Reservation {
    slots: Arc<Mutex<BTreeMap<u64, Option<NativePricingResult>>>>,
    operation: u64,
    result: NativePricingResult,
}
impl Drop for Reservation {
    fn drop(&mut self) {
        if let Ok(mut slots) = self.slots.lock()
            && let Some(slot) = slots.get_mut(&self.operation)
        {
            *slot = Some(self.result);
        }
    }
}
impl From<Values> for NativePricingResult {
    fn from(v: Values) -> Self {
        Self {
            size: 64,
            failure: 0,
            price: v.price,
            volatility: v.volatility,
            delta: v.delta,
            gamma: v.gamma,
            vega: v.vega,
            theta: v.theta,
            rho: v.rho,
        }
    }
}

/// Admits an owned async calculation under the caller's remaining deadline.
/// Validation/admission errors consume neither operation ID nor output slot.
///
/// # Safety
/// request must point to one aligned readable DTO throughout this call. It is
/// copied before return and no caller memory is retained by the native future.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_submit_pricing_v1(
    handle: u64,
    operation: u64,
    request: *const NativePricingRequest,
    budget_ms: u64,
) -> i32 {
    boundary(|| {
        if !valid(request.cast_mut()) {
            return Err(Status::InvalidInput);
        }
        // SAFETY: Alignment validated; caller guarantees a readable DTO for this call.
        let request = unsafe { request.read() };
        if request.size != 80 {
            return Err(Status::AbiMismatch);
        }
        if request.action > 4 || request.flags > 7 || request.profile > 1 {
            return Err(Status::InvalidInput);
        }
        let calculator = Calculator::with_kernel(
            Settings {
                steps: request.steps as usize,
                iterations: request.iterations as usize,
                maximum_volatility: request.maximum_volatility,
                tolerance: request.tolerance,
            },
            if request.profile == 0 {
                EuropeanKernel::ManagedCompatibility
            } else {
                EuropeanKernel::LegacyRustCompatibility
            },
        )
        .map_err(|_| Status::InvalidInput)?;
        let instance = instance(handle)?;
        let controls = instance
            .control
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if controls.contains_key(&operation) {
            return Err(Status::InvalidInput);
        }
        let deadline = foreign_deadline(budget_ms);
        let mut slots = instance
            .pricing
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        if operation == 0 || slots.contains_key(&operation) {
            return Err(Status::InvalidInput);
        }
        if slots.len() >= OPERATION_CAPACITY {
            return Err(Status::Capacity);
        }
        slots.insert(operation, None);
        // Construct the Drop guard before scheduling: even an already-cancelled
        // future which is never polled must release its result into a readable slot.
        let mut reservation = Reservation {
            slots: instance.pricing.clone(),
            operation,
            result: NativePricingResult {
                size: 64,
                failure: 9,
                ..Default::default()
            },
        };
        let work = async move {
            let r = Request {
                forward: request.forward,
                strike: request.strike,
                time: request.time,
                rate: request.rate,
                call: request.flags & 1 != 0,
                american: request.flags & 2 != 0,
                futures_style: request.flags & 4 != 0,
            };
            let token = CancellationToken::new();
            let result = match request.action {
                0 => calculator
                    .theoretical(r, request.input, &token, deadline)
                    .await
                    .map(|price| NativePricingResult {
                        size: 64,
                        price,
                        ..Default::default()
                    }),
                1 => calculator
                    .price(r, request.input, &token, deadline)
                    .await
                    .map(Into::into),
                3 => calculator
                    .price_delta(r, request.input, &token, deadline)
                    .await
                    .map(|value| NativePricingResult {
                        size: 64,
                        price: value.price,
                        delta: value.delta,
                        ..Default::default()
                    }),
                4 => calculator
                    .solve(r, request.input, &token, deadline)
                    .await
                    .map(|value| NativePricingResult {
                        size: 64,
                        price: value.price,
                        volatility: value.volatility,
                        ..Default::default()
                    }),
                _ => calculator
                    .implied(r, request.input, &token, deadline)
                    .await
                    .map(Into::into),
            };
            reservation.result = match result {
                Ok(value) => value,
                Err(error) => NativePricingResult {
                    size: 64,
                    failure: failure_code(error),
                    ..Default::default()
                },
            };
            drop(reservation);
            Ok::<_, Outcome>(0.0)
        };
        // Release the table lock before submit can drop a rejected future (its
        // reservation guard takes that lock). Roll back rejection afterwards.
        drop(slots);
        drop(controls);
        if let Err(status) = instance.submit(operation, deadline, work) {
            instance
                .pricing
                .lock()
                .map_err(|_| Status::InternalFailure)?
                .remove(&operation);
            return Err(status);
        }
        Ok(())
    })
}
/// Copies/removes one ready result. A pending request returns Busy and consumes
/// nothing. Delivery must call this after its matching terminal completion, even
/// for cancellation/deadline outcomes, before releasing the runtime.
///
/// # Safety
/// output must be aligned writable exclusive storage for one result throughout
/// this call, independent of all other callers' buffers.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn ifm_market_data_take_pricing_v1(
    handle: u64,
    operation: u64,
    size: u32,
    output: *mut NativePricingResult,
) -> i32 {
    boundary(|| {
        if !valid(output) {
            return Err(Status::InvalidInput);
        }
        if size != 64 {
            return Err(Status::AbiMismatch);
        }
        let instance = instance(handle)?;
        let mut slots = instance
            .pricing
            .lock()
            .map_err(|_| Status::InternalFailure)?;
        let result = slots
            .get(&operation)
            .ok_or(Status::InvalidInput)?
            .ok_or(Status::Busy)?;
        // SAFETY: Caller guarantees aligned writable output; size checked before consumption.
        unsafe {
            output.write(result);
        }
        slots.remove(&operation);
        Ok(())
    })
}
