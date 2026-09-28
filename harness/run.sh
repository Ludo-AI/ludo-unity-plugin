#!/bin/sh
# Build the plugin against the Unity stubs and run the harness scenarios.
#
#   harness/run.sh                     working tree, fake API
#   harness/run.sh --ref v1.0.2        any git revision of LudoAIPlugin.cs
#   harness/run.sh --filter "audio|3D"  only matching scenarios (| = or)
#   LUDO_API_KEY=... harness/run.sh --live   real API (dev by default; spends credits)
set -e
here="$(cd "$(dirname "$0")" && pwd)"
ref=""
if [ "$1" = "--ref" ]; then ref="$2"; shift 2; fi

out="$(mktemp -d)"
if [ -n "$ref" ]; then
  src="$out/LudoAIPlugin.cs"
  git -C "$here/.." show "$ref:Assets/LudoAIPlugin/Editor/LudoAIPlugin.cs" > "$src"
  set -- "$@"
  build_args="-p:PluginFile=$src"
fi

dotnet build "$here/Plugin" -o "$out/plugin" $build_args -nologo -v q -clp:ErrorsOnly
dotnet build "$here/Runner" -nologo -v q -clp:ErrorsOnly
dotnet run --project "$here/Runner" --no-build -- --plugin "$out/plugin/LudoAIPlugin.Editor.dll" "$@"
