#!/usr/bin/env bash
# Neue Version veröffentlichen: baut die Fenster-App, packt sie mit Velopack (Windows) und lädt sie in die
# GitHub-Releases. Die installierten Apps holen sich das Update von dort selbst.
#   werkzeuge/release.sh            Version aus src/TrackingRecorder.App/TrackingRecorder.App.csproj
# Vorher die <Version> im csproj erhöhen. Braucht: dotnet 8, vpk (dotnet tool install -g vpk), gh (angemeldet).
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH" DOTNET_ROOT="$HOME/.dotnet" DOTNET_CLI_TELEMETRY_OPTOUT=1

V=$(grep -oP '(?<=<Version>)[^<]+' src/TrackingRecorder.App/TrackingRecorder.App.csproj)
REPO=https://github.com/casebasel/trackingrecorder
BUILD=${BUILD_DIR:-/tmp/trackingrecorder-release}
rm -rf "$BUILD"; mkdir -p "$BUILD"

echo "== Tests"; dotnet test -v q | tail -1
echo "== Version $V bauen"
dotnet publish src/TrackingRecorder.App -c Release -r win-x64 --self-contained -o "$BUILD/app" -v q
dotnet publish src/TrackingRecorder.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o "$BUILD/cli" -v q

echo "== Velopack"
vpk download github --channel win --repoUrl "$REPO" -o "$BUILD/releases" || true   # alte Version für Delta-Updates
vpk "[win]" pack -u TrackingRecorder -v "$V" -r win-x64 -p "$BUILD/app" -e TrackingRecorderApp.exe \
  --packTitle "TrackingRecorder" --packAuthors "casebasel" -o "$BUILD/releases"
vpk upload github --channel win --repoUrl "$REPO" --token "$(gh auth token)" -o "$BUILD/releases" \
  --publish --tag "v$V" --releaseName "v$V"
gh release upload "v$V" "$BUILD/cli/TrackingRecorder.exe" --repo casebasel/trackingrecorder --clobber
echo "== fertig: $REPO/releases/tag/v$V"
