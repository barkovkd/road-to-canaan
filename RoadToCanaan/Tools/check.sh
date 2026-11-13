#!/bin/bash
# Fast compile check of the runtime scripts with Unity's own Roslyn (no editor launch, ~10 s).
# Needs Library/ScriptAssemblies from at least one editor run (setup.sh) for the package assemblies.
source "$(dirname "$0")/env.sh"
CSC="$SCRIPTING/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
DOTNET="$SCRIPTING/DotNetSdk/dotnet"
RSP="$PROJECT/Logs/check.rsp"
{
  echo "-nologo -target:library -nostdlib+ -noconfig -langversion:9.0 -nowarn:0169,0414,0649,0219,0168,0618,0162"
  echo "-define:UNITY_6000_0_OR_NEWER;ENABLE_INPUT_SYSTEM;UNITY_STANDALONE;UNITY_STANDALONE_OSX"
  echo "-out:\"$PROJECT/Logs/check.dll\""
  echo "-r:\"$SCRIPTING/NetStandard/ref/2.1.0/netstandard.dll\""
  for f in "$SCRIPTING/Managed/UnityEngine"/UnityEngine*.dll; do echo "-r:\"$f\""; done
  for n in Unity.InputSystem UnityEngine.UI Unity.RenderPipelines.Core.Runtime Unity.RenderPipelines.Universal.Runtime Unity.Mathematics Unity.Collections; do
    [ -f "$PROJECT/Library/ScriptAssemblies/$n.dll" ] && echo "-r:\"$PROJECT/Library/ScriptAssemblies/$n.dll\""
  done
  find "$PROJECT/Assets/Canaan/Scripts" -name "*.cs" | sed 's/.*/"&"/'
} > "$RSP"
"$DOTNET" "$CSC" "@$RSP" 2>&1 | sed "s|$PROJECT/Assets/Canaan/Scripts/||" | grep -E "error|warning CS8|Compilation" | head -${1:-60}
RES=${PIPESTATUS[0]}
"$TOOLS_DIR/check_files.sh" || RES=1
[ "$RES" = "0" ] && echo "check: OK"
exit $RES
