# Mechabellum Observer Stats

一个只读的 Mechabellum 对局统计 CLI。它既能从游戏生成的 `.grbr` 回放中提取内嵌的 `BattleRecord` XML，也能从当前对局读取双方部署。项目不修改游戏、不注入代码，也不发送网络请求。

当前已提取的数据：

- 双方玩家 ID、名称、队伍
- 对局 ID、地图、模式、回放版本和回合
- 每个部署编队的单位 ID/名称、等级、经验、坐标、朝向、装备和出售价值
- 每种单位的编队数量与等级分布
- 每名玩家已激活的单位科技 ID

> 回放里的 `NewUnitData` 一条代表一队/一个部署编队，不一定代表一个模型。例如一队爬虫包含多个爬虫模型；本项目将其统计为“一队”。

## 要求

- Windows 10/11
- .NET 8 SDK 或更高版本
- 已安装 Mechabellum；游戏正在运行时可自动定位安装目录，否则会尝试读取默认 Steam 库

## 快速开始

```powershell
dotnet build
dotnet run -- probe
dotnet run -- latest
```

读取正在进行的对局：

```powershell
dotnet run -- live-probe
dotnet run -- live
dotnet run -- live --json
dotnet run -- live-watch --interval 1000
```

`live-probe` 只报告版本布局、当前对局类型以及部署是否可读。`live` 读取一次，`live-watch` 持续监听且只在部署发生变化时输出。实时读取不再限定对局类型；仍只申请 `PROCESS_VM_READ`/查询权限，并只支持已校验 SHA256 的游戏二进制。

读取指定回放或指定回合：

```powershell
dotnet run -- parse "C:\path\to\match.grbr"
dotnet run -- parse "C:\path\to\match.grbr" --round 6
dotnet run -- parse "C:\path\to\match.grbr" --json
```

输出整个回放的所有已记录回合：

```powershell
dotnet run -- dump "C:\path\to\match.grbr" > match.json
```

监听回放目录；检测到写入完成的新回放后自动输出最后一个双方共有回合：

```powershell
dotnet run -- watch
dotnet run -- watch "C:\path\to\ProjectDatas\Replay" --json
```

## 数据口径

游戏保存的 `Level` 从 0 开始，本项目同时保留 `rawLevel`，并将界面显示等级输出为 `displayLevel = rawLevel + 1`。坐标和朝向保持回放原始值。单位名称表对应当前已验证的 ID；遇到新版本的未知 ID 时仍会保留完整部署数据，并显示为 `Unknown / 未知单位 (ID)`。

## 实时读取说明

`.grbr` 通常在一局结束或回放保存时写入，因此目录监听不等于逐秒实时。当前 `live` 数据源针对回放格式 2119 / 游戏 1.11.0.2a 的 `GameAssembly.dll` 做了精确版本锁定，读取当前玩家对象和部署编队。

游戏更新后，如果二进制 SHA256 改变，`live` 会主动停止并要求更新布局；回放解析通常仍可继续工作。实时模式下 `formationIndex` 是当前 IL2CPP 编队列表中的序号，适合单次统计，但不保证跨回合稳定；稳定的游戏编队索引以 `.grbr` 解析结果为准。

项目不包含写内存、注入、自动操作或网络拦截功能。

布局、偏移来源和版本更新检查清单见 [`docs/MEMORY_LAYOUT.md`](docs/MEMORY_LAYOUT.md)。
