//! Unpatched registry SDK + frozen bridge serde_json version. No endpoint,
//! credential, child process or provider network request is used by this oracle.
fn main(){
    let mut rows=Vec::new();
    for source in ["0","-0","1","1.0","0.00000012345678901234567","0.12345678901234567","1e-20"] {
        let job=format!(r#"{{"id":"job-123","dataset":"GLBX.MDP3","symbols":["ESZ6"],"stype_in":"raw_symbol","stype_out":"instrument_id","schema":"trades","start":"2026-10-26T00:00:00Z","end":"2026-10-27T00:00:00Z","encoding":"dbn","compression":"zstd","pretty_px":false,"pretty_ts":false,"map_symbols":false,"split_symbols":false,"split_duration":"month","delivery":"download","state":"done","cost_usd":{source},"record_count":5000,"billed_size":1234,"ts_received":"2026-10-26T00:00:01Z","ts_queued":null,"ts_process_start":null,"ts_process_done":null,"ts_expiration":null}}"#);
        let parsed:databento::historical::batch::BatchJob=serde_json::from_str(&job).unwrap();
        let cost=parsed.cost_usd.unwrap_or(0.0);
        rows.push(serde_json::json!({"source":source,"jobInput":job,"costLiteral":cost.to_string(),"costBits":cost.to_bits()}));
    }
    std::fs::write(std::env::args().nth(1).expect("oracle output path"),serde_json::to_vec_pretty(&rows).unwrap()).unwrap();
}
