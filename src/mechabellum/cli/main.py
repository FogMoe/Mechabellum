from __future__ import annotations

import argparse
import ctypes
import json
import os
import sys
import time
from pathlib import Path
from typing import Any

from ..collector import locate_game
from ..collector.replay import parse_replay, select_round
from ..contracts import MatchSnapshot, snapshot_to_dict, snapshot_to_json, validate_snapshot


def main() -> int:
    _configure_console()
    parser = _parser()
    args = parser.parse_args()
    if not hasattr(args, "handler"):
        parser.print_help()
        return 2
    try:
        return int(args.handler(args) or 0)
    except KeyboardInterrupt:
        return 0
    except Exception as error:
        print(f"错误: {error}", file=sys.stderr)
        return 1


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="mecha", description="Mechabellum 数据采集、训练与胜率推理。")
    commands = parser.add_subparsers(dest="command")

    _command(commands, "probe", "定位游戏、回放和日志。", _probe)
    latest = _command(commands, "latest", "读取最新回放快照。", _latest)
    latest.add_argument("--replay-dir", type=Path)
    latest.add_argument("--round", type=int)
    latest.add_argument("--json", action="store_true")

    parse = _command(commands, "parse", "读取指定回放快照。", _parse)
    parse.add_argument("replay", type=Path)
    parse.add_argument("--round", type=int)
    parse.add_argument("--json", action="store_true")

    dump = _command(commands, "dump", "输出指定回放的全部标准快照。", _dump)
    dump.add_argument("replay", type=Path)

    export = _command(commands, "export-dataset", "把回放目录导出为 MatchSnapshot v1 JSONL。", _export_dataset)
    export.add_argument("replay_directory", type=Path, nargs="?")
    export.add_argument("--output", type=Path, required=True)

    _command(commands, "live-probe", "报告实时内存布局和对局状态。", _live_probe)
    live = _command(commands, "live", "读取一次实时对局快照。", _live)
    live.add_argument("--json", action="store_true")
    watch = _command(commands, "live-watch", "持续输出变化的实时对局快照。", _live_watch)
    watch.add_argument("--interval", type=int, default=1000, help="轮询毫秒数，范围 250-30000。")
    watch.add_argument("--json", action="store_true")
    live_predict = _command(commands, "live-predict", "每回合战斗开始时直接输出最终胜率。", _live_predict)
    live_predict.add_argument("--interval", type=int, default=500)
    live_predict.add_argument("--model", type=Path, default=Path("artifacts/win_model.joblib"))

    train = _command(commands, "train", "从 MatchSnapshot JSONL 训练最终胜率模型。", _train)
    train.add_argument("--dataset", type=Path, required=True)
    train.add_argument("--output", type=Path, default=Path("artifacts/win_model.joblib"))
    train.add_argument("--test-size", type=float, default=0.25)
    train.add_argument("--seed", type=int, default=20260711)
    train.add_argument("--regularization", type=float)

    predict = _command(commands, "predict", "对一个 MatchSnapshot 计算最终胜率。", _predict)
    predict.add_argument("--input", type=Path, help="JSON 文件；省略时读取标准输入。")
    predict.add_argument("--model", type=Path, default=Path("artifacts/win_model.joblib"))

    timeline = _command(commands, "timeline", "输出一场对局的逐回合胜率。", _timeline)
    timeline.add_argument("dataset", type=Path)
    timeline.add_argument("--battle-id")
    timeline.add_argument("--model", type=Path, default=Path("artifacts/win_model.joblib"))
    return parser


def _command(subparsers: Any, name: str, help_text: str, handler: Any) -> argparse.ArgumentParser:
    command = subparsers.add_parser(name, help=help_text, description=help_text)
    command.set_defaults(handler=handler)
    return command


def _configure_console() -> None:
    if os.name == "nt":
        kernel32 = ctypes.windll.kernel32
        kernel32.SetConsoleOutputCP(65001)
        kernel32.SetConsoleCP(65001)
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if hasattr(sys.stderr, "reconfigure"):
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")


def _probe(_: argparse.Namespace) -> None:
    locations = locate_game()
    print(f"游戏目录: {locations.install_directory or '未找到'}")
    print(f"回放目录: {locations.replay_directory or '未找到'}")
    print(f"玩家日志: {locations.player_log or '未找到'}")


def _latest(args: argparse.Namespace) -> None:
    directory = args.replay_dir or _require_replay_directory()
    paths = sorted(directory.glob("*.grbr"), key=lambda path: path.stat().st_mtime_ns, reverse=True)
    if not paths:
        raise FileNotFoundError(f"回放目录中没有 .grbr：{directory}")
    _render(select_round(parse_replay(paths[0]), args.round), args.json)


def _parse(args: argparse.Namespace) -> None:
    _render(select_round(parse_replay(args.replay), args.round), args.json)


def _dump(args: argparse.Namespace) -> None:
    replay = parse_replay(args.replay)
    snapshots = [
        snapshot_to_dict(select_round(replay, round_number))
        for round_number in replay.common_rounds
        if round_number >= 1
    ]
    print(json.dumps(snapshots, ensure_ascii=False, indent=2))


