#ifndef IFM_MARKET_DATA_H
#define IFM_MARKET_DATA_H

#include <stdint.h>

/* Distinct from the legacy dbf_* ABI. Library lifetime is process-wide.
 * Handles are monotonic integer identities, never native pointers. */
#ifdef __cplusplus
extern "C" {
#endif

enum ifm_market_data_status {
    IFM_MD_OK = 0, IFM_MD_INVALID_INPUT = 1, IFM_MD_CLOSED = 2,
    IFM_MD_ABI_MISMATCH = 3, IFM_MD_CAPACITY = 4,
    IFM_MD_INTERNAL_FAILURE = 5, IFM_MD_INVALID_HANDLE = 6, IFM_MD_BUSY = 7
};

enum ifm_market_data_outcome {
    IFM_MD_SUCCEEDED = 0, IFM_MD_CANCELLED = 1, IFM_MD_DEADLINE_EXPIRED = 2,
    IFM_MD_DOMAIN_INVALID = 3, IFM_MD_TASK_FAILED = 4
};

/* Exactly 32 bytes on the supported Windows x64 target. Caller owns all memory.
 * size=32; reserved=0. value is meaningful only when outcome=SUCCEEDED. */
typedef struct ifm_market_data_completion_v1 {
    uint32_t size;
    int32_t outcome;
    uint64_t operation;
    double value;
    uint64_t reserved;
} ifm_market_data_completion_v1;

/* Copied input, exactly 80 bytes. action=0 theoretical/1 Greeks/2 IV+Greeks/
 * 3 price+delta/4 IV-only. Unrequested result fields are zero.
 * flags: 1 call, 2 American, 4 futures-style premium. profile=0 current managed
 * arithmetic or 1 current legacy Rust arithmetic. Unknown values are rejected. */
typedef struct ifm_market_data_pricing_request_v1 {
    uint32_t size, action, flags, profile;
    double forward, strike, time, rate, input;
    uint32_t steps, iterations;
    double maximum_volatility, tolerance;
} ifm_market_data_pricing_request_v1;
/* Exactly 64 bytes. failure=0 success; 1 invalid input, 2 expired, 3 undefined
 * Greeks, 4 price bounds, 5 IV unidentifiable, 6 unbracketed volatility,
 * 7 nonconvergence, 8 numerical failure, 9 interruption, 10 capacity. */
typedef struct ifm_market_data_pricing_result_v1 {
    uint32_t size, failure;
    double price, volatility, delta, gamma, vega, theta, rho;
} ifm_market_data_pricing_result_v1;

/* Live epoch configuration is exactly 40 bytes. API supplies date YYYYMMDD,
 * UTC ticks since Unix epoch and a nonzero opaque identity. No local date inference. */
typedef struct ifm_market_data_live_configuration_v1 {
    uint32_t size, date;
    int64_t utc_ticks;
    uint64_t identity_low, identity_high;
    uint32_t capacity, advancing;
} ifm_market_data_live_configuration_v1;
typedef struct ifm_market_data_control_result_v1 {
    uint32_t size, failure, length, reserved;
    uint8_t message[4096];
} ifm_market_data_control_result_v1;
typedef struct ifm_market_data_lane_status_v1 {
    uint32_t size, phase;
    uint64_t attempt, episode, attempts, generation_low, generation_high, reserved[2];
} ifm_market_data_lane_status_v1;
typedef struct ifm_market_data_last_v1 {
    uint32_t size, flags;
    uint64_t generation_low, generation_high;
    int64_t sequence, event_ticks, receive_ticks, price;
    uint32_t quantity, reserved;
    int64_t bid, ask;
    uint32_t bid_quantity, ask_quantity;
    int64_t local_ticks;
    uint32_t bid_count, ask_count;
} ifm_market_data_last_v1;
typedef struct ifm_market_data_event_v1 {
    uint32_t size, lane, kind, instrument;
    uint64_t generation_low, generation_high;
    int64_t fields[12];
} ifm_market_data_event_v1;
typedef struct ifm_market_data_diagnostic_v1 {
    uint32_t size, lane, kind, code;
    uint64_t attempt;
    uint32_t last, length;
    uint8_t message[4096];
} ifm_market_data_diagnostic_v1;
/* lane: 1 GLBX futures, 2 XCBF futures, 3 GLBX options. No process is spawned.
 * action: 0 start with copied bounded control JSON; 1 reset; 2 stop (no input).
 * Every admitted call needs completion drain AND take, including cancellation. */
int32_t ifm_market_data_live_configure_v1(uint64_t handle, const ifm_market_data_live_configuration_v1* config);
int32_t ifm_market_data_live_failure_v1(uint64_t handle, uint32_t lane, uint32_t size, ifm_market_data_control_result_v1* output);
int32_t ifm_market_data_live_clock_v1(uint64_t handle, int64_t ticks, uint32_t advancing);
int32_t ifm_market_data_live_submit_v1(uint64_t handle, uint64_t operation, uint32_t action, uint32_t lane, const uint8_t* input, uint32_t length, uint64_t budget_ms);
int32_t ifm_market_data_live_take_v1(uint64_t handle, uint64_t operation, uint32_t size, ifm_market_data_control_result_v1* output);
int32_t ifm_market_data_live_status_v1(uint64_t handle, uint32_t lane, uint32_t size, ifm_market_data_lane_status_v1* output);
int32_t ifm_market_data_live_last_v1(uint64_t handle, uint32_t lane, uint32_t kind, const uint8_t* id, uint32_t length, uint32_t size, ifm_market_data_last_v1* output);
/* Event-driven wake bits only; records/diagnostics retain separate storage.
 * Drain signals after wait returns available=1, even if completion count is zero. */
int32_t ifm_market_data_live_signals_v1(uint64_t handle, uint32_t* output);
/* capacity=0..64, count separate from output; zero permits null output. */
int32_t ifm_market_data_live_events_v1(uint64_t handle, ifm_market_data_event_v1* output, uint32_t capacity, uint32_t* count);
int32_t ifm_market_data_live_diagnostics_v1(uint64_t handle, ifm_market_data_diagnostic_v1* output, uint32_t capacity, uint32_t* count);

uint32_t ifm_market_data_get_abi_version(void);
/* Synchronous numerical core: return 0=success, 1=bad output, 2=domain failure.
 * Failure preserves output. This is not a full Treasury curve operation. */
int32_t ifm_market_data_treasury_rate_v1(double half_yield, double* output);

/* abi=1, completion_size=32, threads=1..4; at most four runtimes. Exclusive,
 * aligned output storage for one uint64_t is required. Errors preserve output. */
int32_t ifm_market_data_runtime_create_v1(uint32_t abi, uint32_t completion_size,
                                        uint32_t threads, uint64_t* output);
/* At most 256 pending/undrained operations. IDs are nonzero and unique until
 * drained. Budget is elapsed time capped at 120000ms; zero is already expired.
 * Acceptance does not imply success; every admitted call has one completion. */
int32_t ifm_market_data_submit_treasury_v1(uint64_t handle, uint64_t operation,
                                         double half_yield, uint64_t budget_ms);
/* Input is copied. Every admitted operation has its normal terminal completion.
 * Separate result reservations (maximum 256) survive completion drain, including
 * cancellation/expiry; take each result before releasing the runtime. */
int32_t ifm_market_data_submit_pricing_v1(uint64_t handle, uint64_t operation,
    const ifm_market_data_pricing_request_v1* request, uint64_t budget_ms);
/* Copy/removes one ready result. Wrong size/alignment and Busy consume nothing.
 * Call after matching completion even when that completion reports failure. */
int32_t ifm_market_data_take_pricing_v1(uint64_t handle, uint64_t operation,
    uint32_t size, ifm_market_data_pricing_result_v1* output);
int32_t ifm_market_data_runtime_cancel_v1(uint64_t handle, uint64_t operation);
/* Nonblocking, idempotent stop. Results are retained, not discarded. */
int32_t ifm_market_data_runtime_stop_v1(uint64_t handle);
/* Blocking signal wait for a dedicated delivery thread, never UI/actor/Tokio.
 * available must remain aligned/writable/exclusive throughout the call.
 * available=0 means stopped, completed and drained. No polling timeout. */
int32_t ifm_market_data_runtime_wait_v1(uint64_t handle, uint32_t* available);
/* Caller storage must hold capacity (0..256) records, aligned and exclusive.
 * count must not overlap records. capacity=0 permits null output and consumes
 * nothing. No borrowed/native allocation crosses the ABI. */
int32_t ifm_market_data_runtime_drain_v1(uint64_t handle,
                                       ifm_market_data_completion_v1* output,
                                       uint32_t capacity, uint32_t* count);
/* Explicit release only after stop + completed delivery + concurrent callers
 * exit. Busy preserves the identity. Never call inside Tokio. Release joins
 * idle runtime workers; finalizers must only request stop, never join/unload. */
int32_t ifm_market_data_runtime_release_v1(uint64_t handle);

/* Cold catalog: one active and one staging universe, each bounded by configured
 * rows (1..100000) and source bytes (1..67108864). Configure once per runtime.
 * Inputs are consumed synchronously. Failed append discards staging; active
 * remains unchanged. Tokens are monotonic and stale cancellation is harmless. */
int32_t ifm_market_data_catalog_configure_v1(uint64_t handle, uint32_t rows, uint32_t bytes);
int32_t ifm_market_data_catalog_begin_v1(uint64_t handle, uint64_t* token);
int32_t ifm_market_data_catalog_append_v1(uint64_t handle, uint64_t token,
    const uint8_t* dataset, uint32_t dataset_length,
    const uint8_t* source, uint32_t source_length);
/* action=0 abort, action=1 commit. Revision changes only on successful commit. */
int32_t ifm_market_data_catalog_finish_v1(uint64_t handle, uint64_t token,
    uint32_t action, uint64_t* revision);
/* Exact canonical resolution at expected revision; returns raw provider JSON.
 * Missing -> length=0; ambiguous -> InvalidInput; stale -> Busy. A buffer of
 * 65536 always fits one valid source line. Failure preserves all output bytes.
 * Pointers must be valid, aligned where applicable, and non-overlapping. */
int32_t ifm_market_data_catalog_resolve_v1(uint64_t handle, uint64_t revision,
    const uint8_t* id, uint32_t id_length, uint8_t* output,
    uint32_t capacity, uint32_t* length);
/* Cold JSON {endpoint,key,dns:[numeric socket addresses]}; maximum 8192 bytes.
 * HTTPS required except numeric localhost fixtures. Configure once. */
int32_t ifm_market_data_history_configure_v1(uint64_t handle,
    const uint8_t* input, uint32_t length);
/* Cold JSON [{dataset,parents:[...]}], 1..3 distinct datasets, 8192 bytes max.
 * One original remaining budget (capped at 120000ms) covers every HTTP request,
 * decode and atomic commit. Take the retained result with live_take_v1 after
 * matching completion, including cancellation/expiry. No pointers retained. */
int32_t ifm_market_data_catalog_refresh_v1(uint64_t handle, uint64_t operation,
    const uint8_t* input, uint32_t length, uint64_t budget_ms);
int32_t ifm_market_data_catalog_revision_v1(uint64_t handle, uint64_t* revision);
/* Immutable cold query. At most four queries / 128 MiB retained source per
 * runtime. Tokens are monotonic; close or runtime disposal releases pinned rows.
 * JSON <=8192 bytes: {kind:"all",dataset?} or chain selector fields documented
 * in ABI-OWNERSHIP.md. source_token optionally selects a prior pinned query. */
typedef struct ifm_market_data_catalog_query_v1_result {
    uint32_t size, reserved;
    uint64_t token;
    uint32_t count, reserved_tail;
} ifm_market_data_catalog_query_v1_result;
int32_t ifm_market_data_catalog_query_v1(uint64_t handle, uint64_t revision,
    const uint8_t* input, uint32_t length, ifm_market_data_catalog_query_v1_result* output);
/* format 0: unchanged source JSON (<=65536 bytes); 1: native parsed projection
 * (<=262144 bytes). Failed reads do not modify output or length. */
int32_t ifm_market_data_catalog_query_row_v1(uint64_t handle, uint64_t token,
    uint32_t index, uint32_t format, uint8_t* output, uint32_t capacity, uint32_t* length);
int32_t ifm_market_data_catalog_query_close_v1(uint64_t handle, uint64_t token);
/* Acquires provider {dataset,parents:[...]} into an independent query, preserving
 * active live definitions. Standard terminal control result payload is JSON
 * {token,count}; take and close any returned token even on expiry/cancellation. */
int32_t ifm_market_data_definitions_query_v1(uint64_t handle, uint64_t operation,
    const uint8_t* input, uint32_t length, uint64_t budget_ms);
/* Source + exact derived state captured by the same native writer. See
 * ABI-OWNERSHIP.md for field slots. No managed/native pointer is retained.
 * Choose raw events or publications; they drain the same authoritative FIFO.
 * Kind/update 5 is trade replay completion; 6 is statistics replay completion.
 * Replay observations aggregate natively and are not queued as live deliveries. */
typedef struct ifm_market_data_publication_v1 {
    ifm_market_data_event_v1 source;
    uint32_t size, flags, update, reserved;
    int64_t fields[64];
} ifm_market_data_publication_v1;
int32_t ifm_market_data_live_publications_v1(uint64_t handle,
    ifm_market_data_publication_v1* output, uint32_t capacity, uint32_t* count);
/* Immediate coherent non-consuming query. source.kind=0 denotes a query result.
 * Publisher comes from the qualified bootstrap mapping. No provider I/O or
 * subscription acquisition. Unready/stopped -> zero presence flags. */
int32_t ifm_market_data_live_state_v1(uint64_t handle, uint32_t lane,
    uint32_t instrument, uint32_t publisher, uint32_t size,
    ifm_market_data_publication_v1* output);
/* Signal bit 4 announces pending HTTP warnings. Copies and consumes one JSON
 * {endpoint,status,request_id,text}; length=0 means empty. Capacity 1..65536.
 * A failed copy preserves the warning and both caller outputs. */
int32_t ifm_market_data_history_warning_v1(uint64_t handle, uint8_t* output,
    uint32_t capacity, uint32_t* length);
/* No CLR in-memory decimal layout is assumed: coefficient low/middle/high,
 * followed by sign/scale flags, preserving all 96 bits and scales 0..28. */
typedef struct ifm_market_data_decimal_v1 { uint32_t bits[4]; } ifm_market_data_decimal_v1;
typedef struct ifm_market_data_pricing_quote_v1 {
    uint64_t generation_low, generation_high;
    int64_t event_ticks, receive_ticks, sequence;
    ifm_market_data_decimal_v1 bid, ask, bid_size, ask_size;
    int32_t event_offset_minutes, receive_offset_minutes;
} ifm_market_data_pricing_quote_v1;
typedef struct ifm_market_data_quote_qualification_v1 {
    uint32_t size, flags;
    uint64_t generation_low, generation_high;
    int64_t at;
    uint32_t maximum_age_ms, maximum_skew_ms, maximum_lead_ms, reserved;
    ifm_market_data_decimal_v1 strike, definition_strike;
    ifm_market_data_pricing_quote_v1 underlying, option;
} ifm_market_data_quote_qualification_v1;
typedef struct ifm_market_data_quote_qualification_result_v1 {
    uint32_t size, failure, input, reserved;
} ifm_market_data_quote_qualification_result_v1;
/* After immutable reference/calendar qualification. No allocation/I/O/tasks.
 * flags: underlying identity=1, option identity=2, schema 3=4, call=8,
 * definition call=16. Financial failure: 0 success, 1 generation, 2 identity/
 * strike, 3 invalid quote, 4 stale, 5 skew. input selects quote 0/1 for 3/4.
 * Nonzero ABI status preserves output. See ABI-OWNERSHIP.md. */
int32_t ifm_market_data_qualify_quotes_v1(const ifm_market_data_quote_qualification_v1* input,
    ifm_market_data_quote_qualification_result_v1* output);
/* Independent physical option scopes beneath one logical options owner.
 * generation=0 before preparation. Scope operations require the expected logical
 * generation; delayed old-generation releases/stops cannot affect replacements.
 * submit actions: 0 install {scope,digest,request:<live start JSON>}, 1 reset
 * {scope}, 2 release {scope}, 3 stop owner {}, 4 prepare/replace owner {},
 * 5 atomic context refresh {scope,pricing:{expected_digest,digest,options}},
 * 6 complete risk demand {scope,pricing:{expected_digest,options:[{instrument,identity:[low,high],required}]}}.
 * Action 6 acknowledges atomic context-fenced admission, not fresh risk readiness.
 * Install may include a qualified pricing configuration (see ABI-OWNERSHIP.md).
 * Standard control completion/take applies. One original capped budget. */
/* Synchronous generation-qualified signal only; not cleanup/join proof. */
int32_t ifm_market_data_scope_stop_signal_v1(uint64_t handle, uint64_t low, uint64_t high);
int32_t ifm_market_data_scope_generation_v1(uint64_t handle, uint64_t* low, uint64_t* high);
int32_t ifm_market_data_scope_submit_v1(uint64_t handle, uint64_t operation,
    uint32_t action, uint64_t expected_low, uint64_t expected_high,
    const uint8_t* input, uint32_t length, uint64_t budget_ms);
/* Scope name: at most 128 UTF-16 units / 512 UTF-8 bytes. No read acquires a
 * subscription. Missing/unready/retiring scopes -> zero presence/count.
 * Caller buffers are never retained. Errors preserve outputs. */
int32_t ifm_market_data_scope_state_v1(uint64_t handle, const uint8_t* scope,
    uint32_t scope_length, uint32_t instrument, uint32_t publisher, uint32_t size,
    ifm_market_data_publication_v1* output);
int32_t ifm_market_data_scope_publications_v1(uint64_t handle, const uint8_t* scope,
    uint32_t scope_length, ifm_market_data_publication_v1* output,
    uint32_t capacity, uint32_t* count);
/* Source-time actual-trade extension, exactly 816 bytes. flags: captured=1,
 * underlying=2, binary reference manifest=4, non-replay option trade=8.
 * All pointers are caller-owned, disjoint and borrowed for this call only.
 * Maximum 16 records and 4 MiB manifest transfer. Native queued manifests share
 * a runtime-wide 16 MiB / 16,384-version bound. Context manifests are prepared
 * once at cold scope control, never serialized on each market record. */
typedef struct ifm_market_data_option_publication_v2 {
    ifm_market_data_publication_v1 publication;
    uint32_t size, flags;
    uint64_t context[2];
    int64_t at, qualified_until; /* Unix UTC ticks */
    ifm_market_data_pricing_quote_v1 underlying;
    uint32_t manifest_offset, manifest_length;
} ifm_market_data_option_publication_v2;
/* Generation mismatch/missing scope -> zero count/written. A record whose full
 * manifest does not fit remains queued; earlier complete records may be returned.
 * If the first record cannot fit, Capacity preserves outputs and the queue.
 * Consume this OR v1 for a scope, never both. Deserialize/copy before buffer reuse.
 * Scoped control JSON maximum is 8 MiB including base64 manifests; individual
 * opaque MessagePack WorkerOptionDefinition manifests are at most 4 MiB. */
int32_t ifm_market_data_scope_publications_v2(uint64_t handle, const uint8_t* scope,
    uint32_t scope_length, uint64_t expected_low, uint64_t expected_high,
    uint32_t size, ifm_market_data_option_publication_v2* output, uint32_t capacity,
    uint8_t* manifests, uint32_t manifest_capacity, uint32_t* count, uint32_t* written);
/* Non-consuming reliable failure; empty scope selects supervisor failure.
 * All scoped provider diagnostics use the existing shared live diagnostic sink
 * and retain original text with scope/physical-generation correlation. */
int32_t ifm_market_data_scope_failure_v1(uint64_t handle, const uint8_t* scope,
    uint32_t scope_length, uint32_t size, ifm_market_data_control_result_v1* output);
int32_t ifm_market_data_scope_status_v1(uint64_t handle, const uint8_t* scope,
    uint32_t scope_length, uint32_t size, ifm_market_data_lane_status_v1* output);
/* Exactly 640 bytes. flags: present=1, selection=2, risk=4, retained IV=8,
 * explicit financial failure=16. Native source generations are separate from
 * logical quote/context generations. No borrowed memory or native pointer. */
typedef struct ifm_market_data_scope_calculation_v1_result {
    uint32_t size, flags;
    uint64_t context[2], physical[2];
    int64_t at;
    double time;
    ifm_market_data_pricing_quote_v1 underlying, option;
    double values[7]; /* price, volatility, delta, gamma, theta, vega, rho */
    uint64_t iv_context[2];
    int64_t iv_at;
    double iv_volatility;
    ifm_market_data_pricing_quote_v1 iv_underlying, iv_option;
    uint32_t failure_kind, failure_code, failure_input, reserved;
    uint64_t policy[4]; /* SHA-256 of UTF-8 refresh version, little-endian words */
} ifm_market_data_scope_calculation_v1_result;
/* Non-acquiring cached read; risk=0 selection or 1 risk. API business clock is
 * used for freshness. Stale expected logical generation returns flags zero.
 * Failure preserves the entire caller destination. */
int32_t ifm_market_data_scope_calculation_v1(uint64_t handle, const uint8_t* scope,
    uint32_t scope_length, uint64_t expected_low, uint64_t expected_high,
    uint32_t instrument, uint32_t risk, uint32_t size,
    ifm_market_data_scope_calculation_v1_result* output);

/* Official Treasury source. Configure once after the API clock, using bounded
 * JSON {endpoint,dns:[numeric socket addresses]}. No credentials are forwarded.
 * Native cache: two months, original retrieval time, exact one-minute expiry.
 * XML input: 1 MiB / 16,384 nodes / 31 entries, DTD prohibited.
 * Reference queries use the existing shared control completion/take contract. */
int32_t ifm_market_data_reference_configure_v1(uint64_t handle,const uint8_t* input,uint32_t length);
/* mode 0 inclusive range, 1 latest at to_date. Dates yyyyMMdd are API supplied.
 * One original capped deadline covers cache gate, DNS/TLS/HTTP and parse.
 * At most 32 retained results, each <=512 KiB; exact decimal words in UTF-8 JSON. */
int32_t ifm_market_data_reference_submit_v1(uint64_t handle,uint64_t operation,
    uint32_t mode,uint32_t from_date,uint32_t to_date,uint64_t budget_ms);
/* Native FMP shares the reference result drain and warning signal. Configuration
 * JSON <=16 KiB carries the current FMP settings, key, and numeric DNS endpoints.
 * API clock must already be configured. No network or child process is started.
 * Query JSON <=8 KiB contains yyyyMMdd from/to and optional countries array.
 * Modes: 0 Treasury range, 1 latest at to, 2 economic calendar.
 * Typed failures are JSON failure envelopes; no partial row set is returned. */
int32_t ifm_market_data_fmp_configure_v1(uint64_t handle,const uint8_t* input,uint32_t length);
int32_t ifm_market_data_fmp_submit_v1(uint64_t handle,uint64_t operation,uint32_t mode,
    const uint8_t* input,uint32_t length,uint64_t budget_ms);
/* Complete copy consumes the result; insufficient capacity preserves it and
 * outputs. Transfer capacity <=16 MiB; all retained results together <=16 MiB.
 * Take control completion first. Warning delivery shares the existing
 * history_warning_v1 export and independent signal bit 4. */
int32_t ifm_market_data_reference_take_v1(uint64_t handle,uint64_t operation,
    uint8_t* output,uint32_t capacity,uint32_t* length);
/* Idempotent abandonment. Cancel/join work independently; late completions
 * cannot recreate a discarded result. Retained results prevent runtime release. */
int32_t ifm_market_data_reference_discard_v1(uint64_t handle,uint64_t operation);


/* Cold API business clock, usable before a live ValueDate exists. Ticks are
 * 100ns UTC units since Unix epoch. advancing is 0/1. No provider I/O occurs. */
int32_t ifm_market_data_clock_v1(uint64_t handle,int64_t ticks,uint32_t advancing);
/* Pure single-line definition projection, no retained query slot. Source
 * <=65,536 bytes, dataset <=64, output <=262,144. Capacity failure preserves
 * output and length. Full-universe definition stream uses the byte-reader drain. */
int32_t ifm_market_data_definition_project_v1(const uint8_t* dataset,uint32_t dataset_length,
    const uint8_t* source,uint32_t source_length,uint8_t* output,uint32_t capacity,uint32_t* length);
int32_t ifm_market_data_definitions_open_v1(uint64_t handle,uint64_t operation,
    const uint8_t* dataset,uint32_t length,uint64_t budget_ms);
/* Historical controls: estimate=0, submit exactly once=1, job=2, files=3.
 * JSON query <=256 KiB. Results use reference_take/discard. Maximum query
 * deadline 30 minutes; recovery cancellation can only shorten this deadline. */
int32_t ifm_market_data_history_control_v1(uint64_t handle,uint64_t operation,
    uint32_t mode,const uint8_t* input,uint32_t length,uint64_t budget_ms);
/* Fixed owned historical record. Prices are signed nanounits, event time UTC
 * nanoseconds. The baseline symbol fallback retains at most 31 UTF-8 bytes. */
typedef struct {
    uint32_t size,abi,kind,schema,instrument;
    uint16_t publisher,conditions;
    int64_t event,sequence,open,high,low,price;
    uint64_t quantity;
    uint8_t action,side,reserved[6],symbol[32];
} ifm_market_data_history_record_v1;
/* Four reader slots per runtime; capacity 1..4096 copied records. Queues
 * backpressure the owned async source. Standard control completion is required
 * before final rows can be committed; close independently cancels the source. */
int32_t ifm_market_data_history_range_open_v1(uint64_t handle,uint64_t operation,
    const uint8_t* input,uint32_t length,uint32_t capacity,uint64_t budget_ms);
int32_t ifm_market_data_history_file_open_v1(uint64_t handle,uint64_t operation,
    const uint8_t* input,uint32_t length,uint32_t capacity,uint64_t budget_ms);
/* Independent read-only current-session replay, <=8192 query bytes; original
 * deadline <=120s. Success requires explicit provider trade-replay completion. */
int32_t ifm_market_data_history_replay_open_v1(uint64_t handle,uint64_t operation,
    const uint8_t* input,uint32_t length,uint32_t capacity,uint64_t budget_ms);
/* File push copies <=65,536 bytes only after reserving a bounded input slot.
 * Capacity consumes nothing. A zero-length push means EOF, never timeout. */
int32_t ifm_market_data_history_file_push_v1(uint64_t handle,uint64_t operation,const uint8_t* input,uint32_t length);
int32_t ifm_market_data_history_records_v1(uint64_t handle,uint64_t operation,
    ifm_market_data_history_record_v1* output,uint32_t capacity,uint32_t* count,uint32_t* ended);
int32_t ifm_market_data_history_download_open_v1(uint64_t handle,uint64_t operation,
    const uint8_t* input,uint32_t length,uint64_t budget_ms);
/* Byte streams use four queued chunks plus a partial front and the producer's
 * current chunk, each <=65,536 bytes. The same drain serves definitions, one
 * original source line per queued item. Small output capacity preserves its tail.
 * Signal bit 5 indicates output/completion; bit 6 file input capacity. */
int32_t ifm_market_data_history_download_take_v1(uint64_t handle,uint64_t operation,
    uint8_t* output,uint32_t capacity,uint32_t* length,uint32_t* ended);
int32_t ifm_market_data_history_reader_close_v1(uint64_t handle,uint64_t operation);

/* Complete native owner command/query ABI. Compiled feature mask 0x7fff;
 * capability presence does not certify Host operation acceptance. Input JSON and
 * output MessagePack bytes are caller-owned; no physical resource pointer crosses
 * the boundary. Live streaming is Rust -> NATS. Wait selectors: 0=both futures,
 * 1..3=lane, 4=all feeds stopped; selectors 0/4 require require_active=0.
 * The native dataset supplies the original recovery deadline.
 */
typedef struct ifm_market_data_owner_health_v1_result {
    uint32_t size,lane,phase,missing;
    uint64_t ticket[3],remaining_ms,pending_failures,reserved;
} ifm_market_data_owner_health_v1_result;
typedef struct ifm_market_data_owner_identity_v1_result {
    uint32_t size,lane;int32_t value_date;uint32_t flags;
    uint64_t worker[2],generation[2];int64_t revision;
} ifm_market_data_owner_identity_v1_result;
typedef struct ifm_market_data_retired_evidence_v1_result {
    uint32_t size,reserved;uint64_t retained,generations,records,bytes;
} ifm_market_data_retired_evidence_v1_result;
/* One-byte result is written only after a validated route command succeeds. */
int32_t ifm_market_data_owner_route_v1(uint64_t handle,const uint8_t *input,uint32_t length,uint8_t *changed);
uint64_t ifm_market_data_owner_features_v1(void);
int32_t ifm_market_data_owner_create_v1(uint64_t handle);
int32_t ifm_market_data_owner_catalog_v1(uint64_t handle, const uint8_t* input, uint32_t length);
int32_t ifm_market_data_owner_futures_v1(uint64_t handle, uint32_t selected, const uint8_t* input, uint32_t length);
int32_t ifm_market_data_owner_start_v1(uint64_t handle, const uint8_t* input, uint32_t length);
int32_t ifm_market_data_owner_command_v1(uint64_t handle, const uint8_t* input, uint32_t length);
int32_t ifm_market_data_owner_backend_status_v1(uint64_t handle, uint32_t ready, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_read_snapshot_v1(uint64_t handle, uint32_t ready, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_retired_evidence_v1(uint64_t handle, uint32_t size, ifm_market_data_retired_evidence_v1_result* output);
int32_t ifm_market_data_owner_identity_v1(uint64_t handle, uint32_t selected, uint32_t size, ifm_market_data_owner_identity_v1_result* output);
int32_t ifm_market_data_owner_health_v1(uint64_t handle, uint32_t selected, uint32_t size, ifm_market_data_owner_health_v1_result* output);
int32_t ifm_market_data_owner_state_v1(uint64_t handle, uint32_t selected, uint32_t instrument, uint32_t publisher, uint32_t size, ifm_market_data_publication_v1* output);
int32_t ifm_market_data_owner_cache_v1(uint64_t handle, const uint8_t* scope, uint32_t scope_length, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_wait_v1(uint64_t handle, uint64_t operation, uint32_t selected, uint32_t require_active);
int32_t ifm_market_data_owner_current_v1(uint64_t handle, uint32_t selected, int64_t after, uint32_t maximum, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_quote_greeks_v1(uint64_t handle, uint32_t instrument, uint32_t publisher, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_trade_greeks_v1(uint64_t handle, uint32_t instrument, uint32_t publisher, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_raw_prices_v1(uint64_t handle, uint32_t selected, const uint8_t* contract, uint32_t contract_length, uint32_t date, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_failures_v1(uint64_t handle, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_failure_ack_v1(uint64_t handle, uint32_t selected, uint64_t ordinal);
int32_t ifm_market_data_owner_recover_v1(uint64_t handle, uint32_t selected);
int32_t ifm_market_data_owner_recovery_request_v1(uint64_t handle, uint64_t operation, const uint8_t* input, uint32_t length);
int32_t ifm_market_data_owner_capture_v1(uint64_t handle, uint64_t operation, const uint8_t* input, uint32_t length);
int32_t ifm_market_data_owner_contracts_v1(uint64_t handle, uint32_t selected, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_prepared_cache_v1(uint64_t handle, const uint8_t* input, uint32_t length, uint8_t* output, uint32_t capacity, uint32_t* required);
int32_t ifm_market_data_owner_query_v1(uint64_t handle, const uint8_t* input, uint32_t length, uint8_t* output, uint32_t capacity, uint32_t* required);

#ifdef __cplusplus
}
static_assert(sizeof(ifm_market_data_owner_health_v1_result) == 64, "IFM owner health layout mismatch");
static_assert(sizeof(ifm_market_data_owner_identity_v1_result) == 56, "IFM owner identity layout mismatch");
static_assert(sizeof(ifm_market_data_retired_evidence_v1_result) == 40, "IFM retired evidence layout mismatch");
static_assert(sizeof(ifm_market_data_history_record_v1) == 120, "IFM historical record layout mismatch");
static_assert(sizeof(ifm_market_data_live_configuration_v1) == 40, "IFM live config layout mismatch");
static_assert(sizeof(ifm_market_data_control_result_v1) == 4112, "IFM control result layout mismatch");
static_assert(sizeof(ifm_market_data_lane_status_v1) == 64, "IFM lane status layout mismatch");
static_assert(sizeof(ifm_market_data_last_v1) == 104, "IFM last layout mismatch");
static_assert(sizeof(ifm_market_data_event_v1) == 128, "IFM event layout mismatch");
static_assert(sizeof(ifm_market_data_publication_v1) == 656, "IFM publication layout mismatch");
static_assert(sizeof(ifm_market_data_option_publication_v2) == 816, "IFM source-time trade layout mismatch");
static_assert(sizeof(ifm_market_data_decimal_v1) == 16, "IFM decimal layout mismatch");
static_assert(sizeof(ifm_market_data_pricing_quote_v1) == 112, "IFM pricing quote layout mismatch");
static_assert(sizeof(ifm_market_data_scope_calculation_v1_result) == 640, "IFM scope pricing layout mismatch");
static_assert(sizeof(ifm_market_data_quote_qualification_v1) == 304, "IFM quote qualification layout mismatch");
static_assert(sizeof(ifm_market_data_quote_qualification_result_v1) == 16, "IFM qualification result layout mismatch");
static_assert(sizeof(ifm_market_data_catalog_query_v1_result) == 24, "IFM catalog query layout mismatch");
static_assert(sizeof(ifm_market_data_diagnostic_v1) == 4128, "IFM diagnostic layout mismatch");
static_assert(sizeof(ifm_market_data_completion_v1) == 32, "IFM MarketData ABI layout mismatch");
static_assert(sizeof(ifm_market_data_pricing_request_v1) == 80, "IFM pricing request layout mismatch");
static_assert(sizeof(ifm_market_data_pricing_result_v1) == 64, "IFM pricing result layout mismatch");
#endif
#endif
