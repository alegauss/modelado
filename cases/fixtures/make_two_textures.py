"""Writes cases/fixtures/two-textures.glb: two triangles, one red-textured and one blue-textured, each
material with its own 8 x 8 PNG (MOD59). Opened, Modelado packs them into one 16 x 8 atlas.

    python cases/fixtures/make_two_textures.py
"""
import json
import pathlib
import struct
import zlib


def png(r: int, g: int, b: int, side: int = 8) -> bytes:
    rows = b"".join(b"\x00" + bytes([r, g, b, 255]) * side for _ in range(side))

    def chunk(kind: bytes, data: bytes) -> bytes:
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    header = struct.pack(">IIBBBBB", side, side, 8, 6, 0, 0, 0)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


binary = bytearray()
views, accessors, primitives, materials, textures, images = [], [], [], [], [], []


def view(data: bytes) -> int:
    views.append({"buffer": 0, "byteOffset": len(binary), "byteLength": len(data)})
    binary.extend(data)
    while len(binary) % 4:
        binary.append(0)
    return len(views) - 1


for t, colour in enumerate([(255, 0, 0), (0, 0, 255)]):
    corners = struct.pack("<9f", t, 0, 0, t + 1, 0, 0, t, 1, 0)
    uvs = struct.pack("<6f", 0.1, 0.9, 0.9, 0.9, 0.1, 0.1)
    accessors.append({"bufferView": view(corners), "componentType": 5126, "count": 3, "type": "VEC3", "min": [t, 0, 0], "max": [t + 1, 1, 0]})
    accessors.append({"bufferView": view(uvs), "componentType": 5126, "count": 3, "type": "VEC2"})
    images.append({"bufferView": view(png(*colour)), "mimeType": "image/png"})
    textures.append({"source": t})
    materials.append({"pbrMetallicRoughness": {"baseColorTexture": {"index": t}}})
    primitives.append({"attributes": {"POSITION": 2 * t, "TEXCOORD_0": 2 * t + 1}, "material": t})

document = {
    "asset": {"version": "2.0"}, "scene": 0, "scenes": [{"nodes": [0]}], "nodes": [{"mesh": 0}],
    "meshes": [{"primitives": primitives}], "materials": materials, "textures": textures, "images": images,
    "accessors": accessors, "bufferViews": views, "buffers": [{"byteLength": len(binary)}],
}
text = json.dumps(document, separators=(",", ":")).encode()
text += b" " * (-len(text) % 4)
glb = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(text) + 8 + len(binary))
glb += struct.pack("<II", len(text), 0x4E4F534A) + text + struct.pack("<II", len(binary), 0x004E4942) + bytes(binary)
out = pathlib.Path(__file__).resolve().parent / "two-textures.glb"
out.write_bytes(glb)
print(f"wrote {out}")
