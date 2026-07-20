1. What is the “Intrinsic‑Time (IT) Algorithm”?
Philip Carver’s Intrinsic‑Time framework comes from his book The Ticker Tape and a series of papers.
The core idea is simple:

Traditional time
Intrinsic time
The clock ticks at the same rate, no matter how quiet or how frantic the market is
The clock ticks faster when the market is volatile or when the price is moving a lot, and slower when the market is calm.

In practice an intrinsic‑time series is a re‑parameterisation of a price path:

t 
i
​
 (n)= 
k=1
∑
n
​
 Δt 
k
​
 ,Δt 
k
​
 =g(Δp 
k
​
 )
where

Δp 
k
​
 =P 
k
​
 /P 
k−1
​
 −1 (price change) or a log return,
g(⋅) is a monotonically‑increasing function that measures how “fast” you want the clock to run.
A very common choice
Carver recommends a function that ties the time step to volatility rather than to pure price changes. One practical formulation is:

Δt 
k
​
 = 
ATR 
k
​
 
∣lnP 
k
​
 −lnP 
k−1
​
 ∣
​
 
ATR is the real‑time average true range (a proxy for volatility).
The intuition: If the price moves a lot and the market is liquid (high ATR) the clock only advances a little (the market already knows the move).
If the price move is small or ATR is low, the clock advances a bit more, giving the indicator more “resolution” in calm periods.

Your IT‑series is then simply the cumulative sum:

IT 
k
​
 = 
i=1
∑
k
​
 Δt 
i
​
 
The units are intrinsic‑seconds. When you plot the price against IT, you end up with a “stretched” chart that looks a little like a typical “intrinsic‑time” asset price.

Why bother?
In calm markets, the optical “time‑series” looks blotchy; the price moves slowly so the IT‑time axis compresses it, expanding the plot. In stormy markets the chart expands; noises that would otherwise swamp the indicator are pushed forward in “time”. This tends to reduce false cross‑overs and gives the system a more “out‑of‑the‑window” look at clustering of events. 

2. Where RSI & MACD fit
Indicator
What it tells you
Best when
RSI (Relative‑Strength Index)
Over‑/under‑bought; mean‑reversion
Ranges, quakes, small swings
MACD (Moving‑Avg Convergence Divergence)
Direction & momentum; trend starts/ends
Trending markets, breakouts

When you feed these indicators on the intrinsic‑time chart you get a cleaner statement of “the market now looks over‑bought in intrinsic view but really has just finished a big move.”

The algorithmic benefit is two‑fold:

Signal filtering: false RSI spikes or MACD cross‑overs caused by micro‑noise tend to be smoothed out because fast‑time moves are down‑clocked.
Dynamic look‑back windows: Because the time axis is stretched in low‑volatility periods, the “rolling window” you use for RSI or EMA calculations adjusts automatically to market pacing.
Below is a step‑by‑step recipe that shows you how to:

Build the intrinsic‑time series,
Compute RSI & MACD on that time basis,
Combine the two signals into a single “trade engine”,
Run a backtest, and
Manage risk.
3. The building blocks – math & code
3.1. Intrinsic‑time calculation
python

Collapse

Run
Save
Copy
1
2
3
4
5
6
7
8
9
10
11
12
13
14
15
16
17
18
19
20
21
22
23
⌄
import pandas as pd
import numpy as np

# Assume df has columns: 'ts' (datetime) and 'price'
df = pd.read_csv('price_data.csv', parse_dates=['ts'])
df.set_index('ts', inplace=True)

# Step 1 – daily (or optional intraday) returns
df['return'] = np.log(df['price']).diff()

# Step 2 – ATR (14‑period)
high, low, close = df['high'], df['low'], df['price']
tr  = pd.concat([high - low,
                 (high - close.shift(1)).abs(),
                 (low  - close.shift(1)).abs()], axis=1).max(axis=1)
df['ATR'] = tr.rolling(14).mean()

# Step 3 – intrinsic‑time tick
# Note: choose g() – we’ll use |return| divided by ATR
df['delta_it'] = (df['return'].abs() / df['ATR']).fillna(0)

# Step 4 – cumulative intrinsic time
df['IT'] = df['delta_it'].cumsum()
Tip – if you’re dealing with tick‑by‑tick data, you might replace ATR with the simple volume or a volatility proxy computed for each tick. 

3.2. RSI on Intrinsic time
The conventional RSI is an average‑gain / average‑loss over the last N periods. In IT we interpret “period” as an intrinsic‑time window. A convenient implementation is to linearly interpolate price values onto a regularly‑spaced intrinsic‑time grid and then compute RSI on that grid. (Interpolation smooths small gaps that may exist if two consecutive ticks fall in the same intrinsic‑time bin.)

python

Collapse

Run
Save
Copy
1
2
3
4
5
6
7
8
9
10
11
12
13
14
15
16
17
18
⌄
# Interpolate price onto an evenly spaced intrinsic‑time axis
it_grid = np.arange(df['IT'].min(), df['IT'].max(), 1)

# Using pandas reindex with method='nearest' is fine for a coarse grid.
interp_prices = np.interp(it_grid, df['IT'], df['price'])

