#!/bin/bash
# Daryva - macOS Intel build and Velopack pack script
# Prerequisites: .NET 8 SDK, vpk tool (dotnet tool install -g vpk)
# Output: ../artifacts/osx-x64, ../releases/

set -e

VERSION="${1:-1.0.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/Daryva-Avalonia"
ARTIFACTS_DIR="$REPO_ROOT/artifacts"
RELEASES_DIR="$REPO_ROOT/releases"
PUBLISH_DIR="$ARTIFACTS_DIR/osx-x64"

echo "Building Daryva for macOS (osx-x64) v$VERSION..."

# Publish
dotnet publish "$PROJECT_DIR" -c Release -r osx-x64 --self-contained -o "$PUBLISH_DIR" -p:Version="$VERSION"

# Velopack pack
echo "Packaging with Velopack..."
vpk pack --packId FBGHaider.Daryva --packVersion "$VERSION" --packDir "$PUBLISH_DIR" --mainExe Daryva --outputDir "$RELEASES_DIR"

echo "Done. Output: $RELEASES_DIR"
