//! Exact mutable pricing checkpoints bound to the original immutable prepared context graph.
use super::*;
use crate::checkpoint as c;
use rmpv::Value as V;
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
fn decimal(v: &qualification::Decimal) -> V {
    V::Array(v.bits.iter().map(|v| (*v).into()).collect())
}
fn read_decimal(v: &V) -> Result<qualification::Decimal, String> {
    let f = c::fields(v, 4)?;
    let mut bits = [0; 4];
    for (i, v) in f.iter().enumerate() {
        bits[i] = c::unsigned(v)?
            .try_into()
            .map_err(|_| "Decimal word overflow")?;
    }
    Ok(qualification::Decimal { bits })
}
fn quote(v: &Quote) -> V {
    V::Array(vec![
        v.generation_low.into(),
        v.generation_high.into(),
        v.event.into(),
        v.received.into(),
        v.sequence.into(),
        decimal(&v.bid),
        decimal(&v.ask),
        decimal(&v.bid_size),
        decimal(&v.ask_size),
        v.event_offset.into(),
        v.received_offset.into(),
    ])
}
fn read_quote(v: &V) -> Result<Quote, String> {
    let f = c::fields(v, 11)?;
    Ok(Quote {
        generation_low: c::unsigned(&f[0])?,
        generation_high: c::unsigned(&f[1])?,
        event: c::number(&f[2])?,
        received: c::number(&f[3])?,
        sequence: c::number(&f[4])?,
        bid: read_decimal(&f[5])?,
        ask: read_decimal(&f[6])?,
        bid_size: read_decimal(&f[7])?,
        ask_size: read_decimal(&f[8])?,
        event_offset: c::number(&f[9])?
            .try_into()
            .map_err(|_| "Quote offset overflow")?,
        received_offset: c::number(&f[10])?
            .try_into()
            .map_err(|_| "Quote offset overflow")?,
    })
}
fn iv(v: &Iv) -> V {
    V::Array(vec![
        c::wide(v.context),
        v.event.into(),
        v.volatility.to_bits().into(),
        quote(&v.underlying),
        quote(&v.option),
        v.at.into(),
    ])
}
fn read_iv(v: &V) -> Result<Iv, String> {
    let f = c::fields(v, 6)?;
    Ok(Iv {
        context: c::read_wide(&f[0])?,
        event: c::number(&f[1])?,
        volatility: f64::from_bits(c::unsigned(&f[2])?),
        underlying: read_quote(&f[3])?,
        option: read_quote(&f[4])?,
        at: c::number(&f[5])?,
    })
}
fn risk(v: &Values) -> V {
    V::Array(
        [
            v.price,
            v.volatility,
            v.delta,
            v.gamma,
            v.vega,
            v.theta,
            v.rho,
        ]
        .iter()
        .map(|v| v.to_bits().into())
        .collect(),
    )
}
fn read_risk(v: &V) -> Result<Values, String> {
    let f = c::fields(v, 7)?
        .iter()
        .map(|v| c::unsigned(v).map(f64::from_bits))
        .collect::<Result<Vec<_>, _>>()?;
    Ok(Values {
        price: f[0],
        volatility: f[1],
        delta: f[2],
        gamma: f[3],
        vega: f[4],
        theta: f[5],
        rho: f[6],
    })
}
fn failure(v: &Failure) -> V {
    let (code, detail) = match v {
        Failure::UnderlyingUnavailable => (1, V::Nil),
        Failure::ContextExpired => (2, V::Nil),
        Failure::Quote(q) => (
            3,
            V::Array(vec![
                q.size.into(),
                q.failure.into(),
                q.input.into(),
                q.reserved.into(),
            ]),
        ),
        Failure::Numerical(n) => {
            use NumericalFailure as N;
            let code = match n {
                N::InvalidInput => 1,
                N::Expired => 2,
                N::UndefinedGreeks => 3,
                N::PriceOutOfBounds => 4,
                N::ImpliedVolatilityNotIdentifiable => 5,
                N::VolatilityNotBracketed => 6,
                N::NonConvergence => 7,
                N::NumericalFailure => 8,
                N::Interrupted(crate::runtime::Interrupted::Cancelled) => 9,
                N::Interrupted(crate::runtime::Interrupted::DeadlineExpired) => 10,
                N::Capacity => 11,
            };
            (4, V::from(code))
        }
        Failure::Stale => (5, V::Nil),
        Failure::CalculationPending => (6, V::Nil),
    };
    V::Array(vec![code.into(), detail])
}
fn read_failure(v: &V) -> Result<Failure, String> {
    let f = c::fields(v, 2)?;
    Ok(match c::unsigned(&f[0])? {
        1 => Failure::UnderlyingUnavailable,
        2 => Failure::ContextExpired,
        3 => {
            let q = c::fields(&f[1], 4)?;
            let mut n = [0u32; 4];
            for (i, v) in q.iter().enumerate() {
                n[i] = c::unsigned(v)?
                    .try_into()
                    .map_err(|_| "Quote failure overflow")?;
            }
            Failure::Quote(qualification::Result {
                size: n[0],
                failure: n[1],
                input: n[2],
                reserved: n[3],
            })
        }
        4 => {
            use NumericalFailure as N;
            Failure::Numerical(match c::unsigned(&f[1])? {
                1 => N::InvalidInput,
                2 => N::Expired,
                3 => N::UndefinedGreeks,
                4 => N::PriceOutOfBounds,
                5 => N::ImpliedVolatilityNotIdentifiable,
                6 => N::VolatilityNotBracketed,
                7 => N::NonConvergence,
                8 => N::NumericalFailure,
                9 => N::Interrupted(crate::runtime::Interrupted::Cancelled),
                10 => N::Interrupted(crate::runtime::Interrupted::DeadlineExpired),
                11 => N::Capacity,
                _ => return Err("Unknown numerical failure".into()),
            })
        }
        5 => Failure::Stale,
        6 => Failure::CalculationPending,
        _ => return Err("Unknown pricing failure".into()),
    })
}
fn calculation(v: &Calculation) -> V {
    V::Array(vec![
        c::wide(v.context),
        c::wide(v.source_generation),
        v.at.into(),
        quote(&v.underlying),
        quote(&v.option),
        v.iv_event.map_or(V::Nil, Into::into),
        optional(v.iv.as_ref(), iv),
        v.time.to_bits().into(),
        optional(v.selection.as_ref(), |v| {
            V::Array(vec![v.price.to_bits().into(), v.delta.to_bits().into()])
        }),
        optional(v.risk.as_ref(), risk),
        optional(v.failure.as_ref(), failure),
    ])
}
fn read_calculation(v: &V) -> Result<Calculation, String> {
    let f = c::fields(v, 11)?;
    Ok(Calculation {
        context: c::read_wide(&f[0])?,
        source_generation: c::read_wide(&f[1])?,
        at: c::number(&f[2])?,
        underlying: read_quote(&f[3])?,
        option: read_quote(&f[4])?,
        iv_event: read_optional(&f[5], c::number)?,
        iv: read_optional(&f[6], read_iv)?,
        time: f64::from_bits(c::unsigned(&f[7])?),
        selection: read_optional(&f[8], |v| {
            let p = c::fields(v, 2)?;
            Ok(PriceDelta {
                price: f64::from_bits(c::unsigned(&p[0])?),
                delta: f64::from_bits(c::unsigned(&p[1])?),
            })
        })?,
        risk: read_optional(&f[9], read_risk)?,
        failure: read_optional(&f[10], read_failure)?,
    })
}
fn binding(e: &Entry) -> Result<V, String> {
    let t = &e.context;
    let q = &t.qualification;
    let m = &t.model;
    let qualifier = V::Array(vec![
        q.size.into(),
        q.flags.into(),
        q.generation_low.into(),
        q.generation_high.into(),
        q.at.into(),
        q.maximum_age.into(),
        q.maximum_skew.into(),
        q.maximum_lead.into(),
        q.reserved.into(),
        decimal(&q.strike),
        decimal(&q.definition_strike),
        quote(&q.underlying),
        quote(&q.option),
    ]);
    let model = V::Array(vec![
        m.forward.to_bits().into(),
        m.strike.to_bits().into(),
        m.time.to_bits().into(),
        m.rate.to_bits().into(),
        m.call.into(),
        m.american.into(),
        m.futures_style.into(),
    ]);
    let reference = match &e.reference {
        None => V::Nil,
        Some(r) => r.checkpoint(),
    };
    let manifest = optional(e.manifest.as_deref(), |m| {
        V::Array(vec![c::wide(m.identity()), V::Binary(m.bytes().to_vec())])
    });
    Ok(V::Array(vec![
        c::wide(t.identity),
        t.underlying.into(),
        qualifier,
        model,
        t.expiration.into(),
        t.days_in_year.into(),
        t.valid_until.into(),
        reference,
        manifest,
    ]))
}

