#!/bin/bash
# One-off (and after settings changes): shaders, player settings, URP assets, materials, Boot scene.
source "$(dirname "$0")/env.sh"
python3 "$TOOLS_DIR/make_shaders.py" "$EDITOR" "$PROJECT" || exit 1
LOG="$PROJECT/Logs/setup.log"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
  -executeMethod Canaan.EditorTools.CanaanSetup.Run -logFile "$LOG"
CODE=$?
grep -E "CANAAN_SETUP|error CS|Shader error|: error" "$LOG" | sort -u | head -40
echo "setup exit code: $CODE"
exit $CODE
