#ifndef IFM_OPTION_PRICER_NATIVE_H
#define IFM_OPTION_PRICER_NATIVE_H

#include <stdint.h>

#if defined(_WIN32)
#define IFM_OPTION_PRICER_API __declspec(dllimport)
#else
#define IFM_OPTION_PRICER_API
#endif

#ifdef __cplusplus
extern "C" {
#endif

#define IFM_OPTION_PRICER_ABI_V1 1u
#define IFM_OPTION_PRICER_NO_ERROR_INDEX UINT32_MAX

typedef int32_t ifm_option_pricer_status_v1;

enum {
    IFM_OPTION_PRICER_OK_V1 = 0,
    IFM_OPTION_PRICER_NULL_POINTER_V1 = 1,
    IFM_OPTION_PRICER_INVALID_ARGUMENT_V1 = 2,
    IFM_OPTION_PRICER_NO_CONVERGENCE_V1 = 3,
    IFM_OPTION_PRICER_PANIC_V1 = 4
};

typedef struct ifm_black76_result_v1 {
    double price;
    double delta;
    double gamma;
    double vega;
    double theta;
    double rho;
} ifm_black76_result_v1;

typedef struct ifm_black76_implied_greeks_result_v1 {
    double implied_volatility;
    double price;
    double delta;
    double gamma;
    double vega;
    double theta;
    double rho;
} ifm_black76_implied_greeks_result_v1;

IFM_OPTION_PRICER_API uint32_t ifm_option_pricer_get_abi_version(void);

IFM_OPTION_PRICER_API ifm_option_pricer_status_v1 ifm_black76_price_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double volatility,
    double time_to_expiry,
    int32_t option_type,
    double* result);

IFM_OPTION_PRICER_API ifm_option_pricer_status_v1 ifm_black76_price_with_greeks_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double volatility,
    double time_to_expiry,
    int32_t option_type,
    ifm_black76_result_v1* result);

IFM_OPTION_PRICER_API ifm_option_pricer_status_v1 ifm_black76_implied_volatility_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double market_price,
    double time_to_expiry,
    int32_t option_type,
    double tolerance,
    int32_t max_iterations,
    int32_t has_initial_guess,
    double initial_guess,
    double* result);

IFM_OPTION_PRICER_API ifm_option_pricer_status_v1 ifm_black76_implied_volatility_with_greeks_v1(
    double forward_price,
    double strike_price,
    double risk_free_rate,
    double market_price,
    double time_to_expiry,
    int32_t option_type,
    double tolerance,
    int32_t max_iterations,
    int32_t has_initial_guess,
    double initial_guess,
    ifm_black76_implied_greeks_result_v1* result);

IFM_OPTION_PRICER_API ifm_option_pricer_status_v1 ifm_black76_price_batch_v1(
    const double* forward_prices,
    const double* strike_prices,
    const double* risk_free_rates,
    const double* volatilities,
    const double* times_to_expiry,
    const int32_t* option_types,
    uint32_t count,
    double* results,
    uint32_t* error_index);

IFM_OPTION_PRICER_API ifm_option_pricer_status_v1 ifm_black76_price_with_greeks_batch_v1(
    const double* forward_prices,
    const double* strike_prices,
    const double* risk_free_rates,
    const double* volatilities,
    const double* times_to_expiry,
    const int32_t* option_types,
    uint32_t count,
    ifm_black76_result_v1* results,
    uint32_t* error_index);

#ifdef __cplusplus
}
#endif

#endif
