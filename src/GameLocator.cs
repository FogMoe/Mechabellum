using System.Diagnostics;
using Microsoft.Win32;

namespace Mechabellum.ObserverStats;

public sealed record GameLocations(string? InstallDirectory, string? ReplayDirectory, string? PlayerLog);

public static class GameLocator
{
    public static GameLocations Locate()
    {
        var install = LocateFromRunningProcess() ?? LocateFromSteamRegistry();
        var replay = install is null ? null : Path.Combine(install, "ProjectDatas", "Replay");
        var playerLog = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "LocalLow", "GameRiver", "Mechabellum", "Player.log");

        return new GameLocations(
            install,
            replay is not null && Directory.Exists(replay) ? replay : null,
            File.Exists(playerLog) ? playerLog : null);
    }

    private static string? LocateFromRunningProcess()
    {
        try
        {
            using var process = Process.GetProcessesByName("Mechabellum").FirstOrDefault();
            var executable = process?.MainModule?.FileName;
            return executable is null ? null : Path.GetDirectoryName(executable);
        }
        catch
        {
            return null;
        }
    }

    private static string? LocateFromSteamRegistry()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
            if (string.IsNullOrWhiteSpace(steamPath))
            {
                return null;
            }

            var install = Path.Combine(steamPath, "steamapps", "common", "Mechabellum");
            return Directory.Exists(install) ? install : null;
        }
        catch
        {
            return null;
        }
    }
}

