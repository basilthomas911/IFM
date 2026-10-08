using System.Text;
using TomasAI.IFM.Application.Storage.ScheduledTaskDb;
namespace TomasAI.IFM.Domain.SystemAdmin.UnitTests;
/// <summary>Checks complete paged UTF-8 output and artifact boundaries.</summary>
public sealed class ScheduledTaskOutputReaderTests
{
    [Fact]
    public async Task Retained_output_pages_preserve_utf8_and_all_captured_text()
    {
        var root = Path.Combine(Path.GetTempPath(), "ifm-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "task", "run"));
        var path = Path.Combine(root, "task", "run", "stdout.log");
        try
        {
            var text = new string('x', 65535) + "\u20ac" + new string('y', 70000);
            await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
            var reader = new ScheduledTaskOutputReader(root);
            var result = new StringBuilder(); long offset = 0;
            while (true)
            {
                var page = await reader.ReadAsync(Path.Combine("task", "run"), offset, CancellationToken.None);
                Assert.True(page.Available); Assert.True(page.NextOffset > offset); Assert.True(page.NextOffset - offset <= 65536);
                result.Append(page.Text); offset = page.NextOffset;
                if (page.EndOfOutput) break;
            }
            Assert.Equal(text, result.ToString());
            var missing = await reader.ReadAsync("missing", 0, CancellationToken.None);
            Assert.False(missing.Available); Assert.True(missing.EndOfOutput);
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.ReadAsync("../outside", 0, CancellationToken.None).AsTask());
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadAsync("task/run", -1, CancellationToken.None).AsTask());
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(Path.Combine(root, "task", "run")); Directory.Delete(Path.Combine(root, "task")); Directory.Delete(root);
        }
    }
}
