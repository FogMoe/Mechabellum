import os
import struct

import pytest

from mechabellum.collector.memory.il2cpp_reader import Il2CppReader
from mechabellum.collector.memory.windows_process import find_module, find_process_id


class FakeMemory:
    def __init__(self, base: int = 0x1000, size: int = 0x1000) -> None:
        self.base = base
        self.data = bytearray(size)

    def write(self, address: int, value: bytes) -> None:
        offset = address - self.base
        self.data[offset : offset + len(value)] = value

    def read(self, address: int, size: int) -> bytes:
        offset = address - self.base
        return bytes(self.data[offset : offset + size])


def test_il2cpp_list_and_string_layout() -> None:
    memory = FakeMemory()
    reader = Il2CppReader(memory)  # type: ignore[arg-type]
    list_address = 0x1100
    array_address = 0x1200
    memory.write(list_address + 0x10, struct.pack("<Q", array_address))
    memory.write(list_address + 0x18, struct.pack("<i", 2))
    memory.write(array_address + 0x18, struct.pack("<Q", 4))
    memory.write(array_address + 0x20, struct.pack("<QQ", 0xABC, 0xDEF))
    assert reader.read_list_pointers(list_address, 8) == [0xABC, 0xDEF]

    string_address = 0x1300
    value = "观战"
    memory.write(string_address + 0x10, struct.pack("<i", len(value)))
    memory.write(string_address + 0x14, value.encode("utf-16-le"))
    assert reader.read_managed_string(string_address) == value


@pytest.mark.skipif(os.name != "nt", reason="Windows integration test")
def test_running_game_module_can_be_enumerated() -> None:
    process_id = find_process_id("Mechabellum.exe")
    if process_id is None:
        pytest.skip("Mechabellum is not running")
    module = find_module(process_id, "GameAssembly.dll")
    assert module is not None
    assert module.base_address > 0
    assert module.path.is_file()
