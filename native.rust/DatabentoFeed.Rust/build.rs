fn main() {
    println!("cargo:rerun-if-changed=exports.def");
    if std::env::var("CARGO_CFG_WINDOWS").is_ok() {
        let manifest = std::env::var("CARGO_MANIFEST_DIR").expect("CARGO_MANIFEST_DIR");
        println!("cargo:rustc-cdylib-link-arg=/DEF:{manifest}\\exports.def");
    }
}
