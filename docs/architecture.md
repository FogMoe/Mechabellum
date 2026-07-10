# 项目架构

仓库包含一个可安装的 Python 包和一个统一命令入口。源码按数据采集、数据契约、数据集、特征、模型、训练、推理和命令行分层。

```text
Mechabellum/
├─ README.md
├─ TODO.md
├─ pyproject.toml
├─ contracts/
│  └─ match-snapshot/v1/
│     ├─ schema.json
│     └─ battle-start.example.json
├─ src/mechabellum/
│  ├─ collector/
│  │  ├─ memory/
│  │  │  ├─ windows_process.py
│  │  │  ├─ il2cpp_reader.py
│  │  │  ├─ layouts.py
│  │  │  └─ live_source.py
│  │  ├─ replay/
│  │  │  ├─ extractor.py
│  │  │  └─ parser.py
│  │  ├─ locator.py
│  │  └─ unit_catalog.py
│  ├─ contracts/
│  ├─ datasets/
│  ├─ features/
│  ├─ models/
│  ├─ training/
│  ├─ inference/
│  └─ cli/
├─ tests/
├─ docs/
├─ data/
└─ artifacts/
```

## 数据流

```text
                    ┌─ replay extractor/parser ─┐
Mechabellum 数据 ───┤                            ├─→ MatchSnapshot v1
                    └─ Windows memory source ───┘             │
                                                               ├─→ JSON / JSONL
                                                               ├─→ feature extraction
                                                               ├─→ model training
                                                               └─→ inference
```

`MatchSnapshot v1` 是采集层的唯一输出类型，也是后续模块的唯一局面输入类型。类型模型负责运行时字段约束，JSON Schema 负责持久化和跨进程数据校验。

## 模块职责

| 模块 | 职责 |
| --- | --- |
| `collector.memory` | 进程定位、Windows API、IL2CPP 原语读取、版本布局与实时快照 |
| `collector.replay` | 安全提取回放 XML、解析回合状态与最终结果 |
| `contracts` | `MatchSnapshot` 强类型模型、序列化和 schema 校验 |
| `datasets` | JSONL 读写、回放目录批量导出和失败统计 |
| `features` | 从完整快照生成确定顺序的双方对称特征 |
| `models` | 模型、特征签名和训练元数据的版本化产物 |
| `training` | 按 `battleId` 分组切分、训练、选择超参数和评估 |
| `inference` | 单快照预测、逐回合时间线和实时预测 |
| `cli` | `mecha` 命令、参数、输出编码和错误边界 |

## 依赖方向

允许的主依赖方向为：

```text
collector ─→ contracts
datasets  ─→ collector.replay + contracts
features  ─→ contracts
models    ─→ features
training  ─→ datasets + features + models
inference ─→ contracts + features + models
cli       ─→ 以上公开入口
```

采集层不依赖机器学习组件。`numpy`、`scikit-learn` 和 `joblib` 位于 `ml` 可选依赖中，基础安装可独立完成回放与实时采集。

## 契约规则

- 新持久化字段先进入对应版本的 JSON Schema，再进入 Pydantic 模型和两个采集源。
- 同一 schema 版本保持字段语义稳定；不兼容修改发布新的版本目录。
- 未知游戏 ID 保留原始数值；未知或不可用字段使用显式空值。
- JSONL 每行包含一个完整 `MatchSnapshot`，同一场对局由 `match.battleId` 关联。
- 模型产物保存特征名称和契约信息，加载时检查特征签名。

## 本地目录

`data/` 保存回放派生的 JSONL 和实验数据，`artifacts/` 保存模型及报告。两者均不进入版本控制。一次性逆向分析输出放入系统临时目录。

## 测试边界

- 契约测试：schema 校验、别名和序列化往返
- 回放测试：嵌入 XML 提取、解析、回合选择与数据集导出
- 内存测试：IL2CPP 字符串/列表读取、进程模块枚举和实时根对象
- 特征测试：玩家交换后的概率对称性
- 管线测试：合成数据训练、模型保存、加载和预测
