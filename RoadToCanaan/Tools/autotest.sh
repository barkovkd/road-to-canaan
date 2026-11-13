#!/bin/bash
# Runs the built player headless with a bot scenario.
#   autotest.sh <scenario> [outdir] [extra player args...]
# Writes report.txt (last line RESULT: PASS / RESULT: FAIL ...), player.log and screenshots into outdir.
source "$(dirname "$0")/env.sh"
SCEN="${1:?scenario}"
OUT="${2:-$PROJECT/Logs/test_$SCEN}"
shift; shift 2>/dev/null
rm -rf "$OUT"; mkdir -p "$OUT"
[ -x "$BIN" ] || { echo "no build at $BIN — run Tools/build.sh"; exit 1; }
if [ -n "$WINDOWED" ]; then
  # a real window at real speed, for frame-rate checks
  caffeinate -i "$BIN" -autotest "$SCEN" -outdir "$OUT" -logFile "$OUT/player.log" -realrender -screen-fullscreen 0 -screen-width 1600 -screen-height 1000 "$@" > /dev/null 2>&1
else
  caffeinate -i "$BIN" -batchmode -autotest "$SCEN" -outdir "$OUT" -logFile "$OUT/player.log" "$@" > /dev/null 2>&1
fi
CODE=$?
tail -${TAIL:-25} "$OUT/report.txt" 2>/dev/null || { echo "no report.txt; player.log tail:"; tail -30 "$OUT/player.log"; }
echo "exit code: $CODE  out: $OUT"
ls "$OUT" | tr '\n' ' '
echo
