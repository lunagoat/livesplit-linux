#!/bin/sh
set -e
cd "$(dirname "$0")/../../lib/livesplit-core"
FEATURES=parsing,image-shrinking
BINDINGS_DIR="$(pwd)/target/livesplit-bindings"
NATIVE_DIR="$(pwd)/../../linux/native"

cargo rustc --profile max-opt -p livesplit-core-capi --crate-type cdylib --no-default-features --features "$FEATURES"
mkdir -p "$NATIVE_DIR"
cp target/max-opt/liblivesplit_core.so "$NATIVE_DIR/liblivesplit_core.so"

if [ "$1" = "--bindings" ]; then
    cd capi/bind_gen
    cargo run -- --no-default-features --features "$FEATURES" --output-dir "$BINDINGS_DIR"
    cp "$BINDINGS_DIR/LiveSplitCore.cs" ../../../../src/LiveSplit.Core/LiveSplitCore.g.cs
fi
