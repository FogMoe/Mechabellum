from __future__ import annotations

import argparse
import json
import sys
from datetime import UTC, datetime
from pathlib import Path

import joblib
import numpy as np
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import accuracy_score, brier_score_loss, log_loss, roc_auc_score
from sklearn.model_selection import GroupKFold, GroupShuffleSplit
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler

from .features import build_features, feature_names
from .replays import load_training_match


def main() -> int:
    _configure_output()
    args = _arguments()
    replay_dir = args.replay_dir.resolve()
    paths = sorted(replay_dir.glob("*.grbr"))
    if not paths:
        raise SystemExit(f"回放目录中没有 .grbr 文件：{replay_dir}")

    rows: list[np.ndarray] = []
    labels: list[int] = []
    groups: list[str] = []
    rounds: list[int] = []
    parsed_matches = 0
    skipped = 0
    failed: list[str] = []

    for path in paths:
        try:
            match = load_training_match(path)
            if match is None:
                skipped += 1
                continue
            parsed_matches += 1
            for snapshot in match.snapshots:
                for perspective in (0, 1):
                    rows.append(build_features(snapshot, perspective))
                    labels.append(int(perspective == match.winner_index))
                    groups.append(match.battle_id)
                    rounds.append(int(snapshot["round"]))
        except Exception as error:  # keep a bad replay from stopping the full dataset
            failed.append(f"{path.name}: {error}")

    if parsed_matches < 10:
        raise SystemExit(f"只有 {parsed_matches} 场可用 1v1，至少需要 10 场才能执行验证划分。")

    x = np.vstack(rows)
    y = np.asarray(labels, dtype=np.int8)
    group_values = np.asarray(groups)
    round_values = np.asarray(rounds)
    splitter = GroupShuffleSplit(n_splits=1, test_size=args.test_size, random_state=args.seed)
    train_indices, test_indices = next(splitter.split(x, y, group_values))

    selected_regularization, cross_validation = _select_regularization(
        x[train_indices],
        y[train_indices],
        group_values[train_indices],
        args.seed,
        args.regularization,
    )
    model = _new_model(selected_regularization, args.seed)
    model.fit(x[train_indices], y[train_indices])
    probabilities = model.predict_proba(x[test_indices])[:, 1]
    predictions = probabilities >= 0.5
    metrics = {
        "accuracy": float(accuracy_score(y[test_indices], predictions)),
        "rocAuc": float(roc_auc_score(y[test_indices], probabilities)),
        "logLoss": float(log_loss(y[test_indices], probabilities)),
        "brier": float(brier_score_loss(y[test_indices], probabilities)),
    }
    metrics_by_round = _metrics_by_round(y[test_indices], probabilities, round_values[test_indices])

    artifact = {
        "model": model,
        "featureNames": feature_names(),
        "schemaVersion": 1,
        "snapshotPhase": "battle_start",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    joblib.dump(artifact, args.output)

    train_matches = len(set(group_values[train_indices]))
    test_matches = len(set(group_values[test_indices]))
    metadata = {
        "createdAtUtc": datetime.now(UTC).isoformat(),
        "snapshotPhase": "battle_start",
        "replayDirectory": str(replay_dir),
        "inputReplayCount": len(paths),
        "usableMatchCount": parsed_matches,
        "skippedReplayCount": skipped,
        "failedReplayCount": len(failed),
        "trainingMatchCount": train_matches,
        "testMatchCount": test_matches,
        "trainingSampleCount": int(len(train_indices)),
        "testSampleCount": int(len(test_indices)),
        "selectedRegularization": selected_regularization,
        "crossValidation": cross_validation,
        "metrics": metrics,
        "metricsByRound": metrics_by_round,
        "warning": "当前数据量很小时，概率仅用于验证流程，不代表已校准的真实胜率。",
        "failures": failed[:20],
    }
    metadata_path = args.output.with_suffix(".json")
    metadata_path.write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(metadata, ensure_ascii=False, indent=2))
    print(f"模型: {args.output.resolve()}")
    print(f"报告: {metadata_path.resolve()}")
    return 0


def _configure_output() -> None:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def _arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="从 Mechabellum .grbr 回放训练 1v1 观战胜率基线模型。")
    parser.add_argument("--replay-dir", type=Path, required=True, help="包含 .grbr 的回放目录。")
    parser.add_argument("--output", type=Path, default=Path("artifacts/win_model.joblib"))
    parser.add_argument("--test-size", type=float, default=0.25)
    parser.add_argument("--seed", type=int, default=20260711)
    parser.add_argument("--regularization", type=float, help="固定逻辑回归 C；省略时在训练集内按组交叉验证选择。")
    return parser.parse_args()


def _new_model(regularization: float, seed: int) -> Pipeline:
    return Pipeline(
        [
            ("scale", StandardScaler()),
            (
                "classifier",
                LogisticRegression(
                    C=regularization,
                    fit_intercept=False,
                    max_iter=5000,
                    random_state=seed,
                ),
            ),
        ]
    )


def _select_regularization(
    x: np.ndarray,
    y: np.ndarray,
    groups: np.ndarray,
    seed: int,
    requested: float | None,
) -> tuple[float, list[dict[str, float]]]:
    candidates = [requested] if requested is not None else [0.001, 0.003, 0.01, 0.03, 0.1, 0.3]
    splitter = GroupKFold(n_splits=5, shuffle=True, random_state=seed)
    results = []
    for regularization in candidates:
        losses = []
        for fold_train, fold_validation in splitter.split(x, y, groups):
            model = _new_model(regularization, seed)
            model.fit(x[fold_train], y[fold_train])
            probabilities = model.predict_proba(x[fold_validation])[:, 1]
            losses.append(float(log_loss(y[fold_validation], probabilities)))
        results.append(
            {
                "regularization": float(regularization),
                "meanLogLoss": float(np.mean(losses)),
                "standardDeviation": float(np.std(losses)),
            }
        )
    selected = min(results, key=lambda result: result["meanLogLoss"])["regularization"]
    return selected, results


def _metrics_by_round(labels: np.ndarray, probabilities: np.ndarray, rounds: np.ndarray) -> dict[str, dict[str, float | int]]:
    result = {}
    for round_number in sorted(set(rounds.tolist())):
        mask = rounds == round_number
        round_labels = labels[mask]
        round_probabilities = probabilities[mask]
        result[str(round_number)] = {
            "samples": int(mask.sum()),
            "accuracy": float(accuracy_score(round_labels, round_probabilities >= 0.5)),
            "rocAuc": float(roc_auc_score(round_labels, round_probabilities)),
            "logLoss": float(log_loss(round_labels, round_probabilities)),
            "brier": float(brier_score_loss(round_labels, round_probabilities)),
        }
    return result


if __name__ == "__main__":
    raise SystemExit(main())
