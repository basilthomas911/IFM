using Black76OptionPricer;
using System.Diagnostics;
using TomasAI.IFM.Framework.OptionPricer.Black76;

namespace Black76OptionPricer.TestApp;

internal class Program
{
    private static void Main()
    {
        double F = 5300.0;
        double K = 5300.0;
        double r = 0.045;
        double sigma = 0.18;
        double T = 0.25;
        int put = -1;
        int call = 1;

        Console.WriteLine("Black-76 ES Futures Option Test");
        Console.WriteLine("===============================");
        Console.WriteLine($"F      = {F}");
        Console.WriteLine($"K      = {K}");
        Console.WriteLine($"r      = {r}");
        Console.WriteLine($"sigma  = {sigma}");
        Console.WriteLine($"T      = {T}");
        Console.WriteLine();

        // --- Scalar Price ---
        var sw = new Stopwatch();
        sw.Start();
        double putPrice = OptionModel.Price(F, K, r, sigma, T, put);
        sw.Stop();
        long putPriceTicks = sw.ElapsedTicks;

        sw.Restart();
        double callPrice = OptionModel.Price(F, K, r, sigma, T, call);
        sw.Stop();
        long callPriceTicks = sw.ElapsedTicks;

        Console.WriteLine("--- Scalar Price ---");
        Console.WriteLine($"Put  Price : {putPrice:F8}  ({putPriceTicks} ticks)");
        Console.WriteLine($"Call Price : {callPrice:F8}  ({callPriceTicks} ticks)");
        Console.WriteLine();

        // --- Scalar PriceWithGreeks ---
        var putGreeks = OptionModel.PriceWithGreeks(F, K, r, sigma, T, put);
        var callGreeks = OptionModel.PriceWithGreeks(F, K, r, sigma, T, call);

        Console.WriteLine("--- PriceWithGreeks ---");
        Console.WriteLine($"{"Greek",-8} {"Put",14} {"Call",14}");
        Console.WriteLine($"{"Price",-8} {putGreeks.Price,14:F8} {callGreeks.Price,14:F8}");
        Console.WriteLine($"{"Delta",-8} {putGreeks.Delta,14:F8} {callGreeks.Delta,14:F8}");
        Console.WriteLine($"{"Gamma",-8} {putGreeks.Gamma,14:F8} {callGreeks.Gamma,14:F8}");
        Console.WriteLine($"{"Vega",-8} {putGreeks.Vega,14:F8} {callGreeks.Vega,14:F8}");
        Console.WriteLine($"{"Theta",-8} {putGreeks.Theta,14:F8} {callGreeks.Theta,14:F8}");
        Console.WriteLine($"{"Rho",-8} {putGreeks.Rho,14:F8} {callGreeks.Rho,14:F8}");
        Console.WriteLine();

        // --- Put-Call Parity Check: C - P = e^(-rT)(F - K) ---
        double parity = callGreeks.Price - putGreeks.Price;
        double expected = Math.Exp(-r * T) * (F - K);
        Console.WriteLine($"Put-Call Parity: C - P = {parity:F8}, e^(-rT)(F-K) = {expected:F8}");
        Console.WriteLine();

        // --- Implied Volatility ---
        Console.WriteLine("--- Implied Volatility (Newton-Raphson) ---");
        double ivPut = OptionModel.ImpliedVolatility(F, K, r, putPrice, T, put);
        double ivCall = OptionModel.ImpliedVolatility(F, K, r, callPrice, T, call);
        Console.WriteLine($"Put  IV : {ivPut:F10}  (expected {sigma})");
        Console.WriteLine($"Call IV : {ivCall:F10}  (expected {sigma})");

        // OTM implied vol
        double otmPutPrice = OptionModel.Price(F, 5100.0, r, 0.22, T, put);
        double ivOtm = OptionModel.ImpliedVolatility(F, 5100.0, r, otmPutPrice, T, put);
        Console.WriteLine($"OTM Put IV (K=5100, σ=0.22) : {ivOtm:F10}  (expected 0.22)");
        Console.WriteLine();

        // --- Batch Pricing ---
        Console.WriteLine("--- PriceBatch ---");
        double[] forwards = [5300.0, 5350.0, 5250.0, 5400.0, 5200.0, 5300.0];
        double[] strikes  = [5300.0, 5300.0, 5300.0, 5300.0, 5300.0, 5300.0];
        double[] rates    = [r, r, r, r, r, r];
        double[] vols     = [sigma, sigma, sigma, sigma, sigma, sigma];
        double[] expiries = [T, T, T, T, T, T];
        int[] types       = [-1, -1, -1, -1, -1, 1]; // 5 puts + 1 call
        int batchSize = forwards.Length;

        double[] batchPrices = new double[batchSize];

        sw.Restart();
        OptionModel.PriceBatch(forwards, strikes, rates, vols, expiries, types, batchPrices);
        sw.Stop();
        Console.WriteLine($"{batchSize} options in {sw.ElapsedTicks} ticks:");
        for (int i = 0; i < batchSize; i++)
        {
            string t = types[i] > 0 ? "Call" : "Put ";
            Console.WriteLine($"  F={forwards[i]:F0}  {t}  Price={batchPrices[i]:F8}");
        }
        Console.WriteLine();

        // --- Batch PriceWithGreeks ---
        Console.WriteLine("--- PriceWithGreeksBatch ---");
        Black76Result[] batchGreeks = new Black76Result[batchSize];

        sw.Restart();
        OptionModel.PriceWithGreeksBatch(forwards, strikes, rates, vols, expiries, types, batchGreeks);
        sw.Stop();
        Console.WriteLine($"{batchSize} options in {sw.ElapsedTicks} ticks:");
        Console.WriteLine($"  {"F",6} {"Type",-5} {"Price",12} {"Delta",10} {"Gamma",10} {"Vega",10} {"Theta",10} {"Rho",10}");
        for (int i = 0; i < batchSize; i++)
        {
            ref readonly var g = ref batchGreeks[i];
            string t = types[i] > 0 ? "Call" : "Put";
            Console.WriteLine($"  {forwards[i],6:F0} {t,-5} {g.Price,12:F6} {g.Delta,10:F6} {g.Gamma,10:F6} {g.Vega,10:F4} {g.Theta,10:F4} {g.Rho,10:F4}");
        }
        Console.WriteLine();

        // --- Large Batch Performance ---
        Console.WriteLine("--- Large Batch Performance ---");
        const int largeBatchSize = 100_000;
        double[] lForwards = new double[largeBatchSize];
        double[] lStrikes  = new double[largeBatchSize];
        double[] lRates    = new double[largeBatchSize];
        double[] lVols     = new double[largeBatchSize];
        double[] lExpiries = new double[largeBatchSize];
        int[] lTypes       = new int[largeBatchSize];
        double[] lResults  = new double[largeBatchSize];

        var rng = new Random(42);
        for (int i = 0; i < largeBatchSize; i++)
        {
            lForwards[i] = 5000.0 + rng.NextDouble() * 600.0;
            lStrikes[i]  = 5000.0 + rng.NextDouble() * 600.0;
            lRates[i]    = r;
            lVols[i]     = 0.10 + rng.NextDouble() * 0.30;
            lExpiries[i] = 0.01 + rng.NextDouble() * 1.0;
            lTypes[i]    = rng.Next(2) == 0 ? -1 : 1;
        }

        sw.Restart();
        OptionModel.PriceBatch(lForwards, lStrikes, lRates, lVols, lExpiries, lTypes, lResults);
        sw.Stop();
        Console.WriteLine($"PriceBatch          : {largeBatchSize:N0} options in {sw.ElapsedTicks,8} ticks ({sw.Elapsed.TotalMilliseconds:F3} ms)");

        Black76Result[] lGreeksResults = new Black76Result[largeBatchSize];
        sw.Restart();
        OptionModel.PriceWithGreeksBatch(lForwards, lStrikes, lRates, lVols, lExpiries, lTypes, lGreeksResults);
        sw.Stop();
        Console.WriteLine($"PriceWithGreeksBatch: {largeBatchSize:N0} options in {sw.ElapsedTicks,8} ticks ({sw.Elapsed.TotalMilliseconds:F3} ms)");
        Console.WriteLine();

        // --- Edge Cases ---
        Console.WriteLine("--- Edge Cases ---");

        // Expired option (T <= 0)
        double expiredPrice = OptionModel.Price(5350.0, 5300.0, r, sigma, 0.0, call);
        Console.WriteLine($"Expired Call (F=5350, K=5300, T=0) : {expiredPrice:F8}  (intrinsic = 50)");

        // Deep ITM put
        double deepItmPut = OptionModel.Price(5000.0, 5500.0, r, sigma, T, put);
        Console.WriteLine($"Deep ITM Put  (F=5000, K=5500)     : {deepItmPut:F8}");

        // Deep OTM put
        double deepOtmPut = OptionModel.Price(5500.0, 5000.0, r, sigma, T, put);
        Console.WriteLine($"Deep OTM Put  (F=5500, K=5000)     : {deepOtmPut:F8}");

        // Zero volatility
        double zeroVolPrice = OptionModel.Price(5350.0, 5300.0, r, 0.0, T, call);
        double discountedIntrinsic = Math.Exp(-r * T) * 50.0;
        Console.WriteLine($"Zero Vol Call (F=5350, K=5300)      : {zeroVolPrice:F8}  (disc. intrinsic = {discountedIntrinsic:F8})");
        Console.WriteLine();
    }
}
