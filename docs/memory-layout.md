# 实时对局内存布局

实时读取代码位于 `data-collector/src/Mechabellum.ObserverStats/ObserverMemorySource.cs`，所有访问均通过 Windows `ReadProcessMemory` 完成。句柄只申请：

- `PROCESS_VM_READ` (`0x0010`)
- `PROCESS_QUERY_INFORMATION` (`0x0400`)

不申请写入、创建线程、挂起进程或调试权限。

## 已适配版本

| 项目 | 值 |
| --- | --- |
| 回放格式 | 2119 |
| 游戏版本 | 1.11.0.2a |
| `GameAssembly.dll` SHA256 | `0FE278BC3A1DD6FF55A51DB2807CCD73ED16A1FE390FE72FFA013F0AEFF495F1` |
| `MatchClient` 类型全局 RVA | `0x3C799F8` |

2026-07-11 已在实际 `WatchMatch` 中完成端到端验证：双方名称、回合、单位 ID、编队数量、等级、经验、坐标、朝向、补给、核心值和上一回合胜负均能随观战进度更新，并与画面一致。

入口链：

```text
GameAssembly + 0x3C799F8
  -> MatchClient Il2CppClass
  -> static_fields (+0xB8)
  -> MatchClient.Current (+0x08)
```

`live` 读取 `MatchClient.Current` 的 IL2CPP 类型名用于诊断，并在基类字段布局有效时读取部署。

## 主要字段

| 对象 | 字段 | 偏移 |
| --- | --- | ---: |
| `Match` | `battleInfo` | `0x30` |
| `Match` | `RoundCount` | `0x64` |
| `Match` | `playerManager` | `0x70` |
| `PlayerManager` | `playerControllers` | `0x18` |
| `PlayerController` | `unitManager` | `0x20` |
| `PlayerController` | `player` | `0x128` |
| `PlayerController` | `teamController` | `0x130` |
| `PlayerController` | `teamSystem` | `0x148` |
| `Player` | `playerAgent` | `0xE0` |
| `Player` | `State` | `0x108` |
| `PlayerAgent` | `technologyManagers` | `0x100` |
| `TeamSystem` | `teamControllers` | `0x28` |
| `UnitManager` | `units` | `0x38` |
| `UnitTechnologyManager` | `technologies` | `0x10` |
| `UnitTechnologyManager` | `UnitID` | `0x18` |
| `ActivableItem/Technology` | `isActive` | `0x18` |
| `CardElement` | `unitData` | `0x10` |
| `CardElement` | `equipment` | `0x28` |
| `CardElement` | `mapElement` | `0x38` |
| `CardElement` | `mechTeam` | `0x48` |
| `CardElement` | `sellSupply` | `0x60` |
| `GRObject/CardData` | `id` | `0x10` |
| `MapElement` | `bound` (`RectInt`) | `0x18` |
| `MapElement` | `isRotate` | `0x2C` |
| `MechTeam` | `level` | `0x2C` |
| `MechTeam` | `expFloat` (`FPoint Q32.32`) | `0x38` |
| `MechTeam` | `roundCount` | `0x50` |

## 更新规则

游戏补丁改变 SHA256 后，使用对应版本的 IL2CPP 元数据确认类型和字段，并同步更新布局与哈希。至少验证：

1. `live-probe` 的根类型是 `MatchClient`；
2. 大厅中 `Current` 为空；
3. 至少分别在观战和目标对局类型中验证双方名称、编队数、单位 ID、等级和坐标与画面一致；
4. 同一局 `.grbr` 的最终快照与实时最后一次输出一致。
