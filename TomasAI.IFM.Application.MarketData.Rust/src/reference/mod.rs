//! Reference-data calculations with explicit source units and application time.

pub mod decimal;
pub mod treasury;
pub mod treasury_curve;

/// Pinned native exchange timezone, rollover and trading-calendar qualification.
pub mod calendar;
pub mod fmp;
/// Canonical pricing/reference identity hashing with current CLR wire semantics.
pub mod semantic;
/// Complete native publication schedule, source qualification and rate conversion.
pub mod treasury_policy;

pub mod decimal_arithmetic;

/// Complete raw reviewed option input and native qualification.
pub mod option_contract;

/// Complete immutable calendar/publication/conversion input validation.
pub mod inputs;
