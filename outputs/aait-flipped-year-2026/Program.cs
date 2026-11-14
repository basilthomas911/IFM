using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

var input = Path.GetFullPath(args[0]);
var output = args[1];
var source = Path.GetDirectoryName(input)!;
var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(input), options)!;
var zone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
var compute = typeof(FuturesItiSignalCompute).GetMethod("TryCompute",
    BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<Compute>();
var days = new Day[manifest.Files.Length];
Parallel.For(0, days.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, index =>
{
    var file = manifest.Files[index];
    var date = DateOnly.ParseExact(file.ValueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    var start = Epoch(date, new TimeOnly(9, 30));
    var end = Epoch(date, new TimeOnly(15, 55));
    var filePath = Path.Combine(source, file.File);
    using (var stream = File.OpenRead(filePath))
        Require(Convert.ToHexString(SHA256.HashData(stream)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase));
    FuturesItiSignalV2ReadModel? previous = null;
    DateTime priorStart = default;
    var ticks = new long[file.Rows];
    var prices = new int[file.Rows];
    var candidates = new List<Candidate>();
    var dcCount = 0;
    var signalCount = 0;

    var row = 0;
    Span<Range> cols = stackalloc Range[9];
    foreach (var line in File.ReadLines(filePath))
    {
        var span = line.AsSpan();
        Require(span.Split(cols, ',') == cols.Length && span[cols[0]].SequenceEqual(file.ValueDate));
        var ns = long.Parse(span[cols[2]], CultureInfo.InvariantCulture);
        var vxNs = long.Parse(span[cols[5]], CultureInfo.InvariantCulture);
        var px = double.Parse(span[cols[4]], CultureInfo.InvariantCulture);
        var vx = double.Parse(span[cols[7]], CultureInfo.InvariantCulture);
        var tick = (int)(px * 4);
        Require(px > 0 && vx > 0 && vxNs < ns && (row == 0 || ticks[row - 1] <= ns) && tick / 4.0 == px);
        ticks[row] = ns;
        prices[row] = tick;

        var time = DateTime.UnixEpoch.AddTicks(ns / 100);
        var command = new GenerateFuturesItiSignalCommand(file.ContractId, date, TimeFrameType.Daily, time, px, vx);
        if (compute(command, previous, out var signal))
        {
            signalCount++;
            if (signal.IntrinsicTimeMode == IntrinsicTimeModeType.TrendDirectionChanged)
            {
                var group = signal.IntrinsicTimeGroupId;
                if (group > 0 && ns >= start && ns < end)
                {
                    dcCount++;
                    var sign = signal.IntrinsicTimeTrend == IntrinsicTimeTrendType.UpTrend ? -1 : 1;
                    Require(previous != null && previous.IntrinsicTimeGroupId == group - 1);
                    Require(signal.Threshold > 0 && (sign > 0 || signal.Threshold == 5));
                    var five = Window(5);
                    var fifteen = Window(15);
                    candidates.Add(new Candidate(group, row, sign, signal.Threshold,
                        five.Range, fifteen.Range, five.Momentum, fifteen.Momentum,
                        (time - priorStart).TotalMinutes, (int)previous!.IntrinsicTimeMode));
                }
                priorStart = time;
            }
            previous = signal;
        }
        row++;

        (double Range, double Momentum) Window(int minutes)
        {
            var bound = ns - minutes * 60_000_000_000L;
            var left = 0;
            var right = row;
            while (left < right)
            {
                var mid = (left + right + 1) / 2;
                if (ticks[mid] <= bound) left = mid;
                else right = mid - 1;
            }
            var high = prices[row];
            var low = high;
            for (var sample = left; sample <= row; sample++)
            {
                high = Math.Max(high, prices[sample]);
                low = Math.Min(low, prices[sample]);
            }
            return ((high - low) / 4.0, (prices[row] - prices[left]) / 4.0);
        }
    }
    Require(row == file.Rows);
    var resolved = new List<Trade>();
    foreach (var c in candidates)
    {
        var entryRow = c.Row + 1;
        if (entryRow >= row || ticks[entryRow] >= end) continue;
        var entry = prices[entryRow];
        var best = entry;
        var distance = (int)Math.Ceiling(c.Threshold * 4);
        var exitRow = entryRow + 1;
        bool stopped;
        while (true)
        {
            if (exitRow >= row || ticks[exitRow] >= end)
            {
                if (exitRow >= row || ticks[exitRow] > end) exitRow--;
                stopped = false;
                break;
            }
            var price = prices[exitRow];
            if (c.Sign * (price - best) > 0) best = price;
            if (c.Sign * (best - price) >= distance)
            {
                stopped = true;
                break;
            }
            exitRow++;
        }
        var exit = prices[exitRow];
        resolved.Add(new Trade(c, entryRow + 1, exitRow + 1, entry / 4.0, exit / 4.0,
            best / 4.0, stopped, c.Sign * (best - entry) >= distance, c.Sign * (exit - entry) * 12.5));
    }
    days[index] = new Day(file.ValueDate, file.ContractId, row, signalCount, dcCount, ticks[row - 1] >= end, resolved);
    if ((index + 1) % 20 == 0) Console.WriteLine($"{date}: {index + 1}/{days.Length}");
});
Require(days.Sum(day => (long)day.Rows) == manifest.EsRows);
File.WriteAllText(output, JsonSerializer.Serialize(days, options));
Console.WriteLine($"Read {days.Length} dates and {manifest.EsRows:N0} ticks. {days.Sum(day => day.SignalCount):N0} C# Daily signals.");

long Epoch(DateOnly date, TimeOnly time) =>
    (TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time), zone).Ticks - DateTime.UnixEpoch.Ticks) * 100;

static void Require(bool valid, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(valid))] string? expression = null)
{
    if (!valid) throw new InvalidDataException($"Invalid replay input or signal state: {expression}");
}

delegate bool Compute(GenerateFuturesItiSignalCommand command, FuturesItiSignalV2ReadModel? current, out FuturesItiSignalV2ReadModel signal);
record Manifest(InputFile[] Files, long EsRows);
record InputFile(string File, string ValueDate, string ContractId, int Rows, string Sha256);
record Candidate(long Group, int Row, int Sign, double Threshold, double Range5, double Range15, double Momentum5, double Momentum15,
    double PriorMinutes, int PriorMode);
record Trade(Candidate Candidate, int EntryRow, int ExitRow, double EntryPrice, double ExitPrice,
    double BestPrice, bool Stopped, bool Hit, double Gross);
record Day(string Date, string Contract, int Rows, int SignalCount, int DcCount, bool HasCutoffTick, List<Trade> Trades);
