#!/bin/bash
# A MonoBehaviour whose class name differs from its file name builds fine and then crashes the player
# at scene load. Fail early instead.
source "$(dirname "$0")/env.sh"
BAD=0
while IFS= read -r f; do
  base="$(basename "$f" .cs)"
  for cls in $(grep -oE "class +[A-Za-z0-9_]+ *: *(MonoBehaviour|ScriptableObject)" "$f" | awk '{print $2}'); do
    if [ "$cls" != "$base" ]; then echo "check_files: $cls is declared in $base.cs — move it to $cls.cs"; BAD=1; fi
  done
done < <(find "$PROJECT/Assets/Canaan/Scripts" -name "*.cs")
exit $BAD
