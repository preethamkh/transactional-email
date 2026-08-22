using System.Text.Json;

namespace MailchimpPoc;

/// <summary>
/// Append-only JSONL operation log: one line per POC operation.
/// This is the logging evidence compared against provider-side reporting.
/// </summary>
public sealed class JsonLinesLogger
{
    private readonly object _gate = new();
    private readonly string _logFilePath;

    public JsonLinesLogger(string logsDirectory)
    {
        Directory.CreateDirectory(logsDirectory);
        _logFilePath = Path.Combine(logsDirectory, $"poc-log-{DateTime.UtcNow:yyyyMMdd}.jsonl");
    }

    public async Task WriteAsync(string operation, string target, string status, long latencyMs, string? error = null)
    {
        var entry = new
        {
            ts = DateTimeOffset.UtcNow.ToString("O"),
            op = operation,
            target,
            status,
            latencyMs,
            error
        };

        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        lock (_gate)
        {
            File.AppendAllText(_logFilePath, line);
        }

        Console.WriteLine($"[log] {entry.ts} op={operation} target={target} status={status} latency={latencyMs}ms{(error is null ? "" : $" error={error}")}");
        await Task.CompletedTask;
    }
}
