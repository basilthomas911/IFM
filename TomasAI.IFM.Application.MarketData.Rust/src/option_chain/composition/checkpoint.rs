//! Exact copied snapshot state. Restoration never seals, reprices or restamps an original capture.
use super::model::{Future, Instrument, Snapshot, Valued};
use crate::{
    checkpoint as c,
    messaging::option_evidence::{Greeks, Quote},
    option_chain::{
        context::Context,
        qualification::{Decimal, Quote as RawQuote},
        selection::Selection,
        strategy::{Parameters, Ranking},
    },
};
use rmpv::Value as V;
use std::sync::Arc;

pub(crate) fn decimal(v: Decimal) -> V {
    V::Array(v.bits.into_iter().map(Into::into).collect())
}
pub(crate) fn read_decimal(v: &V) -> Result<Decimal, String> {
    let f = c::fields(v, 4)?;
    let mut bits = [0; 4];
    for (i, v) in f.iter().enumerate() {
        bits[i] = c::unsigned(v)?
            .try_into()
            .map_err(|_| "Snapshot decimal overflow")?;
    }
    let result = Decimal { bits };
    crate::messaging::codec::decimal_bits(result).map_err(str::to_owned)?;
    Ok(result)
}
pub(crate) fn quote(v: &Quote) -> V {
    let q = v.quote;
    V::Array(vec![
        v.contract.clone().into(),
        q.generation_low.into(),
        q.generation_high.into(),
        q.event.into(),
        q.received.into(),
        q.sequence.into(),
        decimal(q.bid),
        decimal(q.ask),
        decimal(q.bid_size),
        decimal(q.ask_size),
        q.event_offset.into(),
        q.received_offset.into(),
    ])
}
pub(crate) fn read_quote(v: &V) -> Result<Quote, String> {
    let f = c::fields(v, 12)?;
    let result = Quote {
        contract: c::string(&f[0], 4096)?,
        quote: RawQuote {
            generation_low: c::unsigned(&f[1])?,
            generation_high: c::unsigned(&f[2])?,
            event: c::number(&f[3])?,
            received: c::number(&f[4])?,
            sequence: c::number(&f[5])?,
            bid: read_decimal(&f[6])?,
            ask: read_decimal(&f[7])?,
            bid_size: read_decimal(&f[8])?,
            ask_size: read_decimal(&f[9])?,
            event_offset: c::number(&f[10])?
                .try_into()
                .map_err(|_| "Snapshot quote offset overflow")?,
            received_offset: c::number(&f[11])?
                .try_into()
                .map_err(|_| "Snapshot quote offset overflow")?,
        },
    };
    result.value().map_err(str::to_owned)?;
    Ok(result)
}
fn optional<T>(v: Option<&T>, encode: impl Fn(&T) -> V) -> V {
    v.map_or(V::Nil, encode)
}
fn read_optional<T>(v: &V, decode: impl Fn(&V) -> Result<T, String>) -> Result<Option<T>, String> {
    if v.is_nil() {
        Ok(None)
    } else {
        decode(v).map(Some)
    }
}
fn read_guid(v: &V) -> Result<[u8; 16], String> {
    match v {
        V::Binary(bytes) if bytes.len() == 16 => Ok(bytes.as_slice().try_into().unwrap()),
        _ => Err("Snapshot UUID bytes invalid".into()),
    }
}
fn future(v: &Future) -> V {
    V::Array(vec![
        v.contract.clone().into(),
        v.root.clone().into(),
        v.dataset.clone().into(),
        v.exchange.clone().into(),
        v.currency.clone().into(),
        v.last_trading.into(),
        decimal(v.multiplier),
        decimal(v.tick),
        v.digest.clone().into(),
    ])
}
fn read_future(v: &V) -> Result<Future, String> {
    let f = c::fields(v, 9)?;
    Ok(Future {
        contract: c::string(&f[0], 4 * 1024 * 1024)?,
        root: c::string(&f[1], 4 * 1024 * 1024)?,
        dataset: c::string(&f[2], 4 * 1024 * 1024)?,
        exchange: c::string(&f[3], 4 * 1024 * 1024)?,
        currency: c::string(&f[4], 4 * 1024 * 1024)?,
        last_trading: c::number(&f[5])?,
        multiplier: read_decimal(&f[6])?,
        tick: read_decimal(&f[7])?,
        digest: c::string(&f[8], 4 * 1024 * 1024)?,
    })
}
fn greeks(v: &Greeks) -> V {
    let g = v.values;
    V::Array(vec![
        g.price.to_bits().into(),
        g.volatility.to_bits().into(),
        g.delta.to_bits().into(),
        g.gamma.to_bits().into(),
        g.vega.to_bits().into(),
        g.theta.to_bits().into(),
        g.rho.to_bits().into(),
        v.time.to_bits().into(),
        v.digest.clone().into(),
    ])
}
fn read_greeks(v: &V) -> Result<Greeks, String> {
    let f = c::fields(v, 9)?;
    let n = f[..8]
        .iter()
        .map(|v| c::unsigned(v).map(f64::from_bits))
        .collect::<Result<Vec<_>, _>>()?;
    let result = Greeks {
        values: crate::pricing::Values {
            price: n[0],
            volatility: n[1],
            delta: n[2],
            gamma: n[3],
            vega: n[4],
            theta: n[5],
            rho: n[6],
        },
        time: n[7],
        digest: c::string(&f[8], 4 * 1024 * 1024)?,
    };
    result.value().map_err(str::to_owned)?;
    Ok(result)
}
fn instrument(v: &Instrument) -> V {
    V::Array(vec![
        v.contract.clone().into(),
        optional(v.quote.as_ref(), quote),
        optional(v.context.as_deref(), Context::checkpoint),
        v.strike.map_or(V::Nil, decimal),
        v.call.map_or(V::Nil, Into::into),
        optional(v.underlying.as_ref(), quote),
        optional(v.future.as_ref(), future),
        optional(v.selection.as_ref(), Selection::checkpoint),
        v.volume.map_or(V::Nil, Into::into),
        v.interest.map_or(V::Nil, Into::into),
        v.statistics_at.map_or(V::Nil, Into::into),
        v.unavailable.clone().map_or(V::Nil, Into::into),
        v.last_event.map_or(V::Nil, Into::into),
        v.last_received.map_or(V::Nil, Into::into),
        v.volume_date.map_or(V::Nil, Into::into),
        v.interest_date.map_or(V::Nil, Into::into),
    ])
}
fn read_instrument(v: &V) -> Result<Instrument, String> {
    let f = c::fields(v, 16)?;
    let day = |v: &V| -> Result<i32, String> {
        c::number(v)?
            .try_into()
            .map_err(|_| "Snapshot statistics date overflow".into())
    };
    let result = Instrument {
        contract: c::string(&f[0], 4 * 1024 * 1024)?,
        quote: read_optional(&f[1], read_quote)?,
        context: read_optional(&f[2], Context::restore_checkpoint)?.map(Arc::new),
        strike: read_optional(&f[3], read_decimal)?,
        call: read_optional(&f[4], c::boolean)?,
        underlying: read_optional(&f[5], read_quote)?,
        future: read_optional(&f[6], read_future)?,
        selection: read_optional(&f[7], Selection::restore_checkpoint)?,
        volume: read_optional(&f[8], c::number)?,
        interest: read_optional(&f[9], c::number)?,
        statistics_at: read_optional(&f[10], c::number)?,
        unavailable: read_optional(&f[11], |v| c::string(v, 4 * 1024 * 1024))?,
        last_event: read_optional(&f[12], c::number)?,
        last_received: read_optional(&f[13], c::number)?,
        volume_date: read_optional(&f[14], day)?,
        interest_date: read_optional(&f[15], day)?,
    };
    result.value().map_err(str::to_owned)?;
    Ok(result)
}
fn ranking(v: &Ranking) -> V {
    V::Array(vec![
        v.parameters.value(),
        v.direction.into(),
        c::day(v.date),
    ])
}
fn read_ranking(v: &V) -> Result<Ranking, String> {
    let f = c::fields(v, 3)?;
    let result = Ranking {
        parameters: Arc::new(Parameters::new(f[0].clone()).map_err(str::to_owned)?),
        direction: c::unsigned(&f[1])?
            .try_into()
            .map_err(|_| "Snapshot ranking direction overflow")?,
        date: c::date(&f[2])?,
    };
    result.bias().map_err(str::to_owned)?;
    Ok(result)
}
impl Snapshot {
    pub(crate) fn checkpoint(&self) -> V {
        V::Array(vec![
            1.into(),
            optional(self.ranking.as_ref(), ranking),
            self.schema.into(),
            V::Binary(self.id.to_vec()),
            self.scope.clone().into(),
            self.token.clone().into(),
            self.horizon.clone().into(),
            V::Binary(self.generation.to_vec()),
            self.at.into(),
            self.until.into(),
            V::Array(
                self.instruments
                    .iter()
                    .map(|v| {
                        V::Array(vec![
                            instrument(&v.instrument),
                            optional(v.valuation.as_ref(), greeks),
                        ])
                    })
                    .collect(),
            ),
            self.digest.clone().into(),
        ])
    }
    pub(crate) fn restore_checkpoint(value: &V) -> Result<Self, String> {
        let f = c::fields(value, 12)?;
        if c::unsigned(&f[0])? != 1 {
            return Err("Snapshot checkpoint version invalid".into());
        }
        let rows = f[10]
            .as_array()
            .filter(|rows| rows.len() <= crate::option_chain::MAX_LIVE_OPTIONS)
            .ok_or("Snapshot checkpoint instrument bound")?;
        let instruments = rows
            .iter()
            .map(|v| {
                let p = c::fields(v, 2)?;
                Ok(Valued {
                    instrument: read_instrument(&p[0])?,
                    valuation: read_optional(&p[1], read_greeks)?,
                })
            })
            .collect::<Result<Vec<_>, String>>()?;
        let result = Self {
            ranking: read_optional(&f[1], read_ranking)?,
            schema: c::number(&f[2])?
                .try_into()
                .map_err(|_| "Snapshot schema overflow")?,
            id: read_guid(&f[3])?,
            scope: c::string(&f[4], 512)?,
            token: c::string(&f[5], 512)?,
            horizon: c::string(&f[6], 32)?,
            generation: read_guid(&f[7])?,
            at: c::number(&f[8])?,
            until: c::number(&f[9])?,
            instruments,
            digest: c::string(&f[11], 64)?,
        };
        if !matches!(result.schema, 1 | 2)
            || result.id == [0; 16]
            || result.generation == [0; 16]
            || result.digest.len() != 64
            || !result.digest.bytes().all(|v| v.is_ascii_hexdigit())
            || !matches!(result.horizon.as_str(), "Daily" | "Weekly" | "Monthly")
            || result.scope.trim().is_empty()
            || result.scope.encode_utf16().count() > 128
            || result.token.trim().is_empty()
            || result.token.encode_utf16().count() > 128
            || result.instruments.windows(2).any(|p| {
                p[0].instrument
                    .contract
                    .encode_utf16()
                    .cmp(p[1].instrument.contract.encode_utf16())
                    != std::cmp::Ordering::Less
            })
        {
            return Err("Original snapshot identity/order invalid".into());
        }
        result.encode().map_err(str::to_owned)?;
        Ok(result)
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn ranking_keeps_original_policy_value_date_and_direction_without_reselection() {
        let range = |a: &str, b: &str, d: &str| V::Array(vec![a.into(), b.into(), d.into()]);
        let biases = (0..3)
            .map(|direction| {
                V::Array(vec![
                    direction.into(),
                    true.into(),
                    35.into(),
                    45.into(),
                    42.into(),
                    range("0.12", "0.14", "0.16"),
                    range("0.06", "0.08", "0.10"),
                    V::Array(vec!["50".into()]),
                    V::Array(vec!["50".into()]),
                    "0".into(),
                    "1".into(),
                    5000.into(),
                    2000.into(),
                    "5".into(),
                    V::Nil,
                    1.into(),
                    "5".into(),
                    32.into(),
                ])
            })
            .collect();
        let original = V::Array(vec![
            "11111111-1111-1111-1111-111111111111".into(),
            7.into(),
            "original-policy".into(),
            "22222222-2222-2222-2222-222222222222".into(),
            9.into(),
            "ES".into(),
            "Paper".into(),
            true.into(),
            V::Array(biases),
            V::F64(1.5),
            64.into(),
            4.into(),
            1.into(),
        ]);
        let source = Ranking {
            parameters: Arc::new(Parameters::new(original.clone()).unwrap()),
            direction: 2,
            date: time::Date::from_calendar_date(2025, time::Month::November, 7).unwrap(),
        };
        let restored = read_ranking(&ranking(&source)).unwrap();
        assert_eq!(restored.parameters.value(), original);
        assert_eq!(restored.date, source.date);
        assert_eq!(restored.direction, source.direction);
        assert_eq!(
            restored.parameters.hash().unwrap(),
            source.parameters.hash().unwrap()
        );
    }
    #[test]
    fn snapshot_checkpoint_retains_source_scalars_signed_zero_and_original_digest_without_sealing_again(
    ) {
        let d = |v| crate::reference::decimal::parse(v).unwrap();
        let at = 638_000_000_000_000_000;
        let quote = Quote {
            contract: "ESZ2".into(),
            quote: RawQuote {
                generation_low: 17,
                generation_high: 9,
                event: at,
                received: at + 101,
                sequence: 87654321,
                bid: d("3999.25"),
                ask: d("3999.50"),
                bid_size: d("2"),
                ask_size: d("3"),
                ..Default::default()
            },
        };
        let instrument = Instrument {
            contract: "ESZ2".into(),
            quote: Some(quote.clone()),
            context: None,
            strike: None,
            call: None,
            underlying: Some(quote),
            future: Some(Future {
                contract: "ESZ2".into(),
                root: "ES".into(),
                dataset: "GLBX.MDP3".into(),
                exchange: "CME".into(),
                currency: "USD".into(),
                last_trading: at + 10000000,
                multiplier: d("50.00"),
                tick: d("0.25"),
                digest: "original-reference".into(),
            }),
            selection: None,
            volume: Some(123456789),
            interest: Some(456789012),
            statistics_at: Some(at - 123),
            unavailable: Some("original historical reason".into()),
            last_event: Some(at - 110),
            last_received: Some(at - 100),
            volume_date: Some(738000),
            interest_date: Some(737999),
        };
        let mut snapshot = Snapshot {
            ranking: None,
            schema: 1,
            id: [1; 16],
            scope: "原始scope".into(),
            token: "original-scope-token".into(),
            horizon: "Daily".into(),
            generation: [2; 16],
            at,
            until: at + 9000000,
            instruments: vec![Valued {
                instrument,
                valuation: Some(Greeks {
                    values: crate::pricing::Values {
                        price: 1.5,
                        volatility: 0.23,
                        delta: -0.0,
                        gamma: 0.001,
                        vega: 9.0,
                        theta: -1.0,
                        rho: 0.25,
                    },
                    time: 0.125,
                    digest: "original-context-digest".into(),
                }),
            }],
            digest: String::new(),
        };
        snapshot.seal().unwrap();
        let checkpoint = snapshot.checkpoint();
        let restored = Snapshot::restore_checkpoint(&checkpoint).unwrap();
        assert_eq!(checkpoint, restored.checkpoint());
        assert_eq!(snapshot.encode().unwrap(), restored.encode().unwrap());
        assert_eq!(restored.digest, snapshot.digest);
        assert_eq!(
            restored.instruments[0]
                .valuation
                .as_ref()
                .unwrap()
                .values
                .delta
                .to_bits(),
            (-0.0f64).to_bits()
        );
        let mut wrong = checkpoint.clone();
        let V::Array(ref mut fields) = wrong else {
            panic!()
        };
        fields[0] = 99.into();
        assert!(Snapshot::restore_checkpoint(&wrong).is_err());
        let mut wrong = checkpoint;
        let V::Array(ref mut fields) = wrong else {
            panic!()
        };
        fields[10] = V::Array(vec![V::Array(vec![])]);
        assert!(Snapshot::restore_checkpoint(&wrong).is_err());
    }
}
