#!/usr/bin/env bash
# Regenerates the source tree from a NemesisMod.dll:
#   ILSpy -> postdecompile.py fixes -> recover layout -> split into partial files.
#
# usage: tools/decompile.sh <NemesisMod.dll> <out-dir> <ref-dir>...
#   ref-dir  directories holding ScriptHookRDRNetAPI.dll, Newtonsoft.Json.dll and
#            the .NET Framework 4.8 reference assemblies (ILSpy resolves types
#            against them; without net48 refs WinForms Keys and tuple names
#            come out wrong)
#
# Writes <out-dir>/src and <out-dir>/Resources, laid out like this project.
# Pinned to ilspycmd 9.1.0.7988: another ILSpy version may need new fixes in
# postdecompile.py.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
dll="$(realpath "$1")"; out="$(realpath -m "$2")"; shift 2
export DOTNET_ROLL_FORWARD=Major   # ilspycmd 9.1 targets .NET 8; run it on whatever runtime is installed

tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/refs"
for d in "$@"; do d="$(realpath "$d")"
  for sub in "$d" "$d/Facades"; do
    [ -d "$sub" ] && find "$sub" -maxdepth 1 -name '*.dll' -exec ln -sf {} "$tmp/refs/" \;
  done
done

"${ILSPYCMD:-$HOME/.dotnet/tools/ilspycmd}" -p -o "$tmp/raw" -r "$tmp/refs" --nested-directories --disable-updatecheck "$dll" >/dev/null

rm -rf "$out/src" "$out/Resources"
mkdir -p "$out/src" "$out/Resources"
cp -r "$tmp/raw/NemesisSystem" "$tmp/raw/Properties" "$out/src/"
cp "$tmp/raw/doors.json" "$out/Resources/"
python3 -I "$here/postdecompile.py" "$out/src" >/dev/null

run() { dotnet "$here/$1/bin/Release/net10.0/$1.dll" "${@:2}"; }
run Layout "$dll" NemesisSystem.NemesisMod > "$tmp/layout.tsv"
run Layout "$dll" NemesisSystem.NemesisMod --cctor-deps > "$tmp/cctor-deps.tsv"
if grep -q 'read before assigned' "$tmp/cctor-deps.tsv"; then echo "static init reads a field before it is set:" >&2; cat "$tmp/cctor-deps.tsv" >&2; exit 1; fi
run Splitter "$out/src/NemesisSystem/NemesisMod.cs" "$tmp/layout.tsv" "$tmp/cctor-deps.tsv" "$here/Splitter/nemesismod.map" "$out/src/NemesisSystem/NemesisMod"
rm "$out/src/NemesisSystem/NemesisMod.cs"
