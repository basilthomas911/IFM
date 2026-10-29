//! Exact current VWAP checkpoint accumulation. Positive price-volume arithmetic
//! preserves .NET Decimal's 96-bit coefficient, scale and half-even reduction;
//! it never substitutes binary floating point for authoritative aggregate state.
use super::normalization::TradeAction;
/// Positive CLR-compatible decimal used by provider price/volume checkpoints.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Decimal {
    /// Unsigned coefficient, always at most 96 bits.
    pub coefficient: u128,
    /// Fractional decimal digits (0..9 for these market source operations).
    pub scale: u32,
}
impl Decimal {
    fn from_price(price: i64) -> Self {
        let mut coefficient = price as u128;
        let mut scale = 9;
        while scale > 0 && coefficient.is_multiple_of(10) {
            coefficient /= 10;
            scale -= 1;
        }
        Self { coefficient, scale }
    }
    fn bounded(coefficient: u128, scale: u32) -> Result<Self, Failure> {
        const MAX: u128 = (1u128 << 96) - 1;
        for removed in 0..=scale {
            let factor = 10u128.pow(removed);
            let quotient = coefficient / factor;
            let remainder = coefficient % factor;
            let rounded = quotient
                + u128::from(
                    remainder > factor / 2
                        || removed > 0 && remainder == factor / 2 && !quotient.is_multiple_of(2),
                );
            if rounded <= MAX {
                return Ok(Self {
                    coefficient: rounded,
                    scale: scale - removed,
                });
            }
        }
        Err(Failure::DecimalOverflow)
    }
    fn multiply(self, integer: i64) -> Result<Self, Failure> {
        Self::bounded(
            self.coefficient
                .checked_mul(integer as u128)
                .ok_or(Failure::DecimalOverflow)?,
            self.scale,
        )
    }
    fn add(self, other: Self) -> Result<Self, Failure> {
        let scale = self.scale.max(other.scale);
        Self::bounded(
            self.coefficient * 10u128.pow(scale - self.scale)
                + other.coefficient * 10u128.pow(scale - other.scale),
            scale,
        )
    }
}
/// Existing published invalidity identities, kept sticky until explicit reset.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
#[repr(u8)]
pub enum InvalidReason {
    /// No known source fault.
    #[default]
    None = 0,
    /// Replay was not completed before live observations.
    RecoveryIncomplete = 5,
    /// Changed stream continuity identity.
    StreamEpochChanged = 2,
    /// Nonconsecutive live trade ordinal.
    DeliveryGap = 1,
    /// A correction cannot be correlated into cumulative sums.
    UncorrelatableCorrection = 3,
    /// Nonpositive price/quantity, snapshot, undefined price, or unsupported action.
    InvalidTrade = 4,
}
/// Checked aggregate failures; caller reports unhealthy/reseeds the source.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Cumulative decimal price-volume exceeds CLR Decimal representation.
    DecimalOverflow,
    /// Published signed quantity/count cannot advance without overflow.
    IntegerOverflow,
}
/// Immutable checkpoint consumed by the existing application projection.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct Checkpoint {
    /// Current live continuity epoch.
    pub epoch: u128,
    /// Last received live ordinal, including records which invalidate the source.
    pub ordinal: i64,
    /// Last source sequence.
    pub sequence: i64,
    /// Last API UTC event ticks since Unix epoch.
    pub at: i64,
    /// Exact sum of eligible price * contracts.
    pub price_volume: Decimal,
    /// Eligible cumulative contracts.
    pub volume: i64,
    /// Eligible records.
    pub eligible: i64,
    /// Invalid/correction records.
    pub rejected: i64,
    /// Most recent positive price, even if its record was ineligible.
    pub last_price: Decimal,
    /// False after any continuity/record fault until reset.
    pub valid: bool,
    /// Explicit replay completion.
    pub replay_complete: bool,
    /// Current recovery/replay identity.
    pub generation: u128,
    /// First observed invalidity (later failures do not hide the original cause).
    pub reason: InvalidReason,
}
/// One source contribution; sequence is not a live trade ordinal.
#[derive(Debug, Clone, Copy)]
pub struct Input {
    /// Exact provider price in 1e-9 units.
    pub price: i64,
    /// Signed source contracts, checked for eligibility.
    pub size: i64,
    /// Source sequence.
    pub sequence: i64,
    /// Event UTC ticks supplied by the normalized input boundary.
    pub at: i64,
    /// Normalized lifecycle action.
    pub action: TradeAction,
    /// Normalized provider flags.
    pub conditions: u16,
}
/// Single-writer source accumulator; no per-trade allocation or historical queue.
pub struct Vwap {
    checkpoint: Checkpoint,
}
impl Default for Vwap {
    fn default() -> Self {
        Self {
            checkpoint: Checkpoint {
                valid: true,
                at: -621355968000000000,
                ..Default::default()
            },
        }
    }
}
impl Vwap {
    /// Clears source state and sets the new explicit recovery generation.
    pub fn reset(&mut self, generation: u128) {
        self.checkpoint = Checkpoint {
            valid: true,
            generation,
            at: -621355968000000000,
            ..Default::default()
        };
    }
    /// Incorporates each replay trade before display/latest-state coalescing.
    pub fn replay(&mut self, input: Input) -> Result<(), Failure> {
        self.apply(input)
    }
    /// Establishes live continuity without clearing reconstructed aggregate state.
    pub fn complete_replay(&mut self, epoch: u128) {
        self.checkpoint.epoch = epoch;
        self.checkpoint.ordinal = 0;
        self.checkpoint.replay_complete = true;
    }
    /// Validates continuity, then incorporates the record even after invalidity;
    /// a snapshot records incomplete state rather than pretending repair succeeded.
    pub fn live(&mut self, input: Input, epoch: u128, ordinal: i64) -> Result<(), Failure> {
        if !self.checkpoint.replay_complete {
            self.invalidate(InvalidReason::RecoveryIncomplete);
        } else if epoch != self.checkpoint.epoch {
            self.invalidate(InvalidReason::StreamEpochChanged);
        } else if ordinal != self.checkpoint.ordinal.wrapping_add(1) {
            self.invalidate(InvalidReason::DeliveryGap);
        }
        self.checkpoint.epoch = epoch;
        self.checkpoint.ordinal = ordinal;
        self.apply(input)
    }
    fn apply(&mut self, input: Input) -> Result<(), Failure> {
        let s = &mut self.checkpoint;
        s.at = input.at;
        s.sequence = input.sequence;
        if input.price > 0 {
            s.last_price = Decimal::from_price(input.price);
        }
        if input.price <= 0
            || input.size <= 0
            || input.action != TradeAction::New
            || input.conditions & (1 << 2 | 1 << 8) != 0
        {
            s.rejected = s.rejected.checked_add(1).ok_or(Failure::IntegerOverflow)?;
            self.invalidate(
                if matches!(
                    input.action,
                    TradeAction::Change
                        | TradeAction::Cancel
                        | TradeAction::Correct
                        | TradeAction::Clear
                ) {
                    InvalidReason::UncorrelatableCorrection
                } else {
                    InvalidReason::InvalidTrade
                },
            );
            return Ok(());
        }
        s.price_volume = s
            .price_volume
            .add(Decimal::from_price(input.price).multiply(input.size)?)?;
        s.volume = s
            .volume
            .checked_add(input.size)
            .ok_or(Failure::IntegerOverflow)?;
        s.eligible = s.eligible.checked_add(1).ok_or(Failure::IntegerOverflow)?;
        Ok(())
    }
    fn invalidate(&mut self, reason: InvalidReason) {
        self.checkpoint.valid = false;
        if self.checkpoint.reason == InvalidReason::None {
            self.checkpoint.reason = reason;
        }
    }
    /// Coherent scalar copy; no input history is retained or replayed on read.
    pub fn snapshot(&self) -> Checkpoint {
        self.checkpoint
    }
}
