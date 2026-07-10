# mechabellum-ml

Mechabellum Analytics 的 Python 机器学习子项目，负责从 1v1 `.grbr` 回放构造逐回合样本、训练最终胜率模型、评估概率质量，并对录像或实时观战 JSON 执行推理。

## 模型定义

- 样本阶段：`battle_start`
- 输入：双方锁定后进入战斗的局面
- 标签：该场对局最终胜者
- 划分：按完整 `battleId` 隔离训练集和测试集
- 对称性：交换双方后特征取反，预测概率交换
- 模型：标准化特征 + 强 L2 正则逻辑回归

特征包含核心、补给、上一轮结果、单位数量、等级、经验、出售价值、装备数量、科技数量和坐标统计。完整局面 schema 与空间模型规划见整体路线图。

## 安装

以下命令均从仓库根目录执行：

```powershell
python -m venv .venv
.venv\Scripts\python -m pip install -e .\win-predictor
```

安装后提供四个稳定入口：

```text
mecha-train
mecha-predict
mecha-watch
mecha-timeline
```

## 训练

```powershell
.venv\Scripts\mecha-train `
  --replay-dir "C:\path\to\ProjectDatas\Replay"
```

模型默认写入 `artifacts/win_model.joblib`，同名 JSON 文件记录训练规模、按组交叉验证、整体指标和逐回合指标。

## 单次预测

```powershell
dotnet run -c Release --project data-collector\src\Mechabellum.ObserverStats -- live --json |
  .venv\Scripts\mecha-predict
```

## 持续观战

双方进入 `Fighting` 时，每回合只输出一次整局最终胜率：

```powershell
dotnet run -c Release --project data-collector\src\Mechabellum.ObserverStats -- live-watch --json |
  .venv\Scripts\mecha-watch
```

## 录像胜率时间线

```powershell
.venv\Scripts\mecha-timeline "C:\path\to\match.grbr"
```

## 测试

```powershell
.venv\Scripts\python -m unittest discover `
  -s win-predictor\tests -v
```

概率校准使用数千场同版本 1v1 录像，并按对局和时间隔离训练、验证与测试数据。完整坐标表示、具体科技/装备、发牌、专家、建筑和克制交互见 [整体路线图](../TODO.md)。整体架构见 [根 README](../README.md)。
