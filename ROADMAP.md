# Roadmap

Last updated: 2026-10-25 (0.9.5.3-decomped.1 tested in game; releasing)

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

**0.9.5.3-decomped.1: companions and recruits** (design: `docs/decisions/D-001-companions-and-recruits.md`)
- [x] Test harness and `make ci` barrier: unit tests, static-init check, publishing invariants (T-000). xunit on net10 against the built net48 DLL; a WinForms `Keys` shim lets settings types load off Windows. Gate `GATE OK: T-000`, and an adversarial verifier planted an identity hit, binaries, a non-UTC commit, a failing test and a zero-test filter: all fired.
- [x] Version string `0.9.5.3-decomped.1`; assembly version stays `0.9.5.3` (T-001). `GATE OK: T-001`, 3 tests.
- [x] Camp companions back on: the lost per-tick dispatcher is rewritten, with an `[Companions] Enabled` switch (T-010). Gate `GATE OK: T-010`; barrier `ci: OK` (4 tests). Not yet run in game.
- [x] `[Recruits]` settings: `Enabled`, `MaxKept`, `ChancePercent` (T-012). `GATE OK: T-012`, 6 tests including clamps and section layout.
- [x] `RecruitRules.OfferChance`: pure and unit-tested (T-020). `GATE OK: T-020`, 25 cases.
- [x] `WorldState.Recruits` saved in `world.json`; old saves still load (T-021). `GATE OK: T-021`, 6 tests through the mod's real `SafeFile`/`SlicedJson` paths; an old reader ignores the new key.
- [x] Recruit flow: spare a man, he offers, "Ride With Me", he leaves his gang and joins your men; send for him or send him home (T-023). `GATE OK: T-023`, 52 tests. The wave review found the MaxKept cap could be overshot by two open offers; fixed with a re-check at take-on, independently verified. Not yet run in game.
- [x] F11 "Your Men" page (T-024). It is a chip on the Hierarchy faction bar after "Unaffiliated", reached with Q/E like the other chips, not a header tab (see Decisions). `GATE OK: T-024`. The wave review found the Let Him Go confirm survived leaving the page; fixed and verified. Not yet seen in game.
- [x] What's New page, CHANGELOG, README and ARCHITECTURE updated (T-030). `GATE OK: T-030`, barrier `ci: OK` 52 tests. The review caught an overclaim in the What's New text ("the other riders say yes": Uncle is 50/50, Kieran only once free); fixed.
- [x] In-game acceptance test, 2026-10-25, on a copied save slot with the profile and enRAGE folders snapshotted and restored afterwards (verified byte-identical). Passed: spare → roll → offer (questioning path), Recruit Now, Send For Him, Send Home, Let Him Go, riding, fighting, laid up, recruits kept across a mod reload and a game save/load. The test found 6 bugs, all fixed and reviewed (CHANGELOG § Fixed during the in-game test). The code-review fixes made after the test (mounted, surrendered and tied men in companion fights, the tied-offer cap, offers ending at mission start) are not re-tested in game yet.
- [ ] Camp companions ("Ride With Me" at a gang camp) in game: not exercised, because the gang was away on a story mission. Test next time the gang is at camp.
- [x] Pushed `master` + `upstream`, tagged `v0.9.5.3-decomped.1` (2026-10-25)

**Ideas, not scheduled.** Each is a pure refactor you can check with `make verify`:
- split `RosterMenu.cs` (4.8k lines) the same way;
- put `const` names back where the compiler inlined their values (`180000` -> `BandWatchMs`);
- prune unused `using`s in each partial file;
- rename ILSpy's locals (`num4`, `text2`) in the files you work in.

## Decisions and deviations

- D-001 was amended after the in-game test: the offer waits while he's tied, no prompt during missions, companion threats are people only, debug Recruit Now, and cowards are slower to swear revenge. See D-001 § Amendments.

- D-001 (2026-10-25): companions and recruits design, version scheme. `docs/decisions/D-001-companions-and-recruits.md`.
- "Your Men" (T-024) sits on the Hierarchy faction bar (Q/E, like Unaffiliated) rather than as a header tab. A header tab would mean changing the Tab-key cycle, which the task forbade, and touching `MenuTab.cs`/`NemesisMod.Input.cs`. Revisit if the in-game test finds it hard to discover.
- Recruit offers stop a fleeing man: he faces you and says an offer line for the 60 s window (T-023), because a running man can rarely be reached within 3 m. If the offer lapses, he runs as before.
- `make verify` passes only on the untouched reconstruction. From 0.9.5.3-decomped.1 on, it is expected to fail. `make ci` is the gate, and `make diff-upstream` shows what changed.

- File names are chosen, not recovered. Topics and order come from the DLL (docs/RECONSTRUCTION.md).
- Static field initializers moved from ILSpy's synthesized static constructor back onto the fields. The 13 fields that read another during init stay in one file with their source.
- No binaries in the repo. `Newtonsoft.Json` comes from NuGet (13.0.3, byte-identical to enRAGE's copy). `ScriptHookRDRNetAPI.dll` comes from its GitHub release at build time, or from your game folder.
- `Microsoft.NETFramework.ReferenceAssemblies` (NuGet) replaces the Windows-only .NET Framework targeting pack, so it builds on Linux and macOS.
