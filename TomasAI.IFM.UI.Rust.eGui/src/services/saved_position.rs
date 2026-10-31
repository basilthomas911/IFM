//! Attach typed backend risk observations without changing saved lifecycle or accounting values.
use crate::models::{trade_contracts::*, wire_types::DecimalText};
fn decimal(value: &Option<DecimalText>) -> Option<crate::backend::contracts::ExactDecimal> {
    crate::backend::contracts::ExactDecimal::parse(value.as_ref()?.as_str()).ok()
}
fn equal_amount(a: &Option<DecimalText>, b: &Option<DecimalText>) -> bool {
    decimal(a)
        .zip(decimal(b))
        .is_some_and(|(a, b)| super::option_selection::compare(&a, &b).is_eq())
}
pub fn enrich(
    saved: &StrategyPositionSnapshot,
    current: &StrategyPositionSnapshot,
) -> Option<StrategyPositionSnapshot> {
    if matches!(
        saved.phase,
        Some(StrategyPositionPhase::Open | StrategyPositionPhase::Close)
    ) || saved.id != current.id
        || saved.value_date != current.value_date
        || saved.route_generation != current.route_generation
    {
        return None;
    }
    let before = saved.legs.as_ref()?;
    let latest = current.legs.as_ref()?;
    if before.len() != latest.len() {
        return None;
    }
    let mut legs = Vec::with_capacity(before.len());
    for leg in before {
        let mut matching = latest.iter().filter(|other| {
            other.trade_leg_id == leg.trade_leg_id
                && other.contract_id == leg.contract_id
                && other.signed_quantity == leg.signed_quantity
                && equal_amount(&other.current_price, &leg.current_price)
                && equal_amount(&other.opening_price, &leg.opening_price)
        });
        let source = matching.next()?;
        if matching.next().is_some() {
            return None;
        }
        let mut next = leg.clone();
        next.option_tick = source.option_tick.clone();
        next.option_risk = source.option_risk.clone();
        next.scenario = source.scenario.clone();
        let tick = source
            .option_tick
            .as_ref()
            .filter(|tick| tick.pricing_inputs.is_some())
            .or(source.option_risk.as_ref());
        if let Some(tick) = tick.filter(|tick| tick.contract_id == source.contract_id) {
            if let Some(multiplier) = tick
                .pricing_inputs
                .as_ref()
                .and_then(|inputs| inputs.multiplier.as_ref())
            {
                if crate::backend::contracts::ExactDecimal::parse(multiplier.as_str())
                    .ok()
                    .is_some_and(|value| {
                        super::option_selection::compare(
                            &value,
                            &crate::backend::contracts::ExactDecimal::parse("0").unwrap(),
                        )
                        .is_gt()
                    })
                {
                    next.cash_multiplier = Some(multiplier.clone());
                }
            }
        }
        legs.push(next);
    }
    let mut result = saved.clone();
    result.legs = Some(legs);
    Some(result)
}
