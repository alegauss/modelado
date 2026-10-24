#!/usr/bin/env bash
# run-tests.cmd for Linux and macOS: everything but the WPF app, which only builds here.
set -euo pipefail
cd "$(dirname "$0")"
config="${1:-Debug}"
dotnet build Modelado.slnx --configuration "$config" --nologo -v quiet
failed=0
for project in tests/*.Tests; do
  name="$(basename "$project")"
  echo; echo "=== $name ==="
  "$project/bin/$config/net10.0/$name" || failed=$((failed + 1))
done
echo; echo "=== scripts/tests (Python) ==="
py=python3; [ -x .venv/bin/python ] && py=.venv/bin/python
"$py" -W ignore::DeprecationWarning -m unittest discover -s scripts/tests || failed=$((failed + 1))
[ "$failed" -eq 0 ] && echo "All suites passed." || { echo "$failed suite(s) failed."; exit 1; }
