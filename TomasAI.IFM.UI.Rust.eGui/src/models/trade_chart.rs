//! Owned spread-chart data decoded directly from the existing MessagePack row.
//! Decimal strings become floats only at the plotting boundary, never for money
//! calculations or order submission. No JSON tree is created by this path.
use serde::Deserialize;

/// Authoritative lifetime backend series. Decimal text remains exact until plot preparation.
#[derive(Clone, Debug, Default, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TradeChartPoint {
    /// Backend UTC action timestamp, never derived from the workstation clock.
    pub at_utc: String,
    /// Published exact .NET decimal representation.
    pub net_spread: String,
    /// Optional published exact forward spread.
    pub forward_spread: Option<String>,
}

/// Dataset-independent stored chart history owned by the trade controller.
#[derive(Clone, Debug, Default)]
pub struct TradeChartSeries {
    /// Backend-authoritative inclusive range start.
    pub start_utc: String,
    /// Backend-authoritative range end.
    pub end_utc: String,
    /// Ordered, scope-checked points from the backend query.
    pub points: Vec<TradeChartPoint>,
}

impl TradeChartSeries {
    /// Decode the compiled C# array contract directly, preserving range/order checks.
    /// The schema-index parity test pins these slots to the exported contract inventory.
    pub fn from_backend(value: &rmpv::Value) -> Result<Self, String> {
        use crate::backend::contracts::{ExactDecimal, timestamp, utc_roundtrip};
        let fields = value
            .as_array()
            .filter(|v| v.len() >= 3)
            .ok_or("Invalid chart series contract")?;
        let start = timestamp(&fields[0])?;
        let end = timestamp(&fields[1])?;
        if end < start {
            return Err("Chart history has a reversed backend window".into());
        }
        let rows = fields[2]
            .as_array()
            .filter(|v| v.len() <= 100_000)
            .ok_or("Invalid bounded chart history")?;
        let mut previous = None;
        let mut points = Vec::with_capacity(rows.len());
        for row in rows {
            let row = row
                .as_array()
                .filter(|v| v.len() >= 3)
                .ok_or("Invalid chart point contract")?;
            let at = timestamp(&row[0])?;
            if at < start || at > end || previous.is_some_and(|p| at < p) {
                return Err("Chart points crossed or reordered the backend window".into());
            }
            previous = Some(at);
            let net = ExactDecimal::parse(row[1].as_str().ok_or("Missing chart spread decimal")?)?;
            let forward = if row[2].is_nil() {
                None
            } else {
                Some(
                    ExactDecimal::parse(row[2].as_str().ok_or("Invalid forward spread decimal")?)?
                        .as_str()
                        .to_owned(),
                )
            };
            points.push(TradeChartPoint {
                at_utc: utc_roundtrip(at),
                net_spread: net.as_str().to_owned(),
                forward_spread: forward,
            });
        }
        Ok(Self {
            start_utc: utc_roundtrip(start),
            end_utc: utc_roundtrip(end),
            points,
        })
    }
}

/// Borrowed native row projection, serialized directly to MessagePack.
#[derive(serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct TradeChartRow<'a> {
    /// Inclusive trade-session start.
    pub chart_start_utc: Option<&'a str>,
    /// Absent while historical hydration is still running.
    pub chart_end_utc: Option<&'a str>,
    /// Absent before the first successful database query.
    pub points: Option<&'a [TradeChartPoint]>,
}
impl<'a> From<Option<&'a TradeChartSeries>> for TradeChartRow<'a> {
    /// Converts the supplied value into this representation.
    fn from(series: Option<&'a TradeChartSeries>) -> Self {
        Self {
            chart_start_utc: series.map(|v| v.start_utc.as_str()),
            chart_end_utc: series.map(|v| v.end_utc.as_str()),
            points: series.map(|v| v.points.as_slice()),
        }
    }
}

/// Plot cells decode directly through a Serde visitor. Unlike untagged enums,
/// this does not buffer and replay every value while trying alternatives.
struct PlotCellVisitor {
    /// Whether timestamp is enabled or currently true.
    timestamp: bool,
}
impl<'de> serde::de::Visitor<'de> for PlotCellVisitor {
    type Value = Option<f64>;


    /// Describes the accepted input shape in deserialization errors.
    fn expecting(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str("a published plot cell")
    }