fn read_binding(
    v: &V,
    budget: &Arc<super::super::manifest::Budget>,
) -> Result<
    (
        Arc<Context>,
        Option<Arc<super::super::context::Context>>,
        Option<Arc<super::super::manifest::Manifest>>,
    ),
    String,
> {
    let t = c::fields(v, 9)?;
    let q = c::fields(&t[2], 13)?;
    let small = |v: &V| -> Result<u32, String> {
        c::unsigned(v)?
            .try_into()
            .map_err(|_| "Qualifier word overflow".into())
    };
    let qualification = qualification::Request {
        size: small(&q[0])?,
        flags: small(&q[1])?,
        generation_low: c::unsigned(&q[2])?,
        generation_high: c::unsigned(&q[3])?,
        at: c::number(&q[4])?,
        maximum_age: small(&q[5])?,
        maximum_skew: small(&q[6])?,
        maximum_lead: small(&q[7])?,
        reserved: small(&q[8])?,
        strike: read_decimal(&q[9])?,
        definition_strike: read_decimal(&q[10])?,
        underlying: read_quote(&q[11])?,
        option: read_quote(&q[12])?,
    };
    let m = c::fields(&t[3], 7)?;
    let model = Request {
        forward: f64::from_bits(c::unsigned(&m[0])?),
        strike: f64::from_bits(c::unsigned(&m[1])?),
        time: f64::from_bits(c::unsigned(&m[2])?),
        rate: f64::from_bits(c::unsigned(&m[3])?),
        call: c::boolean(&m[4])?,
        american: c::boolean(&m[5])?,
        futures_style: c::boolean(&m[6])?,
    };
    let context = Context {
        identity: c::read_wide(&t[0])?,
        underlying: small(&t[1])?,
        qualification,
        model,
        expiration: c::number(&t[4])?,
        days_in_year: c::unsigned(&t[5])?
            .try_into()
            .map_err(|_| "Year denominator overflow")?,
        valid_until: c::number(&t[6])?,
    };
    context.validate().map_err(str::to_owned)?;
    let reference =
        read_optional(&t[7], super::super::context::Context::restore_checkpoint)?.map(Arc::new);
    let manifest = read_optional(&t[8], |v| {
        let m = c::fields(v, 2)?;
        let bytes = m[1].as_slice().ok_or("Manifest bytes invalid")?;
        budget
            .retain(c::read_wide(&m[0])?, bytes)
            .map_err(str::to_owned)
    })?;
    Ok((Arc::new(context), reference, manifest))
}

