"""The Blender add-on's manifest meets extensions.blender.org's rules (MOD70).

Blender's own `extension validate` runs in blender\\tests\\run-blender-tests.cmd where a Blender
exists; this holds the same limits on any desk, plus what the listing promises: Windows only, the
site as the website, and network access declared because the cloud jobs leave the PC.
"""

from __future__ import annotations

import tomllib
import unittest
from pathlib import Path

MANIFEST = Path(__file__).resolve().parents[2] / "blender" / "modelado" / "blender_manifest.toml"
SHORT = 64  # Blender's limit for the tagline and each permission's reason


class BlenderManifestTests(unittest.TestCase):
    def setUp(self):
        self.manifest = tomllib.loads(MANIFEST.read_text(encoding="utf-8"))

    def assert_short_text(self, text: str, what: str) -> None:
        self.assertLessEqual(len(text), SHORT, f"{what} is over {SHORT} characters: {text!r}")
        self.assertNotIn(text[-1], ".!?,;:", f"{what} ends in punctuation: {text!r}")

    def test_tagline_fits_the_platform(self):
        self.assert_short_text(self.manifest["tagline"], "tagline")

    def test_every_permission_has_a_short_reason(self):
        permissions = self.manifest["permissions"]
        self.assertLessEqual(set(permissions), {"files", "network", "clipboard", "camera", "microphone"})
        for name, reason in permissions.items():
            self.assert_short_text(reason, f"permissions.{name}")

    def test_cloud_jobs_declare_the_network(self):
        self.assertIn("network", self.manifest["permissions"])
        self.assertIn("files", self.manifest["permissions"])

    def test_listed_for_windows_only(self):
        self.assertEqual(self.manifest["platforms"], ["windows-x64"])

    def test_website_is_the_site(self):
        self.assertEqual(self.manifest["website"], "https://alegauss.com/modelado/")

    def test_licence_is_spdx_gpl(self):
        self.assertEqual(self.manifest["license"], ["SPDX:GPL-3.0-or-later"])


if __name__ == "__main__":
    unittest.main()
