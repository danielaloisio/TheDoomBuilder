#!/usr/bin/env bash
# Builds the self-contained macOS packages (x64 and arm64, as a .app bundle) via package.sh.
#   tools/build_macos.sh [version]
set -euo pipefail
version="${1:-dev}"
root="$(cd "$(dirname "$0")/.." && pwd)"

for rid in osx-x64 osx-arm64; do
  "$root/tools/package.sh" "$rid" "$version"
done
