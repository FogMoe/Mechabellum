from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any

import joblib

from .features import build_features, feature_names


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    args = _arguments()
    artifact = joblib.load(args.model)
    if artifact.get("featureNames") != feature_names():
        raise SystemExit("模型特征版本与当前代码不一致，请重新训练。")

    snapshot = _read_snapshot(args.input)
    result = predict_snapshot(artifact, snapshot, args.model)
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def predict_snapshot(artifact: dict[str, Any], snapshot: dict[str, Any], model_path: Path) -> dict[str, Any]:
    players = snapshot.get("players") or []
    if len(players) != 2:
        raise ValueError("当前预测器只支持 1v1 局面。")
    model = artifact["model"]
    raw = [float(model.predict_proba(build_features(snapshot, index).reshape(1, -1))[0, 1]) for index in (0, 1)]
    total = sum(raw)
    probabilities = [0.5, 0.5] if total <= 0 else [value / total for value in raw]
    states = [player.get("state") for player in players]
    result: dict[str, Any] = {
        "battleId": snapshot.get("battleId"),
        "round": snapshot.get("round"),
        "snapshotPhase": artifact.get("snapshotPhase", "unknown"),
        "predictions": [
            {
                "playerId": player.get("playerId"),
                "name": player.get("name"),
                "winProbability": probability,
                "winPercent": round(probability * 100, 1),
            }
            for player, probability in zip(players, probabilities, strict=True)
        ],
        "model": str(model_path.resolve()),
    }
    if any(states) and not all(state in ("Fighting", "BattleStart") for state in states):
        result["warning"] = f"输入玩家状态为 {states}，不是双方战斗开始快照。"
    return result


def _read_snapshot(path: Path | None) -> dict[str, Any]:
    if path is None:
        return json.load(sys.stdin)
    with path.open("r", encoding="utf-8-sig") as stream:
        return json.load(stream)


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="对 live/latest 输出的 1v1 JSON 计算观战胜率。")
    parser.add_argument("--model", type=Path, default=Path("artifacts/win_model.joblib"))
    parser.add_argument("--input", type=Path, help="局面 JSON；省略时从标准输入读取。")
    return parser.parse_args()


if __name__ == "__main__":
    raise SystemExit(main())
