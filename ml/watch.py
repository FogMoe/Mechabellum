from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import joblib

from features import feature_names
from predict import predict_snapshot


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    args = _arguments()
    artifact = joblib.load(args.model)
    if artifact.get("featureNames") != feature_names():
        raise SystemExit("模型特征版本与当前代码不一致，请重新训练。")

    emitted: set[tuple[str | None, int | None]] = set()
    for line in sys.stdin:
        if not line.strip():
            continue
        snapshot = json.loads(line)
        players = snapshot.get("players") or []
        if len(players) != 2 or not all(player.get("state") == "Fighting" for player in players):
            continue
        key = (snapshot.get("battleId"), snapshot.get("round"))
        if key in emitted:
            continue
        emitted.add(key)
        print(json.dumps(predict_snapshot(artifact, snapshot, args.model), ensure_ascii=False), flush=True)
    return 0


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="从 live-watch JSON Lines 中每回合输出一次战斗开始胜率。")
    parser.add_argument("--model", type=Path, default=Path("artifacts/win_model.joblib"))
    return parser.parse_args()


if __name__ == "__main__":
    raise SystemExit(main())
