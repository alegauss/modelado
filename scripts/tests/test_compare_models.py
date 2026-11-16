"""compare_models.py's measures: STL reading, closedness, Chamfer distance and the document's table (MOD127)."""

from __future__ import annotations

import struct
import sys
import tempfile
import unittest
from pathlib import Path

# numpy is in scripts/requirements.txt, not in a bare Python such as CI's: skipped there, as
# test_add_model.py's ONNX tests are, rather than failing the suite at import.
try:
    import numpy as np
except ModuleNotFoundError:
    raise unittest.SkipTest("numpy is not installed (scripts/requirements.txt)") from None

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

import compare_models as cm  # noqa: E402

CUBE = np.array([[0, 0, 0], [1, 0, 0], [1, 1, 0], [0, 1, 0], [0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]], dtype=float)
FACES = [(0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7), (0, 1, 5), (0, 5, 4), (1, 2, 6), (1, 6, 5), (2, 3, 7), (2, 7, 6), (3, 0, 4), (3, 4, 7)]


def cube(scale: float = 1.0, drop: int = 0) -> np.ndarray:
    return np.array([[CUBE[i] * scale for i in face] for face in FACES[drop:]])


def write_stl(path: Path, triangles: np.ndarray) -> None:
    with path.open("wb") as f:
        f.write(b"\0" * 80 + struct.pack("<I", len(triangles)))
        for t in triangles:
            f.write(struct.pack("<12fH", 0, 0, 0, *t.ravel(), 0))


class CompareModelsTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def test_an_stl_reads_back_as_its_triangles(self):
        write_stl(self.root / "cube.stl", cube())
        np.testing.assert_allclose(cube(), cm.read_stl(self.root / "cube.stl"))

    def test_a_cube_is_closed_and_one_with_a_face_missing_is_not(self):
        self.assertTrue(cm.closed(cube()))
        self.assertFalse(cm.closed(cube(drop=2)))

    def test_the_same_surface_is_near_zero_apart_and_a_bigger_one_is_not(self):
        self.assertLess(cm.chamfer(cube(), cube()), 2.0)
        self.assertGreater(cm.chamfer(cube(1.5), cube()), 5.0)

    def test_the_table_replaces_what_was_between_the_markers(self):
        doc = self.root / "models.md"
        doc.write_text("# Models\n\nText kept.\n", encoding="utf-8")
        row = {"photo": "robot.png", "model": "TripoSR fp32, CPU", "where": "CPU", "seconds": 14.2, "peak_mb": 4300, "triangles": 86524, "closed": True, "chamfer": None, "render": "robot.png"}
        cm.write_doc(doc, cm.table([row]))
        cm.write_doc(doc, cm.table([{**row, "model": "TripoSR fp16, GPU", "chamfer": 0.42}]))
        text = doc.read_text(encoding="utf-8")
        self.assertIn("Text kept.", text)
        self.assertEqual(1, text.count(cm.START))
        self.assertIn("| robot.png | TripoSR fp16, GPU | CPU | 14.2 s | 4.2 GB | 86,524 | yes | 0.42% | robot.png |", text)
        self.assertNotIn("TripoSR fp32, CPU", text)


if __name__ == "__main__":
    unittest.main()
