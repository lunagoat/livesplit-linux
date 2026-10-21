#!/usr/bin/env python3
"""Audit the release library with binutils; run after cargo build --release."""
from pathlib import Path
import subprocess

root = Path(__file__).resolve().parents[1]
expected = set((root / "exports.txt").read_text().split())
library = root / "target/release/libwin32shim.so"
output = subprocess.check_output(["nm", "-D", "--defined-only", str(library)], text=True)
actual = {line.split()[-1] for line in output.splitlines() if line.split()}
missing = expected - actual
extra = actual - expected
if missing:
    raise SystemExit("Missing exports: " + ", ".join(sorted(missing)))
print(f"PASS: all {len(expected)} required names and variants exported ({len(actual)} total).")
if extra:
    print("Additional exports: " + ", ".join(sorted(extra)))
