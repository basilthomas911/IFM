//! Unpatched registry DBN oracle. This independent Cargo root intentionally has
//! no path overrides. Writes a bounded valid-input metadata reference corpus.
use dbn::{Metadata, MetadataBuilder, MappingInterval, SymbolMapping, SType, Schema,
    decode::dbn::MetadataDecoder, encode::dbn::AsyncMetadataEncoder, VersionUpgradePolicy};
use std::num::NonZeroU64;
use sha2::{Digest,Sha256};
#[tokio::main(flavor="current_thread")]
async fn main() {
    let mut results=Vec::new();
    for version in 1..=3 {
        for width in if version==1 {vec![22]} else {vec![8,71,65535]} {
            for count in [0,1,1001] {
                let mut m:Metadata=MetadataBuilder::new().dataset("GLBX.MDP3").schema(Some(Schema::Trades)).stype_in(Some(SType::RawSymbol)).stype_out(SType::InstrumentId).start(123).build();
                m.version=version;m.symbol_cstr_len=width;m.ts_out=true;m.end=NonZeroU64::new(999);m.limit=NonZeroU64::new(17);
                m.symbols=(0..count).map(|i|format!("S{i}")).collect();m.partial=vec!["PARTIAL".into()];m.not_found=vec!["MISSING".into()];
                m.mappings=vec![SymbolMapping{raw_symbol:"ES".into(),intervals:vec![MappingInterval{start_date:time_date(23),end_date:time_date(26),symbol:"42".into()}]}];
                let mut bytes=Vec::new();AsyncMetadataEncoder::new(&mut bytes).encode(&m).await.unwrap();
                let expected=MetadataDecoder::with_upgrade_policy(bytes.as_slice(),VersionUpgradePolicy::AsIs).decode().unwrap();
                results.push(format!("{version}:{width}:{count}:{:x}",Sha256::digest(format!("{expected:?}").as_bytes())));
            }
        }
    }
    std::fs::write(std::env::args().nth(1).expect("output path"),results.join("\n")+"\n").unwrap();
}
fn time_date(day:u8)->time::Date {time::Date::from_calendar_date(2026,time::Month::October,day).unwrap()}
