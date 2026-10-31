//! Provisional draft economics come exclusively from TradeOrderQuery. Input
//! revisions fence late responses; none of these totals authorizes execution.
use crate::backend::{contracts::*, schema::Registry, transport::Backend};
use crate::models::{trade_contracts::TradeOrderPreview, wire_types::FromBackend};
use rmpv::Value;
use std::collections::BTreeMap;
const ROOT: &str = "TomasAI.IFM.Domain.Trade.Shared.Order";
#[derive(Clone, Debug)]
pub struct Leg {
    pub contract: String,
    pub side: i32,
    pub strike: Option<ExactDecimal>,
    pub is_call: bool,
    pub is_future: bool,
    pub bid: Option<ExactDecimal>,
    pub ask: Option<ExactDecimal>,
    pub delta: Option<f64>,
    pub vega: Option<f64>,
    pub multiplier: Option<ExactDecimal>,
}
impl Leg {
    fn wire(&self, registry: &Registry, quantity: i32) -> Result<Value, String> {
        if self.contract.trim().is_empty()
            || ![-1, 1].contains(&self.side)
            || self.delta.is_some_and(|v| !v.is_finite())
            || self.vega.is_some_and(|v| !v.is_finite())
        {
            return Err("Invalid provisional preview leg".into());
        }
        let decimal =
            |v: &Option<ExactDecimal>| v.as_ref().map(ExactDecimal::wire).unwrap_or(Value::Nil);
        let double = |v: Option<f64>| v.map(Value::F64).unwrap_or(Value::Nil);
        registry.encode(
            &format!("{ROOT}.TradeOrderPreviewLeg"),
            &BTreeMap::from([
                ("ContractId".into(), self.contract.clone().into()),
                ("Side".into(), self.side.into()),
                ("Quantity".into(), quantity.into()),
                ("Strike".into(), decimal(&self.strike)),
                ("IsCall".into(), self.is_call.into()),
                ("IsFuture".into(), self.is_future.into()),
                ("Bid".into(), decimal(&self.bid)),
                ("Ask".into(), decimal(&self.ask)),
                ("Delta".into(), double(self.delta)),
                ("Vega".into(), double(self.vega)),
                ("CashMultiplier".into(), decimal(&self.multiplier)),
            ]),
        )
    }
}
#[derive(Default)]
pub struct Preview {
    revision: i64,
}
impl Preview {
    pub fn revision(&self) -> i64 {
        self.revision
    }
    pub async fn load(
        &mut self,
        backend: &Backend,
        registry: &Registry,
        legs: &[Leg],
        quantity: i32,
        entered_price: Option<&ExactDecimal>,
        underlying_reference: Option<&ExactDecimal>,
        deadline: tokio::time::Instant,
    ) -> Result<TradeOrderPreview, String> {
        if !(1..=100000).contains(&quantity) || legs.len() > 4 {
            return Err("Preview quantity or leg count is outside supported bounds".into());
        }
        let unique: std::collections::BTreeSet<_> = legs.iter().map(|v| &v.contract).collect();
        if unique.len() != legs.len() {
            return Err("Preview contains duplicate contracts".into());
        }
        self.revision = self
            .revision
            .checked_add(1)
            .ok_or("Preview revision exhausted")?;
        let request = registry.encode(
            &format!("{ROOT}.TradeOrderPreviewRequest"),
            &BTreeMap::from([
                ("InputRevision".into(), self.revision.into()),
                (
                    "SelectedLegs".into(),
                    Value::Array(
                        legs.iter()
                            .map(|l| l.wire(registry, quantity))
                            .collect::<Result<_, _>>()?,
                    ),
                ),
                ("StrategyQuantity".into(), quantity.into()),
                (
                    "EnteredOrderPrice".into(),
                    entered_price.map(ExactDecimal::wire).unwrap_or(Value::Nil),
                ),
                // The compatibility slot is deliberately unchanged; the backend resolves
                // the actual instrument entry tick. It is not a local tick assumption.
                ("TickIncrement".into(), "1".into()),
                (
                    "UnderlyingReferencePrice".into(),
                    underlying_reference
                        .map(ExactDecimal::wire)
                        .unwrap_or(Value::Nil),
                ),
            ]),
        )?;
        let kind = format!("{ROOT}.GetTradeOrderPreviewQuery");
        let (actor, verb) = registry.route(&kind)?;
        let subject = ActorSubject::query(actor, verb, "Preview");
        let body = registry.encode(
            &kind,
            &BTreeMap::from([
                ("Subject".into(), subject.wire()),
                (
                    "EntityId".into(),
                    Value::Array(vec![0.into(), Value::Array(vec!["none".into()])]),
                ),
                ("TradeOrderPreviewRequest".into(), request),
            ]),
        )?;
        let reply = backend
            .request_until(&subject, &body, deadline, None)
            .await?;
        let result = TradeOrderPreview::from_backend(service_result(&reply)?)?;
        if result.input_revision != Some(self.revision) {
            return Err("Trade preview response does not match the current draft revision".into());
        }
        Ok(result)
    }
}