impl Coalesced {
    pub(crate) fn from_checkpoint(value: &V) -> Result<Self, String> {
        let f = c::fields(value, 16)?;
        let maximum = c::unsigned(&f[1])?
            .try_into()
            .map_err(|_| "Pricing maximum overflow")?;
        let batch = c::unsigned(&f[2])?
            .try_into()
            .map_err(|_| "Pricing batch overflow")?;
        let ticks = c::number(&f[3])?;
        if ticks % 10000 != 0 {
            return Err("Pricing cadence invalid".into());
        }
        let mut owner = Self::with_policy(
            maximum,
            batch,
            (ticks / 10000)
                .try_into()
                .map_err(|_| "Pricing cadence overflow")?,
            "checkpoint-original-policy",
        )
        .map_err(str::to_owned)?;
        let policy = c::fields(&f[4], 4)?;
        for (i, v) in policy.iter().enumerate() {
            owner.policy[i] = c::unsigned(v)?;
        }
        owner.restore_checkpoint(value)?;
        Ok(owner)
    }

    pub(crate) fn checkpoint(&self) -> Result<V, String> {
        let _selection = self
            .selection_pass
            .try_lock()
            .map_err(|_| "Pricing selection pass active at checkpoint")?;
        let _risk = self
            .risk_pass
            .try_lock()
            .map_err(|_| "Pricing risk pass active at checkpoint")?;
        if self
            .reference_incident
            .lock()
            .map_err(|_| "Reference recovery interrupted")?
            .is_some()
        {
            return Err("Active reference recovery cannot certify a healthy checkpoint".into());
        }
        let s = self
            .state
            .lock()
            .map_err(|_| "Pricing writer interrupted")?;
        let rows = s
            .entries
            .iter()
            .map(|(id, e)| {
                Ok(V::Array(vec![
                    (*id).into(),
                    binding(e)?,
                    c::wide(e.physical),
                    optional(e.quote.as_ref(), quote),
                    e.observed
                        .map_or(V::Nil, |(a, b)| V::Array(vec![a.into(), b.into()])),
                    optional(e.iv.as_ref(), iv),
                    optional(e.selection.as_deref(), calculation),
                    optional(e.risk.as_deref(), calculation),
                    e.required.into(),
                ]))
            })
            .collect::<Result<Vec<_>, String>>()?;
        let memberships = s.memberships.as_ref().map_or(V::Nil, |v| {
            V::Array(
                v.iter()
                    .map(|ids| V::Array(ids.iter().map(|id| (*id).into()).collect()))
                    .collect(),
            )
        });
        let validation = s.reference_validation.as_ref().map_or(V::Nil, |v| {
            V::Array(vec![
                v.checked_at.into(),
                v.reuse_until.into(),
                v.valid_until.into(),
            ])
        });
        Ok(V::Array(vec![
            1.into(),
            (self.maximum as u64).into(),
            (self.batch as u64).into(),
            self.iv_ticks.into(),
            V::Array(self.policy.iter().map(|v| (*v).into()).collect()),
            self.fenced
                .load(std::sync::atomic::Ordering::Acquire)
                .into(),
            V::Array(rows),
            memberships,
            validation,
            (s.selection_cursor as u64).into(),
            (s.risk_cursor as u64).into(),
            s.observations.into(),
            s.selections.into(),
            s.solves.into(),
            (*self.changes.borrow()).into(),
            (*self.reference_updates.borrow()).into(),
        ]))
    }
    pub(crate) fn restore_checkpoint(&self, value: &V) -> Result<(), String> {
        let _selection = self
            .selection_pass
            .try_lock()
            .map_err(|_| "Pricing selection pass active during restore")?;
        let _risk = self
            .risk_pass
            .try_lock()
            .map_err(|_| "Pricing risk pass active during restore")?;
        let f = c::fields(value, 16)?;
        let policy = V::Array(self.policy.iter().map(|v| (*v).into()).collect());
        if c::unsigned(&f[0])? != 1
            || c::unsigned(&f[1])? != self.maximum as u64
            || c::unsigned(&f[2])? != self.batch as u64
            || c::number(&f[3])? != self.iv_ticks
            || f[4] != policy
        {
            return Err("Pricing checkpoint policy mismatch".into());
        }
        let mut s = self
            .state
            .lock()
            .map_err(|_| "Pricing writer interrupted")?;
        let rows = f[6]
            .as_array()
            .filter(|r| {
                (s.entries.is_empty() || r.len() == s.entries.len()) && r.len() <= self.maximum
            })
            .ok_or("Pricing checkpoint registration mismatch")?;
        let mut entries = BTreeMap::new();
        let budget =
            super::super::manifest::Budget::new(16 * 1024 * 1024).map_err(str::to_owned)?;
        for row in rows {
            let e = c::fields(row, 9)?;
            let id = c::unsigned(&e[0])?
                .try_into()
                .map_err(|_| "Pricing instrument overflow")?;
            if let Some(original) = s.entries.get(&id) {
                if binding(original)? != e[1] {
                    return Err(
                        "Original immutable pricing context does not match checkpoint".into(),
                    );
                }
            }
            let (context, reference, manifest) = read_binding(&e[1], &budget)?;
            let restored = Entry {
                reference,
                manifest,
                context,
                physical: c::read_wide(&e[2])?,
                quote: read_optional(&e[3], read_quote)?,
                observed: read_optional(&e[4], |v| {
                    let t = c::fields(v, 2)?;
                    Ok((c::number(&t[0])?, c::number(&t[1])?))
                })?,
                iv: read_optional(&e[5], read_iv)?,
                selection: read_optional(&e[6], |v| read_calculation(v).map(Arc::new))?,
                risk: read_optional(&e[7], |v| read_calculation(v).map(Arc::new))?,
                required: c::boolean(&e[8])?,
            };
            if entries.insert(id, restored).is_some() {
                return Err("Duplicate pricing instrument".into());
            }
        }
        let memberships = read_optional(&f[7], |v| {
            let lists = c::fields(v, 2)?;
            let mut result = Vec::new();
            for list in lists {
                let ids = list
                    .as_array()
                    .filter(|v| v.len() <= self.maximum)
                    .ok_or("Pricing membership bound")?
                    .iter()
                    .map(|v| {
                        c::unsigned(v)?
                            .try_into()
                            .map_err(|_| "Pricing member overflow".into())
                    })
                    .collect::<Result<Vec<u32>, String>>()?;
                if ids.iter().any(|id| !entries.contains_key(id))
                    || ids.windows(2).any(|p| p[0] >= p[1])
                {
                    return Err("Pricing membership invalid".into());
                }
                result.push(Arc::new(ids));
            }
            Ok([result.remove(0), result.remove(0)])
        })?;
        let validation = read_optional(&f[8], |v| {
            let p = c::fields(v, 3)?;
            Ok(ReferenceValidation {
                checked_at: c::number(&p[0])?,
                reuse_until: c::number(&p[1])?,
                valid_until: c::number(&p[2])?,
            })
        })?;
        let cursor = |v: &V| -> Result<usize, String> {
            c::unsigned(v)?
                .try_into()
                .map_err(|_| "Pricing cursor overflow".into())
        };
        let restored = State {
            entries,
            memberships,
            reference_validation: validation,
            selection_cursor: cursor(&f[9])?,
            risk_cursor: cursor(&f[10])?,
            observations: c::unsigned(&f[11])?,
            selections: c::unsigned(&f[12])?,
            solves: c::unsigned(&f[13])?,
        };
        let fenced = c::boolean(&f[5])?;
        let changes = c::unsigned(&f[14])?;
        let references = c::unsigned(&f[15])?;
        *s = restored;
        self.fenced
            .store(fenced, std::sync::atomic::Ordering::Release);
        self.changes.send_replace(changes);
        self.reference_updates.send_replace(references);
        Ok(())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    fn engine() -> Coalesced {
        let engine = Coalesced::new(1, 1, 5000).unwrap();
        let strike = qualification::Decimal {
            bits: [5000, 0, 0, 0],
        };
        engine
            .register(
                1,
                17,
                Context {
                    identity: 21,
                    underlying: 9,
                    expiration: 100_000,
                    days_in_year: 365,
                    valid_until: 90_000,
                    model: Request {
                        forward: 0.,
                        strike: 5000.,
                        time: 0.,
                        rate: 0.04,
                        call: true,
                        american: false,
                        futures_style: false,
                    },
                    qualification: qualification::Request {
                        size: 304,
                        flags: 3 | 4 | 8 | 16,
                        generation_low: 1,
                        maximum_age: 5000,
                        maximum_skew: 2000,
                        strike,
                        definition_strike: strike,
                        ..Default::default()
                    },
                },
                true,
            )
            .unwrap();
        engine
    }
    #[test]
    fn checkpoint_retains_original_iv_risk_failure_cursors_and_refuses_context_changes() {
        let source = engine();
        let quote = Quote {
            generation_low: 17,
            event: 100,
            received: 101,
            sequence: 7,
            bid: qualification::Decimal {
                bits: [12, 0, 0, 0],
            },
            ask: qualification::Decimal {
                bits: [13, 0, 0, 0],
            },
            ..Default::default()
        };
        let iv = Iv {
            context: 21,
            event: 100,
            volatility: 0.23,
            underlying: quote,
            option: quote,
            at: 102,
        };
        let calc = Calculation {
            context: 21,
            source_generation: 17,
            at: 103,
            underlying: quote,
            option: quote,
            iv_event: Some(100),
            iv: Some(iv),
            time: 0.01,
            selection: Some(PriceDelta {
                price: 12.5,
                delta: -0.0,
            }),
            risk: Some(Values {
                price: 12.5,
                volatility: 0.23,
                delta: 0.2,
                gamma: 0.001,
                vega: 8.,
                theta: -1.,
                rho: 0.4,
            }),
            failure: Some(Failure::Numerical(NumericalFailure::Interrupted(
                crate::runtime::Interrupted::DeadlineExpired,
            ))),
        };
        {
            let mut s = source.state.lock().unwrap();
            let e = s.entries.get_mut(&1).unwrap();
            e.quote = Some(quote);
            e.observed = Some((100, 7));
            e.iv = Some(iv);
            e.selection = Some(Arc::new(calc.clone()));
            e.risk = Some(Arc::new(calc));
            s.selection_cursor = 1;
            s.risk_cursor = 2;
            s.observations = 19;
            s.selections = 20;
            s.solves = 21;
            s.memberships = Some([Arc::new(vec![1]), Arc::new(vec![1])]);
            s.reference_validation = Some(ReferenceValidation {
                checked_at: 101,
                reuse_until: 103,
                valid_until: 90000,
            });
        }
        let value = source.checkpoint().unwrap();
        let from_original = Coalesced::from_checkpoint(&value).unwrap();
        assert_eq!(value, from_original.checkpoint().unwrap());
        let cold = engine();
        cold.restore_checkpoint(&value).unwrap();
        assert_eq!(value, cold.checkpoint().unwrap());
        {
            let mut s = cold.state.lock().unwrap();
            let e = s.entries.get_mut(&1).unwrap();
            let mut context = (*e.context).clone();
            context.model.rate = 0.05;
            e.context = Arc::new(context);
        }
        assert!(cold.restore_checkpoint(&value).is_err());
        let busy = source.selection_pass.try_lock().unwrap();
        assert!(source.checkpoint().is_err());
        drop(busy);
        *source.reference_incident.lock().unwrap() = Some(Deadline::recovery());
        assert!(source.checkpoint().is_err());
    }
    #[test]
    fn every_existing_failure_roundtrips_without_default_or_enum_drift() {
        use NumericalFailure as N;
        let failures = [
            N::InvalidInput,
            N::Expired,
            N::UndefinedGreeks,
            N::PriceOutOfBounds,
            N::ImpliedVolatilityNotIdentifiable,
            N::VolatilityNotBracketed,
            N::NonConvergence,
            N::NumericalFailure,
            N::Interrupted(crate::runtime::Interrupted::Cancelled),
            N::Interrupted(crate::runtime::Interrupted::DeadlineExpired),
            N::Capacity,
        ];
        for f in failures {
            let f = Failure::Numerical(f);
            assert_eq!(read_failure(&failure(&f)).unwrap(), f);
        }
        let f = Failure::Quote(qualification::Result {
            size: 16,
            failure: 5,
            input: 1,
            reserved: 0,
        });
        assert_eq!(read_failure(&failure(&f)).unwrap(), f);
        assert!(read_failure(&V::Array(vec![99.into(), V::Nil])).is_err());
    }
}