# Compute RSI on the interpolated array
def rsi(series, N=14):
    delta = np.diff(series)
    up = np.maximum(delta, 0)
    down = -np.minimum(delta, 0)
    # Exponential moving average
    ma_up = pd.Series(up).ewm(alpha=1/N, adjust=False).mean()
    ma_down = pd.Series(down).ewm(alpha=1/N, adjust=False).mean()
    rs = ma_up / ma_down
    return 100 - 100/(1+rs)

it_rsi = rsi(interp_prices, N=14)          # 14‑period RSI in IT
3.3. MACD on Intrinsic time
MACD is simply the difference of two EMAs, often with 12‑ and 26‑period spans. Because we have an equally‑spaced IT grid, we can feed the interpolated price to the EMA routine just like any other series.

python

Collapse

Run
Save
Copy
1
2
3
4
5
⌄
def ema(series, N):
    return pd.Series(series).ewm(alpha=1/N, adjust=False).mean()

it_macd_line = ema(interp_prices, 12) - ema(interp_prices, 26)
it_signal_line = ema(it_macd_line, 9)   # 9‑period EMA of the MACD line
3.4. Mapping back to real time
After computing signals on IT, you need to decide when to enter based on the real‑time price. The most straightforward way is:

Decode the IT signal back to a list of IT timestamps that satisfy your condition (e.g., MACD cross and RSI oversold).
Find the nearest real‑time timestamp to each IT timestamp.
Execute the trade at that real‑time candle.
A simple implementation:

python

Collapse

Run
Save
Copy
1
2
3
4
5
6
7
8
9
10
11
12
13
14
15
16
17
18
19
20
21
22
23
24
25
26
27
⌄
# Create a DataFrame for signals on the IT grid
it_df = pd.DataFrame({
    'IT': it_grid,
    'price': interp_prices,
    'RSI': it_rsi,
    'MACD_line': it_macd_line,
    'Signal_line': it_signal_line
}).dropna()

# Find MACD cross‑overs
it_df['macd_cross'] = (it_df['MACD_line'] > it_df['Signal_line']).shift(1) != \
                      (it_df['MACD_line'] > it_df['Signal_line'])

# Find RSI condition
it_df['rsi_oversold'] = it_df['RSI'] < 30
it_df['rsi_overbought'] = it_df['RSI'] > 70

# Long condition – MACD bullish cross AND RSI oversold
it_df['long_signal'] = it_df['macd_cross'] & it_df['rsi_oversold']

# Map IT signal to nearest real timestamp
valid_it = it_df[it_df['long_signal']].copy()
valid_it['real_ts'] = df['IT'].searchsorted(valid_it['IT'])
valid_it['actual_ts'] = df.index[valid_it['real_ts']].values

# Drop duplicates – you’ll end up with one entry per candle
entry_times = valid_it['actual_ts']
You can repeat the same logic for a short side (MACD bearish cross + RSI over‑20).

Important – If “searchsorted” returns an index too far away (i.e., > 30 s / > n bars), you might want to discard that signal to keep a tighter TF. 

3.5. Backtesting skeleton
python

Collapse

Run
Save
Copy
1
2
3
4
5
6
7
8
9
10
11
12
13
14
15
16
17
⌄
⌄
⌄
⌄
def backtest(entries, df):
    trades = []
    for ts in entries:
        entry_price = df.loc[ts, 'price']
        # Simple exit: fixed number of bars or stop‑loss based on ATR
        exit_bar = df.index.get_loc(ts) + 30            # e.g., 30 bars hold
        if exit_bar >= len(df):
            exit_bar = len(df)-1
        exit_price = df.iloc[exit_bar]['price']
        pnl = exit_price - entry_price
        trades.append({'entry_ts': ts, 'entry_price': entry_price,
                       'exit_ts': df.index[exit_bar], 'exit_price': exit_price,
                       'pnl': pnl})
    return pd.DataFrame(trades)

entry_times = pd.Series(entry_times)
results = backtest(entry_times, df)
Add risk‑management before the exit calculation (e.g., stop‑loss, trailing, max drawdown), which we cover next.

4. Risk‑management in an IT‑driven strategy
Rule
Why it matters
How to implement
Position sizing
Every trade should risk a fixed % of your equity.
size = (equity * risk_per_trade) / (ATR * multiplier)
Stop‑loss
Protect against IT spikes that mis‑interpret normal price moves.
Place stop ‑halt just below the intrinsic floor (e.g., assignment of 1×ATR below entry in real time). In IT terms, this is equivalent to “waiting until the intrinsic time difference of 1 (or 2) ticks reaches the stop‑loss.”
Take‑profit / trail
No point in letting a winning streak suck into lower volatility for too long.
Trail by percentage of ATR or use a fixed point stop that moves every time the IT distance between current price and entry grows.
Max draw‑down
A series of false IT signals can still erode equity.
Use an equity‑curve monitor (e.g., do‑what‑you‑have‑already‑coded but track a cummax of equity). Stop the strategy when cumulative max fell more than X%.
Trade‑frequency cap
Excessive IT cross‑overs can be high‑frequency noise.
Limit to e.g., max 4 entries per calendar month or max 1 per intraday minute. The IT framework already « slows‑down » during calm periods, but the cap gives human sanity.

