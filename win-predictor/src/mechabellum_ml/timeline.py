from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import joblib

from .features import build_features, feature_names
from .replays import load_training_match


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    args = _arguments()
    artifact = joblib.load(args.model)
    if artifact.get("featureNames") != feature_names():
        raise SystemExit("模型特征版本与当前代码不一致，请重新训练。")
    match = load_training_match(args.replay)
    if match is None:
        raise SystemExit("该录像不是带有唯一最终胜者的 1v1。")

    rounds = []
    for snapshot in match.snapshots:
        raw = [
            float(artifact["model"].predict_proba(build_features(snapshot, index).reshape(1, -1))[0, 1])
            for index in (0, 1)
        ]
        total = sum(raw)
        probabilities = [0.5, 0.5] if total <= 0 else [value / total for value in raw]
        rounds.append(
            {
                "round": snapshot["round"],
                "players": [
                    {
                        "name": player["name"],
                        "winPercent": round(probability * 100, 1),
                    }
                    for player, probability in zip(snapshot["players"], probabilities, strict=True)
                ],
            }
        )

    result = {
        "battleId": match.battle_id,
        "snapshotPhase": artifact.get("snapshotPhase", "unknown"),
        "finalWinner": match.snapshots[-1]["players"][match.winner_index]["name"],
        "rounds": rounds,
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="输出一场录像每回合战斗开始时的最终胜率轨迹。")
    parser.add_argument("replay", type=Path)
    parser.add_argument("--model", type=Path, default=Path("artifacts/win_model.joblib"))
    return parser.parse_args()


if __name__ == "__main__":
    raise SystemExit(main())
