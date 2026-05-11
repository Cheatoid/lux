#!/usr/bin/env bash
# Installer for the lux CLI on Linux and macOS.
#
# Detects the host OS + architecture, downloads the matching release archive
# from https://github.com/LuaLux/lux/releases/latest, extracts it to
# ~/.lux, and symlinks the binary into ~/.local/bin so `lux` works in any
# new shell. Honour these env vars to override:
#   LUX_INSTALL_DIR  - where to extract the archive (default ~/.lux)
#   LUX_BIN_DIR      - where the `lux` symlink goes  (default ~/.local/bin)
#   LUX_VERSION      - install a specific tag       (default: latest)
#
# Usage: curl -fsSL https://raw.githubusercontent.com/LuaLux/lux/main/scripts/install.sh | bash

set -euo pipefail

REPO="LuaLux/lux"
INSTALL_DIR="${LUX_INSTALL_DIR:-$HOME/.lux}"
BIN_DIR="${LUX_BIN_DIR:-$HOME/.local/bin}"
TAG="${LUX_VERSION:-latest}"

err() { printf '\033[31merror:\033[0m %s\n' "$*" >&2; exit 1; }
info() { printf '\033[36m==>\033[0m %s\n' "$*"; }

command -v curl  >/dev/null || err "curl is required."
command -v tar   >/dev/null || err "tar is required."

case "$(uname -s)" in
    Linux)  os_tag="linux"  ;;
    Darwin) os_tag="osx"    ;;
    *) err "Unsupported OS: $(uname -s) (only Linux and macOS are supported by this script)." ;;
esac

case "$(uname -m)" in
    x86_64|amd64)  arch_tag="x64"   ;;
    aarch64|arm64) arch_tag="arm64" ;;
    *) err "Unsupported architecture: $(uname -m)." ;;
esac

archive="lux-${os_tag}-${arch_tag}.tar.gz"

if [ "$TAG" = "latest" ]; then
    info "Resolving latest release tag..."
    TAG="$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
        | grep -m1 '"tag_name"' \
        | sed -E 's/.*"tag_name":[[:space:]]*"([^"]+)".*/\1/')"
    [ -n "$TAG" ] || err "Could not determine latest release tag."
fi

url="https://github.com/${REPO}/releases/download/${TAG}/${archive}"
info "Downloading ${archive} (${TAG})..."

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

curl -fSL --progress-bar -o "${tmp}/${archive}" "$url" \
    || err "Download failed: $url"

# Best-effort checksum verification (release ships a .sha256 sibling).
if curl -fsSL -o "${tmp}/${archive}.sha256" "${url}.sha256" 2>/dev/null; then
    info "Verifying checksum..."
    (cd "$tmp" && {
        if command -v sha256sum >/dev/null 2>&1; then
            sha256sum -c "${archive}.sha256" >/dev/null
        elif command -v shasum >/dev/null 2>&1; then
            shasum -a 256 -c "${archive}.sha256" >/dev/null
        fi
    }) || err "Checksum verification failed."
fi

info "Extracting to ${INSTALL_DIR}..."
mkdir -p "$INSTALL_DIR"
tar -xzf "${tmp}/${archive}" -C "$INSTALL_DIR"
chmod +x "${INSTALL_DIR}/lux"

info "Linking ${BIN_DIR}/lux -> ${INSTALL_DIR}/lux"
mkdir -p "$BIN_DIR"
ln -sf "${INSTALL_DIR}/lux" "${BIN_DIR}/lux"

# Add BIN_DIR to PATH in the user's shell rc if it isn't already exported.
case ":${PATH}:" in
    *":${BIN_DIR}:"*) ;;
    *)
        shell_name="$(basename "${SHELL:-bash}")"
        case "$shell_name" in
            bash) rc="$HOME/.bashrc" ;;
            zsh)  rc="${ZDOTDIR:-$HOME}/.zshrc" ;;
            *)    rc="" ;;
        esac

        if [ -n "$rc" ] && ! grep -qsF "$BIN_DIR" "$rc" 2>/dev/null; then
            {
                echo ""
                echo "# Added by lux installer"
                echo "export PATH=\"\$PATH:$BIN_DIR\""
            } >> "$rc"
            info "Added $BIN_DIR to PATH via $rc — restart your shell or run: source $rc"
        elif [ -z "$rc" ]; then
            info "Add $BIN_DIR to your PATH manually for your shell."
        fi
        ;;
esac

echo
info "lux ${TAG} installed."
info "Try: lux version"
