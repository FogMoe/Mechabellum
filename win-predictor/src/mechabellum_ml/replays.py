from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Any
from xml.etree import ElementTree


@dataclass(frozen=True)
class TrainingMatch:
    battle_id: str
    snapshots: list[dict[str, Any]]
    winner_index: int


def load_training_match(path: Path) -> TrainingMatch | None:
    root = _extract_xml(path)
    battle_info = root.find("BattleInfo")
    if battle_info is None or _text(battle_info, "MatchMode") != "VS_1_1":
        return None

    player_records = root.findall("./playerRecords/PlayerRecord")
    if len(player_records) != 2:
        return None

    match_snapshots = root.findall("./matchDatas/MatchSnapshotData")
    if not match_snapshots:
        return None
    reports = match_snapshots[-1].findall("./lastFightResult/Reports/FightReport")
    if len(reports) != 2:
        return None
    scores = [_int(report, "Score") for report in reports]
    if scores[0] == scores[1]:
        return None
    winner_index = 0 if scores[0] > scores[1] else 1

    player_rounds = [_round_map(player) for player in player_records]
    # Round 0 is the opening/advance-team selection state. Round N (N >= 1)
    # contains the locked deployment that enters battle N; actions stored under
    # that record become visible in the round N+1 snapshot.
    common_rounds = sorted(
        round_number
        for round_number in set(player_rounds[0]).intersection(player_rounds[1])
        if round_number >= 1
    )
    if not common_rounds:
        return None

    battle_id = _text(battle_info, "BattleID") or path.stem
    snapshots = []
    for round_number in common_rounds:
        snapshots.append(
            {
                "sourceFile": str(path.resolve()),
                "version": _optional_int(root, "Version"),
                "battleId": battle_id,
                "mapId": _optional_int(battle_info, "MapID"),
                "gameMode": _text(battle_info, "GameMode"),
                "matchMode": _text(battle_info, "MatchMode"),
                "round": round_number,
                "players": [
                    _parse_player_round(player_records[0], player_rounds[0][round_number]),
                    _parse_player_round(player_records[1], player_rounds[1][round_number]),
                ],
            }
        )
    return TrainingMatch(battle_id, snapshots, winner_index)


def _extract_xml(path: Path) -> ElementTree.Element:
    data = path.read_bytes()
    start = data.find(b"<?xml")
    end_tag = b"</BattleRecord>"
    end = data.find(end_tag, start)
    if start < 0 or end < 0:
        raise ValueError("没有找到内嵌 BattleRecord XML")
    return ElementTree.fromstring(data[start : end + len(end_tag)])


def _round_map(player: ElementTree.Element) -> dict[int, ElementTree.Element]:
    result = {}
    for round_element in player.findall("./playerRoundRecords/PlayerRoundRecord"):
        result[_int(round_element, "round")] = round_element
    return result


def _parse_player_round(player: ElementTree.Element, round_element: ElementTree.Element) -> dict[str, Any]:
    player_data = round_element.find("playerData")
    if player_data is None:
        raise ValueError("PlayerRoundRecord 缺少 playerData")

    units = []
    for unit in player_data.findall("./units/NewUnitData"):
        position = unit.find("Position")
        units.append(
            {
                "unitId": _int(unit, "id"),
                "formationIndex": _int(unit, "Index"),
                "rawLevel": _int(unit, "Level"),
                "displayLevel": _int(unit, "Level") + 1,
                "experience": _int(unit, "Exp"),
                "x": _float(position, "x"),
                "y": _float(position, "y"),
                "isRotated": (_text(unit, "IsRotate") or "").lower() == "true",
                "equipmentId": _int(unit, "EquipmentID"),
                "sellSupply": _int(unit, "SellSupply"),
            }
        )

    active_technologies = []
    for technology_set in player_data.findall("./activeTechnologies/UnitData"):
        active_technologies.append(
            {
                "unitId": _int(technology_set, "id"),
                "technologyIds": [
                    int(node.attrib.get("data", "0"))
                    for node in technology_set.findall("./techs/tech")
                    if node.attrib.get("data", "0").lstrip("-").isdigit()
                ],
            }
        )

    player_id = _text(player, "id")
    return {
        "playerId": int(player_id) if player_id and player_id.isdigit() else None,
        "name": _text(player, "name") or "Unknown player",
        "team": _optional_int(player.find("data"), "team"),
        "supply": _optional_int(player_data, "supply"),
        "reactorCore": _optional_int(player_data, "reactorCore"),
        "previousFightResult": _text(player_data, "preRoundFightResult"),
        "units": units,
        "activeTechnologies": active_technologies,
    }


def _text(parent: ElementTree.Element | None, name: str) -> str | None:
    if parent is None:
        return None
    value = parent.findtext(name)
    return value.strip() if value and value.strip() else None


def _int(parent: ElementTree.Element | None, name: str) -> int:
    return _optional_int(parent, name) or 0


def _optional_int(parent: ElementTree.Element | None, name: str) -> int | None:
    value = _text(parent, name)
    try:
        return int(value) if value is not None else None
    except ValueError:
        return None


def _float(parent: ElementTree.Element | None, name: str) -> float:
    value = _text(parent, name)
    try:
        return float(value) if value is not None else 0.0
    except ValueError:
        return 0.0
