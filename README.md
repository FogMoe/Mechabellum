# Mechabellum Analytics

面向 Mechabellum 观战分析的数据与模型项目，提供对局状态采集、版本化局面数据、逐回合最终胜率预测，以及完整局面表示、行动推荐和 AI 自博弈的统一工程基础。

仓库包含两个平级子项目：

| 子项目 | 技术 | 职责 |
| --- | --- | --- |
| [data-collector](data-collector/README.md) | C# / .NET | 解析 `.grbr`、只读实时进程、输出统一对局 JSON |
| [win-predictor](win-predictor/README.md) | Python | 回放数据集、特征、训练、评估、录像时间线和实时胜率推理 |

## 数据流

```text
Mechabellum 实时进程 / .grbr 回放
                 ↓
       C# 数据采集与统一 JSON
                 ↓
       Python 特征、训练与推理
                 ↓
        模型、指标与胜率时间线
```

`data-collector` 定义并输出局面数据，`win-predictor` 消费该数据完成训练与推理。Web UI、数据服务、模拟器和自博弈环境使用独立的功能子项目。

## 仓库结构

```text
Mechabellum/
├─ README.md                         # 总项目入口
├─ TODO.md                           # 跨项目路线图
├─ data-collector/
│  ├─ Mechabellum.DataCollector.sln
│  ├─ README.md
│  └─ src/Mechabellum.ObserverStats/ # 数据采集实现
├─ win-predictor/
│  ├─ pyproject.toml
│  └─ src/mechabellum_ml/            # 胜率预测子项目
├─ docs/                             # 跨项目架构与协议文档
├─ data/                             # 本地数据，不提交
└─ artifacts/                        # 本地模型与报告，不提交
```

更详细的目录边界见 [项目结构说明](docs/architecture.md)。

## 快速开始

构建并运行采集器：

```powershell
dotnet build data-collector\Mechabellum.DataCollector.sln
dotnet run --project data-collector\src\Mechabellum.ObserverStats -- latest
```

安装模型子项目：

```powershell
python -m venv .venv
.venv\Scripts\python -m pip install -e .\win-predictor
```

持续观战并在每回合战斗开始时预测整局最终胜率：

```powershell
dotnet run -c Release --project data-collector\src\Mechabellum.ObserverStats -- live-watch --json |
  .venv\Scripts\mecha-watch
```

完整采集命令见 [data-collector README](data-collector/README.md)，训练和评估命令见 [win-predictor README](win-predictor/README.md)。

## 功能范围

- `.grbr` 回放解析和 Windows 实时只读局面采集
- `battle_start` 口径的 1v1 最终胜率模型
- 按整场对局隔离的训练、测试与逐回合胜率时间线
- 版本化完整局面、科技、发牌、专家、建筑与空间表示规划
- 行动推荐与 AI 自博弈规划

路线图见 [TODO.md](TODO.md)。内存布局和版本更新规则见 [memory-layout.md](docs/memory-layout.md)。
