using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using TomasAI.IFM.Domain.MarketData.Analytics.FuturesItiSignal.Command.Model;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.Commands;
using TomasAI.IFM.Domain.MarketData.Analytics.Shared.ViewModels;

if (args.Length == 2 && args[0] == "--batch-evidence")
{
    foreach (var line in File.ReadLines(args[1]))
    {
        var paths = line.Split('\t');
        try
        {
            await PilotEvidence.RunAsync(paths[0], paths[1], paths[2]);
        }
        catch (Exception exception)
        {
            File.WriteAllText(paths[2], JsonSerializer.Serialize(new { error = exception.Message }));
            Console.Error.WriteLine($"{paths[1]}: {exception.Message}");
        }
    }
    return;
}

if (args.Length == 4 && args[0] == "--pilot-evidence")
{
    await PilotEvidence.RunAsync(args[1], args[2], args[3]);
    return;
}

if (args.Length is < 2 or > 3)
    throw new ArgumentException("Usage: MonthlyItiReplay <input.json> <output.jsonl> [last-value-date]");

var manifestPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
var lastValueDate = args.Length == 3
    ? DateOnly.ParseExact(args[2], "yyyy-MM-dd", CultureInfo.InvariantCulture)
    : DateOnly.MaxValue;
var json = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};
var manifest = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), json)
    ?? throw new InvalidDataException("Replay manifest is missing.");
var compute = typeof(FuturesItiSignalCompute)
    .GetMethod("TryCompute", BindingFlags.NonPublic | BindingFlags.Static,
        [typeof(GenerateFuturesItiSignalCommand), typeof(FuturesItiSignalV2ReadModel), typeof(FuturesItiSignalV2ReadModel).MakeByRefType()])!
    .CreateDelegate<Compute>();
var daily = new Dictionary<(string Contract, DateOnly ValueDate), FuturesItiSignalV2ReadModel>();
var monthly = new Dictionary<(string Contract, DateOnly Bucket), FuturesItiSignalV2ReadModel>();
long tickCount = 0, dailyCount = 0, monthlyCount = 0;
var inputFolder = Path.GetDirectoryName(manifestPath)!;
var temporary = outputPath + ".partial";
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
using (var output = new StreamWriter(temporary))
{
    foreach (var file in manifest.Files.OrderBy(file => file.ValueDate, StringComparer.Ordinal))
    {
        var date = DateOnly.ParseExact(file.ValueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (date > lastValueDate)
            break;
        var path = Path.Combine(inputFolder, file.File);
        using var input = File.OpenRead(path);
        if (!Convert.ToHexString(SHA256.HashData(input)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Checksum mismatch for {file.File}.");
        input.Position = 0;
        using var reader = new StreamReader(input);
        var bucket = new DateOnly(date.Year, date.Month, 1);
        var monthlyKey = (file.ContractId, bucket);
        var processedRows = 0;
        FuturesItiSignalV2ReadModel? dailyState = null;
        monthly.TryGetValue(monthlyKey, out var monthlyState);
        long previousSourceTime = 0;
        var fields = new Range[9];
        while (reader.ReadLine() is { } line)
        {
            var span = line.AsSpan();
            if (span.Split(fields, ',') != fields.Length || !span[fields[0]].SequenceEqual(file.ValueDate))
                throw new InvalidDataException($"Unexpected row in {file.File}.");
            var timestamp = long.Parse(span[fields[2]], CultureInfo.InvariantCulture);
            var vxTimestamp = long.Parse(span[fields[5]], CultureInfo.InvariantCulture);
            var esReceiveTime = long.Parse(span[fields[3]], CultureInfo.InvariantCulture);
            var vxReceiveTime = long.Parse(span[fields[6]], CultureInfo.InvariantCulture);
            var price = double.Parse(span[fields[4]], CultureInfo.InvariantCulture);
            var vx = double.Parse(span[fields[7]], CultureInfo.InvariantCulture);
            if (timestamp < previousSourceTime || vxTimestamp >= timestamp || price <= 0 || vx <= 0)
                throw new InvalidDataException($"Invalid time or price in {file.File}, row {processedRows + 1}.");
            previousSourceTime = timestamp;
            processedRows++;
            tickCount++;

            var time = DateTime.UnixEpoch.AddTicks(timestamp / 100);
            var dailyCommand = new GenerateFuturesItiSignalCommand(file.ContractId, date, TimeFrameType.Daily, time, price, vx);
            if (!compute(dailyCommand, dailyState, out var dailySignal))
                continue;
            dailyState = dailySignal;
            dailyCount++;

            var monthlyCommand = new GenerateFuturesItiSignalCommand(file.ContractId, date, TimeFrameType.Monthly,
                dailySignal.IntrinsicTime, dailySignal.IntrinsicPrice, vx, bucket);
            if (!compute(monthlyCommand, monthlyState, out var monthlySignal))
                continue;
            monthlyState = monthlySignal;
            monthlyCount++;
            var availableAtNs = Math.Max(timestamp, Math.Max(esReceiveTime, vxReceiveTime));
            output.WriteLine(JsonSerializer.Serialize(new
            {
                FileValueDate = date,
                Contract = file.ContractId,
                Bucket = bucket,
                SourceRow = processedRows,
                SourceTimestampNs = timestamp,
                AvailableAtNs = availableAtNs,
                VxTimestampNs = vxTimestamp,
                VxPrice = vx,
                DailyMode = dailySignal.IntrinsicTimeMode.ToString(),
                Mode = monthlySignal.IntrinsicTimeMode.ToString(),
                Direction = monthlySignal.IntrinsicTimeTrend.ToString(),
                Group = monthlySignal.IntrinsicTimeGroupId,
                FuturesPrice = monthlySignal.IntrinsicPrice,
                monthlySignal.Threshold, monthlySignal.BandLevel, monthlySignal.ReversalLevel, monthlySignal.SequenceId
            }, json));
        }
        if (processedRows != file.Rows)
            throw new InvalidDataException($"Expected {file.Rows} rows in {file.File}, found {processedRows}.");
        if (monthlyState is not null)
            monthly[monthlyKey] = monthlyState;
    }
}
File.Move(temporary, outputPath, overwrite: true);
Console.WriteLine($"Read {tickCount:N0} paired ticks. Generated {dailyCount:N0} Daily and {monthlyCount:N0} Monthly signals across {monthly.Count} contract/month buckets.");

internal delegate bool Compute(GenerateFuturesItiSignalCommand command, FuturesItiSignalV2ReadModel? current,
    out FuturesItiSignalV2ReadModel signal);
internal sealed record Manifest(InputFile[] Files);
internal sealed record InputFile(string File, string ValueDate, string ContractId, int Rows, string Sha256);
