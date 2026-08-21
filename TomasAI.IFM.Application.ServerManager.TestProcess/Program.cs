using System.IO.Pipes;
using System.Text;

namespace TomasAI.IFM.Application.ServerManager.TestProcess;

public static class TestProcessMarker;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var options = Parse(args);
        var stdoutCount = GetInt(options, "stdout-count");
        var stderrCount = GetInt(options, "stderr-count");

        for (var index = 0; index < Math.Max(stdoutCount, stderrCount); index++)
        {
            if (index < stdoutCount)
            {
                Console.Out.WriteLine($"stdout-{index}");
            }

            if (index < stderrCount)
            {
                Console.Error.WriteLine($"stderr-{index}");
            }
        }

        if (options.TryGetValue("wait-for-shutdown", out var expectedInput))
        {
            var input = await Console.In.ReadLineAsync();
            if (!string.Equals(input, expectedInput, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"unexpected-shutdown-input:{input}");
                return 91;
            }

            Console.Out.WriteLine("graceful-shutdown");
        }

        if (options.ContainsKey("wait-for-control-pipe"))
        {
            var pipeName = Environment.GetEnvironmentVariable("IFM_TASK_CONTROL_PIPE")
                ?? throw new InvalidOperationException("IFM_TASK_CONTROL_PIPE was not supplied.");
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.WaitForConnectionAsync();
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), leaveOpen: true);
            var command = await reader.ReadLineAsync();
            if (!string.Equals(command, "Cancel", StringComparison.Ordinal))
            {
                return 92;
            }

            Console.Out.WriteLine("control-pipe-cancelled");
            return 2;
        }

        var delayMilliseconds = GetInt(options, "delay-ms");
        if (delayMilliseconds > 0)
        {
            await Task.Delay(delayMilliseconds);
        }

        return GetInt(options, "exit-code");
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < args.Length; index += 2)
        {
            var key = args[index].TrimStart('-');
            result[key] = index + 1 < args.Length ? args[index + 1] : string.Empty;
        }

        return result;
    }

    private static int GetInt(IReadOnlyDictionary<string, string> options, string key)
        => options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : 0;
}
