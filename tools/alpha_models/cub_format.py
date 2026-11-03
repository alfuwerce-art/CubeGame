"""Cube World Alpha 2013 `.cub` voxel model format: decode helpers.

Own clean-room implementation written for the S05A-0 conversion pilot. It is
based on the documented structure of the format (research notes in
`docs/research/alpha2013/BEHAVIOR_SPECS/K_assets.md`) and on a reconstructed
pseudo-C description of the loader (`VoxelModel_load_from_file_or_buffer`,
`blob_deobfuscate`). No code from the original game or from third parties is
copied here.

Layout of a decoded `.cub` payload::

    int32le width
    int32le depth
    int32le height
    uint8[width * depth * height * 3]   # RGB per voxel
                                        # index = (h * depth + d) * width + x
                                        # (0, 0, 0) means "empty voxel"

The field order ``(width, depth, height)`` and the ``(h, d, x)`` iteration order
were cross-checked against the independent open-source implementation in
`vengi` (`src/modules/voxelformat/private/cubeworld/CubFormat.cpp`), which reads
the same three fields and notes that Cube World's depth/height axes are swapped
relative to its own convention.

Stored blobs in `data1.db` are obfuscated: a table-driven de-shuffle followed by
a bitwise complement of every byte. :func:`deobfuscate` reverses that.

This module never reads the original game files by itself; callers pass bytes.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass

# Table at 0x006FFA68 in Cube.exe (44 x int32), used by `blob_deobfuscate`.
DEOBFUSCATION_TABLE = (
    4242, 9551, 840, 84800, 9242, 9846, 127, 9, 9483, 394, 123, 4834,
    32444, 24355, 2433, 17, 34234, 42342, 4243, 14, 184934, 1987, 3094,
    1901, 89409, 4813, 37, 143, 3490, 19483, 1343, 432, 84732, 9184,
    9612, 1233, 3434, 1839, 2984, 1993, 2984, 4895, 816583, 13,
)
TABLE_MOD = 0x2C  # 44

HEADER_SIZE = 12
BYTES_PER_VOXEL = 3

MAX_DIMENSION = 512  # sanity bound: the largest shipped model is 64x64x50


class CubFormatError(ValueError):
    """Raised for malformed `.cub` payloads."""


@dataclass(frozen=True)
class VoxelModel:
    """Decoded voxel model.

    ``width`` — x axis, ``depth`` — second stored field, ``height`` — third.
    """

    width: int
    depth: int
    height: int
    #: RGB bytes, length == width * depth * height * 3
    pixels: bytes

    @property
    def voxel_count(self) -> int:
        return self.width * self.depth * self.height

    @property
    def dimensions(self) -> tuple[int, int, int]:
        return (self.width, self.depth, self.height)

    @property
    def non_empty_count(self) -> int:
        px = self.pixels
        return sum(
            1 for i in range(0, len(px), 3)
            if px[i] or px[i + 1] or px[i + 2]
        )

    def rgb_at(self, x: int, d: int, h: int) -> tuple[int, int, int]:
        if not (0 <= x < self.width and 0 <= d < self.depth and 0 <= h < self.height):
            raise CubFormatError(f"voxel ({x},{d},{h}) outside model bounds")
        offset = ((h * self.depth + d) * self.width + x) * BYTES_PER_VOXEL
        return tuple(self.pixels[offset:offset + 3])  # type: ignore[return-value]


def deobfuscate(data: bytes) -> bytes:
    """Reverse `blob_deobfuscate`: de-shuffle with the 44-entry table, then invert bytes."""
    buf = bytearray(data)
    n = len(buf)
    if n == 0:
        return bytes(buf)
    for i in range(n - 1, -1, -1):
        swap_pos = (DEOBFUSCATION_TABLE[i % TABLE_MOD] + i) % n
        buf[i], buf[swap_pos] = buf[swap_pos], buf[i]
    for i in range(n):
        buf[i] = (~buf[i]) & 0xFF
    return bytes(buf)


def obfuscate(data: bytes) -> bytes:
    """Inverse of :func:`deobfuscate` (used by tests to prove round-trip)."""
    buf = bytearray(data)
    for i in range(len(buf)):
        buf[i] = (~buf[i]) & 0xFF
    n = len(buf)
    for i in range(n):
        swap_pos = (DEOBFUSCATION_TABLE[i % TABLE_MOD] + i) % n
        buf[i], buf[swap_pos] = buf[swap_pos], buf[i]
    return bytes(buf)


def parse_decoded(decoded: bytes) -> VoxelModel:
    """Parse a de-obfuscated `.cub` payload. Raises :class:`CubFormatError` on bad input."""
    if len(decoded) < HEADER_SIZE:
        raise CubFormatError(
            f"payload too small: {len(decoded)} bytes, need at least {HEADER_SIZE}")
    width, depth, height = struct.unpack_from("<iii", decoded, 0)
    for name, value in (("width", width), ("depth", depth), ("height", height)):
        if value <= 0 or value > MAX_DIMENSION:
            raise CubFormatError(f"implausible {name}={value} (expected 1..{MAX_DIMENSION})")
    expected = HEADER_SIZE + width * depth * height * BYTES_PER_VOXEL
    if len(decoded) != expected:
        raise CubFormatError(
            f"size mismatch: {len(decoded)} bytes, expected {expected} "
            f"for {width}x{depth}x{height} (w*d*h)")
    return VoxelModel(width, depth, height, decoded[HEADER_SIZE:])


def parse_stored(stored: bytes) -> VoxelModel:
    """Parse a blob exactly as stored in `data1.db` (obfuscated)."""
    return parse_decoded(deobfuscate(stored))


def encode_model(model: VoxelModel) -> bytes:
    """Rebuild a de-obfuscated payload from a model (round-trip helper for tests)."""
    return struct.pack("<iii", model.width, model.depth, model.height) + model.pixels


def solid_colors(model: VoxelModel, limit: int | None = None) -> dict[tuple[int, int, int], int]:
    """Histogram of non-empty voxel colours, most frequent first."""
    counts: dict[tuple[int, int, int], int] = {}
    px = model.pixels
    for i in range(0, len(px), 3):
        r, g, b = px[i], px[i + 1], px[i + 2]
        if r or g or b:
            key = (r, g, b)
            counts[key] = counts.get(key, 0) + 1
    ordered = dict(sorted(counts.items(), key=lambda kv: (-kv[1], kv[0])))
    if limit is not None:
        return dict(list(ordered.items())[:limit])
    return ordered


def find_anchor_colors(model: VoxelModel) -> dict[tuple[int, int, int], list[tuple[int, int, int]]]:
    """Pure red/green/blue voxels, used by the original engine as markers."""
    anchors: dict[tuple[int, int, int], list[tuple[int, int, int]]] = {
        (255, 0, 0): [], (0, 255, 0): [], (0, 0, 255): [],
    }
    px = model.pixels
    for h in range(model.height):
        for d in range(model.depth):
            for x in range(model.width):
                offset = ((h * model.depth + d) * model.width + x) * BYTES_PER_VOXEL
                bucket = anchors.get((px[offset], px[offset + 1], px[offset + 2]))
                if bucket is not None:
                    bucket.append((x, d, h))
    return anchors
