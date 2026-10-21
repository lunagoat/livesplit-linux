#!/bin/sh
# LiveSplit for Linux: user-local installer.
#
#   curl -fsSL https://raw.githubusercontent.com/lunagoat/livesplit-linux/linux-native/linux/install.sh | sh
#
# Installs to ~/.local (no root). Re-run to update. Remove with: install.sh --uninstall
#
# Env overrides:
#   LIVESPLIT_VERSION  release tag to install (default: latest)
#   LIVESPLIT_TARBALL  local tarball path to install instead of downloading
#   PREFIX             install prefix (default: ~/.local)
set -eu

REPO="lunagoat/livesplit-linux"
PREFIX="${PREFIX:-$HOME/.local}"
APP_DIR="$PREFIX/share/livesplit"
BIN_DIR="$PREFIX/bin"
APPS_DIR="$PREFIX/share/applications"
MIME_DIR="$PREFIX/share/mime"
ICON_DIR="$PREFIX/share/icons/hicolor"

say() { printf '==> %s\n' "$*"; }
die() { printf 'error: %s\n' "$*" >&2; exit 1; }

if [ "${1:-}" = "--uninstall" ]; then
    rm -rf "$APP_DIR"
    rm -f "$BIN_DIR/livesplit" "$BIN_DIR/livesplit-ctl" "$APPS_DIR/livesplit.desktop" \
          "$MIME_DIR/packages/livesplit.xml" \
          "$ICON_DIR/scalable/apps/livesplit.svg" "$ICON_DIR/256x256/apps/livesplit.png"
    command -v update-mime-database >/dev/null 2>&1 && update-mime-database "$MIME_DIR" >/dev/null 2>&1 || true
    command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS_DIR" >/dev/null 2>&1 || true
    say "Removed LiveSplit. Your splits and settings in ~/.config/LiveSplit are untouched."
    exit 0
fi

[ "$(uname -s)" = "Linux" ] || die "Linux only"
case "$(uname -m)" in
    x86_64|amd64) ;;
    *) die "only x86_64 builds are published (this is $(uname -m))" ;;
esac
command -v tar >/dev/null 2>&1 || die "tar is required"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

if [ -n "${LIVESPLIT_TARBALL:-}" ]; then
    [ -f "$LIVESPLIT_TARBALL" ] || die "no such file: $LIVESPLIT_TARBALL"
    cp "$LIVESPLIT_TARBALL" "$TMP/ls.tar.gz"
else
    if [ -n "${LIVESPLIT_VERSION:-}" ]; then
        URL="https://github.com/$REPO/releases/download/$LIVESPLIT_VERSION/livesplit-linux-x64.tar.gz"
    else
        URL="https://github.com/$REPO/releases/latest/download/livesplit-linux-x64.tar.gz"
    fi
    say "Downloading $URL"
    if command -v curl >/dev/null 2>&1; then
        curl -fL --progress-bar -o "$TMP/ls.tar.gz" "$URL" || die "download failed"
    elif command -v wget >/dev/null 2>&1; then
        wget -q --show-progress -O "$TMP/ls.tar.gz" "$URL" || die "download failed"
    else
        die "curl or wget is required"
    fi
fi

say "Extracting"
mkdir "$TMP/x"
tar -C "$TMP/x" -xzf "$TMP/ls.tar.gz"
[ -x "$TMP/x/livesplit/LiveSplit" ] || die "tarball does not contain livesplit/LiveSplit"

say "Installing to $APP_DIR"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR" "$BIN_DIR" "$APPS_DIR" "$MIME_DIR/packages" \
         "$ICON_DIR/scalable/apps" "$ICON_DIR/256x256/apps"
cp -r "$TMP/x/livesplit/." "$APP_DIR/"
chmod +x "$APP_DIR/LiveSplit"

# Launcher: the bundled runtime means no dotnet install is needed.
cat > "$BIN_DIR/livesplit" <<EOF
#!/bin/sh
exec "$APP_DIR/LiveSplit" "\$@"
EOF
chmod +x "$BIN_DIR/livesplit"

install -m755 "$APP_DIR/livesplit-ctl" "$BIN_DIR/livesplit-ctl"
sed "s|^Exec=.*|Exec=$BIN_DIR/livesplit %F|" "$APP_DIR/livesplit.desktop" > "$APPS_DIR/livesplit.desktop"
install -m644 "$APP_DIR/livesplit.xml" "$MIME_DIR/packages/livesplit.xml"
[ -f "$APP_DIR/res/Icon.svg" ] && install -m644 "$APP_DIR/res/Icon.svg" "$ICON_DIR/scalable/apps/livesplit.svg"
[ -f "$APP_DIR/res/Icon.png" ] && install -m644 "$APP_DIR/res/Icon.png" "$ICON_DIR/256x256/apps/livesplit.png"

command -v update-mime-database >/dev/null 2>&1 && update-mime-database "$MIME_DIR" >/dev/null 2>&1 || true
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database "$APPS_DIR" >/dev/null 2>&1 || true
command -v gtk-update-icon-cache >/dev/null 2>&1 && gtk-update-icon-cache -q "$ICON_DIR" >/dev/null 2>&1 || true

say "Installed LiveSplit. Run: livesplit"
case ":$PATH:" in
    *":$BIN_DIR:"*) ;;
    *) say "Note: $BIN_DIR is not on your PATH. Add it, or run $BIN_DIR/livesplit" ;;
esac
if [ -r /proc/sys/kernel/yama/ptrace_scope ] && [ "$(cat /proc/sys/kernel/yama/ptrace_scope)" != "0" ]; then
    say "Auto splitters read the game's memory; your kernel only allows that for processes LiveSplit started itself (ptrace_scope=$(cat /proc/sys/kernel/yama/ptrace_scope))."
    say "To attach to running games, run once: sudo setcap cap_sys_ptrace=ep $APP_DIR/LiveSplit"
    say "(or: sudo sysctl kernel.yama.ptrace_scope=0). See linux/README.md, 'Auto splitters'."
fi
command -v fc-list >/dev/null 2>&1 || say "Note: fontconfig not found; text rendering needs it."
