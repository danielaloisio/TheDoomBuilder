#!/usr/bin/env bash
# Builds a self-contained package of the editor for one runtime id.
#   tools/package.sh [rid] [version]      rid: linux-x64 (default), linux-arm64, win-x64, osx-x64, osx-arm64
# Output: artifacts/TheDoomBuilder-<version>-<rid>.tar.gz (zip for win-x64).
# osx-*: the publish output is wrapped in a TheDoomBuilder.app bundle (Info.plist) before taring, so the macOS menu
# bar shows "TheDoomBuilder" instead of Avalonia's fallback name ("Avalonia Application", shown when there is no
# bundle). No code signing: Apple Silicon still runs it (dotnet ad-hoc signs osx-arm64 builds on publish); only
# distributing outside your own Mac without a Gatekeeper warning needs a paid Developer ID + notarization.
set -euo pipefail
rid="${1:-linux-x64}"
version="${2:-dev}"
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/artifacts/publish-$rid"
name="TheDoomBuilder-$version-$rid"
exe="DoomBuilder.App"

rm -rf "$out"
dotnet publish "$root/src/DoomBuilder.App" -c Release -r "$rid" --self-contained -o "$out/$name"
find "$out/$name" -name '*.pdb' -delete

mkdir -p "$root/artifacts"
case "$rid" in
  win-*)
    (cd "$out" && if command -v zip >/dev/null; then zip -qr "$root/artifacts/$name.zip" "$name"; else 7z a -tzip -bso0 "$root/artifacts/$name.zip" "$name"; fi)
    ;;
  osx-*)
    bundle="$out/TheDoomBuilder.app"
    mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
    (shopt -s dotglob; mv "$out/$name"/* "$bundle/Contents/MacOS/")
    rm -rf "$out/$name"
    chmod +x "$bundle/Contents/MacOS/$exe"
    cat > "$bundle/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleName</key>
	<string>TheDoomBuilder</string>
	<key>CFBundleDisplayName</key>
	<string>TheDoomBuilder</string>
	<key>CFBundleIdentifier</key>
	<string>io.github.danielaloisio.thedoombuilder</string>
	<key>CFBundleVersion</key>
	<string>$version</string>
	<key>CFBundleShortVersionString</key>
	<string>$version</string>
	<key>CFBundlePackageType</key>
	<string>APPL</string>
	<key>CFBundleExecutable</key>
	<string>$exe</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>LSMinimumSystemVersion</key>
	<string>11.0</string>
	<key>NSHighResolutionCapable</key>
	<true/>
	<key>NSPrincipalClass</key>
	<string>NSApplication</string>
</dict>
</plist>
PLIST
    (cd "$out" && tar -czf "$root/artifacts/$name.tar.gz" "TheDoomBuilder.app")
    ;;
  *)
    tar -C "$out" -czf "$root/artifacts/$name.tar.gz" "$name"
    ;;
esac
echo "Package: $root/artifacts/$name.*"
