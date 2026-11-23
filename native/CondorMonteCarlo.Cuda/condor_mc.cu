#include "condor_mc.h"

#include <cuda_runtime.h>
#include <curand_kernel.h>

#include <cmath>
#include <cstdio>
#include <stdexcept>
#include <vector>

namespace {
constexpr uint32_t block_size = 128;
enum Outcome { call_stop, put_stop, profit_target, time_exit };

struct BlockResult {
    uint32_t outcomes[4]{};
    uint32_t recovered{};
    float pnl{};
};

void check(cudaError_t status) {
    if (status != cudaSuccess)
        throw std::runtime_error(cudaGetErrorString(status));
}

template <class T> struct DeviceArray {
    T *data = nullptr;
    explicit DeviceArray(size_t count) {
        check(cudaMalloc(&data, count * sizeof(T)));
    }
    ~DeviceArray() { cudaFree(data); }
    DeviceArray(const DeviceArray &) = delete;
    DeviceArray &operator=(const DeviceArray &) = delete;
};

struct Event {
    cudaEvent_t handle{};
    Event() { check(cudaEventCreate(&handle)); }
    ~Event() { cudaEventDestroy(handle); }
    Event(const Event &) = delete;
    Event &operator=(const Event &) = delete;
};

__device__ float cdf(float x) {
    return 0.5f * erfcf(-x * 0.7071067811865475f);
}

__device__ float option(float f, float strike, float iv, float years, float discount,
                        float sign, float &delta) {
    const float scale = iv * sqrtf(years);
    const float d1 = logf(f / strike) / scale + 0.5f * scale;
    const float n1 = cdf(sign * d1);
    delta = discount * n1;
    return discount * sign * (f * n1 - strike * cdf(sign * (d1 - scale)));
}

__device__ BlockResult simulate(const condor_mc_input_v1 &input,
                               const condor_mc_step_v1 *steps,
                               uint64_t seed, uint32_t path) {
    curandStatePhilox4_32_10_t rng;
    curand_init(seed, path, 0, &rng);
    float futures = input.futures_price;
    float iv_shift = 0.0f;
    float debit = 0.0f;
    bool recovered = false;
    Outcome outcome = time_exit;

    for (uint32_t i = 0; i < input.step_count; ++i) {
        const auto step = steps[input.step_offset + i];
        const float root_dt = sqrtf(step.variance_years);
        const float2 shock = curand_normal2(&rng);
        const float es = input.annualized_es_vol * root_dt;
        futures *= expf(es * shock.x - 0.5f * es * es);
        // Common log-IV movement, with negative spot/IV correlation. The entry skew is retained.
        iv_shift += input.annualized_iv_log_vol * root_dt * (-0.6f * shock.x + 0.8f * shock.y);
        const float vol_scale = expf(iv_shift);
        const float discount = expf(-input.rate * step.years_to_expiry);
        float short_delta[2]{};
        float mid = 0.0f;
        for (int leg = 0; leg < 4; ++leg) {
            float delta;
            const float iv = fminf(3.0f, fmaxf(0.01f, input.iv[leg] * vol_scale));
            const float premium = option(futures, input.strikes[leg], iv, step.years_to_expiry,
                                         discount, leg < 2 ? -1.0f : 1.0f, delta);
            if ((leg & 1) == 0) {
                short_delta[leg / 2] = delta;
                mid += premium;
            } else {
                mid -= premium;
            }
        }
        debit = fmaxf(0.0f, mid) + input.half_spread;
        recovered |= debit <= input.opening_credit;
        if (short_delta[0] >= 0.30f || short_delta[1] >= 0.30f) {
            outcome = short_delta[0] > short_delta[1] ? put_stop : call_stop;
            break;
        }
        if (debit <= 0.60f * input.opening_credit) {
            outcome = profit_target;
            break;
        }
    }

    BlockResult result;
    result.outcomes[outcome] = 1;
    result.recovered = recovered ? 1 : 0;
    result.pnl = input.opening_credit - debit;
    return result;
}

__global__ void forecasts(const condor_mc_input_v1 *inputs,
                          const condor_mc_step_v1 *steps, uint32_t paths,
                          uint64_t seed, BlockResult *blocks) {
    __shared__ BlockResult shared[block_size];
    const uint32_t path = blockIdx.x * block_size + threadIdx.x;
    BlockResult sample;
    if (path < paths)
        sample = simulate(inputs[blockIdx.y], steps, seed, path);
    shared[threadIdx.x] = sample;
    __syncthreads();
    for (uint32_t width = block_size / 2; width > 0; width /= 2) {
        if (threadIdx.x < width) {
            auto &a = shared[threadIdx.x];
            const auto &b = shared[threadIdx.x + width];
            for (int k = 0; k < 4; ++k)
                a.outcomes[k] += b.outcomes[k];
            a.recovered += b.recovered;
            a.pnl += b.pnl;
        }
        __syncthreads();
    }
    if (threadIdx.x == 0)
        blocks[blockIdx.y * gridDim.x + blockIdx.x] = shared[0];
}

void validate(const condor_mc_input_v1 &x, const condor_mc_step_v1 *steps, uint32_t count) {
    if (!std::isfinite(x.futures_price) || x.futures_price <= 0 ||
        !std::isfinite(x.opening_credit) || x.opening_credit <= 0 ||
        !std::isfinite(x.half_spread) || x.half_spread < 0 ||
        !std::isfinite(x.annualized_es_vol) || x.annualized_es_vol < 0 ||
        !std::isfinite(x.annualized_iv_log_vol) || x.annualized_iv_log_vol < 0 ||
        !std::isfinite(x.rate) || !std::isfinite(x.years_to_expiry) || x.years_to_expiry <= 0 ||
        x.step_count == 0 || x.step_offset > count || x.step_count > count - x.step_offset)
        throw std::invalid_argument("Invalid forecast input");
    for (int i = 0; i < 4; ++i)
        if (!std::isfinite(x.strikes[i]) || x.strikes[i] <= 0 ||
            !std::isfinite(x.iv[i]) || x.iv[i] <= 0 || x.iv[i] > 3)
            throw std::invalid_argument("Invalid option leg");
    if (!(x.strikes[1] < x.strikes[0] && x.strikes[0] < x.strikes[2] && x.strikes[2] < x.strikes[3]))
        throw std::invalid_argument("Invalid iron condor strike order");
    float prior = x.years_to_expiry;
    for (uint32_t i = 0; i < x.step_count; ++i) {
        auto step = steps[x.step_offset + i];
        if (!std::isfinite(step.variance_years) || step.variance_years < 0 ||
            !std::isfinite(step.years_to_expiry) || step.years_to_expiry <= 0 ||
            step.years_to_expiry > prior)
            throw std::invalid_argument("Invalid simulation schedule");
        prior = step.years_to_expiry;
    }
}
} // namespace

