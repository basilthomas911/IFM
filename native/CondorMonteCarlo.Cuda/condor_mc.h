#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifdef _WIN32
#define CONDOR_MC_API __declspec(dllexport)
#else
#define CONDOR_MC_API
#endif

/* V1 arrays use fixed-width values and are owned by the caller. Monetary fields use ES points. */
typedef struct {
    float futures_price, opening_credit, half_spread, annualized_es_vol;
    float rate, annualized_iv_log_vol, years_to_expiry;
    float strikes[4], iv[4]; /* short put, long put, short call, long call */
    uint32_t step_offset, step_count;
} condor_mc_input_v1;

typedef struct {
    float variance_years, years_to_expiry;
} condor_mc_step_v1;

typedef struct {
    uint32_t call_stops, put_stops, profit_targets, time_exits, recovered;
    float expected_pnl_points;
} condor_mc_result_v1;

CONDOR_MC_API uint32_t condor_mc_abi_version(void);
/* Returns zero on success. Errors are returned in the caller's UTF-8 error buffer. */
CONDOR_MC_API int32_t condor_mc_device_v1(int32_t device, char *name, uint32_t name_capacity);
CONDOR_MC_API int32_t condor_mc_run_v1(
    int32_t device, const condor_mc_input_v1 *inputs, uint32_t input_count,
    const condor_mc_step_v1 *steps, uint32_t step_count, uint32_t paths, uint64_t seed,
    condor_mc_result_v1 *results, float *kernel_ms, char *error, uint32_t error_capacity);

#ifdef __cplusplus
}
#endif
