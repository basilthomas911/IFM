# ES iron condor Monte Carlo experiment

The Windows x64 DLL exports a versioned C ABI in `condor_mc.h`. Python calls the DLL for the offline replay. Inputs and outputs contain fixed-width scalar fields and caller-owned arrays. The interface can also be called through C# P/Invoke or implemented by another native library.

Build from the repository root in an x64 Visual Studio Developer Command Prompt with CUDA on the path.

```bat
mkdir .artifacts\condor-monte-carlo
nvcc -std=c++17 -O3 -arch=sm_89 --shared native\CondorMonteCarlo.Cuda\condor_mc.cu -o .artifacts\condor-monte-carlo\condor_mc.dll
```

Run with the existing Databento replay environment.

```powershell
& .artifacts/es-backtest-venv/Scripts/python.exe scripts/Backtest/replay_condor_monte_carlo.py
```

Pass `--call-sweep` to compare call-side thresholds from 70% to 100% in five-point steps. It reuses one forecast per eligible negative-P&L observation with the short call at least as high in absolute delta as the short put. Each threshold is replayed independently and takes its first executable exit. Results go to `.artifacts/condor-monte-carlo/call-sweep.json`.

The replay verifies the original 37 fills from cached quotes and checks every saved Monthly ITI event while the position is open. Forecasts require a fresh as-of book and negative P&L marked from the original opening credit to the closing midpoint. ITI steps and hypothetical early exits can occur outside regular hours if the quotes are available. A simulated exit uses the first eligible closing ask at or after the ITI event. The report records exclusions, decisions and actual counterfactual fills in `.artifacts/condor-monte-carlo/replay.json`.

The experiment uses a driftless ES process with volatility set to the mean of the current short-leg IVs, a common stochastic log-IV factor with fixed 50% annual volatility and −0.60 correlation to the ES shock, and current per-leg IVs to initialize the skew. It reprices the four options with Black-76 on a 15-minute regular-hours grid, using the current quote width for the simulated closing spread. The weekday grid is a simplified trading calendar. The policy asks to exit when the pressured side has at least 50% simulated 30-delta stop probability and the model's expected terminal P&L is below the current executable P&L. These assumptions are part of the experiment and have not been calibrated or validated out of sample.
