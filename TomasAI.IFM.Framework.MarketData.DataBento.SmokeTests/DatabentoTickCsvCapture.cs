using System.Globalization;
using System.Text;

namespace TomasAI.IFM.Framework.MarketData.DataBento.SmokeTests;

internal sealed class DatabentoTickCsvCapture : IDisposable
{
    internal const string OutputDirectoryEnvironmentVariable =
        "IFM_DATABENTO_TICK_CSV_DIRECTORY";

    private const decimal PriceScale = 1_000_000_000m;
    private const int WriterBufferSize = 1024 * 1024;

    private readonly IReadOnlyDictionary<InstrumentKey, string> _symbols;
    private readonly FileStream _stream;
    private readonly StreamWriter _writer;
    private readonly StringBuilder _line = new(512);
    private long _rows;
    private bool _disposed;

    private DatabentoTickCsvCapture(
        string filePath,
        IReadOnlyDictionary<InstrumentKey, string> symbols)
    {
        FilePath = filePath;
        _symbols = symbols;
        _stream = new FileStream(
            filePath,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                BufferSize = WriterBufferSize,
                Options = FileOptions.SequentialScan
            });
        _writer = new StreamWriter(
            _stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            WriterBufferSize,
            leaveOpen: true)
        {
            NewLine = "\n"
        };
        _writer.WriteLine(
            "ordinal,raw_symbol,publisher_id,instrument_id,record_kind,"
            + "event_timestamp_ns,receive_timestamp_ns,receive_minus_event_ns,"
            + "sequence,source_schema,header_flags,"
            + "bid_price_raw,bid_price,bid_size,bid_count,"
            + "ask_price_raw,ask_price,ask_size,ask_count,"
            + "trade_price_raw,trade_price,trade_size,trade_action,trade_side,"
            + "trade_dbn_flags,trade_depth,trade_timestamp_in_delta_ns,"
            + "trade_channel_id,trade_timestamp_out_ns,"
            + "mbo_order_id,mbo_price_raw,mbo_price,mbo_size,mbo_action,"
            + "mbo_side,mbo_dbn_flags,mbo_timestamp_in_delta_ns,mbo_channel_id");
    }

    internal string FilePath { get; }

    internal long Rows => Interlocked.Read(ref _rows);

    internal long Bytes => _disposed
        ? new FileInfo(FilePath).Length
        : _stream.Position;

    internal static DatabentoTickCsvCapture? CreateIfEnabled(
        string captureName,
        IReadOnlyDictionary<InstrumentKey, string> symbols)
    {
        var directory = Environment.GetEnvironmentVariable(
            OutputDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var fullDirectory = Path.GetFullPath(directory);
        Directory.CreateDirectory(fullDirectory);
        var timestamp = DateTimeOffset.UtcNow.ToString(
            "yyyyMMddTHHmmssfffZ",
            CultureInfo.InvariantCulture);
        var filePath = Path.Combine(
            fullDirectory,
            $"databento-{captureName}-{timestamp}.csv");
        return new DatabentoTickCsvCapture(filePath, symbols);
    }

    internal void Write(long ordinal, in MarketRecord64 record)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var header = record.Header;
        var instrument = new InstrumentKey(
            header.PublisherId,
            header.InstrumentId);

        _line.Clear();
        _line.Append(ordinal).Append(',');
        AppendCsv(_symbols.GetValueOrDefault(instrument));
        _line.Append(',').Append(header.PublisherId)
            .Append(',').Append(header.InstrumentId)
            .Append(',').Append(header.RecordKind)
            .Append(',').Append(header.EventTimestampNanoseconds)
            .Append(',').Append(header.ReceiveTimestampNanoseconds)
            .Append(',').Append(
                unchecked(
                    header.ReceiveTimestampNanoseconds
                    - header.EventTimestampNanoseconds))
            .Append(',').Append(header.Sequence)
            .Append(',').Append(header.SourceSchema)
            .Append(',').Append(header.Flags)
            .Append(',');

        switch (header.RecordKind)
        {
            case MarketRecordKind.Quote:
                AppendQuote(record.Quote);
                break;
            case MarketRecordKind.Trade:
                AppendTrade(record.Trade);
                break;
            case MarketRecordKind.Mbo:
                AppendMbo(record.Mbo);
                break;
            default:
                AppendTrailingEmptyFields(27);
                break;
        }

        _writer.WriteLine(_line);
        Interlocked.Increment(ref _rows);
    }

    internal void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _writer.Flush();
        _stream.Flush(flushToDisk: false);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _writer.Flush();
        _writer.Dispose();
        _stream.Dispose();
        _disposed = true;
    }

    private void AppendQuote(in QuoteRecord64 quote)
    {
        AppendPrice(quote.BidPrice);
        _line.Append(',').Append(quote.BidSize)
            .Append(',').Append(quote.BidCount)
            .Append(',');
        AppendPrice(quote.AskPrice);
        _line.Append(',').Append(quote.AskSize)
            .Append(',').Append(quote.AskCount)
            .Append(',');
        AppendTrailingEmptyFields(19);
    }

    private void AppendTrade(in TradeRecord64 trade)
    {
        AppendEmptyFieldsBefore(8);
        AppendPrice(trade.Price);
        _line.Append(',').Append(trade.Size)
            .Append(',').Append(trade.Action)
            .Append(',').Append(trade.Side)
            .Append(',').Append(trade.DbnFlags)
            .Append(',').Append(trade.Depth)
            .Append(',').Append(trade.TimestampInDeltaNanoseconds)
            .Append(',').Append(trade.ChannelId)
            .Append(',').Append(trade.TimestampOutNanoseconds)
            .Append(',');
        AppendTrailingEmptyFields(9);
    }

    private void AppendMbo(in MboRecord64 mbo)
    {
        AppendEmptyFieldsBefore(18);
        _line.Append(mbo.OrderId).Append(',');
        AppendPrice(mbo.Price);
        _line.Append(',').Append(mbo.Size)
            .Append(',').Append(mbo.Action)
            .Append(',').Append(mbo.Side)
            .Append(',').Append(mbo.DbnFlags)
            .Append(',').Append(mbo.TimestampInDeltaNanoseconds)
            .Append(',').Append(mbo.ChannelId);
    }

    private void AppendPrice(long rawPrice)
    {
        _line.Append(rawPrice).Append(',');
        if (rawPrice != long.MaxValue)
        {
            _line.AppendFormat(
                CultureInfo.InvariantCulture,
                "{0:0.000000000}",
                rawPrice / PriceScale);
        }
    }

    private void AppendCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (!value.Contains(',')
            && !value.Contains('"')
            && !value.Contains('\r')
            && !value.Contains('\n'))
        {
            _line.Append(value);
            return;
        }

        _line.Append('"');
        foreach (var character in value)
        {
            if (character == '"')
            {
                _line.Append('"');
            }
            _line.Append(character);
        }
        _line.Append('"');
    }

    private void AppendEmptyFieldsBefore(int count)
    {
        if (count > 0)
        {
            _line.Append(',', count);
        }
    }

    private void AppendTrailingEmptyFields(int count)
    {
        if (count > 0)
        {
            _line.Append(',', count - 1);
        }
    }
}
