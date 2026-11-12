//! Imports the managed option feed capacity into the native crate at build time.
use std::{env, fs, path::PathBuf};

fn main() {
    // The managed contracts own the capacity shared by both sides of the wire.
    let source = "../TomasAI.IFM.Shared.Contracts/Compatibility/TomasAI.IFM.Domain.MarketData.Shared/OptionFeedCapacity.cs";
    println!("cargo:rerun-if-changed={source}");
    let declaration = fs::read_to_string(source).expect("read the shared option feed capacity");
    let maximum = declaration
        .split_once("public const int MaximumContracts = ")
        .and_then(|(_, value)| value.split_once(';'))
        .and_then(|(value, _)| value.trim().parse::<usize>().ok())
        .filter(|value| *value > 0)
        .expect("parse the shared option feed capacity");
    let output = PathBuf::from(env::var_os("OUT_DIR").expect("Cargo build output directory"));
    fs::write(
        output.join("option_feed_capacity.rs"),
        format!("/// Maximum option contracts installed in one native live generation.\npub(crate) const MAX_LIVE_OPTIONS: usize = {maximum};\n"),
    )
    .expect("write the shared option feed capacity");
}
