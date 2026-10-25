# How this source was reconstructed, and how far to trust it

## Input

| Artifact | SHA-256 |
|---|---|
| `NemesisMod.dll` 0.9.5.3 (official Nexus Mods main file) | `2ad902dd6d2e3ea9e1afeddb8d6c211f0edb85b6f23ce03918bbbc5e4f736831` |
| MVID | `5bf605a9-24e4-409c-be7e-c7ed55198d9d` |
| `ScriptHookRDRNetAPI.dll` built against (2.2.0.0, ScriptHookRDR2DotNet V2 2.2.0 release) | `bfb7607d94b2de7f8530289377f163caad81098afc1fb7ee86191810a6cc6bba` |

Static metadata inspection, which never loads or runs the assembly:
pure IL (no ReadyToRun or native code), not obfuscated, not strong-named, .NET Framework 4.8,
x64. 1297 types, 10493 methods, 9086 fields. References: mscorlib, System, System.Core,
System.Windows.Forms, Newtonsoft.Json 13, ScriptHookRDRNetAPI 2.2.0. 16 P/Invokes
(kernel32 memory info, XInput, user32 cursor and window). One embedded resource, `doors.json`.
Informational version `0.9.5.3+01e530aebf007909b0ddc220a14611a05d541e02`, the author's git commit.

## Pipeline (`make decompile`, `tools/decompile.sh`)

1. **ILSpy** (`ilspycmd` 9.1.0.7988) decompiles to a project, resolving types against the
   .NET Framework 4.8 reference assemblies. Without them, WinForms `Keys` turns into `int` and
   tuple element names are lost (178 compile errors).
2. **`tools/postdecompile.py`** makes it compile and match:
   - restores tuple element names ILSpy dropped from 15 local declarations and 8 explicit LINQ
     type arguments;
   - restores two local functions the original declares but never calls
     (`RosterMenu.DrawDetail` → `Meter`, `Traits.FromModel` → `Has`), rebuilt from their IL;
   - removes `[module: RefSafetyRules(11)]` (the compiler emits it).
3. **`tools/Layout`** recovers how the `NemesisMod` class was laid out in source. Roslyn bakes
   each method's position in its type's member list into compiler-generated names
   (`<M>b__ORD_k` lambdas, `<M>g__F|ORD_k` local functions, `<M>d__ORD` iterators,
   `<>c__DisplayClassORD_k` closures). That gives 784 of 2258 methods an exact position. From
   position minus method index, you get how many fields, properties and nested types were
   declared before each anchored method. The count rises monotonically across all 784 anchors
   (once overloaded names are excluded) and ends at 1844, against the 1842 non-method members
   the type actually has. Read in order, the methods form topic runs in alphabetical order
   (Affiliation, Agenda, Ambush, Army, Bands, Calm, Camp…, Cards, …, World, WorldSim). That is
   what you'd see if the author compiled partial files named `NemesisMod.<Topic>.cs`, with the
   main `NemesisMod.cs` (constructor, `OnTick`) between Crime and Debug, where a
   case-insensitive sort puts it.
4. **`tools/Splitter`** (Roslyn) cuts the class into those files:
   `tools/Splitter/nemesismod.map` names each file's first method. Fields and nested types keep
   declaration order and are cut into the same file sequence by a dynamic program. It
   maximises how often a file uses what it declares, held to the field-block counts from step 3.
   1220 of 1657 fields land in a file that uses them; the rest are shared state or inlined
   `const`s. The 252 assignments ILSpy gathered into a synthesized static constructor go back
   onto their field declarations, as the original evidently had them: the original static
   constructor body is a single `StartupTrace.Write("static init done")`.

File names and boundaries are a reconstruction. The order, and the topics each file covers, are
recovered from the DLL; the names are mine. The original had about 150 to 300 partial files
(318 field blocks), so some of these files merge several of the author's.

## Verification

`make verify` rebuilds and compares against the shipped DLL with `tools/AsmDiff`. It reads
metadata only and never loads either assembly.

- **Surface: identical.** 10,469 types, fields, methods, properties and events, with signatures
  and visibility.
- **Bodies: per user-written method (lambdas, local functions and state machines folded into
  their owner), the multiset of calls, field accesses and string literals.** 4940 of 4952
  match. The 12 that differ are all compiler-version lowering, recorded in
  `tools/asmdiff-baseline.txt`:
  - 8 `switch`es on strings: newer Roslyn dispatches on `Length` / `get_Chars` before comparing
    (`InterroChips`, `InterroNature`, `KindColor`, `ReactByTemper`, `RunWorldEventAt`,
    `OutcomeColor`, `Theme.Single`, `Traits.KinWeight`);
  - 2 empty `case` labels whose jump target equals `default` (`case "faith":` in `Mind.Roll`,
    `case "lemoyneraiders":` in `PickAgenda`): the newer compiler drops the redundant compare;
  - closure allocation (`Object..ctor` counts) in `NemesisMod` and `NemesisSettings`.
- **The split is a pure refactor:** the build before and after splitting compares with zero
  differences.
- **Static init order:** `tools/Layout --cctor-deps` lists the 13 static fields that read
  another during initialization, transitively through calls such as `D(...)`. In the shipped DLL
  and in this build, every one reads an already-initialized field. `make check` enforces it.
- **The pipeline is reproducible:** `make decompile` regenerates `src/` byte for byte.

The comparison proves the same members call the same things with the same literals. It does not
prove identical control flow: `AsmDiff` compares multisets, not order or branches. The game is
the gate for that, and the build has passed an in-game smoke test (2026-10-25: Steam build under
Proton, Script Hook RDR2 1.0.1491.17, ScriptHookRDR2DotNet V2 2.2.0). The test loaded an existing
Story save that had been running the official 0.9.5.3 DLL, then played for about five minutes:

- Startup ran the same stages in the same order as the official DLL (`startup.log`). The API
  check passed, `SELFCHECK ok - 7 checks`, and every `ALIVE` heartbeat reported `errors 0`.
- The F11 menu opened and drew its leader pages and a history page. A fist fight ran the full
  nerve → peace → law-group → death chain. Greetings raised like scores, with gains in the same
  range as under the official DLL. A game save produced a mod save point. Arriving in a town
  triggered the town and reputation logic.
- `nemeses.json`, `world.json`, `people.json` and `fingerprints2.json` were written with the
  same schema they had before. The only changes were the people met in the session and strangers
  aged out by `LookMemoryDays`.
- The game quit cleanly: no crash dump, and Steam logged a normal stop.

That covers startup, the main tick, the menu, combat, social and persistence. It is not a
playthrough: wars, camps, nemesis encounters and the interrogation and telegram flows didn't
come up in five minutes.

## When enRAGE updates

```sh
make decompile UPSTREAM_DLL=/path/to/new/NemesisMod.dll   # -> build/upstream-src/
```

New methods inherit the file of whatever precedes them. A new feature file shows up as a new
alphabetical topic run, which you name with a line in `nemesismod.map`. `postdecompile.py`
fails loudly on any fix that no longer applies. Keep pristine upstream on its own git branch
and rebase your changes onto each new decompile; see the README.
