from pathlib import Path

from mechabellum.collector.replay import parse_replay, select_round
from mechabellum.contracts import snapshot_to_dict, validate_snapshot
from mechabellum.datasets import export_replay_directory, load_snapshots


def _write_replay(path: Path) -> None:
    xml = """<?xml version="1.0" encoding="utf-8"?>
<BattleRecord>
  <playerRecords>
    <PlayerRecord><id>1</id><name>A</name><data><team>0</team></data><playerRoundRecords>
      <PlayerRoundRecord><round>1</round><playerData><reactorCore>4500</reactorCore><supply>100</supply><preRoundFightResult>Win</preRoundFightResult><units>
        <NewUnitData><id>3</id><Index>0</Index><RoundCount>0</RoundCount><Durability>0</Durability><Exp>12</Exp><Level>0</Level><Position><x>-20</x><y>-150</y></Position><EquipmentID>0</EquipmentID><IsRotate>false</IsRotate><SellSupply>400</SellSupply></NewUnitData>
      </units><activeTechnologies><UnitData><id>3</id><techs><tech data="30101" /></techs></UnitData></activeTechnologies></playerData></PlayerRoundRecord>
    </playerRoundRecords></PlayerRecord>
    <PlayerRecord><id>2</id><name>B</name><data><team>1</team></data><playerRoundRecords>
      <PlayerRoundRecord><round>1</round><playerData><reactorCore>4300</reactorCore><supply>50</supply><preRoundFightResult>Lose</preRoundFightResult><units /><activeTechnologies /></playerData></PlayerRoundRecord>
    </playerRoundRecords></PlayerRecord>
  </playerRecords>
  <matchDatas><MatchSnapshotData><lastFightResult><Reports><FightReport><Score>500</Score></FightReport><FightReport><Score>0</Score></FightReport></Reports></lastFightResult></MatchSnapshotData></matchDatas>
  <Version>2119</Version>
  <BattleInfo><BattleID>fixture-battle</BattleID><MapID>1001</MapID><GameMode>Normal</GameMode><MatchMode>VS_1_1</MatchMode></BattleInfo>
</BattleRecord>"""
    path.write_bytes(b"binary-prefix" + xml.encode("utf-8") + b"binary-suffix")


def test_replay_parser_outputs_contract(tmp_path: Path) -> None:
    replay_path = tmp_path / "fixture.grbr"
    _write_replay(replay_path)
    replay = parse_replay(replay_path)
    snapshot = select_round(replay, 1)
    assert replay.winner_index == 0
    assert snapshot.players[0].units[0].unit_id == 3
    assert snapshot.outcome is not None and snapshot.outcome.winner_name == "A"
    validate_snapshot(snapshot_to_dict(snapshot))


def test_dataset_export_uses_replay_parser(tmp_path: Path) -> None:
    _write_replay(tmp_path / "fixture.grbr")
    output = tmp_path / "snapshots.jsonl"
    summary = export_replay_directory(tmp_path, output)
    snapshots = load_snapshots(output)
    assert summary.snapshot_count == 1
    assert summary.failure_count == 0
    assert snapshots[0].match.battle_id == "fixture-battle"
