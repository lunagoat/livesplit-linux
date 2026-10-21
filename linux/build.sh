#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/linux/dist/livesplit"
NATIVE_DIR="$ROOT_DIR/linux/native"

SKIP_NATIVE=0
RUN_VERIFY=0

for arg in "$@"; do
    case "$arg" in
        --skip-native)
            SKIP_NATIVE=1
            ;;
        --verify)
            RUN_VERIFY=1
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo "  --skip-native    Skip rebuilding native Rust cdylib libraries"
            echo "  --verify         Run screenshot verification test after publishing"
            exit 0
            ;;
    esac
done

# Ensure DOTNET_ROOT is set if available
if [ -z "${DOTNET_ROOT:-}" ]; then
    if [ -d "$HOME/.dotnet" ]; then
        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$DOTNET_ROOT:$PATH"
    fi
fi

mkdir -p "$NATIVE_DIR"

if [ "$SKIP_NATIVE" -eq 0 ]; then
    echo "==> [1/3] Building liblivesplit_core.so..."
    bash "$ROOT_DIR/src/LiveSplit.Core/build_livesplit_core.sh"

    echo "==> [2/3] Building libasr_capi.so..."
    (
        cd "$ROOT_DIR/components/LiveSplit.AutoSplittingRuntime/src/asr-capi"
        cargo build --release
    )
    cp -f "$ROOT_DIR/components/LiveSplit.AutoSplittingRuntime/src/asr-capi/target/release/libasr_capi.so" "$NATIVE_DIR/libasr_capi.so"
else
    echo "==> Skipping native library builds (--skip-native)"
fi

echo "==> [3/3] Publishing LiveSplit.App (Release, linux-x64, framework-dependent)..."
mkdir -p "$OUTPUT_DIR"
dotnet publish "$ROOT_DIR/linux/LiveSplit.App/LiveSplit.App.csproj" \
    -c Release \
    -r linux-x64 \
    --no-self-contained \
    -o "$OUTPUT_DIR"

# Ensure native libraries are present next to executable
cp -f "$NATIVE_DIR/liblivesplit_core.so" "$OUTPUT_DIR/"
cp -f "$NATIVE_DIR/libasr_capi.so" "$OUTPUT_DIR/"

# Ensure Components folder is fully populated
mkdir -p "$OUTPUT_DIR/Components"
if [ -d "$ROOT_DIR/artifacts/build/release/Components" ]; then
    cp -f "$ROOT_DIR/artifacts/build/release/Components"/*.dll "$OUTPUT_DIR/Components/" 2>/dev/null || true
fi
# Remove host assemblies from Components if present
rm -f "$OUTPUT_DIR/Components/LiveSplit.Core.dll" \
      "$OUTPUT_DIR/Components/LiveSplit.Drawing.dll" \
      "$OUTPUT_DIR/Components/SpeedrunComSharp.dll"

# Copy resource icons into dist
mkdir -p "$OUTPUT_DIR/res"
cp -r "$ROOT_DIR/res/"* "$OUTPUT_DIR/res/"
mkdir -p "$OUTPUT_DIR/Resources"
cp -r "$ROOT_DIR/res/"* "$OUTPUT_DIR/Resources/"

echo "==> Build complete: $OUTPUT_DIR"

if [ "$RUN_VERIFY" -eq 1 ]; then
    echo "==> Verifying published build..."
    TMP_CONFIG="$(mktemp -d /tmp/livesplit-verify-cfg-XXXXXX)"
    TMP_PNG="$(mktemp /tmp/livesplit-verify-XXXXXX.png)"
    trap 'rm -rf "$TMP_CONFIG" "$TMP_PNG"' EXIT

    timeout 8 "$OUTPUT_DIR/LiveSplit" \
        --screenshot "$TMP_PNG" \
        --screenshot-demo \
        --no-server \
        -s "$ROOT_DIR/lib/livesplit-core/tests/run_files/Celeste - Any% (1.2.1.5).lss" \
        -l "$ROOT_DIR/src/LiveSplit.View/Resources/DefaultLayout.lsl" \
        --config-dir "$TMP_CONFIG"

    if [ -s "$TMP_PNG" ]; then
        PNG_SIZE="$(stat -c%s "$TMP_PNG")"
        echo "==> Verification successful! Screenshot generated: $TMP_PNG ($PNG_SIZE bytes)"
    else
        echo "==> Verification FAILED: output PNG is missing or empty." >&2
        exit 1
    fi
fi