def _export_dataset(args: argparse.Namespace) -> None:
    from ..datasets import export_replay_directory

    summary = export_replay_directory(args.replay_directory or _require_replay_directory(), args.output)
    for failure in summary.failures:
        print(f"跳过: {failure}", file=sys.stderr)
    print(
        f"导出完成：回放 {summary.replay_file_count}，1v1 {summary.match_count}，"
        f"快照 {summary.snapshot_count}，失败 {summary.failure_count}，输出 {args.output.resolve()}",
        file=sys.stderr,
    )


def _live_probe(_: argparse.Namespace) -> None:
    from ..collector.memory import LiveMemorySource

    with LiveMemorySource.attach() as source:
        print(json.dumps(source.get_status().to_dict(), ensure_ascii=False, indent=2))


def _live(args: argparse.Namespace) -> None:
    from ..collector.memory import LiveMemorySource

    with LiveMemorySource.attach() as source:
        _render(source.read_snapshot(), args.json)


def _live_watch(args: argparse.Namespace) -> None:
    from ..collector.memory import LiveMemorySource, ObserverStateError

    interval = _interval(args.interval)
    previous: str | None = None
    with LiveMemorySource.attach() as source:
        while True:
            try:
                snapshot = source.read_snapshot()
                stable = snapshot.model_copy(
                    update={"source": snapshot.source.model_copy(update={"captured_at_utc": None})}
                )
                fingerprint = snapshot_to_json(stable)
                if fingerprint != previous:
                    _render(snapshot, args.json)
                    previous = fingerprint
            except ObserverStateError as error:
                message = str(error)
                if message != previous:
                    print(message, file=sys.stderr)
                    previous = message
            time.sleep(interval)


def _live_predict(args: argparse.Namespace) -> None:
    from ..collector.memory import LiveMemorySource, ObserverStateError
    from ..features import feature_names
    from ..inference import predict_snapshot
    from ..models import load_artifact

    artifact = load_artifact(args.model, feature_names())
    emitted: set[tuple[str | None, int]] = set()
    interval = _interval(args.interval)
    with LiveMemorySource.attach() as source:
        while True:
            try:
                snapshot = source.read_snapshot()
                key = (snapshot.match.battle_id, snapshot.match.round)
                if snapshot.match.phase == "battle_start" and key not in emitted:
                    emitted.add(key)
                    print(json.dumps(predict_snapshot(artifact, snapshot, args.model), ensure_ascii=False), flush=True)
            except ObserverStateError as error:
                print(str(error), file=sys.stderr)
            time.sleep(interval)


def _train(args: argparse.Namespace) -> None:
    from ..training import train_model

    metadata = train_model(
        args.dataset,
        args.output,
        test_size=args.test_size,
        seed=args.seed,
        regularization=args.regularization,
    )
    print(json.dumps(metadata, ensure_ascii=False, indent=2))


def _predict(args: argparse.Namespace) -> None:
    from ..features import feature_names
    from ..inference import predict_snapshot
    from ..models import load_artifact

    data = json.load(sys.stdin) if args.input is None else json.loads(args.input.read_text(encoding="utf-8-sig"))
    snapshot = validate_snapshot(data)
    print(
        json.dumps(
            predict_snapshot(load_artifact(args.model, feature_names()), snapshot, args.model),
            ensure_ascii=False,
            indent=2,
        )
    )


def _timeline(args: argparse.Namespace) -> None:
    from ..inference import render_timeline

    print(json.dumps(render_timeline(args.dataset, args.model, args.battle_id), ensure_ascii=False, indent=2))


def _render(snapshot: MatchSnapshot, as_json: bool) -> None:
    if as_json:
        print(snapshot_to_json(snapshot, indent=2))
        return
    print(f"来源: {snapshot.source.uri}")
    print(
        f"对局: {snapshot.match.battle_id or '未知'}  版本: {snapshot.game_build.replay_version or '未知'}  "
        f"地图: {snapshot.match.map_id or '未知'}"
    )
    print(
        f"模式: {snapshot.match.game_mode or '未知'}/{snapshot.match.match_mode or '未知'}  "
        f"回合: {snapshot.match.round}  阶段: {snapshot.match.phase}"
    )
    for player in snapshot.players:
        print(
            f"\n[{player.name}] Team={player.team if player.team is not None else '?'}  "
            f"状态={player.state or '?'}  编队数={len(player.units)}  "
            f"补给={player.supply if player.supply is not None else '?'}  "
            f"核心={player.reactor_core if player.reactor_core is not None else '?'}"
        )
        counts: dict[tuple[int, str, int], int] = {}
        for unit in player.units:
            key = (unit.unit_id, unit.unit_name, unit.display_level)
            counts[key] = counts.get(key, 0) + 1
        for (unit_id, name, level), count in sorted(counts.items()):
            print(f"  {name} [ID {unit_id}]: Lv{level} × {count}")


def _require_replay_directory() -> Path:
    replay = locate_game().replay_directory
    if replay is None:
        raise FileNotFoundError("未找到 Mechabellum 回放目录。")
    return replay


def _interval(milliseconds: int) -> float:
    if milliseconds < 250 or milliseconds > 30_000:
        raise ValueError("interval 必须在 250 到 30000 毫秒之间。")
    return milliseconds / 1000
