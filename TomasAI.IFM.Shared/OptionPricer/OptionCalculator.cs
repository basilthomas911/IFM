using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using QLNet;
using TomasAI.IFM.Shared.MarketDataFeed;

namespace TomasAI.IFM.Shared.OptionPricer;

/// <summary>
/// Provides functionality to calculate option Greeks using the Black-Scholes-Merton model for American-style options.
/// </summary>
/// <remarks>The <see cref="OptionCalculator"/> class is initialized with a value date and a maturity date, which
/// are used in the calculation of option Greeks. It handles exceptions internally and returns default values if an
/// error occurs during calculations.</remarks>
/// <param name="valueDate"></param>
/// <param name="maturityDate"></param>
public class OptionCalculator(DateOnly valueDate, DateOnly maturityDate)
{
    readonly DateTime _valueDate = valueDate.ToDateTime(TimeOnly.MinValue);
    readonly DateTime _maturityDate = maturityDate.ToDateTime(TimeOnly.MinValue);

    /// <summary>
    /// Calculates the option Greeks for a given option type and market conditions.
    /// </summary>
    /// <remarks>This method uses the Black-Scholes-Merton model to compute the option Greeks, assuming
    /// American-style options. The method handles exceptions internally and returns a default <see
    /// cref="OptionGreeks"/> object with all values set to zero if an error occurs.</remarks>
    /// <param name="optionTypeName">The name of the option type, such as "call" or "put".</param>
    /// <param name="assetPrice">The current price of the underlying asset.</param>
    /// <param name="strikePrice">The strike price of the option.</param>
    /// <param name="optionValue">The current market value of the option.</param>
    /// <param name="riskFreeRate">The risk-free interest rate, expressed as a decimal.</param>
    /// <returns>An <see cref="OptionGreeks"/> object containing the calculated Greeks: implied volatility, delta, gamma, theta,
    /// vega, and rho. If the calculation fails, the <see cref="OptionGreeks.Success"/> property will be <see
    /// langword="false"/>.</returns>
    public OptionGreeks GetOptionGreeks(string optionTypeName, double assetPrice, double strikePrice, double optionValue, double riskFreeRate)
    {
        try
        {
            var optionType = GetOptionType(optionTypeName);
            var impliedVolatility = CalculateImpliedVolatility(optionType, assetPrice, strikePrice, optionValue, riskFreeRate);
            var calendar = new TARGET();
            Settings.setEvaluationDate(new DateTime(_valueDate.Year, _valueDate.Month, _valueDate.Day));
            Settings.includeReferenceDateEvents = true;
            var dayCounter = new Actual365Fixed();
            var underlyingH = new Handle<Quote>(new SimpleQuote(assetPrice));
            var flatTermStructure = new Handle<YieldTermStructure>(new FlatForward(_valueDate, riskFreeRate, dayCounter));
            var flatDividendTS = new Handle<YieldTermStructure>(new FlatForward(_valueDate, 0, dayCounter));
            var flatVolTS = new Handle<BlackVolTermStructure>(new BlackConstantVol(_valueDate, calendar, impliedVolatility, dayCounter));
            var payoff = new PlainVanillaPayoff(optionType, strikePrice);
            var bsmProcess = new BlackScholesMertonProcess(underlyingH, flatDividendTS, flatTermStructure, flatVolTS);
            var exerciseEngine = new AmericanExercise(_valueDate, _maturityDate);
            var optionEngine = new VanillaOption(payoff, exerciseEngine);
            optionEngine.setPricingEngine(new BinomialVanillaEngine<CoxRossRubinstein>(bsmProcess, 801));
            var optionNPV = optionEngine.NPV();
            return new OptionGreeks(
                success: true,
                impliedVol: optionEngine.impliedVolatility(optionNPV, bsmProcess, 0.0001, 1000, 1e-07, 4.00),
                delta: optionEngine.delta(),
                gamma: optionEngine.gamma(),
                theta: optionEngine.theta(),
                vega: optionEngine.vega(),
                rho: optionEngine.rho());
        }
        catch { }
        return new OptionGreeks(false, 0, 0, 0, 0, 0, 0);
    }

    /// <summary>
    /// Retrieves the <see cref="QLNet.Option.Type"/> corresponding to the specified option type name.
    /// </summary>
    /// <param name="optionTypeName">The name of the option type. Must be either "Call" or "Put".</param>
    /// <returns>The <see cref="QLNet.Option.Type"/> that matches the specified option type name.</returns>
    static Option.Type GetOptionType(string optionTypeName)
    {
        var optionType = default(QLNet.Option.Type);
        switch (optionTypeName)
        {
            case OptionTypeName.Call:
                optionType = QLNet.Option.Type.Call;
                break;
            case OptionTypeName.Put:
                optionType = QLNet.Option.Type.Put;
                break;
        }
        return optionType;
    }

    /// <summary>
    /// Calculates the implied volatility of an option given its type, asset price, strike price, option value, and
    /// risk-free rate.
    /// </summary>
    /// <remarks>This method uses a binomial pricing model to estimate the implied volatility. The calculation
    /// assumes American-style options and uses the Cox-Ross-Rubinstein model with 801 time steps.</remarks>
    /// <param name="optionType">The type of the option, either call or put.</param>
    /// <param name="assetPrice">The current price of the underlying asset.</param>
    /// <param name="strikePrice">The strike price of the option.</param>
    /// <param name="optionValue">The market value of the option.</param>
    /// <param name="riskFreeRate">The risk-free interest rate used for discounting.</param>
    /// <returns>The implied volatility of the option as a double.</returns>
    double CalculateImpliedVolatility(Option.Type optionType, double assetPrice, double strikePrice, double optionValue, double riskFreeRate)
    {
        var calendar = new TARGET();
        var valueDate = new DateTime(_valueDate.Year, _valueDate.Month, _valueDate.Day);
        Settings.setEvaluationDate(valueDate);
        Settings.includeReferenceDateEvents = true;
        var dayCounter = new Actual365Fixed();

        var underlyingH = new Handle<Quote>(new SimpleQuote(assetPrice));
        var flatTermStructure = new Handle<YieldTermStructure>(new FlatForward(_valueDate, riskFreeRate, dayCounter));
        var flatDividendTS = new Handle<YieldTermStructure>(new FlatForward(_valueDate, 0, dayCounter));
        var flatVolTS = new Handle<BlackVolTermStructure>(new BlackConstantVol(_valueDate, calendar, 0.2, dayCounter));
        var payoff = new PlainVanillaPayoff(optionType, strikePrice);
        var bsmProcess = new BlackScholesMertonProcess(underlyingH, flatDividendTS, flatTermStructure, flatVolTS);
        var exerciseEngine = new AmericanExercise(_valueDate, _maturityDate);
        var optionEngine = new VanillaOption(payoff, exerciseEngine);
        optionEngine.setPricingEngine(new BinomialVanillaEngine<CoxRossRubinstein>(bsmProcess, 801));
        optionEngine.NPV();
        return optionEngine.impliedVolatility(optionValue, bsmProcess, 0.0001, 1000, 1e-07, 4.00);
    }

}
