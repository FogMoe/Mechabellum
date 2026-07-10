# Mechabellum.ObserverStats

Mechabellum Analytics 的数据采集子项目。它从游戏生成的 `.grbr` 回放中提取 `BattleRecord`，并通过 Windows `ReadProcessMemory` 只读采集运行中对局的双方状态，输出供统计或模型消费的 JSON。

项目不修改游戏、不注入代码，也不发送网络请求。

## 输出字段

- 双方玩家 ID、名称、队伍与实时阶段
- 对局 ID、地图、模式、回放版本和回合
- 每个部署编队的单位 ID/名称、等级、经验、坐标和朝向
- 装备 ID、出售价值、编队轮次和耐久字段
- 剩余补给、核心值和上一轮胜负
- 每种单位已激活的科技 ID

`NewUnitData` 一条代表一队部署编队。例如一队爬虫包含多个模型，数据口径统计为一队。

## 要求

- Windows 10/11
- .NET 8 SDK 或更高版本
- 已安装 Mechabellum

以下命令均从仓库根目录执行。

## 构建与定位

```powershell
dotnet build data-collector\Mechabellum.DataCollector.sln
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- probe
```

## 回放读取

```powershell
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- latest
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- latest --round 6 --json
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- parse "C:\path\to\match.grbr"
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- dump "C:\path\to\match.grbr"
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- watch "C:\path\to\ProjectDatas\Replay" --json
```

## 实时读取

```powershell
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- live-probe
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- live --json
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- live-watch --interval 1000 --json
```

`live-probe` 报告布局版本和对局类型；`live` 读取一次；`live-watch` 在局面变化时输出 JSON Lines。实时入口申请 `PROCESS_VM_READ` 和查询权限，并对已适配的 `GameAssembly.dll` 做精确 SHA256 校验。

## 数据口径

游戏保存的 `Level` 从 0 开始，输出同时保留 `rawLevel`，并提供 `displayLevel = rawLevel + 1`。未知单位输出原始 ID 和未知标记。

回放中的 `round >= 1` 的 `playerData` 对应双方锁定后进入该回合战斗的部署；`round 0` 是开局阵容选择阶段。实时数据通过玩家 `State` 区分 `Deploying`、`DeployOver`、`Fighting` 和 `FightOver`。

## 版本适配

实时读取适配回放格式 2119 / 游戏 1.11.0.2a。版本适配同时校验 IL2CPP 字段、偏移和 `GameAssembly.dll` SHA256。

详细偏移、入口链和验证清单见 [内存布局文档](../docs/memory-layout.md)。整体项目架构见 [根 README](../README.md)。
