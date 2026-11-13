#!/bin/bash
# Regenerates the surface and terrain textures (and SurfaceTable.cs). Run Tools/setup.sh afterwards.
#   textures.sh [name ...]
source "$(dirname "$0")/env.sh"
# the grass atlas is its own picture: "textures.sh grass" redraws only that
if [ "$1" != "grass" ] && [ "$1" != "clouds" ]; then "$BPY" "$TOOLS_DIR/textures/make_textures.py" "$PROJECT" "$@" || exit 1; fi
if [ $# -eq 0 ] || [ "$1" = "grass" ]; then "$BPY" "$TOOLS_DIR/textures/make_grass.py" "$PROJECT"; fi
# the clouds' 3D noise: "textures.sh clouds" makes only that
if [ $# -eq 0 ] || [ "$1" = "clouds" ]; then "$BPY" "$TOOLS_DIR/textures/make_clouds.py" "$PROJECT"; fi
