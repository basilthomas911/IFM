//! Fixed-capacity CLR decimal arithmetic for native coverage envelopes. Six
//! 32-bit words hold the full unrounded product/aligned sum; reducing scale uses
//! original discarded digits once, with nearest-even rounding. No heap, float
//! conversion or arbitrary-precision allocation participates in these operations.
use crate::option_chain::qualification::Decimal;
#[derive(Clone, Copy, Default)]
struct Wide([u32; 6]);
impl Wide {
    fn small(&mut self, m: u32) {
        let mut carry = 0u64;
        for word in &mut self.0 {
            let n = u64::from(*word) * u64::from(m) + carry;
            *word = n as u32;
            carry = n >> 32;
        }
        debug_assert_eq!(carry, 0);
    }
    fn divide10(&mut self) -> u32 {
        let mut carry = 0u64;
        for word in self.0.iter_mut().rev() {
            let n = (carry << 32) | u64::from(*word);
            *word = (n / 10) as u32;
            carry = n % 10;
        }
        carry as u32
    }
    fn compare(&self, other: &Self) -> std::cmp::Ordering {
        self.0.iter().rev().cmp(other.0.iter().rev())
    }
    fn add(self, other: Self) -> Self {
        let mut out = Self::default();
        let mut carry = 0u64;
        for (i, word) in out.0.iter_mut().enumerate() {
            let n = u64::from(self.0[i]) + u64::from(other.0[i]) + carry;
            *word = n as u32;
            carry = n >> 32;
        }
        debug_assert_eq!(carry, 0);
        out
    }
    fn subtract(self, other: Self) -> Self {
        let mut out = Self::default();
        let mut borrow = 0i64;
        for (i, word) in out.0.iter_mut().enumerate() {
            let n = i64::from(self.0[i]) - i64::from(other.0[i]) - borrow;
            *word = n as u32;
            borrow = i64::from(n < 0);
        }
        debug_assert_eq!(borrow, 0);
        out
    }
    fn fit(self) -> Option<[u32; 3]> {
        if self.0[3..].iter().any(|v| *v != 0) {
            None
        } else {
            Some([self.0[0], self.0[1], self.0[2]])
        }
    }
    fn rounded(mut self, scale: u32, negative: bool) -> Option<Decimal> {
        let mut last = 0;
        let mut sticky = false;
        for removed in 0..=scale {
            if scale - removed <= 28 && self.fit().is_some() {
                let round = last > 5 || last == 5 && (sticky || self.0[0] & 1 != 0);
                let rounded = if round {
                    self.add(Self([1, 0, 0, 0, 0, 0]))
                } else {
                    self
                };
                if let Some(words) = rounded.fit() {
                    return Some(Decimal {
                        bits: [
                            words[0],
                            words[1],
                            words[2],
                            ((scale - removed) << 16) | if negative { 0x80000000 } else { 0 },
                        ],
                    });
                }
            }
            if removed < scale {
                sticky |= last != 0;
                last = self.divide10();
            }
        }
        None
    }
}
fn parts(value: Decimal) -> Option<(Wide, u32, bool)> {
    let [lo, mid, hi, flags] = value.bits;
    let scale = (flags >> 16) & 255;
    if flags & 0x7f00ffff != 0 || scale > 28 {
        return None;
    }
    Some((Wide([lo, mid, hi, 0, 0, 0]), scale, flags >> 31 != 0))
}
/// Checked addition with CLR scale/96-bit rounding behavior.
pub fn add(left: Decimal, right: Decimal) -> Option<Decimal> {
    let (mut a, sa, na) = parts(left)?;
    let (mut b, sb, nb) = parts(right)?;
    let scale = sa.max(sb);
    for _ in sa..scale {
        a.small(10);
    }
    for _ in sb..scale {
        b.small(10);
    }
    if na == nb {
        a.add(b).rounded(scale, na)
    } else if a.compare(&b).is_lt() {
        b.subtract(a).rounded(scale, nb)
    } else {
        a.subtract(b).rounded(scale, na)
    }
}
/// Checked subtraction, without conversion through binary floating point.
pub fn subtract(left: Decimal, mut right: Decimal) -> Option<Decimal> {
    right.bits[3] ^= 0x80000000;
    add(left, right)
}
/// Checked multiplication with a complete 192-bit unrounded intermediate.
pub fn multiply(left: Decimal, right: Decimal) -> Option<Decimal> {
    let (a, sa, na) = parts(left)?;
    let (b, sb, nb) = parts(right)?;
    let mut product = Wide::default();
    for i in 0..3 {
        let mut carry = 0u64;
        for j in 0..3 {
            let n = u64::from(a.0[i]) * u64::from(b.0[j]) + u64::from(product.0[i + j]) + carry;
            product.0[i + j] = n as u32;
            carry = n >> 32;
        }
        product.0[i + 3] = carry as u32;
    }
    // CLR's narrow-operand fast path keeps scale on zero, except when its
    // guaranteed-underflow shortcut applies. Wide all-zero products normalize.
    let narrow = a.0[1..3].iter().all(|v| *v == 0) && b.0[1..3].iter().all(|v| *v == 0);
    if product.0.iter().all(|v| *v == 0) && !narrow || narrow && sa + sb > 47 {
        return Some(Decimal::default());
    }
    product.rounded(sa + sb, na != nb)
}
