#!/usr/bin/env bash
# Builds a self-contained package of the editor for one runtime id.
#   tools/package.sh [rid] [version]      rid: linux-x64 (default), linux-arm64, win-x64, osx-x64, osx-arm64
# Output: artifacts/TheDoomBuilder-<version>-<rid>.tar.gz (zip for win-x64).
set -euo pipefail
rid="${1:-linux-x64}"
version="${2:-dev}"
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/artifacts/publish-$rid"
name="TheDoomBuilder-$version-$rid"

rm -rf "$out"
dotnet publish "$root/src/DoomBuilder.App" -c Release -r "$rid" --self-contained -o "$out/$name"
find "$out/$name" -name '*.pdb' -delete

mkdir -p "$root/artifacts"
case "$rid" in
  win-*) (cd "$out" && if command -v zip >/dev/null; then zip -qr "$root/artifacts/$name.zip" "$name"; else 7z a -tzip -bso0 "$root/artifacts/$name.zip" "$name"; fi) ;;
  *)     tar -C "$out" -czf "$root/artifacts/$name.tar.gz" "$name" ;;
esac
echo "Package: $root/artifacts/$name.*"
