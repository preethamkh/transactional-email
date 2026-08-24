using System.Text.Json;
using EmailCentral.Api.Domain;

namespace EmailCentral.Api.Logging;

/// <summary>
/// Append-only JSONL activity log plus in-memory tail for querying.
/// POC persistence: logs/activity-yyyyMM.jsonl. Production would use SQL.
/// </summary>
public sealed class ActivityLog
{
    private readonly object _gate = new();
    private readonly List<ActivityEntry> _recent = [];
    private readonly string _logsDirectory;

    public ActivityLog(string logsDirectory)
    {
        _logsDirectory = logsDirectory;
        Directory.CreateDirectory(logsDirectory);
    }

    public void Append(ActivityEntry entry)
    {
        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        var filePath = Path.Combine(_logsDirectory, $"activity-{entry.Timestamp:yyyyMM}.jsonl");

        lock (_gate)
        {
            File.AppendAllText(filePath, line);
            _recent.Add(entry);
            if (_recent.Count > 500)
            {
                _recent.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<ActivityEntry> Query(int take)
    {
        lock (_gate)
        {
            return _recent.TakeLast(Math.Clamp(take, 1, 100)).Reverse().ToArray();
        }
    }
}
