using System.Text;
using TomasAI.IFM.Domain.SystemAdmin.Shared.ScheduledTask.Contracts;
namespace TomasAI.IFM.Application.Storage.ScheduledTaskDb;
/// <summary>Reads retained stdout from the configured local or mounted host artifact root in bounded pages.</summary>
public sealed class ScheduledTaskOutputReader(string root) : IScheduledTaskOutputReader
{
    /// <inheritdoc />
    public async ValueTask<ScheduledTaskOutputPage> ReadAsync(string relativeDirectory, long offset, CancellationToken cancellationToken)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (string.IsNullOrWhiteSpace(relativeDirectory)) return new() { EndOfOutput = true };
        var canonicalRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(canonicalRoot, relativeDirectory, "stdout.log"));
        if (!path.StartsWith(canonicalRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidOperationException("ScheduledTask output path escapes its configured root.");
        // Reject symlink/junction components so relative paths cannot escape through a mounted link.
        for (var component = Path.GetDirectoryName(path); component is not null && component.StartsWith(canonicalRoot, StringComparison.Ordinal); component = Path.GetDirectoryName(component))
            if (Directory.Exists(component) && (File.GetAttributes(component) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("ScheduledTask output directory cannot be a symbolic link.");
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("ScheduledTask output cannot be a symbolic link.");
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536, true);
            if (offset > stream.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            stream.Seek(offset, SeekOrigin.Begin);
            var buffer = new byte[65536];
            var count = await stream.ReadAsync(buffer, cancellationToken);
            var final = offset + count >= stream.Length;
            // Leave an incomplete UTF-8 character for the next page rather than corrupting it.
            var consumed = count;
            if (!final && count > 0)
            {
                var lead = count - 1;
                while (lead > 0 && (buffer[lead] & 0xC0) == 0x80) lead--;
                var needed = buffer[lead] >= 0xF0 ? 4 : buffer[lead] >= 0xE0 ? 3 : buffer[lead] >= 0xC0 ? 2 : 1;
                if (count - lead < needed) consumed = lead;
            }
            return new() { Text = Encoding.UTF8.GetString(buffer, 0, consumed), NextOffset = offset + consumed, EndOfOutput = final, Available = true };
        }
        catch (FileNotFoundException) { return new() { EndOfOutput = true }; }
        catch (DirectoryNotFoundException) { return new() { EndOfOutput = true }; }
    }
}
