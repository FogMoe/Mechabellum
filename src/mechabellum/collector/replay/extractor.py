from pathlib import Path
from xml.etree import ElementTree

XML_START = b"<?xml"
BATTLE_RECORD_END = b"</BattleRecord>"
MAX_XML_BYTES = 64 * 1024 * 1024


def extract_battle_record(path: Path) -> ElementTree.Element:
    if not path.is_file():
        raise FileNotFoundError(f"找不到回放文件：{path}")
    data = path.read_bytes()
    start = data.find(XML_START)
    if start < 0:
        raise ValueError("回放中未找到 BattleRecord XML 起始标记。")
    end = data.find(BATTLE_RECORD_END, start)
    if end < 0:
        raise ValueError("回放中的 BattleRecord XML 不完整。")
    payload = data[start : end + len(BATTLE_RECORD_END)]
    if len(payload) > MAX_XML_BYTES:
        raise ValueError(f"回放 XML 超过安全上限 {MAX_XML_BYTES // 1024 // 1024} MiB。")
    if b"<!DOCTYPE" in payload.upper():
        raise ValueError("BattleRecord XML 包含不允许的 DTD。")
    return ElementTree.fromstring(payload)
