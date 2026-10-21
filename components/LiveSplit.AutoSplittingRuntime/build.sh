#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/src/asr-capi"
cargo build --release --locked
mkdir -p ../../../../linux/native
cp target/release/libasr_capi.so ../../../../linux/native/
