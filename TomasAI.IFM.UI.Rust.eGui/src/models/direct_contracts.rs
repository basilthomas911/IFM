//! Generated streaming option-contract readers. Do not edit by hand.
use super::{direct_backend::*, trade_contracts::*, wire_types::*};
use serde::de::{IgnoredAny, SeqAccess, Visitor};
impl DirectRecord for EvaluatedOptionChainReadModel {
    /// Streams the positional backend record using its pinned field slots.
    fn deserialize_backend<'de, D: serde::Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        /// Record Visitor used by the direct contracts component.
        struct RecordVisitor;
        impl<'de> Visitor<'de> for RecordVisitor {
            type Value = EvaluatedOptionChainReadModel;


            /// Describes the accepted input shape in deserialization errors.
            fn expecting(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
                f.write_str("positional backend record")
            }


            /// Reads the sequence entries through this value's deserialization rules.
            fn visit_seq<A: SeqAccess<'de>>(self, mut seq: A) -> Result<Self::Value, A::Error> {
                let underlying_contract_id = seq
                    .next_element::<Scalar<Option<String>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let expiry_date = seq
                    .next_element::<Scalar<Option<DateText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let underlying_price = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let lower_strike_bound = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let upper_strike_bound = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let window_method = seq
                    .next_element::<Scalar<Option<String>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let as_of_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let contracts = seq
                    .next_element::<OptionalRecords<EvaluatedOptionContractReadModel>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let bollinger_standard_deviation20 = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let previous_session_close = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let iv_rank = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let iv_rank_status = seq
                    .next_element::<Scalar<Option<String>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let bollinger_as_of_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let last_underlying_price = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let last_underlying_trade_at_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let atm_implied_volatility = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let daily_price_change = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let daily_price_change_fraction = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let days_to_expiry = seq
                    .next_element::<Scalar<Option<i32>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                while seq.next_element::<IgnoredAny>()?.is_some() {}
                Ok(EvaluatedOptionChainReadModel {
                    underlying_contract_id,
                    expiry_date,
                    underlying_price,
                    lower_strike_bound,
                    upper_strike_bound,
                    window_method,
                    as_of_utc,
                    contracts,
                    bollinger_standard_deviation20,
                    previous_session_close,
                    iv_rank,
                    iv_rank_status,
                    bollinger_as_of_utc,
                    last_underlying_price,
                    last_underlying_trade_at_utc,
                    atm_implied_volatility,
                    daily_price_change,
                    daily_price_change_fraction,
                    days_to_expiry,
                })
            }
        }
        d.deserialize_seq(RecordVisitor)
    }
}
impl DirectRecord for EvaluatedOptionContractReadModel {
    /// Streams the positional backend record using its pinned field slots.
    fn deserialize_backend<'de, D: serde::Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        /// Record Visitor used by the direct contracts component.
        struct RecordVisitor;
        impl<'de> Visitor<'de> for RecordVisitor {
            type Value = EvaluatedOptionContractReadModel;


            /// Describes the accepted input shape in deserialization errors.
            fn expecting(&self, f: &mut std::fmt::Formatter) -> std::fmt::Result {
                f.write_str("positional backend record")
            }


            /// Reads the sequence entries through this value's deserialization rules.
            fn visit_seq<A: SeqAccess<'de>>(self, mut seq: A) -> Result<Self::Value, A::Error> {
                let contract_id = seq
                    .next_element::<Scalar<Option<String>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let strike = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let is_call = seq
                    .next_element::<Scalar<Option<bool>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let bid = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let ask = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let bid_size = seq
                    .next_element::<Scalar<Option<u32>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let ask_size = seq
                    .next_element::<Scalar<Option<u32>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let last = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let last_size = seq
                    .next_element::<Scalar<Option<u32>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let implied_volatility = seq
                    .next_element::<Scalar<Option<f64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let theoretical_price = seq
                    .next_element::<Scalar<Option<f64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let delta = seq
                    .next_element::<Scalar<Option<f64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let gamma = seq
                    .next_element::<Scalar<Option<f64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let vega = seq
                    .next_element::<Scalar<Option<f64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let theta = seq
                    .next_element::<Scalar<Option<f64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let rho = seq
                    .next_element::<Scalar<Option<f64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let volume = seq
                    .next_element::<Scalar<Option<i64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let open_interest = seq
                    .next_element::<Scalar<Option<i64>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let greeks_valid = seq
                    .next_element::<Scalar<Option<bool>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let is_stale = seq
                    .next_element::<Scalar<Option<bool>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let quote_at_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let trade_at_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let evaluated_at_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let volume_value_date = seq
                    .next_element::<Scalar<Option<DateText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let open_interest_value_date = seq
                    .next_element::<Scalar<Option<DateText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let selection_valid = seq
                    .next_element::<Scalar<Option<bool>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let quote_unavailable_reason = seq
                    .next_element::<Scalar<Option<String>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let last_quote_event_at_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let last_quote_received_at_utc = seq
                    .next_element::<Scalar<Option<OffsetText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                let cash_multiplier = seq
                    .next_element::<Scalar<Option<DecimalText>>>()?
                    .map(|v| v.0)
                    .unwrap_or_default();
                while seq.next_element::<IgnoredAny>()?.is_some() {}
                Ok(EvaluatedOptionContractReadModel {
                    contract_id,
                    strike,
                    is_call,
                    bid,
                    ask,
                    bid_size,
                    ask_size,
                    last,
                    last_size,
                    implied_volatility,
                    theoretical_price,
                    delta,
                    gamma,
                    vega,
                    theta,
                    rho,
                    volume,
                    open_interest,
                    greeks_valid,
                    is_stale,
                    quote_at_utc,
                    trade_at_utc,
                    evaluated_at_utc,
                    volume_value_date,
                    open_interest_value_date,
                    selection_valid,
                    quote_unavailable_reason,
                    last_quote_event_at_utc,
                    last_quote_received_at_utc,
                    cash_multiplier,
                })
            }
        }
        d.deserialize_seq(RecordVisitor)
    }
}
