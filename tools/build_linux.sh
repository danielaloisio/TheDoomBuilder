#!/usr/bin/env bash
# Builds the self-contained Linux packages (x64 and arm64) via package.sh.
#   tools/build_linux.sh [version]
set -euo pipefail
version="${1:-dev}"
root="$(cd "$(dirname "$0")/.." && pwd)"

for rid in linux-x64 linux-arm64; do
  "$root/tools/package.sh" "$rid" "$version"
done
