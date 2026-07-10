namespace Mechabellum.ObserverStats;

public sealed class ReplayDirectoryWatcher
{
    private readonly ReplayParser _parser = new();
    private readonly Dictionary<string, FileState> _states = new(StringComparer.OrdinalIgnoreCase);

    public async Task WatchAsync(string directory, bool json, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"找不到回放目录：{directory}");
        }

        Console.Error.WriteLine($"正在监听：{Path.GetFullPath(directory)}");
        Console.Error.WriteLine("检测到完整的新回放或更新后会输出最后一个双方共有回合；按 Ctrl+C 退出。");

        foreach (var path in Directory.EnumerateFiles(directory, "*.grbr", SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(path);
            _states[path] = new FileState(info.Length, info.LastWriteTimeUtc, StablePolls: 2, Processed: true);
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var path in Directory.EnumerateFiles(directory, "*.grbr", SearchOption.TopDirectoryOnly))
            {
                ProcessCandidate(path, json);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }

    private void ProcessCandidate(string path, bool json)
    {
        var info = new FileInfo(path);
        var current = new FileState(info.Length, info.LastWriteTimeUtc, StablePolls: 0, Processed: false);

        if (!_states.TryGetValue(path, out var previous))
        {
            _states[path] = current;
            return;
        }

        if (previous.Length != current.Length || previous.LastWriteTimeUtc != current.LastWriteTimeUtc)
        {
            _states[path] = current;
            return;
        }

        if (previous.Processed)
        {
            return;
        }

        var stablePolls = previous.StablePolls + 1;
        if (stablePolls < 2)
        {
            _states[path] = previous with { StablePolls = stablePolls };
            return;
        }

        try
        {
            var observed = RoundSelector.Select(_parser.Parse(path));
            if (json)
            {
                ConsoleRenderer.WriteJson(observed, indented: false);
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"=== {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} ===");
                ConsoleRenderer.WriteRound(observed);
            }

            _states[path] = previous with { StablePolls = stablePolls, Processed = true };
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _states[path] = previous with { StablePolls = stablePolls };
        }
    }

    private sealed record FileState(long Length, DateTime LastWriteTimeUtc, int StablePolls, bool Processed);
}
