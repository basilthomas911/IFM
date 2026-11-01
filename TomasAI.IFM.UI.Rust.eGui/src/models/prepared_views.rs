//! Immutable view indexes and text prepared outside pointer-only painting.
use super::{broker_presentation::MarketDisplay, cells::Cells};
use std::{collections::BTreeMap, ops::Range};

/// Prepared strike rows and bounded cell text shared by paints of one immutable option chain.
#[derive(Default, Debug)]
pub struct OptionGrid {
    /// Unique strike prices in descending display order.
    pub strikes: Vec<f64>,
    /// Prepared call/put indexes in the same order as `strikes`.
    pub rows: Vec<OptionStrikeRow>,
    /// At most 1,024 formatted visible-contract entries, keyed by immutable contract index.
    text: std::sync::Mutex<BTreeMap<usize, std::sync::Arc<[String; 5]>>>,
    /// Prepared row index nearest to the published underlying price.
    pub nearest: Option<usize>,
}

/// Call/put contract indexes and strike caption for one displayed strike row.
#[derive(Debug)]
pub struct OptionStrikeRow {
    /// Index of the first matching call contract in the immutable publication.
    pub call: Option<usize>,
    /// Index of the first matching put contract in the immutable publication.
    pub put: Option<usize>,
    /// Strike text formatted once for reuse while painting.
    pub strike_text: String,
}
impl OptionGrid {
    pub const FIELDS: [&'static str; 5] = ["delta", "openInterest", "volume", "bid", "ask"];


    /// Constructs OptionGrid from the supplied inputs.
    pub fn new(market: &MarketDisplay) -> Self {
        let contracts = market.contracts.as_deref().unwrap_or_default();
        let mut strikes: Vec<_> = contracts
            .iter()
            .filter_map(|c| c.cell("strike").number())
            .collect();
        strikes.sort_by(|a, b| b.total_cmp(a));
        strikes.dedup();
        let key = |n: f64| if n == 0. { 0 } else { n.to_bits() };
        let mut index = BTreeMap::new();
        for (i, c) in contracts.iter().enumerate() {
            if let Some(strike) = c.cell("strike").number() {
                if let Some(call) = c.is_call {
                    index.entry((key(strike), call)).or_insert(i);
                }
            }
        }
        let rows = strikes
            .iter()
            .map(|s| OptionStrikeRow {
                call: index.get(&(key(*s), true)).copied(),
                put: index.get(&(key(*s), false)).copied(),
                strike_text: format!("{s:.2}"),
            })
            .collect();
        let nearest = market
            .cell("lastUnderlyingPrice")
            .number()
            .and_then(|price| {
                strikes
                    .iter()
                    .enumerate()
                    .min_by(|(_, a), (_, b)| (*a - price).abs().total_cmp(&(*b - price).abs()))
                    .map(|(i, _)| i)
            });
        Self {
            strikes,
            rows,
            text: Default::default(),
            nearest,
        }
    }


    /// Cache only requested cells. A bounded cache avoids preformatting every
    /// off-screen quote or retaining unbounded text while scrolling a large chain.
    /// The caller borrows the immutable contract from this grid's publication.
    pub fn formatted(&self, index: usize, contract: &impl Cells) -> std::sync::Arc<[String; 5]> {
        let mut text = self.text.lock().unwrap_or_else(|p| p.into_inner());
        if let Some(value) = text.get(&index) {
            return value.clone();
        }
        if text.len() >= 1024 {
            text.pop_first();
        }
        let value = std::sync::Arc::new(Self::FIELDS.map(|field| option_text(contract, field)));
        text.insert(index, value.clone());
        value
    }
}


/// Preserve the existing Blotter's fixed display format; financial inputs stay exact.
pub fn option_text(v: &impl Cells, key: &str) -> String {
    let Some(number) = v.cell(key).number() else {
        return if v.cell(key).is_missing() {
            "-".into()
        } else {
            v.cell(key).text()
        };
    };
    match key {
        "delta" => {
            if number == 0. {
                "0.000".into()
            } else {
                format!("{number:+.3}")
            }
        }
        "openInterest" | "volume" => {
            if number >= 1_000_000. {
                format!("{:.1}M", number / 1_000_000.)
            } else if number >= 1_000. {
                format!("{:.1}K", number / 1_000.)
            } else {
                format!("{number:.0}")
            }
        }
        "bid" | "ask" | "strike" => format!("{number:.2}"),
        _ => v.cell(key).text(),
    }
}

/// Offsets borrow the caller's immutable publication text; no log buffer copy.
#[derive(Default, Debug)]
pub struct LineIndex {
    /// UTF-8 byte ranges for each line; the original text remains owned by the publication.
    ranges: Vec<Range<usize>>,
}
impl LineIndex {
    /// Constructs LineIndex from the supplied inputs.
    pub fn new(text: &str) -> Self {
        let base = text.as_ptr() as usize;
        Self {
            ranges: text
                .lines()
                .map(|line| {
                    let start = line.as_ptr() as usize - base;
                    start..start + line.len()
                })
                .collect(),
        }
    }


    /// Returns the number of retained entries.
    pub fn len(&self) -> usize {
        self.ranges.len()
    }


    /// Checks is empty against the current prepared views state.
    pub fn is_empty(&self) -> bool {
        self.ranges.is_empty()
    }


    /// Use the immutable source text from which this index was prepared.
    pub fn line<'a>(&self, text: &'a str, index: usize) -> &'a str {
        &text[self.ranges[index].clone()]
    }
}

/// Owned convenience wrapper for isolated tests and benchmarks.
#[derive(Default, Debug)]
pub struct PreparedLines {
    /// Owned immutable text used by the isolated line-index wrapper.
    text: String,
    /// Line ranges prepared from exactly this owned text.
    index: LineIndex,
}
impl PreparedLines {
    /// Constructs PreparedLines from the supplied inputs.
    pub fn new(text: String) -> Self {
        let index = LineIndex::new(&text);
        Self { text, index }
    }


    /// Returns the number of retained entries.
    pub fn len(&self) -> usize {
        self.index.len()
    }


    /// Checks is empty against the current prepared views state.
    pub fn is_empty(&self) -> bool {
        self.index.is_empty()
    }


    /// Borrows the indexed line from the matching immutable output text.
    pub fn line(&self, index: usize) -> &str {
        self.index.line(&self.text, index)
    }
}

/// One immutable market section owns its indexes and bounded visible-text cache.
#[derive(Debug, Default)]
pub struct PreparedMarket {
    /// Authoritative market display record; no prices are recomputed here.
    pub value: MarketDisplay,
    /// Indexes and visible-cell text prepared from this market record.
    pub grid: OptionGrid,
}
impl PreparedMarket {
    /// Constructs PreparedMarket from the supplied inputs.
    pub fn new(value: MarketDisplay) -> Self {
        let grid = OptionGrid::new(&value);
        Self { value, grid }
    }
}
impl std::ops::Deref for PreparedMarket {
    type Target = MarketDisplay;


    /// Borrows the underlying value without transferring ownership.
    fn deref(&self) -> &MarketDisplay {
        &self.value
    }
}
impl Cells for PreparedMarket {
    /// Resolves a named display cell; unsupported columns remain missing.
    fn cell(&self, column: &str) -> super::cells::Cell<'_> {
        self.value.cell(column)
    }
}
