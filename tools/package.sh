#!/usr/bin/env bash
# Builds a self-contained package of the editor for one runtime id.
#   tools/package.sh [rid] [version]      rid: linux-x64 (default), linux-arm64, win-x64, osx-x64, osx-arm64
# Output: build/TheDoomBuilder-<version>-<rid>.tar.gz (zip for win-x64).
# osx-*: the publish output is wrapped in a TheDoomBuilder.app bundle (Info.plist) before taring, so the macOS menu
# bar shows "TheDoomBuilder" instead of Avalonia's fallback name ("Avalonia Application", shown when there is no
# bundle). No code signing: Apple Silicon still runs it (dotnet ad-hoc signs osx-arm64 builds on publish); only
# distributing outside your own Mac without a Gatekeeper warning needs a paid Developer ID + notarization.
set -euo pipefail
rid="${1:-linux-x64}"
version="${2:-dev}"
root="$(cd "$(dirname "$0")/.." && pwd)"
out="$root/build/publish-$rid"
name="TheDoomBuilder-$version-$rid"
publishedexe="TheDoomBuilder.Application"
exe="TheDoomBuilder"

rm -rf "$out"
dotnet publish "$root/src/TheDoomBuilder.Application" -c Release -r "$rid" --self-contained -o "$out/$name"
find "$out/$name" -name '*.pdb' -delete

# Cosmetic rename for the end user: the assembly is "TheDoomBuilder.Application" (what plugins/InternalsVisibleTo know it as),
# only the apphost executable on disk is renamed.
suffix=""; case "$rid" in win-*) suffix=".exe" ;; esac
mv "$out/$name/$publishedexe$suffix" "$out/$name/$exe$suffix"

mkdir -p "$root/build"
case "$rid" in
  win-*)
    (cd "$out" && if command -v zip >/dev/null; then zip -qr "$root/build/$name.zip" "$name"; else 7z a -tzip -bso0 "$root/build/$name.zip" "$name"; fi)
    ;;
  osx-*)
    bundle="$out/TheDoomBuilder.app"
    mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
    (shopt -s dotglob; mv "$out/$name"/* "$bundle/Contents/MacOS/")
    rm -rf "$out/$name"
    chmod +x "$bundle/Contents/MacOS/$exe"
    cp "$root/tools/icons/AppIcon.icns" "$bundle/Contents/Resources/AppIcon.icns"
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
	<key>CFBundleIconFile</key>
	<string>AppIcon</string>
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
    (cd "$out" && tar -czf "$root/build/$name.tar.gz" "TheDoomBuilder.app")
    ;;
  *)
    cp "$root/src/TheDoomBuilder.Application/Resources/AppIcon.png" "$out/$name/AppIcon.png"
    cat > "$out/$name/install-desktop-entry.sh" <<'SCRIPT'
#!/usr/bin/env bash
# Registers TheDoomBuilder in the desktop's application menu (GNOME/KDE/...), pointing at this
# extracted folder. Run once after extracting the package; run again if you move the folder.
set -euo pipefail
dir="$(cd "$(dirname "$0")" && pwd)"
apps="$HOME/.local/share/applications"
mkdir -p "$apps"
cat > "$apps/thedoombuilder.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=The Doom Builder
Comment=Doom map editor
Exec=$dir/TheDoomBuilder %f
Icon=$dir/AppIcon.png
Terminal=false
Categories=Development;Game;
DESKTOP
echo "Installed $apps/thedoombuilder.desktop (pointing at $dir)"
SCRIPT
    chmod +x "$out/$name/install-desktop-entry.sh"
    tar -C "$out" -czf "$root/build/$name.tar.gz" "$name"
    ;;
esac
echo "Package: $root/build/$name.*"
