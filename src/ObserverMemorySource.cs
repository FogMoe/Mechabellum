using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Mechabellum.ObserverStats;

public sealed class ObserverMemorySource : IDisposable
{
    // Mechabellum replay format 2119 / GameAssembly.dll 1.11.0.2a.
    // The exact binary hash is intentional: a patch must never be read using stale offsets.
    private static readonly ObserverMemoryLayout SupportedLayout = new(
        ReplayVersion: 2119,
        GameAssemblySha256: "0FE278BC3A1DD6FF55A51DB2807CCD73ED16A1FE390FE72FFA013F0AEFF495F1",
        MatchClientClassGlobalRva: 0x3C799F8);

    private readonly Process _process;
    private readonly ProcessMemory _memory;
    private readonly nuint _gameAssemblyBase;
    private readonly ObserverMemoryLayout _layout;

    private ObserverMemorySource(Process process, ProcessMemory memory, nuint gameAssemblyBase, ObserverMemoryLayout layout)
    {
        _process = process;
        _memory = memory;
        _gameAssemblyBase = gameAssemblyBase;
        _layout = layout;
    }

    public static ObserverMemorySource Attach()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("实时只读数据源目前只支持 Windows。");
        }

        var process = Process.GetProcessesByName("Mechabellum").FirstOrDefault()
            ?? throw new InvalidOperationException("Mechabellum 尚未运行。");

        try
        {
            var module = process.Modules
                .Cast<ProcessModule>()
                .FirstOrDefault(item => item.ModuleName.Equals("GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("运行中的进程没有加载 GameAssembly.dll。");

            var modulePath = module.FileName;
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(modulePath)));
            if (!hash.Equals(SupportedLayout.GameAssemblySha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new NotSupportedException(
                    $"当前 GameAssembly.dll 尚未适配（SHA256={hash}）。" +
                    "为避免旧偏移读取错误数据，本项目会拒绝盲读；请为当前游戏版本生成新的 layout。");
            }

            var memory = new ProcessMemory(process.Id);
            return new ObserverMemorySource(process, memory, (nuint)module.BaseAddress, SupportedLayout);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    public ObserverMemoryStatus GetStatus()
    {
        var matchClientClass = _memory.ReadPointer(_gameAssemblyBase + _layout.MatchClientClassGlobalRva);
        if (matchClientClass == 0)
        {
            return new ObserverMemoryStatus(_layout.ReplayVersion, "MatchClient", null, false);
        }

        var rootClassName = _memory.ReadClassName(matchClientClass);
        var staticFields = _memory.ReadPointer(matchClientClass + 0xB8);
        var currentMatch = staticFields == 0 ? 0 : _memory.ReadPointer(staticFields + 0x08);
        var currentType = currentMatch == 0 ? null : _memory.ReadObjectClassName(currentMatch);
        return new ObserverMemoryStatus(
            _layout.ReplayVersion,
            rootClassName,
            currentType,
            currentMatch != 0);
    }

    public ObservedRound ReadSnapshot()
    {
        var match = GetCurrentMatch(out var matchType);
        var battleInfo = _memory.ReadPointer(match + 0x30);
        var playerManager = _memory.ReadPointer(match + 0x70);
        if (playerManager == 0)
        {
            throw new ObserverStateException("当前对局尚未完成玩家数据初始化，请稍后重试。");
        }

        var controllersList = _memory.ReadPointer(playerManager + 0x18);
        var controllers = _memory.ReadListPointers(controllersList, maximumCount: 8);
        if (controllers.Count < 2)
        {
            throw new ObserverStateException($"当前只找到 {controllers.Count} 名玩家，对局快照可能仍在加载。");
        }

        var players = controllers
            .Where(pointer => pointer != 0)
            .Select(ReadPlayer)
            .ToArray();

        return new ObservedRound(
            SourceFile: $"memory://Mechabellum/{_process.Id}",
            Version: _layout.ReplayVersion,
            BattleId: battleInfo == 0 ? null : _memory.ReadManagedString(_memory.ReadPointer(battleInfo + 0x18)),
            MapId: battleInfo == 0 ? null : _memory.ReadInt32(battleInfo + 0x34),
            GameMode: battleInfo == 0 ? null : GameEnums.GameMode(_memory.ReadInt32(battleInfo + 0x58)),
            MatchMode: battleInfo == 0 ? matchType : $"{GameEnums.MatchMode(_memory.ReadInt32(battleInfo + 0x5C))} ({matchType})",
            Round: _memory.ReadInt32(match + 0x64),
            Players: players);
    }

    private nuint GetCurrentMatch(out string matchType)
    {
        var matchClientClass = _memory.ReadPointer(_gameAssemblyBase + _layout.MatchClientClassGlobalRva);
        if (matchClientClass == 0 || !_memory.ReadClassName(matchClientClass).Equals("MatchClient", StringComparison.Ordinal))
        {
            throw new InvalidDataException("版本布局校验失败：MatchClient 类型根指针无效。");
        }

        var staticFields = _memory.ReadPointer(matchClientClass + 0xB8);
        var currentMatch = staticFields == 0 ? 0 : _memory.ReadPointer(staticFields + 0x08);
        if (currentMatch == 0)
        {
            throw new ObserverStateException("当前没有正在运行的对局。");
        }

        matchType = _memory.ReadObjectClassName(currentMatch);
        if (string.IsNullOrWhiteSpace(matchType))
        {
            throw new InvalidDataException("无法识别当前对局的 IL2CPP 类型。");
        }

        return currentMatch;
    }

    private ObservedPlayer ReadPlayer(nuint controller)
    {
        var player = _memory.ReadPointer(controller + 0x128);
        if (player == 0)
        {
            throw new InvalidDataException("PlayerController 中的 Player 指针为空。");
        }

        var playerId = _memory.ReadUInt64(player + 0x18);
        var riskInfo = _memory.ReadPointer(player + 0x28);
        var name = riskInfo == 0
            ? $"Player {playerId}"
            : _memory.ReadManagedString(_memory.ReadPointer(riskInfo + 0x10)) ?? $"Player {playerId}";

        var unitManager = _memory.ReadPointer(controller + 0x20);
        var unitsList = unitManager == 0 ? 0 : _memory.ReadPointer(unitManager + 0x38);
        var unitPointers = unitsList == 0
            ? Array.Empty<nuint>()
            : _memory.ReadListPointers(unitsList, maximumCount: 256);

        var units = unitPointers
            .Select((pointer, index) => ReadUnit(pointer, index))
            .Where(unit => unit is not null)
            .Cast<UnitDeployment>()
            .ToArray();

        var activeTechnologies = ReadActiveTechnologies(player);
        var team = ReadTeamIndex(controller);

        return new ObservedPlayer(
            PlayerId: playerId,
            Name: name,
            Team: team,
            Supply: _memory.ReadInt32(player + 0xEC),
            ReactorCore: _memory.ReadInt32(player + 0x134),
            PreviousFightResult: GameEnums.FightResult(_memory.ReadInt32(player + 0x158)),
            Units: units,
            ActiveTechnologies: activeTechnologies);
    }

    private int? ReadTeamIndex(nuint controller)
    {
        var teamController = _memory.ReadPointer(controller + 0x130);
        var teamSystem = _memory.ReadPointer(controller + 0x148);
        var teamControllersList = teamSystem == 0 ? 0 : _memory.ReadPointer(teamSystem + 0x28);
        if (teamController == 0 || teamControllersList == 0)
        {
            return null;
        }

        var teamControllers = _memory.ReadListPointers(teamControllersList, maximumCount: 8);
        for (var index = 0; index < teamControllers.Count; index++)
        {
            if (teamControllers[index] == teamController)
            {
                return index;
            }
        }

        return null;
    }

    private IReadOnlyList<UnitTechnologySet> ReadActiveTechnologies(nuint player)
    {
        var playerAgent = _memory.ReadPointer(player + 0xE0);
        var managersList = playerAgent == 0 ? 0 : _memory.ReadPointer(playerAgent + 0x100);
        if (managersList == 0)
        {
            return [];
        }

        var result = new List<UnitTechnologySet>();
        foreach (var manager in _memory.ReadListPointers(managersList, maximumCount: 256))
        {
            if (manager == 0)
            {
                continue;
            }

            var unitId = _memory.ReadInt32(manager + 0x18);
            var technologiesList = _memory.ReadPointer(manager + 0x10);
            if (technologiesList == 0)
            {
                continue;
            }

            var activeIds = new List<int>();
            foreach (var technology in _memory.ReadListPointers(technologiesList, maximumCount: 32))
            {
                if (technology == 0 || _memory.ReadByte(technology + 0x18) == 0)
                {
                    continue;
                }

                var technologyData = _memory.ReadPointer(technology + 0x10);
                if (technologyData != 0)
                {
                    var technologyId = _memory.ReadInt32(technologyData + 0x10);
                    if (technologyId > 0)
                    {
                        activeIds.Add(technologyId);
                    }
                }
            }

            if (activeIds.Count > 0)
            {
                result.Add(new UnitTechnologySet(unitId, UnitCatalog.GetName(unitId), activeIds));
            }
        }

        return result;
    }

    private UnitDeployment? ReadUnit(nuint cardElement, int listIndex)
    {
        if (cardElement == 0)
        {
            return null;
        }

        var cardData = _memory.ReadPointer(cardElement + 0x10);
        var mapElement = _memory.ReadPointer(cardElement + 0x38);
        var mechTeam = _memory.ReadPointer(cardElement + 0x48);
        if (cardData == 0 || mapElement == 0 || mechTeam == 0)
        {
            return null;
        }

        var unitId = _memory.ReadInt32(cardData + 0x10);
        if (unitId <= 0 || unitId > 100_000)
        {
            return null;
        }

        var boundX = _memory.ReadInt32(mapElement + 0x18);
        var boundY = _memory.ReadInt32(mapElement + 0x1C);
        var width = _memory.ReadInt32(mapElement + 0x20);
        var height = _memory.ReadInt32(mapElement + 0x24);
        var rawLevel = _memory.ReadInt32(mechTeam + 0x2C);
        var expRaw = _memory.ReadInt64(mechTeam + 0x38);
        var experience = (int)Math.Clamp(expRaw / 4_294_967_296d, 0, int.MaxValue);

        var equipment = _memory.ReadPointer(cardElement + 0x28);
        var equipmentData = equipment == 0 ? 0 : _memory.ReadPointer(equipment + 0x20);

        return new UnitDeployment(
            UnitId: unitId,
            UnitName: UnitCatalog.GetName(unitId),
            FormationIndex: listIndex,
            RawLevel: rawLevel,
            DisplayLevel: rawLevel + 1,
            Experience: experience,
            X: boundX + width / 2f,
            Y: boundY + height / 2f,
            IsRotated: _memory.ReadByte(mapElement + 0x2C) != 0,
            EquipmentId: equipmentData == 0 ? 0 : _memory.ReadInt32(equipmentData + 0x10),
            SellSupply: _memory.ReadInt32(cardElement + 0x60),
            RoundCount: _memory.ReadInt32(mechTeam + 0x50),
            Durability: equipment == 0 ? 0 : _memory.ReadInt32(equipment + 0x30));
    }

    public void Dispose()
    {
        _memory.Dispose();
        _process.Dispose();
    }

    private sealed record ObserverMemoryLayout(int ReplayVersion, string GameAssemblySha256, nuint MatchClientClassGlobalRva);
}

public sealed record ObserverMemoryStatus(
    int LayoutReplayVersion,
    string RootClass,
    string? CurrentMatchType,
    bool DeploymentReadAvailable);

public sealed class ObserverStateException(string message) : InvalidOperationException(message);

internal sealed class ProcessMemory : IDisposable
{
    private const uint ProcessVmRead = 0x0010;
    private const uint ProcessQueryInformation = 0x0400;
    private readonly nint _handle;

    public ProcessMemory(int processId)
    {
        _handle = NativeMethods.OpenProcess(ProcessVmRead | ProcessQueryInformation, false, processId);
        if (_handle == 0)
        {
            throw new Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error(), "无法以只读权限打开 Mechabellum 进程。");
        }
    }

    public byte ReadByte(nuint address) => ReadBytes(address, 1)[0];
    public int ReadInt32(nuint address) => BitConverter.ToInt32(ReadBytes(address, sizeof(int)));
    public long ReadInt64(nuint address) => BitConverter.ToInt64(ReadBytes(address, sizeof(long)));
    public ulong ReadUInt64(nuint address) => BitConverter.ToUInt64(ReadBytes(address, sizeof(ulong)));
    public nuint ReadPointer(nuint address) => (nuint)ReadUInt64(address);

    public IReadOnlyList<nuint> ReadListPointers(nuint list, int maximumCount)
    {
        if (list == 0)
        {
            return [];
        }

        var items = ReadPointer(list + 0x10);
        var size = ReadInt32(list + 0x18);
        if (size < 0 || size > maximumCount)
        {
            throw new InvalidDataException($"IL2CPP List 大小异常：{size}（上限 {maximumCount}）。");
        }

        if (size == 0)
        {
            return [];
        }

        if (items == 0)
        {
            throw new InvalidDataException("IL2CPP List 的 items 指针为空。");
        }

        var capacity = ReadUInt64(items + 0x18);
        if (capacity < (ulong)size || capacity > (ulong)maximumCount * 16)
        {
            throw new InvalidDataException($"IL2CPP Array 容量异常：{capacity}，List 大小为 {size}。");
        }

        var bytes = ReadBytes(items + 0x20, checked(size * sizeof(ulong)));
        var result = new nuint[size];
        for (var index = 0; index < size; index++)
        {
            result[index] = (nuint)BitConverter.ToUInt64(bytes, index * sizeof(ulong));
        }

        return result;
    }

    public string ReadObjectClassName(nuint objectAddress)
    {
        var classAddress = ReadPointer(objectAddress);
        return classAddress == 0 ? string.Empty : ReadClassName(classAddress);
    }

    public string ReadClassName(nuint classAddress)
    {
        var nameAddress = ReadPointer(classAddress + 0x10);
        return ReadNullTerminatedUtf8(nameAddress, 256);
    }

    public string? ReadManagedString(nuint address)
    {
        if (address == 0)
        {
            return null;
        }

        var length = ReadInt32(address + 0x10);
        if (length < 0 || length > 16_384)
        {
            throw new InvalidDataException($"IL2CPP String 长度异常：{length}。");
        }

        return length == 0 ? string.Empty : Encoding.Unicode.GetString(ReadBytes(address + 0x14, length * sizeof(char)));
    }

    private string ReadNullTerminatedUtf8(nuint address, int maximumBytes)
    {
        if (address == 0)
        {
            return string.Empty;
        }

        var bytes = ReadBytes(address, maximumBytes);
        var length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    private byte[] ReadBytes(nuint address, int count)
    {
        if (address == 0 || count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        var buffer = new byte[count];
        if (!NativeMethods.ReadProcessMemory(_handle, address, buffer, (nuint)count, out var read) || read != (nuint)count)
        {
            throw new Win32Exception(
                System.Runtime.InteropServices.Marshal.GetLastWin32Error(),
                $"只读内存读取失败：0x{address:X}，请求 {count} 字节，实际 {read} 字节。");
        }

        return buffer;
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            NativeMethods.CloseHandle(_handle);
        }
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        internal static extern nint OpenProcess(uint desiredAccess, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool inheritHandle, int processId);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool ReadProcessMemory(nint process, nuint baseAddress, byte[] buffer, nuint size, out nuint bytesRead);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        internal static extern bool CloseHandle(nint handle);
    }
}
