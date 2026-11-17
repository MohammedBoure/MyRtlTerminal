using System.IO;
using System.Text.Json;

namespace RtlTerminal;

public sealed record CommandSuggestion(string Command, bool IsHistory);

/// <summary>
/// Persists recent terminal commands and ranks prefix/substring suggestions for autocomplete.
/// </summary>
public sealed class CommandHistoryStore
{
    private const int MaximumEntries = 500;
    private readonly string _filePath;
    private readonly object _gate = new();
    private readonly List<string> _entries = [];
    private bool _loaded;
    private int _savePending;

    public CommandHistoryStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RtlTerminal",
            "command-history.json");
    }

    public void EnsureLoadedAsync()
    {
        lock (_gate)
        {
            if (_loaded) return;
        }
        _ = Task.Run(EnsureLoaded);
    }

    private void EnsureLoaded()
    {
        lock (_gate)
        {
            if (_loaded) return;
            _loaded = true;
            try
            {
                if (!File.Exists(_filePath)) return;
                var json = File.ReadAllText(_filePath);
                var items = JsonSerializer.Deserialize<List<string>>(json);
                if (items is null) return;
                foreach (var item in items)
                {
                    if (!string.IsNullOrWhiteSpace(item) && _entries.Count < MaximumEntries)
                        _entries.Add(item);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Ignore corrupt or inaccessible history; startup and typing must remain unaffected.
            }
        }
    }

    public void Record(string command)
    {
        var trimmed = command.Trim();
        if (trimmed.Length == 0 || trimmed.Length > 512) return;
        if (trimmed.Contains('\r') || trimmed.Contains('\n')) return;

        lock (_gate)
        {
            EnsureLoaded();
            var existing = _entries.FindIndex(x => string.Equals(x, trimmed, StringComparison.Ordinal));
            if (existing == 0) return;
            if (existing > 0) _entries.RemoveAt(existing);
            _entries.Insert(0, trimmed);
            if (_entries.Count > MaximumEntries)
                _entries.RemoveRange(MaximumEntries, _entries.Count - MaximumEntries);
        }

        QueueSave();
    }

    public IReadOnlyList<CommandSuggestion> Suggest(
        string prefix,
        IReadOnlyList<string> builtInCommands,
        int maxCount = 8)
    {
        var query = prefix.TrimStart();
        if (query.Length == 0) return Array.Empty<CommandSuggestion>();

        List<string> historySnapshot;
        lock (_gate)
        {
            EnsureLoaded();
            historySnapshot = [.. _entries];
        }

        var results = new List<CommandSuggestion>(maxCount);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfMatch(string candidate, bool isHistory)
        {
            if (results.Count >= maxCount) return;
            if (string.Equals(candidate, query, StringComparison.OrdinalIgnoreCase)) return;
            if (seen.Add(candidate))
                results.Add(new CommandSuggestion(candidate, isHistory));
        }

        // 1. Recent history prefix matches (most recent first)
        foreach (var item in historySnapshot)
        {
            if (item.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                AddIfMatch(item, isHistory: true);
            if (results.Count >= maxCount) return results;
        }

        // 2. Built-in RtlTerminal command prefix matches
        foreach (var cmd in builtInCommands)
        {
            if (cmd.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                AddIfMatch(cmd, isHistory: false);
            if (results.Count >= maxCount) return results;
        }

        // 3. Recent history substring matches
        if (query.Length >= 2)
        {
            foreach (var item in historySnapshot)
            {
                if (item.Contains(query, StringComparison.OrdinalIgnoreCase))
                    AddIfMatch(item, isHistory: true);
                if (results.Count >= maxCount) return results;
            }

            foreach (var cmd in builtInCommands)
            {
                if (cmd.Contains(query, StringComparison.OrdinalIgnoreCase))
                    AddIfMatch(cmd, isHistory: false);
                if (results.Count >= maxCount) return results;
            }
        }

        return results;
    }

    private void QueueSave()
    {
        if (Interlocked.Exchange(ref _savePending, 1) != 0) return;
        _ = Task.Run(async () =>
        {
            await Task.Delay(250);
            Interlocked.Exchange(ref _savePending, 0);
            string[] snapshot;
            lock (_gate)
            {
                snapshot = [.. _entries];
            }
            try
            {
                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var json = JsonSerializer.Serialize(snapshot);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort persistence.
            }
        });
    }
}
