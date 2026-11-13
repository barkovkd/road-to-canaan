#!/bin/bash
# Builds Builds/Canaan.app. Prints compile errors and the build summary.
source "$(dirname "$0")/env.sh"
python3 "$TOOLS_DIR/make_shaders.py" "$EDITOR" "$PROJECT" > /dev/null || exit 1
"$TOOLS_DIR/check_files.sh" || exit 1
LOG="$PROJECT/Logs/build.log"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
  -executeMethod Canaan.EditorTools.CanaanBuild.Build -logFile "$LOG"
CODE=$?
grep -E "CANAAN_BUILD|error CS|Shader error|: error|Build completed|Error building" "$LOG" | sort -u | head -40
echo "build exit code: $CODE"
# A crashed player makes macOS ask about restoring windows on the next launch and blocks the run.
defaults write com.canaan.roadtocanaan ApplePersistenceIgnoreState -bool true 2>/dev/null
exit $CODE
