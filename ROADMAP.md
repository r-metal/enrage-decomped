# Roadmap

Last updated: 2026-10-25

## Status

**Reconstruction (enRAGE 0.9.5.3)**
- [x] Decompiled source builds (`make build`, 0 errors, 0 warnings)
- [x] Same member surface as the official DLL: 10,469 members (`make verify`)
- [x] Same calls, fields and literals per method, apart from 12 documented compiler differences (`tools/asmdiff-baseline.txt`)
- [x] Dead local functions ILSpy drops restored from IL (`Meter`, `Has`)
- [x] Main class (86k lines) split into 150 feature files, as a pure refactor (0 differences)
- [x] Static-initializer order preserved and enforced (`make check`)
- [x] Pipeline reproducible from the DLL (`make decompile` regenerates `src/` byte for byte)
- [x] Builds from a clean clone with public, pinned inputs only (NuGet + the ScriptHookRDR2DotNet 2.2.0 release, checked by SHA-256)
- [x] Docs: README, `docs/ARCHITECTURE.md`, `docs/RECONSTRUCTION.md`
- [x] In-game smoke test of the rebuilt DLL: 5 min on an existing Story save. Startup matched the official DLL stage by stage, with `SELFCHECK ok` and `errors 0`. The F11 menu, a fight, greetings, a save point and a town arrival all worked, the save files kept their schema, and the game quit cleanly (`docs/RECONSTRUCTION.md`)

**Ideas, not scheduled.** Each is a pure refactor you can check with `make verify`:
- split `RosterMenu.cs` (4.8k lines) the same way;
- put `const` names back where the compiler inlined their values (`180000` -> `BandWatchMs`);
- prune unused `using`s in each partial file;
- rename ILSpy's locals (`num4`, `text2`) in the files you work in.

## Decisions and deviations

- File names are chosen, not recovered. Topics and order come from the DLL (docs/RECONSTRUCTION.md).
- Static field initializers moved from ILSpy's synthesized static constructor back onto the fields. The 13 fields that read another during init stay in one file with their source.
- No binaries in the repo. `Newtonsoft.Json` comes from NuGet (13.0.3, byte-identical to enRAGE's copy). `ScriptHookRDRNetAPI.dll` comes from its GitHub release at build time, or from your game folder.
- `Microsoft.NETFramework.ReferenceAssemblies` (NuGet) replaces the Windows-only .NET Framework targeting pack, so it builds on Linux and macOS.