    /// Accepts text through this value's deserialization rules.
    fn visit_str<E: serde::de::Error>(self, value: &str) -> Result<Self::Value, E> {
        Ok(if self.timestamp {
            chrono::DateTime::parse_from_rfc3339(value)
                .ok()
                .map(|at| at.timestamp_millis() as f64 / 1000.0)
        } else {
            value.parse().ok()
        })
    }


    /// Provides visit f64 for the plot cell visitor implementation.
    fn visit_f64<E: serde::de::Error>(self, value: f64) -> Result<Self::Value, E> {
        Ok((!self.timestamp).then_some(value))
    }


    /// Accepts a signed integer through this value's deserialization rules.
    fn visit_i64<E: serde::de::Error>(self, value: i64) -> Result<Self::Value, E> {
        self.visit_f64(value as f64)
    }


    /// Accepts an unsigned integer through this value's deserialization rules.
    fn visit_u64<E: serde::de::Error>(self, value: u64) -> Result<Self::Value, E> {
        self.visit_f64(value as f64)
    }


    /// Provides visit bool for the plot cell visitor implementation.
    fn visit_bool<E: serde::de::Error>(self, _: bool) -> Result<Self::Value, E> {
        Ok(None)
    }


    /// Handles an explicit null value during deserialization.
    fn visit_unit<E: serde::de::Error>(self) -> Result<Self::Value, E> {
        Ok(None)
    }


    /// Provides visit bytes for the plot cell visitor implementation.
    fn visit_bytes<E: serde::de::Error>(self, _: &[u8]) -> Result<Self::Value, E> {
        Ok(None)
    }


    /// Reads the sequence entries through this value's deserialization rules.
    fn visit_seq<A: serde::de::SeqAccess<'de>>(self, mut seq: A) -> Result<Self::Value, A::Error> {
        while seq.next_element::<serde::de::IgnoredAny>()?.is_some() {}
        Ok(None)
    }


    /// Reads named entries through this value's deserialization rules.
    fn visit_map<A: serde::de::MapAccess<'de>>(self, mut map: A) -> Result<Self::Value, A::Error> {
        while map
            .next_entry::<serde::de::IgnoredAny, serde::de::IgnoredAny>()?
            .is_some()
        {}
        Ok(None)
    }
}

/// Float conversion is restricted to rendering coordinates, never financial state.
struct PlotNumber(
    /// Wrapped `Option<f64>` value owned by PlotNumber.
    Option<f64>,
);
impl<'de> Deserialize<'de> for PlotNumber {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: serde::Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        d.deserialize_any(PlotCellVisitor { timestamp: false })
            .map(Self)
    }
}
impl PlotNumber {
    /// Provides value for the plot number implementation.
    fn value(&self) -> Option<f64> {
        self.0
    }
}

/// Parsed backend timestamp in UTC plotting seconds; invalid cells remain absent.
struct PlotTime(
    /// Wrapped `Option<f64>` value owned by PlotTime.
    Option<f64>,
);
impl<'de> Deserialize<'de> for PlotTime {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: serde::Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        d.deserialize_any(PlotCellVisitor { timestamp: true })
            .map(Self)
    }
}
impl PlotTime {
    /// Provides seconds for the plot time implementation.
    fn seconds(&self) -> Option<f64> {
        self.0
    }
}

/// Published chart point. Unknown backend extension fields are safely ignored.
#[derive(Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
struct Point {
    /// Optional at utc; `None` represents an absent value.
    at_utc: Option<PlotTime>,
    /// Optional net spread; `None` represents an absent value.
    net_spread: Option<PlotNumber>,
    /// Optional forward spread; `None` represents an absent value.
    forward_spread: Option<PlotNumber>,
}

/// Point Record used by the trade chart component.
enum PointRecord {
    Point(Point),
    Other,
}
impl<'de> Deserialize<'de> for PointRecord {
    /// Deserializes this value while preserving the declared field and scalar rules.
    fn deserialize<D: serde::Deserializer<'de>>(d: D) -> Result<Self, D::Error> {
        /// Point Visitor used by the trade chart component.
        struct PointVisitor;
        impl<'de> serde::de::Visitor<'de> for PointVisitor {
            type Value = PointRecord;


            /// Describes the accepted input shape in deserialization errors.
            fn expecting(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
                f.write_str("a chart point map")
            }


