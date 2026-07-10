# 实时对局内存布局

实时采集由 `src/mechabellum/collector/memory/` 实现。`windows_process.py` 封装进程与模块枚举、`OpenProcess`、`ReadProcessMemory` 和 `CloseHandle`；`il2cpp_reader.py` 读取指针、值类型、字符串、列表和类型信息；`layouts.py` 保存构建绑定的地址与字段偏移；`live_source.py` 将对象图转换为 `MatchSnapshot v1`。

进程句柄使用以下权限：

- `PROCESS_VM_READ` (`0x0010`)
- `PROCESS_QUERY_INFORMATION` (`0x0400`)

## 支持版本

| 项目 | 值 |
| --- | --- |
| 回放格式 | 2119 |
| 游戏版本 | 1.11.0.2a |
| `GameAssembly.dll` SHA256 | `0FE278BC3A1DD6FF55A51DB2807CCD73ED16A1FE390FE72FFA013F0AEFF495F1` |
| `MatchClient` 类型全局 RVA | `0x3C799F8` |

该布局已在实际 `WatchMatch` 中验证双方名称、回合、单位 ID、编队数量、等级、经验、坐标、朝向、补给、核心值和上一回合胜负。

## 根对象

```text
GameAssembly + 0x3C799F8
  → MatchClient Il2CppClass
  → static_fields (+0xB8)
  → MatchClient.Current (+0x08)
```

`live-probe` 会验证模块哈希、根类型和当前对局对象。大厅中 `MatchClient.Current` 为空；进入可读取的观战或对局后，`live` 从其基类字段读取部署状态。

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

## 构建更新

`LiveMemorySource.attach()` 会计算 `GameAssembly.dll` SHA256，并只选择完全匹配的布局。适配新构建时，在 `layouts.py` 增加独立布局记录并完成以下验证：

1. `mecha live-probe` 返回 `MatchClient` 根类型和目标布局；
2. 大厅中的 `Current` 状态为空；
3. 在观战中核对双方名称、编队数、单位 ID、等级、经济和坐标；
4. 连续回合的阶段与部署变化能够稳定读取；
5. 同一对局回放的最终快照与实时输出字段一致；
6. 内存单元测试和实时集成测试全部通过。
