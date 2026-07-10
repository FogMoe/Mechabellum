from dataclasses import dataclass


@dataclass(frozen=True)
class MemoryLayout:
    replay_version: int
    game_version: str
    game_assembly_sha256: str
    match_client_class_global_rva: int


SUPPORTED_LAYOUT = MemoryLayout(
    replay_version=2119,
    game_version="1.11.0.2a",
    game_assembly_sha256="0FE278BC3A1DD6FF55A51DB2807CCD73ED16A1FE390FE72FFA013F0AEFF495F1",
    match_client_class_global_rva=0x3C799F8,
)
