pub const ABI_VERSION: u32 = 1;

pub type Status = i32;
pub const OK: Status = 0;
pub const NULL_POINTER: Status = 1;
pub const INVALID_ARGUMENT: Status = 2;
pub const NO_CONVERGENCE: Status = 3;
pub const PANIC: Status = 4;

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Black76ResultV1 {
    pub price: f64,
    pub delta: f64,
    pub gamma: f64,
    pub vega: f64,
    pub theta: f64,
    pub rho: f64,
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default, PartialEq)]
pub struct Black76ImpliedGreeksResultV1 {
    pub implied_volatility: f64,
    pub price: f64,
    pub delta: f64,
    pub gamma: f64,
    pub vega: f64,
    pub theta: f64,
    pub rho: f64,
}

#[cfg(test)]
mod tests {
    use std::mem::{align_of, offset_of, size_of};

    use super::{Black76ImpliedGreeksResultV1, Black76ResultV1};

    #[test]
    fn black76_result_v1_layout_matches_the_frozen_abi() {
        assert_eq!(size_of::<Black76ResultV1>(), 48);
        assert_eq!(align_of::<Black76ResultV1>(), 8);
        assert_eq!(offset_of!(Black76ResultV1, price), 0);
        assert_eq!(offset_of!(Black76ResultV1, delta), 8);
        assert_eq!(offset_of!(Black76ResultV1, gamma), 16);
        assert_eq!(offset_of!(Black76ResultV1, vega), 24);
        assert_eq!(offset_of!(Black76ResultV1, theta), 32);
        assert_eq!(offset_of!(Black76ResultV1, rho), 40);
    }

    #[test]
    fn implied_greeks_result_v1_layout_matches_the_frozen_abi() {
        assert_eq!(size_of::<Black76ImpliedGreeksResultV1>(), 56);
        assert_eq!(align_of::<Black76ImpliedGreeksResultV1>(), 8);
        assert_eq!(
            offset_of!(Black76ImpliedGreeksResultV1, implied_volatility),
            0
        );
        assert_eq!(offset_of!(Black76ImpliedGreeksResultV1, price), 8);
        assert_eq!(offset_of!(Black76ImpliedGreeksResultV1, delta), 16);
        assert_eq!(offset_of!(Black76ImpliedGreeksResultV1, gamma), 24);
        assert_eq!(offset_of!(Black76ImpliedGreeksResultV1, vega), 32);
        assert_eq!(offset_of!(Black76ImpliedGreeksResultV1, theta), 40);
        assert_eq!(offset_of!(Black76ImpliedGreeksResultV1, rho), 48);
    }
}
