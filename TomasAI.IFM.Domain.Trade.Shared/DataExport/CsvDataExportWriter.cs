using System.Globalization;
using System.Text;

namespace TomasAI.IFM.Domain.Trade.Shared.DataExport;

static class CsvDataExportWriter
{
    public static async Task ExportAsync<T>(IReadOnlyCollection<T> results, string fileName,
        bool overwrite, string[] headers, Func<T, string[]> row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(row);
        cancellationToken.ThrowIfCancellationRequested();

        var target = Path.GetFullPath(fileName);
        var directory = Path.GetDirectoryName(target);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            throw new DirectoryNotFoundException($"The CSV export directory does not exist: {directory}");
        if (!overwrite && File.Exists(target))
            throw new IOException($"The CSV export file already exists: {target}");

        var temporary = Path.Combine(directory, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(true)) { NewLine = "\r\n" })
            {
                await WriteRowAsync(writer, headers, cancellationToken).ConfigureAwait(false);
                foreach (var item in results)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var values = row(item);
                    if (values.Length != headers.Length)
                        throw new InvalidOperationException(
                            $"CSV row has {values.Length} values but {headers.Length} headers were declared.");
                    await WriteRowAsync(writer, values, cancellationToken).ConfigureAwait(false);
                }
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, target, overwrite);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    static Task WriteRowAsync(StreamWriter writer, IEnumerable<string> values,
        CancellationToken cancellationToken) => writer.WriteLineAsync(
            string.Join(',', values.Select(Escape)).AsMemory(), cancellationToken);

    static string Escape(string? value)
    {
        value ??= string.Empty;
        return value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    public static string Value(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Value(ushort value) => value.ToString(CultureInfo.InvariantCulture);
    public static string Value(bool value) => value ? "true" : "false";
    public static string Values<T>(IEnumerable<T> values) => string.Join('|', values);
}
