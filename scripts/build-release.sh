#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

PROJECT="$REPO_ROOT/DataDock.Gui/DataDock.Gui.csproj"
CONFIG="Release"
FRAMEWORK="net8.0"
RIDS=("win-x64" "osx-x64" "osx-arm64" "linux-x64")
OUTPUT_DIR="$REPO_ROOT/artifacts"
VERSION_FILE="$REPO_ROOT/Directory.Build.props"

VERSION=$(dotnet msbuild -nologo "$PROJECT" -property:TargetFramework=$FRAMEWORK -target:GetAssemblyVersion 2>/dev/null | tail -n 1 || true)
VERSION=$(echo "$VERSION" | tr -d '[:space:]')

if [[ -z "$VERSION" && -f "$VERSION_FILE" ]]; then
  VERSION=$(sed -n 's|.*<VersionPrefix>\(.*\)</VersionPrefix>.*|\1|p' "$VERSION_FILE" | head -n 1)
  VERSION=$(echo "$VERSION" | tr -d '[:space:]')
fi

if [[ -z "$VERSION" ]]; then
  echo "Unable to determine version. Falling back to 0.0.0" >&2
  VERSION="0.0.0"
fi

rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

for rid in "${RIDS[@]}"; do
  echo "Publishing $PROJECT for $rid..."
  dotnet publish "$PROJECT" -c "$CONFIG" -r "$rid" --self-contained true \
    /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

  publish_dir="$REPO_ROOT/DataDock.Gui/bin/$CONFIG/$FRAMEWORK/$rid/publish"
  if [[ ! -d "$publish_dir" ]]; then
    echo "Publish directory not found: $publish_dir" >&2
    exit 1
  fi

  archive_name="DataDock-${rid}-v${VERSION}.zip"
  zip_target="$OUTPUT_DIR/$archive_name"
  (cd "$publish_dir" && zip -r "$zip_target" .)
  echo "Created ${zip_target#$REPO_ROOT/}"
done

echo "Artifacts ready under ${OUTPUT_DIR#$REPO_ROOT/}/"
