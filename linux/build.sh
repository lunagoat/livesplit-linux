#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
OUTPUT_DIR="$ROOT_DIR/linux/dist/livesplit"
NATIVE_DIR="$ROOT_DIR/linux/native"

SKIP_NATIVE=0
RUN_VERIFY=0
SELF_CONTAINED=0
MAKE_TARBALL=0

for arg in "$@"; do
    case "$arg" in
        --skip-native)
            SKIP_NATIVE=1
            ;;
        --verify)
            RUN_VERIFY=1
            ;;
        --self-contained)
            SELF_CONTAINED=1
            ;;
        --tarball)
            SELF_CONTAINED=1
            MAKE_TARBALL=1
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo "  --skip-native    Skip rebuilding native Rust cdylib libraries"
            echo "  --verify         Run screenshot verification test after publishing"
            echo "  --self-contained Bundle the .NET runtime (no dotnet install needed)"
            echo "  --tarball        Implies --self-contained; also writes linux/dist/livesplit-linux-x64.tar.gz"
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

if [ "$SELF_CONTAINED" -eq 1 ]; then
    SC_FLAG="--self-contained"
    echo "==> [3/3] Publishing LiveSplit.App (Release, linux-x64, self-contained)..."
else
    SC_FLAG="--no-self-contained"
    echo "==> [3/3] Publishing LiveSplit.App (Release, linux-x64, framework-dependent)..."
fi
mkdir -p "$OUTPUT_DIR"
dotnet publish "$ROOT_DIR/linux/LiveSplit.App/LiveSplit.App.csproj" \
    -c Release \
    -r linux-x64 \
    $SC_FLAG \
    -o "$OUTPUT_DIR"

# Windows auto-splitter compatibility: Win32 shim (.so) + generated WinForms stub assembly.
if [ "$SKIP_NATIVE" -eq 0 ] || [ ! -f "$NATIVE_DIR/libwin32shim.so" ]; then
    echo "==> Building libwin32shim.so..."
    (cd "$ROOT_DIR/linux/win32shim" && cargo build --release)
    cp -f "$ROOT_DIR/linux/win32shim/target/release/libwin32shim.so" "$NATIVE_DIR/libwin32shim.so"
fi
cp -f "$NATIVE_DIR/libwin32shim.so" "$OUTPUT_DIR/"

WINFORMS_REF_VERSION="10.0.13"
REF_DIR="$NATIVE_DIR/winforms-ref"
if [ ! -f "$REF_DIR/System.Windows.Forms.dll" ]; then
    echo "==> Fetching WinForms reference assemblies ($WINFORMS_REF_VERSION)..."
    mkdir -p "$REF_DIR"
    REF_TMP="$(mktemp -d)"
    curl -fsSL -o "$REF_TMP/ref.nupkg" \
        "https://api.nuget.org/v3-flatcontainer/microsoft.windowsdesktop.app.ref/$WINFORMS_REF_VERSION/microsoft.windowsdesktop.app.ref.$WINFORMS_REF_VERSION.nupkg"
    unzip -q -o -j "$REF_TMP/ref.nupkg" \
        'ref/net10.0/System.Windows.Forms.dll' 'ref/net10.0/System.Windows.Forms.Primitives.dll' 'ref/net10.0/System.Drawing.Common.dll' \
        -d "$REF_DIR"
    rm -rf "$REF_TMP"
fi
echo "==> Generating LiveSplit.Compat.WinForms stub..."
STUB_TMP="$(mktemp -d)"
dotnet run --project "$ROOT_DIR/linux/tools/WinFormsStubGen" -c Release -- \
    --ref-dir "$REF_DIR" \
    --core "$OUTPUT_DIR/LiveSplit.Core.dll" \
    --drawing "$OUTPUT_DIR/LiveSplit.Drawing.dll" \
    --out "$STUB_TMP"
# The System.Windows.Forms / System.Drawing.Common forwarders resolve references from assemblies a component
# loads itself (e.g. Costura-embedded). Never copy the generated System.Drawing.dll: the runtime ships its own.
STUB_FILES=("$STUB_TMP/LiveSplit.Compat.WinForms.dll" "$STUB_TMP/LiveSplit.Compat.WinForms.Support.dll" "$STUB_TMP/LiveSplit.View.dll" "$STUB_TMP/System.Windows.Forms.dll" "$STUB_TMP/System.Drawing.Common.dll")
cp -f "${STUB_FILES[@]}" "$OUTPUT_DIR/"
cp -f "${STUB_FILES[@]}" "$NATIVE_DIR/"
rm -rf "$STUB_TMP"

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

if [ "$MAKE_TARBALL" -eq 1 ]; then
    # Tarball layout: livesplit/{LiveSplit,...} plus the files install.sh needs.
    cp -f "$SCRIPT_DIR/packaging/livesplit-ctl" "$SCRIPT_DIR/packaging/livesplit.desktop" \
          "$SCRIPT_DIR/packaging/livesplit.xml" "$OUTPUT_DIR/"
    cp -f "$ROOT_DIR/LICENSE" "$OUTPUT_DIR/LICENSE"
    TARBALL="$ROOT_DIR/linux/dist/livesplit-linux-x64.tar.gz"
    tar -C "$ROOT_DIR/linux/dist" -czf "$TARBALL" livesplit
    echo "==> Tarball: $TARBALL ($(du -h "$TARBALL" | cut -f1))"
fi

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
