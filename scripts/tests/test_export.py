"""export_models.py's pieces that need no weights: framing, the grid order, the decoder rewrite.

These need torch (scripts/requirements.txt) and are skipped without it. The exports themselves
are checked against PyTorch by export_models.py every time it runs.
"""

from __future__ import annotations

import json
import sys
import unittest
from pathlib import Path

try:
    import numpy as np
    import torch

    sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
    import export_models as em
except ImportError:  # pragma: no cover - depends on the desk
    em = None


@unittest.skipIf(em is None, "needs scripts/requirements.txt (torch)")
class ExportPiecesTests(unittest.TestCase):
    def test_framing_centres_the_object_on_grey(self):
        rgba = np.zeros((100, 100, 4), dtype=np.uint8)
        rgba[10:30, 60:90] = (255, 0, 0, 255)
        x = em.composite_for_triposr(rgba)
        self.assertEqual((1, 3, 512, 512), x.shape)
        self.assertAlmostEqual(0.5, float(x[0, 0, 5, 5]), places=3)
        self.assertGreater(float(x[0, 0, 256, 256]), 0.95)
        self.assertLess(float(x[0, 1, 256, 256]), 0.05)

    def test_grid_points_run_x_slowest_like_the_apps_scalar_grid(self):
        points = em.grid_points(4)
        self.assertEqual((64, 3), points.shape)
        np.testing.assert_allclose(points[0], [-em.TRIPOSR_RADIUS] * 3)
        np.testing.assert_allclose(points[1], [-em.TRIPOSR_RADIUS, -em.TRIPOSR_RADIUS, -em.TRIPOSR_RADIUS / 3], rtol=1e-6)
        np.testing.assert_allclose(points[16][0], -em.TRIPOSR_RADIUS / 3, rtol=1e-6)

    def test_the_decoder_rewrite_matches_query_triplane(self):
        from omegaconf import OmegaConf

        _, _, _, _, mlp, renderer = em._triposr_modules()

        class Holder(torch.nn.Module):
            pass

        holder = Holder()
        holder.decoder = mlp(OmegaConf.create({"in_channels": 120, "n_neurons": 64, "n_hidden_layers": 9, "activation": "silu"}))
        holder.renderer = renderer(OmegaConf.create({"radius": em.TRIPOSR_RADIUS, "feature_reduction": "concat", "density_activation": "exp", "density_bias": -1.0}))
        torch.manual_seed(0)
        triplane = torch.randn(1, 3, 40, 64, 64)
        points = torch.rand(500, 3) * 1.6 - 0.8
        with torch.no_grad():
            density, color = em.Decoder(holder)(triplane, points)
            expected = holder.renderer.query_triplane(holder.decoder, points, triplane[0])
        torch.testing.assert_close(density, expected["density_act"][..., 0])
        torch.testing.assert_close(color, expected["color"])

    def test_u2net_input_is_rembgs_normalisation(self):
        rgb = np.full((40, 60, 3), 128, dtype=np.uint8)
        x = em.u2net_input(rgb)
        self.assertEqual((1, 3, 320, 320), x.shape)
        # Divided by the brightest sample (128), so every channel is (1 - mean) / std.
        self.assertAlmostEqual((1 - 0.485) / 0.229, float(x[0, 0, 100, 100]), places=4)

    def test_exported_meta_files_carry_what_the_app_reads(self):
        models = em.OUT
        metas = sorted(models.glob("*/meta.json"))
        if not metas:
            self.skipTest(f"nothing exported in {models}")
        for path in metas:
            with self.subTest(path.parent.name):
                meta = json.loads(path.read_text(encoding="utf-8"))
                self.assertEqual(em.META_SCHEMA, meta["schema"])
                self.assertIn("fp32", meta["variants"])
                for key in ("inputs", "outputs", "preprocess", "params", "upstream"):
                    self.assertIn(key, meta)
                self.assertEqual(2, len(meta["preprocess"]["value_range"]))


if __name__ == "__main__":
    unittest.main()
