#!/bin/bash
# Daryva - macOS Apple Silicon build and Velopack pack script
# Prerequisites: .NET 8 SDK, vpk tool (dotnet tool install -g vpk)
# Output: ../artifacts/osx-arm64, ../releases/

set -e

VERSION="${1:-1.0.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/Daryva-Avalonia"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"
RELEASES_DIR="$REPO_ROOT/releases"
PUBLISH_DIR="$ARTIFACTS_DIR/osx-arm64"

echo "Building Daryva for macOS (osx-arm64) v$VERSION..."

# Publish
dotnet publish "$PROJECT_DIR" -c Release -r osx-arm64 --self-contained -o "$PUBLISH_DIR" -p:Version="$VERSION"

# Velopack pack (with welcome, license, conclusion pages)
INSTALLER_ASSETS="$SCRIPT_DIR/installer-assets"
VPK_ARGS=(
  --packId FBGHaider.Daryva
  --packVersion "$VERSION"
  --packDir "$PUBLISH_DIR"
  --mainExe Daryva
  --outputDir "$RELEASES_DIR"
  --packTitle "Daryva"
)
[[ -f "$INSTALLER_ASSETS/welcome.rtf" ]] && VPK_ARGS+=(--pkgWelcome "$INSTALLER_ASSETS/welcome.rtf")
[[ -f "$INSTALLER_ASSETS/terms.rtf" ]] && VPK_ARGS+=(--pkgLicense "$INSTALLER_ASSETS/terms.rtf")
[[ -f "$INSTALLER_ASSETS/conclusion.rtf" ]] && VPK_ARGS+=(--pkgConclusion "$INSTALLER_ASSETS/conclusion.rtf")

echo "Packaging with Velopack..."
vpk pack "${VPK_ARGS[@]}"

echo "Done. Output: $RELEASES_DIR"
