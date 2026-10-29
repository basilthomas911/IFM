//! Execution continuity is verified through the production native wire encoder.
use base64::{Engine, engine::general_purpose::STANDARD};
use ifm_market_data_native::{
    dataset::{
        book::Book,
        live::{Output, OutputSource, Registration},
        records::{Header, Observation, Trade},
        transport::Lane,
    },
    latest::Asset,
    messaging::{codec, futures::Futures},
};

fn events() -> Vec<String> {
    let date = time::Date::from_calendar_date(2026, time::Month::October, 27).unwrap();
    let mut book = Book::<()>::new(date, 7, 1).unwrap();
    book.register_with_tick_size(42, "ES20261218", Asset::Futures, 4, Some(250_000_000))
        .unwrap();
    book.complete_replay(7, 42, 99).unwrap();
    let mut encoder = Futures::new(
        Lane::FuturesGlbx,
        date,
        date,
        vec![Registration {
            instrument: 42,
            publisher: 3,
            contract: "ES20261218".into(),
            source: "42".into(),
            asset: Asset::Futures,
            tick: Some(250_000_000),
            vwap: false,
            required: true,
        }],
        2,
    )
    .unwrap();
    let mut result = Vec::new();
    // Two unchanged-price executions followed by a changed price. Repeating
    // each source row also checks that duplicate inputs do not create volume.
    for (index, price) in [6_000_000_000_000, 6_000_000_000_000, 6_000_250_000_000]
        .into_iter()
        .enumerate()
    {
        let record = Observation::Trade(Trade {
            header: Header {
                instrument: 42,
                publisher: 3,
                sequence: (index + 1) as u32,
                event_ns: 1793116800000000000 + (index as i64 + 1) * 1_000_000_000,
                received_ns: 1793116800000000000 + (index as i64 + 1) * 1_000_000_000,
                flags: 0,
                schema: 0,
            },
            price,
            size: (index + 1) as u32,
            action: b'T',
            side: b'B',
            dbn_flags: 0,
            depth: 0,
            delta_ns: 0,
        });
        for duplicate in [false, true] {
            let update = book.apply(7, record, 0).unwrap();
            let output = encoder
                .process(&Output {
                    observed: 17931168040000000,
                    lane: Lane::FuturesGlbx,
                    generation: 7,
                    source: OutputSource::Observation(record),
                    state: book.publication_state(42, update),
                    trade: None,
                })
                .unwrap();
            assert_eq!(output.len(), usize::from(!duplicate));
            for event in output {
                assert_eq!(event.kind, 3);
                let fields = event.value.as_array().unwrap();
                let trade = fields[9].as_array().unwrap()[6].as_array().unwrap();
                assert_eq!(trade[9].as_i64(), Some(index as i64 + 1));
                assert_eq!(trade[1].as_u64(), Some(index as u64 + 1));
                result.push(STANDARD.encode(codec::encode(&event.value).unwrap()));
            }
        }
    }
    result
}

#[test]
fn unchanged_prices_preserve_every_execution_and_duplicates_emit_nothing() {
    assert_eq!(events().len(), 3);
}

#[test]
#[ignore = "Runs the actual C# serializer and production bar accumulator"]
fn native_wire_reaches_actual_csharp_bars_without_false_gaps_or_lost_volume() {
    let root = std::path::Path::new(env!("CARGO_MANIFEST_DIR"));
    let fixture = root.join("target/futures-bar-continuity.json");
    std::fs::write(&fixture, serde_json::to_vec(&events()).unwrap()).unwrap();
    let status = std::process::Command::new("dotnet")
        .args(["run", "--project"])
        .arg(root.join("qualification/FuturesBarBridge/FuturesBarBridge.csproj"))
        .arg("--")
        .arg(&fixture)
        .status()
        .unwrap();
    assert!(status.success());
}
