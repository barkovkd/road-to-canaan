#!/bin/bash
# Rebuilds Assets/Canaan/Resources/Sounds from the recorded sources (Tools/sound/SOURCES.md lists every file, where
# it comes from, its licence and author). Blender runs in the background for its audio decoder and numpy.
#   Tools/sounds.sh <sources dir>          then Tools/setup.sh (import settings) and Tools/build.sh
source "$(dirname "$0")/env.sh"
SRC="${1:?usage: Tools/sounds.sh <sources dir>}"
"$BLENDER_APP/Contents/MacOS/Blender" -b --factory-startup --python-exit-code 1 \
    --python "$TOOLS_DIR/sound/make_sounds.py" -- "$SRC" "$PROJECT" 2>&1 | grep -v "^Blender quit\|^Read prefs\|^$"
exit "${PIPESTATUS[0]}"
