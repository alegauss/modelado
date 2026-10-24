"""TripoSR's model code, vendored for the ONNX export (MIT, see LICENSE).

Source: github.com/VAST-AI-Research/TripoSR at commit 107cefdc244c39106fa830359024f6a2f1c78871,
tsr/models and the part of tsr/utils.py they import. Changes: utils.py is trimmed to what the
models use, and the image tokenizer reads the DINO configuration from a local path. The
TSR system class is replaced by build() in scripts/export_models.py.
"""
