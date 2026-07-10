# Mechabellum Analytics

Mechabellum 观战分析研究项目。它把 `.grbr` 回放解析、Windows 实时只读采集、版本化局面数据、数据集生成、模型训练和逐回合最终胜率推理整合在一个 Python 包中。

项目以 `MatchSnapshot v1` 作为正式数据边界：

```text
.grbr 回放 ─┐
             ├─ collector ─→ MatchSnapshot v1 ─→ datasets ─→ training ─→ model
实时进程 ────┘                         └──────────────────────→ inference
```

运行时模块直接传递强类型 Python 对象；保存和交换数据时使用通过 JSON Schema 校验的版本化 JSON 或 JSONL。

## 功能

- 解析 `.grbr` 回放并输出每回合 `battle_start` 快照
- 通过 Windows `ReadProcessMemory` 读取观战中的双方部署
- 记录玩家、回合、经济、单位、等级、经验、坐标、装备与科技
- 将回放目录导出为 `MatchSnapshot v1` JSONL 数据集
- 按整场对局隔离训练集和测试集，训练双方对称的胜率基线
- 对单个局面、整场时间线或实时战斗开始局面计算最终胜率

## 安装

需要 Python 3.11 或更高版本。实时内存采集仅支持 Windows；回放处理和模型管线可在其他 Python 平台运行。

```powershell
python -m venv .venv
.venv\Scripts\python -m pip install --upgrade pip
.venv\Scripts\python -m pip install -e .
```

基础安装提供回放和实时采集。训练与推理安装 `ml` 组件，开发环境同时安装 `dev` 组件：

```powershell
.venv\Scripts\python -m pip install -e ".[ml]"
.venv\Scripts\python -m pip install -e ".[ml,dev]"
```

## 使用

定位游戏、回放目录和日志：

```powershell
.venv\Scripts\mecha probe
```

读取最新回放，或导出可训练数据集：

```powershell
.venv\Scripts\mecha latest --json
.venv\Scripts\mecha export-dataset --output data\match-snapshots.v1.jsonl
```

检查内存布局、读取一次实时局面、持续观察变化：

```powershell
.venv\Scripts\mecha live-probe
.venv\Scripts\mecha live --json
.venv\Scripts\mecha live-watch --json --interval 500
```

训练模型并查看一场对局的逐回合胜率：

```powershell
.venv\Scripts\mecha train --dataset data\match-snapshots.v1.jsonl
.venv\Scripts\mecha timeline data\match-snapshots.v1.jsonl
```

在观战中于每回合战斗开始时输出双方最终胜率：

```powershell
.venv\Scripts\mecha live-predict --model artifacts\win_model.joblib
```

所有命令及参数可通过 `.venv\Scripts\mecha --help` 和对应子命令的 `--help` 查看。

## 项目结构

```text
Mechabellum/
├─ pyproject.toml
├─ contracts/match-snapshot/v1/     # JSON Schema 与示例
├─ src/mechabellum/
│  ├─ collector/                     # 回放与实时内存数据源
│  ├─ contracts/                     # 强类型 MatchSnapshot
│  ├─ datasets/                      # JSONL 读写与回放导出
│  ├─ features/                      # 双方对称的基线特征
│  ├─ models/                        # 模型产物格式
│  ├─ training/                      # 训练与评估
│  ├─ inference/                     # 单局、时间线与实时推理
│  └─ cli/                           # 统一 mecha 命令
├─ tests/
├─ docs/
├─ data/                             # 本地数据，不提交
└─ artifacts/                        # 本地模型，不提交
```

模块边界和依赖方向见 [项目架构](docs/architecture.md)，实时版本与偏移见 [内存布局](docs/memory-layout.md)，后续功能见 [TODO](TODO.md)。

## 数据契约

规范文件位于 [contracts/match-snapshot/v1/schema.json](contracts/match-snapshot/v1/schema.json)，可读示例位于 [battle-start.example.json](contracts/match-snapshot/v1/battle-start.example.json)。采集、数据集、训练与推理入口均使用同一组 Pydantic 模型，并在持久化边界执行 JSON Schema 校验。

## 验证

```powershell
.venv\Scripts\ruff check src tests
.venv\Scripts\pytest -q
```

实时采集依赖已适配的游戏构建和 `GameAssembly.dll` 哈希。游戏更新后应先运行 `mecha live-probe`，并按内存布局文档完成版本校验。
