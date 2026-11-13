#!/bin/bash
# Shared paths for the tool scripts.
TOOLS_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$(dirname "$TOOLS_DIR")"
EDITOR="/Applications/Unity/Hub/Editor/6000.6.7f1"
UNITY="$EDITOR/Unity.app/Contents/MacOS/Unity"
SCRIPTING="$EDITOR/Unity.app/Contents/Resources/Scripting"
APP="$PROJECT/Builds/Canaan.app"
BIN="$APP/Contents/MacOS/RoadToCanaan"
mkdir -p "$PROJECT/Logs"
# Blender's bundled Python (numpy + OpenImageIO): used by the texture and contact-sheet tools
BLENDER_APP="$HOME/Applications/Blender.app"
BPY="$(ls "$BLENDER_APP"/Contents/Resources/*/python/bin/python3* 2>/dev/null | head -1)"
