# 项目结构

仓库按功能组织 Mechabellum 数据与模型。采集器和胜率预测器是平级子项目，各自拥有独立构建入口、README 和测试边界。

```text
Mechabellum/
├─ README.md
├─ TODO.md
├─ data-collector/
│  ├─ Mechabellum.DataCollector.sln
│  ├─ README.md
│  └─ src/Mechabellum.ObserverStats/
│     ├─ Mechabellum.ObserverStats.csproj
│     └─ *.cs
├─ win-predictor/
│  ├─ README.md
│  ├─ pyproject.toml
│  ├─ src/mechabellum_ml/
│  └─ tests/
├─ docs/
├─ data/          # 本地采集数据，不提交
└─ artifacts/     # 本地模型和报告，不提交
```

## README 边界

- 根 `README.md`：整体目标、组件关系、统一数据流、快速开始和功能范围。
- `data-collector` README：采集字段、回放/实时命令、内存版本和数据口径。
- `win-predictor` README：模型口径、安装、训练、评估、推理和测试。

## 目录职责

| 目录 | 职责 |
| --- | --- |
| 根目录 | 总览、跨项目路线图和仓库配置 |
| `data-collector` | 基于 .NET 的回放和实时数据采集 |
| `win-predictor` | 基于 Python 的胜率特征、训练、评估和推理 |
| `docs` | 跨组件架构、数据契约和版本适配 |
| `data` | JSONL、数据集和索引 |
| `artifacts` | 模型、评估报告和导出结果 |

## 依赖方向

```text
Mechabellum / .grbr
        ↓
C# ObserverStats（采集与统一 JSON）
        ↓
Python mechabellum-ml（特征、训练、评估、推理）
        ↓
artifacts（模型与报告）
```

`data-collector` 输出版本化局面 schema，`win-predictor` 依赖该 schema 构造特征。共享数据契约存放在 `docs`。

## 扩展规则

- Web UI、API 服务、模拟器和自博弈环境新增为平级子项目。
- 新的内存或回放数据源归入 C# 采集器。
- 新的特征、模型和训练命令归入 Python 包。
- 一次性逆向输出保留在系统临时目录，不进入仓库。
- 大型录像、数据集和训练模型只进入 `data` / `artifacts`。
