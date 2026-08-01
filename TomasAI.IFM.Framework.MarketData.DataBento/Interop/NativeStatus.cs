using System.Text;
using TomasAI.IFM.Framework.MarketData.DataBento;

namespace TomasAI.IFM.Framework.MarketData.DataBento.Interop;

internal static class NativeStatus
{
    internal static void ThrowIfFailed(
        DatabentoFeedStatus status,
        SafeDbFeedHandle? feed,
        string operation)
    {
        if (status == DatabentoFeedStatus.Ok)
        {
            return;
        }
        var detail = feed is null || feed.IsInvalid || feed.IsClosed
            ? null
            : ReadLastError(feed);
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"{operation} failed with {status}."
            : $"{operation} failed with {status}: {detail}";
        if (status == DatabentoFeedStatus.Timeout)
        {
            throw new DatabentoFeedTimeoutException(message);
        }
        throw new DatabentoFeedException(status, message);
    }

    private static unsafe string? ReadLastError(SafeDbFeedHandle feed)
    {
        var status = NativeMethods.FeedGetLastError(feed, null, 0, out var required);
        if (status != DatabentoFeedStatus.BufferTooSmall || required <= 1)
        {
            return null;
        }
        var buffer = new byte[required];
        fixed (byte* pointer = buffer)
        {
            status = NativeMethods.FeedGetLastError(feed, pointer, required, out _);
        }
        return status == DatabentoFeedStatus.Ok
            ? Encoding.UTF8.GetString(buffer.AsSpan(0, checked((int)required - 1)))
            : null;
    }
}