static_assert(sizeof(condor_mc_input_v1) == 68 && sizeof(condor_mc_step_v1) == 8 &&
              sizeof(condor_mc_result_v1) == 24);

uint32_t condor_mc_abi_version(void) { return 1; }

int32_t condor_mc_device_v1(int32_t device, char *name, uint32_t capacity) {
    cudaDeviceProp properties{};
    if (!name || capacity == 0 || cudaGetDeviceProperties(&properties, device) != cudaSuccess)
        return 1;
    std::snprintf(name, capacity, "%s", properties.name);
    return 0;
}

int32_t condor_mc_run_v1(int32_t device, const condor_mc_input_v1 *inputs, uint32_t count,
                         const condor_mc_step_v1 *steps, uint32_t step_count, uint32_t paths,
                         uint64_t seed, condor_mc_result_v1 *results, float *kernel_ms,
                         char *error, uint32_t error_capacity) {
    try {
        if (!inputs || !steps || !results || !kernel_ms || count == 0 || count > 65535 ||
            paths == 0 || paths > (1u << 20))
            throw std::invalid_argument("Invalid forecast batch");
        for (uint32_t i = 0; i < count; ++i)
            validate(inputs[i], steps, step_count);

        check(cudaSetDevice(device));
        const uint32_t grid = (paths + block_size - 1) / block_size;
        const size_t block_count = static_cast<size_t>(count) * grid;
        DeviceArray<condor_mc_input_v1> d_inputs(count);
        DeviceArray<condor_mc_step_v1> d_steps(step_count);
        DeviceArray<BlockResult> d_blocks(block_count);
        check(cudaMemcpy(d_inputs.data, inputs, count * sizeof(*inputs), cudaMemcpyHostToDevice));
        check(cudaMemcpy(d_steps.data, steps, step_count * sizeof(*steps), cudaMemcpyHostToDevice));

        Event start, end;
        check(cudaEventRecord(start.handle));
        forecasts<<<dim3(grid, count), block_size>>>(d_inputs.data, d_steps.data, paths, seed, d_blocks.data);
        check(cudaGetLastError());
        check(cudaEventRecord(end.handle));
        check(cudaEventSynchronize(end.handle));
        check(cudaEventElapsedTime(kernel_ms, start.handle, end.handle));

        std::vector<BlockResult> blocks(block_count);
        check(cudaMemcpy(blocks.data(), d_blocks.data, block_count * sizeof(BlockResult), cudaMemcpyDeviceToHost));
        for (uint32_t i = 0; i < count; ++i) {
            condor_mc_result_v1 result{};
            double pnl = 0;
            for (uint32_t j = 0; j < grid; ++j) {
                const auto &b = blocks[static_cast<size_t>(i) * grid + j];
                result.call_stops += b.outcomes[call_stop];
                result.put_stops += b.outcomes[put_stop];
                result.profit_targets += b.outcomes[profit_target];
                result.time_exits += b.outcomes[time_exit];
                result.recovered += b.recovered;
                pnl += b.pnl;
            }
            result.expected_pnl_points = static_cast<float>(pnl / paths);
            results[i] = result;
        }
        if (error && error_capacity)
            error[0] = '\0';
        return 0;
    } catch (const std::exception &exception) {
        if (error && error_capacity)
            std::snprintf(error, error_capacity, "%s", exception.what());
        return 1;
    }
}