            /// Reads named entries through this value's deserialization rules.
            fn visit_map<A: serde::de::MapAccess<'de>>(
                self,
                map: A,
            ) -> Result<Self::Value, A::Error> {
                Point::deserialize(serde::de::value::MapAccessDeserializer::new(map))
                    .map(PointRecord::Point)
            }


            /// Reads the sequence entries through this value's deserialization rules.
            fn visit_seq<A: serde::de::SeqAccess<'de>>(
                self,
                mut seq: A,
            ) -> Result<Self::Value, A::Error> {
                while seq.next_element::<serde::de::IgnoredAny>()?.is_some() {}
                Ok(PointRecord::Other)
            }


            /// Handles an explicit null value during deserialization.
            fn visit_unit<E: serde::de::Error>(self) -> Result<Self::Value, E> {
                Ok(PointRecord::Other)
            }


            /// Provides visit bool for the point visitor implementation.
            fn visit_bool<E: serde::de::Error>(self, _: bool) -> Result<Self::Value, E> {
                Ok(PointRecord::Other)
            }


            /// Accepts text through this value's deserialization rules.
            fn visit_str<E: serde::de::Error>(self, _: &str) -> Result<Self::Value, E> {
                Ok(PointRecord::Other)
            }


            /// Provides visit f64 for the point visitor implementation.
            fn visit_f64<E: serde::de::Error>(self, _: f64) -> Result<Self::Value, E> {
                Ok(PointRecord::Other)
            }


            /// Accepts a signed integer through this value's deserialization rules.
            fn visit_i64<E: serde::de::Error>(self, _: i64) -> Result<Self::Value, E> {
                Ok(PointRecord::Other)
            }


            /// Accepts an unsigned integer through this value's deserialization rules.
            fn visit_u64<E: serde::de::Error>(self, _: u64) -> Result<Self::Value, E> {
                Ok(PointRecord::Other)
            }


            /// Provides visit bytes for the point visitor implementation.
            fn visit_bytes<E: serde::de::Error>(self, _: &[u8]) -> Result<Self::Value, E> {
                Ok(PointRecord::Other)
            }
        }
        d.deserialize_any(PointVisitor)
    }
}

/// Named-field MessagePack chart-row schema already used by the current UI.
#[derive(Default, Deserialize)]
#[serde(default, rename_all = "camelCase")]
struct Chart {
    /// Full trade-session range start.
    chart_start_utc: Option<PlotTime>,
    /// Optional chart end utc; `None` represents an absent value.
    chart_end_utc: Option<PlotTime>,
    /// Optional points; `None` represents an absent value.
    points: Option<Vec<PointRecord>>,
}

/// Ready-to-render points and authoritative backend window end. Reused by frames.
#[derive(Debug, Default, PartialEq)]
pub struct PreparedTradeChart {
    /// Trade-session start in UTC seconds.
    pub start_utc_seconds: Option<f64>,
    /// UTC seconds, derived solely from the published backend timestamp.
    pub end_utc_seconds: Option<f64>,
    /// Net spread plot coordinates.
    pub net: Vec<[f64; 2]>,
    /// Forward spread plot coordinates, when present alongside a valid net point.
    pub forward: Vec<[f64; 2]>,
}
impl PreparedTradeChart {
    /// Decode one MessagePack row and prepare coordinates, without a JSON tree.
    pub fn from_messagepack(bytes: &[u8]) -> Result<Self, String> {
        let chart: Chart = crate::wire::decode(bytes)?;
        Ok(Self::prepare(chart))
    }


    /// Prepares trade chart for prepared trade chart.
    fn prepare(chart: Chart) -> Self {
        let mut result = Self {
            start_utc_seconds: chart.chart_start_utc.as_ref().and_then(PlotTime::seconds),
            end_utc_seconds: chart.chart_end_utc.as_ref().and_then(PlotTime::seconds),
            ..Default::default()
        };
        for record in chart.points.unwrap_or_default() {
            let PointRecord::Point(point) = record else {
                continue;
            };
            if let (Some(at), Some(net)) = (
                point.at_utc.as_ref().and_then(PlotTime::seconds),
                point.net_spread.as_ref().and_then(PlotNumber::value),
            ) {
                result.net.push([at, net]);
                if let Some(forward) = point.forward_spread.as_ref().and_then(PlotNumber::value) {
                    result.forward.push([at, forward]);
                }
            }
        }
        result
    }
}