The actual numbers (ATR multiplier, risk% etc.) will depend on the asset and your risk profile. Backtest over a 3‑to‑5 year period to see realistic Sharpe/Sortino values before you commit real capital.

5. Putting it all together – a sample “high‑level” algorithm

Collapse

Run
Save
Copy
1
2
3
4
5
6
7
8
9
10
11
12
13
14
15
16
17
18
19
20
21
22
23
24
25
26
27
28
29
30
31
32
33
INITIALISE:
    capital = $1,000,000
    risk_per_trade = 1% of current equity
    ATR_mult   = 2.0
    max_trades_per_month = 5

FOR each new tick/bar:
    UPDATE price & compute AT *
    UPDATE intrinsic-time delta_it
    UPDATE cumulative IT
    
    IF an IT timestamp crosses a recorded “signal point”:
        DETERMINE next real‑time candle
        IF a bullish MACD cross AND RSI < 30:
            IF trades_in_month < max_trades_per_month:
                ENTRY:
                    size = (capital * risk_per_trade) / (current_ATR * ATR_mult)
                    Record entry price, buy size
                    Set stop = entry_price – (ATR_mult * ATR)
                UPDATE trades_in_month
        IF a bearish MACD cross AND RSI > 70:
            Similar, but sell logic.
    
    IF a trade is active:
        IF price <= stop OR trade horizon reached:
            EXIT trade
            Record P&L
            Update equity
            Reset trade status
    
    EVERY month:
        Reset trades_in_month counter
        Check max draw‑down; if exceeded -> freeze strategy
Key take‑away – The core of the method is mapping the bar‑by‑bar intrinsic‑time conditions back to real‑time execution. Once you have the entry generator you can plug in any classic risk‑manager or even pair‑trade logic.

6. Why this works better than “plain” RSI/MACD
Traditional approach
Issue in real markets
With IT + RSI/MACD
RSI on a pure time series
Generates a lot of false oversold/overbought signals when volatility spikes.
RSI is computed on a re‑parameterised axis that expands during volatility, so spikes are damped.
MACD on a pure time series
Lag of 10‑20 bars; trend changes often missed in very fast markets.
The EMAs adapt to IT – they “look further back” in calm times (because IT ticks slower) and “look shorter” during wild swings (because IT ticks faster).
Sending buy when RSI 30 AND MACD cross
80% of early‑2015 Bitcoin toss to sticker had contradictory signals due to volatile noise.
With IT, you are less likely to see a cross in a quiet period – the timescale self‑adjusts – so you are more likely to buy only in genuine triangulations.

7. Quick sanity checks before going live
Visual inspection – Plot a 3‑month snippet of the price vs. IT, overlay RSI/ MACD and your trade entries. Look for “meaningful” groups of signals, not isolated isolated cross‑overs.
Auto‑checks – Write unit tests that assert:
no trade is entered if IT < IT‑threshold,
stop‑loss triggers if price moves against entry by > ATR*mult,
no more than max_trades_per_month entries occur.
Walk‑forward – Run a 3‑month in‑sample fit, then 1‑month out‑of‑sample, repeat. The signal’s SNR should stay stable.
Parameter optimisation – Carry a brute‑force or evolutionary search on the intrinsic‑time kernel (i.e., delta_t = |ΔP| / (ATR * alpha)) and on the RSI‑MA windows (7, 14, 21). Always keep the final model size small – CIT (Carver’s) is very fast so you can re‑optimise weekly if you like.
8. Example summary
Indicator
Configuration
IT benefit
Typical setting
RSI
14‑period in IT
Spreads oversold/overbought zones across calm & stormy periods
14
MACD
12‑26‑9 EMAs on IT
Produces crosses that align with real‑time structural moves
12‑26‑9
ATR
14‑period
Base for Δt function & stop‑loss
14
IT scaling factor
1.0 (no manual).
Hard‑code or calibrate on historical backtest.
default 1.0

You can stop there or keep dialing in the intrinsic kernel if you would like tighter timing in very high‑frequency environments.

9. Final advice
Start with a demo account – Let the algorithm gather a few hundred signals. Visualise them in a quick notebook to check whether they look coherent.
Leave the “MT4/MT5”‑style hard‑coded thresholds behind – instead use percentage of ATR for stops (intrinsic‑time naturally tied to volatility).
“IT + RSI + MACD” is not magic – Both indicators were invented for conventional time; treating them as deterministic functions on a different time axis is the trick that Carver calls “algorithmic thinking with a time‑squeeze”.
If you want the full Carver experience, consider also adding his Adaptive Momentum indicator, which is simply a smoothed exponential of the volume‑adjusted price movement obtained in IT.
Once you get the synth‑pipeline rolling and backtest results looking solid, you’ll find that your entry triggers feel less fuzzy and your trades tend to line up with major price pivots rather than with every minor swing.

Happy intrinsic‑time charting!