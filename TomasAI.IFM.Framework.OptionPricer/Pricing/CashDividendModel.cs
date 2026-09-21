using System.Buffers;
using System.Runtime.CompilerServices;

namespace TomasAI.IFM.Framework.OptionPricer.Pricing;

/// <summary>Implicit finite differences with exact-time cash jump conditions and American
/// obstacle projection. Cash payout is capped at the stock value (limited liability).
/// No escrow or continuous-yield approximation is used.</summary>
internal static class CashDividendModel
{
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    internal static (double Price, double Delta, double Gamma) Evaluate(
        in OptionPricingRequest r, double sigma, PricingSettings policy, CancellationToken token,
        OptionPricingRequest? timeGridReference = null)
    {
        int m = policy.SpatialSteps, size = m + 1;
        double cash = 0;
        foreach (var d in r.CashDividends) cash += d.Amount;
        double maximum = 4 * Math.Max(r.UnderlyingPrice, r.Strike) + cash;
        double dx = maximum / m;
        // This uniform grid is qualified for moderate total volatility/carry only.
        // Refuse unresolved or tail-dominated cases instead of returning plausible prices.
        if (sigma * Math.Sqrt(r.TimeToExpiry) > .5 || Math.Abs(r.Rate * r.TimeToExpiry) > .25 ||
            r.UnderlyingPrice / dx < 2 || r.UnderlyingPrice / dx > m - 2)
            return (double.NaN, 0, 0);
        var lease = ArrayPool<double>.Shared.Rent(size * 8);
        try
        {
            var v = lease.AsSpan(0, size);
            var w = lease.AsSpan(size, size);
            var a = lease.AsSpan(2 * size, size);
            var b = lease.AsSpan(3 * size, size);
            var c = lease.AsSpan(4 * size, size);
            var rhs = lease.AsSpan(5 * size, size);
            var cp = lease.AsSpan(6 * size, size);
            var dp = lease.AsSpan(7 * size, size);
            int sign = r.Side == OptionSide.Call ? 1 : -1;
            for (int j = 0; j <= m; j++) v[j] = Math.Max(sign * (j * dx - r.Strike), 0);
            double from = r.TimeToExpiry;
            var grid = timeGridReference ?? r;
            double gridFrom = grid.TimeToExpiry;
            // Stop on every exact ex-dividend time, then apply V(t-,S)=V(t+,max(S-D,0)).
            for (int dividend = r.CashDividends.Length - 1; dividend >= -1; dividend--)
            {
                double to = dividend < 0 ? 0 : r.CashDividends[dividend].Time;
                double gridTo = dividend < 0 ? 0 : grid.CashDividends[dividend].Time;
                // Freeze segment counts during theta bumps: changing ceil(N*dt/T)
                // across a grid threshold is a discretization jump, not calendar theta.
                int count = Math.Max(1, (int)Math.Ceiling((gridFrom - gridTo) / grid.TimeToExpiry * policy.Steps));
                double dt = (from - to) / count;
                for (int j = 1; j < m; j++)
                {
                    double diffusion = .5 * sigma * sigma * j * j, drift = .5 * r.Rate * j;
                    // Monotone upwind stencil if central convection would have negative rates.
                    double down = diffusion - drift, up = diffusion + drift;
                    if (down < 0 || up < 0)
                    {
                        down = diffusion + Math.Max(-r.Rate * j, 0);
                        up = diffusion + Math.Max(r.Rate * j, 0);
                    }
                    a[j] = -dt * down;
                    b[j] = 1 + dt * (down + up + r.Rate);
                    c[j] = -dt * up;
                    if (b[j] <= 0) return (double.NaN, 0, 0);
                }
                for (int step = 1; step <= count; step++)
                {
                    token.ThrowIfCancellationRequested();
                    double at = from - step * dt, remaining = r.TimeToExpiry - at;
                    v.CopyTo(rhs);
                    v.CopyTo(w);
                    w[0] = sign > 0 ? 0 : r.Strike * Math.Exp(-r.Rate * remaining);
                    if (r.Exercise == ExerciseKind.American) w[0] = Math.Max(w[0], sign < 0 ? r.Strike : 0);
                    double pv = 0;
                    foreach (var d in r.CashDividends)
                        if (d.Time > at) pv += d.Amount * Math.Exp(-r.Rate * (d.Time - at));
                    w[m] = sign > 0 ? Math.Max(maximum - pv - r.Strike * Math.Exp(-r.Rate * remaining), 0) : 0;
                    if (r.Exercise == ExerciseKind.American)
                        w[m] = Math.Max(w[m], Math.Max(sign * (maximum - r.Strike), 0));
                    rhs[1] -= a[1] * w[0];
                    rhs[m - 1] -= c[m - 1] * w[m];
                    if (r.Exercise == ExerciseKind.European)
                    {
                        cp[1] = c[1] / b[1]; dp[1] = rhs[1] / b[1];
                        for (int j = 2; j < m; j++)
                        {
                            var pivot = b[j] - a[j] * cp[j - 1];
                            cp[j] = c[j] / pivot;
                            dp[j] = (rhs[j] - a[j] * dp[j - 1]) / pivot;
                        }
                        w[m - 1] = dp[m - 1];
                        for (int j = m - 2; j >= 1; j--) w[j] = dp[j] - cp[j] * w[j + 1];
                    }
                    else
                    {
                        bool converged = false;
                        for (int iteration = 0; iteration < policy.MaximumSorIterations; iteration++)
                        {
                            if ((iteration & 31) == 0) token.ThrowIfCancellationRequested();
                            double change = 0;
                            for (int j = 1; j < m; j++)
                            {
                                double old = w[j];
                                var residual = rhs[j] - (j > 1 ? a[j] * w[j - 1] : 0) -
                                    (j < m - 1 ? c[j] * w[j + 1] : 0);
                                w[j] = Math.Max(old + 1.2 * (residual / b[j] - old),
                                    Math.Max(sign * (j * dx - r.Strike), 0));
                                change = Math.Max(change, Math.Abs(w[j] - old));
                            }
                            if (change <= policy.SorTolerance) { converged = true; break; }
                        }
                        if (!converged) return (double.NaN, 0, 0);
                    }
                    w.CopyTo(v);
                }
                if (dividend >= 0)
                {
                    var amount = r.CashDividends[dividend].Amount;
                    for (int j = 0; j <= m; j++)
                    {
                        var shifted = Math.Max(j * dx - amount, 0) / dx;
                        int k = Math.Min((int)shifted, m - 1);
                        var weight = shifted - k;
                        w[j] = v[k] * (1 - weight) + v[k + 1] * weight;
                        if (r.Exercise == ExerciseKind.American)
                            w[j] = Math.Max(w[j], Math.Max(sign * (j * dx - r.Strike), 0));
                    }
                    w.CopyTo(v);
                }
                from = to;
                gridFrom = gridTo;
            }
            double x = r.UnderlyingPrice / dx;
            int i = Math.Clamp((int)x, 1, m - 2);
            double f = x - i;
            double price = v[i] * (1 - f) + v[i + 1] * f;
            double delta = ((v[i + 1] - v[i - 1]) * (1 - f) + (v[i + 2] - v[i]) * f) / (2 * dx);
            double gamma = ((v[i + 1] - 2 * v[i] + v[i - 1]) * (1 - f) +
                (v[i + 2] - 2 * v[i + 1] + v[i]) * f) / (dx * dx);
            return (price, delta, gamma);
        }
        finally { ArrayPool<double>.Shared.Return(lease); }
    }

    internal static double Deterministic(in OptionPricingRequest r)
    {
        double s = r.UnderlyingPrice, previous = 0, best = 0;
        int sign = r.Side == OptionSide.Call ? 1 : -1;
        if (r.Exercise == ExerciseKind.American) best = Math.Max(sign * (s - r.Strike), 0);
        foreach (var d in r.CashDividends)
        {
            s *= Math.Exp(r.Rate * (d.Time - previous));
            if (r.Exercise == ExerciseKind.American)
                best = Math.Max(best, Math.Exp(-r.Rate * d.Time) * Math.Max(sign * (s - r.Strike), 0));
            s = Math.Max(s - d.Amount, 0);
            if (r.Exercise == ExerciseKind.American)
                best = Math.Max(best, Math.Exp(-r.Rate * d.Time) * Math.Max(sign * (s - r.Strike), 0));
            previous = d.Time;
        }
        s *= Math.Exp(r.Rate * (r.TimeToExpiry - previous));
        return Math.Max(best, Math.Exp(-r.Rate * r.TimeToExpiry) * Math.Max(sign * (s - r.Strike), 0));
    }
}
