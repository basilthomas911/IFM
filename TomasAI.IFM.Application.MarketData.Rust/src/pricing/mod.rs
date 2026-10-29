//! Futures-option engines with unchanged C# operation order and numerical policy.
//! Calculations use API-supplied year fractions and original absolute deadlines.
//! American trees yield every 32 levels; no blocking worker or background DLL is used.
mod black76;
mod legacy_black76;
use crate::runtime::{Deadline, Interrupted};
use tokio_util::sync::CancellationToken;
/// Per-option frozen numerical inputs; only qualified futures conventions enter here.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Request {
    /// Futures price.
    pub forward: f64,
    /// Strike price.
    pub strike: f64,
    /// API-qualified year fraction.
    pub time: f64,
    /// Continuous annual rate.
    pub rate: f64,
    /// Call when true, put otherwise.
    pub call: bool,
    /// American when true, European otherwise.
    pub american: bool,
    /// Futures-style premium when true; paid upfront otherwise.
    pub futures_style: bool,
}
impl Request {
    fn sign(self) -> i32 {
        if self.call { 1 } else { -1 }
    }
    fn discount_rate(self) -> f64 {
        if self.futures_style { 0.0 } else { self.rate }
    }
}
/// Full per-option result in existing units (annual theta, unit vega/rho).
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Values {
    /// Model price.
    pub price: f64,
    /// Volatility used.
    pub volatility: f64,
    /// Forward delta.
    pub delta: f64,
    /// Forward gamma.
    pub gamma: f64,
    /// Unit volatility sensitivity.
    pub vega: f64,
    /// Annual theta.
    pub theta: f64,
    /// Unit rate sensitivity.
    pub rho: f64,
}
/// Cheap selection price/delta result, separate from a complete display risk pass.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct PriceDelta {
    /// Model price.
    pub price: f64,
    /// Forward delta.
    pub delta: f64,
}
/// IV-only convergence evidence from the current unified solver.
#[derive(Debug, Clone, Copy, PartialEq)]
pub struct Solution {
    /// Solved volatility.
    pub volatility: f64,
    /// Last model repricing.
    pub price: f64,
    /// Model price minus observed market mark.
    pub error: f64,
    /// One-based bisection iterations.
    pub iterations: u32,
}
/// Existing numerical failure names, plus explicit lifecycle/allocation outcomes.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Failure {
    /// Inputs outside finite policy.
    InvalidInput,
    /// Negative expiry time.
    Expired,
    /// Zero time/volatility has no smooth complete Greek vector.
    UndefinedGreeks,
    /// Model-arbitrage bounds violated.
    PriceOutOfBounds,
    /// IV is not identifiable at expiry/intrinsic value.
    ImpliedVolatilityNotIdentifiable,
    /// IV numerical interval cannot bracket the price.
    VolatilityNotBracketed,
    /// Bisection exhausted its iterations.
    NonConvergence,
    /// A model intermediate/result is nonfinite.
    NumericalFailure,
    /// Cancellation or original deadline expiry.
    Interrupted(Interrupted),
    /// Fallible buffer reservation failed.
    Capacity,
}
/// Existing futures numerical policy, frozen in result provenance at the facade.
#[derive(Debug, Clone, Copy)]
pub struct Settings {
    /// CRR levels (32..4096).
    pub steps: usize,
    /// IV iterations (1..256).
    pub iterations: usize,
    /// Maximum volatility (0,10].
    pub maximum_volatility: f64,
    /// Absolute repricing tolerance (0,.01].
    pub tolerance: f64,
}
impl Default for Settings {
    fn default() -> Self {
        Self {
            steps: 801,
            iterations: 100,
            maximum_volatility: 4.0,
            tolerance: 1e-8,
        }
    }
}
/// Bounded calculator owning only immutable settings; each tree owns its scratch.
pub struct Calculator {
    settings: Settings,
    kernel: EuropeanKernel,
}
/// Existing selectable European-pricer arithmetic profiles. Both are native here;
/// this choice preserves the configured current backend's numerical operation order.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum EuropeanKernel {
    /// Current managed formula's explicit fused multiply-add sites.
    ManagedCompatibility,
    /// Current legacy Rust formula without those explicit FMA sites.
    LegacyRustCompatibility,
}
fn max(a: f64, b: f64) -> f64 {
    if a.is_nan() || b.is_nan() {
        f64::NAN
    } else if a > b {
        a
    } else {
        b
    }
}
impl Calculator {
    /// Validates numerical policy before accepting any work.
    pub fn new(s: Settings) -> Result<Self, Failure> {
        if !(32..=4096).contains(&s.steps)
            || !(1..=256).contains(&s.iterations)
            || !s.maximum_volatility.is_finite()
            || s.maximum_volatility <= 0.0
            || s.maximum_volatility > 10.0
            || !s.tolerance.is_finite()
            || s.tolerance <= 0.0
            || s.tolerance > 0.01
        {
            return Err(Failure::InvalidInput);
        }
        Ok(Self {
            settings: s,
            kernel: EuropeanKernel::ManagedCompatibility,
        })
    }
    /// Selects an existing compatibility profile without changing numerical policy.
    pub fn with_kernel(settings: Settings, kernel: EuropeanKernel) -> Result<Self, Failure> {
        let mut value = Self::new(settings)?;
        value.kernel = kernel;
        Ok(value)
    }
    /// Existing semantic engine version required by captured pricing contexts.
    pub fn engine_version(&self, r: Request) -> &'static str {
        if r.american {
            "AmericanFutures.CRR.Managed/v1"
        } else {
            match self.kernel {
                EuropeanKernel::ManagedCompatibility => "Black76.Managed/v1",
                EuropeanKernel::LegacyRustCompatibility => "Black76.Rust/v1",
            }
        }
    }
    fn validate(&self, r: Request) -> Result<(), Failure> {
        if !r.forward.is_finite()
            || r.forward <= 0.0
            || !r.strike.is_finite()
            || r.strike <= 0.0
            || !r.time.is_finite()
            || !r.rate.is_finite()
        {
            return Err(Failure::InvalidInput);
        }
        if r.time < 0.0 {
            return Err(Failure::Expired);
        }
        if r.time > 100.0 || (r.rate * r.time).abs() > 100.0 {
            return Err(Failure::InvalidInput);
        }
        Ok(())
    }
    fn sigma(&self, s: f64) -> Result<(), Failure> {
        if !s.is_finite() || s < 0.0 || s > self.settings.maximum_volatility {
            Err(Failure::InvalidInput)
        } else {
            Ok(())
        }
    }
    async fn core(
        &self,
        r: Request,
        sigma: f64,
        token: &CancellationToken,
        buffer: &mut Vec<f64>,
    ) -> Result<(f64, f64, f64), Failure> {
        if token.is_cancelled() {
            return Err(Failure::Interrupted(Interrupted::Cancelled));
        }
        if !r.american {
            return Ok((
                match self.kernel {
                    EuropeanKernel::ManagedCompatibility => black76::price,
                    EuropeanKernel::LegacyRustCompatibility => legacy_black76::price,
                }(
                    r.forward,
                    r.strike,
                    r.discount_rate(),
                    sigma,
                    r.time,
                    r.sign(),
                ),
                0.0,
                0.0,
            ));
        }
        let n = self.settings.steps;
        if r.forward.ln().abs() + sigma * (r.time * n as f64).sqrt() > 650.0 {
            return Ok((f64::NAN, 0.0, 0.0));
        }
        let dt = r.time / n as f64;
        let u = (sigma * dt.sqrt()).exp();
        let d = 1.0 / u;
        let prob = (1.0 - d) / (u - d);
        let df = (-r.discount_rate() * dt).exp();
        if !prob.is_finite() || !(0.0..=1.0).contains(&prob) {
            return Ok((f64::NAN, 0.0, 0.0));
        }
        if buffer.capacity() < n + 1 {
            buffer
                .try_reserve_exact(n + 1 - buffer.len())
                .map_err(|_| Failure::Capacity)?;
        }
        buffer.resize(n + 1, 0.0);
        let mut s = r.forward * d.powf(n as f64);
        let ratio = u * u;
        for value in buffer.iter_mut() {
            *value = max(f64::from(r.sign()) * (s - r.strike), 0.0);
            s *= ratio;
        }
        let (mut delta, mut gamma) = (0.0, 0.0);
        for i in (0..n).rev() {
            if i & 31 == 0 {
                tokio::task::yield_now().await;
                if token.is_cancelled() {
                    return Err(Failure::Interrupted(Interrupted::Cancelled));
                }
            }
            s = r.forward * d.powf(i as f64);
            for j in 0..=i {
                buffer[j] = max(
                    df * ((1.0 - prob) * buffer[j] + prob * buffer[j + 1]),
                    max(f64::from(r.sign()) * (s - r.strike), 0.0),
                );
                s *= ratio;
            }
            if i == 2 {
                let up = (buffer[2] - buffer[1]) / (r.forward * (u * u - 1.0));
                let down = (buffer[1] - buffer[0]) / (r.forward * (1.0 - d * d));
                gamma = (up - down) / (r.forward * (u * u - d * d) / 2.0);
            }
            if i == 1 {
                delta = (buffer[1] - buffer[0]) / (r.forward * (u - d));
            }
        }
        Ok((buffer[0], delta, gamma))
    }
    /// Price-only operation includes expiry and zero-volatility boundary values.
    pub async fn theoretical(
        &self,
        r: Request,
        sigma: f64,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<f64, Failure> {
        deadline
            .run(token, async {
                let mut scratch = Vec::new();
                self.validate(r)?;
                self.sigma(sigma)?;
                let intrinsic = max(f64::from(r.sign()) * (r.forward - r.strike), 0.0);
                let price = if r.time == 0.0 {
                    intrinsic
                } else if sigma == 0.0 {
                    let p = (-r.discount_rate() * r.time).exp() * intrinsic;
                    if r.american { max(p, intrinsic) } else { p }
                } else {
                    self.core(r, sigma, token, &mut scratch).await?.0
                };
                if price.is_finite() {
                    Ok(price)
                } else {
                    Err(Failure::NumericalFailure)
                }
            })
            .await
            .map_err(Failure::Interrupted)?
    }
    async fn greeks(
        &self,
        r: Request,
        sigma: f64,
        token: &CancellationToken,
    ) -> Result<Values, Failure> {
        self.validate(r)?;
        self.sigma(sigma)?;
        if r.time == 0.0 || sigma == 0.0 {
            return Err(Failure::UndefinedGreeks);
        }
        let v = if !r.american {
            let g = match self.kernel {
                EuropeanKernel::ManagedCompatibility => black76::price_with_greeks,
                EuropeanKernel::LegacyRustCompatibility => legacy_black76::price_with_greeks,
            }(
                r.forward,
                r.strike,
                r.discount_rate(),
                sigma,
                r.time,
                r.sign(),
            )?;
            Values {
                price: g.price,
                volatility: sigma,
                delta: g.delta,
                gamma: g.gamma,
                vega: g.vega,
                theta: g.theta,
                rho: if r.futures_style { 0.0 } else { g.rho },
            }
        } else {
            let mut scratch = Vec::new();
            let core = self.core(r, sigma, token, &mut scratch).await?;
            if !core.0.is_finite() {
                return Err(Failure::NumericalFailure);
            }
            let dv = 1e-4_f64.min(sigma / 2.0);
            let dr = 1e-5;
            let dt = 1e-5_f64.min(r.time / 2.0);
            let vega = (self.core(r, sigma + dv, token, &mut scratch).await?.0
                - self.core(r, sigma - dv, token, &mut scratch).await?.0)
                / (2.0 * dv);
            let rho = (self
                .core(
                    Request {
                        rate: r.rate + dr,
                        ..r
                    },
                    sigma,
                    token,
                    &mut scratch,
                )
                .await?
                .0
                - self
                    .core(
                        Request {
                            rate: r.rate - dr,
                            ..r
                        },
                        sigma,
                        token,
                        &mut scratch,
                    )
                    .await?
                    .0)
                / (2.0 * dr);
            let theta = (self
                .core(
                    Request {
                        time: r.time - dt,
                        ..r
                    },
                    sigma,
                    token,
                    &mut scratch,
                )
                .await?
                .0
                - self
                    .core(
                        Request {
                            time: r.time + dt,
                            ..r
                        },
                        sigma,
                        token,
                        &mut scratch,
                    )
                    .await?
                    .0)
                / (2.0 * dt);
            Values {
                price: core.0,
                volatility: sigma,
                delta: core.1,
                gamma: core.2,
                vega,
                theta,
                rho,
            }
        };
        if [
            v.price,
            v.volatility,
            v.delta,
            v.gamma,
            v.vega,
            v.theta,
            v.rho,
        ]
        .iter()
        .all(|x| x.is_finite())
        {
            Ok(v)
        } else {
            Err(Failure::NumericalFailure)
        }
    }
    /// Complete Greeks from the existing European or American futures model.
    pub async fn price(
        &self,
        r: Request,
        sigma: f64,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Values, Failure> {
        deadline
            .run(token, self.greeks(r, sigma, token))
            .await
            .map_err(Failure::Interrupted)?
    }
    /// Model-consistent IV-only bisection with convergence evidence and no risk postpass.
    pub async fn solve(
        &self,
        r: Request,
        market: f64,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Solution, Failure> {
        deadline
            .run(token, async {
                let mut scratch = Vec::new();
                self.validate(r)?;
                if !market.is_finite() || market < 0.0 {
                    return Err(Failure::InvalidInput);
                }
                if r.time == 0.0 {
                    return Err(Failure::ImpliedVolatilityNotIdentifiable);
                }
                let discount = (-r.discount_rate() * r.time).exp();
                let intrinsic = max(f64::from(r.sign()) * (r.forward - r.strike), 0.0);
                let mut lower = discount * intrinsic;
                if r.american {
                    lower = max(lower, intrinsic);
                }
                let upper = if r.call {
                    r.forward * max(1.0, discount)
                } else {
                    r.strike * max(1.0, discount)
                };
                let tolerance = self.settings.tolerance;
                if market < lower - tolerance || market > upper + tolerance {
                    return Err(Failure::PriceOutOfBounds);
                }
                if market <= lower + tolerance {
                    return Err(Failure::ImpliedVolatilityNotIdentifiable);
                }
                let mut lo = 1e-7;
                let ceiling = self.settings.maximum_volatility;
                let mut hi = ceiling.min(max(0.25, lo * 2.0));
                let lp = self.core(r, lo, token, &mut scratch).await?.0;
                let mut hp = self.core(r, hi, token, &mut scratch).await?.0;
                while hp.is_finite() && hp < market - tolerance && hi < ceiling {
                    hi = ceiling.min(hi * 2.0);
                    hp = self.core(r, hi, token, &mut scratch).await?.0;
                }
                if !lp.is_finite() || !hp.is_finite() {
                    return Err(Failure::NumericalFailure);
                }
                if market < lp - tolerance || market > hp + tolerance {
                    return Err(Failure::VolatilityNotBracketed);
                }
                for iteration in 0..self.settings.iterations {
                    tokio::task::yield_now().await;
                    let mid = (lo + hi) / 2.0;
                    let p = self.core(r, mid, token, &mut scratch).await?.0;
                    if !p.is_finite() {
                        return Err(Failure::NumericalFailure);
                    }
                    if (p - market).abs() <= tolerance {
                        return Ok(Solution {
                            volatility: mid,
                            price: p,
                            error: p - market,
                            iterations: iteration as u32 + 1,
                        });
                    }
                    if p < market {
                        lo = mid;
                    } else {
                        hi = mid;
                    }
                }
                Err(Failure::NonConvergence)
            })
            .await
            .map_err(Failure::Interrupted)?
    }
    /// Model-consistent IV solve followed by the existing full-Greek risk pass.
    /// Both operations consume the same original deadline, never separate timers.
    pub async fn implied(
        &self,
        r: Request,
        market: f64,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<Values, Failure> {
        let solved = self.solve(r, market, token, deadline).await?;
        self.price(r, solved.volatility, token, deadline).await
    }
    /// Current fast price/delta selection path, with no IV solve or sensitivity
    /// bumps. American trees still yield and honor the original cancellation.
    pub async fn price_delta(
        &self,
        r: Request,
        sigma: f64,
        token: &CancellationToken,
        deadline: Deadline,
    ) -> Result<PriceDelta, Failure> {
        deadline
            .run(token, async {
                self.validate(r)?;
                self.sigma(sigma)?;
                if r.time == 0.0 || sigma == 0.0 {
                    return Err(Failure::UndefinedGreeks);
                }
                let (price, delta) = if r.american {
                    let mut scratch = Vec::new();
                    let core = self.core(r, sigma, token, &mut scratch).await?;
                    (core.0, core.1)
                } else {
                    match self.kernel {
                        EuropeanKernel::ManagedCompatibility => black76::price_delta(r, sigma),
                        EuropeanKernel::LegacyRustCompatibility => {
                            let value = legacy_black76::price_with_greeks(
                                r.forward,
                                r.strike,
                                r.discount_rate(),
                                sigma,
                                r.time,
                                r.sign(),
                            )?;
                            (value.price, value.delta)
                        }
                    }
                };
                if !price.is_finite() || !delta.is_finite() {
                    Err(Failure::NumericalFailure)
                } else {
                    Ok(PriceDelta { price, delta })
                }
            })
            .await
            .map_err(Failure::Interrupted)?
    }
}

/// Managed-compatible fixed-point quote preparation.
pub mod midpoint;
