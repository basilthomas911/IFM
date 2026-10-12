using System.Net;
using System.Net.Http;

namespace TomasAI.IFM.Framework.Telemetry.Logging.Serilog;

/// <summary>Sends a sink-owned log stream without taking ownership of that stream.</summary>
internal sealed class BorrowedLogStreamContent(Stream source) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => source.CopyToAsync(stream);

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        => source.CopyToAsync(stream, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        length = source.CanSeek ? source.Length - source.Position : 0;
        return source.CanSeek;
    }
}
