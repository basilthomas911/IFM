//! Source-time actual-trade inputs. A queue may delay delivery, but must never
//! replace the original underlying/reference assumptions with newer hot values.
use super::{coalesced::TradeContext, qualification::Quote};

/// Frozen preparation retained by the copied authoritative trade record.
#[derive(Clone, Debug)]
pub struct Captured {
    /// Immutable numerical and opaque application-context version.
    pub prepared: TradeContext,
    /// Underlying quote at source processing, absent when prerequisites were absent.
    pub underlying: Option<Quote>,
    /// Authoritative API valuation instant in UTC ticks since Unix epoch.
    pub at: i64,
}
