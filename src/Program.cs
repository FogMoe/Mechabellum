namespace Mechabellum.ObserverStats;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        try
        {
            if (args.Length == 0)
            {
                return ParseLatest(json: false, requestedRound: null);
            }

            var command = args[0].ToLowerInvariant();
            return command switch
            {
                "probe" => Probe(),
                "live-probe" => LiveProbe(),
                "live" => Live(args),
                "live-watch" => await LiveWatch(args),
                "latest" => ParseLatest(HasFlag(args, "--json"), ReadRound(args)),
                "parse" => ParseFile(args),
                "dump" => DumpFile(args),
                "watch" => await Watch(args),
                "help" or "--help" or "-h" => Help(),
                _ => UnknownCommand(command)
            };
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"错误: {exception.Message}");
            return 1;
        }
    }

    private static int Probe()
    {
        var locations = GameLocator.Locate();
        Console.WriteLine($"游戏目录: {locations.InstallDirectory ?? "未找到"}");
        Console.WriteLine($"回放目录: {locations.ReplayDirectory ?? "未找到"}");
        Console.WriteLine($"玩家日志: {locations.PlayerLog ?? "未找到"}");
        return locations.ReplayDirectory is null ? 1 : 0;
    }

    private static int LiveProbe()
    {
        using var source = ObserverMemorySource.Attach();
        ConsoleRenderer.WriteJson(source.GetStatus());
        return 0;
    }

    private static int Live(string[] args)
    {
        using var source = ObserverMemorySource.Attach();
        var observed = source.ReadSnapshot();
        if (HasFlag(args, "--json"))
        {
            ConsoleRenderer.WriteJson(observed);
        }
        else
        {
            ConsoleRenderer.WriteRound(observed);
        }

        return 0;
    }

    private static async Task<int> LiveWatch(string[] args)
    {
        using var source = ObserverMemorySource.Attach();
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var interval = ReadInterval(args);
        var json = HasFlag(args, "--json");
        string? previousFingerprint = null;
        string? previousState = null;
        Console.Error.WriteLine($"正在监听只读对局数据，间隔 {interval.TotalMilliseconds:0} ms；仅在部署变化时输出，按 Ctrl+C 退出。");

        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                var observed = source.ReadSnapshot();
                var fingerprint = System.Text.Json.JsonSerializer.Serialize(observed);
                if (!fingerprint.Equals(previousFingerprint, StringComparison.Ordinal))
                {
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

                    previousFingerprint = fingerprint;
                }

                previousState = null;
            }
            catch (ObserverStateException exception)
            {
                if (!exception.Message.Equals(previousState, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine(exception.Message);
                    previousState = exception.Message;
                    previousFingerprint = null;
                }
            }

            await Task.Delay(interval, cancellation.Token);
        }

        return 0;
    }

    private static int ParseLatest(bool json, int? requestedRound)
    {
        var directory = RequireReplayDirectory();
        var latest = Directory
            .EnumerateFiles(directory, "*.grbr", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault()
            ?? throw new FileNotFoundException($"回放目录中没有 .grbr 文件：{directory}");

        return RenderSelected(latest.FullName, json, requestedRound);
    }

    private static int ParseFile(string[] args)
    {
        var path = ReadPositionalPath(args, 1) ?? throw new ArgumentException("用法: parse <file.grbr> [--round N] [--json]");
        return RenderSelected(path, HasFlag(args, "--json"), ReadRound(args));
    }

    private static int DumpFile(string[] args)
    {
        var path = ReadPositionalPath(args, 1) ?? throw new ArgumentException("用法: dump <file.grbr>");
        ConsoleRenderer.WriteJson(new ReplayParser().Parse(path));
        return 0;
    }

    private static async Task<int> Watch(string[] args)
    {
        var directory = ReadPositionalPath(args, 1) ?? RequireReplayDirectory();
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        await new ReplayDirectoryWatcher().WatchAsync(directory, HasFlag(args, "--json"), cancellation.Token);
        return 0;
    }

    private static int RenderSelected(string path, bool json, int? requestedRound)
    {
        var match = new ReplayParser().Parse(path);
        var observed = RoundSelector.Select(match, requestedRound);
        if (json)
        {
            ConsoleRenderer.WriteJson(observed);
        }
        else
        {
            ConsoleRenderer.WriteRound(observed);
        }

        return 0;
    }

    private static string RequireReplayDirectory() =>
        GameLocator.Locate().ReplayDirectory
        ?? throw new DirectoryNotFoundException("未自动找到 Mechabellum 回放目录；请显式传入目录或回放文件。");

    private static int? ReadRound(string[] args)
    {
        var index = Array.FindIndex(args, argument => argument.Equals("--round", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var round))
        {
            throw new ArgumentException("--round 后需要整数回合号。");
        }

        return round;
    }

    private static TimeSpan ReadInterval(string[] args)
    {
        var index = Array.FindIndex(args, argument => argument.Equals("--interval", StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return TimeSpan.FromSeconds(1);
        }

        if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var milliseconds) || milliseconds is < 250 or > 30_000)
        {
            throw new ArgumentException("--interval 后需要 250 到 30000 之间的毫秒数。");
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static bool HasFlag(string[] args, string flag) =>
        args.Any(argument => argument.Equals(flag, StringComparison.OrdinalIgnoreCase));

    private static string? ReadPositionalPath(string[] args, int index)
    {
        if (index >= args.Length || args[index].StartsWith('-'))
        {
            return null;
        }

        return args[index];
    }

    private static int Help()
    {
        Console.WriteLine("""
            Mechabellum Observer Stats

            用法:
              dotnet run -- probe
              dotnet run -- live-probe
              dotnet run -- live [--json]
              dotnet run -- live-watch [--interval 1000] [--json]
              dotnet run -- latest [--round N] [--json]
              dotnet run -- parse <file.grbr> [--round N] [--json]
              dotnet run -- dump <file.grbr>
              dotnet run -- watch [replay-directory] [--json]

            不带参数时等同于 latest。
            --json 输出适合程序消费的 JSON；watch 模式下输出 JSON Lines。
            """);
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"未知命令：{command}");
        Help();
        return 2;
    }
}
